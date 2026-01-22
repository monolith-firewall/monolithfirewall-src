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
            const response = await Monolith.API.get('/api/setup/status');
            // Core API returns { Success: true, Data: {...}, Error: null }
            // Extract the actual status data from Data property
            const statusData = response.Data || response.data;
            const data = statusData || response;
            this.setupStatus = data;
            
            // Check if setup is needed (default to true if property is missing)
            // Core API uses PascalCase: NeedsSetup
            const needsSetup = data.NeedsSetup !== undefined ? data.NeedsSetup : 
                              (data.needsSetup !== undefined ? data.needsSetup : true);
            
            // Allow forcing access with ?force=true query parameter
            const forceAccess = new URLSearchParams(window.location.search).get('force') === 'true';
            
            console.log('Setup status:', { needsSetup, forceAccess, data });
            
            // Only redirect if setup is explicitly NOT needed AND user didn't force access
            if (needsSetup === false && !forceAccess && window.location.pathname.startsWith('/setup')) {
                // Redirect to dashboard if setup not needed (unless forced)
                console.log('Setup not needed, redirecting to dashboard');
                window.location.href = '/';
                return;
            }

            await this.buildSteps();
            this.renderCurrentStep();
            this.updateProgress();
            this.updateNavigation();
        } catch (err) {
            console.error('Failed to load setup status:', err);
            // Don't redirect on error - show the setup page with error message
            // This allows users to see what went wrong
            if (typeof Monolith !== 'undefined' && Monolith.UI) {
                Monolith.UI.showError('Failed to load setup wizard. Please refresh the page.');
            } else {
                alert('Failed to load setup wizard. Please refresh the page.');
            }
            // Still try to render the setup page even if status failed
            await this.buildSteps();
            this.renderCurrentStep();
            this.updateProgress();
            this.updateNavigation();
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
            const packagesData = response.data || response.Data || response;
            const packages = packagesData.packages || packagesData.Packages || [];
            
            if (packages.length > 0) {
                packages.forEach(pkg => {
                    const packageId = pkg.packageId || pkg.PackageId;
                    const setupPages = pkg.setupPages || pkg.SetupPages || [];
                    
                    setupPages.forEach(page => {
                        const pageId = page.id || page.Id;
                        this.steps.push({
                            id: `package:${packageId}:${pageId}`,
                            title: page.title || page.Title,
                            description: page.description || page.Description,
                            route: page.route || page.Route,
                            required: page.isRequired !== undefined ? page.isRequired : (page.IsRequired !== undefined ? page.IsRequired : false),
                            component: 'package',
                            packageId: packageId,
                            pageId: pageId,
                            order: page.order !== undefined ? page.order : (page.Order !== undefined ? page.Order : 10)
                        });
                    });
                });
            }
        } catch (err) {
            console.error('Failed to load package setup pages:', err);
        }

        // Sort steps by order
        this.steps.sort((a, b) => (a.order || 999) - (b.order || 999));
        
        // Find current step index based on completed steps
        if (this.setupStatus && this.setupStatus.completedSteps) {
            const completed = this.setupStatus.completedSteps;
            for (let i = 0; i < this.steps.length; i++) {
                if (!completed.includes(this.steps[i].id)) {
                    this.currentStep = i;
                    break;
                }
            }
        }
    },

    renderCurrentStep: function() {
        if (this.currentStep >= this.steps.length) {
            this.showFinish();
            return;
        }

        const step = this.steps[this.currentStep];
        
        // For router, network, and package steps, redirect to their pages
        // The pages will handle their own navigation
        if (step.component === 'router' || step.component === 'network' || step.component === 'package') {
            const targetRoute = (step.component === 'package' && step.packageId && step.pageId)
                ? `/setup/package-step/${step.packageId}/${step.pageId}`
                : step.route;
            if (window.location.pathname !== targetRoute) {
                window.location.href = targetRoute;
            } else {
                // Already on the correct page, just update navigation
                this.updateNavigation();
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
        this.updateNavigation();
    },

    updateProgress: function() {
        const total = this.steps.length;
        const completedSteps = this.setupStatus?.completedSteps || this.setupStatus?.CompletedSteps || [];
        const completed = Array.isArray(completedSteps) ? completedSteps.length : 0;
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
        } else if (step.component === 'package' && window.validatePackageStep) {
            validator = window.validatePackageStep;
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
                const response = await Monolith.API.post('/api/system/settings', stepData);
                if (!response.success && !response.Success) {
                    throw new Error(response.error || response.Error || 'Failed to save settings');
                }
            } catch (err) {
                console.error('Failed to save router settings:', err);
                if (typeof Monolith !== 'undefined' && Monolith.UI) {
                    Monolith.UI.showError('Failed to save system settings: ' + (err.message || err));
                } else {
                    alert('Failed to save system settings: ' + (err.message || err));
                }
                return;
            }
        } else if (step.component === 'network' && window.getNetworkSetupData) {
            stepData = window.getNetworkSetupData();
            // Save network configuration
            try {
                await this.saveNetworkConfiguration(stepData);
            } catch (err) {
                console.error('Failed to save network configuration:', err);
                if (typeof Monolith !== 'undefined' && Monolith.UI) {
                    Monolith.UI.showError('Failed to save network configuration: ' + (err.message || err));
                } else {
                    alert('Failed to save network configuration: ' + (err.message || err));
                }
                return;
            }
        } else if (step.component === 'package' && window.getPackageStepData) {
            stepData = window.getPackageStepData();
            // Save package-specific configuration
            try {
                await this.savePackageConfiguration(step.packageId, step.pageId, stepData);
            } catch (err) {
                console.error('Failed to save package configuration:', err);
                if (typeof Monolith !== 'undefined' && Monolith.UI) {
                    Monolith.UI.showError('Failed to save package configuration: ' + (err.message || err));
                } else {
                    alert('Failed to save package configuration: ' + (err.message || err));
                }
                return;
            }
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
            const response = await Monolith.API.post('/api/setup/complete-step', {
                stepId: stepId,
                data: data
            });
            if (response.success || response.Success) {
                if (!this.setupStatus.completedSteps) {
                    this.setupStatus.completedSteps = [];
                }
                if (!this.setupStatus.completedSteps.includes(stepId)) {
                    this.setupStatus.completedSteps.push(stepId);
                }
                this.updateProgress();
            } else {
                throw new Error(response.error || response.Error || 'Failed to complete step');
            }
        } catch (err) {
            console.error('Failed to complete step:', err);
            if (typeof Monolith !== 'undefined' && Monolith.UI) {
                Monolith.UI.showError('Failed to save step progress: ' + (err.message || err));
            } else {
                alert('Failed to save step progress');
            }
            throw err;
        }
    },

    finishSetup: async function() {
        try {
            const response = await Monolith.API.post('/api/setup/finish', {
                skipRemaining: false
            });
            
            if (response.success || response.Success) {
                if (typeof Monolith !== 'undefined' && Monolith.UI) {
                    Monolith.UI.showSuccess('Setup completed successfully!');
                } else {
                    alert('Setup completed successfully!');
                }
                setTimeout(() => {
                    window.location.href = '/';
                }, 1500);
            } else {
                throw new Error(response.error || response.Error || 'Failed to finish setup');
            }
        } catch (err) {
            console.error('Failed to finish setup:', err);
            if (typeof Monolith !== 'undefined' && Monolith.UI) {
                Monolith.UI.showError('Failed to complete setup: ' + (err.message || err));
            } else {
                alert('Failed to complete setup');
            }
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

    saveNetworkConfiguration: async function(networkData) {
        // Save interface assignments (WAN/LAN roles)
        if (networkData.wanInterface) {
            try {
                const response = await Monolith.API.post('/api/interfaces/assignments', {
                    interface: networkData.wanInterface,
                    type: 'physical',
                    role: 'wan',
                    ipMode: 'dhcp' // WAN typically uses DHCP
                });
                if (!response.success && !response.Success) {
                    throw new Error(response.error || response.Error || 'Failed to assign WAN interface');
                }
            } catch (err) {
                console.error('Failed to assign WAN interface:', err);
                throw new Error('Failed to assign WAN interface: ' + (err.message || err));
            }
        }

        if (networkData.lanInterface) {
            try {
                // Determine IP mode and address
                const ipMode = networkData.lanConfig === 'static' ? 'static' : 'dhcp';
                const addressCidr = networkData.lanConfig === 'static' && networkData.lanIp
                    ? `${networkData.lanIp}/${this.netmaskToCidr(networkData.lanNetmask || '255.255.255.0')}`
                    : null;

                const response = await Monolith.API.post('/api/interfaces/assignments', {
                    interface: networkData.lanInterface,
                    type: 'physical',
                    role: 'lan',
                    ipMode: ipMode,
                    addressCidr: addressCidr
                });
                if (!response.success && !response.Success) {
                    throw new Error(response.error || response.Error || 'Failed to assign LAN interface');
                }
            } catch (err) {
                console.error('Failed to assign LAN interface:', err);
                throw new Error('Failed to assign LAN interface: ' + (err.message || err));
            }
        }

        // Save gateway (routing) - use gateways endpoint
        if (networkData.gateway) {
            try {
                const response = await Monolith.API.post('/api/routing/gateways', {
                    name: 'default',
                    address: networkData.gateway,
                    interface: networkData.wanInterface || null,
                    isDefault: true,
                    metric: 1
                });
                if (!response.success && !response.Success) {
                    console.warn('Failed to save gateway:', response.error || response.Error);
                }
            } catch (err) {
                console.error('Failed to save gateway:', err);
                // Don't throw - gateway can be configured later
            }
        }

        // Save DNS servers to system settings
        if (networkData.dnsServers && networkData.dnsServers.length > 0) {
            try {
                const response = await Monolith.API.post('/api/system/settings', {
                    dnsServers: networkData.dnsServers
                });
                if (!response.success && !response.Success) {
                    console.warn('Failed to save DNS servers:', response.error || response.Error);
                }
            } catch (err) {
                console.error('Failed to save DNS servers:', err);
                // Don't throw - DNS can be configured later
            }
        }
    },

    netmaskToCidr: function(netmask) {
        // Convert netmask (e.g., 255.255.255.0) to CIDR (e.g., 24)
        const parts = netmask.split('.');
        if (parts.length !== 4) return 24; // Default
        
        let cidr = 0;
        for (let i = 0; i < 4; i++) {
            const octet = parseInt(parts[i], 10);
            cidr += (octet >>> 0).toString(2).split('1').length - 1;
        }
        return cidr || 24; // Default to /24 if calculation fails
    },

    savePackageConfiguration: async function(packageId, pageId, data) {
        // Save package-specific configuration via generic module API endpoints
        try {
            let response;
            
            // Use generic package module API endpoint
            // The package/module will handle the specific configuration format
            response = await Monolith.API.post(`/api/packages/${packageId}/modules/${pageId}/update-settings`, data);
            
            // If the generic endpoint doesn't work, try the setup-specific endpoint as fallback
            if (!response || response.error || (response.success === false)) {
                response = await Monolith.API.post(`/api/setup/package/${packageId}/${pageId}`, data);
            }
            
            if (!response.success && !response.Success) {
                throw new Error(response.error || response.Error || 'Failed to save package configuration');
            }
        } catch (err) {
            // If the endpoint doesn't exist, that's OK - the package might handle it differently
            console.warn('Package setup endpoint not available, configuration may be saved elsewhere:', err);
            // Don't throw - allow the step to complete (user can configure later)
        }
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
            // Wait a bit for SetupWizard to initialize
            setTimeout(() => {
                setupStepPageNavigation();
            }, 100);
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

    // Remove existing handlers to prevent duplicates
    $('#btn-setup-next').off('click');
    $('#btn-setup-back').off('click');
    $('#btn-setup-skip').off('click');

    $('#btn-setup-next').on('click', async () => {
        if (window.SetupWizard) {
            await window.SetupWizard.nextStep();
        }
    });

    $('#btn-setup-back').on('click', () => {
        if (window.SetupWizard) {
            window.SetupWizard.prevStep();
        }
    });

    $('#btn-setup-skip').on('click', async () => {
        if (window.SetupWizard) {
            await window.SetupWizard.skipStep();
        }
    });

    // Update button states based on current step
    if (window.SetupWizard && window.SetupWizard.steps) {
        const currentStep = window.SetupWizard.currentStep;
        const step = window.SetupWizard.steps[currentStep];
        if (step) {
            $('#btn-setup-back').prop('disabled', currentStep === 0);
            $('#btn-setup-skip').toggle(!step.required && currentStep < window.SetupWizard.steps.length - 1);
        }
    }
}

// Export for individual step pages
window.SetupWizard = SetupWizard;
