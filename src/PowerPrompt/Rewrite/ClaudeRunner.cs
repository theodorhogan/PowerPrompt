using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PowerPrompt.Rewrite;

public enum RewriteFailure
{
    None,
    LaunchError,   // could not start the claude process (e.g. not found)
    Timeout,       // exceeded the configured timeout; process killed
    ProcessError,  // non-zero exit code
    EmptyOutput    // exit 0 but empty/whitespace stdout
}

public sealed record RewriteResult(bool Ok, string? Output, RewriteFailure Failure)
{
    public static RewriteResult Success(string output) => new(true, output, RewriteFailure.None);
    public static RewriteResult Fail(RewriteFailure failure) => new(false, null, failure);
}

/// <summary>
/// Tunables for the claude invocation. Defaults reflect the project decision to
/// drop --bare (OAuth auth) and pin the model. Stage 6 wires these to settings.
/// </summary>
public sealed record ClaudeOptions
{
    public string ClaudePath { get; init; } = "claude";
    public string Model { get; init; } = "sonnet";
    public int TimeoutSeconds { get; init; } = 60;
}

/// <summary>
/// Invokes the Claude CLI as a subprocess and returns clean stdout.
///
/// Invocation: claude -p "&lt;full prompt&gt;" --output-format text --model &lt;model&gt;
/// Notes (see memory: claude-cli-invocation):
///  - No --bare: it only reads ANTHROPIC_API_KEY and would fail under OAuth auth.
///  - The full prompt (template + injected text) is passed as the -p argument via
///    ArgumentList, which handles escaping; stdin is closed immediately so the CLI
///    does not block waiting on it.
///  - CreateNoWindow + UseShellExecute=false: no console flash on every call.
///  - A neutral working directory avoids picking up a project CLAUDE.md.
/// </summary>
public sealed class ClaudeRunner
{
    private readonly ClaudeOptions _options;

    public ClaudeRunner(ClaudeOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Resolves the configured claude path to a full executable path, or null if not
    /// found. Used by Settings to tell the user whether the CLI is installed.
    /// </summary>
    public static string? Locate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            path = "claude";

        if (path.Contains('\\') || path.Contains('/'))
            return File.Exists(path) ? path : null;

        var exts = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir, path)))
                    return Path.Combine(dir, path);
                foreach (var ext in exts)
                {
                    string candidate = Path.Combine(dir, path + ext);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }
        return null;
    }

    public async Task<RewriteResult> RunAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _options.ClaudePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetTempPath()
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(prompt);
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("text");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(_options.Model);

        using var process = new Process { StartInfo = psi };

        try
        {
            if (!process.Start())
                return RewriteResult.Fail(RewriteFailure.LaunchError);
        }
        catch
        {
            return RewriteResult.Fail(RewriteFailure.LaunchError);
        }

        // We pass everything via -p; signal EOF so the CLI never blocks on stdin.
        try { process.StandardInput.Close(); } catch { /* ignore */ }

        // Read both streams concurrently to avoid pipe-buffer deadlocks on large output.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            return RewriteResult.Fail(RewriteFailure.Timeout);
        }

        string stdout = await stdoutTask;
        _ = await stderrTask; // drain; not surfaced (tray is the only indicator)

        if (process.ExitCode != 0)
            return RewriteResult.Fail(RewriteFailure.ProcessError);

        string output = stdout.Trim();

        // "exit 0 but empty stdout" is a known headless failure mode — treat as failure.
        if (string.IsNullOrWhiteSpace(output))
            return RewriteResult.Fail(RewriteFailure.EmptyOutput);

        return RewriteResult.Success(output);
    }
}
