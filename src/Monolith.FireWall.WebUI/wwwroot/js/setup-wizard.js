/**
 * Setup Wizard - Standalone Controller
 * Manages the setup wizard flow independently from the main app
 */

// Ensure Monolith namespace exists
if (typeof window.Monolith === 'undefined') {
    window.Monolith = {};
}

// Ensure API client exists (load from core if needed)
if (typeof Monolith.API === 'undefined') {
    console.error('Monolith.API not found. Make sure monolith.api.js is loaded first.');
}

const SetupWizard = {
    currentStep: 0,
    steps: [],
    setupStatus: null,
    isInitialized: false,

    init: function() {
        if (this.isInitialized) return;
        this.isInitialized = true;
        
        console.log('Setup Wizard initializing...');
        this.setupEventHandlers();
        this.loadSetupStatus();
    },

    setupEventHandlers: function() {
        // Skip setup button in header
        $(document).on('click', '#btn-skip-setup-header', () => {
            this.showSkipConfirmation();
        });

        // Navigation buttons
        $(document).on('click', '#btn-setup-back', () => {
            this.previousStep();
        });

        $(document).on('click', '#btn-setup-next', () => {
            this.nextStep();
        });

        $(document).on('click', '#btn-setup-skip', () => {
            this.skipCurrentStep();
        });

        $(document).on('click', '#btn-setup-finish', () => {
            this.finishSetup();
        });
    },

    loadSetupStatus: async function() {
        try {
            const response = await Monolith.API.get('/api/setup/status');
            const statusData = response.Data || response.data;
            const data = statusData || response;
            this.setupStatus = data;
            
            console.log('Setup status loaded:', data);

            await this.buildSteps();
            this.updateProgress();
            this.updateNavigation();
        } catch (err) {
            console.error('Failed to load setup status:', err);
            this.showError('Failed to load setup wizard. Please refresh the page.');
            // Still try to build steps with defaults
            await this.buildSteps();
            this.updateProgress();
        }
    },

    buildSteps: async function() {
        this.steps = [
            {
                id: 'router',
                title: 'Router & System',
                description: 'Configure hostname, timezone, and time synchronization',
                route: '/setup/router',
                required: true,
                component: 'router',
                order: 0
            },
            {
                id: 'network',
                title: 'Network',
                description: 'Configure WAN and LAN interfaces',
                route: '/setup/network',
                required: false,
                component: 'network',
                order: 1
            }
        ];

        // Load package setup steps
        try {
            const response = await Monolith.API.get('/api/setup/packages');
            const packagesData = response.Data || response.data || response;
            const packages = packagesData.packages || packagesData.Packages || [];
            
            if (packages && packages.length > 0) {
                packages.forEach(pkg => {
                    const packageId = pkg.packageId || pkg.PackageId;
                    const setupPages = pkg.setupPages || pkg.SetupPages || [];
                    
                    if (setupPages && setupPages.length > 0) {
                        setupPages.forEach(page => {
                            const pageId = page.id || page.Id;
                            this.steps.push({
                                id: `package:${packageId}:${pageId}`,
                                title: page.title || page.Title,
                                description: page.description || page.Description,
                                route: page.route || page.Route,
                                required: page.isRequired !== undefined ? page.isRequired : 
                                         (page.IsRequired !== undefined ? page.IsRequired : false),
                                component: 'package',
                                packageId: packageId,
                                pageId: pageId,
                                order: page.order !== undefined ? page.order : 
                                      (page.Order !== undefined ? page.Order : 10)
                            });
                        });
                    }
                });
            }
        } catch (err) {
            console.error('Failed to load package setup pages:', err);
        }

        // Sort steps by order
        this.steps.sort((a, b) => (a.order || 999) - (b.order || 999));
        
        // Find current step based on completed steps
        if (this.setupStatus) {
            const completed = this.setupStatus.CompletedSteps || this.setupStatus.completedSteps || [];
            for (let i = 0; i < this.steps.length; i++) {
                if (!completed.includes(this.steps[i].id)) {
                    this.currentStep = i;
                    break;
                }
            }
        }
    },

    updateProgress: function() {
        if (!this.setupStatus || !this.steps.length) return;

        const totalSteps = this.steps.length;
        const completed = this.setupStatus.CompletedSteps || this.setupStatus.completedSteps || [];
        const completedCount = completed.length;
        const progress = totalSteps > 0 ? Math.round((completedCount / totalSteps) * 100) : 0;
        const currentStepNum = this.currentStep + 1;

        // Update progress bar
        $('#setup-progress-fill').css('width', `${progress}%`);
        $('#setup-progress-text').text(`Step ${currentStepNum} of ${totalSteps}`);
    },

    updateNavigation: function() {
        // Back button
        const $backBtn = $('#btn-setup-back, #btn-back');
        if (this.currentStep > 0) {
            $backBtn.prop('disabled', false);
        } else {
            $backBtn.prop('disabled', true);
        }

        // Next/Finish button
        const $nextBtn = $('#btn-setup-next, #btn-next');
        const $finishBtn = $('#btn-setup-finish, #btn-finish');
        
        if (this.currentStep >= this.steps.length - 1) {
            $nextBtn.hide();
            $finishBtn.show();
        } else {
            $nextBtn.show();
            $finishBtn.hide();
        }

        // Skip button (only for optional steps)
        const $skipBtn = $('#btn-setup-skip, #btn-skip');
        const currentStepData = this.steps[this.currentStep];
        if (currentStepData && !currentStepData.required) {
            $skipBtn.show();
        } else {
            $skipBtn.hide();
        }
    },

    nextStep: async function() {
        const currentIndex = this.currentStep;
        const currentStepData = this.steps[currentIndex];
        const nextStepData = this.steps[currentIndex + 1];
        if (!currentStepData) return;

        // Validate current step
        if (typeof window.validateCurrentStep === 'function') {
            const isValid = window.validateCurrentStep();
            if (!isValid) {
                this.showError('Please complete all required fields before continuing.');
                return;
            }
        }

        // Save current step data
        try {
            await this.saveCurrentStep();
        } catch (err) {
            console.error('Failed to save step:', err);
            this.showError('Failed to save step data. Please try again.');
            return;
        }

        // Move to next step
        if (nextStepData) {
            this.navigateToStep(nextStepData);
        }
    },

    previousStep: function() {
        if (this.currentStep > 0) {
            this.currentStep--;
            this.navigateToStep(this.steps[this.currentStep]);
        }
    },

    skipCurrentStep: async function() {
        const currentStepData = this.steps[this.currentStep];
        if (!currentStepData || currentStepData.required) {
            return;
        }

        if (!confirm(`Skip "${currentStepData.title}"? You can configure this later in the dashboard.`)) {
            return;
        }

        try {
            await Monolith.API.post('/api/setup/skip-step', {
                stepId: currentStepData.id
            });

            // Move to next step
            if (this.currentStep < this.steps.length - 1) {
                this.currentStep++;
                this.navigateToStep(this.steps[this.currentStep]);
            } else {
                // Last step, finish setup
                await this.finishSetup();
            }
        } catch (err) {
            console.error('Failed to skip step:', err);
            this.showError('Failed to skip step. Please try again.');
        }
    },

    navigateToStep: function(step) {
        if (!step) return;
        
        // For package steps, use the PackageStep page route
        if (step.component === 'package' && step.packageId && step.pageId) {
            window.location.href = `/setup/package-step/${step.packageId}/${step.pageId}`;
        } else {
            // For core steps, use the route directly
            window.location.href = step.route;
        }
    },

    saveCurrentStep: async function() {
        const currentStepData = this.steps[this.currentStep];
        if (!currentStepData) return;

        // Get step data from page-specific function
        let stepData = {};
        if (typeof window.getCurrentStepData === 'function') {
            stepData = window.getCurrentStepData();
        }

        // Save step completion
        await Monolith.API.post('/api/setup/complete-step', {
            stepId: currentStepData.id,
            data: stepData
        });

        // Reload status to update progress
        await this.loadSetupStatus();
    },

    finishSetup: async function() {
        if (!confirm('Complete setup? You can change these settings later in the dashboard.')) {
            return;
        }

        try {
            await Monolith.API.post('/api/setup/finish', {});
            this.showSuccess('Setup completed successfully! Redirecting to dashboard...');
            
            setTimeout(() => {
                window.location.href = '/';
            }, 1500);
        } catch (err) {
            console.error('Failed to finish setup:', err);
            this.showError('Failed to complete setup. Please try again.');
        }
    },

    showSkipConfirmation: function() {
        if (!confirm('Skip the entire setup wizard? You can configure these settings later in the dashboard.')) {
            return;
        }

        this.skipSetup();
    },

    skipSetup: async function() {
        try {
            await Monolith.API.post('/api/setup/skip', {});
            this.showSuccess('Setup skipped. Redirecting to dashboard...');
            
            setTimeout(() => {
                window.location.href = '/';
            }, 1500);
        } catch (err) {
            console.error('Failed to skip setup:', err);
            this.showError('Failed to skip setup. Please try again.');
        }
    },

    showError: function(message) {
        if (typeof Monolith !== 'undefined' && Monolith.UI && Monolith.UI.showError) {
            Monolith.UI.showError(message);
        } else {
            alert(message);
        }
    },

    showSuccess: function(message) {
        if (typeof Monolith !== 'undefined' && Monolith.UI && Monolith.UI.showSuccess) {
            Monolith.UI.showSuccess(message);
        } else {
            alert(message);
        }
    }
};

// Initialize when DOM is ready
$(document).ready(function() {
    // Only initialize on setup pages
    if (window.location.pathname.startsWith('/setup')) {
        // Wait for API client to be ready
        if (typeof Monolith.API !== 'undefined') {
            SetupWizard.init();
        } else {
            // Retry after a short delay
            setTimeout(() => {
                if (typeof Monolith.API !== 'undefined') {
                    SetupWizard.init();
                } else {
                    console.error('Monolith.API not available after retry');
                }
            }, 500);
        }
    }
});

// Export for global access
window.SetupWizard = SetupWizard;
