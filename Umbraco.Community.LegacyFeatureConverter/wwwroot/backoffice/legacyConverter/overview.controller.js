(function () {
    'use strict';

    angular.module('umbraco')
        .controller('legacyConverter.overview.controller', LegacyConverterOverviewController);

    LegacyConverterOverviewController.$inject = [
        '$scope', '$http', '$interval', 'notificationsService', 'editorService', 'assetsService'
    ];

    /**
     * Main overview controller for the Legacy Feature Converter.
     * Shows available converters, active queue, and conversion history.
     * Uses SignalR for real-time progress updates with polling as fallback.
     */
    function LegacyConverterOverviewController($scope, $http, $interval, notificationsService, editorService, assetsService) {
        var vm = this;
        var apiBase = '/umbraco/backoffice/LegacyFeatureConverter/LegacyConverterApi';
        var pollInterval;
        var hubConnection;

        // State
        vm.loading = true;
        vm.converters = [];
        vm.queue = [];
        vm.history = { items: [], pageNumber: 1, pageSize: 10, totalPages: 0, totalItems: 0 };
        vm.progressMap = {}; // Keyed by queueItemId

        // Internal tracking for auto-refresh
        var _queueHadActiveItems = false;

        // Methods
        vm.startNewConversion = startNewConversion;
        vm.viewDetails = viewDetails;
        vm.cancelQueueItem = cancelQueueItem;
        vm.getStatusClass = getStatusClass;
        vm.getConverterType = getConverterType;
        vm.getProgress = getProgress;
        vm.getOverallPercent = getOverallPercent;
        vm.nextPage = function (pageNumber) { loadHistory(pageNumber); };
        vm.prevPage = function (pageNumber) { loadHistory(pageNumber); };
        vm.goToPage = function (pageNumber) { loadHistory(pageNumber); };

        // Initialize
        init();

        // Cleanup on scope destroy
        $scope.$on('$destroy', function () {
            if (pollInterval) $interval.cancel(pollInterval);
            if (hubConnection) {
                hubConnection.stop();
            }
        });

        // ===== Implementation =====

        function init() {
            vm.loading = true;
            loadConverters();
            loadQueue();
            loadHistory(1);

            // Poll queue status every 5 seconds as fallback
            pollInterval = $interval(function () {
                loadQueue();
            }, 5000);

            // Set up real-time progress via SignalR
            initSignalR();
        }

        function initSignalR() {
            var signalRScript = Umbraco.Sys.ServerVariables.umbracoSettings.umbracoPath
                + '/lib/signalr/signalr.min.js';

            assetsService.loadJs(signalRScript).then(function () {
                var hubUrl = Umbraco.Sys.ServerVariables.umbracoSettings.umbracoPath
                    + '/LegacyFeatureConverter/ConversionHub';

                hubConnection = new signalR.HubConnectionBuilder()
                    .withUrl(hubUrl)
                    .withAutomaticReconnect()
                    .build();

                hubConnection.on('ReceiveProgress', function (progress) {
                    $scope.$apply(function () {
                        vm.progressMap[progress.queueItemId] = progress;
                    });
                });

                hubConnection.on('ConversionCompleted', function (queueItemId, status, conversionHistoryId) {
                    $scope.$apply(function () {
                        delete vm.progressMap[queueItemId];
                        loadQueue();
                        loadHistory(1);
                    });
                });

                hubConnection.start().catch(function (err) {
                    console.warn('Legacy Converter: SignalR connection failed, relying on polling', err);
                });
            });
        }

        function loadConverters() {
            $http.get(apiBase + '/GetConverters')
                .then(function (response) {
                    vm.converters = response.data;
                    vm.loading = false;
                })
                .catch(function (error) {
                    notificationsService.error('Legacy Converter', 'Failed to load converters');
                    vm.loading = false;
                });
        }

        function loadQueue() {
            $http.get(apiBase + '/GetQueueStatus')
                .then(function (response) {
                    vm.queue = response.data;

                    // Auto-refresh history when all active conversions have completed
                    var hasActiveItems = vm.queue.length > 0;
                    if (_queueHadActiveItems && !hasActiveItems) {
                        loadHistory(1);
                    }
                    _queueHadActiveItems = hasActiveItems;
                })
                .catch(function () {
                    // Silent fail for background polling
                });
        }

        function loadHistory(page) {
            $http.get(apiBase + '/GetHistory', {
                params: { page: page, pageSize: vm.history.pageSize }
            })
            .then(function (response) {
                vm.history = response.data;
            })
            .catch(function () {
                notificationsService.error('Legacy Converter', 'Failed to load history');
            });
        }

        function startNewConversion() {
            editorService.open({
                title: 'Start a new conversion',
                size: 'medium',
                view: '/App_Plugins/LegacyFeatureConverter/backoffice/legacyConverter/wizard.html',
                converters: vm.converters,
                submit: function (model) {
                    editorService.close();
                    loadQueue();
                    loadHistory(1);
                },
                close: function () {
                    editorService.close();
                }
            });
        }

        function viewDetails(conversionId) {
            editorService.open({
                title: 'Conversion details',
                size: 'large',
                view: '/App_Plugins/LegacyFeatureConverter/backoffice/legacyConverter/details.html',
                conversionId: conversionId,
                close: function () {
                    editorService.close();
                }
            });
        }

        function cancelQueueItem(id) {
            $http.delete(apiBase + '/CancelConversion', { params: { id: id } })
                .then(function () {
                    notificationsService.success('Legacy Converter', 'Conversion cancelled');
                    loadQueue();
                })
                .catch(function () {
                    notificationsService.error('Legacy Converter', 'Failed to cancel conversion');
                });
        }

        function getStatusClass(status) {
            switch (status) {
                case 'Completed': return 'lfc-status--success';
                case 'CompletedWithErrors': return 'lfc-status--warning';
                case 'Failed': return 'lfc-status--danger';
                case 'Running': return 'lfc-status--info';
                case 'Cancelled': return 'lfc-status--muted';
                default: return '';
            }
        }

        function getConverterType(queueItem) {
            try {
                var options = JSON.parse(queueItem.serializedOptions);
                return options.ConverterType || options.converterType || 'Unknown';
            } catch (e) {
                return 'Unknown';
            }
        }

        /**
         * Gets the current progress for a queue item, if available.
         */
        function getProgress(item) {
            return vm.progressMap[item.id] || null;
        }

        /**
         * Computes a weighted overall percentage across all 4 conversion phases.
         * Phase weights reflect typical relative durations:
         *   Determining document types: 5%
         *   Creating data types: 15%
         *   Updating document types: 15%
         *   Converting content: 65%
         */
        function getOverallPercent(progress) {
            if (!progress) return 0;

            var phaseWeights = {
                'Determining document types': { base: 0, weight: 5 },
                'Creating data types': { base: 5, weight: 15 },
                'Updating document types': { base: 20, weight: 15 },
                'Converting content': { base: 35, weight: 65 }
            };

            var phaseInfo = phaseWeights[progress.phase];
            if (!phaseInfo) return progress.percentComplete;

            return Math.round(phaseInfo.base + (phaseInfo.weight * progress.percentComplete / 100));
        }
    }
})();
