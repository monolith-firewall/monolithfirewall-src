/**
 * Setup Wizard - Navigation Helper
 * Provides navigation button wiring for setup pages
 * The main setup wizard controller is in setup-wizard.js
 */

// Setup navigation for individual step pages (router, network, etc.)
// Navigation buttons are in the layout footer, so we just need to wire up handlers
function setupStepPageNavigation() {
    // Wire up navigation button handlers (buttons are in layout footer)
    $('#btn-setup-next').off('click').on('click', async () => {
        if (window.SetupWizard) {
            await window.SetupWizard.nextStep();
        }
    });

    $('#btn-setup-back').off('click').on('click', () => {
        if (window.SetupWizard) {
            window.SetupWizard.previousStep();
        }
    });

    $('#btn-setup-skip').off('click').on('click', async () => {
        if (window.SetupWizard) {
            await window.SetupWizard.skipCurrentStep();
        }
    });

    $('#btn-setup-finish').off('click').on('click', async () => {
        if (window.SetupWizard) {
            await window.SetupWizard.finishSetup();
        }
    });

    // Update navigation state
    if (window.SetupWizard) {
        window.SetupWizard.updateNavigation();
    }
}

// Initialize when document is ready
$(document).ready(function() {
    // Only initialize if we're on the setup page
    if (window.location.pathname === '/setup' || window.location.pathname.startsWith('/setup/')) {
        // Wait for setup-wizard.js to initialize
        setTimeout(() => {
            if (window.SetupWizard) {
                // Ensure navigation is set up
                setupStepPageNavigation();
                // Update navigation state
                window.SetupWizard.updateNavigation();
            }
        }, 200);
    }
});

// Export for use in setup pages
window.setupStepPageNavigation = setupStepPageNavigation;
