using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Services;
using Monolith.FireWall.Platform.Models;
using Monolith.FireWall.Platform.Validation;

namespace Monolith.FireWall.Core.Services.Platform;

public sealed class PlatformExecutor
{
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;
    private readonly Dictionary<string, PlatformActionDefinition> _actions;
    private readonly ModuleRegistry _moduleRegistry;
    private readonly PlatformPolicyStore? _policyStore;

    public PlatformExecutor(ModuleRegistry moduleRegistry, PlatformPolicyStore? policyStore = null)
    {
        _commandRunner = new PlatformCommandRunner();
        _loggingManager = LoggingManager.Instance;
        _moduleRegistry = moduleRegistry;
        _policyStore = policyStore;
        _actions = BuildActions();
    }

    public async Task<PlatformResponse> HandleAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return PlatformResponse.Fail(new PlatformError
            {
                Code = PlatformErrorCode.ValidationError,
                Message = "Action is required"
            });
        }

        if (!_actions.TryGetValue(request.Action, out var action))
        {
            return PlatformResponse.Fail(new PlatformError
            {
                Code = PlatformErrorCode.NotFound,
                Message = $"Unknown platform action '{request.Action}'"
            });
        }

        if (!IsActionAllowed(request, action))
        {
            return PlatformResponse.Fail(new PlatformError
            {
                Code = PlatformErrorCode.PermissionDenied,
                Message = "Platform action not permitted by policy"
            });
        }

        if (!HasCapability(request.Context, action.RequiredCapability, action.IsWrite))
        {
            return PlatformResponse.Fail(new PlatformError
            {
                Code = PlatformErrorCode.PermissionDenied,
                Message = "Insufficient capability for platform action",
                Details = new Dictionary<string, object>
                {
                    ["required"] = action.RequiredCapability.ToString(),
                    ["provided"] = request.Context.Capabilities.ToString()
                }
            });
        }

        var stopwatch = Stopwatch.StartNew();
        PlatformHandlerResult? handlerResult = null;
        PlatformError? error = null;

        try
        {
            handlerResult = await action.Handler(request, cancellationToken);
        }
        catch (PlatformValidationException ex)
        {
            error = new PlatformError
            {
                Code = PlatformErrorCode.ValidationError,
                Message = ex.Message,
                Details = ex.Details
            };
        }
        catch (PlatformOperationException ex)
        {
            error = new PlatformError
            {
                Code = ex.Code,
                Message = ex.Message,
                Details = ex.Details
            };
        }
        catch (Exception ex)
        {
            error = new PlatformError
            {
                Code = PlatformErrorCode.CommandFailed,
                Message = ex.Message
            };
        }

        stopwatch.Stop();

        var diagnostics = new PlatformDiagnostics
        {
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            CommandId = request.Context.CorrelationId
        };

        var response = error == null
            ? PlatformResponse.Ok(handlerResult?.Data, diagnostics)
            : PlatformResponse.Fail(error, diagnostics);

        await LogActionAsync(request, action, response, handlerResult?.CommandResult);
        return response;
    }

    private bool HasCapability(PlatformContext context, PlatformCapability requiredCapability, bool isWrite)
    {
        var allowedCapabilities = ResolveAllowedCapabilities(context);
        if (allowedCapabilities == PlatformCapability.None)
        {
            if (!string.IsNullOrWhiteSpace(context.PackageId) || !string.IsNullOrWhiteSpace(context.ModuleId))
            {
                return false;
            }

            return !isWrite;
        }

        return (allowedCapabilities & requiredCapability) == requiredCapability;
    }

    private PlatformCapability ResolveAllowedCapabilities(PlatformContext context)
    {
        var allowed = context.Capabilities;
        var policyCaps = _policyStore?.GetCapabilityAllowlist(context.PackageId, context.ModuleId);
        if (policyCaps.HasValue)
        {
            allowed &= policyCaps.Value;
        }

        return allowed;
    }

    private bool IsActionAllowed(PlatformRequest request, PlatformActionDefinition action)
    {
        var allowlist = _policyStore?.GetActionAllowlist(request.Context.PackageId, request.Context.ModuleId);
        if (allowlist == null || allowlist.Count == 0)
        {
            return true;
        }

        return allowlist.Any(entry => string.Equals(entry, request.Action, StringComparison.OrdinalIgnoreCase));
    }

    private async Task LogActionAsync(
        PlatformRequest request,
        PlatformActionDefinition action,
        PlatformResponse response,
        PlatformCommandResult? commandResult)
    {
        var details = new Dictionary<string, object>
        {
            ["action"] = request.Action,
            ["packageId"] = request.Context.PackageId ?? "",
            ["moduleId"] = request.Context.ModuleId ?? "",
            ["userId"] = request.Context.UserId ?? 0,
            ["success"] = response.Success,
            ["errorCode"] = response.Error?.Code.ToString() ?? "",
            ["durationMs"] = response.Diagnostics?.DurationMs ?? 0
        };

        if (commandResult != null)
        {
            details["command"] = commandResult.Command;
            details["arguments"] = commandResult.Arguments;
            details["exitCode"] = commandResult.ExitCode;
            details["usedSudo"] = commandResult.UsedSudo;
            details["commandDurationMs"] = commandResult.DurationMs;
            details["stdoutLength"] = commandResult.StdOut?.Length ?? 0;
            details["stderrLength"] = commandResult.StdErr?.Length ?? 0;
        }

        var message = response.Success
            ? $"Platform action '{request.Action}' completed"
            : $"Platform action '{request.Action}' failed";

        if (action.IsWrite)
        {
            await _loggingManager.LogSecurityAsync(
                "Platform",
                response.Success ? "info" : "error",
                "PlatformExecutor",
                message,
                request.Context.UserId,
                null,
                details);
        }
        else
        {
            await _loggingManager.LogSystemAsync(
                "Platform",
                response.Success ? "info" : "error",
                "PlatformExecutor",
                message,
                details);
        }
    }

    private Dictionary<string, PlatformActionDefinition> BuildActions()
    {
        return new Dictionary<string, PlatformActionDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["platform.system.get-hostname"] = new PlatformActionDefinition(
                "platform.system.get-hostname",
                PlatformCapability.SystemRead,
                false,
                HandleGetHostnameAsync
            ),
            ["platform.system.set-hostname"] = new PlatformActionDefinition(
                "platform.system.set-hostname",
                PlatformCapability.SystemWrite,
                true,
                HandleSetHostnameAsync
            ),
            ["platform.network.interfaces.list"] = new PlatformActionDefinition(
                "platform.network.interfaces.list",
                PlatformCapability.NetworkRead,
                false,
                HandleListInterfacesAsync
            ),
            ["platform.network.addresses.list"] = new PlatformActionDefinition(
                "platform.network.addresses.list",
                PlatformCapability.NetworkRead,
                false,
                HandleListAddressesAsync
            ),
            ["platform.network.routes.list"] = new PlatformActionDefinition(
                "platform.network.routes.list",
                PlatformCapability.NetworkRead,
                false,
                HandleListRoutesAsync
            ),
            ["platform.network.dns.get-resolvers"] = new PlatformActionDefinition(
                "platform.network.dns.get-resolvers",
                PlatformCapability.NetworkRead,
                false,
                HandleGetResolversAsync
            ),
            ["platform.network.interfaces.set-state"] = new PlatformActionDefinition(
                "platform.network.interfaces.set-state",
                PlatformCapability.NetworkWrite,
                true,
                HandleSetInterfaceStateAsync
            ),
            ["platform.network.addresses.add"] = new PlatformActionDefinition(
                "platform.network.addresses.add",
                PlatformCapability.NetworkWrite,
                true,
                HandleAddAddressAsync
            ),
            ["platform.network.addresses.remove"] = new PlatformActionDefinition(
                "platform.network.addresses.remove",
                PlatformCapability.NetworkWrite,
                true,
                HandleRemoveAddressAsync
            ),
            ["platform.network.routes.add"] = new PlatformActionDefinition(
                "platform.network.routes.add",
                PlatformCapability.NetworkWrite,
                true,
                HandleAddRouteAsync
            ),
            ["platform.network.routes.remove"] = new PlatformActionDefinition(
                "platform.network.routes.remove",
                PlatformCapability.NetworkWrite,
                true,
                HandleRemoveRouteAsync
            ),
            ["platform.network.dns.set-resolvers"] = new PlatformActionDefinition(
                "platform.network.dns.set-resolvers",
                PlatformCapability.NetworkWrite,
                true,
                HandleSetResolversAsync
            ),
            ["platform.diagnostics.ping"] = new PlatformActionDefinition(
                "platform.diagnostics.ping",
                PlatformCapability.NetworkRead,
                false,
                HandlePingAsync
            ),
            ["platform.diagnostics.traceroute"] = new PlatformActionDefinition(
                "platform.diagnostics.traceroute",
                PlatformCapability.NetworkRead,
                false,
                HandleTracerouteAsync
            ),
            ["platform.diagnostics.mtr"] = new PlatformActionDefinition(
                "platform.diagnostics.mtr",
                PlatformCapability.NetworkRead,
                false,
                HandleMtrAsync
            ),
            ["platform.files.read"] = new PlatformActionDefinition(
                "platform.files.read",
                PlatformCapability.FilesystemRead,
                false,
                HandleReadFileAsync
            ),
            ["platform.files.write"] = new PlatformActionDefinition(
                "platform.files.write",
                PlatformCapability.FilesystemWrite,
                true,
                HandleWriteFileAsync
            )
        };
    }

    private async Task<PlatformHandlerResult> HandleGetHostnameAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        var hostname = "";
        try
        {
            if (File.Exists("/etc/hostname"))
            {
                hostname = (await File.ReadAllTextAsync("/etc/hostname", cancellationToken)).Trim();
            }
        }
        catch
        {
            hostname = "";
        }

        if (string.IsNullOrWhiteSpace(hostname))
        {
            hostname = Environment.MachineName;
        }

        return new PlatformHandlerResult
        {
            Data = new HostnameInfo { Hostname = hostname }
        };
    }

    private async Task<PlatformHandlerResult> HandleSetHostnameAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out SetHostnameRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        if (!PlatformValidators.IsValidHostname(payload.Hostname))
        {
            throw new PlatformValidationException("Invalid hostname", new Dictionary<string, object>
            {
                ["field"] = "hostname"
            });
        }

        if (!_commandRunner.CommandExists("hostnamectl"))
        {
            throw new PlatformOperationException(PlatformErrorCode.NotSupported, "hostnamectl not available", null);
        }

        var command = new PlatformCommand
        {
            FileName = "hostnamectl",
            Arguments = $"set-hostname {payload.Hostname}",
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.TimedOut)
        {
            throw new PlatformOperationException(PlatformErrorCode.Timeout, "Hostname update timed out", null);
        }

        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Failed to set hostname", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        return new PlatformHandlerResult
        {
            Data = new HostnameInfo { Hostname = payload.Hostname },
            CommandResult = result
        };
    }

    private Task<PlatformHandlerResult> HandleListInterfacesAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        var interfaces = new List<InterfaceInfo>();
        var netPath = "/sys/class/net";
        if (!Directory.Exists(netPath))
        {
            return Task.FromResult(new PlatformHandlerResult { Data = interfaces });
        }

        foreach (var dir in Directory.GetDirectories(netPath))
        {
            var iface = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(iface))
            {
                continue;
            }

            var operState = ReadFileTrim(Path.Combine(dir, "operstate"));
            var mac = ReadFileTrim(Path.Combine(dir, "address"));
            var mtuValue = ReadFileTrim(Path.Combine(dir, "mtu"));

            var mtu = int.TryParse(mtuValue, out var parsed) ? parsed : 0;
            var isUp = string.Equals(operState, "up", StringComparison.OrdinalIgnoreCase);

            interfaces.Add(new InterfaceInfo
            {
                Name = iface,
                MacAddress = mac,
                Mtu = mtu,
                OperState = operState,
                IsUp = isUp
            });
        }

        return Task.FromResult(new PlatformHandlerResult { Data = interfaces });
    }

    private async Task<PlatformHandlerResult> HandleListAddressesAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        string args = "-j addr show";
        if (TryGetPayload(request, out InterfaceRequest payload, out _) && !string.IsNullOrWhiteSpace(payload.Interface))
        {
            if (!PlatformValidators.IsValidInterfaceName(payload.Interface))
            {
                throw new PlatformValidationException("Invalid interface name", new Dictionary<string, object>
                {
                    ["field"] = "interface"
                });
            }

            if (!InterfaceExists(payload.Interface))
            {
                throw new PlatformValidationException("Interface not found", new Dictionary<string, object>
                {
                    ["field"] = "interface"
                });
            }

            args = $"-j addr show dev {payload.Interface}";
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = args,
            UseSudo = false,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Failed to read addresses", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        var addresses = new List<AddressInfo>();
        if (!string.IsNullOrWhiteSpace(result.StdOut))
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            foreach (var iface in doc.RootElement.EnumerateArray())
            {
                var ifname = iface.GetProperty("ifname").GetString() ?? string.Empty;
                if (!iface.TryGetProperty("addr_info", out var addrInfo) || addrInfo.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var addr in addrInfo.EnumerateArray())
                {
                    var local = addr.GetProperty("local").GetString() ?? string.Empty;
                    var family = addr.GetProperty("family").GetString() ?? string.Empty;
                    var prefix = addr.TryGetProperty("prefixlen", out var prefixEl) ? prefixEl.GetInt32() : 0;

                    addresses.Add(new AddressInfo
                    {
                        Interface = ifname,
                        Family = family,
                        Address = local,
                        PrefixLength = prefix
                    });
                }
            }
        }

        return new PlatformHandlerResult
        {
            Data = addresses,
            CommandResult = result
        };
    }

    private async Task<PlatformHandlerResult> HandleListRoutesAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = "-j route show",
            UseSudo = false,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Failed to read routes", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        var routes = new List<RouteInfo>();
        if (!string.IsNullOrWhiteSpace(result.StdOut))
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            foreach (var route in doc.RootElement.EnumerateArray())
            {
                var destination = route.TryGetProperty("dst", out var dstEl) ? dstEl.GetString() ?? "" : "default";
                var gateway = route.TryGetProperty("gateway", out var gwEl) ? gwEl.GetString() : null;
                var dev = route.TryGetProperty("dev", out var devEl) ? devEl.GetString() : null;
                var protocol = route.TryGetProperty("protocol", out var protoEl) ? protoEl.GetString() : null;
                var scope = route.TryGetProperty("scope", out var scopeEl) ? scopeEl.GetString() : null;

                routes.Add(new RouteInfo
                {
                    Destination = destination,
                    Gateway = gateway,
                    Interface = dev,
                    Protocol = protocol,
                    Scope = scope
                });
            }
        }

        return new PlatformHandlerResult
        {
            Data = routes,
            CommandResult = result
        };
    }

    private Task<PlatformHandlerResult> HandleGetResolversAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        var resolvers = new List<string>();
        const string resolvPath = "/etc/resolv.conf";
        if (File.Exists(resolvPath))
        {
            foreach (var line in File.ReadAllLines(resolvPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("nameserver ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        resolvers.Add(parts[1]);
                    }
                }
            }
        }

        return Task.FromResult(new PlatformHandlerResult
        {
            Data = new ResolverInfo
            {
                Source = "resolv.conf",
                Servers = resolvers.ToArray()
            }
        });
    }

    private async Task<PlatformHandlerResult> HandleSetInterfaceStateAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out SetInterfaceStateRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        if (!PlatformValidators.IsValidInterfaceName(payload.Interface))
        {
            throw new PlatformValidationException("Invalid interface name", new Dictionary<string, object>
            {
                ["field"] = "interface"
            });
        }

        if (!InterfaceExists(payload.Interface))
        {
            throw new PlatformValidationException("Interface not found", new Dictionary<string, object>
            {
                ["field"] = "interface"
            });
        }

        var state = payload.State?.ToLowerInvariant();
        if (state != "up" && state != "down")
        {
            throw new PlatformValidationException("Invalid state", new Dictionary<string, object>
            {
                ["field"] = "state"
            });
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = $"link set dev {payload.Interface} {state}",
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Failed to set interface state", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        return new PlatformHandlerResult
        {
            Data = new { interfaceName = payload.Interface, state = state },
            CommandResult = result
        };
    }

    private async Task<PlatformHandlerResult> HandleAddAddressAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        return await HandleAddressChangeAsync(request, true, cancellationToken);
    }

    private async Task<PlatformHandlerResult> HandleRemoveAddressAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        return await HandleAddressChangeAsync(request, false, cancellationToken);
    }

    private async Task<PlatformHandlerResult> HandleAddressChangeAsync(PlatformRequest request, bool add, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out AddressRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        if (!PlatformValidators.IsValidInterfaceName(payload.Interface))
        {
            throw new PlatformValidationException("Invalid interface name", new Dictionary<string, object>
            {
                ["field"] = "interface"
            });
        }

        if (!InterfaceExists(payload.Interface))
        {
            throw new PlatformValidationException("Interface not found", new Dictionary<string, object>
            {
                ["field"] = "interface"
            });
        }

        if (!PlatformValidators.TryParseCidr(payload.AddressCidr, out _, out _))
        {
            throw new PlatformValidationException("Invalid address CIDR", new Dictionary<string, object>
            {
                ["field"] = "addressCidr"
            });
        }

        var verb = add ? "add" : "del";
        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = $"addr {verb} {payload.AddressCidr} dev {payload.Interface}",
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Failed to update address", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        return new PlatformHandlerResult
        {
            Data = new { interfaceName = payload.Interface, address = payload.AddressCidr, operation = verb },
            CommandResult = result
        };
    }

    private async Task<PlatformHandlerResult> HandleAddRouteAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        return await HandleRouteChangeAsync(request, true, cancellationToken);
    }

    private async Task<PlatformHandlerResult> HandleRemoveRouteAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        return await HandleRouteChangeAsync(request, false, cancellationToken);
    }

    private async Task<PlatformHandlerResult> HandleRouteChangeAsync(PlatformRequest request, bool add, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out RouteRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        if (!PlatformValidators.TryParseCidr(payload.Destination, out _, out _))
        {
            throw new PlatformValidationException("Invalid destination CIDR", new Dictionary<string, object>
            {
                ["field"] = "destination"
            });
        }

        if (string.IsNullOrWhiteSpace(payload.Gateway) && string.IsNullOrWhiteSpace(payload.Interface))
        {
            throw new PlatformValidationException("Gateway or interface is required", new Dictionary<string, object>
            {
                ["field"] = "gateway"
            });
        }

        if (!string.IsNullOrWhiteSpace(payload.Gateway) && !PlatformValidators.IsValidIp(payload.Gateway))
        {
            throw new PlatformValidationException("Invalid gateway IP", new Dictionary<string, object>
            {
                ["field"] = "gateway"
            });
        }

        if (!string.IsNullOrWhiteSpace(payload.Interface))
        {
            if (!PlatformValidators.IsValidInterfaceName(payload.Interface))
            {
                throw new PlatformValidationException("Invalid interface name", new Dictionary<string, object>
                {
                    ["field"] = "interface"
                });
            }

            if (!InterfaceExists(payload.Interface))
            {
                throw new PlatformValidationException("Interface not found", new Dictionary<string, object>
                {
                    ["field"] = "interface"
                });
            }
        }

        var verb = add ? "add" : "del";
        var args = $"route {verb} {payload.Destination}";
        if (!string.IsNullOrWhiteSpace(payload.Gateway))
        {
            args += $" via {payload.Gateway}";
        }

        if (!string.IsNullOrWhiteSpace(payload.Interface))
        {
            args += $" dev {payload.Interface}";
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = args,
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Failed to update route", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        return new PlatformHandlerResult
        {
            Data = new { destination = payload.Destination, gateway = payload.Gateway, interfaceName = payload.Interface, operation = verb },
            CommandResult = result
        };
    }

    private async Task<PlatformHandlerResult> HandleSetResolversAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out DnsResolversRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        if (payload.Servers.Length == 0 || !PlatformValidators.AreValidDnsServers(payload.Servers))
        {
            throw new PlatformValidationException("Invalid DNS servers", new Dictionary<string, object>
            {
                ["field"] = "servers"
            });
        }

        if (string.IsNullOrWhiteSpace(payload.Interface))
        {
            throw new PlatformValidationException("Interface is required for resolvectl", new Dictionary<string, object>
            {
                ["field"] = "interface"
            });
        }

        if (!_commandRunner.CommandExists("resolvectl"))
        {
            throw new PlatformOperationException(PlatformErrorCode.NotSupported, "resolvectl not available", null);
        }

        if (!PlatformValidators.IsValidInterfaceName(payload.Interface))
        {
            throw new PlatformValidationException("Invalid interface name", new Dictionary<string, object>
            {
                ["field"] = "interface"
            });
        }

        if (!InterfaceExists(payload.Interface))
        {
            throw new PlatformValidationException("Interface not found", new Dictionary<string, object>
            {
                ["field"] = "interface"
            });
        }

        var servers = string.Join(' ', payload.Servers);
        var command = new PlatformCommand
        {
            FileName = "resolvectl",
            Arguments = $"dns {payload.Interface} {servers}",
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Failed to update DNS resolvers", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        return new PlatformHandlerResult
        {
            Data = new { interfaceName = payload.Interface, servers = payload.Servers },
            CommandResult = result
        };
    }

    private async Task<PlatformHandlerResult> HandlePingAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out PingRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        var host = NormalizeHost(payload.Host);
        var count = Clamp(payload.Count, 1, 20, 4);
        var size = Clamp(payload.Size, 0, 1400, 56);
        var intervalMs = Clamp(payload.IntervalMs, 200, 5000, 1000);
        var timeoutMs = Clamp(payload.TimeoutMs, 500, 10000, 3000);

        if (!_commandRunner.CommandExists("ping"))
        {
            throw new PlatformOperationException(PlatformErrorCode.NotSupported, "ping is not available", null);
        }

        var intervalSeconds = Math.Max(0.2, intervalMs / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
        var timeoutSeconds = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000d));

        var command = new PlatformCommand
        {
            FileName = "ping",
            Arguments = $"-c {count} -s {size} -i {intervalSeconds} -W {timeoutSeconds} -n {host}",
            UseSudo = false,
            TimeoutMs = timeoutMs + (count * intervalMs) + 2000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.TimedOut)
        {
            throw new PlatformOperationException(PlatformErrorCode.Timeout, "Ping timed out", null);
        }

        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Ping failed", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        var parsed = ParsePingOutput(host, result.StdOut);
        return new PlatformHandlerResult
        {
            Data = parsed,
            CommandResult = result
        };
    }

    private async Task<PlatformHandlerResult> HandleTracerouteAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out TracerouteRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        var host = NormalizeHost(payload.Host);
        var isFast = payload.Fast;
        var maxHops = isFast
            ? Clamp(payload.MaxHops, 1, 40, 20)
            : Clamp(payload.MaxHops, 1, 64, 30);
        var waitMs = isFast
            ? Clamp(payload.WaitMs, 200, 2000, 1000)
            : Clamp(payload.WaitMs, 500, 10000, 3000);
        var waitSeconds = Math.Max(1, (int)Math.Ceiling(waitMs / 1000d));
        var resolveFlag = payload.Resolve ? "" : " -n";
        var probeCount = isFast ? 1 : 3;

        if (!_commandRunner.CommandExists("traceroute"))
        {
            throw new PlatformOperationException(PlatformErrorCode.NotSupported, "traceroute is not available", null);
        }

        var command = new PlatformCommand
        {
            FileName = "traceroute",
            Arguments = $"-m {maxHops} -w {waitSeconds} -q {probeCount}{resolveFlag} {host}",
            UseSudo = false,
            TimeoutMs = waitMs * maxHops + 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.TimedOut)
        {
            throw new PlatformOperationException(PlatformErrorCode.Timeout, "Traceroute timed out", null);
        }

        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "Traceroute failed", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        var parsed = ParseTracerouteOutput(host, result.StdOut);
        return new PlatformHandlerResult
        {
            Data = parsed,
            CommandResult = result
        };
    }

    private async Task<PlatformHandlerResult> HandleMtrAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out MtrRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        var host = NormalizeHost(payload.Host);
        var count = Clamp(payload.Count, 1, 50, 10);
        var intervalMs = Clamp(payload.IntervalMs, 200, 5000, 1000);
        var intervalSeconds = Math.Max(0.2, intervalMs / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
        var resolveFlag = payload.Resolve ? "" : " -n";

        if (!_commandRunner.CommandExists("mtr"))
        {
            throw new PlatformOperationException(PlatformErrorCode.NotSupported, "mtr is not available", null);
        }

        var command = new PlatformCommand
        {
            FileName = "mtr",
            Arguments = $"-r -c {count} -i {intervalSeconds}{resolveFlag} {host}",
            UseSudo = false,
            TimeoutMs = intervalMs * count + 30000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.TimedOut)
        {
            throw new PlatformOperationException(PlatformErrorCode.Timeout, "MTR timed out", null);
        }

        if (result.ExitCode != 0)
        {
            throw new PlatformOperationException(PlatformErrorCode.CommandFailed, "MTR failed", new Dictionary<string, object>
            {
                ["stderr"] = result.StdErr
            });
        }

        var parsed = ParseMtrOutput(host, count, result.StdOut);
        return new PlatformHandlerResult
        {
            Data = parsed,
            CommandResult = result
        };
    }

    private async Task<PlatformHandlerResult> HandleReadFileAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out FileReadRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        if (!EnsureModuleContext(request.Context))
        {
            throw new PlatformOperationException(PlatformErrorCode.PermissionDenied, "Module context required", null);
        }

        if (!PlatformValidators.IsValidAbsolutePath(payload.Path))
        {
            throw new PlatformValidationException("Invalid file path", new Dictionary<string, object>
            {
                ["field"] = "path"
            });
        }

        if (!IsFilePathAllowed(request.Context, payload.Path, false))
        {
            throw new PlatformOperationException(PlatformErrorCode.PermissionDenied, "File read not permitted", new Dictionary<string, object>
            {
                ["path"] = payload.Path
            });
        }

        if (!File.Exists(payload.Path))
        {
            throw new PlatformOperationException(PlatformErrorCode.NotFound, "File not found", new Dictionary<string, object>
            {
                ["path"] = payload.Path
            });
        }

        var maxBytes = payload.MaxBytes ?? 1024 * 1024;
        var info = new FileInfo(payload.Path);
        if (info.Length > maxBytes)
        {
            throw new PlatformOperationException(PlatformErrorCode.ValidationError, "File too large", new Dictionary<string, object>
            {
                ["maxBytes"] = maxBytes,
                ["size"] = info.Length
            });
        }

        var content = await File.ReadAllTextAsync(payload.Path, cancellationToken);
        return new PlatformHandlerResult
        {
            Data = new FileReadResponse
            {
                Path = payload.Path,
                Content = content
            }
        };
    }

    private async Task<PlatformHandlerResult> HandleWriteFileAsync(PlatformRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetPayload(request, out FileWriteRequest payload, out var error))
        {
            throw new PlatformValidationException(error.Message, error.Details);
        }

        if (!EnsureModuleContext(request.Context))
        {
            throw new PlatformOperationException(PlatformErrorCode.PermissionDenied, "Module context required", null);
        }

        if (!PlatformValidators.IsValidAbsolutePath(payload.Path))
        {
            throw new PlatformValidationException("Invalid file path", new Dictionary<string, object>
            {
                ["field"] = "path"
            });
        }

        if (!IsFilePathAllowed(request.Context, payload.Path, true))
        {
            throw new PlatformOperationException(PlatformErrorCode.PermissionDenied, "File write not permitted", new Dictionary<string, object>
            {
                ["path"] = payload.Path
            });
        }

        var contentLength = payload.Content?.Length ?? 0;
        if (contentLength > 1024 * 1024)
        {
            throw new PlatformValidationException("Content too large", new Dictionary<string, object>
            {
                ["maxBytes"] = 1024 * 1024
            });
        }

        var directory = Path.GetDirectoryName(payload.Path);
        if (!string.IsNullOrWhiteSpace(directory) && payload.CreateDirectories)
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(payload.Path, payload.Content ?? string.Empty, cancellationToken);
        return new PlatformHandlerResult
        {
            Data = new FileWriteResponse
            {
                Path = payload.Path,
                BytesWritten = contentLength
            }
        };
    }

    private static string ReadFileTrim(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool InterfaceExists(string iface)
    {
        return Directory.Exists(Path.Combine("/sys/class/net", iface));
    }

    private bool EnsureModuleContext(PlatformContext context)
    {
        return !string.IsNullOrWhiteSpace(context.PackageId) && !string.IsNullOrWhiteSpace(context.ModuleId);
    }

    private bool IsFilePathAllowed(PlatformContext context, string path, bool write)
    {
        if (string.IsNullOrWhiteSpace(context.PackageId) || string.IsNullOrWhiteSpace(context.ModuleId))
        {
            return false;
        }

        var module = _moduleRegistry.GetModule(context.PackageId, context.ModuleId);
        if (module == null)
        {
            return false;
        }

        var requestedPath = Path.GetFullPath(path);
        var permissionType = write
            ? Monolith.FireWall.Common.Enums.SystemPermissionType.FileWrite
            : Monolith.FireWall.Common.Enums.SystemPermissionType.FileRead;

        var moduleAllowed = module.Module.GetSystemPermissions()
            .Where(p => p.Type == permissionType)
            .Select(p => p.Resource)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        if (moduleAllowed.Count == 0)
        {
            return false;
        }

        if (!IsPathAllowed(requestedPath, moduleAllowed))
        {
            return false;
        }

        var policyAllowed = _policyStore?.GetFileAllowlist(context.PackageId, context.ModuleId, write) ?? Array.Empty<string>();
        if (policyAllowed.Count > 0 && !IsPathAllowed(requestedPath, policyAllowed))
        {
            return false;
        }

        return true;
    }

    private static bool IsPathAllowed(string requestedPath, IEnumerable<string> allowedResources)
    {
        foreach (var resource in allowedResources)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                continue;
            }

            var allowSubpaths = false;
            var normalized = resource.Trim();
            if (normalized.EndsWith("/*", StringComparison.Ordinal))
            {
                allowSubpaths = true;
                normalized = normalized[..^2];
            }
            else if (normalized.EndsWith("/", StringComparison.Ordinal))
            {
                allowSubpaths = true;
                normalized = normalized.TrimEnd('/');
            }

            var full = Path.GetFullPath(normalized);
            if (allowSubpaths)
            {
                if (string.Equals(requestedPath, full, StringComparison.Ordinal))
                {
                    return true;
                }

                if (requestedPath.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (string.Equals(requestedPath, full, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new PlatformValidationException("Host is required", new Dictionary<string, object>
            {
                ["field"] = "host"
            });
        }

        var trimmed = host.Trim();
        if (!PlatformValidators.IsValidIp(trimmed) && !PlatformValidators.IsValidHostname(trimmed))
        {
            throw new PlatformValidationException("Invalid host", new Dictionary<string, object>
            {
                ["field"] = "host"
            });
        }

        return trimmed;
    }

    private static int Clamp(int? value, int min, int max, int fallback)
    {
        if (!value.HasValue)
        {
            return fallback;
        }

        return Math.Min(max, Math.Max(min, value.Value));
    }

    private static PingResult ParsePingOutput(string host, string output)
    {
        var result = new PingResult
        {
            Host = host,
            OutputLines = SplitLines(output)
        };

        var packetRegex = new Regex(@"(?<tx>\d+)\s+packets transmitted,\s+(?<rx>\d+)\s+received.*?(?<loss>\d+(?:\.\d+)?)% packet loss", RegexOptions.IgnoreCase);
        var packetMatch = packetRegex.Match(output);
        if (packetMatch.Success)
        {
            result.Transmitted = ParseInt(packetMatch.Groups["tx"].Value);
            result.Received = ParseInt(packetMatch.Groups["rx"].Value);
            result.LossPercent = ParseDouble(packetMatch.Groups["loss"].Value);
        }

        var rttRegex = new Regex(@"(?<label>rtt|round-trip).*?=\s*(?<min>[0-9.]+)/(?<avg>[0-9.]+)/(?<max>[0-9.]+)/(?<mdev>[0-9.]+)", RegexOptions.IgnoreCase);
        var rttMatch = rttRegex.Match(output);
        if (rttMatch.Success)
        {
            result.MinMs = ParseDouble(rttMatch.Groups["min"].Value);
            result.AvgMs = ParseDouble(rttMatch.Groups["avg"].Value);
            result.MaxMs = ParseDouble(rttMatch.Groups["max"].Value);
            result.MdevMs = ParseDouble(rttMatch.Groups["mdev"].Value);
        }

        return result;
    }

    private static TracerouteResult ParseTracerouteOutput(string host, string output)
    {
        var result = new TracerouteResult
        {
            Host = host,
            OutputLines = SplitLines(output)
        };

        foreach (var line in result.OutputLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            if (!char.IsDigit(trimmed.FirstOrDefault()))
            {
                continue;
            }

            var parts = Regex.Split(trimmed, @"\s+");
            if (!int.TryParse(parts[0], out var hopNumber))
            {
                continue;
            }

            var hop = new TracerouteHop
            {
                Hop = hopNumber,
                Raw = trimmed
            };

            if (parts.Length > 1)
            {
                hop.Host = parts[1];
            }

            for (var i = 2; i < parts.Length; i++)
            {
                var token = parts[i];
                if (token == "*")
                {
                    continue;
                }

                if (token.Equals("ms", StringComparison.OrdinalIgnoreCase) && i > 0)
                {
                    var prev = parts[i - 1];
                    if (double.TryParse(prev, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        hop.TimesMs.Add(parsed);
                    }
                    continue;
                }

                if (token.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
                {
                    token = token[..^2];
                }

                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    hop.TimesMs.Add(value);
                }
            }

            result.Hops.Add(hop);
        }

        return result;
    }

    private static MtrResult ParseMtrOutput(string host, int count, string output)
    {
        var result = new MtrResult
        {
            Host = host,
            Count = count,
            OutputLines = SplitLines(output)
        };

        var hopRegex = new Regex(@"^\s*(?<hop>\d+)\.\|\-\-\s+(?<host>\S+)\s+(?<loss>[0-9.]+)%\s+(?<sent>\d+)\s+(?<last>[0-9.]+)\s+(?<avg>[0-9.]+)\s+(?<best>[0-9.]+)\s+(?<worst>[0-9.]+)\s+(?<stdev>[0-9.]+)", RegexOptions.Compiled);

        foreach (var line in result.OutputLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = hopRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            result.Hops.Add(new MtrHop
            {
                Hop = ParseInt(match.Groups["hop"].Value),
                Host = match.Groups["host"].Value,
                LossPercent = ParseDouble(match.Groups["loss"].Value),
                Sent = ParseInt(match.Groups["sent"].Value),
                LastMs = ParseDouble(match.Groups["last"].Value),
                AvgMs = ParseDouble(match.Groups["avg"].Value),
                BestMs = ParseDouble(match.Groups["best"].Value),
                WorstMs = ParseDouble(match.Groups["worst"].Value),
                StDevMs = ParseDouble(match.Groups["stdev"].Value),
                Raw = line.Trim()
            });
        }

        return result;
    }

    private static string[] SplitLines(string output)
    {
        return (output ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static double ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static bool TryGetPayload<T>(PlatformRequest request, out T payload, out PlatformError error)
    {
        payload = default!;
        error = new PlatformError
        {
            Code = PlatformErrorCode.ValidationError,
            Message = "Payload is required"
        };

        if (request.Payload.ValueKind == JsonValueKind.Undefined || request.Payload.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var value = JsonSerializer.Deserialize<T>(request.Payload.GetRawText(), options);
            if (value == null)
            {
                error.Message = "Invalid payload";
                return false;
            }

            payload = value;
            return true;
        }
        catch (Exception ex)
        {
            error.Message = "Failed to parse payload";
            error.Details = new Dictionary<string, object>
            {
                ["error"] = ex.Message
            };
            return false;
        }
    }
}

internal sealed class PlatformValidationException : Exception
{
    public PlatformValidationException(string message, Dictionary<string, object>? details) : base(message)
    {
        Details = details;
    }

    public Dictionary<string, object>? Details { get; }
}

internal sealed class PlatformOperationException : Exception
{
    public PlatformOperationException(PlatformErrorCode code, string message, Dictionary<string, object>? details) : base(message)
    {
        Code = code;
        Details = details;
    }

    public PlatformErrorCode Code { get; }
    public Dictionary<string, object>? Details { get; }
}
