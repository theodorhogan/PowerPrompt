# PowerPrompt

A lightweight Windows system-tray app that lives in your tray and does two things:

- **Quick Rewrite** — copy text, press a hotkey, pick *Correct spelling* / *Polish* / *Professionalize*, and the improved text replaces your clipboard. Powered by the [Claude Code](https://claude.com/claude-code) CLI running locally.
- **Prompt Library** — a fast, keyboard-driven organizer for your saved prompts: categories → prompts, search, and an editable preview pane. Press a hotkey, find a prompt, hit Enter to copy it.

It's designed to be compact, dark, and unintrusive — open fast, close fast, stay out of the way.

---

## Install

1. Download **`PowerPrompt.exe`** from the [Releases](../../releases) page.
2. Run it. It's an unsigned single-file app, so Windows SmartScreen may warn you — click **More info → Run anyway**.
3. A grey icon appears in your system tray (bottom-right, possibly under the `^` overflow). Left-click it to open the window; right-click for the menu.

There's nothing to install into — the app keeps its data in `%APPDATA%\PowerPrompt\` and seeds a few example prompts on first run.

### Requirement for Quick Rewrite
The rewrite feature shells out to the **`claude` CLI** and uses *your* Claude account. Install [Claude Code](https://claude.com/claude-code) and sign in (`claude` should work from a terminal). Settings shows whether it's detected. **The Prompt Library, History, and Settings all work without Claude** — only Quick Rewrite needs it.

---

## Using it

### Hotkeys
| Action | Default |
|---|---|
| Rewrite popup | `Ctrl+Alt+R` |
| Open/close Library | `Ctrl+Alt+L` |

Both are configurable in **Settings → Hotkeys** (click a field, then press your combo). If a combo is already used by another app, the editor tells you it's unavailable so you can pick another. The tray's **Quick Actions** menu also runs rewrites without a hotkey.

### Quick Rewrite
1. Copy some text (`Ctrl+C`).
2. Press the rewrite hotkey → a small popup appears above the tray.
3. Pick an option (arrow keys + Enter, or click; Esc cancels). The tray shows a loading icon, then a green check.
4. Press `Ctrl+V` to paste the improved text. *(The app only overwrites the clipboard — it never auto-pastes, and never touches your clipboard if the rewrite fails.)*

### Prompt Library
- Press the Library hotkey to open. `↑/↓` move, `→`/`Enter` drill into a category, `←` go back, **Enter on a prompt copies its body**.
- The right pane shows the selected prompt and lets you **edit it in place** (Save, or it auto-saves when you switch away).
- Full CRUD via the toolbar and right-click menus; a search box filters across all prompts.

### History & Settings
- **History** — a log of your rewrites; click a row to expand, re-copy any output, or clear all.
- **Settings** — edit the three rewrite templates (keep the `{text}` placeholder), the hotkeys, the Claude path/model/timeout, run-on-startup, and git sync.

---

## Optional: sync your library between machines

PowerPrompt can sync your library + templates between your own computers via a **private git repo** (no `git.exe` needed — it uses LibGit2Sharp). This is entirely optional; leave it blank to stay local-only.

1. Create an **empty private repo** on GitHub (e.g. `powerprompt-library`). Don't initialize it with a README.
2. Create a **Personal Access Token** with `repo` scope.
3. In **Settings → Sync**: paste the repo URL, paste the token → **Save token**, then **Link & Sync now**. Your token is stored in **Windows Credential Manager**, never in a file.
4. On your other machine, do the same — it pulls your prompts down.

After linking: edits push automatically (debounced), and each launch fast-forward-pulls. If both machines edited the *same* prompt and the histories diverge, the app surfaces it rather than auto-merging — resolve it in the repo.

Your prompts live as one JSON file per prompt under `%APPDATA%\PowerPrompt\data\library\` (open that folder to browse them).

---

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone <this-repo>
cd PowerPrompt
dotnet build src/PowerPrompt/PowerPrompt.csproj          # debug
# single-file release exe:
dotnet publish src/PowerPrompt/PowerPrompt.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The published exe lands in `src/PowerPrompt/bin/Release/net8.0-windows/win-x64/publish/`.

It's self-contained (bundles the .NET runtime, ~156 MB) so it runs on any Windows 10/11 machine with no prerequisites. For a much smaller (~few MB) build that instead requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0), use `--self-contained false`.

---

## Uninstall

There's no installer, but there *is* a clean uninstall built in — because simply deleting the exe would leave two hidden traces behind (a startup registry entry if you enabled "Run on startup", and your GitHub token in Windows Credential Manager).

Open **Settings → Uninstall → Uninstall PowerPrompt…**. It removes those traces, then closes and deletes the program. It asks whether to also delete your data (`%APPDATA%\PowerPrompt` — your prompts, templates, history, settings) or keep it in case you reinstall.

(If you ever need to do it by hand: delete the exe, delete `%APPDATA%\PowerPrompt`, remove the `PowerPrompt` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, and remove the `PowerPrompt:GitHubToken` generic credential in Credential Manager.)

## Notes

- **Windows only** (.NET 8 / WPF).
- Claude is invoked as a **local subprocess** — there's no HTTP API, no API keys, no telemetry. Your text goes to your own Claude CLI and back.
- The exe is **unsigned**; expect a SmartScreen prompt on first run.
- Built with [Claude Code](https://claude.com/claude-code).
