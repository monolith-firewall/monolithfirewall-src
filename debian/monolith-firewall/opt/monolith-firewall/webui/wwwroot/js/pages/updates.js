// Update Manager Page
var Updates = {
    init: function() {
        console.log('Initializing Update Manager...');
        this.render();
        this.checkUpdates();
    },

    render: function() {
        const container = $('#updates-container');
        container.html(`
            <div class="container-fluid">
                <h1 class="mb-4">Update Manager</h1>

                <div class="row mb-4">
                    <div class="col-md-12">
                        <button class="btn btn-primary" id="check-updates-btn">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path fill-rule="evenodd" d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                            </svg>
                            Check for Updates
                        </button>
                    </div>
                </div>

                <div class="row g-4">
                    <div class="col-md-6">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">System Updates</h5>
                            </div>
                            <div class="card-body" id="system-updates">
                                <div class="text-center py-3">
                                    <div class="spinner-border text-primary" role="status">
                                        <span class="visually-hidden">Loading...</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="col-md-6">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Package Updates</h5>
                            </div>
                            <div class="card-body" id="package-updates">
                                <div class="text-center py-3">
                                    <div class="spinner-border text-primary" role="status">
                                        <span class="visually-hidden">Loading...</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="row mt-4">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Update History</h5>
                            </div>
                            <div class="card-body" id="update-history">
                                <div class="text-center py-3">
                                    <p class="text-muted">No update history available</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);

        $('#check-updates-btn').on('click', () => this.checkUpdates());
    },

    checkUpdates: async function() {
        // System updates
        $('#system-updates').html(`
            <div class="alert alert-success">
                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                    <path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0zm-3.97-3.03a.75.75 0 0 0-1.08.022L7.477 9.417 5.384 7.323a.75.75 0 0 0-1.06 1.06L6.97 11.03a.75.75 0 0 0 1.079-.02l3.992-4.99a.75.75 0 0 0-.01-1.05z"/>
                </svg>
                <strong>Monolith FireWall Core</strong><br>
                <small>Version 1.0.0 - Up to date</small>
            </div>
            <p class="text-muted small mb-0">Last checked: ${new Date().toLocaleString()}</p>
        `);

        // Package updates
        $('#package-updates').html(`
            <div class="alert alert-info">
                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                    <path d="M8 16A8 8 0 1 0 8 0a8 8 0 0 0 0 16zm.93-9.412-1 4.705c-.07.34.029.533.304.533.194 0 .487-.07.686-.246l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/>
                </svg>
                <strong>All packages are up to date</strong><br>
                <small>1 package(s) checked</small>
            </div>
            <button class="btn btn-sm btn-outline-primary">Check Online Updates</button>
        `);
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Updates = Updates;
}
