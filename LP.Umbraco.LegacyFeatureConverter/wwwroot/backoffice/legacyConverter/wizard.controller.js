(function () {
    'use strict';

    angular.module('umbraco')
        .controller('legacyConverter.wizard.controller', LegacyConverterWizardController);

    LegacyConverterWizardController.$inject = ['$scope', '$http', 'notificationsService'];

    /**
     * Wizard controller for starting a new conversion.
     * Step 1: Welcome / introduction
     * Step 2: Document type selection
     * Step 3: Settings and confirmation
     */
    function LegacyConverterWizardController($scope, $http, notificationsService) {
        var vm = this;
        var apiBase = '/umbraco/backoffice/LegacyFeatureConverter/LegacyConverterApi';

        // State
        vm.currentStep = 0;
        vm.steps = ['Welcome', 'Document Types', 'Settings'];
        vm.loadingDocTypes = false;
        vm.submitting = false;
        vm.selectAll = true;
        vm.documentTypes = [];
        vm.settings = {
            stopOnError: false,
            runTestFirst: true,
            isTestRun: false
        };

        // Methods
        vm.nextStep = nextStep;
        vm.prevStep = prevStep;
        vm.toggleSelectAll = toggleSelectAll;
        vm.getSelectedCount = getSelectedCount;
        vm.queueConversion = queueConversion;

        // ===== Implementation =====

        function nextStep() {
            if (vm.currentStep === 0) {
                // Moving to step 2: load document types
                loadDocumentTypes();
            }
            vm.currentStep++;
        }

        function prevStep() {
            if (vm.currentStep > 0) {
                vm.currentStep--;
            }
        }

        function loadDocumentTypes() {
            if (vm.documentTypes.length > 0) return; // Already loaded

            vm.loadingDocTypes = true;
            $http.get(apiBase + '/GetDocumentTypes', {
                params: { converterName: $scope.model.converter.name }
            })
            .then(function (response) {
                vm.documentTypes = response.data.map(function (dt) {
                    dt.selected = true; // Select all by default
                    return dt;
                });
                vm.loadingDocTypes = false;
            })
            .catch(function () {
                notificationsService.error('Legacy Converter', 'Failed to load document types');
                vm.loadingDocTypes = false;
            });
        }

        function toggleSelectAll() {
            vm.documentTypes.forEach(function (dt) {
                dt.selected = vm.selectAll;
            });
        }

        function getSelectedCount() {
            return vm.documentTypes.filter(function (dt) { return dt.selected; }).length;
        }

        function queueConversion() {
            vm.submitting = true;

            var selectedKeys = vm.documentTypes
                .filter(function (dt) { return dt.selected; })
                .map(function (dt) { return dt.key; });

            var request = {
                converterType: $scope.model.converter.name,
                selectedDocumentTypeKeys: selectedKeys.length > 0 ? selectedKeys : null,
                isTestRun: vm.settings.isTestRun,
                stopOnError: vm.settings.stopOnError,
                runTestFirst: vm.settings.runTestFirst
            };

            $http.post(apiBase + '/QueueConversion', request)
                .then(function (response) {
                    notificationsService.success('Legacy Converter',
                        vm.settings.isTestRun
                            ? 'Test conversion queued successfully'
                            : 'Conversion queued successfully');
                    vm.submitting = false;
                    $scope.model.submit($scope.model);
                })
                .catch(function (error) {
                    var message = error.data && error.data.error
                        ? error.data.error
                        : 'Failed to queue conversion';
                    notificationsService.error('Legacy Converter', message);
                    vm.submitting = false;
                });
        }
    }
})();
