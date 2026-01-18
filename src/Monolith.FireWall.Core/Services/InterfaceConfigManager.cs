using System.Globalization;
using System.Net;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class InterfaceConfigManager
{
    private const string ManagedFilePath = "/etc/network/interfaces.d/monolith";
    private const string UnmanagedFilePath = "/etc/network/interfaces.d/monolith-unmanaged";
    private const string MainInterfacesPath = "/etc/network/interfaces";
    private const string BackupDirPath = "/var/lib/monolith-firewall/backups/interfaces";
    private const string IncludeLine = "source /etc/network/interfaces.d/*";

    private readonly PlatformCommandRunner _commandRunner;

    public InterfaceConfigManager(PlatformCommandRunner? commandRunner = null)
    {
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    public string ManagedPath => ManagedFilePath;
    public string UnmanagedPath => UnmanagedFilePath;
    public string BackupPath => BackupDirPath;

    /// <summary>
    /// Read unmanaged interfaces from all files in interfaces.d except monolith-managed files.
    /// </summary>
    public async Task<List<InterfaceStanza>> ReadUnmanagedInterfacesAsync(CancellationToken cancellationToken)
    {
        var unmanaged = new List<InterfaceStanza>();
        var interfacesDir = "/etc/network/interfaces.d";
        
        if (!Directory.Exists(interfacesDir))
        {
            return unmanaged;
        }

        try
        {
            // Read from all files in interfaces.d except monolith-managed files
            foreach (var filePath in Directory.GetFiles(interfacesDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Skip monolith-managed files
                if (IsManagedFilePath(filePath))
                {
                    continue;
                }
                
                // Skip backup files
                if (IsBackupFilePath(filePath))
                {
                    continue;
                }
                
                // Skip the unmanaged file itself (it's handled separately below)
                if (string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(UnmanagedFilePath), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(filePath, UnmanagedFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                // Parse stanzas from this file
                var stanzas = ParseStanzas(filePath);
                unmanaged.AddRange(stanzas);
            }
            
            // Also read from the unmanaged file (monolith-unmanaged) if it exists
            if (File.Exists(UnmanagedFilePath))
            {
                var lines = await File.ReadAllLinesAsync(UnmanagedFilePath, cancellationToken);
                var inUnmanagedBlock = false;
                var currentBlockInterface = string.Empty;
                var currentStanza = new List<string>();

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    
                    // Check for BEGIN MONOLITH UNMANAGED marker
                    if (trimmed.StartsWith("# BEGIN MONOLITH UNMANAGED ", StringComparison.OrdinalIgnoreCase))
                    {
                        inUnmanagedBlock = true;
                        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            currentBlockInterface = parts[3];
                        }
                        currentStanza.Clear();
                        continue;
                    }

                    // Check for END MONOLITH UNMANAGED marker
                    if (trimmed.StartsWith("# END MONOLITH UNMANAGED ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (inUnmanagedBlock && !string.IsNullOrEmpty(currentBlockInterface) && currentStanza.Count > 0)
                        {
                            // Parse the stanza
                            var stanza = ParseStanzaFromLines(currentBlockInterface, currentStanza);
                            if (stanza != null)
                            {
                                unmanaged.Add(stanza);
                            }
                        }
                        inUnmanagedBlock = false;
                        currentBlockInterface = string.Empty;
                        currentStanza.Clear();
                        continue;
                    }

                    // Collect lines within unmanaged block
                    if (inUnmanagedBlock)
                    {
                        currentStanza.Add(line);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Return what we've collected so far on error
        }

        return unmanaged;
    }

    private static InterfaceStanza? ParseStanzaFromLines(string interfaceName, List<string> lines)
    {
        var stanza = new InterfaceStanza
        {
            Interface = interfaceName,
            FilePath = UnmanagedFilePath,
            Method = "manual"
        };

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            // Parse iface line
            if (trimmed.StartsWith("iface ", StringComparison.Ordinal))
            {
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 && parts[1] == interfaceName)
                {
                    stanza.Method = parts[3];
                }
                continue;
            }

            // Parse options
            var optionParts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (optionParts.Length == 2)
            {
                stanza.Options[optionParts[0]] = optionParts[1];
            }
        }

        return stanza;
    }

    public string BuildManagedConfig(IEnumerable<InterfaceAssignmentEntity> assignments, IReadOnlyList<string>? dnsServers)
    {
        var lines = new List<string>
        {
            "# BEGIN MONOLITH MANAGED",
            $"# Generated by Monolith FireWall at {DateTime.UtcNow:O}",
            "# Do not edit this file by hand.",
            ""
        };

        foreach (var assignment in assignments.OrderBy(a => a.Type).ThenBy(a => a.InterfaceName, StringComparer.OrdinalIgnoreCase))
        {
            lines.AddRange(BuildStanza(assignment, dnsServers));
            lines.Add("");
        }

        lines.Add("# END MONOLITH MANAGED");
        return string.Join('\n', lines);
    }

    public async Task<InterfaceApplyResult> ApplyAsync(
        IEnumerable<InterfaceAssignmentEntity> assignments,
        IReadOnlyList<string>? dnsServers,
        CancellationToken cancellationToken)
    {
        var managedDir = Path.GetDirectoryName(ManagedFilePath);
        if (!string.IsNullOrEmpty(managedDir))
        {
            Directory.CreateDirectory(managedDir);
        }

        var lockPath = "/var/lib/monolith-firewall/locks/interfaces.lock";
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath) ?? "/var/lib/monolith-firewall/locks");

        await using var lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var backupPath = await BackupIfExistsAsync(ManagedFilePath, cancellationToken);

        var content = BuildManagedConfig(assignments, dnsServers);
        await File.WriteAllTextAsync(ManagedFilePath, content, cancellationToken);

        return new InterfaceApplyResult
        {
            Success = true,
            Message = "Managed interface configuration written",
            ManagedFile = ManagedFilePath,
            AssignmentCount = assignments.Count(),
            BackupFile = backupPath
        };
    }

    /// <summary>
    /// Apply interface configuration to running system using ifup/ifdown or ifreload
    /// </summary>
    public async Task<InterfaceApplyNowResult> ApplyToSystemAsync(
        IEnumerable<InterfaceAssignmentEntity> assignments,
        CancellationToken cancellationToken)
    {
        // Try ifreload first (if available) - it's more robust
        var hasIfreload = _commandRunner.CommandExists("ifreload");

        if (hasIfreload)
        {
            return await ApplyViaIfreloadAsync(assignments, cancellationToken);
        }

        // Fallback to ifup/ifdown
        return await ApplyViaIfupDownAsync(assignments, cancellationToken);
    }

    /// <summary>
    /// Apply configuration using ifreload command (ifupdown2 package)
    /// </summary>
    private async Task<InterfaceApplyNowResult> ApplyViaIfreloadAsync(
        IEnumerable<InterfaceAssignmentEntity> assignments,
        CancellationToken cancellationToken)
    {
        var interfaceList = assignments.Select(a => a.InterfaceName).ToList();
        var interfaceArgs = interfaceList.Count > 0 ? string.Join(" ", interfaceList) : "-a";

        var command = new PlatformCommand
        {
            FileName = "ifreload",
            Arguments = interfaceArgs,
            UseSudo = true,
            TimeoutMs = 60000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);

        return new InterfaceApplyNowResult
        {
            Success = result.ExitCode == 0,
            Message = result.ExitCode == 0
                ? $"Successfully reloaded {interfaceList.Count} interface(s) using ifreload"
                : "ifreload command failed",
            Command = $"ifreload {interfaceArgs}",
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            StdOut = result.StdOut,
            StdErr = result.StdErr
        };
    }

    /// <summary>
    /// Apply configuration using ifdown/ifup commands (traditional ifupdown package)
    /// </summary>
    private async Task<InterfaceApplyNowResult> ApplyViaIfupDownAsync(
        IEnumerable<InterfaceAssignmentEntity> assignments,
        CancellationToken cancellationToken)
    {
        var results = new List<string>();
        var errors = new List<string>();
        var allSuccess = true;

        foreach (var assignment in assignments)
        {
            // Skip if interface is in 'manual' mode (IpMode.None)
            if (assignment.IpMode == InterfaceIpMode.None)
            {
                results.Add($"Skipped {assignment.InterfaceName} (manual mode)");
                continue;
            }

            // Bring interface down first
            var downCommand = new PlatformCommand
            {
                FileName = "ifdown",
                Arguments = assignment.InterfaceName,
                UseSudo = true,
                TimeoutMs = 30000
            };

            var downResult = await _commandRunner.RunAsync(downCommand, cancellationToken);

            // ifdown may fail if interface is not up, which is fine
            if (downResult.ExitCode != 0 && !string.IsNullOrWhiteSpace(downResult.StdErr))
            {
                results.Add($"ifdown {assignment.InterfaceName}: {downResult.StdErr.Trim()}");
            }

            // Bring interface up with new configuration
            var upCommand = new PlatformCommand
            {
                FileName = "ifup",
                Arguments = assignment.InterfaceName,
                UseSudo = true,
                TimeoutMs = 30000
            };

            var upResult = await _commandRunner.RunAsync(upCommand, cancellationToken);

            if (upResult.ExitCode == 0)
            {
                results.Add($"✓ {assignment.InterfaceName} configured successfully");
            }
            else
            {
                allSuccess = false;
                var errorMsg = !string.IsNullOrWhiteSpace(upResult.StdErr)
                    ? upResult.StdErr.Trim()
                    : "Unknown error";
                errors.Add($"✗ {assignment.InterfaceName}: {errorMsg}");
                results.Add($"✗ {assignment.InterfaceName} failed");
            }
        }

        var message = allSuccess
            ? $"Successfully applied configuration to all interfaces"
            : $"Some interfaces failed to apply. {errors.Count} error(s)";

        return new InterfaceApplyNowResult
        {
            Success = allSuccess,
            Message = message,
            Command = "ifdown/ifup for each interface",
            ExitCode = allSuccess ? 0 : 1,
            TimedOut = false,
            StdOut = string.Join("\n", results),
            StdErr = errors.Count > 0 ? string.Join("\n", errors) : null
        };
    }

    /// <summary>
    /// Bring up a specific interface
    /// </summary>
    public async Task<InterfaceApplyNowResult> BringUpInterfaceAsync(
        string interfaceName,
        CancellationToken cancellationToken)
    {
        var command = new PlatformCommand
        {
            FileName = "ifup",
            Arguments = interfaceName,
            UseSudo = true,
            TimeoutMs = 30000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);

        return new InterfaceApplyNowResult
        {
            Success = result.ExitCode == 0,
            Message = result.ExitCode == 0
                ? $"Interface {interfaceName} brought up successfully"
                : $"Failed to bring up {interfaceName}",
            Command = $"ifup {interfaceName}",
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            StdOut = result.StdOut,
            StdErr = result.StdErr
        };
    }

    /// <summary>
    /// Bring down a specific interface
    /// </summary>
    public async Task<InterfaceApplyNowResult> BringDownInterfaceAsync(
        string interfaceName,
        CancellationToken cancellationToken)
    {
        var command = new PlatformCommand
        {
            FileName = "ifdown",
            Arguments = interfaceName,
            UseSudo = true,
            TimeoutMs = 30000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);

        return new InterfaceApplyNowResult
        {
            Success = result.ExitCode == 0,
            Message = result.ExitCode == 0
                ? $"Interface {interfaceName} brought down successfully"
                : $"Failed to bring down {interfaceName}",
            Command = $"ifdown {interfaceName}",
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            StdOut = result.StdOut,
            StdErr = result.StdErr
        };
    }

    public Task<InterfaceConfigCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        return CheckAsync(null, cancellationToken);
    }

    public async Task<InterfaceConfigCheckResult> CheckAsync(IEnumerable<InterfaceAssignmentEntity>? assignments, CancellationToken cancellationToken)
    {
        var result = new InterfaceConfigCheckResult
        {
            ManagedFile = ManagedFilePath,
            ManagedFilePresent = File.Exists(ManagedFilePath)
        };

        if (!File.Exists(MainInterfacesPath))
        {
            result.Issues.Add(new InterfaceConfigIssue
            {
                Type = "missing-main",
                Message = "Main interfaces file not found",
                File = MainInterfacesPath
            });
        }
        else
        {
            var lines = await File.ReadAllLinesAsync(MainInterfacesPath, cancellationToken);
            result.IncludePresent = lines.Any(line => line.Trim().Equals(IncludeLine, StringComparison.OrdinalIgnoreCase));
            if (!result.IncludePresent)
            {
                result.Issues.Add(new InterfaceConfigIssue
                {
                    Type = "missing-include",
                    Message = "Missing interfaces.d include line",
                    File = MainInterfacesPath,
                    Detail = IncludeLine
                });
            }
        }

        var stanzas = new List<InterfaceStanza>();
        foreach (var file in EnumerateInterfaceFiles())
        {
            try
            {
                stanzas.AddRange(ParseStanzas(file));
            }
            catch (Exception ex)
            {
                result.Issues.Add(new InterfaceConfigIssue
                {
                    Type = "parse-error",
                    Message = $"Failed to parse {file}",
                    File = file,
                    Detail = ex.Message
                });
            }
        }

        var interfaceMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var stanza in stanzas)
        {
            if (!interfaceMap.TryGetValue(stanza.Interface, out var files))
            {
                files = new List<string>();
                interfaceMap[stanza.Interface] = files;
            }
            if (!files.Contains(stanza.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                files.Add(stanza.FilePath);
            }
        }

        foreach (var pair in interfaceMap)
        {
            if (pair.Value.Count > 1)
            {
                result.Issues.Add(new InterfaceConfigIssue
                {
                    Type = "duplicate-interface",
                    Message = $"Interface '{pair.Key}' defined in multiple files",
                    Interface = pair.Key,
                    Detail = string.Join(", ", pair.Value)
                });
            }
        }

        var managedInterfaces = assignments != null
            ? new HashSet<string>(assignments.Select(a => a.InterfaceName), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var iface in managedInterfaces)
        {
            var files = stanzas
                .Where(s => string.Equals(s.Interface, iface, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.FilePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var externalFiles = files
                .Where(file => !IsManagedFilePath(file))
                .ToList();

            if (externalFiles.Count > 0)
            {
                result.Issues.Add(new InterfaceConfigIssue
                {
                    Type = "managed-conflict",
                    Message = $"Managed interface '{iface}' also defined outside Monolith",
                    Interface = iface,
                    Detail = string.Join(", ", externalFiles)
                });
            }
        }

        var vlanMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var stanza in stanzas)
        {
            if (!TryParseVlan(stanza.Interface, out var vlanKey))
            {
                continue;
            }

            if (!vlanMap.TryGetValue(vlanKey, out var entries))
            {
                entries = new List<string>();
                vlanMap[vlanKey] = entries;
            }
            entries.Add(stanza.FilePath);
        }

        foreach (var pair in vlanMap)
        {
            if (pair.Value.Count > 1)
            {
                result.Issues.Add(new InterfaceConfigIssue
                {
                    Type = "duplicate-vlan",
                    Message = $"Duplicate VLAN definition for {pair.Key}",
                    Detail = string.Join(", ", pair.Value)
                });
            }
        }

        result.Ok = result.Issues.Count == 0;
        return result;
    }

    public async Task<(bool Changed, string? BackupFile)> EnsureIncludeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(MainInterfacesPath))
        {
            return (false, null);
        }

        var lines = (await File.ReadAllLinesAsync(MainInterfacesPath, cancellationToken)).ToList();
        if (lines.Any(line => line.Trim().Equals(IncludeLine, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, null);
        }

        var backupPath = await BackupIfExistsAsync(MainInterfacesPath, cancellationToken);
        var insertIndex = 0;
        while (insertIndex < lines.Count && (string.IsNullOrWhiteSpace(lines[insertIndex]) || lines[insertIndex].TrimStart().StartsWith("#", StringComparison.Ordinal)))
        {
            insertIndex++;
        }

        lines.Insert(insertIndex, IncludeLine);
        lines.Insert(insertIndex + 1, string.Empty);
        await File.WriteAllLinesAsync(MainInterfacesPath, lines, cancellationToken);
        return (true, backupPath);
    }

    public async Task<(bool Success, string? Error, string? BackupFile)> RemoveInterfaceFromFileAsync(
        string filePath,
        string interfaceName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(interfaceName))
        {
            return (false, "File path and interface name are required", null);
        }

        if (!File.Exists(filePath))
        {
            return (true, null, null); // File doesn't exist, nothing to remove
        }

        // Skip monolith-managed files
        if (IsManagedFilePath(filePath))
        {
            return (false, "Cannot remove interface from monolith-managed file", null);
        }

        try
        {
            var lines = (await File.ReadAllLinesAsync(filePath, cancellationToken)).ToList();
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { interfaceName };
            
            // Check if it's the unmanaged file - use special handling for unmanaged blocks
            if (string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(UnmanagedFilePath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(filePath, UnmanagedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                var updated = RemoveUnmanagedBlock(lines, interfaceName, out var removed);
                if (!removed)
                {
                    // Try regular removal as fallback
                    updated = RemoveInterfacesFromLines(updated, targets, out var changed, out _);
                    if (!changed)
                    {
                        return (false, "Interface not found in file", null);
                    }
                }

                // Write back the file (or delete if empty)
                if (updated.All(string.IsNullOrWhiteSpace) || updated.Count == 0)
                {
                    var backup = await BackupIfExistsAsync(filePath, cancellationToken);
                    File.Delete(filePath);
                    return (true, null, backup);
                }
                else
                {
                    var backup = await BackupIfExistsAsync(filePath, cancellationToken);
                    await File.WriteAllLinesAsync(filePath, updated, cancellationToken);
                    return (true, null, backup);
                }
            }
            else
            {
                // Regular file - use standard removal
                var updated = RemoveInterfacesFromLines(lines, targets, out var changed, out var removed);
                if (!changed || removed == 0)
                {
                    return (false, "Interface not found in file", null);
                }

                var backup = await BackupIfExistsAsync(filePath, cancellationToken);
                
                // Write back the file (or delete if empty)
                if (updated.All(string.IsNullOrWhiteSpace) || updated.Count == 0)
                {
                    File.Delete(filePath);
                }
                else
                {
                    await File.WriteAllLinesAsync(filePath, updated, cancellationToken);
                }

                return (true, null, backup);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to remove interface from file: {ex.Message}", null);
        }
    }

    public async Task<(int RemovedStanzas, List<string> BackupFiles)> RemoveConflictsAsync(
        IEnumerable<InterfaceAssignmentEntity> assignments,
        CancellationToken cancellationToken)
    {
        var targets = new HashSet<string>(assignments.Select(a => a.InterfaceName), StringComparer.OrdinalIgnoreCase);
        var backups = new List<string>();
        var removedStanzas = 0;

        if (targets.Count == 0)
        {
            return (0, backups);
        }

        foreach (var file in EnumerateInterfaceFiles())
        {
            if (IsManagedFilePath(file))
            {
                continue;
            }

            var lines = (await File.ReadAllLinesAsync(file, cancellationToken)).ToList();
            var updated = RemoveInterfacesFromLines(lines, targets, out var changed, out var removed);
            if (!changed)
            {
                continue;
            }

            var backup = await BackupIfExistsAsync(file, cancellationToken);
            if (!string.IsNullOrWhiteSpace(backup))
            {
                backups.Add(backup);
            }

            await File.WriteAllLinesAsync(file, updated, cancellationToken);
            removedStanzas += removed;
        }

        return (removedStanzas, backups);
    }

    public Task<int> MoveLegacyBackupsAsync(CancellationToken cancellationToken)
    {
        return MoveLegacyBackupsInternalAsync(cancellationToken);
    }

    public async Task<(bool Success, string? Error, string? BackupFile)> ExportAssignmentToUnmanagedAsync(
        InterfaceAssignmentEntity assignment,
        CancellationToken cancellationToken)
    {
        if (assignment == null || string.IsNullOrWhiteSpace(assignment.InterfaceName))
        {
            return (false, "Assignment is required", null);
        }

        var (_, includeBackup) = await EnsureIncludeAsync(cancellationToken);
        var mainBackup = await RemoveInterfaceFromMainAsync(assignment.InterfaceName, cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(UnmanagedFilePath) ?? "/etc/network/interfaces.d");
        List<string> lines;
        var fileExists = File.Exists(UnmanagedFilePath);
        if (fileExists)
        {
            lines = (await File.ReadAllLinesAsync(UnmanagedFilePath, cancellationToken)).ToList();
        }
        else
        {
            lines = new List<string>();
        }

        lines = RemoveUnmanagedBlock(lines, assignment.InterfaceName, out _);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { assignment.InterfaceName };
        lines = RemoveInterfacesFromLines(lines, targets, out _, out _);

        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.Add(string.Empty);
        }

        lines.AddRange(BuildUnmanagedBlock(assignment));

        var unmanagedBackup = fileExists
            ? await BackupIfExistsAsync(UnmanagedFilePath, cancellationToken)
            : null;

        await File.WriteAllLinesAsync(UnmanagedFilePath, lines, cancellationToken);

        var backupToReturn = mainBackup ?? unmanagedBackup ?? includeBackup;
        return (true, null, backupToReturn);
    }

    private static IEnumerable<string> EnumerateInterfaceFiles()
    {
        if (File.Exists(MainInterfacesPath))
        {
            yield return MainInterfacesPath;
        }

        var dir = "/etc/network/interfaces.d";
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(dir))
        {
            if (IsBackupFilePath(file))
            {
                continue;
            }

            yield return file;
        }
    }

    private static List<InterfaceStanza> ParseStanzas(string filePath)
    {
        var stanzas = new List<InterfaceStanza>();
        InterfaceStanza? current = null;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("iface ", StringComparison.Ordinal))
            {
                if (current != null)
                {
                    stanzas.Add(current);
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    current = new InterfaceStanza
                    {
                        Interface = parts[1],
                        Method = parts[3],
                        FilePath = filePath
                    };
                }
                else
                {
                    current = null;
                }

                continue;
            }

            if (current == null)
            {
                continue;
            }

            var optionParts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (optionParts.Length == 2)
            {
                current.Options[optionParts[0]] = optionParts[1];
            }
        }

        if (current != null)
        {
            stanzas.Add(current);
        }

        return stanzas;
    }

    private static bool IsManagedFilePath(string path)
    {
        try
        {
            return string.Equals(Path.GetFullPath(path), Path.GetFullPath(ManagedFilePath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(path, ManagedFilePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<string?> RemoveInterfaceFromMainAsync(string iface, CancellationToken cancellationToken)
    {
        if (!File.Exists(MainInterfacesPath))
        {
            return null;
        }

        var lines = (await File.ReadAllLinesAsync(MainInterfacesPath, cancellationToken)).ToList();
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { iface };
        var updated = RemoveInterfacesFromLines(lines, targets, out var changed, out _);
        if (!changed)
        {
            return null;
        }

        var backupPath = await BackupIfExistsAsync(MainInterfacesPath, cancellationToken);
        await File.WriteAllLinesAsync(MainInterfacesPath, updated, cancellationToken);
        return backupPath;
    }

    private static bool IsBackupFilePath(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return name.Contains(".bak-", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> MoveLegacyBackupsInternalAsync(CancellationToken cancellationToken)
    {
        var dir = "/etc/network/interfaces.d";
        if (!Directory.Exists(dir))
        {
            return 0;
        }

        Directory.CreateDirectory(BackupDirPath);
        var moved = 0;

        foreach (var file in Directory.GetFiles(dir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsBackupFilePath(file))
            {
                continue;
            }

            try
            {
                var dest = Path.Combine(BackupDirPath, Path.GetFileName(file));
                dest = EnsureUniquePath(dest);
                File.Move(file, dest);
                moved++;
            }
            catch
            {
                // Ignore move failures to avoid blocking config operations.
            }
        }

        return moved;
    }

    private static bool EnsureIncludeLine(List<string> lines)
    {
        if (lines.Any(line => line.Trim().Equals(IncludeLine, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var insertIndex = 0;
        while (insertIndex < lines.Count && (string.IsNullOrWhiteSpace(lines[insertIndex]) || lines[insertIndex].TrimStart().StartsWith("#", StringComparison.Ordinal)))
        {
            insertIndex++;
        }

        lines.Insert(insertIndex, IncludeLine);
        lines.Insert(insertIndex + 1, string.Empty);
        return true;
    }

    private static List<string> RemoveUnmanagedBlock(List<string> lines, string iface, out bool removed)
    {
        removed = false;
        var begin = $"# BEGIN MONOLITH UNMANAGED {iface}";
        var end = $"# END MONOLITH UNMANAGED {iface}";
        var output = new List<string>(lines.Count);
        var inBlock = false;

        foreach (var line in lines)
        {
            if (!inBlock && line.Trim().Equals(begin, StringComparison.OrdinalIgnoreCase))
            {
                inBlock = true;
                removed = true;
                continue;
            }

            if (inBlock)
            {
                if (line.Trim().Equals(end, StringComparison.OrdinalIgnoreCase))
                {
                    inBlock = false;
                }
                continue;
            }

            output.Add(line);
        }

        return output;
    }

    private static List<string> BuildUnmanagedBlock(InterfaceAssignmentEntity assignment)
    {
        var lines = new List<string>
        {
            $"# BEGIN MONOLITH UNMANAGED {assignment.InterfaceName}",
            $"# Generated by Monolith FireWall at {DateTime.UtcNow:O}",
            "# Monolith no longer manages this interface.",
            ""
        };

        lines.AddRange(BuildStanza(assignment, null));
        lines.Add($"# END MONOLITH UNMANAGED {assignment.InterfaceName}");
        return lines;
    }

    private static List<string> RemoveInterfacesFromLines(
        List<string> lines,
        ISet<string> interfaces,
        out bool changed,
        out int removedStanzas)
    {
        changed = false;
        removedStanzas = 0;
        if (interfaces.Count == 0)
        {
            return lines;
        }

        var output = new List<string>(lines.Count);
        var skipStanza = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (skipStanza)
            {
                if (IsBoundaryLine(trimmed))
                {
                    skipStanza = false;
                    i--;
                }
                else
                {
                    changed = true;
                }
                continue;
            }

            if (IsIfaceLine(trimmed, out var iface) && interfaces.Contains(iface))
            {
                skipStanza = true;
                changed = true;
                removedStanzas++;
                continue;
            }

            if (TryFilterInterfaceListLine(line, "auto", interfaces, out var updatedAuto))
            {
                if (!string.IsNullOrEmpty(updatedAuto))
                {
                    output.Add(updatedAuto);
                }
                changed = true;
                continue;
            }

            if (TryFilterInterfaceListLine(line, "allow-hotplug", interfaces, out var updatedHotplug))
            {
                if (!string.IsNullOrEmpty(updatedHotplug))
                {
                    output.Add(updatedHotplug);
                }
                changed = true;
                continue;
            }

            output.Add(line);
        }

        return output;
    }

    private static bool TryFilterInterfaceListLine(string line, string keyword, ISet<string> targets, out string updatedLine)
    {
        updatedLine = string.Empty;
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith($"{keyword} ", StringComparison.Ordinal))
        {
            return false;
        }

        var indent = line.Substring(0, line.Length - trimmed.Length);
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
        {
            return true;
        }

        var remaining = parts
            .Skip(1)
            .Where(part => !targets.Contains(part))
            .ToList();

        if (remaining.Count == 0)
        {
            return true;
        }

        if (remaining.Count == parts.Length - 1)
        {
            updatedLine = line;
            return false;
        }

        updatedLine = $"{indent}{keyword} {string.Join(' ', remaining)}";
        return true;
    }

    private static bool IsIfaceLine(string trimmed, out string iface)
    {
        iface = string.Empty;
        if (!trimmed.StartsWith("iface ", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        iface = parts[1];
        return true;
    }

    private static bool IsBoundaryLine(string trimmed)
    {
        return trimmed.StartsWith("iface ", StringComparison.Ordinal)
            || trimmed.StartsWith("auto ", StringComparison.Ordinal)
            || trimmed.StartsWith("allow-hotplug ", StringComparison.Ordinal)
            || trimmed.StartsWith("mapping ", StringComparison.Ordinal)
            || trimmed.StartsWith("source ", StringComparison.Ordinal)
            || trimmed.StartsWith("source-directory ", StringComparison.Ordinal);
    }

    private static bool TryParseVlan(string iface, out string key)
    {
        key = string.Empty;
        var parts = iface.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var vlanId))
        {
            return false;
        }

        key = $"{parts[0]}.{vlanId}";
        return true;
    }

    private static List<string> BuildStanza(InterfaceAssignmentEntity assignment, IReadOnlyList<string>? dnsServers)
    {
        var lines = new List<string>
        {
            $"# {assignment.Name} ({assignment.Type})",
            $"auto {assignment.InterfaceName}"
        };

        var hasIpv4 = assignment.IpMode != InterfaceIpMode.None;
        var hasIpv6 = assignment.Ipv6Mode != InterfaceIpMode.None;

        if (!hasIpv4 && !hasIpv6)
        {
            // Preserve interface definition for link-layer settings even if no IP family is configured
            lines.AddRange(BuildIpv4Stanza(assignment, dnsServers, forceManual: true));
            return lines;
        }

        if (hasIpv4)
        {
            lines.AddRange(BuildIpv4Stanza(assignment, dnsServers));
        }

        if (hasIpv6)
        {
            lines.AddRange(BuildIpv6Stanza(assignment, dnsServers));
        }

        return lines;
    }

    private static string PrefixToNetmask(int prefixLength)
    {
        if (prefixLength <= 0)
        {
            return "0.0.0.0";
        }

        if (prefixLength >= 32)
        {
            return "255.255.255.255";
        }

        var mask = uint.MaxValue << (32 - prefixLength);
        var bytes = BitConverter.GetBytes(mask);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return string.Join('.', bytes);
    }

    private static List<string> BuildIpv4Stanza(
        InterfaceAssignmentEntity assignment,
        IReadOnlyList<string>? dnsServers,
        bool forceManual = false)
    {
        var lines = new List<string>();
        var method = forceManual
            ? "manual"
            : assignment.IpMode switch
            {
                InterfaceIpMode.Dhcp => "dhcp",
                InterfaceIpMode.Static => "static",
                _ => "manual"
            };

        lines.Add($"iface {assignment.InterfaceName} inet {method}");

        if (assignment.IpMode == InterfaceIpMode.Static && !string.IsNullOrWhiteSpace(assignment.IpAddress))
        {
            lines.Add($"  address {assignment.IpAddress}");
            if (assignment.PrefixLength.HasValue)
            {
                lines.Add($"  netmask {PrefixToNetmask(assignment.PrefixLength.Value)}");
            }

            if (!string.IsNullOrWhiteSpace(assignment.Gateway))
            {
                lines.Add($"  gateway {assignment.Gateway}");
            }
        }

        var ipv4Dns = FilterDns(dnsServers, System.Net.Sockets.AddressFamily.InterNetwork);
        if (!forceManual && assignment.IpMode != InterfaceIpMode.None && ipv4Dns.Count > 0)
        {
            lines.Add($"  dns-nameservers {string.Join(' ', ipv4Dns)}");
        }

        AppendLinkLayerOptions(lines, assignment);
        return lines;
    }

    private static List<string> BuildIpv6Stanza(
        InterfaceAssignmentEntity assignment,
        IReadOnlyList<string>? dnsServers)
    {
        var lines = new List<string>();
        var method = assignment.Ipv6Mode switch
        {
            InterfaceIpMode.Dhcp => "dhcp",
            InterfaceIpMode.Static => "static",
            _ => "manual"
        };

        lines.Add($"iface {assignment.InterfaceName} inet6 {method}");

        if (assignment.Ipv6Mode == InterfaceIpMode.Static && !string.IsNullOrWhiteSpace(assignment.Ipv6Address))
        {
            lines.Add($"  address {assignment.Ipv6Address}");
            if (assignment.Ipv6PrefixLength.HasValue)
            {
                lines.Add($"  netmask {assignment.Ipv6PrefixLength.Value}");
            }

            if (!string.IsNullOrWhiteSpace(assignment.Ipv6Gateway))
            {
                lines.Add($"  gateway {assignment.Ipv6Gateway}");
            }
        }

        if (assignment.Ipv6AcceptRa)
        {
            lines.Add("  accept_ra 1");
        }

        if (assignment.Ipv6Autoconf)
        {
            lines.Add("  autoconf 1");
        }

        var ipv6Dns = FilterDns(dnsServers, System.Net.Sockets.AddressFamily.InterNetworkV6);
        if (assignment.Ipv6Mode != InterfaceIpMode.None && ipv6Dns.Count > 0)
        {
            lines.Add($"  dns-nameservers {string.Join(' ', ipv6Dns)}");
        }

        AppendLinkLayerOptions(lines, assignment);
        return lines;
    }

    private static void AppendLinkLayerOptions(List<string> lines, InterfaceAssignmentEntity assignment)
    {
        if (assignment.Type == InterfaceAssignmentType.Vlan && !string.IsNullOrWhiteSpace(assignment.ParentInterface))
        {
            lines.Add($"  vlan-raw-device {assignment.ParentInterface}");
        }

        if (assignment.Type == InterfaceAssignmentType.Bridge)
        {
            var ports = (assignment.BridgePorts ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ports.Length > 0)
            {
                lines.Add($"  bridge_ports {string.Join(' ', ports)}");
            }

            lines.Add($"  bridge_stp {(assignment.BridgeStp ? "on" : "off")}");
            if (assignment.BridgeForwardDelay.HasValue)
            {
                lines.Add($"  bridge_fd {assignment.BridgeForwardDelay.Value}");
            }
        }
    }

    private static List<string> FilterDns(IReadOnlyList<string>? dnsServers, System.Net.Sockets.AddressFamily family)
    {
        var results = new List<string>();
        if (dnsServers == null || dnsServers.Count == 0)
        {
            return results;
        }

        foreach (var server in dnsServers)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                continue;
            }

            if (!IPAddress.TryParse(server, out var ip))
            {
                continue;
            }

            if (ip.AddressFamily == family)
            {
                results.Add(server);
            }
        }

        return results;
    }

    private static async Task<string?> BackupIfExistsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var backupPath = BuildBackupPath(path);
        await using var source = File.OpenRead(path);
        await using var dest = File.Create(backupPath);
        await source.CopyToAsync(dest, cancellationToken);
        return backupPath;
    }

    private static string BuildBackupPath(string path)
    {
        Directory.CreateDirectory(BackupDirPath);
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "interfaces";
        }

        return Path.Combine(BackupDirPath, $"{name}.bak-{DateTime.UtcNow:yyyyMMddHHmmss}");
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var dir = Path.GetDirectoryName(path) ?? BackupDirPath;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        return Path.Combine(dir, $"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}");
    }

    public sealed class InterfaceStanza
    {
        public string Interface { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
