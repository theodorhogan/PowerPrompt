using System.Threading.Tasks;
using PowerPrompt.Common;
using PowerPrompt.History;
using PowerPrompt.Models;
using PowerPrompt.Settings;
using PowerPrompt.Tray;

namespace PowerPrompt.Rewrite;

/// <summary>
/// Orchestrates Feature A: clipboard -> (popup choose option) -> tray Loading ->
/// Claude -> overwrite clipboard -> tray Done. A second trigger while a rewrite is
/// in progress is ignored (no queue). The clipboard is overwritten only on a
/// verified non-empty result; on any failure (timeout, empty output, process
/// error, oversize) it is left untouched and the tray reverts to Idle.
/// </summary>
public sealed class RewriteService
{
    private readonly TrayState _trayState;
    private readonly HistoryStore _history;
    private readonly SettingsStore _settings;
    private bool _busy;

    public RewriteService(TrayState trayState, HistoryStore history, SettingsStore settings)
    {
        _trayState = trayState;
        _history = history;
        _settings = settings;
    }

    /// <summary>Hotkey/left-click entry point: show the option popup above the tray.</summary>
    public void ShowOptionPopup()
    {
        if (_busy)
            return; // ignore a second trigger while a rewrite is in progress

        string text = ClipboardHelper.GetText();
        string? note = Unavailable(text);

        var popup = new RewritePopup(note);
        if (note is null)
            popup.OptionChosen += option => _ = RunAsync(option, text);
        popup.Show();
    }

    /// <summary>Quick Actions menu entry point: run a specific option on the current clipboard.</summary>
    public void RunQuickAction(RewriteOption option)
    {
        if (_busy)
            return;

        string text = ClipboardHelper.GetText();
        if (Unavailable(text) is not null)
            return; // empty or oversize; tray stays Idle (no popup surface here)

        _ = RunAsync(option, text);
    }

    /// <summary>Returns a user-facing note if the text cannot be rewritten, else null.</summary>
    private string? Unavailable(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Clipboard is empty";
        int max = _settings.Settings.MaxInputChars;
        if (text.Length > max)
            return $"Text is too long ({text.Length} chars). Max is {max}.";
        return null;
    }

    private async Task RunAsync(RewriteOption option, string input)
    {
        if (_busy || Unavailable(input) is not null)
            return;

        _busy = true;
        _trayState.SetLoading();
        try
        {
            var s = _settings.Settings;
            string prompt = _settings.Templates.Build(option, input);
            var runner = new ClaudeRunner(new ClaudeOptions
            {
                ClaudePath = s.ClaudePath,
                Model = s.ClaudeModel,
                TimeoutSeconds = s.TimeoutSeconds
            });
            RewriteResult result = await runner.RunAsync(prompt);

            bool ok = result.Ok && !string.IsNullOrWhiteSpace(result.Output);
            if (ok)
            {
                ClipboardHelper.SetText(result.Output!);
                _trayState.SetDone();
            }
            else
            {
                _trayState.SetIdle();
            }

            Log(option, input, ok ? result.Output! : string.Empty, ok);
        }
        catch
        {
            _trayState.SetIdle();
            Log(option, input, string.Empty, ok: false);
        }
        finally
        {
            _busy = false;
        }
    }

    private void Log(RewriteOption option, string input, string output, bool ok)
    {
        _history.Add(new HistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            Option = RewriteOptionInfo.DisplayName(option),
            Input = input,
            Output = output,
            Ok = ok
        });
    }
}
