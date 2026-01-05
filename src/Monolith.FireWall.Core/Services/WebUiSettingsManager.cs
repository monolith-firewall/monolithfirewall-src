using System.Net;
using System.Text.Json;
using CL.SQLite.Services;
using CodeLogic;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Manages WebUI binding settings stored in database.
/// </summary>
public sealed class WebUiSettingsManager
{
    private readonly ILogger _logger;
    private Repository<WebUiSettingsEntity>? _repository;

    public WebUiSettingsManager(ILogger logger)
    {
        _logger = logger;
        InitializeRepository();
    }

    private void InitializeRepository()
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite != null)
            {
                _repository = sqlite.CreateRepository<WebUiSettingsEntity>();
                _logger.LogInformation("WebUI settings repository initialized");
            }
            else
            {
                _logger.LogWarning("SQLite library not available for WebUI settings");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebUI settings repository");
        }
    }

    /// <summary>
    /// Get current WebUI settings.
    /// </summary>
    public async Task<WebUiSettingsView> GetSettingsAsync()
    {
        if (_repository == null)
        {
            // Return defaults if repository not available
            return new WebUiSettingsView
            {
                HttpPort = 80,
                HttpsPort = 443,
                BindToAllInterfaces = true,
                BindingAddresses = new List<string>()
            };
        }

        try
        {
            var result = await _repository.GetAllAsync();
            if (result.IsSuccess && result.Data != null && result.Data.Any())
            {
                var entity = result.Data.First();
                var addresses = new List<string>();
                
                if (!string.IsNullOrEmpty(entity.BindingAddresses))
                {
                    try
                    {
                        addresses = JsonSerializer.Deserialize<List<string>>(entity.BindingAddresses) ?? new List<string>();
                    }
                    catch
                    {
                        // Fallback: try comma-separated
                        addresses = entity.BindingAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }
                }

                return new WebUiSettingsView
                {
                    HttpPort = entity.HttpPort,
                    HttpsPort = entity.HttpsPort,
                    BindingAddresses = addresses,
                    BindToAllInterfaces = addresses.Count == 0
                };
            }
            else
            {
                // No settings in database, create defaults
                await CreateDefaultSettingsAsync();
                return new WebUiSettingsView
                {
                    HttpPort = 80,
                    HttpsPort = 443,
                    BindToAllInterfaces = true,
                    BindingAddresses = new List<string>()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WebUI settings");
            return new WebUiSettingsView
            {
                HttpPort = 80,
                HttpsPort = 443,
                BindToAllInterfaces = true,
                BindingAddresses = new List<string>()
            };
        }
    }

    /// <summary>
    /// Update WebUI settings.
    /// </summary>
    public async Task<WebUiSettingsUpdateResult> UpdateSettingsAsync(WebUiSettingsUpdateRequest request)
    {
        var result = new WebUiSettingsUpdateResult
        {
            Success = false
        };

        if (_repository == null)
        {
            result.Error = "WebUI settings repository not available";
            return result;
        }

        try
        {
            // Validate ports
            if (request.HttpPort.HasValue && (request.HttpPort.Value < 1 || request.HttpPort.Value > 65535))
            {
                result.Error = "HTTP port must be between 1 and 65535";
                return result;
            }

            if (request.HttpsPort.HasValue && (request.HttpsPort.Value < 1 || request.HttpsPort.Value > 65535))
            {
                result.Error = "HTTPS port must be between 1 and 65535";
                return result;
            }

            // Get current settings or create new
            var getAllResult = await _repository.GetAllAsync();
            WebUiSettingsEntity entity;

            if (getAllResult.IsSuccess && getAllResult.Data != null && getAllResult.Data.Any())
            {
                entity = getAllResult.Data.First();
            }
            else
            {
                entity = new WebUiSettingsEntity
                {
                    HttpPort = 80,
                    HttpsPort = 443,
                    UpdatedAt = DateTime.UtcNow
                };
            }

            // Check if restart is needed
            var needsRestart = false;
            if (request.HttpPort.HasValue && request.HttpPort.Value != entity.HttpPort)
            {
                needsRestart = true;
                entity.HttpPort = request.HttpPort.Value;
            }

            if (request.HttpsPort.HasValue && request.HttpsPort.Value != entity.HttpsPort)
            {
                needsRestart = true;
                entity.HttpsPort = request.HttpsPort.Value;
            }

            // Update binding addresses
            if (request.BindToAllInterfaces.HasValue || request.BindingAddresses != null)
            {
                var newAddresses = new List<string>();
                
                if (request.BindToAllInterfaces == true)
                {
                    // Bind to all interfaces - empty list
                    newAddresses = new List<string>();
                }
                else if (request.BindingAddresses != null)
                {
                    // Validate IP addresses
                    foreach (var addr in request.BindingAddresses)
                    {
                        if (!IPAddress.TryParse(addr, out _))
                        {
                            result.Error = $"Invalid IP address: {addr}";
                            return result;
                        }
                        newAddresses.Add(addr);
                    }
                }
                else
                {
                    // Keep existing addresses if not specified
                    if (!string.IsNullOrEmpty(entity.BindingAddresses))
                    {
                        try
                        {
                            newAddresses = JsonSerializer.Deserialize<List<string>>(entity.BindingAddresses) ?? new List<string>();
                        }
                        catch
                        {
                            newAddresses = new List<string>();
                        }
                    }
                }

                var addressesJson = newAddresses.Count > 0 
                    ? JsonSerializer.Serialize(newAddresses)
                    : null;

                if (addressesJson != entity.BindingAddresses)
                {
                    needsRestart = true;
                    entity.BindingAddresses = addressesJson;
                }
            }

            entity.UpdatedAt = DateTime.UtcNow;

            // Save to database
            bool saveSuccess;
            if (entity.Id == 0)
            {
                var insertResult = await _repository.InsertAsync(entity);
                saveSuccess = insertResult.IsSuccess;
            }
            else
            {
                var updateResult = await _repository.UpdateAsync(entity);
                saveSuccess = updateResult.IsSuccess;
            }

            if (!saveSuccess)
            {
                result.Error = "Failed to save WebUI settings";
                return result;
            }

            result.Success = true;
            result.RequiresRestart = needsRestart;
            result.Settings = new WebUiSettingsView
            {
                HttpPort = entity.HttpPort,
                HttpsPort = entity.HttpsPort,
                BindingAddresses = string.IsNullOrEmpty(entity.BindingAddresses)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(entity.BindingAddresses) ?? new List<string>(),
                BindToAllInterfaces = string.IsNullOrEmpty(entity.BindingAddresses)
            };

            _logger.LogInformation($"WebUI settings updated: HTTP={entity.HttpPort}, HTTPS={entity.HttpsPort}, Addresses={entity.BindingAddresses ?? "all"}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating WebUI settings");
            result.Error = ex.Message;
            return result;
        }
    }

    private async Task CreateDefaultSettingsAsync()
    {
        if (_repository == null)
        {
            return;
        }

        try
        {
            var defaultEntity = new WebUiSettingsEntity
            {
                HttpPort = 80,
                HttpsPort = 443,
                BindingAddresses = null, // null = bind to all interfaces
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.InsertAsync(defaultEntity);
            _logger.LogInformation("Created default WebUI settings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default WebUI settings");
        }
    }
}
