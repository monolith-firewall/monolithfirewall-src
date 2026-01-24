/**
 * MonolithFireWall UI Components
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.UI = {
    /**
     * Toast container and active toasts tracking
     */
    _toastContainer: null,
    _activeToasts: [],

    /**
     * Initialize toast container if it doesn't exist
     */
    _initToastContainer: function() {
        if (!this._toastContainer || !this._toastContainer.length) {
            this._toastContainer = $('#monolith-toast-container');
            if (!this._toastContainer.length) {
                this._toastContainer = $(`
                    <div id="monolith-toast-container" 
                         style="position: fixed; top: 20px; right: 20px; z-index: 99999; pointer-events: none;">
                    </div>
                `);
                $('body').append(this._toastContainer);
            }
        }
        return this._toastContainer;
    },

    /**
     * Update positions of all active toasts
     */
    _updateToastPositions: function() {
        const container = this._toastContainer;
        if (!container || !container.length) return;

        let topOffset = 0;
        const spacing = 10; // Space between toasts

        // Update positions for all active toasts
        this._activeToasts.forEach((toastId) => {
            const toast = $(`#${toastId}`);
            if (toast.length) {
                toast.css({
                    'top': topOffset + 'px',
                    'right': '0px',
                    'pointer-events': 'auto'
                });
                // Get height of this toast (including margin)
                const toastHeight = toast.outerHeight(true);
                topOffset += toastHeight + spacing;
            }
        });
    },

    /**
     * Remove toast from active list and update positions
     */
    _removeToast: function(toastId) {
        const index = this._activeToasts.indexOf(toastId);
        if (index > -1) {
            this._activeToasts.splice(index, 1);
        }
        this._updateToastPositions();
    },

    /**
     * Show toast notification
     */
    toast: function(message, type = 'info') {
        const toastId = 'toast-' + Date.now() + '-' + Math.random().toString(36).substring(2, 11);
        const bgClass = {
            'success': 'alert-success',
            'error': 'alert-danger',
            'warning': 'alert-warning',
            'info': 'alert-info'
        }[type] || 'alert-info';

        const icon = {
            'success': '✓',
            'error': '✗',
            'warning': '⚠',
            'info': 'ℹ'
        }[type] || 'ℹ';

        // Initialize container
        const container = this._initToastContainer();

        const toast = $(`
            <div id="${toastId}" class="alert ${bgClass} alert-dismissible fade show" 
                 style="min-width: 300px; max-width: 400px; box-shadow: 0 4px 12px rgba(0,0,0,0.3); margin-bottom: 0; margin-right: 0;">
                <strong>${icon}</strong> ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `);

        // Add to container and active list
        container.append(toast);
        this._activeToasts.push(toastId);

        // Update positions
        this._updateToastPositions();

        // Handle manual close (Bootstrap alert dismiss)
        toast.on('closed.bs.alert', () => {
            this._removeToast(toastId);
        });

        // Also handle close button click directly (fallback)
        toast.find('.btn-close').on('click', () => {
            setTimeout(() => {
                if ($(`#${toastId}`).length) {
                    this._removeToast(toastId);
                }
            }, 350); // Wait for fade animation
        });

        // Auto-remove after 5 seconds
        const autoRemoveTimeout = setTimeout(() => {
            if ($(`#${toastId}`).length) {
                toast.fadeOut(300, () => {
                    toast.remove();
                    this._removeToast(toastId);
                });
            }
        }, 5000);

        // Clear timeout if toast is manually closed
        toast.on('closed.bs.alert', () => {
            clearTimeout(autoRemoveTimeout);
        });
    },
    showError: function(message) {
        this.toast(message, 'error');
    },
    showSuccess: function(message) {
        this.toast(message, 'success');
    },

    /**
     * Show loading spinner
     */
    showLoading: function(container) {
        const spinner = $(`
            <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <p class="mt-2 text-muted">Loading...</p>
            </div>
        `);
        $(container).html(spinner);
    },

    /**
     * Confirm dialog
     */
    confirm: function(message, callback) {
        if (confirm(message)) {
            callback();
        }
    },

    /**
     * Show a Bootstrap modal
     */
    showModal: function(title, bodyHtml, options) {
        const modalId = 'modal-' + Date.now();
        const sizeClass = options && options.size ? `modal-${options.size}` : '';
        const footerHtml = options && options.footerHtml ? options.footerHtml : `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
        `;

        const modal = $(`
            <div class="modal fade" id="${modalId}" tabindex="-1" aria-hidden="true">
                <div class="modal-dialog ${sizeClass} modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">${title}</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">${bodyHtml}</div>
                        <div class="modal-footer">${footerHtml}</div>
                    </div>
                </div>
            </div>
        `);

        $('body').append(modal);
        const instance = new bootstrap.Modal(modal[0], {
            backdrop: options && options.staticBackdrop ? 'static' : true,
            keyboard: !(options && options.disableKeyboard)
        });

        modal.on('hidden.bs.modal', function() {
            modal.remove();
            if (options && typeof options.onClose === 'function') {
                options.onClose();
            }
        });

        instance.show();
        return { id: modalId, instance: instance, element: modal };
    },

    /**
     * Format date
     */
    formatDate: function(dateString) {
        if (!dateString) {
            return 'n/a';
        }

        const date = new Date(dateString);
        if (isNaN(date.getTime())) {
            return 'n/a';
        }

        return date.toLocaleString();
    }
};
