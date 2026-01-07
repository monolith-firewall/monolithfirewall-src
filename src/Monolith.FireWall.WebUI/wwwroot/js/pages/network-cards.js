// Network Cards Page Module
var NetworkCards = {
    cards: [],
    loading: false,

    init: function() {
        console.log('Initializing Network Cards...');
        
        // Check if we're in advanced settings tab (don't re-render if already there)
        const advancedTabContainer = $('#network-cards-container');
        
        if (advancedTabContainer.length && advancedTabContainer.parent().hasClass('tab-pane')) {
            // Already in advanced settings, just load cards
            this.loadCards();
            return;
        }
        
        // For standalone page, render into page-content (like other pages do)
        this.render();
        this.bindEvents();
        this.loadCards();
    },
    
    render: function() {
        // Not used when in Advanced Settings tab - the tab already has the container
        return;
    },
    
    bindEvents: function() {
        $(document).off('click', '#network-cards-page-refresh');
        $(document).on('click', '#network-cards-page-refresh', () => {
            this.loadCards();
        });
    },

    loadCards: async function() {
        if (this.loading) return;
        
        this.loading = true;
        const container = $('#network-cards-container');
        
        try {
            container.html('<div class="text-center text-muted py-4">Loading network cards...</div>');
            
            const response = await Monolith.API.get('/system/network-cards');
            const data = response.Data || response.data || [];
            
            this.cards = Array.isArray(data) ? data : [];
            this.render();
        } catch (error) {
            console.error('Failed to load network cards:', error);
            container.html(`
                <div class="alert alert-danger">
                    <strong>Error loading network cards:</strong> ${error.message || 'Unknown error'}
                    <br><small>Make sure ethtool and pciutils are installed.</small>
                </div>
            `);
            Monolith.UI.toast('Failed to load network cards', 'error');
        } finally {
            this.loading = false;
        }
    },

    render: function() {
        const container = $('#network-cards-container');
        
        if (!container.length) {
            return;
        }

        if (this.cards.length === 0) {
            container.html(`
                <div class="text-center text-muted py-4">
                    <p>No network cards detected.</p>
                    <small>Make sure ethtool and pciutils packages are installed.</small>
                </div>
            `);
            return;
        }

        const cardsHtml = this.cards.map(card => this.renderCard(card)).join('');
        container.html(`<div class="network-cards-list">${cardsHtml}</div>`);
        
        // Initialize collapse icons rotation
        this.initCollapseIcons();
    },
    
    initCollapseIcons: function() {
        // Rotate chevron icon when collapse state changes
        $('.network-card-item .card-header').on('show.bs.collapse', function() {
            $(this).find('.collapse-icon').css('transform', 'rotate(90deg)');
        }).on('hide.bs.collapse', function() {
            $(this).find('.collapse-icon').css('transform', 'rotate(0deg)');
        });
    },

    renderCard: function(card) {
        const pciInfo = card.PciInfo || card.pciInfo;
        const pciVendor = pciInfo?.Vendor || pciInfo?.vendor || 'Unknown';
        const pciDevice = pciInfo?.Device || pciInfo?.device || 'Unknown';
        const pciSlot = pciInfo?.Slot || pciInfo?.slot || '';
        const driver = card.Driver || card.driver || 'Unknown';
        const speedRaw = card.Speed || card.speed || 'N/A';
        const duplexRaw = card.Duplex || card.duplex || 'N/A';
        const linkDetected = card.LinkDetected || card.linkDetected || 'no';
        const macAddress = card.MacAddress || card.macAddress || 'N/A';
        const busInfo = card.BusInfo || card.busInfo || '';
        const firmwareVersion = card.FirmwareVersion || card.firmwareVersion || 'N/A';
        
        // Extract speed number from string (e.g., "1000Mb/s" -> "1000")
        let speedNumber = '';
        if (speedRaw !== 'N/A' && speedRaw) {
            const speedMatch = speedRaw.match(/(\d+)/);
            if (speedMatch) {
                speedNumber = speedMatch[1];
            }
        }
        const speed = speedRaw;
        const duplex = duplexRaw;
        
        // Normalize duplex (Full -> full, Half -> half)
        const duplexNormalized = duplex.toLowerCase() === 'full' ? 'full' : 
                                 duplex.toLowerCase() === 'half' ? 'half' : '';
        
        const linkStatus = linkDetected === 'yes' ? 'up' : 'down';
        const linkBadgeClass = linkDetected === 'yes' ? 'bg-success' : 'bg-secondary';
        const linkText = linkDetected === 'yes' ? 'Link Up' : 'Link Down';
        const autonegOn = (card.AutoNegotiation || card.autoNegotiation) === 'on';

        const interfaceName = card.Interface || card.interface;
        const collapseId = `network-card-${interfaceName}`;
        
        return `
            <div class="card mb-3 network-card-item" data-interface="${interfaceName}">
                <div class="card-header" style="cursor: pointer;" data-bs-toggle="collapse" data-bs-target="#${collapseId}" aria-expanded="false" aria-controls="${collapseId}">
                    <div class="d-flex justify-content-between align-items-center">
                        <div class="flex-grow-1">
                            <h5 class="mb-0 d-inline-flex align-items-center">
                                <i class="bi bi-chevron-right me-2 collapse-icon" style="transition: transform 0.2s;"></i>
                                <span class="badge ${linkBadgeClass} me-2">${linkText}</span>
                                <strong>${interfaceName}</strong>
                            </h5>
                            <small class="text-muted d-block mt-1">
                                ${pciVendor} ${pciDevice}
                                ${pciSlot ? `(${pciSlot})` : ''}
                            </small>
                        </div>
                        <div class="text-end" onclick="event.stopPropagation();">
                            <button class="btn btn-sm btn-outline-primary me-2" onclick="NetworkCards.refreshCard('${interfaceName}')">
                                <i class="bi bi-arrow-clockwise"></i> Refresh
                            </button>
                            <button class="btn btn-sm btn-success" onclick="NetworkCards.applyAllChanges('${interfaceName}')">
                                <i class="bi bi-check-all"></i> Apply All
                            </button>
                            <button class="btn btn-sm btn-outline-danger ms-2" onclick="NetworkCards.revertToDefaults('${interfaceName}')">
                                <i class="bi bi-arrow-counterclockwise"></i> Revert
                            </button>
                        </div>
                    </div>
                </div>
                <div id="${collapseId}" class="collapse" data-bs-parent=".network-cards-list">
                    <div class="card-body">
                    <div class="row mb-3">
                        <div class="col-md-6">
                            <h6 class="text-muted mb-2">Basic Information</h6>
                            <table class="table table-sm table-borderless mb-0">
                                <tr>
                                    <td class="text-muted" style="width: 40%;">Driver:</td>
                                    <td><code>${driver}</code></td>
                                </tr>
                                <tr>
                                    <td class="text-muted">MAC Address:</td>
                                    <td><code>${macAddress}</code></td>
                                </tr>
                                <tr>
                                    <td class="text-muted">Bus Info:</td>
                                    <td><code>${busInfo || 'N/A'}</code></td>
                                </tr>
                                <tr>
                                    <td class="text-muted">Firmware:</td>
                                    <td>${firmwareVersion}</td>
                                </tr>
                            </table>
                        </div>
                        <div class="col-md-6">
                            <h6 class="text-muted mb-2">Link Status</h6>
                            <table class="table table-sm table-borderless mb-0">
                                <tr>
                                    <td class="text-muted" style="width: 40%;">Status:</td>
                                    <td>
                                        <span class="badge ${linkBadgeClass}">${linkText}</span>
                                    </td>
                                </tr>
                                <tr>
                                    <td class="text-muted">Speed:</td>
                                    <td><strong>${speedRaw}</strong></td>
                                </tr>
                                <tr>
                                    <td class="text-muted">Duplex:</td>
                                    <td><strong>${duplexRaw}</strong></td>
                                </tr>
                                <tr>
                                    <td class="text-muted">Auto-negotiation:</td>
                                    <td>
                                        ${card.AutoNegotiation || card.autoNegotiation === 'on' ? 
                                            '<span class="badge bg-info">On</span>' : 
                                            '<span class="badge bg-secondary">Off</span>'}
                                    </td>
                                </tr>
                            </table>
                        </div>
                    </div>
                    
                    ${pciInfo ? `
                    <div class="mb-3">
                        <h6 class="text-muted mb-2">PCI Information</h6>
                        <table class="table table-sm table-borderless mb-0">
                            <tr>
                                <td class="text-muted" style="width: 20%;">Slot:</td>
                                <td><code>${pciSlot}</code></td>
                            </tr>
                            <tr>
                                <td class="text-muted">Vendor:</td>
                                <td>${pciVendor}</td>
                            </tr>
                            <tr>
                                <td class="text-muted">Device:</td>
                                <td>${pciDevice}</td>
                            </tr>
                            ${pciInfo.SubsystemVendor || pciInfo.subsystemVendor ? `
                            <tr>
                                <td class="text-muted">Subsystem:</td>
                                <td>${pciInfo.SubsystemVendor || pciInfo.subsystemVendor} ${pciInfo.SubsystemDevice || pciInfo.subsystemDevice || ''}</td>
                            </tr>
                            ` : ''}
                        </table>
                    </div>
                    ` : ''}
                    
                    <div class="mt-3 border-top pt-3">
                        <h6 class="text-muted mb-3">Speed & Duplex Configuration</h6>
                        <div class="row g-3">
                            <div class="col-md-4">
                                <label class="form-label small">Speed (Mbps)</label>
                                <select class="form-select form-select-sm" id="card-speed-${card.Interface || card.interface}">
                                    <option value="">Auto (use current)</option>
                                    <option value="10">10</option>
                                    <option value="100">100</option>
                                    <option value="1000">1000</option>
                                    <option value="2500">2500</option>
                                    <option value="5000">5000</option>
                                    <option value="10000">10000</option>
                                    <option value="25000">25000</option>
                                    <option value="40000">40000</option>
                                    <option value="50000">50000</option>
                                    <option value="100000">100000</option>
                                </select>
                                <small class="text-muted">Current: ${speedRaw}</small>
                            </div>
                            <div class="col-md-4">
                                <label class="form-label small">Duplex</label>
                                <select class="form-select form-select-sm" id="card-duplex-${card.Interface || card.interface}">
                                    <option value="">Auto (use current)</option>
                                    <option value="half">Half</option>
                                    <option value="full">Full</option>
                                </select>
                                <small class="text-muted">Current: ${duplexRaw}</small>
                            </div>
                            <div class="col-md-4">
                                <label class="form-label small">Auto-negotiation</label>
                                <div class="form-check form-switch mt-2">
                                    <input class="form-check-input" type="checkbox" id="card-autoneg-${card.Interface || card.interface}" 
                                           ${autonegOn ? 'checked' : ''}>
                                    <label class="form-check-label small" for="card-autoneg-${card.Interface || card.interface}">
                                        Enable auto-negotiation
                                    </label>
                                </div>
                            </div>
                        </div>
                        <div class="mt-3">
                            <button class="btn btn-sm btn-primary" onclick="NetworkCards.applySpeed('${card.Interface || card.interface}')">
                                <i class="bi bi-check-circle"></i> Apply Speed/Duplex Settings
                            </button>
                            <button class="btn btn-sm btn-outline-secondary ms-2" onclick="NetworkCards.resetSpeedForm('${card.Interface || card.interface}')">
                                <i class="bi bi-arrow-counterclockwise"></i> Reset
                            </button>
                        </div>
                        <div class="mt-2">
                            <small class="text-muted">
                                <i class="bi bi-info-circle"></i> Changes are applied immediately. Some settings may require the interface to be restarted.
                            </small>
                        </div>
                    </div>
                    
                    ${this.renderOffloadsSection(card)}
                    ${this.renderBuffersSection(card)}
                    </div>
                </div>
            </div>
        `;
    },

    renderOffloadsSection: function(card) {
        const offloads = card.Offloads || card.offloads || {};
        const interfaceName = card.Interface || card.interface;

        // Group offloads by category
        const segmentationOffloads = [
            { key: 'tso', label: 'TCP Segmentation Offload (TSO)', value: offloads.Tso || offloads.tso },
            { key: 'gso', label: 'Generic Segmentation Offload (GSO)', value: offloads.Gso || offloads.gso },
            { key: 'gro', label: 'Generic Receive Offload (GRO)', value: offloads.Gro || offloads.gro },
            { key: 'lro', label: 'Large Receive Offload (LRO)', value: offloads.Lro || offloads.lro },
            { key: 'ufo', label: 'UDP Fragmentation Offload (UFO)', value: offloads.Ufo || offloads.ufo }
        ];

        const checksumOffloads = [
            { key: 'tx-checksumming', label: 'TX Checksumming', value: offloads.TxChecksumming || offloads.txChecksumming },
            { key: 'rx-checksumming', label: 'RX Checksumming', value: offloads.RxChecksumming || offloads.rxChecksumming },
            { key: 'tx-checksum-ipv4', label: 'TX Checksum IPv4', value: offloads.TxChecksumIpv4 || offloads.txChecksumIpv4 },
            { key: 'tx-checksum-ipv6', label: 'TX Checksum IPv6', value: offloads.TxChecksumIpv6 || offloads.txChecksumIpv6 },
            { key: 'tx-checksum-ip-generic', label: 'TX Checksum IP Generic', value: offloads.TxChecksumIpGeneric || offloads.txChecksumIpGeneric },
            { key: 'tx-checksum-sctp', label: 'TX Checksum SCTP', value: offloads.TxChecksumSctp || offloads.txChecksumSctp },
            { key: 'rx-checksum-ipv4', label: 'RX Checksum IPv4', value: offloads.RxChecksumIpv4 || offloads.rxChecksumIpv4 },
            { key: 'rx-checksum-ipv6', label: 'RX Checksum IPv6', value: offloads.RxChecksumIpv6 || offloads.rxChecksumIpv6 },
            { key: 'rx-checksum-ip-generic', label: 'RX Checksum IP Generic', value: offloads.RxChecksumIpGeneric || offloads.rxChecksumIpGeneric },
            { key: 'rx-checksum-sctp', label: 'RX Checksum SCTP', value: offloads.RxChecksumSctp || offloads.rxChecksumSctp }
        ];

        const vlanOffloads = [
            { key: 'rx-vlan-offload', label: 'RX VLAN Offload', value: offloads.Rxvlan || offloads.rxvlan },
            { key: 'tx-vlan-offload', label: 'TX VLAN Offload', value: offloads.Txvlan || offloads.txvlan },
            { key: 'tx-vlan-stag-hw-insert', label: 'TX VLAN STAG HW Insert', value: offloads.TxvlanStagHwInsert || offloads.txvlanStagHwInsert },
            { key: 'rx-vlan-stag-filter', label: 'RX VLAN STAG Filter', value: offloads.RxvlanStagFilter || offloads.rxvlanStagFilter },
            { key: 'rx-vlan-stag-hw-parse', label: 'RX VLAN STAG HW Parse', value: offloads.RxvlanStagHwParse || offloads.rxvlanStagHwParse }
        ];

        const scatterGatherOffloads = [
            { key: 'scatter-gather', label: 'Scatter-Gather', value: offloads.ScatterGather || offloads.scatterGather },
            { key: 'tx-scatter-gather', label: 'TX Scatter-Gather', value: offloads.TxScatterGather || offloads.txScatterGather },
            { key: 'tx-scatter-gather-fraglist', label: 'TX Scatter-Gather Fraglist', value: offloads.TxScatterGatherFragList || offloads.txScatterGatherFragList },
            { key: 'tx-scatter-gather-ipv4', label: 'TX Scatter-Gather IPv4', value: offloads.TxScatterGatherIpv4 || offloads.txScatterGatherIpv4 },
            { key: 'tx-scatter-gather-ipv6', label: 'TX Scatter-Gather IPv6', value: offloads.TxScatterGatherIpv6 || offloads.txScatterGatherIpv6 }
        ];

        const otherOffloads = [
            { key: 'rx-hashing', label: 'RX Hashing', value: offloads.Rxhash || offloads.rxhash },
            { key: 'rx-all', label: 'RX All', value: offloads.RxAll || offloads.rxAll },
            { key: 'tx-nocache-copy', label: 'TX No Cache Copy', value: offloads.TxNocacheCopy || offloads.txNocacheCopy },
            { key: 'rx-udp_tunnel-port-offload', label: 'RX UDP Tunnel Port Offload', value: offloads.RxUdpTunnelPortOffload || offloads.rxUdpTunnelPortOffload },
            { key: 'tx-udp_tunnel-port-offload', label: 'TX UDP Tunnel Port Offload', value: offloads.TxUdpTunnelPortOffload || offloads.txUdpTunnelPortOffload }
        ];

        const renderOffloadGroup = (title, offloads, category) => {
            if (!offloads || offloads.length === 0) return '';
            
            const toggles = offloads
                .filter(o => o.value !== null && o.value !== undefined)
                .map(o => {
                    const checked = o.value === true || o.value === 'on' || o.value === 'yes';
                    return `
                        <div class="form-check form-switch mb-2">
                            <input class="form-check-input offload-toggle" type="checkbox" 
                                   id="offload-${interfaceName}-${o.key}" 
                                   data-offload-key="${o.key}"
                                   ${checked ? 'checked' : ''}>
                            <label class="form-check-label" for="offload-${interfaceName}-${o.key}">
                                ${o.label}
                            </label>
                        </div>
                    `;
                }).join('');

            if (!toggles) return '';

            return `
                <div class="mb-4">
                    <h6 class="text-muted mb-3">${title}</h6>
                    <div class="ps-3">
                        ${toggles}
                    </div>
                </div>
            `;
        };

        const hasAnyOffloads = segmentationOffloads.some(o => o.value !== null && o.value !== undefined) ||
                               checksumOffloads.some(o => o.value !== null && o.value !== undefined) ||
                               vlanOffloads.some(o => o.value !== null && o.value !== undefined) ||
                               scatterGatherOffloads.some(o => o.value !== null && o.value !== undefined) ||
                               otherOffloads.some(o => o.value !== null && o.value !== undefined);

        if (!hasAnyOffloads) {
            return `
                <div class="mt-3 border-top pt-3">
                    <h6 class="text-muted mb-3">Offload Features</h6>
                    <div class="alert alert-info mb-0">
                        <small>No offload features detected for this interface. This may be a virtual interface or the driver does not support offload configuration.</small>
                    </div>
                </div>
            `;
        }

        return `
            <div class="mt-3 border-top pt-3">
                <h6 class="text-muted mb-3">Offload Features</h6>
                ${renderOffloadGroup('Segmentation Offloads', segmentationOffloads, 'segmentation')}
                ${renderOffloadGroup('Checksum Offloads', checksumOffloads, 'checksum')}
                ${renderOffloadGroup('VLAN Offloads', vlanOffloads, 'vlan')}
                ${renderOffloadGroup('Scatter-Gather Offloads', scatterGatherOffloads, 'scatter-gather')}
                ${renderOffloadGroup('Other Offloads', otherOffloads, 'other')}
                <div class="mt-3">
                    <button class="btn btn-sm btn-primary" onclick="NetworkCards.applyOffloads('${interfaceName}')">
                        <i class="bi bi-check-circle"></i> Apply Offload Settings
                    </button>
                    <button class="btn btn-sm btn-outline-secondary ms-2" onclick="NetworkCards.resetOffloadsForm('${interfaceName}')">
                        <i class="bi bi-arrow-counterclockwise"></i> Reset
                    </button>
                </div>
                <div class="mt-2">
                    <small class="text-muted">
                        <i class="bi bi-info-circle"></i> Offload features can improve performance by offloading processing to the network card hardware.
                    </small>
                </div>
            </div>
        `;
    },

    refreshCard: async function(interfaceName) {
        try {
            const response = await Monolith.API.get(`/system/network-cards/${encodeURIComponent(interfaceName)}`);
            const card = response.Data || response.data;
            
            if (card) {
                // Update the card in our list
                const index = this.cards.findIndex(c => 
                    (c.Interface || c.interface) === interfaceName
                );
                if (index >= 0) {
                    this.cards[index] = card;
                } else {
                    this.cards.push(card);
                }
                
                // Re-render
                this.render();
                this.initCollapseIcons();
                Monolith.UI.toast('Network card information refreshed', 'success');
            }
        } catch (error) {
            console.error('Failed to refresh card:', error);
            Monolith.UI.toast(`Failed to refresh card: ${error.message || 'Unknown error'}`, 'error');
        }
    },

    applySpeed: async function(interfaceName) {
        const speedSelect = $(`#card-speed-${interfaceName}`);
        const duplexSelect = $(`#card-duplex-${interfaceName}`);
        const autonegCheckbox = $(`#card-autoneg-${interfaceName}`);
        const applyButton = speedSelect.closest('.mt-3').find('button');

        // Get values
        const speed = speedSelect.val()?.trim() || null;
        const duplex = duplexSelect.val()?.trim() || null;
        const autoneg = autonegCheckbox.is(':checked');

        // Validation: if autoneg is off, speed and duplex must be specified
        if (!autoneg && (!speed || !duplex)) {
            Monolith.UI.toast('Speed and Duplex must be specified when auto-negotiation is disabled', 'warning');
            return;
        }

        // Build request
        const request = {
            Interface: interfaceName,
            AutoNegotiation: autoneg
        };

        if (speed) {
            request.Speed = speed;
        }

        if (duplex) {
            request.Duplex = duplex;
        }

        // If autoneg is on and no speed/duplex specified, that's fine (just enable autoneg)
        if (autoneg && !speed && !duplex) {
            // Just enabling autoneg, which is valid
        }

        try {
            // Disable button and show loading
            const originalText = applyButton.html();
            applyButton.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Applying...');

            const response = await Monolith.API.post(`/system/network-cards/${encodeURIComponent(interfaceName)}/speed`, request);

            if (response.Success || response.success) {
                Monolith.UI.toast('Speed/Duplex settings applied successfully', 'success');
                
                // Refresh card info to show updated values
                await this.refreshCard(interfaceName);
            } else {
                const errorMsg = response.Error || response.error || 'Unknown error';
                Monolith.UI.toast(`Failed to apply settings: ${errorMsg}`, 'error');
            }
        } catch (error) {
            console.error('Failed to apply speed/duplex:', error);
            Monolith.UI.toast(`Failed to apply settings: ${error.message || 'Unknown error'}`, 'error');
        } finally {
            // Re-enable button
            applyButton.prop('disabled', false).html('<i class="bi bi-check-circle"></i> Apply Speed/Duplex Settings');
        }
    },

    resetSpeedForm: function(interfaceName) {
        const card = this.cards.find(c => (c.Interface || c.interface) === interfaceName);
        if (!card) return;

        const speedSelect = $(`#card-speed-${interfaceName}`);
        const duplexSelect = $(`#card-duplex-${interfaceName}`);
        const autonegCheckbox = $(`#card-autoneg-${interfaceName}`);

        // Reset to current values
        speedSelect.val('');
        duplexSelect.val('');
        autonegCheckbox.prop('checked', (card.AutoNegotiation || card.autoNegotiation) === 'on');

        Monolith.UI.toast('Form reset to current values', 'info');
    },

    applyOffloads: async function(interfaceName) {
        // Collect all offload toggles for this interface
        const toggles = $(`.offload-toggle[id^="offload-${interfaceName}-"]`);
        const offloads = {};

        // Map frontend keys to ethtool keys
        const keyMapping = {
            'tso': 'tcp-segmentation-offload',
            'gso': 'generic-segmentation-offload',
            'gro': 'generic-receive-offload',
            'lro': 'large-receive-offload',
            'ufo': 'udp-fragmentation-offload',
            'tx-checksumming': 'tx-checksumming',
            'rx-checksumming': 'rx-checksumming',
            'tx-checksum-ipv4': 'tx-checksum-ipv4',
            'tx-checksum-ipv6': 'tx-checksum-ipv6',
            'tx-checksum-ip-generic': 'tx-checksum-ip-generic',
            'tx-checksum-sctp': 'tx-checksum-sctp',
            'rx-checksum-ipv4': 'rx-checksum-ipv4',
            'rx-checksum-ipv6': 'rx-checksum-ipv6',
            'rx-checksum-ip-generic': 'rx-checksum-ip-generic',
            'rx-checksum-sctp': 'rx-checksum-sctp',
            'rx-vlan-offload': 'rx-vlan-offload',
            'tx-vlan-offload': 'tx-vlan-offload',
            'tx-vlan-stag-hw-insert': 'tx-vlan-stag-hw-insert',
            'rx-vlan-stag-filter': 'rx-vlan-stag-filter',
            'rx-vlan-stag-hw-parse': 'rx-vlan-stag-hw-parse',
            'scatter-gather': 'scatter-gather',
            'tx-scatter-gather': 'tx-scatter-gather',
            'tx-scatter-gather-fraglist': 'tx-scatter-gather-fraglist',
            'tx-scatter-gather-ipv4': 'tx-scatter-gather-ipv4',
            'tx-scatter-gather-ipv6': 'tx-scatter-gather-ipv6',
            'rx-hashing': 'rx-hashing',
            'rx-all': 'rx-all',
            'tx-nocache-copy': 'tx-nocache-copy',
            'rx-udp_tunnel-port-offload': 'rx-udp_tunnel-port-offload',
            'tx-udp_tunnel-port-offload': 'tx-udp_tunnel-port-offload'
        };

        toggles.each((_, toggle) => {
            const $toggle = $(toggle);
            const frontendKey = $toggle.data('offload-key');
            const enabled = $toggle.is(':checked');
            if (frontendKey) {
                const ethtoolKey = keyMapping[frontendKey] || frontendKey;
                offloads[ethtoolKey] = enabled;
            }
        });

        if (Object.keys(offloads).length === 0) {
            Monolith.UI.toast('No offload settings to apply', 'info');
            return;
        }

        const request = {
            Interface: interfaceName,
            Offloads: offloads
        };

        // Find the apply button
        const applyButton = $(`.network-card-item[data-interface="${interfaceName}"]`)
            .find('button[onclick*="applyOffloads"]');

        try {
            // Disable button and show loading
            const originalText = applyButton.html();
            applyButton.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Applying...');

            const response = await Monolith.API.post(`/system/network-cards/${encodeURIComponent(interfaceName)}/offloads`, request);

            if (response.Success || response.success) {
                Monolith.UI.toast('Offload settings applied successfully', 'success');
                
                // Refresh card info to show updated values
                await this.refreshCard(interfaceName);
            } else {
                const errorMsg = response.Error || response.error || 'Unknown error';
                Monolith.UI.toast(`Failed to apply offload settings: ${errorMsg}`, 'error');
            }
        } catch (error) {
            console.error('Failed to apply offloads:', error);
            Monolith.UI.toast(`Failed to apply offload settings: ${error.message || 'Unknown error'}`, 'error');
        } finally {
            // Re-enable button
            applyButton.prop('disabled', false).html('<i class="bi bi-check-circle"></i> Apply Offload Settings');
        }
    },

    resetOffloadsForm: function(interfaceName) {
        const card = this.cards.find(c => (c.Interface || c.interface) === interfaceName);
        if (!card) return;

        const offloads = card.Offloads || card.offloads || {};
        
        // Reset all toggles to current values
        const toggles = $(`.offload-toggle[id^="offload-${interfaceName}-"]`);
        toggles.each((_, toggle) => {
            const $toggle = $(toggle);
            const key = $toggle.data('offload-key');
            
            // Map key to property name
            let value = null;
            switch (key) {
                case 'tso': value = offloads.Tso || offloads.tso; break;
                case 'gso': value = offloads.Gso || offloads.gso; break;
                case 'gro': value = offloads.Gro || offloads.gro; break;
                case 'lro': value = offloads.Lro || offloads.lro; break;
                case 'ufo': value = offloads.Ufo || offloads.ufo; break;
                case 'tx-checksumming': value = offloads.TxChecksumming || offloads.txChecksumming; break;
                case 'rx-checksumming': value = offloads.RxChecksumming || offloads.rxChecksumming; break;
                case 'tx-checksum-ipv4': value = offloads.TxChecksumIpv4 || offloads.txChecksumIpv4; break;
                case 'tx-checksum-ipv6': value = offloads.TxChecksumIpv6 || offloads.txChecksumIpv6; break;
                case 'tx-checksum-ip-generic': value = offloads.TxChecksumIpGeneric || offloads.txChecksumIpGeneric; break;
                case 'tx-checksum-sctp': value = offloads.TxChecksumSctp || offloads.txChecksumSctp; break;
                case 'rx-checksum-ipv4': value = offloads.RxChecksumIpv4 || offloads.rxChecksumIpv4; break;
                case 'rx-checksum-ipv6': value = offloads.RxChecksumIpv6 || offloads.rxChecksumIpv6; break;
                case 'rx-checksum-ip-generic': value = offloads.RxChecksumIpGeneric || offloads.rxChecksumIpGeneric; break;
                case 'rx-checksum-sctp': value = offloads.RxChecksumSctp || offloads.rxChecksumSctp; break;
                case 'rx-vlan-offload': value = offloads.Rxvlan || offloads.rxvlan; break;
                case 'tx-vlan-offload': value = offloads.Txvlan || offloads.txvlan; break;
                case 'tx-vlan-stag-hw-insert': value = offloads.TxvlanStagHwInsert || offloads.txvlanStagHwInsert; break;
                case 'rx-vlan-stag-filter': value = offloads.RxvlanStagFilter || offloads.rxvlanStagFilter; break;
                case 'rx-vlan-stag-hw-parse': value = offloads.RxvlanStagHwParse || offloads.rxvlanStagHwParse; break;
                case 'scatter-gather': value = offloads.ScatterGather || offloads.scatterGather; break;
                case 'tx-scatter-gather': value = offloads.TxScatterGather || offloads.txScatterGather; break;
                case 'tx-scatter-gather-fraglist': value = offloads.TxScatterGatherFragList || offloads.txScatterGatherFragList; break;
                case 'tx-scatter-gather-ipv4': value = offloads.TxScatterGatherIpv4 || offloads.txScatterGatherIpv4; break;
                case 'tx-scatter-gather-ipv6': value = offloads.TxScatterGatherIpv6 || offloads.txScatterGatherIpv6; break;
                case 'rx-hashing': value = offloads.Rxhash || offloads.rxhash; break;
                case 'rx-all': value = offloads.RxAll || offloads.rxAll; break;
                case 'tx-nocache-copy': value = offloads.TxNocacheCopy || offloads.txNocacheCopy; break;
                case 'rx-udp_tunnel-port-offload': value = offloads.RxUdpTunnelPortOffload || offloads.rxUdpTunnelPortOffload; break;
                case 'tx-udp_tunnel-port-offload': value = offloads.TxUdpTunnelPortOffload || offloads.txUdpTunnelPortOffload; break;
            }

            if (value !== null && value !== undefined) {
                const checked = value === true || value === 'on' || value === 'yes';
                $toggle.prop('checked', checked);
            }
        });

        Monolith.UI.toast('Offload form reset to current values', 'info');
    },

    renderBuffersSection: function(card) {
        const buffers = card.Buffers || card.buffers || {};
        const interfaceName = card.Interface || card.interface;

        const bufferTypes = [
            { 
                key: 'rx-mini', 
                label: 'RX Mini', 
                current: buffers.RxMini || buffers.rxMini, 
                max: buffers.RxMiniMax || buffers.rxMiniMax 
            },
            { 
                key: 'rx', 
                label: 'RX', 
                current: buffers.Rx || buffers.rx, 
                max: buffers.RxMax || buffers.rxMax 
            },
            { 
                key: 'rx-jumbo', 
                label: 'RX Jumbo', 
                current: buffers.RxJumbo || buffers.rxJumbo, 
                max: buffers.RxJumboMax || buffers.rxJumboMax 
            },
            { 
                key: 'tx', 
                label: 'TX', 
                current: buffers.Tx || buffers.tx, 
                max: buffers.TxMax || buffers.txMax 
            }
        ];

        // Filter out buffer types that don't have values
        const availableBuffers = bufferTypes.filter(b => 
            (b.current !== null && b.current !== undefined) || 
            (b.max !== null && b.max !== undefined)
        );

        if (availableBuffers.length === 0) {
            return `
                <div class="mt-3 border-top pt-3">
                    <h6 class="text-muted mb-3">Ring Buffers</h6>
                    <div class="alert alert-info mb-0">
                        <small>No ring buffer information available for this interface. This may be a virtual interface or the driver does not support buffer configuration.</small>
                    </div>
                </div>
            `;
        }

        const bufferInputs = availableBuffers.map(buffer => {
            const currentValue = buffer.current !== null && buffer.current !== undefined ? buffer.current : '';
            const maxValue = buffer.max !== null && buffer.max !== undefined ? buffer.max : '';
            const maxText = maxValue ? ` (Max: ${maxValue})` : '';
            
            return `
                <div class="col-md-6 mb-3">
                    <label class="form-label small">
                        ${buffer.label}${maxText}
                    </label>
                    <div class="input-group input-group-sm">
                        <input type="number" 
                               class="form-control buffer-input" 
                               id="buffer-${interfaceName}-${buffer.key}"
                               data-buffer-key="${buffer.key}"
                               data-buffer-max="${maxValue || ''}"
                               value="${currentValue}"
                               min="0"
                               ${maxValue ? `max="${maxValue}"` : ''}
                               placeholder="Current: ${currentValue || 'N/A'}">
                        <span class="input-group-text">entries</span>
                    </div>
                    <small class="text-muted">Current: ${currentValue || 'N/A'}${maxText}</small>
                </div>
            `;
        }).join('');

        return `
            <div class="mt-3 border-top pt-3">
                <h6 class="text-muted mb-3">Ring Buffers</h6>
                <p class="text-muted small mb-3">
                    Ring buffers control how many packets the network card can queue before dropping. 
                    Larger buffers can improve performance under high load but use more memory.
                </p>
                <div class="row g-3">
                    ${bufferInputs}
                </div>
                <div class="mt-3">
                    <button class="btn btn-sm btn-primary" onclick="NetworkCards.applyBuffers('${interfaceName}')">
                        <i class="bi bi-check-circle"></i> Apply Buffer Settings
                    </button>
                    <button class="btn btn-sm btn-outline-secondary ms-2" onclick="NetworkCards.resetBuffersForm('${interfaceName}')">
                        <i class="bi bi-arrow-counterclockwise"></i> Reset
                    </button>
                </div>
                <div class="mt-2">
                    <small class="text-muted">
                        <i class="bi bi-info-circle"></i> Buffer changes are applied immediately. Some drivers may require an interface restart.
                    </small>
                </div>
            </div>
        `;
    },

    applyBuffers: async function(interfaceName) {
        // Collect all buffer inputs for this interface
        const inputs = $(`.buffer-input[id^="buffer-${interfaceName}-"]`);
        const buffers = {};

        inputs.each((_, input) => {
            const $input = $(input);
            const key = $input.data('buffer-key');
            const value = $input.val()?.trim();
            
            if (key && value && value !== '') {
                const intValue = parseInt(value, 10);
                if (!isNaN(intValue) && intValue >= 0) {
                    // Map frontend keys to ethtool keys (ethtool uses lowercase without dashes)
                    const ethtoolKey = key.replace(/-/g, '').toLowerCase();
                    buffers[ethtoolKey] = intValue;
                }
            }
        });

        if (Object.keys(buffers).length === 0) {
            Monolith.UI.toast('No buffer settings to apply', 'info');
            return;
        }

        // Validate against maximums
        let hasError = false;
        inputs.each((_, input) => {
            const $input = $(input);
            const key = $input.data('buffer-key');
            const maxValue = $input.data('buffer-max');
            const value = $input.val()?.trim();
            
            if (value && maxValue) {
                const intValue = parseInt(value, 10);
                const intMax = parseInt(maxValue, 10);
                if (!isNaN(intValue) && !isNaN(intMax) && intValue > intMax) {
                    Monolith.UI.toast(`${key} value (${intValue}) exceeds maximum (${intMax})`, 'error');
                    hasError = true;
                }
            }
        });

        if (hasError) {
            return;
        }

        const request = {
            Interface: interfaceName,
            Buffers: buffers
        };

        // Find the apply button
        const applyButton = $(`.network-card-item[data-interface="${interfaceName}"]`)
            .find('button[onclick*="applyBuffers"]');

        try {
            // Disable button and show loading
            const originalText = applyButton.html();
            applyButton.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Applying...');

            const response = await Monolith.API.post(`/system/network-cards/${encodeURIComponent(interfaceName)}/buffers`, request);

            if (response.Success || response.success) {
                Monolith.UI.toast('Buffer settings applied successfully', 'success');
                
                // Refresh card info to show updated values
                await this.refreshCard(interfaceName);
            } else {
                const errorMsg = response.Error || response.error || 'Unknown error';
                Monolith.UI.toast(`Failed to apply buffer settings: ${errorMsg}`, 'error');
            }
        } catch (error) {
            console.error('Failed to apply buffers:', error);
            Monolith.UI.toast(`Failed to apply buffer settings: ${error.message || 'Unknown error'}`, 'error');
        } finally {
            // Re-enable button
            applyButton.prop('disabled', false).html('<i class="bi bi-check-circle"></i> Apply Buffer Settings');
        }
    },

    resetBuffersForm: function(interfaceName) {
        const card = this.cards.find(c => (c.Interface || c.interface) === interfaceName);
        if (!card) return;

        const buffers = card.Buffers || card.buffers || {};
        
        // Reset all buffer inputs to current values
        const inputs = $(`.buffer-input[id^="buffer-${interfaceName}-"]`);
        inputs.each((_, input) => {
            const $input = $(input);
            const key = $input.data('buffer-key');
            
            // Map key to property name
            let value = null;
            switch (key) {
                case 'rx-mini': value = buffers.RxMini || buffers.rxMini; break;
                case 'rx': value = buffers.Rx || buffers.rx; break;
                case 'rx-jumbo': value = buffers.RxJumbo || buffers.rxJumbo; break;
                case 'tx': value = buffers.Tx || buffers.tx; break;
            }

            if (value !== null && value !== undefined) {
                $input.val(value);
            } else {
                $input.val('');
            }
        });

        Monolith.UI.toast('Buffer form reset to current values', 'info');
    },

    applyAllChanges: async function(interfaceName) {
        // Show confirmation dialog
        const confirmed = await new Promise((resolve) => {
            const modalId = 'modal-apply-all-' + Date.now();
            const modalHtml = `
                <div class="modal fade" id="${modalId}" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">Apply All Changes</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                            </div>
                            <div class="modal-body">
                                <p>Are you sure you want to apply all changes for interface <strong>${interfaceName}</strong>?</p>
                                <p>This will apply:</p>
                                <ul>
                                    <li>Speed/Duplex settings</li>
                                    <li>Offload features</li>
                                    <li>Ring buffer settings</li>
                                </ul>
                                <p class="text-muted small">Changes are applied immediately and may affect network connectivity.</p>
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                                <button type="button" class="btn btn-primary" id="${modalId}-confirm">Apply All</button>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            
            $('body').append(modalHtml);
            const modal = new bootstrap.Modal(document.getElementById(modalId));
            modal.show();
            
            $(`#${modalId}-confirm`).on('click', () => {
                modal.hide();
                $(`#${modalId}`).remove();
                resolve(true);
            });
            
            $(`#${modalId}`).on('hidden.bs.modal', () => {
                if (!$(`#${modalId}-confirm`).is(':focus')) {
                    resolve(false);
                }
                $(`#${modalId}`).remove();
            });
        });

        if (!confirmed) {
            return;
        }

        const cardElement = $(`.network-card-item[data-interface="${interfaceName}"]`);
        const applyAllButton = cardElement.find('button[onclick*="applyAllChanges"]');
        
        try {
            // Disable button and show loading
            const originalText = applyAllButton.html();
            applyAllButton.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Applying...');

            const results = {
                speed: false,
                offloads: false,
                buffers: false,
                errors: []
            };

            // 1. Apply speed/duplex if changed
            const speedSelect = $(`#card-speed-${interfaceName}`);
            const duplexSelect = $(`#card-duplex-${interfaceName}`);
            const autonegCheckbox = $(`#card-autoneg-${interfaceName}`);
            
            const speed = speedSelect.val()?.trim() || null;
            const duplex = duplexSelect.val()?.trim() || null;
            const autoneg = autonegCheckbox.is(':checked');
            const card = this.cards.find(c => (c.Interface || c.interface) === interfaceName);
            const currentAutoneg = card && (card.AutoNegotiation || card.autoNegotiation) === 'on';
            
            const speedChanged = speed !== null || duplex !== null || autoneg !== currentAutoneg;
            
            if (speedChanged) {
                try {
                    const speedRequest = {
                        Interface: interfaceName,
                        AutoNegotiation: autoneg
                    };
                    if (speed) speedRequest.Speed = speed;
                    if (duplex) speedRequest.Duplex = duplex;

                    const response = await Monolith.API.post(`/system/network-cards/${encodeURIComponent(interfaceName)}/speed`, speedRequest);
                    if (response.Success || response.success) {
                        results.speed = true;
                    } else {
                        results.errors.push(`Speed/Duplex: ${response.Error || response.error || 'Failed'}`);
                    }
                } catch (error) {
                    results.errors.push(`Speed/Duplex: ${error.message || 'Unknown error'}`);
                }
            } else {
                results.speed = true; // No changes, skip
            }

            // 2. Apply offloads if changed
            const offloadToggles = $(`.offload-toggle[id^="offload-${interfaceName}-"]`);
            const offloads = {};
            let offloadsChanged = false;

            const keyMapping = {
                'tso': 'tcp-segmentation-offload',
                'gso': 'generic-segmentation-offload',
                'gro': 'generic-receive-offload',
                'lro': 'large-receive-offload',
                'ufo': 'udp-fragmentation-offload',
                'tx-checksumming': 'tx-checksumming',
                'rx-checksumming': 'rx-checksumming',
                'tx-checksum-ipv4': 'tx-checksum-ipv4',
                'tx-checksum-ipv6': 'tx-checksum-ipv6',
                'tx-checksum-ip-generic': 'tx-checksum-ip-generic',
                'tx-checksum-sctp': 'tx-checksum-sctp',
                'rx-checksum-ipv4': 'rx-checksum-ipv4',
                'rx-checksum-ipv6': 'rx-checksum-ipv6',
                'rx-checksum-ip-generic': 'rx-checksum-ip-generic',
                'rx-checksum-sctp': 'rx-checksum-sctp',
                'rx-vlan-offload': 'rx-vlan-offload',
                'tx-vlan-offload': 'tx-vlan-offload',
                'tx-vlan-stag-hw-insert': 'tx-vlan-stag-hw-insert',
                'rx-vlan-stag-filter': 'rx-vlan-stag-filter',
                'rx-vlan-stag-hw-parse': 'rx-vlan-stag-hw-parse',
                'scatter-gather': 'scatter-gather',
                'tx-scatter-gather': 'tx-scatter-gather',
                'tx-scatter-gather-fraglist': 'tx-scatter-gather-fraglist',
                'tx-scatter-gather-ipv4': 'tx-scatter-gather-ipv4',
                'tx-scatter-gather-ipv6': 'tx-scatter-gather-ipv6',
                'rx-hashing': 'rx-hashing',
                'rx-all': 'rx-all',
                'tx-nocache-copy': 'tx-nocache-copy',
                'rx-udp_tunnel-port-offload': 'rx-udp_tunnel-port-offload',
                'tx-udp_tunnel-port-offload': 'tx-udp_tunnel-port-offload'
            };

            offloadToggles.each((_, toggle) => {
                const $toggle = $(toggle);
                const frontendKey = $toggle.data('offload-key');
                const enabled = $toggle.is(':checked');
                
                if (frontendKey) {
                    const ethtoolKey = keyMapping[frontendKey] || frontendKey;
                    const currentValue = card && card.Offloads ? 
                        (this.getOffloadValue(card.Offloads, frontendKey) === true || 
                         this.getOffloadValue(card.Offloads, frontendKey) === 'on') : false;
                    
                    if (enabled !== currentValue) {
                        offloadsChanged = true;
                        offloads[ethtoolKey] = enabled;
                    }
                }
            });

            if (offloadsChanged && Object.keys(offloads).length > 0) {
                try {
                    const offloadRequest = {
                        Interface: interfaceName,
                        Offloads: offloads
                    };
                    const response = await Monolith.API.post(`/system/network-cards/${encodeURIComponent(interfaceName)}/offloads`, offloadRequest);
                    if (response.Success || response.success) {
                        results.offloads = true;
                    } else {
                        results.errors.push(`Offloads: ${response.Error || response.error || 'Failed'}`);
                    }
                } catch (error) {
                    results.errors.push(`Offloads: ${error.message || 'Unknown error'}`);
                }
            } else {
                results.offloads = true; // No changes, skip
            }

            // 3. Apply buffers if changed
            const bufferInputs = $(`.buffer-input[id^="buffer-${interfaceName}-"]`);
            const buffers = {};
            let buffersChanged = false;

            bufferInputs.each((_, input) => {
                const $input = $(input);
                const key = $input.data('buffer-key');
                const value = $input.val()?.trim();
                
                if (key && value && value !== '') {
                    const intValue = parseInt(value, 10);
                    if (!isNaN(intValue) && intValue >= 0) {
                        const ethtoolKey = key.replace(/-/g, '').toLowerCase();
                        const currentValue = this.getBufferValue(card, key);
                        
                        if (intValue !== currentValue) {
                            buffersChanged = true;
                            buffers[ethtoolKey] = intValue;
                        }
                    }
                }
            });

            if (buffersChanged && Object.keys(buffers).length > 0) {
                // Validate against maximums
                let hasValidationError = false;
                bufferInputs.each((_, input) => {
                    const $input = $(input);
                    const key = $input.data('buffer-key');
                    const maxValue = $input.data('buffer-max');
                    const value = $input.val()?.trim();
                    
                    if (value && maxValue) {
                        const intValue = parseInt(value, 10);
                        const intMax = parseInt(maxValue, 10);
                        if (!isNaN(intValue) && !isNaN(intMax) && intValue > intMax) {
                            results.errors.push(`${key} value (${intValue}) exceeds maximum (${intMax})`);
                            hasValidationError = true;
                        }
                    }
                });

                if (!hasValidationError) {
                    try {
                        const bufferRequest = {
                            Interface: interfaceName,
                            Buffers: buffers
                        };
                        const response = await Monolith.API.post(`/system/network-cards/${encodeURIComponent(interfaceName)}/buffers`, bufferRequest);
                        if (response.Success || response.success) {
                            results.buffers = true;
                        } else {
                            results.errors.push(`Buffers: ${response.Error || response.error || 'Failed'}`);
                        }
                    } catch (error) {
                        results.errors.push(`Buffers: ${error.message || 'Unknown error'}`);
                    }
                }
            } else {
                results.buffers = true; // No changes, skip
            }

            // Show results
            if (results.errors.length === 0) {
                const applied = [];
                if (results.speed && speedChanged) applied.push('Speed/Duplex');
                if (results.offloads && offloadsChanged) applied.push('Offloads');
                if (results.buffers && buffersChanged) applied.push('Buffers');
                
                if (applied.length > 0) {
                    Monolith.UI.toast(`Successfully applied: ${applied.join(', ')}`, 'success');
                } else {
                    Monolith.UI.toast('No changes to apply', 'info');
                }
                
                // Refresh card info
                await this.refreshCard(interfaceName);
            } else {
                const errorMsg = results.errors.join('<br>');
                Monolith.UI.showModal(
                    'Apply All Changes - Errors',
                    `<div class="alert alert-danger">Some changes failed to apply:</div><ul class="mb-0">${results.errors.map(e => `<li>${e}</li>`).join('')}</ul>`,
                    { size: 'md' }
                );
            }
        } catch (error) {
            console.error('Failed to apply all changes:', error);
            Monolith.UI.toast(`Failed to apply changes: ${error.message || 'Unknown error'}`, 'error');
        } finally {
            // Re-enable button
            applyAllButton.prop('disabled', false).html('<i class="bi bi-check-all"></i> Apply All Changes');
        }
    },

    revertToDefaults: async function(interfaceName) {
        // Show confirmation dialog
        const confirmed = await new Promise((resolve) => {
            const modalId = 'modal-revert-' + Date.now();
            const modalHtml = `
                <div class="modal fade" id="${modalId}" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">Revert to Defaults</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                            </div>
                            <div class="modal-body">
                                <p>Are you sure you want to revert interface <strong>${interfaceName}</strong> to default settings?</p>
                                <p>This will reset:</p>
                                <ul>
                                    <li>Speed/Duplex to auto-negotiation</li>
                                    <li>All offload features to driver defaults</li>
                                    <li>All ring buffers to driver defaults</li>
                                </ul>
                                <div class="alert alert-warning mb-0">
                                    <strong>Warning:</strong> This action cannot be undone and may affect network connectivity.
                                </div>
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                                <button type="button" class="btn btn-danger" id="${modalId}-confirm">Revert to Defaults</button>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            
            $('body').append(modalHtml);
            const modal = new bootstrap.Modal(document.getElementById(modalId));
            modal.show();
            
            $(`#${modalId}-confirm`).on('click', () => {
                modal.hide();
                $(`#${modalId}`).remove();
                resolve(true);
            });
            
            $(`#${modalId}`).on('hidden.bs.modal', () => {
                if (!$(`#${modalId}-confirm`).is(':focus')) {
                    resolve(false);
                }
                $(`#${modalId}`).remove();
            });
        });

        if (!confirmed) {
            return;
        }

        const cardElement = $(`.network-card-item[data-interface="${interfaceName}"]`);
        const revertButton = cardElement.find('button[onclick*="revertToDefaults"]');
        
        try {
            // Disable button and show loading
            const originalText = revertButton.html();
            revertButton.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Reverting...');

            const response = await Monolith.API.post(`/system/network-cards/${encodeURIComponent(interfaceName)}/revert`, {});

            if (response.Success || response.success) {
                Monolith.UI.toast('Interface reverted to default settings', 'success');
                
                // Refresh card info to show updated values
                await this.refreshCard(interfaceName);
            } else {
                const errorMsg = response.Error || response.error || 'Unknown error';
                Monolith.UI.toast(`Failed to revert to defaults: ${errorMsg}`, 'error');
            }
        } catch (error) {
            console.error('Failed to revert to defaults:', error);
            Monolith.UI.toast(`Failed to revert to defaults: ${error.message || 'Unknown error'}`, 'error');
        } finally {
            // Re-enable button
            revertButton.prop('disabled', false).html('<i class="bi bi-arrow-counterclockwise"></i> Revert to Defaults');
        }
    },

    getOffloadValue: function(offloads, key) {
        const mapping = {
            'tso': 'Tso',
            'gso': 'Gso',
            'gro': 'Gro',
            'lro': 'Lro',
            'ufo': 'Ufo',
            'tx-checksumming': 'TxChecksumming',
            'rx-checksumming': 'RxChecksumming',
            'tx-checksum-ipv4': 'TxChecksumIpv4',
            'tx-checksum-ipv6': 'TxChecksumIpv6',
            'tx-checksum-ip-generic': 'TxChecksumIpGeneric',
            'tx-checksum-sctp': 'TxChecksumSctp',
            'rx-checksum-ipv4': 'RxChecksumIpv4',
            'rx-checksum-ipv6': 'RxChecksumIpv6',
            'rx-checksum-ip-generic': 'RxChecksumIpGeneric',
            'rx-checksum-sctp': 'RxChecksumSctp',
            'rx-vlan-offload': 'Rxvlan',
            'tx-vlan-offload': 'Txvlan',
            'tx-vlan-stag-hw-insert': 'TxvlanStagHwInsert',
            'rx-vlan-stag-filter': 'RxvlanStagFilter',
            'rx-vlan-stag-hw-parse': 'RxvlanStagHwParse',
            'scatter-gather': 'ScatterGather',
            'tx-scatter-gather': 'TxScatterGather',
            'tx-scatter-gather-fraglist': 'TxScatterGatherFragList',
            'tx-scatter-gather-ipv4': 'TxScatterGatherIpv4',
            'tx-scatter-gather-ipv6': 'TxScatterGatherIpv6',
            'rx-hashing': 'Rxhash',
            'rx-all': 'RxAll',
            'tx-nocache-copy': 'TxNocacheCopy',
            'rx-udp_tunnel-port-offload': 'RxUdpTunnelPortOffload',
            'tx-udp_tunnel-port-offload': 'TxUdpTunnelPortOffload'
        };

        const propName = mapping[key];
        if (!propName || !offloads) return null;
        
        return offloads[propName] || offloads[propName.toLowerCase()] || null;
    },

    getBufferValue: function(card, key) {
        if (!card || !card.Buffers) return null;
        
        const buffers = card.Buffers || card.buffers || {};
        const mapping = {
            'rx-mini': 'RxMini',
            'rx': 'Rx',
            'rx-jumbo': 'RxJumbo',
            'tx': 'Tx'
        };

        const propName = mapping[key];
        if (!propName) return null;
        
        return buffers[propName] || buffers[propName.toLowerCase()] || null;
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.NetworkCards = NetworkCards;
}
