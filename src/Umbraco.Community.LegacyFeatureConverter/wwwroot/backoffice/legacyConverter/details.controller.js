(function () {
    'use strict';

    angular.module('umbraco')
        .controller('legacyConverter.details.controller', LegacyConverterDetailsController);

    LegacyConverterDetailsController.$inject = ['$scope', '$http', 'notificationsService'];

    /**
     * Details controller showing a single conversion's summary and log entries.
     * Opened as a modal via editorService — reads conversionId from $scope.model.
     */
    function LegacyConverterDetailsController($scope, $http, notificationsService) {
        var vm = this;
        var apiBase = '/umbraco/backoffice/LegacyFeatureConverter/LegacyConverterApi';

        // State
        vm.loading = true;
        vm.title = 'Conversion Details';
        vm.history = null;
        vm.logs = [];
        vm.summary = null;            // Parsed history.summary JSON
        vm.failures = [];             // Flattened list of !Success && !Skipped items
        vm.skipped = [];              // Flattened list of Skipped items (with reason)
        vm.skippedExpanded = false;   // Skipped panel is collapsed by default — counts can be large
        vm.logFilter = 'all';         // Default; switched to 'errors' when failures > 0

        // Methods
        vm.close = close;
        vm.getStatusClass = getStatusClass;
        vm.getLogLevelClass = getLogLevelClass;
        vm.setLogFilter = setLogFilter;
        vm.filteredLogs = filteredLogs;
        vm.toggleSkipped = toggleSkipped;

        // Initialize
        init();

        // ===== Implementation =====

        function init() {
            var conversionId = $scope.model.conversionId;
            if (!conversionId) {
                vm.loading = false;
                return;
            }

            $http.get(apiBase + '/GetConversionDetails', {
                params: { id: conversionId }
            })
            .then(function (response) {
                vm.history = response.data.history;
                vm.logs = response.data.logs || [];
                vm.title = (vm.history.converterType || 'Conversion') + ' - ' +
                    new Date(vm.history.startedAt).toLocaleString();

                // The per-item Success/Skipped/ErrorMessage data lives in the serialized
                // ConversionHistory.Summary JSON. Parse it so the Failures panel can show
                // each failing item with its message — independent of ConversionLogs.
                vm.summary = parseSummary(vm.history.summary);
                vm.failures = buildFailures(vm.summary);
                vm.skipped = buildSkipped(vm.summary);

                // Default to "Errors only" when this run actually has failures so the
                // user lands on what matters; otherwise show everything.
                vm.logFilter = (vm.history.failureCount > 0) ? 'errors' : 'all';

                vm.loading = false;
            })
            .catch(function (error) {
                if (error.status === 404) {
                    vm.history = null;
                } else {
                    notificationsService.error('Legacy Converter', 'Failed to load conversion details');
                }
                vm.loading = false;
            });
        }

        function close() {
            $scope.model.close();
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

        function getLogLevelClass(level) {
            switch (level) {
                case 'Error': return 'lfc-status--danger';
                case 'Warning': return 'lfc-status--warning';
                case 'Information': return 'lfc-status--info';
                default: return '';
            }
        }

        function setLogFilter(level) {
            vm.logFilter = level;
        }

        function filteredLogs() {
            if (!vm.logs || vm.logs.length === 0) return [];
            switch (vm.logFilter) {
                case 'errors':
                    return vm.logs.filter(function (l) { return l.level === 'Error'; });
                case 'warnings':
                    return vm.logs.filter(function (l) { return l.level === 'Warning' || l.level === 'Error'; });
                case 'info':
                    return vm.logs.filter(function (l) { return l.level === 'Information'; });
                default:
                    return vm.logs;
            }
        }

        function parseSummary(raw) {
            if (!raw) return null;
            try { return (typeof raw === 'string') ? JSON.parse(raw) : raw; }
            catch (e) { return null; }
        }

        // Tolerant key lookup: new runs use camelCase (matches the API's PropertyNamingPolicy),
        // but historical runs in the same database have a PascalCase summary string because
        // the serializer wasn't casing-aware before this fix. Try both so old details pages
        // still render (with whatever fields they captured at the time).
        function pick(obj, camelKey) {
            if (!obj) return undefined;
            if (obj[camelKey] !== undefined) return obj[camelKey];
            var pascalKey = camelKey.charAt(0).toUpperCase() + camelKey.slice(1);
            return obj[pascalKey];
        }

        function buildFailures(summary) {
            if (!summary) return [];
            var out = [];
            var sections = [
                { key: 'documentTypes', label: 'Document Type' },
                { key: 'dataTypes',     label: 'Data Type' },
                { key: 'contentNodes',  label: 'Content' }
            ];
            sections.forEach(function (section) {
                var items = pick(summary, section.key);
                if (!items || !items.length) return;
                items.forEach(function (item) {
                    if (pick(item, 'success') || pick(item, 'skipped')) return;
                    out.push({
                        type: section.label,
                        name: pick(item, 'name') || '(unnamed)',
                        key: pick(item, 'key') || null,
                        errorMessage: pick(item, 'errorMessage') || '(no message recorded)'
                    });
                });
            });
            return out;
        }

        // Mirrors buildFailures but for items the converter intentionally left alone.
        // The reason for the skip lives on item.message (e.g. "No properties to convert",
        // "Data type already exists", "Schema already on target editor; no content to process").
        // We want this surfaced because a large skip count without explanation is opaque —
        // the user can't tell whether the skips were correct or hiding something.
        function buildSkipped(summary) {
            if (!summary) return [];
            var out = [];
            var sections = [
                { key: 'documentTypes', label: 'Document Type' },
                { key: 'dataTypes',     label: 'Data Type' },
                { key: 'contentNodes',  label: 'Content' }
            ];
            sections.forEach(function (section) {
                var items = pick(summary, section.key);
                if (!items || !items.length) return;
                items.forEach(function (item) {
                    if (!pick(item, 'skipped')) return;
                    out.push({
                        type: section.label,
                        name: pick(item, 'name') || '(unnamed)',
                        key: pick(item, 'key') || null,
                        message: pick(item, 'message') || '(no reason recorded)'
                    });
                });
            });
            return out;
        }

        function toggleSkipped() {
            vm.skippedExpanded = !vm.skippedExpanded;
        }
    }
})();
