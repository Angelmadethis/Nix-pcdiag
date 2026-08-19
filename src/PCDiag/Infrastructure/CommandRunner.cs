using System.Diagnostics;

namespace PCDiag.Infrastructure;

/// <summary>
/// Helper to run external commands and capture output safely.
/// Used by diagnostic checks that need to query system tools.
/// </summary>
public static class CommandRunner
{
    /// <summary>
    /// Run a command and capture its output.
    /// Returns null if the command fails or is not found.
    /// </summary>
    public static async Task<CommandResult> RunAsync(
        string fileName,
        string arguments = "",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();

        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(effectiveTimeout);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            sw.Stop();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new CommandResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                Duration = sw.Elapsed,
                Success = process.ExitCode == 0
            };
        }
        catch (System.ComponentModel.Win32Exception)
        {
            sw.Stop();
            return new CommandResult
            {
                ExitCode = -1,
                StandardOutput = "",
                StandardError = $"Command not found: {fileName}",
                Duration = sw.Elapsed,
                Success = false
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new CommandResult
            {
                ExitCode = -1,
                StandardOutput = "",
                StandardError = $"Command timed out after {effectiveTimeout.TotalSeconds}s",
                Duration = sw.Elapsed,
                Success = false
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new CommandResult
            {
                ExitCode = -1,
                StandardOutput = "",
                StandardError = ex.Message,
                Duration = sw.Elapsed,
                Success = false
            };
        }
    }
}

/// <summary>
/// Result of an external command execution.
/// </summary>
public sealed class CommandResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public TimeSpan Duration { get; init; }
    public bool Success { get; init; }
}
