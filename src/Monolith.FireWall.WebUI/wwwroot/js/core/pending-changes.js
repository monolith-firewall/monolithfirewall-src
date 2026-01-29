/**
 * Pending Changes Indicator
 * Shows a badge in the navbar when there are configuration changes waiting to be applied.
 * Uses SignalR for real-time updates with polling fallback.
 */
(function() {
    'use strict';

    const POLL_INTERVAL = 30000; // Fallback polling every 30 seconds
    const RECONNECT_DELAY = 5000; // SignalR reconnect delay
    let pollTimer = null;
    let currentCount = 0;
    let connection = null;
    let useSignalR = true;

    /**
     * Initialize the pending changes indicator
     */
    function init() {
        // Try to connect with SignalR
        initSignalR();

        // Initial check via API
        checkPendingChanges();

        // Bind click events
        bindEvents();
    }

    /**
     * Initialize SignalR connection
     */
    function initSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR client not loaded, falling back to polling');
            useSignalR = false;
            startPolling();
            return;
        }

        try {
            connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/pending-changes')
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Warning)
                .build();

            // Handle pending count updates
            connection.on('PendingCountChanged', function(count) {
                console.log('SignalR: Pending count changed to', count);
                updateBadge(count);
            });

            // Handle new change added
            connection.on('ChangeAdded', function(change) {
                console.log('SignalR: Change added', change);
                // Increment count and show notification
                updateBadge(currentCount + 1);
                showNotification('info', 'New configuration change staged');
            });

            // Handle changes applied
            connection.on('ChangesApplied', function(data) {
                console.log('SignalR: Changes applied', data);
                // Refresh the count
                checkPendingChanges();
            });

            // Handle changes discarded
            connection.on('ChangesDiscarded', function(data) {
                console.log('SignalR: Changes discarded', data);
                // Refresh the count
                checkPendingChanges();
            });

            // Connection state handlers
            connection.onreconnecting(function(error) {
                console.log('SignalR: Reconnecting...', error);
            });

            connection.onreconnected(function(connectionId) {
                console.log('SignalR: Reconnected', connectionId);
                // Refresh count after reconnect
                checkPendingChanges();
            });

            connection.onclose(function(error) {
                console.log('SignalR: Connection closed', error);
                // Fall back to polling
                startPolling();
            });

            // Start the connection
            startSignalR();

        } catch (error) {
            console.error('Failed to initialize SignalR:', error);
            useSignalR = false;
            startPolling();
        }
    }

    /**
     * Start SignalR connection
     */
    async function startSignalR() {
        if (!connection) return;

        try {
            await connection.start();
            console.log('SignalR: Connected to pending-changes hub');
            useSignalR = true;
            // Stop polling since SignalR is working
            stopPolling();
        } catch (error) {
            console.warn('SignalR: Failed to connect, will retry...', error);
            // Retry after delay
            setTimeout(startSignalR, RECONNECT_DELAY);
            // Use polling as fallback
            startPolling();
        }
    }

    /**
     * Start polling for pending changes (fallback)
     */
    function startPolling() {
        if (pollTimer) {
            return; // Already polling
        }
        console.log('Starting polling fallback for pending changes');
        pollTimer = setInterval(checkPendingChanges, POLL_INTERVAL);
    }

    /**
     * Stop polling
     */
    function stopPolling() {
        if (pollTimer) {
            clearInterval(pollTimer);
            pollTimer = null;
            console.log('Stopped polling (using SignalR)');
        }
    }

    /**
     * Check for pending changes from the API
     */
    async function checkPendingChanges() {
        try {
            const response = await fetch('/api/settings/pending/count');
            const data = await response.json();

            // Handle both uppercase (C#) and lowercase (JS) property names
            const success = data.Success || data.success;
            const responseData = data.Data || data.data;

            if (success && responseData) {
                updateBadge(responseData.count || responseData.Count || 0);
            }
        } catch (error) {
            console.warn('Failed to check pending changes:', error);
        }
    }

    /**
     * Update the badge count
     */
    function updateBadge(count) {
        currentCount = count;

        const badge = document.getElementById('pending-changes-badge');
        const icon = document.getElementById('pending-changes-icon');
        const container = document.getElementById('pending-changes-container');

        if (!badge || !container) return;

        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : count.toString();
            badge.classList.remove('d-none');
            container.classList.remove('d-none');
            if (icon) {
                icon.classList.add('text-warning');
            }
        } else {
            badge.classList.add('d-none');
            container.classList.add('d-none');
            if (icon) {
                icon.classList.remove('text-warning');
            }
        }
    }

    /**
     * Get all pending changes
     */
    async function getPendingChanges() {
        try {
            const response = await fetch('/api/settings/pending');
            const data = await response.json();

            const success = data.Success || data.success;
            const responseData = data.Data || data.data;

            if (success && responseData) {
                return responseData.changes || responseData.Changes || [];
            }
        } catch (error) {
            console.error('Failed to get pending changes:', error);
        }
        return [];
    }

    /**
     * Apply all pending changes
     */
    async function applyAllChanges() {
        try {
            const response = await fetch('/api/settings/apply', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({})
            });
            const data = await response.json();

            const success = data.Success || data.success;
            const responseData = data.Data || data.data;

            if (success && responseData && (responseData.Success || responseData.success)) {
                const appliedCount = responseData.AppliedCount || responseData.appliedCount || 0;
                showNotification('success', `Applied ${appliedCount} change(s) successfully`);
                // SignalR will update the count, but check anyway for fallback
                if (!useSignalR) {
                    await checkPendingChanges();
                }
                return true;
            } else {
                const error = responseData?.Message || responseData?.message || data.Error || data.error || 'Failed to apply changes';
                showNotification('error', error);
                return false;
            }
        } catch (error) {
            console.error('Failed to apply changes:', error);
            showNotification('error', 'Failed to apply changes: ' + error.message);
            return false;
        }
    }

    /**
     * Discard all pending changes
     */
    async function discardAllChanges() {
        try {
            const response = await fetch('/api/settings/pending', {
                method: 'DELETE'
            });
            const data = await response.json();

            const success = data.Success || data.success;

            if (success) {
                showNotification('info', 'Discarded all pending changes');
                // SignalR will update the count, but check anyway for fallback
                if (!useSignalR) {
                    await checkPendingChanges();
                }
                return true;
            } else {
                showNotification('error', data.Error || data.error || 'Failed to discard changes');
                return false;
            }
        } catch (error) {
            console.error('Failed to discard changes:', error);
            showNotification('error', 'Failed to discard changes: ' + error.message);
            return false;
        }
    }

    /**
     * Show the pending changes modal
     */
    async function showPendingChangesModal() {
        const changes = await getPendingChanges();

        let modalHtml = `
            <div class="modal fade" id="pendingChangesModal" tabindex="-1">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">
                                <i class="fa-solid fa-clock-rotate-left me-2"></i>
                                Pending Configuration Changes
                                ${useSignalR ? '<span class="badge bg-success ms-2" title="Real-time updates active"><i class="fa-solid fa-wifi"></i></span>' : ''}
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            ${changes.length === 0 ?
                                '<p class="text-muted text-center py-4">No pending changes</p>' :
                                renderChangesTable(changes)
                            }
                        </div>
                        <div class="modal-footer">
                            <a href="/system/pending-changes" class="btn btn-outline-primary me-auto">
                                <i class="fa-solid fa-external-link me-1"></i> View Full Page
                            </a>
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                            ${changes.length > 0 ? `
                                <button type="button" class="btn btn-outline-danger" id="btn-discard-all-changes">
                                    <i class="fa-solid fa-trash me-1"></i> Discard All
                                </button>
                                <button type="button" class="btn btn-success" id="btn-apply-all-changes">
                                    <i class="fa-solid fa-check me-1"></i> Apply All
                                </button>
                            ` : ''}
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal if present
        const existingModal = document.getElementById('pendingChangesModal');
        if (existingModal) {
            existingModal.remove();
        }

        // Add new modal to DOM
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Initialize and show modal
        const modal = new bootstrap.Modal(document.getElementById('pendingChangesModal'));
        modal.show();

        // Bind modal buttons
        const applyBtn = document.getElementById('btn-apply-all-changes');
        const discardBtn = document.getElementById('btn-discard-all-changes');

        if (applyBtn) {
            applyBtn.addEventListener('click', async () => {
                applyBtn.disabled = true;
                applyBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Applying...';

                const success = await applyAllChanges();
                if (success) {
                    modal.hide();
                } else {
                    applyBtn.disabled = false;
                    applyBtn.innerHTML = '<i class="fa-solid fa-check me-1"></i> Apply All';
                }
            });
        }

        if (discardBtn) {
            discardBtn.addEventListener('click', async () => {
                if (confirm('Are you sure you want to discard all pending changes? This cannot be undone.')) {
                    const success = await discardAllChanges();
                    if (success) {
                        modal.hide();
                    }
                }
            });
        }

        // Clean up modal when hidden
        document.getElementById('pendingChangesModal').addEventListener('hidden.bs.modal', function() {
            this.remove();
        });
    }

    /**
     * Render the changes table HTML
     */
    function renderChangesTable(changes) {
        let html = `
            <div class="table-responsive">
                <table class="table table-sm table-hover">
                    <thead>
                        <tr>
                            <th>Category</th>
                            <th>Target</th>
                            <th>Description</th>
                            <th>Changed By</th>
                            <th>Time</th>
                        </tr>
                    </thead>
                    <tbody>
        `;

        for (const change of changes) {
            // Handle both casing conventions
            const targetCategory = change.targetCategory || change.TargetCategory || 'Other';
            const targetKey = change.targetKey || change.TargetKey || '';
            const description = change.description || change.Description || 'Configuration change';
            const createdBy = change.createdBy || change.CreatedBy || 'Unknown';
            const createdAt = change.createdAt || change.CreatedAt;
            const timeAgo = createdAt ? formatTimeAgo(new Date(createdAt)) : '';

            html += `
                <tr>
                    <td><span class="badge bg-secondary">${escapeHtml(targetCategory)}</span></td>
                    <td><code>${escapeHtml(targetKey)}</code></td>
                    <td>${escapeHtml(description)}</td>
                    <td>${escapeHtml(createdBy)}</td>
                    <td><small class="text-muted">${timeAgo}</small></td>
                </tr>
            `;
        }

        html += `
                    </tbody>
                </table>
            </div>
        `;

        return html;
    }

    /**
     * Format time ago
     */
    function formatTimeAgo(date) {
        const seconds = Math.floor((new Date() - date) / 1000);

        if (seconds < 60) return 'Just now';
        if (seconds < 3600) return Math.floor(seconds / 60) + ' min ago';
        if (seconds < 86400) return Math.floor(seconds / 3600) + ' hours ago';
        return Math.floor(seconds / 86400) + ' days ago';
    }

    /**
     * Escape HTML
     */
    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Show notification
     */
    function showNotification(type, message) {
        // Use the existing Monolith UI notification if available
        if (window.Monolith && window.Monolith.UI && window.Monolith.UI.toast) {
            window.Monolith.UI.toast(message, type);
        } else if (window.MonolithUI && window.MonolithUI.showToast) {
            window.MonolithUI.showToast(message, type);
        } else {
            // Fallback to console
            console.log(`[${type}] ${message}`);
        }
    }

    /**
     * Bind click events
     */
    function bindEvents() {
        // Click on the pending changes button opens the modal
        $(document).off('click', '#pending-changes-button').on('click', '#pending-changes-button', function(e) {
            e.preventDefault();
            showPendingChangesModal();
        });
    }

    /**
     * Notify that a change was made - triggers immediate check
     */
    function notifyChange() {
        checkPendingChanges();
    }

    /**
     * Check if SignalR is connected
     */
    function isConnected() {
        return connection && connection.state === signalR.HubConnectionState.Connected;
    }

    /**
     * Get connection status
     */
    function getConnectionStatus() {
        if (!connection) return 'not-initialized';
        return connection.state;
    }

    // Expose public API
    const publicAPI = {
        init: init,
        check: checkPendingChanges,
        getCount: () => currentCount,
        apply: applyAllChanges,
        discard: discardAllChanges,
        showModal: showPendingChangesModal,
        startPolling: startPolling,
        stopPolling: stopPolling,
        notifyChange: notifyChange,
        updateBadge: updateBadge,
        isConnected: isConnected,
        getConnectionStatus: getConnectionStatus
    };

    // Expose both as PendingChanges and Monolith.PendingChanges
    window.PendingChanges = publicAPI;
    window.Monolith = window.Monolith || {};
    window.Monolith.PendingChanges = publicAPI;

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
