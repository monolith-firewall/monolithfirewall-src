// Status Pages
var Status = {
    init: function() {
        console.log('Initializing Status module...');
    },

    renderPage: function() {
        console.log('Rendering Status page...');
        const path = window.location.pathname || '';
        if (path.startsWith('/status/system')) {
            this.renderSystem();
        } else if (path.startsWith('/status/interfaces')) {
            this.renderInterfaces();
        } else if (path.startsWith('/status/services')) {
            this.renderServices();
        } else if (path.startsWith('/status/logs')) {
            this.renderLogs();
        } else {
            this.renderSystem();
        }
    },

    renderSystem: function() {
        const container = $('#status-system-container, #page-content').first();
        if (!container.length) return;
        
        container.html(`
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h4 class="mb-0">System Status</h4>
                            </div>
                            <div class="card-body">
                                <p class="text-muted">System status information - Coming soon</p>
                                <p>This page will display system uptime, CPU usage, memory usage, and disk usage.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    renderInterfaces: function() {
        const container = $('#status-interfaces-container, #page-content').first();
        if (!container.length) return;

        container.html(`
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h4 class="mb-0">Interface Status</h4>
                            </div>
                            <div class="card-body">
                                <p class="text-muted">Interface status information - Coming soon</p>
                                <p>This page will display detailed interface statistics and status.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    renderServices: function() {
        const container = $('#status-services-container, #page-content').first();
        if (!container.length) return;

        container.html(`
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h4 class="mb-0">Services Status</h4>
                            </div>
                            <div class="card-body">
                                <p class="text-muted">Services status information - Coming soon</p>
                                <p>This page will display the status of all system services.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    renderLogs: function() {
        const container = $('#status-logs-container, #page-content').first();
        if (!container.length) return;

        container.html(`
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h4 class="mb-0">System Logs</h4>
                            </div>
                            <div class="card-body">
                                <p class="text-muted">System logs viewer - Coming soon</p>
                                <p>This page will display system logs with filtering and search capabilities.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Status = Status;
}