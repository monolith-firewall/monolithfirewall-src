// Firewall Schedules Module
var Schedules = {
    schedules: [],

    init: function() {
        console.log('Initializing Schedules module...');
        this.loadSchedules();
        this.attachEventHandlers();
    },

    loadSchedules: async function() {
        try {
            const response = await Monolith.API.get('/firewall/schedules');
            if (response.success || response.success) {
                const data = response.data || {}; const items = data.items || data || [];
                this.schedules = items.map(s => this.normalizeSchedule(s));
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
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="Schedules.editSchedule(${schedule.id})">Edit</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="Schedules.deleteSchedule(${schedule.id})">Delete</button>
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
    },

    showAddScheduleModal: function() {
        this.showScheduleModal(null);
    },

    showScheduleModal: function(schedule) {
        const isEdit = schedule !== null;
        const timeRanges = schedule && schedule.timeRanges ? schedule.timeRanges : [{ day: 'all', startTime: '00:00', endTime: '23:59' }];
        
        let timeRangesHtml = '';
        timeRanges.forEach((tr, index) => {
            timeRangesHtml += `
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
                            <button type="button" class="btn btn-sm btn-outline-danger" onclick="Schedules.removeTimeRange(${index})">
                                <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                    <path d="M2.146 2.854a.5.5 0 1 1 .708-.708L8 7.293l5.146-5.147a.5.5 0 0 1 .708.708L8.707 8l5.147 5.146a.5.5 0 0 1-.708.708L8 8.707l-5.146 5.147a.5.5 0 0 1-.708-.708L7.293 8 2.146 2.854Z"/>
                                </svg>
                            </button>
                        </div>
                    </div>
                </div>
            `;
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
                                    <button type="button" class="btn btn-sm btn-outline-primary mt-2" onclick="Schedules.addTimeRange()">
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
                            <button type="button" class="btn btn-primary" onclick="Schedules.saveSchedule(${schedule ? schedule.id : 'null'})">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        $('#scheduleModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('scheduleModal'));
        modal.show();
        
        $('#scheduleModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    addTimeRange: function() {
        const container = $('#timeRangesContainer');
        const index = container.children().length;
        const html = `
            <div class="time-range-item mb-2" data-index="${index}">
                <div class="row g-2">
                    <div class="col-md-4">
                        <select class="form-select form-select-sm day-select">
                            <option value="all">All Days</option>
                            <option value="weekdays">Weekdays</option>
                            <option value="weekends">Weekends</option>
                            <option value="monday">Monday</option>
                            <option value="tuesday">Tuesday</option>
                            <option value="wednesday">Wednesday</option>
                            <option value="thursday">Thursday</option>
                            <option value="friday">Friday</option>
                            <option value="saturday">Saturday</option>
                            <option value="sunday">Sunday</option>
                        </select>
                    </div>
                    <div class="col-md-3">
                        <input type="time" class="form-control form-control-sm start-time" value="00:00">
                    </div>
                    <div class="col-md-3">
                        <input type="time" class="form-control form-control-sm end-time" value="23:59">
                    </div>
                    <div class="col-md-2">
                        <button type="button" class="btn btn-sm btn-outline-danger" onclick="Schedules.removeTimeRange(${index})">
                            <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M2.146 2.854a.5.5 0 1 1 .708-.708L8 7.293l5.146-5.147a.5.5 0 0 1 .708.708L8.707 8l5.147 5.146a.5.5 0 0 1-.708.708L8 8.707l-5.146 5.147a.5.5 0 0 1-.708-.708L7.293 8 2.146 2.854Z"/>
                            </svg>
                        </button>
                    </div>
                </div>
            </div>
        `;
        container.append(html);
    },

    removeTimeRange: function(index) {
        $(`.time-range-item[data-index="${index}"]`).remove();
        // Re-index remaining items
        $('#timeRangesContainer .time-range-item').each(function(i) {
            $(this).attr('data-index', i);
            $(this).find('button').attr('onclick', `Schedules.removeTimeRange(${i})`);
        });
    },

    editSchedule: async function(id) {
        try {
            const response = await Monolith.API.get(`/firewall/schedules/${id}`);
            if (response.success || response.success) {
                const schedule = this.normalizeSchedule(response.data || response.data);
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
        if (!form.checkValidity()) {
            form.reportValidity();
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

            if (response.success || response.success) {
                bootstrap.Modal.getInstance(document.getElementById('scheduleModal')).hide();
                this.showMessage(id ? 'Schedule updated successfully' : 'Schedule created successfully', 'success');
                this.loadSchedules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.error || 'Failed to save schedule', 'error');
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
            if (response.success || response.success) {
                this.showMessage('Schedule deleted successfully', 'success');
                this.loadSchedules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.error || 'Failed to delete schedule', 'error');
            }
        } catch (error) {
            console.error('Error deleting schedule:', error);
            this.showMessage('Failed to delete schedule', 'error');
        }
    },

    markPendingChanges: function() {
        $('#applyChangesBanner').removeClass('d-none');
    },

    applyChanges: async function() {
        if (!confirm('Apply all pending firewall changes? This will update the system configuration.')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/apply', {});
            if (response.success || response.success) {
                this.showMessage('Changes applied successfully', 'success');
                $('#applyChangesBanner').addClass('d-none');
            } else {
                this.showMessage(response.error || response.error || 'Failed to apply changes', 'error');
            }
        } catch (error) {
            console.error('Error applying changes:', error);
            this.showMessage('Failed to apply changes', 'error');
        }
    },

    discardChanges: async function() {
        if (!confirm('Discard all pending changes? This will revert all unsaved modifications.')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/discard', {});
            if (response.success || response.success) {
                this.showMessage('Changes discarded', 'info');
                $('#applyChangesBanner').addClass('d-none');
                this.loadSchedules();
            } else {
                this.showMessage(response.error || response.error || 'Failed to discard changes', 'error');
            }
        } catch (error) {
            console.error('Error discarding changes:', error);
            this.showMessage('Failed to discard changes', 'error');
        }
    },

    showMessage: function(message, type) {
        const alert = $('#schedulesStatusMessage');
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.FirewallSchedules = Schedules;
}

// Register with Monolith.Pages
if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.Firewall = Monolith.Pages.Firewall.Firewall || {};
    Monolith.Pages.Firewall.Firewall.Schedules = Schedules;
}
