(function () {
    'use strict';

    angular.module('umbraco')
        .controller('legacyConverter.wizard.controller', LegacyConverterWizardController);

    LegacyConverterWizardController.$inject = ['$scope', '$http', 'notificationsService'];

    /**
     * Wizard controller for starting a new conversion.
     * Step 1: Welcome / introduction
     * Step 2: Approach selection (DocumentType, Content, Both)
     * Step 3: Impact overview — pre-computed plan with counts
     * Step 4: Settings and confirmation
     */
    function LegacyConverterWizardController($scope, $http, notificationsService) {
        var vm = this;
        var apiBase = '/umbraco/backoffice/Umbraco.Community.LegacyFeatureConverter/LegacyConverterApi';

        // State
        vm.currentStep = 0;
        vm.steps = ['Converter', 'Approach', 'Impact', 'Settings'];
        vm.converters = $scope.model.converters || [];
        vm.selectedConverter = null;
        vm.approach = 'DocumentType';
        vm.plan = null;
        vm.loadingPlan = false;
        vm._lastComputedApproach = null; // tracks which approach the current plan was computed for
        vm.submitting = false;
        vm.selectAll = true;
        vm.settings = {
            stopOnError: false,
            runTestFirst: true,
            isTestRun: false,
            publishAfterConversion: false
        };

        // Methods
        vm.nextStep = nextStep;
        vm.prevStep = prevStep;
        vm.nextDisabled = nextDisabled;
        vm.cancel = cancel;
        vm.selectConverter = selectConverter;
        vm.selectApproach = selectApproach;
        vm.setRunMode = setRunMode;
        vm.toggleSelectAll = toggleSelectAll;
        vm.getSelectedDocTypeCount = getSelectedDocTypeCount;
        vm.getSelectedContentCount = getSelectedContentCount;
        vm.getSelectedPropertyCount = getSelectedPropertyCount;
        vm.queueConversion = queueConversion;

        // ===== Implementation =====

        function cancel() {
            $scope.model.close();
        }

        function selectConverter(converter) {
            vm.selectedConverter = converter;
        }

        function selectApproach(approach) {
            vm.approach = approach;
        }

        function setRunMode(isTestRun) {
            vm.settings.isTestRun = isTestRun;
            if (isTestRun) {
                vm.settings.runTestFirst = false;
                vm.settings.publishAfterConversion = false;
            }
        }

        function nextDisabled() {
            // Step 0 (converter): disable Next until a converter is selected
            if (vm.currentStep === 0) {
                return !vm.selectedConverter;
            }
            // Step 2 (impact): disable Next while plan is loading or plan is empty
            if (vm.currentStep === 2) {
                return vm.loadingPlan || !vm.plan || vm.plan.documentTypes.length === 0 || getSelectedDocTypeCount() === 0;
            }
            return false;
        }

        function nextStep() {
            if (vm.currentStep === 1) {
                // Moving to step 3 (impact): compute plan if approach changed or not yet computed
                if (vm._lastComputedApproach !== vm.approach) {
                    vm.currentStep++;
                    computePlan();
                    return;
                }
            }
            vm.currentStep++;
        }

        function prevStep() {
            if (vm.currentStep > 0) {
                vm.currentStep--;
            }
        }

        function computePlan() {
            vm.loadingPlan = true;
            vm.plan = null;
            vm._lastComputedApproach = vm.approach;

            $http.post(apiBase + '/ComputePlan', {
                converterName: vm.selectedConverter.name,
                approach: vm.approach
            })
            .then(function (response) {
                vm.plan = response.data;
                // All doc types selected by default
                if (vm.plan && vm.plan.documentTypes) {
                    vm.plan.documentTypes.forEach(function (dt) {
                        dt.selected = true;
                    });
                }
                vm.selectAll = true;
                vm.loadingPlan = false;
            })
            .catch(function () {
                notificationsService.error('Legacy Converter', 'Failed to compute conversion plan');
                vm.loadingPlan = false;
            });
        }

        function toggleSelectAll() {
            if (vm.plan && vm.plan.documentTypes) {
                vm.plan.documentTypes.forEach(function (dt) {
                    dt.selected = vm.selectAll;
                });
            }
        }

        function getSelectedDocTypeCount() {
            if (!vm.plan || !vm.plan.documentTypes) return 0;
            return vm.plan.documentTypes.filter(function (dt) { return dt.selected; }).length;
        }

        function getSelectedContentCount() {
            if (!vm.plan || !vm.plan.documentTypes) return 0;
            return vm.plan.documentTypes
                .filter(function (dt) { return dt.selected; })
                .reduce(function (sum, dt) { return sum + (dt.contentNodeCount || 0); }, 0);
        }

        function getSelectedPropertyCount() {
            if (!vm.plan || !vm.plan.documentTypes) return 0;
            return vm.plan.documentTypes
                .filter(function (dt) { return dt.selected; })
                .reduce(function (sum, dt) { return sum + (dt.propertyCount || 0); }, 0);
        }

        function queueConversion() {
            vm.submitting = true;

            var selectedKeys = vm.plan && vm.plan.documentTypes
                ? vm.plan.documentTypes
                    .filter(function (dt) { return dt.selected; })
                    .map(function (dt) { return dt.key; })
                : null;

            var request = {
                converterType: vm.selectedConverter.name,
                approach: vm.approach,
                plan: vm.plan,
                selectedDocumentTypeKeys: selectedKeys && selectedKeys.length > 0 ? selectedKeys : null,
                isTestRun: vm.settings.isTestRun,
                stopOnError: vm.settings.stopOnError,
                runTestFirst: vm.settings.runTestFirst,
                publishAfterConversion: vm.settings.isTestRun ? false : vm.settings.publishAfterConversion
            };

            $http.post(apiBase + '/QueueConversion', request)
                .then(function () {
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
