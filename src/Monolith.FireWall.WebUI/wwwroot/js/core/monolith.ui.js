/**
 * MonolithFireWall UI Components
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.UI = {
    /**
     * Show toast notification
     */
    toast: function(message, type = 'info') {
        const toastId = 'toast-' + Date.now();
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

        const toast = $(`
            <div id="${toastId}" class="alert ${bgClass} alert-dismissible fade show position-fixed" 
                 style="top: 20px; right: 20px; z-index: 9999; min-width: 300px; box-shadow: 0 4px 6px rgba(0,0,0,0.2);">
                <strong>${icon}</strong> ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `);

        $('body').append(toast);

        setTimeout(() => {
            toast.fadeOut(300, function() {
                $(this).remove();
            });
        }, 5000);
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
