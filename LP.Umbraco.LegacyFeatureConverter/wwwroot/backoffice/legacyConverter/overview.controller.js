(function () {
    'use strict';

    angular.module('umbraco')
        .controller('legacyConverter.overview.controller', LegacyConverterOverviewController);

    LegacyConverterOverviewController.$inject = [
        '$scope', '$http', '$location', '$interval', 'notificationsService', 'editorService'
    ];

    /**
     * Main overview controller for the Legacy Feature Converter.
     * Shows available converters, active queue, and conversion history.
     */
    function LegacyConverterOverviewController($scope, $http, $location, $interval, notificationsService, editorService) {
        var vm = this;
        var apiBase = '/umbraco/backoffice/LegacyFeatureConverter/LegacyConverterApi';
        var pollInterval;

        // State
        vm.loading = true;
        vm.headerDescription = '';
        vm.converters = [];
        vm.queue = [];
        vm.history = { items: [], pageNumber: 1, pageSize: 10, totalPages: 0, totalItems: 0 };

        // Methods
        vm.startWizard = startWizard;
        vm.viewDetails = viewDetails;
        vm.cancelQueueItem = cancelQueueItem;
        vm.getStatusClass = getStatusClass;
        vm.getConverterType = getConverterType;
        vm.nextPage = function (pageNumber) { loadHistory(pageNumber); };
        vm.prevPage = function (pageNumber) { loadHistory(pageNumber); };
        vm.goToPage = function (pageNumber) { loadHistory(pageNumber); };

        // Initialize
        init();

        // Cleanup polling on scope destroy
        $scope.$on('$destroy', function () {
            if (pollInterval) $interval.cancel(pollInterval);
        });

        // ===== Implementation =====

        function init() {
            vm.loading = true;
            loadConverters();
            loadQueue();
            loadHistory(1);

            // Poll queue status every 5 seconds for real-time updates
            pollInterval = $interval(function () {
                loadQueue();
            }, 5000);
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

        function startWizard(converter) {
            editorService.open({
                title: converter.name,
                size: 'medium',
                view: '/App_Plugins/LegacyFeatureConverter/backoffice/legacyConverter/wizard.html',
                converter: converter,
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
            $location.path('/settings/legacyConverter/details/' + conversionId);
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
                return options.converterType || 'Unknown';
            } catch (e) {
                return 'Unknown';
            }
        }
    }
})();
