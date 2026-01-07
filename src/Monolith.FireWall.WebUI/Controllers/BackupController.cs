using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.Json;

namespace Monolith.FireWall.WebUI.Controllers;

[ApiController]
[Route("api/backup")]
public class BackupController : ControllerBase
{
    private readonly ILogger<BackupController> _logger;
    private const string BackupDirectory = "/var/lib/monolith-firewall/backups";
    private const long MaxUploadSize = 100 * 1024 * 1024; // 100 MB

    public BackupController(ILogger<BackupController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Download a backup file
    /// </summary>
    [HttpGet("download/{fileName}")]
    public IActionResult DownloadBackup(string fileName)
    {
        try
        {
            // Security: Validate filename to prevent path traversal
            if (string.IsNullOrEmpty(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return BadRequest(new { error = "Invalid file name" });
            }

            // Ensure it's a backup file
            if (!fileName.EndsWith(".db.gz", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Invalid backup file type" });
            }

            var filePath = Path.Combine(BackupDirectory, fileName);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "Backup file not found" });
            }

            var fileInfo = new FileInfo(filePath);
            var fileStream = System.IO.File.OpenRead(filePath);
            
            return File(fileStream, "application/gzip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading backup: {FileName}", fileName);
            return StatusCode(500, new { error = "Failed to download backup: " + ex.Message });
        }
    }

    /// <summary>
    /// Upload a backup file
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadSize)]
    public async Task<IActionResult> UploadBackup(IFormFile file, string? description = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded" });
            }

            // Validate file size
            if (file.Length > MaxUploadSize)
            {
                return BadRequest(new { error = $"File size exceeds maximum allowed size of {MaxUploadSize / (1024 * 1024)} MB" });
            }

            // Validate file extension
            var fileName = file.FileName;
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".db.gz", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Invalid file type. Only .db.gz backup files are allowed." });
            }

            // Security: Validate filename to prevent path traversal
            var safeFileName = Path.GetFileName(fileName);
            if (safeFileName.Contains("..") || safeFileName.Contains("/") || safeFileName.Contains("\\"))
            {
                return BadRequest(new { error = "Invalid file name" });
            }

            // Generate a unique filename if a file with the same name exists
            var targetPath = Path.Combine(BackupDirectory, safeFileName);
            if (System.IO.File.Exists(targetPath))
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var nameWithoutExt = Path.GetFileNameWithoutExtension(safeFileName);
                safeFileName = $"{nameWithoutExt}-uploaded-{timestamp}.db.gz";
                targetPath = Path.Combine(BackupDirectory, safeFileName);
            }

            // Ensure backup directory exists
            Directory.CreateDirectory(BackupDirectory);

            // Save the file
            using (var fileStream = new FileStream(targetPath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Set file permissions
            try
            {
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = "644 " + targetPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit();
            }
            catch
            {
                // Best effort
            }

            // Validate the uploaded file is a valid gzip archive
            try
            {
                using (var fileStream = System.IO.File.OpenRead(targetPath))
                using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    // Try to read a small amount to verify it's valid
                    var buffer = new byte[1024];
                    var bytesRead = await gzipStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0 && fileStream.Length > 0)
                    {
                        throw new InvalidDataException("Invalid gzip file");
                    }
                }
            }
            catch
            {
                // Invalid gzip file, delete it
                System.IO.File.Delete(targetPath);
                return BadRequest(new { error = "Uploaded file is not a valid gzip archive" });
            }

            // Create metadata file
            var metadataPath = targetPath.Replace(".db.gz", ".json");
            var metadata = new
            {
                version = "1.0.0",
                createdAt = DateTime.UtcNow,
                description = description,
                databaseVersion = "3.x",
                fileSize = new FileInfo(targetPath).Length,
                type = "local",
                uploaded = true
            };

            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(metadataPath, metadataJson);

            _logger.LogInformation("Backup file uploaded: {FileName} ({Size} bytes)", safeFileName, new FileInfo(targetPath).Length);

            return Ok(new
            {
                success = true,
                fileName = safeFileName,
                size = new FileInfo(targetPath).Length,
                message = "Backup uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading backup");
            return StatusCode(500, new { error = "Failed to upload backup: " + ex.Message });
        }
    }
}
