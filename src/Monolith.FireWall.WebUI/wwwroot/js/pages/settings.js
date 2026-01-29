// Settings Page with Tabbed Interface
var Settings = {
    currentTab: 'system',
    tabs: {},

    init: function() {
        console.log('Initializing Settings...');
    },

    renderPage: function() {
        console.log('Rendering Settings page...');
        this.renderStructure();
        
        // Small delay to ensure sub-scripts are parsed
        setTimeout(() => {
            this.loadTab(this.currentTab || 'system', true);
        }, 100);
    },

    renderStructure: function() {
        const container = $('#settings-container');
        if (!container.length) return;

        // Render page header
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "General Settings",
                icon: "fa-gear",
                description: "Configure system and WebUI settings",
                container: container,
                prepend: true
            });
        }

        container.append(`
            <div class="container-fluid p-4">
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
                </ul>

                <!-- Tab Content -->
                <div class="tab-content" id="settings-tab-content">
                    <div class="tab-pane fade show active" id="system-pane" role="tabpanel" aria-labelledby="system-tab">
                        <div id="system-tab-content"></div>
                    </div>
                    <div class="tab-pane fade" id="webui-pane" role="tabpanel" aria-labelledby="webui-tab">
                        <div id="webui-tab-content"></div>
                    </div>
                </div>
            </div>
        `);

        // Handle tab switching - use .off() first to prevent duplicate handlers
        $('#settings-tabs button[data-bs-toggle="tab"]').off('shown.bs.tab').on('shown.bs.tab', (e) => {
            const target = $(e.target).data('bsTarget');
            if (target === '#system-pane') {
                this.loadTab('system');
            } else if (target === '#webui-pane') {
                this.loadTab('webui');
            }
        });
    },

    loadTab: function(tabName, forceRender = false, retryCount = 0) {
        if (!forceRender && this.currentTab === tabName && this.tabs[tabName]) {
            return; // Already loaded
        }

        this.currentTab = tabName;

        // Load tab module if not already loaded
        if (!this.tabs[tabName]) {
            if (tabName === 'system') {
                this.tabs[tabName] = typeof SettingsSystem !== 'undefined' ? SettingsSystem : null;
            } else if (tabName === 'webui') {
                this.tabs[tabName] = typeof SettingsWebUI !== 'undefined' ? SettingsWebUI : null;
            }
        }

        // Initialize and render tab
        const tab = this.tabs[tabName];
        if (tab) {
            if (!tab.isInitialized && typeof tab.init === 'function') {
                tab.init();
                tab.isInitialized = true;
            }
            if (typeof tab.renderPage === 'function') {
                tab.renderPage();
            } else if (typeof tab.render === 'function') {
                tab.render();
            }
        } else if (retryCount < 5) {
            console.warn(`Tab module ${tabName} not found, retrying (${retryCount + 1}/5)...`);
            setTimeout(() => {
                this.loadTab(tabName, forceRender, retryCount + 1);
            }, 200);
        }
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Settings = Settings;
}