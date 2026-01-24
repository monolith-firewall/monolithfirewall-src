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

        // Navigation buttons - use off() first to prevent duplicate handlers
        $(document).off('click', '#btn-setup-back').on('click', '#btn-setup-back', () => {
            this.previousStep();
        });

        $(document).off('click', '#btn-setup-next').on('click', '#btn-setup-next', async () => {
            await this.nextStep();
        });

        $(document).off('click', '#btn-setup-skip').on('click', '#btn-setup-skip', async () => {
            await this.skipCurrentStep();
        });

        $(document).off('click', '#btn-setup-finish').on('click', '#btn-setup-finish', async () => {
            await this.finishSetup();
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
        const $backBtn = $('#btn-setup-back');
        if (this.currentStep > 0) {
            $backBtn.prop('disabled', false).show();
        } else {
            $backBtn.prop('disabled', true).show(); // Always show, just disabled
        }

        // Next/Finish button
        const $nextBtn = $('#btn-setup-next');
        const $finishBtn = $('#btn-setup-finish');
        
        if (this.currentStep >= this.steps.length - 1) {
            $nextBtn.hide();
            $finishBtn.show();
        } else {
            $nextBtn.show();
            $finishBtn.hide();
        }

        // Skip button (only for optional steps, and not on last step)
        const $skipBtn = $('#btn-setup-skip');
        const currentStepData = this.steps[this.currentStep];
        if (currentStepData && !currentStepData.required && this.currentStep < this.steps.length - 1) {
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
        let validator = null;
        if (currentStepData.component === 'router' && typeof window.validateRouterSetup === 'function') {
            validator = window.validateRouterSetup;
        } else if (currentStepData.component === 'network' && typeof window.validateNetworkSetup === 'function') {
            validator = window.validateNetworkSetup;
        } else if (currentStepData.component === 'package' && typeof window.validatePackageStep === 'function') {
            validator = window.validatePackageStep;
        } else if (typeof window.validateCurrentStep === 'function') {
            validator = window.validateCurrentStep;
        }

        if (validator && !validator()) {
            this.showError('Please complete all required fields before continuing.');
            return;
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
            this.currentStep++;
            this.navigateToStep(nextStepData);
        } else {
            // Last step, finish setup
            await this.finishSetup();
        }
    },

    previousStep: function() {
        if (this.currentStep > 0) {
            this.currentStep--;
            const prevStep = this.steps[this.currentStep];
            if (prevStep) {
                this.navigateToStep(prevStep);
            }
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
            await Monolith.API.post('/api/setup/complete-step', {
                stepId: currentStepData.id,
                data: { skipped: true }
            });

            // Reload status to update progress
            await this.loadSetupStatus();

            // Move to next step
            if (this.currentStep < this.steps.length - 1) {
                this.currentStep++;
                const nextStep = this.steps[this.currentStep];
                if (nextStep) {
                    this.navigateToStep(nextStep);
                }
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
        
        // Update navigation before navigating
        this.updateNavigation();
        
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

        // Save step-specific configuration before completing step
        if (currentStepData.component === 'router' && typeof window.getRouterSetupData === 'function') {
            const routerData = window.getRouterSetupData();
            try {
                const response = await Monolith.API.post('/api/system/settings', routerData);
                if (!response.success && !response.Success) {
                    throw new Error(response.error || response.Error || 'Failed to save settings');
                }
            } catch (err) {
                console.error('Failed to save router settings:', err);
                throw err;
            }
        } else if (currentStepData.component === 'network' && typeof window.getNetworkSetupData === 'function') {
            const networkData = window.getNetworkSetupData();
            await this.saveNetworkConfiguration(networkData);
        } else if (currentStepData.component === 'package' && typeof window.getPackageStepData === 'function') {
            const packageData = window.getPackageStepData();
            await this.savePackageConfiguration(currentStepData.packageId, currentStepData.pageId, packageData);
        }

        // Save step completion
        await Monolith.API.post('/api/setup/complete-step', {
            stepId: currentStepData.id,
            data: stepData
        });

        // Reload status to update progress
        await this.loadSetupStatus();
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
            // Update navigation after initialization
            setTimeout(() => {
                SetupWizard.updateNavigation();
            }, 100);
        } else {
            // Retry after a short delay
            setTimeout(() => {
                if (typeof Monolith.API !== 'undefined') {
                    SetupWizard.init();
                    setTimeout(() => {
                        SetupWizard.updateNavigation();
                    }, 100);
                } else {
                    console.error('Monolith.API not available after retry');
                }
            }, 500);
        }
    }
});

// Export for global access
window.SetupWizard = SetupWizard;
