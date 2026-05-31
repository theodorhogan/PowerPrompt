using System.Drawing;
using System.Reflection;
using System.Windows.Threading;

namespace PowerPrompt.Tray;

public enum TrayStateKind
{
    Idle,
    Loading,
    Done
}

/// <summary>
/// Owns the Idle/Loading/Done state machine and the matching tray icon swap.
/// Done auto-reverts to Idle after 2 seconds. There is intentionally no error
/// or disabled state — the tray icon is the only progress indicator.
/// </summary>
public sealed class TrayState : IDisposable
{
    private readonly Icon _idle;
    private readonly Icon _loading;
    private readonly Icon _done;
    private readonly DispatcherTimer _doneTimer;
    private readonly Action<Icon> _applyIcon;

    public TrayStateKind Current { get; private set; } = TrayStateKind.Idle;

    public TrayState(Action<Icon> applyIcon)
    {
        _applyIcon = applyIcon;
        _idle = LoadIcon("idle.ico");
        _loading = LoadIcon("loading.ico");
        _done = LoadIcon("done.ico");

        _doneTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _doneTimer.Tick += (_, _) =>
        {
            _doneTimer.Stop();
            SetIdle();
        };

        _applyIcon(_idle);
    }

    public void SetIdle()
    {
        _doneTimer.Stop();
        Current = TrayStateKind.Idle;
        _applyIcon(_idle);
    }

    public void SetLoading()
    {
        _doneTimer.Stop();
        Current = TrayStateKind.Loading;
        _applyIcon(_loading);
    }

    /// <summary>Show the Done checkmark, then auto-revert to Idle after 2 seconds.</summary>
    public void SetDone()
    {
        Current = TrayStateKind.Done;
        _applyIcon(_done);
        _doneTimer.Stop();
        _doneTimer.Start();
    }

    private static Icon LoadIcon(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = $"PowerPrompt.Assets.{fileName}";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded icon not found: {resourceName}");
        return new Icon(stream);
    }

    public void Dispose()
    {
        _doneTimer.Stop();
        _idle.Dispose();
        _loading.Dispose();
        _done.Dispose();
    }
}
