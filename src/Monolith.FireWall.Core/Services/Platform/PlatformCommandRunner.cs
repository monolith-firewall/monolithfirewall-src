using System.Diagnostics;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services.Platform;

public sealed class PlatformCommandRunner
{
    public async Task<PlatformCommandResult> RunAsync(PlatformCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new PlatformCommandResult
        {
            Command = command.FileName,
            Arguments = command.Arguments,
            UsedSudo = command.UseSudo
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(command.TimeoutMs));

        var fileName = command.FileName;
        var arguments = command.Arguments;

        if (command.UseSudo)
        {
            fileName = "/usr/bin/sudo";
            arguments = $"-n {command.FileName} {command.Arguments}";
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                result.TimedOut = true;
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Best effort to stop the process.
                }
            }

            result.ExitCode = process.HasExited ? process.ExitCode : -1;
            result.StdOut = await stdoutTask;
            result.StdErr = await stderrTask;
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.StdErr = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.DurationMs = (int)stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    public bool CommandExists(string command)
    {
        if (File.Exists(command))
        {
            return true;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(entry, command);
            if (File.Exists(fullPath))
            {
                return true;
            }
        }

        return false;
    }
}
