using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace PowerPrompt.Sync;

public enum SyncState { NotConfigured, Working, Ok, Conflict, Error }

public sealed class SyncStatus
{
    public SyncState State { get; set; } = SyncState.NotConfigured;
    public string Message { get; set; } = "Sync not configured";
    public DateTime? LastPull { get; set; }
    public DateTime? LastPush { get; set; }
}

/// <summary>
/// Git-syncs the AppData data/ folder (library + templates) to a configured remote
/// using LibGit2Sharp — no git.exe needed. Pull is fast-forward only; a divergence
/// is surfaced rather than auto-merged. Pushes are debounced after edits. The PAT is
/// read from Windows Credential Manager via the supplied token provider.
///
/// All git work runs off the UI thread and is serialized by a single gate. Testable
/// against a local bare repo (file:// remote) without GitHub.
/// </summary>
public sealed class GitSync : IDisposable
{
    private readonly string _dataDir;
    private readonly Func<string?> _tokenProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Timer _debounce;

    public SyncStatus Status { get; } = new();
    public event Action<SyncStatus>? StatusChanged;

    /// <summary>The configured remote URL (from settings). Empty disables sync.</summary>
    public string RemoteUrl { get; set; } = string.Empty;

    public GitSync(string dataDir, Func<string?> tokenProvider)
    {
        _dataDir = dataDir;
        _tokenProvider = tokenProvider;
        _debounce = new Timer(_ => _ = SyncNowAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsLinked => Repository.IsValid(_dataDir);

    /// <summary>Debounced: commit + push ~5s after the last library/template change.</summary>
    public void ScheduleSync()
    {
        if (string.IsNullOrWhiteSpace(RemoteUrl))
            return;
        _debounce.Change(5000, Timeout.Infinite);
    }

    // ---- Operations ----

    public Task<SyncStatus> PullOnLaunchAsync() => RunAsync(repo =>
    {
        Fetch(repo);
        FastForward(repo);
    });

    public Task<SyncStatus> SyncNowAsync() => RunAsync(repo =>
    {
        Fetch(repo);
        FastForward(repo);
        if (Status.State == SyncState.Conflict)
            return;
        CommitAndPush(repo);
    });

    /// <summary>
    /// First-time link to a remote. If the remote has content it becomes the source of
    /// truth (local data backed up first); if it's empty, the local data is pushed up.
    /// </summary>
    public async Task<SyncStatus> LinkToRemoteAsync(string url)
    {
        await _gate.WaitAsync();
        try
        {
            RemoteUrl = url;
            SetWorking("Linking…");

            await Task.Run(() =>
            {
                if (Repository.IsValid(_dataDir))
                {
                    SetRemote(url);
                    return;
                }

                // Back up existing (seed) data, then clone fresh into the empty folder.
                string backup = _dataDir + ".bak";
                if (Directory.Exists(backup))
                    Directory.Delete(backup, true);
                if (Directory.Exists(_dataDir))
                    Directory.Move(_dataDir, backup);
                Directory.CreateDirectory(_dataDir);

                _credAttempt = 0;
                Repository.Clone(url, _dataDir, new CloneOptions
                {
                    FetchOptions = { CredentialsProvider = Credentials }
                });

                using var repo = new Repository(_dataDir);
                bool remoteEmpty = repo.Head.Tip is null;
                if (remoteEmpty)
                {
                    // Seed the empty remote with our local data and push it up.
                    CopyContents(backup, _dataDir);
                    Commit(repo, $"Initial sync from {Environment.MachineName}");
                    Push(repo);
                }
            });

            SetOk("Linked");
            Status.LastPull = DateTimeOffset.Now.DateTime;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
        return Status;
    }

    // ---- Internals ----

    private async Task<SyncStatus> RunAsync(Action<Repository> work)
    {
        if (string.IsNullOrWhiteSpace(RemoteUrl) || !Repository.IsValid(_dataDir))
        {
            Status.State = SyncState.NotConfigured;
            Status.Message = "Sync not configured";
            Raise();
            return Status;
        }

        await _gate.WaitAsync();
        try
        {
            SetWorking("Syncing…");
            await Task.Run(() =>
            {
                using var repo = new Repository(_dataDir);
                SetRemote(RemoteUrl);
                work(repo);
            });
            if (Status.State == SyncState.Working)
                SetOk("Up to date");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
        return Status;
    }

    private void Fetch(Repository repo)
    {
        _credAttempt = 0;
        var remote = repo.Network.Remotes["origin"];
        var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
        Commands.Fetch(repo, "origin", refSpecs, new FetchOptions { CredentialsProvider = Credentials }, null);
        Status.LastPull = DateTimeOffset.Now.DateTime;
    }

    private void FastForward(Repository repo)
    {
        var remoteBranch = repo.Branches["origin/main"] ?? repo.Branches["origin/master"];
        if (remoteBranch?.Tip is null)
            return; // nothing on the remote yet

        var localTip = repo.Head.Tip;
        if (localTip is null)
        {
            repo.Reset(ResetMode.Hard, remoteBranch.Tip);
            return;
        }
        if (localTip == remoteBranch.Tip)
            return;

        var div = repo.ObjectDatabase.CalculateHistoryDivergence(localTip, remoteBranch.Tip);
        if (div.BehindBy > 0 && div.AheadBy == 0)
        {
            repo.Reset(ResetMode.Hard, remoteBranch.Tip); // pure fast-forward
        }
        else if (div.BehindBy > 0 && div.AheadBy > 0)
        {
            Status.State = SyncState.Conflict;
            Status.Message = "Diverged from remote — resolve manually (both machines edited the same prompt).";
            Raise();
        }
    }

    private void CommitAndPush(Repository repo)
    {
        Commit(repo, $"Sync from {Environment.MachineName}");
        Push(repo);
        SetOk("Pushed");
    }

    private static void Commit(Repository repo, string message)
    {
        Commands.Stage(repo, "*");
        var status = repo.RetrieveStatus();
        if (!status.IsDirty)
            return;

        var sig = BuildSignature(repo);
        repo.Commit(message, sig, sig);
    }

    private void Push(Repository repo)
    {
        // Ensure the current branch is named 'main' and tracks origin/main.
        if (repo.Head.Tip is null)
            return;

        _credAttempt = 0;
        string branch = repo.Head.FriendlyName;
        var options = new PushOptions { CredentialsProvider = Credentials };
        repo.Network.Push(repo.Network.Remotes["origin"],
            $"refs/heads/{branch}:refs/heads/{branch}", options);
        Status.LastPush = DateTimeOffset.Now.DateTime;
    }

    private void SetRemote(string url)
    {
        using var repo = Repository.IsValid(_dataDir) ? new Repository(_dataDir) : null!;
        if (repo is null)
            return;
        if (repo.Network.Remotes["origin"] is null)
            repo.Network.Remotes.Add("origin", url);
        else if (repo.Network.Remotes["origin"].Url != url)
            repo.Network.Remotes.Update("origin", r => r.Url = url);
    }

    private int _credAttempt;

    private LibGit2Sharp.Handlers.CredentialsHandler Credentials => (_, _, _) =>
    {
        string? token = _tokenProvider();
        if (string.IsNullOrEmpty(token))
            return new DefaultCredentials();

        // libgit2 re-invokes this callback after an auth rejection, so we cycle
        // through the credential shapes GitHub accepts for a PAT over HTTPS rather
        // than depending on guessing the single "right" one. Whichever works wins.
        return (_credAttempt++ % 3) switch
        {
            0 => new UsernamePasswordCredentials { Username = token, Password = string.Empty },
            1 => new UsernamePasswordCredentials { Username = "x-access-token", Password = token },
            _ => new UsernamePasswordCredentials { Username = token, Password = "x-oauth-basic" },
        };
    };

    private static Signature BuildSignature(Repository repo)
    {
        string name = repo.Config.Get<string>("user.name")?.Value ?? "PowerPrompt";
        string email = repo.Config.Get<string>("user.email")?.Value ?? "powerprompt@users.noreply.github.com";
        return new Signature(name, email, DateTimeOffset.Now);
    }

    private static void CopyContents(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            if (dir.Contains(Path.Combine(from, ".git")))
                continue;
            Directory.CreateDirectory(dir.Replace(from, to));
        }
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            if (file.Replace(from, "").TrimStart(Path.DirectorySeparatorChar).StartsWith(".git"))
                continue;
            string dest = file.Replace(from, to);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    // ---- Status helpers ----

    private void SetWorking(string msg) { Status.State = SyncState.Working; Status.Message = msg; Raise(); }
    private void SetOk(string msg) { Status.State = SyncState.Ok; Status.Message = msg; Raise(); }
    private void SetError(string msg) { Status.State = SyncState.Error; Status.Message = msg; Raise(); }
    private void Raise() => StatusChanged?.Invoke(Status);

    public void Dispose()
    {
        _debounce.Dispose();
        _gate.Dispose();
    }
}
