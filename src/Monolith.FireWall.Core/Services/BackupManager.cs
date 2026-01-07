using System.IO.Compression;
using System.Text.Json;
using CodeLogic;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Manages backup and restore operations for the Monolith Firewall database
/// </summary>
public sealed class BackupManager
{
    private readonly ILogger _logger;
    private readonly PlatformCommandRunner _commandRunner;
    private const string BackupDirectory = "/var/lib/monolith-firewall/backups";
    private const string DatabasePath = "/var/lib/monolith-firewall/codelogic/CL.cl.sqlite/data/database.db";
    private const string SettingsFilePath = "/var/lib/monolith-firewall/backups/.settings.json";
    private BackupSettings _settings;

    public BackupManager(ILogger logger, PlatformCommandRunner commandRunner)
    {
        _logger = logger;
        _commandRunner = commandRunner;
        EnsureBackupDirectoryExists();
        _settings = LoadSettings();
    }

    private void EnsureBackupDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(BackupDirectory))
            {
                Directory.CreateDirectory(BackupDirectory);
                _logger.LogInformation($"Created backup directory: {BackupDirectory}");
            }

            // Set permissions: root:root, 755
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = "755 " + BackupDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to ensure backup directory exists: {BackupDirectory}");
        }
    }

    /// <summary>
    /// Get the current database path
    /// </summary>
    public string GetDatabasePath()
    {
        return File.Exists(DatabasePath) ? DatabasePath : "";
    }

    /// <summary>
    /// Create a backup of the database
    /// </summary>
    public async Task<BackupResult> CreateBackupAsync(string? description = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var dbPath = GetDatabasePath();
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                return new BackupResult
                {
                    Success = false,
                    Error = "Database file not found"
                };
            }

            // Generate backup filename using naming pattern
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var fileName = GenerateBackupFileName(timestamp, description);
            var backupPath = Path.Combine(BackupDirectory, fileName);
            var metadataPath = backupPath.Replace(".db.gz", ".json");

            // Create compressed backup
            using (var sourceStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var targetStream = File.Create(backupPath))
            using (var gzipStream = new GZipStream(targetStream, CompressionLevel.Optimal))
            {
                await sourceStream.CopyToAsync(gzipStream);
            }

            var fileInfo = new FileInfo(backupPath);
            var metadata = new BackupMetadata
            {
                Version = "1.0.0",
                CreatedAt = DateTime.UtcNow,
                Description = description,
                DatabaseVersion = "3.x",
                FileSize = fileInfo.Length,
                Type = "local"
            };

            // Save metadata
            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, metadataJson);

            // Set file permissions
            SetFilePermissions(backupPath);
            SetFilePermissions(metadataPath);

            _logger.LogInformation($"Backup created: {fileName} (Size: {fileInfo.Length} bytes)");

            return new BackupResult
            {
                Success = true,
                Message = "Backup created successfully",
                Backup = new BackupInfo
                {
                    FileName = fileName,
                    CreatedAt = metadata.CreatedAt,
                    Size = fileInfo.Length,
                    Description = description,
                    Type = "local"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup");
            return new BackupResult
            {
                Success = false,
                Error = $"Failed to create backup: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// List all available backups
    /// </summary>
    public async Task<List<BackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        var backups = new List<BackupInfo>();

        try
        {
            if (!Directory.Exists(BackupDirectory))
            {
                return backups;
            }

            var backupFiles = Directory.GetFiles(BackupDirectory, "monolith-backup-*.db.gz")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            foreach (var backupFile in backupFiles)
            {
                var fileName = Path.GetFileName(backupFile);
                var metadataPath = backupFile.Replace(".db.gz", ".json");

                BackupInfo? backupInfo = null;

                // Try to load metadata
                if (File.Exists(metadataPath))
                {
                    try
                    {
                        var metadataJson = await File.ReadAllTextAsync(metadataPath);
                        var metadata = JsonSerializer.Deserialize<BackupMetadata>(metadataJson);
                        if (metadata != null)
                        {
                            backupInfo = new BackupInfo
                            {
                                FileName = fileName,
                                CreatedAt = metadata.CreatedAt,
                                Size = metadata.FileSize,
                                Description = metadata.Description,
                                Type = metadata.Type
                            };
                        }
                    }
                    catch
                    {
                        // Fall through to file-based info
                    }
                }

                // Fallback to file info if metadata not available
                if (backupInfo == null)
                {
                    var fileInfo = new FileInfo(backupFile);
                    backupInfo = new BackupInfo
                    {
                        FileName = fileName,
                        CreatedAt = fileInfo.CreationTimeUtc,
                        Size = fileInfo.Length,
                        Description = null,
                        Type = "local"
                    };
                }

                backups.Add(backupInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list backups");
        }

        return backups;
    }

    /// <summary>
    /// Get information about a specific backup
    /// </summary>
    public async Task<BackupInfo?> GetBackupInfoAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var backupPath = Path.Combine(BackupDirectory, fileName);
            if (!File.Exists(backupPath))
            {
                return null;
            }

            var metadataPath = backupPath.Replace(".db.gz", ".json");
            if (File.Exists(metadataPath))
            {
                var metadataJson = await File.ReadAllTextAsync(metadataPath);
                var metadata = JsonSerializer.Deserialize<BackupMetadata>(metadataJson);
                if (metadata != null)
                {
                    return new BackupInfo
                    {
                        FileName = fileName,
                        CreatedAt = metadata.CreatedAt,
                        Size = metadata.FileSize,
                        Description = metadata.Description,
                        Type = metadata.Type
                    };
                }
            }

            // Fallback to file info
            var fileInfo = new FileInfo(backupPath);
            return new BackupInfo
            {
                FileName = fileName,
                CreatedAt = fileInfo.CreationTimeUtc,
                Size = fileInfo.Length,
                Description = null,
                Type = "local"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get backup info for {fileName}");
            return null;
        }
    }

    /// <summary>
    /// Restore database from backup
    /// </summary>
    public async Task<BackupResult> RestoreBackupAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var backupPath = Path.Combine(BackupDirectory, fileName);
            if (!File.Exists(backupPath))
            {
                return new BackupResult
                {
                    Success = false,
                    Error = "Backup file not found"
                };
            }

            var dbPath = GetDatabasePath();
            if (string.IsNullOrEmpty(dbPath))
            {
                return new BackupResult
                {
                    Success = false,
                    Error = "Database path not found"
                };
            }

            _logger.LogInformation($"Starting restore from backup: {fileName}");

            // Create safety backup before restore
            var safetyBackup = await CreateBackupAsync("Safety backup before restore", cancellationToken);
            if (!safetyBackup.Success)
            {
                _logger.LogWarning("Failed to create safety backup, continuing with restore");
            }

            // Stop Core service to release database lock
            _logger.LogInformation("Stopping monolith-firewall-core service...");
            var stopCommand = new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = "stop monolith-firewall-core.service",
                TimeoutMs = 10000
            };
            var stopResult = await _commandRunner.RunAsync(stopCommand, cancellationToken);
            if (stopResult.ExitCode != 0)
            {
                _logger.LogWarning($"Failed to stop Core service: {stopResult.StdErr}");
                // Continue anyway - database might not be locked
            }

            await Task.Delay(1000, cancellationToken); // Give service time to stop

            BackupResult restoreResult = new BackupResult
            {
                Success = false,
                Error = "Unknown error during restore"
            };
            
            try
            {
                // Decompress and restore database
                using (var sourceStream = new FileStream(backupPath, FileMode.Open, FileAccess.Read))
                using (var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress))
                using (var targetStream = File.Create(dbPath))
                {
                    await gzipStream.CopyToAsync(targetStream, cancellationToken);
                }

                // Set database permissions
                SetFilePermissions(dbPath);

                _logger.LogInformation("Database restored successfully");
                restoreResult = new BackupResult
                {
                    Success = true,
                    Message = "Backup restored successfully. Services restarted."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore database file");
                restoreResult = new BackupResult
                {
                    Success = false,
                    Error = $"Failed to restore database: {ex.Message}"
                };
            }
            finally
            {
                // Always restart Core service
                _logger.LogInformation("Starting monolith-firewall-core service...");
                var startCommand = new PlatformCommand
                {
                    FileName = "systemctl",
                    Arguments = "start monolith-firewall-core.service",
                    TimeoutMs = 10000
                };
                var startResult = await _commandRunner.RunAsync(startCommand, cancellationToken);
                if (startResult.ExitCode != 0)
                {
                    _logger.LogError($"Failed to start Core service: {startResult.StdErr}");
                    if (restoreResult.Success)
                    {
                        restoreResult.Success = false;
                        restoreResult.Error = $"Database restored but failed to start Core service: {startResult.StdErr}";
                    }
                }
                else
                {
                    // Also restart WebUI service
                    await Task.Delay(2000, cancellationToken); // Wait for Core to initialize
                    _logger.LogInformation("Restarting monolith-firewall-webui service...");
                    var restartCommand = new PlatformCommand
                    {
                        FileName = "systemctl",
                        Arguments = "restart monolith-firewall-webui.service",
                        TimeoutMs = 10000
                    };
                    await _commandRunner.RunAsync(restartCommand, cancellationToken);
                }
            }

            return restoreResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup");
            
            // Try to restart services even on error
            try
            {
                var startCommand = new PlatformCommand
                {
                    FileName = "systemctl",
                    Arguments = "start monolith-firewall-core.service",
                    TimeoutMs = 10000
                };
                await _commandRunner.RunAsync(startCommand, cancellationToken);
                
                var restartCommand = new PlatformCommand
                {
                    FileName = "systemctl",
                    Arguments = "restart monolith-firewall-webui.service",
                    TimeoutMs = 10000
                };
                await _commandRunner.RunAsync(restartCommand, cancellationToken);
            }
            catch
            {
                // Ignore restart errors
            }

            return new BackupResult
            {
                Success = false,
                Error = $"Failed to restore backup: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Delete a backup file
    /// </summary>
    public async Task<BackupResult> DeleteBackupAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var backupPath = Path.Combine(BackupDirectory, fileName);
            var metadataPath = backupPath.Replace(".db.gz", ".json");

            if (!File.Exists(backupPath))
            {
                return new BackupResult
                {
                    Success = false,
                    Error = "Backup file not found"
                };
            }

            File.Delete(backupPath);
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }

            _logger.LogInformation($"Backup deleted: {fileName}");

            return new BackupResult
            {
                Success = true,
                Message = "Backup deleted successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to delete backup: {fileName}");
            return new BackupResult
            {
                Success = false,
                Error = $"Failed to delete backup: {ex.Message}"
            };
        }
    }

    private void SetFilePermissions(string filePath)
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = "644 " + filePath,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch
        {
            // Best effort
        }
    }

    /// <summary>
    /// Get current backup settings
    /// </summary>
    public BackupSettings GetSettings()
    {
        return _settings;
    }

    /// <summary>
    /// Update backup settings
    /// </summary>
    public async Task<bool> UpdateSettingsAsync(BackupSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            _settings = settings;
            await SaveSettingsAsync(cancellationToken);
            _logger.LogInformation("Backup settings updated");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update backup settings");
            return false;
        }
    }

    private BackupSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<BackupSettings>(json);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load backup settings, using defaults: {ex.Message}");
        }

        return new BackupSettings();
    }

    private async Task SaveSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken);
            SetFilePermissions(SettingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save backup settings");
            throw;
        }
    }

    private string GenerateBackupFileName(string timestamp, string? description)
    {
        var pattern = _settings.NamingPattern;
        
        // Replace placeholders
        pattern = pattern.Replace("{timestamp}", timestamp);
        pattern = pattern.Replace("{date}", DateTime.UtcNow.ToString("yyyyMMdd"));
        pattern = pattern.Replace("{time}", DateTime.UtcNow.ToString("HHmmss"));
        pattern = pattern.Replace("{datetime}", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        
        if (!string.IsNullOrEmpty(description))
        {
            // Sanitize description for filename
            var safeDesc = string.Join("", description.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' '));
            safeDesc = safeDesc.Replace(" ", "-").ToLower();
            if (safeDesc.Length > 30) safeDesc = safeDesc.Substring(0, 30);
            pattern = pattern.Replace("{description}", safeDesc);
        }
        else
        {
            pattern = pattern.Replace("-{description}", "").Replace("{description}", "");
        }

        // Ensure it ends with .db.gz
        if (!pattern.EndsWith(".db.gz"))
        {
            pattern += ".db.gz";
        }

        // Remove any invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            pattern = pattern.Replace(c, '-');
        }

        return pattern;
    }
}
