// Firewall Schedules Module
var Schedules = {
    schedules: [],

    init: function() {
        console.log('Initializing Schedules module...');
    },

    renderPage: function() {
        console.log('Rendering Schedules page...');
        const content = $('#page-content');
        content.empty();

        content.append(`
            <div class="package-page schedules-page" data-module="schedules" data-package="firewall">
                <div class="container-fluid p-4">
                    <div class="row mb-4">
                        <div class="col-12">
                            <h2 class="page-title">
                                <svg width="24" height="24" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                                    <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM4.5 7.5a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7z"/>
                                </svg>
                                Schedules
                            </h2>
                            <p class="text-muted">Define time-based schedules for firewall rules</p>
                        </div>
                    </div>

                    <div id="applyChangesBanner" class="alert alert-warning d-none mb-3" role="alert">
                        <div class="d-flex justify-content-between align-items-center">
                            <div>
                                <strong>⚠️ Pending Changes</strong>
                                <span class="ms-2">You have unsaved changes. Click "Apply Changes" to apply them to the system.</span>
                            </div>
                            <div>
                                <button type="button" class="btn btn-sm btn-success me-2" onclick="Monolith.router.navigate('/firewall/apply')">
                                    Apply Changes
                                </button>
                            </div>
                        </div>
                    </div>

                    <div id="schedulesStatusMessage" class="alert d-none"></div>

                    <ul class="nav nav-tabs mb-4" id="schedulesTabs" role="tablist">
                        <li class="nav-item" role="presentation">
                            <button class="nav-link active" id="schedules-list-tab" data-bs-toggle="tab" data-bs-target="#schedules-list" 
                                    type="button" role="tab" aria-controls="schedules-list" aria-selected="true">
                                Schedules
                            </button>
                        </li>
                    </ul>

                    <div class="tab-content" id="schedulesTabContent">
                        <div class="tab-pane fade show active" id="schedules-list" role="tabpanel">
                            <div class="card">
                                <div class="card-header d-flex justify-content-between align-items-center">
                                    <h5 class="mb-0">Defined Schedules</h5>
                                    <button type="button" class="btn btn-sm btn-primary" id="btn-add-schedule">
                                        Add Schedule
                                    </button>
                                </div>
                                <div class="card-body">
                                    <div class="table-responsive">
                                        <table class="table table-hover" id="schedulesTable">
                                            <thead>
                                                <tr>
                                                    <th>Name</th>
                                                    <th>Time Ranges</th>
                                                    <th>Description</th>
                                                    <th>Status</th>
                                                    <th>Actions</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                <tr><td colspan="5" class="text-center text-muted">Loading...</td></tr>
                                            </tbody>
                                        </table>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);

        this.loadSchedules();
        this.attachEventHandlers();
    },

    loadSchedules: async function() {
        try {
            const response = await Monolith.API.get('/firewall/schedules');
            if (response.success || response.Success) {
                const data = response.data || response.Data || {};
                const items = data.items || data || [];
                const scheduleArray = Array.isArray(items) ? items : (Array.isArray(data) ? data : []);
                this.schedules = scheduleArray.map(s => this.normalizeSchedule(s));
            } else {
                this.schedules = [];
            }
            this.renderSchedules();
        } catch (error) {
            console.error('Error loading schedules:', error);
            this.showMessage('Failed to load schedules', 'error');
            this.schedules = [];
            this.renderSchedules();
        }
    },

    normalizeSchedule: function(schedule) {
        return {
            id: schedule.Id || schedule.id,
            name: schedule.Name || schedule.name,
            description: schedule.Description || schedule.description,
            timeRanges: schedule.TimeRanges || schedule.timeRanges || [],
            enabled: schedule.Enabled !== undefined ? schedule.Enabled : (schedule.enabled !== undefined ? schedule.enabled : true)
        };
    },

    renderSchedules: function() {
        const tbody = $('#schedulesTable tbody');
        if (!tbody.length) return;

        if (this.schedules.length === 0) {
            tbody.html('<tr><td colspan="5" class="text-center text-muted">No schedules configured</td></tr>');
            return;
        }

        let html = '';
        this.schedules.forEach(schedule => {
            const statusBadge = schedule.enabled 
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';
            
            const timeRangesDisplay = schedule.timeRanges.length > 0
                ? schedule.timeRanges.map(tr => `${tr.Day || tr.day}: ${tr.StartTime || tr.startTime}-${tr.EndTime || tr.endTime}`).join(', ')
                : 'No time ranges';
            
            html += `
                <tr>
                    <td><code>${schedule.name}</code></td>
                    <td>${timeRangesDisplay}</td>
                    <td>${schedule.description || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary me-1" data-action="edit-schedule" data-id="${schedule.id}">Edit</button>
                        <button class="btn btn-sm btn-outline-danger" data-action="delete-schedule" data-id="${schedule.id}">Delete</button>
                    </td>
                </tr>
            `;
        });
        tbody.html(html);
    },

    attachEventHandlers: function() {
        $(document).off('click', '#btn-add-schedule');
        $(document).on('click', '#btn-add-schedule', () => {
            this.showAddScheduleModal();
        });

        $(document).off('click', '[data-action="edit-schedule"]');
        $(document).on('click', '[data-action="edit-schedule"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.editSchedule(id);
        });

        $(document).off('click', '[data-action="delete-schedule"]');
        $(document).on('click', '[data-action="delete-schedule"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.deleteSchedule(id);
        });
    },

    showAddScheduleModal: function() {
        this.showScheduleModal(null);
    },

    showScheduleModal: function(schedule) {
        const isEdit = schedule !== null;
        const timeRanges = schedule && schedule.timeRanges ? schedule.timeRanges : [{ day: 'all', startTime: '00:00', endTime: '23:59' }];
        
        let timeRangesHtml = '';
        timeRanges.forEach((tr, index) => {
            timeRangesHtml += this.buildTimeRangeRow(tr, index);
        });

        const modalHtml = `
            <div class="modal fade" id="scheduleModal" tabindex="-1" aria-labelledby="scheduleModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="scheduleModalLabel">${isEdit ? 'Edit' : 'Add'} Schedule</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="scheduleForm">
                                <div class="mb-3">
                                    <label for="scheduleName" class="form-label">Name <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control" id="scheduleName" required 
                                           value="${schedule ? schedule.name : ''}" 
                                           placeholder="e.g., Business_Hours">
                                </div>
                                <div class="mb-3">
                                    <label for="scheduleDescription" class="form-label">Description</label>
                                    <input type="text" class="form-control" id="scheduleDescription" 
                                           value="${schedule ? schedule.description : ''}" 
                                           placeholder="Optional description">
                                </div>
                                <div class="mb-3">
                                    <label class="form-label">Time Ranges <span class="text-danger">*</span></label>
                                    <div id="timeRangesContainer">
                                        ${timeRangesHtml}
                                    </div>
                                    <button type="button" class="btn btn-sm btn-outline-primary mt-2" data-action="add-time-range">
                                        <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                            <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                        </svg>
                                        Add Time Range
                                    </button>
                                </div>
                                <div class="mb-3 form-check">
                                    <input type="checkbox" class="form-check-input" id="scheduleEnabled" 
                                           ${schedule && schedule.enabled !== false ? 'checked' : ''}>
                                    <label class="form-check-label" for="scheduleEnabled">
                                        Enabled
                                    </label>
                                </div>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" data-action="save-schedule-submit">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        $('#scheduleModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('scheduleModal'));
        modal.show();

        $(document).off('click', '[data-action="add-time-range"]');
        $(document).on('click', '[data-action="add-time-range"]', () => this.addTimeRange());

        $(document).off('click', '[data-action="remove-time-range"]');
        $(document).on('click', '[data-action="remove-time-range"]', (e) => {
            $(e.currentTarget).closest('.time-range-item').remove();
        });

        $(document).off('click', '[data-action="save-schedule-submit"]');
        $(document).on('click', '[data-action="save-schedule-submit"]', () => {
            this.saveSchedule(schedule ? schedule.id : null);
        });
        
        $('#scheduleModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    buildTimeRangeRow: function(tr, index) {
        return `
            <div class="time-range-item mb-2" data-index="${index}">
                <div class="row g-2">
                    <div class="col-md-4">
                        <select class="form-select form-select-sm day-select">
                            <option value="all" ${tr.Day === 'all' || tr.day === 'all' ? 'selected' : ''}>All Days</option>
                            <option value="weekdays" ${tr.Day === 'weekdays' || tr.day === 'weekdays' ? 'selected' : ''}>Weekdays</option>
                            <option value="weekends" ${tr.Day === 'weekends' || tr.day === 'weekends' ? 'selected' : ''}>Weekends</option>
                            <option value="monday" ${tr.Day === 'monday' || tr.day === 'monday' ? 'selected' : ''}>Monday</option>
                            <option value="tuesday" ${tr.Day === 'tuesday' || tr.day === 'tuesday' ? 'selected' : ''}>Tuesday</option>
                            <option value="wednesday" ${tr.Day === 'wednesday' || tr.day === 'wednesday' ? 'selected' : ''}>Wednesday</option>
                            <option value="thursday" ${tr.Day === 'thursday' || tr.day === 'thursday' ? 'selected' : ''}>Thursday</option>
                            <option value="friday" ${tr.Day === 'friday' || tr.day === 'friday' ? 'selected' : ''}>Friday</option>
                            <option value="saturday" ${tr.Day === 'saturday' || tr.day === 'saturday' ? 'selected' : ''}>Saturday</option>
                            <option value="sunday" ${tr.Day === 'sunday' || tr.day === 'sunday' ? 'selected' : ''}>Sunday</option>
                        </select>
                    </div>
                    <div class="col-md-3">
                        <input type="time" class="form-control form-control-sm start-time" 
                               value="${tr.StartTime || tr.startTime || '00:00'}">
                    </div>
                    <div class="col-md-3">
                        <input type="time" class="form-control form-control-sm end-time" 
                               value="${tr.EndTime || tr.endTime || '23:59'}">
                    </div>
                    <div class="col-md-2">
                        <button type="button" class="btn btn-sm btn-outline-danger" data-action="remove-time-range">
                            <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M2.146 2.854a.5.5 0 1 1 .708-.708L8 7.293l5.146-5.147a.5.5 0 0 1 .708.708L8.707 8l5.147 5.146a.5.5 0 0 1-.708.708L8 8.707l-5.146 5.147a.5.5 0 0 1-.708-.708L7.293 8 2.146 2.854Z"/>
                            </svg>
                        </button>
                    </div>
                </div>
            </div>
        `;
    },

    addTimeRange: function() {
        const container = $('#timeRangesContainer');
        if (!container.length) return;
        const index = container.children().length;
        container.append(this.buildTimeRangeRow({ day: 'all', startTime: '00:00', endTime: '23:59' }, index));
    },

    editSchedule: async function(id) {
        try {
            const response = await Monolith.API.get(`/firewall/schedules/${id}`);
            if (response.success || response.Success) {
                const schedule = this.normalizeSchedule(response.data || response.Data);
                this.showScheduleModal(schedule);
            } else {
                this.showMessage('Failed to load schedule', 'error');
            }
        } catch (error) {
            console.error('Error loading schedule:', error);
            this.showMessage('Failed to load schedule', 'error');
        }
    },

    saveSchedule: async function(id) {
        const form = document.getElementById('scheduleForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        // Collect time ranges
        const timeRanges = [];
        $('#timeRangesContainer .time-range-item').each(function() {
            const day = $(this).find('.day-select').val();
            const startTime = $(this).find('.start-time').val();
            const endTime = $(this).find('.end-time').val();
            if (day && startTime && endTime) {
                timeRanges.push({ day, startTime, endTime });
            }
        });

        if (timeRanges.length === 0) {
            this.showMessage('At least one time range is required', 'error');
            return;
        }

        const schedule = {
            name: $('#scheduleName').val().trim(),
            description: $('#scheduleDescription').val().trim(),
            timeRanges: timeRanges,
            enabled: $('#scheduleEnabled').is(':checked')
        };

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/schedules/${id}`, schedule);
            } else {
                response = await Monolith.API.post('/firewall/schedules', schedule);
            }

            if (response.success || response.Success) {
                const modalEl = document.getElementById('scheduleModal');
                if (modalEl) {
                    const modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();
                }
                this.showMessage(id ? 'Schedule updated successfully' : 'Schedule created successfully', 'success');
                this.loadSchedules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to save schedule', 'error');
            }
        } catch (error) {
            console.error('Error saving schedule:', error);
            this.showMessage('Failed to save schedule', 'error');
        }
    },

    deleteSchedule: async function(id) {
        if (!confirm('Are you sure you want to delete this schedule? This action cannot be undone.')) {
            return;
        }

        try {
            const response = await Monolith.API.delete(`/firewall/schedules/${id}`);
            if (response.success || response.Success) {
                this.showMessage('Schedule deleted successfully', 'success');
                this.loadSchedules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to delete schedule', 'error');
            }
        } catch (error) {
            console.error('Error deleting schedule:', error);
            this.showMessage('Failed to delete schedule', 'error');
        }
    },

    markPendingChanges: function() {
        $('#applyChangesBanner').removeClass('d-none');
    },

    showMessage: function(message, type) {
        const alert = $('#schedulesStatusMessage');
        if (!alert.length) return;
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    }
};

// Register with Monolith.Pages
if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.Schedules = Schedules;
}