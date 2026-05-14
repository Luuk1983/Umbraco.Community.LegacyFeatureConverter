(function () {
    'use strict';

    angular.module('umbraco')
        .controller('legacyConverter.wizard.controller', LegacyConverterWizardController);

    LegacyConverterWizardController.$inject = ['$scope', '$http', 'notificationsService'];

    /**
     * Wizard controller for starting a new conversion.
     * Step 1: Converter selection (with category filter: All / Property editor / Macro)
     * Step 2: Approach selection (Fast, Thorough) — applies to both families with family-specific meaning
     * Step 3: Impact overview — pre-computed plan with counts (doc-types OR macros depending on category)
     * Step 4: Settings and confirmation
     */
    function LegacyConverterWizardController($scope, $http, notificationsService) {
        var vm = this;
        var apiBase = '/umbraco/backoffice/LegacyFeatureConverter/LegacyConverterApi';

        // === State ===
        vm.currentStep = 0;
        vm.steps = ['Converter', 'Approach', 'Impact', 'Settings'];
        vm.converters = $scope.model.converters || [];
        vm.categoryFilter = 'all'; // 'all' | 'Property editor' | 'Macro'
        vm.availableCategories = computeAvailableCategories(vm.converters);
        vm.selectedConverter = null;
        vm.approach = 'Fast';
        vm.plan = null;
        vm.loadingPlan = false;
        vm._lastComputedApproach = null;
        vm.submitting = false;
        // Selection bookkeeping per impact mode.
        vm.impactMode = 'docTypes'; // 'docTypes' | 'macros'
        vm.selectAll = true;
        vm.macros = [];           // populated for macro converters in step 2 (Impact)
        vm.macroSelectAll = true;
        vm.settings = {
            stopOnError: false,
            runTestFirst: true,
            isTestRun: false,
            publishAfterConversion: false,
            generateStubPartialViews: true
        };

        // === Methods ===
        vm.nextStep = nextStep;
        vm.prevStep = prevStep;
        vm.nextDisabled = nextDisabled;
        vm.cancel = cancel;
        vm.selectConverter = selectConverter;
        vm.setCategoryFilter = setCategoryFilter;
        vm.filteredConverters = filteredConverters;
        vm.selectApproach = selectApproach;
        vm.setRunMode = setRunMode;
        vm.toggleSelectAll = toggleSelectAll;
        vm.toggleSelectAllMacros = toggleSelectAllMacros;
        vm.getSelectedDocTypeCount = getSelectedDocTypeCount;
        vm.getSelectedContentCount = getSelectedContentCount;
        vm.getSelectedPropertyCount = getSelectedPropertyCount;
        vm.getSelectedMacroCount = getSelectedMacroCount;
        vm.queueConversion = queueConversion;

        // === Implementation ===

        function cancel() {
            $scope.model.close();
        }

        /**
         * Computes the set of distinct categories present in the converter list so the
         * filter tabs only show options that actually have converters. Always includes
         * the "all" pseudo-category.
         */
        function computeAvailableCategories(converters) {
            var set = {};
            (converters || []).forEach(function (c) {
                if (c.category) {
                    set[c.category] = true;
                }
            });
            return Object.keys(set).sort();
        }

        function setCategoryFilter(category) {
            vm.categoryFilter = category;
            // Clear the selected converter if it no longer matches the filter so the
            // user isn't carrying along a hidden selection.
            if (vm.selectedConverter
                && category !== 'all'
                && vm.selectedConverter.category !== category) {
                vm.selectedConverter = null;
            }
        }

        function filteredConverters() {
            if (vm.categoryFilter === 'all') {
                return vm.converters;
            }
            return vm.converters.filter(function (c) {
                return c.category === vm.categoryFilter;
            });
        }

        function selectConverter(converter) {
            vm.selectedConverter = converter;
            // Drive which Impact table we show in step 2.
            vm.impactMode = (converter && converter.category === 'Macro') ? 'macros' : 'docTypes';
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
            // Step 2 (impact)
            if (vm.currentStep === 2) {
                if (vm.loadingPlan) return true;
                if (!vm.plan) return true;
                if (vm.impactMode === 'macros') {
                    return (!vm.plan.macros || vm.plan.macros.length === 0)
                        || getSelectedMacroCount() === 0;
                }
                return (!vm.plan.documentTypes || vm.plan.documentTypes.length === 0)
                    || getSelectedDocTypeCount() === 0;
            }
            return false;
        }

        function nextStep() {
            if (vm.currentStep === 1) {
                // Moving to impact: compute plan if approach changed or not yet computed
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

                // Pre-select all items so the user can run with one click.
                if (vm.impactMode === 'macros') {
                    if (vm.plan && vm.plan.macros) {
                        vm.plan.macros.forEach(function (m) { m.selected = true; });
                    }
                    vm.macroSelectAll = true;
                } else {
                    if (vm.plan && vm.plan.documentTypes) {
                        vm.plan.documentTypes.forEach(function (dt) { dt.selected = true; });
                    }
                    vm.selectAll = true;
                }
                vm.loadingPlan = false;
            })
            .catch(function () {
                notificationsService.error('Legacy Converter', 'Failed to compute conversion plan');
                vm.loadingPlan = false;
            });
        }

        function toggleSelectAll() {
            if (vm.plan && vm.plan.documentTypes) {
                vm.plan.documentTypes.forEach(function (dt) { dt.selected = vm.selectAll; });
            }
        }

        function toggleSelectAllMacros() {
            if (vm.plan && vm.plan.macros) {
                vm.plan.macros.forEach(function (m) { m.selected = vm.macroSelectAll; });
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

        function getSelectedMacroCount() {
            if (!vm.plan || !vm.plan.macros) return 0;
            return vm.plan.macros.filter(function (m) { return m.selected; }).length;
        }

        function queueConversion() {
            vm.submitting = true;

            var selectedDocTypeKeys = vm.impactMode === 'docTypes' && vm.plan && vm.plan.documentTypes
                ? vm.plan.documentTypes
                    .filter(function (dt) { return dt.selected; })
                    .map(function (dt) { return dt.key; })
                : null;

            var selectedMacroKeys = vm.impactMode === 'macros' && vm.plan && vm.plan.macros
                ? vm.plan.macros
                    .filter(function (m) { return m.selected; })
                    .map(function (m) { return m.key; })
                : null;

            var request = {
                converterType: vm.selectedConverter.name,
                approach: vm.approach,
                plan: vm.plan,
                selectedDocumentTypeKeys: selectedDocTypeKeys && selectedDocTypeKeys.length > 0 ? selectedDocTypeKeys : null,
                selectedMacroKeys: selectedMacroKeys && selectedMacroKeys.length > 0 ? selectedMacroKeys : null,
                isTestRun: vm.settings.isTestRun,
                stopOnError: vm.settings.stopOnError,
                runTestFirst: vm.settings.runTestFirst,
                publishAfterConversion: vm.settings.isTestRun ? false : vm.settings.publishAfterConversion,
                generateStubPartialViews: vm.settings.generateStubPartialViews
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
