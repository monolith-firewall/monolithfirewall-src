/**
 * Setup Wizard - Main Controller
 */

const SetupWizard = {
    currentStep: 0,
    steps: [],
    setupStatus: null,

    init: function() {
        this.loadSetupStatus();
        this.setupEventHandlers();
    },

    loadSetupStatus: async function() {
        try {
            const data = await Monolith.API.get('/setup/status');
            this.setupStatus = data;
            
            if (!data.needsSetup && window.location.pathname.startsWith('/setup')) {
                // Redirect to dashboard if setup not needed
                window.location.href = '/';
                return;
            }

            await this.buildSteps();
            this.renderCurrentStep();
            this.updateProgress();
        } catch (err) {
            console.error('Failed to load setup status:', err);
            Monolith.UI.showError('Failed to load setup wizard. Please refresh the page.');
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
                component: 'router'
            },
            {
                id: 'network',
                title: 'Network',
                description: 'Configure WAN and LAN interfaces',
                route: '/setup/network',
                required: false,
                component: 'network'
            }
        ];

        // Load package setup steps
        try {
            const packagesData = await Monolith.API.get('/api/setup/packages');
            if (packagesData && packagesData.packages) {
                packagesData.packages.forEach(pkg => {
                    pkg.setupPages.forEach(page => {
                        this.steps.push({
                            id: `package:${pkg.packageId}:${page.id}`,
                            title: page.title,
                            description: page.description,
                            route: page.route,
                            required: page.isRequired,
                            component: 'package',
                            packageId: pkg.packageId,
                            pageId: page.id
                        });
                    });
                });
            }
        } catch (err) {
            console.error('Failed to load package setup pages:', err);
        }
    },

    renderCurrentStep: function() {
        if (this.currentStep >= this.steps.length) {
            this.showFinish();
            return;
        }

        const step = this.steps[this.currentStep];
        
        // For router and network steps, redirect to their pages
        // The pages will handle their own navigation
        if (step.component === 'router' || step.component === 'network' || step.component === 'package') {
            if (window.location.pathname !== step.route) {
                window.location.href = step.route;
            }
            return;
        }

        // For other steps, render inline
        const container = $('#setup-steps-container');
        container.html(`
            <div class="step-content">
                <h5>${step.title}</h5>
                <p class="text-muted">${step.description}</p>
            </div>
        `);
    },

    updateProgress: function() {
        const total = this.steps.length;
        const completed = this.setupStatus?.completedSteps?.length || 0;
        const progress = total > 0 ? Math.round((completed / total) * 100) : 0;

        $('#progress-bar').css('width', progress + '%');
        $('#progress-text').text(progress + '%');
    },

    setupEventHandlers: function() {
        $('#btn-next').on('click', () => this.nextStep());
        $('#btn-back').on('click', () => this.prevStep());
        $('#btn-skip').on('click', () => this.skipStep());
        $('#btn-finish').on('click', () => this.finishSetup());
    },

    nextStep: async function() {
        const step = this.steps[this.currentStep];
        
        // Validate current step (check for step-specific validators)
        let validator = null;
        if (step.component === 'router' && window.validateRouterSetup) {
            validator = window.validateRouterSetup;
        } else if (step.component === 'network' && window.validateNetworkSetup) {
            validator = window.validateNetworkSetup;
        } else if (window.validateStep) {
            validator = window.validateStep;
        }

        if (validator && !validator()) {
            return;
        }

        // Save step data (check for step-specific data getters)
        let stepData = {};
        if (step.component === 'router' && window.getRouterSetupData) {
            stepData = window.getRouterSetupData();
            // Also save to system settings
            try {
                await Monolith.API.post('/api/system/settings', stepData);
            } catch (err) {
                console.error('Failed to save router settings:', err);
            }
        } else if (step.component === 'network' && window.getNetworkSetupData) {
            stepData = window.getNetworkSetupData();
            // TODO: Save network configuration
        } else if (window.getStepData) {
            stepData = window.getStepData();
        }

        await this.completeStep(step.id, stepData);

        // Move to next step
        this.currentStep++;
        this.renderCurrentStep();
        this.updateNavigation();
    },

    prevStep: function() {
        if (this.currentStep > 0) {
            this.currentStep--;
            this.renderCurrentStep();
            this.updateNavigation();
        }
    },

    skipStep: async function() {
        const step = this.steps[this.currentStep];
        if (!step.required) {
            await this.completeStep(step.id, { skipped: true });
            this.currentStep++;
            this.renderCurrentStep();
            this.updateNavigation();
        }
    },

    completeStep: async function(stepId, data) {
        try {
            await Monolith.API.post('/api/setup/complete-step', {
                stepId: stepId,
                data: data
            });
            this.setupStatus.completedSteps.push(stepId);
            this.updateProgress();
        } catch (err) {
            console.error('Failed to complete step:', err);
            Monolith.UI.showError('Failed to save step progress');
        }
    },

    finishSetup: async function() {
        try {
            await Monolith.API.post('/api/setup/finish', {
                skipRemaining: false
            });
            
            Monolith.UI.showSuccess('Setup completed successfully!');
            setTimeout(() => {
                window.location.href = '/';
            }, 1500);
        } catch (err) {
            console.error('Failed to finish setup:', err);
            Monolith.UI.showError('Failed to complete setup');
        }
    },

    updateNavigation: function() {
        const step = this.steps[this.currentStep];
        const isLast = this.currentStep >= this.steps.length - 1;

        $('#btn-back').prop('disabled', this.currentStep === 0);
        $('#btn-next').toggle(!isLast);
        $('#btn-finish').toggle(isLast);
        $('#btn-skip').toggle(!isLast && !step.required);
    },

    showFinish: function() {
        $('#setup-steps-container').html(`
            <div class="text-center py-5">
                <i class="bi bi-check-circle text-success" style="font-size: 4rem;"></i>
                <h4 class="mt-3">Setup Complete!</h4>
                <p class="text-muted">Your Monolith FireWall is now configured and ready to use.</p>
            </div>
        `);
        $('#btn-next').hide();
        $('#btn-back').hide();
        $('#btn-skip').hide();
        $('#btn-finish').show();
    }
};

// Initialize when document is ready
$(document).ready(function() {
    // Only initialize if we're on the setup page
    if (window.location.pathname === '/setup' || window.location.pathname.startsWith('/setup/')) {
        SetupWizard.init();
        
        // Setup navigation buttons for individual step pages
        if (window.location.pathname !== '/setup') {
            setupStepPageNavigation();
        }
    }
});

// Setup navigation for individual step pages (router, network, etc.)
function setupStepPageNavigation() {
    // Add navigation buttons if they don't exist
    if ($('#setup-nav-buttons').length === 0) {
        const navHtml = `
            <div id="setup-nav-buttons" class="d-flex justify-content-between mt-4">
                <button type="button" class="btn btn-outline-secondary" id="btn-setup-back">
                    <i class="bi bi-arrow-left me-1"></i> Back
                </button>
                <div>
                    <button type="button" class="btn btn-outline-warning me-2" id="btn-setup-skip" style="display: none;">
                        Skip
                    </button>
                    <button type="button" class="btn btn-primary" id="btn-setup-next">
                        Next <i class="bi bi-arrow-right ms-1"></i>
                    </button>
                </div>
            </div>
        `;
        $('.card-body').append(navHtml);
    }

    $('#btn-setup-next').on('click', () => {
        if (window.SetupWizard) {
            window.SetupWizard.nextStep();
        }
    });

    $('#btn-setup-back').on('click', () => {
        if (window.SetupWizard) {
            window.SetupWizard.prevStep();
        }
    });

    $('#btn-setup-skip').on('click', () => {
        if (window.SetupWizard) {
            window.SetupWizard.skipStep();
        }
    });
}

// Export for individual step pages
window.SetupWizard = SetupWizard;
