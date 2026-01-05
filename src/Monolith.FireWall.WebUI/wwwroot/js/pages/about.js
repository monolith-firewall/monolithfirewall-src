/**
 * About Monolith Firewall Page
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Pages = Monolith.Pages || {};
Monolith.Pages.About = {
    init: function() {
        this.render();
    },

    render: function() {
        const html = `
            <div class="container-fluid content-container p-4">
                <div class="row justify-content-center">
                    <div class="col-lg-10 col-xl-8">
                        <div class="card shadow-sm">
                            <div class="card-header bg-primary text-white">
                                <h3 class="mb-0">
                                    <svg width="24" height="24" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                                        <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>
                                        <path d="m8.93 6.588-2.29.287-.082.38.45.083c.294.07.352.176.288.469l-.738 3.468c-.194.897.105 1.319.808 1.319.545 0 1.178-.252 1.465-.598l.088-.416c-.2.176-.492.246-.686.246-.275 0-.375-.193-.304-.533L8.93 6.588zM9 4.5a1 1 0 1 1-2 0 1 1 0 0 1 2 0z"/>
                                    </svg>
                                    About Monolith Firewall
                                </h3>
                            </div>
                            <div class="card-body">
                                <div class="text-center mb-4">
                                    <h1 class="display-4 mb-3">🛡️ Monolith Firewall</h1>
                                    <p class="lead text-muted">Enterprise-grade firewall management system</p>
                                </div>

                                <hr class="my-4">

                                <div class="row mb-4">
                                    <div class="col-md-6">
                                        <h4>Overview</h4>
                                        <p>
                                            Monolith Firewall is a comprehensive, modular firewall management system 
                                            designed for network administrators who need powerful, flexible control 
                                            over their network security infrastructure.
                                        </p>
                                        <p>
                                            Built with modern technologies and a plugin-based architecture, Monolith 
                                            Firewall provides an intuitive web interface for managing firewall rules, 
                                            network interfaces, routing, and system services.
                                        </p>
                                    </div>
                                    <div class="col-md-6">
                                        <h4>Key Features</h4>
                                        <ul>
                                            <li>Modular package system for extensibility</li>
                                            <li>Intuitive web-based management interface</li>
                                            <li>Advanced firewall rule management</li>
                                            <li>Network interface and routing configuration</li>
                                            <li>Real-time monitoring and diagnostics</li>
                                            <li>Package-based VPN support (IPsec, OpenVPN, WireGuard)</li>
                                            <li>DHCP and DNS server management</li>
                                        </ul>
                                    </div>
                                </div>

                                <hr class="my-4">

                                <div class="row mb-4">
                                    <div class="col-md-12">
                                        <h4>Technology Stack</h4>
                                        <div class="row">
                                            <div class="col-md-4 mb-3">
                                                <strong>Backend:</strong>
                                                <ul class="list-unstyled ms-3">
                                                    <li>• .NET 10.0</li>
                                                    <li>• C#</li>
                                                    <li>• SQLite</li>
                                                </ul>
                                            </div>
                                            <div class="col-md-4 mb-3">
                                                <strong>Frontend:</strong>
                                                <ul class="list-unstyled ms-3">
                                                    <li>• ASP.NET Core</li>
                                                    <li>• Bootstrap 5</li>
                                                    <li>• jQuery</li>
                                                </ul>
                                            </div>
                                            <div class="col-md-4 mb-3">
                                                <strong>Architecture:</strong>
                                                <ul class="list-unstyled ms-3">
                                                    <li>• Plugin-based modules</li>
                                                    <li>• Unix Domain Sockets</li>
                                                    <li>• Systemd integration</li>
                                                </ul>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                <hr class="my-4">

                                <div class="row">
                                    <div class="col-md-12 text-center">
                                        <h4>Get Involved</h4>
                                        <p class="mb-3">
                                            Monolith Firewall is an open-source project. Contributions, bug reports, 
                                            and feature requests are welcome!
                                        </p>
                                        <div class="d-flex justify-content-center gap-3">
                                            <a href="https://github.com/yourusername/monolithfirewall" 
                                               target="_blank" 
                                               rel="noopener noreferrer" 
                                               class="btn btn-outline-primary">
                                                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.012 8.012 0 0 0 16 8c0-4.42-3.58-8-8-8z"/>
                                                </svg>
                                                View on GitHub
                                            </a>
                                            <a href="https://github.com/yourusername/monolithfirewall/issues" 
                                               target="_blank" 
                                               rel="noopener noreferrer" 
                                               class="btn btn-outline-success">
                                                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                    <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>
                                                    <path d="M5.255 5.786a.237.237 0 0 0 .241.247h.825c.138 0 .248-.113.266-.25.09-.656.54-1.134 1.342-1.134.686 0 1.314.343 1.314 1.168 0 .635-.374.927-.965 1.371-.673.489-1.206 1.06-1.168 1.987l.003.217a.25.25 0 0 0 .25.246h.811a.25.25 0 0 0 .25-.25v-.105c0-.718.273-.927 1.01-1.486.609-.463 1.244-.977 1.244-2.056 0-1.511-1.276-2.241-2.673-2.241-1.326 0-2.786.647-2.754 2.533zm1.25 4.331c0 .18.013.357.025.47.012.114.03.232.06.353.079.466.239.93.545 1.313.304.383.707.663 1.211.87.505.207 1.09.31 1.756.31s1.251-.103 1.756-.31c.504-.207.907-.487 1.211-.87.306-.383.466-.847.545-1.313.03-.12.048-.24.06-.353.012-.113.025-.29.025-.47v-1.25H6.505v1.25z"/>
                                                </svg>
                                                Get Support
                                            </a>
                                        </div>
                                    </div>
                                </div>

                                <hr class="my-4">

                                <div class="text-center text-muted small">
                                    <p class="mb-0">Monolith Firewall v1.0.0</p>
                                    <p class="mb-0">Built with ❤️ for network administrators</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#page-content').html(html);
    }
};
