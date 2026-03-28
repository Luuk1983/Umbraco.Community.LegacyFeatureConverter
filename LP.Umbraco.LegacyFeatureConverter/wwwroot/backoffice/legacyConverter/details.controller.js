(function () {
    'use strict';

    angular.module('umbraco')
        .controller('legacyConverter.details.controller', LegacyConverterDetailsController);

    LegacyConverterDetailsController.$inject = ['$scope', '$http', '$routeParams', '$location', 'notificationsService'];

    /**
     * Details controller showing a single conversion's summary and log entries.
     */
    function LegacyConverterDetailsController($scope, $http, $routeParams, $location, notificationsService) {
        var vm = this;
        var apiBase = '/umbraco/backoffice/LegacyFeatureConverter/LegacyConverterApi';

        // State
        vm.loading = true;
        vm.title = 'Conversion Details';
        vm.history = null;
        vm.logs = [];

        // Methods
        vm.goBack = goBack;
        vm.getStatusClass = getStatusClass;
        vm.getLogLevelClass = getLogLevelClass;

        // Initialize
        init();

        // ===== Implementation =====

        function init() {
            var conversionId = $routeParams.id;
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

        function goBack() {
            $location.path('/settings/legacyConverter/overview');
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
    }
})();
