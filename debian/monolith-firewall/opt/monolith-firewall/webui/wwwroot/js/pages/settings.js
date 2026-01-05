// Settings Page with Tabbed Interface
var Settings = {
    currentTab: 'system',
    tabs: {},

    init: function() {
        console.log('Initializing Settings...');
        this.render();
        this.loadTab('system');
    },

    render: function() {
        const container = $('#settings-container');
        container.html(`
            <div class="container-fluid">
                <h1 class="mb-4">Settings</h1>

                <!-- Tab Navigation -->
                <ul class="nav nav-tabs mb-4" id="settings-tabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" id="system-tab" data-bs-toggle="tab" data-bs-target="#system-pane" 
                                type="button" role="tab" aria-controls="system-pane" aria-selected="true">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>
                                <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                            </svg>
                            System
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="webui-tab" data-bs-toggle="tab" data-bs-target="#webui-pane" 
                                type="button" role="tab" aria-controls="webui-pane" aria-selected="false">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M0 2a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2V2zm15 2h-4V0H9v4H7V0H5v4H3V0H1v4h1.5L1 5.5v9L2.5 16h11l1.5-1.5v-9L14.5 4H15V2zM1 5.5l1.5-1.5H5v1H3v8H2V5.5zm13 9H3V6h11v8.5z"/>
                            </svg>
                            Web UI
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="advanced-tab" data-bs-toggle="tab" data-bs-target="#advanced-pane" 
                                type="button" role="tab" aria-controls="advanced-pane" aria-selected="false">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M8 1a1 1 0 0 1 1 1v1.05a5.5 5.5 0 0 1 2.243.93l.743-.743a1 1 0 0 1 1.414 1.414l-.743.743a5.5 5.5 0 0 1 .93 2.243H14a1 1 0 1 1 0 2h-1.05a5.5 5.5 0 0 1-.93 2.243l.743.743a1 1 0 0 1-1.414 1.414l-.743-.743a5.5 5.5 0 0 1-2.243.93V14a1 1 0 1 1-2 0v-1.05a5.5 5.5 0 0 1-2.243-.93l-.743.743a1 1 0 1 1-1.414-1.414l.743-.743a5.5 5.5 0 0 1-.93-2.243H2a1 1 0 1 1 0-2h1.05a5.5 5.5 0 0 1 .93-2.243l-.743-.743a1 1 0 1 1 1.414-1.414l.743.743A5.5 5.5 0 0 1 7 3.05V2a1 1 0 0 1 1-1zm0 4a3 3 0 1 0 0 6 3 3 0 0 0 0-6z"/>
                            </svg>
                            Advanced
                        </button>
                    </li>
                </ul>

                <!-- Tab Content -->
                <div class="tab-content" id="settings-tab-content">
                    <div class="tab-pane fade show active" id="system-pane" role="tabpanel" aria-labelledby="system-tab">
                        <div id="system-tab-content"></div>
                    </div>
                    <div class="tab-pane fade" id="webui-pane" role="tabpanel" aria-labelledby="webui-tab">
                        <div id="webui-tab-content"></div>
                    </div>
                    <div class="tab-pane fade" id="advanced-pane" role="tabpanel" aria-labelledby="advanced-tab">
                        <div id="advanced-tab-content"></div>
                    </div>
                </div>
            </div>
        `);

        // Handle tab switching
        $('#settings-tabs button[data-bs-toggle="tab"]').on('shown.bs.tab', (e) => {
            const target = $(e.target).data('bsTarget');
            if (target === '#system-pane') {
                this.loadTab('system');
            } else if (target === '#webui-pane') {
                this.loadTab('webui');
            } else if (target === '#advanced-pane') {
                this.loadTab('advanced');
            }
        });
    },

    loadTab: function(tabName) {
        if (this.currentTab === tabName && this.tabs[tabName]) {
            return; // Already loaded
        }

        this.currentTab = tabName;

        // Load tab module if not already loaded
        if (!this.tabs[tabName]) {
            if (tabName === 'system') {
                this.tabs[tabName] = SettingsSystem;
            } else if (tabName === 'webui') {
                this.tabs[tabName] = SettingsWebUI;
            } else if (tabName === 'advanced') {
                this.tabs[tabName] = SettingsAdvanced;
            }
        }

        // Initialize tab
        if (this.tabs[tabName] && typeof this.tabs[tabName].init === 'function') {
            this.tabs[tabName].init();
        }
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Settings = Settings;
}
