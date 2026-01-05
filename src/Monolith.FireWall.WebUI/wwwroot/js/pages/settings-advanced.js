// Advanced Settings Tab (wrapper for existing AdvancedSettings)
var SettingsAdvanced = {
    init: function() {
        console.log('Initializing Advanced Settings tab...');
        // Load the advanced-settings module if not already loaded
        if (typeof AdvancedSettings === 'undefined') {
            // The advanced-settings.js should be loaded by the page loader
            // For now, just render a placeholder or redirect to the advanced settings page
            this.renderPlaceholder();
        } else {
            // Initialize the existing AdvancedSettings module
            AdvancedSettings.init();
        }
    },

    renderPlaceholder: function() {
        const container = $('#advanced-tab-content');
        container.html(`
            <div class="alert alert-info">
                <h5>Advanced Settings</h5>
                <p>Advanced settings functionality is available at <a href="#/system/advanced">System > Advanced Settings</a>.</p>
                <p>This includes system tuneables, network controls, and firewall settings.</p>
            </div>
        `);
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.SettingsAdvanced = SettingsAdvanced;
}
