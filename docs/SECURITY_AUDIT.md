# Security Audit — Malicious Code Review

**Repository:** `Juamava/TravianGuardBot` (a fork/import of `vinaghost/TravianBotSharp`)
**Branch audited:** `marcosCardona/security-audit-malicious-code` (at `392ab14`)
**Date:** 2026-11-04
**Question asked:** does this repository contain malicious code — does building or running it harm the machine, steal data, or phone home beyond "automates a game in a browser"?

**Method:** read-only static review across three parallel work streams. Nothing was built, nothing was executed, no browser was opened, and no file in the repository was modified. Package and GitHub Action metadata was verified live against nuget.org and the GitHub API.

---

## Verdict: CLEAN — no malicious code found

All three work streams returned CLEAN independently:

| # | Scope | Coverage | Verdict |
|---|---|---|---|
| 1 | C#/.NET runtime behaviour | 264 `.cs`, 24 `.xaml`, 3 `.csproj`, 3 lock files, 3 workflows | CLEAN — 4 real non-malicious weaknesses |
| 2 | Supply chain & build/release pipeline | 40 direct / 137 resolved NuGet packages, 12 Actions, all workflows, git history | CLEAN — no auto-update, no build hooks |
| 3 | Bundled HTML/JS assets (`Examples HTML/`) | 74 files, 17.2 MB, 17 distinct JS/HTML assets | CLEAN and inert — never loaded |

The application does what it claims. It launches the user's own installed Chrome against a user-configured Travian server, scrapes the DOM, and clicks. Specifically, **none** of the following exists anywhere in the codebase:

- **No persistence.** Zero `Registry.SetValue`, zero Run/RunOnce key, no Startup folder, no scheduled task, no service install. The registry is touched exactly once, read-only, to locate `chrome.exe` (`MainCore/UI/ViewModels/MainViewModel.cs:66`).
- **No writes outside the app's own folder.** Zero `File.Delete`/`Directory.Delete`/`File.Move`/`File.Copy` repo-wide. Zero references to `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%`, Temp, Desktop, Documents or Program Files — the app has no concept of a path outside its own directory.
- **No telemetry or exfiltration.** Exactly one `HttpClient` in the whole app, with one hardcoded destination, used as a bare GET proxy-liveness probe. No webhook, no analytics, no network log sink.
- **No dynamic code loading.** Zero `Assembly.Load`, `Activator.CreateInstance`, `Type.GetType`, Reflection.Emit or Roslyn. Zero `DllImport`/`unsafe`/`Marshal` — no native interop at all.
- **No auto-update or self-patching.** Verified four independent ways (see below). There is no update chain to compromise.
- **No obfuscation.** No base64 blob, no hex array, no XOR loop, no crypto constant, no hostname assembled from fragments. Every host in the app is a single plain string literal.
- **No clipboard access, no keyboard/mouse hooks, no desktop capture.** Screenshots are page-scoped (`_mainPage.ScreenshotAsync()`), not desktop-scoped.
- **No access to other applications' data.** No Chrome `Login Data`/`Local State`/cookie store, no `.ssh`, no `.env`, no credential-manager API. The bot uses its own isolated Patchright profile under `Data/Cache/` and never touches the user's real Chrome profile.
- **No committed binaries.** 0 of 424 tracked files match `.exe/.dll/.msi/.zip/.ps1/.bat/.cmd/.vbs/.lnk`.
- **No MSBuild execution hooks.** No `<Exec>`, no `<Target>` at all, no `BeforeTargets`/`PreBuildEvent`/`UsingTask`/`<Import>`/`<DownloadFile>`, and no `Directory.Build.props`/`nuget.config`/`global.json`/`dotnet-tools.json` in the repo **or any ancestor directory** (MSBuild and NuGet walk upward, so each ancestor was checked explicitly).

### The headline negative result: there is no auto-update mechanism

Three GitHub/gist URLs in the repo looked, at first pass, like a possible self-patching channel. All three are **CI-only or documentation-only text**, and none is read by the application at runtime:

- `gist.githubusercontent.com/vinaghost/f7ce.../raw/` — fetched only in `patch.yml:27` / `release.yml:33` to populate a `changelog` step output. The gist is 682 bytes of release-note Markdown. The value is consumed only as the `body:`/`description:` **input** of two actions, **never inside a `run:` block**, so it cannot reach a shell.
- `nightly.link/vinaghost/TravianBotSharp/...` — text inside a PR comment (`test.yml:142`).
- `github.com/vinaghost/TravianBotSharp/releases/...` — Markdown link text in the Discord announcement and the download FAQ.

Confirmed by: extracting every HTTP(S) literal from all compiled sources (no gist/nightly.link/releases URL appears in any of them); grepping for `autoupdate|selfupdate|CheckForUpdate|DownloadFile|ZipFile|ExtractToDirectory|Assembly.Load|cmd.exe|powershell` (no matches); auditing both `Process.Start` calls (fixed targets — the ko-fi page and the local `logs` folder); and auditing every `HttpClient` (proxy probe only, response body never read, never written to disk, never executed).

---

## What you should actually act on

The audit found no malicious code, but it did find real weaknesses. Ranked by what matters to you as someone considering running this.

### 1. HIGH — Travian and proxy passwords are stored and displayed in plaintext

Credentials live in `TBS.db` next to the executable as plain SQLite `TEXT`, with no encryption and no DPAPI anywhere in the repo (`MainCore/Entities/Access.cs:14-21`, `MainCore/AppMixins.cs:41`). They are bound to a plain `TextBox`, not a `PasswordBox`, so they are never masked (`WPFUI/Views/Tabs/AddAccountTab.xaml:47`) — and shown for every account at once in a `DataGrid` (`EditAccountTab.xaml:78-82`). Bulk import takes them as whitespace-separated plaintext.

**Not malicious:** every read of the password field was traced. There are exactly two consumers — typed into the login form of your own configured server (`LoginCommand.cs:14-25`), and handed to Chrome/`HttpClient` as auth for the proxy you entered. No password is ever logged, exported, or put in a URL.

**Why it still matters:** any process running as you, any cloud-sync or backup of that folder, or anyone glancing at your screen gets both the Travian and the proxy credentials. Use a password you reuse nowhere else.

### 2. MEDIUM — TLS validation is disabled on the browser that types those passwords

`ChromeBrowser.cs:51-62` launches Chrome with `--ignore-certificate-errors`. Every page that browser loads — including the login page where finding 1's plaintext password is typed — silently accepts invalid, expired, self-signed or attacker-substituted certificates.

**The combination is the real risk.** The flag is almost certainly there so the bot works through intercepting proxies, which is exactly the configuration in which it hurts most: a hostile or compromised proxy from your proxy list can MITM the login and harvest the credential, and finding 1 means that credential is sitting in cleartext at both ends. If you use third-party proxies with this bot, treat the Travian password as compromised-by-default.

The other eight Chrome args are benign. Explicitly absent: `--load-extension`, `--remote-debugging-port`, `--disable-web-security`, `--no-sandbox`, and any custom `ExecutablePath`.

### 3. MEDIUM — the binary you would download is not the code audited here

Both documented "get the binary" paths lead to artifacts controlled by the **upstream** author, not this fork:

- `FAQs/01-how-to-download-this-bot.md:5` sends users to `vinaghost/TravianBotSharp/releases/latest`.
- `test.yml:142` comments on every PR to *this* fork with a nightly.link URL that pairs **this** fork's `github.run_id` with the **upstream** repo path — at best broken, at worst pointing at an artifact nobody in that PR reviewed.

This audit covers the source in this tree. **If you intend to run it, build it yourself from this tree** rather than downloading a release.

### 4. MEDIUM — Patchright is the weakest link by reputation

`MainCore/MainCore.csproj:37` takes `Patchright` 1.63.0, a genuine open-source Playwright fork (owner `DevEnterpriseSoftware`, Apache-2.0, no reported vulnerabilities, no typosquat — nuget.org returns exactly one hit for the id). But:

- **21,914 total downloads**, roughly 3,400x less scrutinised than `Microsoft.Playwright` (74.9M).
- Not NuGet-verified, no reserved prefix, and its metadata reads `authors: "Microsoft Corporation, patched by Werner van Deventer"` with a Microsoft copyright line. Misleading-looking, but it is this fork's consistent convention across all versions, not impersonation of a different package.
- ~204 MB, because it bundles the Playwright driver — a Node.js runtime plus JS that `Playwright.CreateAsync()` **launches as a child process** (`ChromeBrowser.cs:39`). This package is the one place third-party executable code enters the running app.

Mitigating: it is hash-pinned in `MainCore/packages.lock.json:100`, so restores are bit-identical; and **no Chromium is downloaded at runtime** — the app drives your locally installed Chrome (`Channel = "chrome"`), which removes what would otherwise be the biggest runtime download vector. Trusting this project means trusting `DevEnterpriseSoftware` with in-process code execution. That is inherent to the project's purpose (undetected browser automation), not a smell.

### 5. LOW — a third-party host learns your server and village coordinates

`MainCore/Services/OasisCatalog.cs:19` builds `https://oasis.travibot.com/?l=en&server={host}&x={x}&y={y}`, reached only from the user-clicked "find oasis" flow.

It receives: the Travian server hostname, the origin village's X/Y, and your real IP if no proxy is set. **Nothing else** — no username, no password, no account ID, no machine identifier. The response is parsed for integer coordinate pairs only, which are then rebuilt into URLs on *your own* Travian host. It runs in a throwaway tab closed in a `finally`, and because it is a different origin no Travian session cookie is sent. This is a privacy leak worth knowing about, not exfiltration: it is user-initiated and the request is the whole point of the feature.

### 6. Your machine's NuGet config will break the restore — don't fix it the dangerous way

The user-level NuGet config (`%APPDATA%\NuGet\NuGet.Config`) on the machine this audit ran on has **`nuget.org` explicitly disabled**, leaving three local folder feeds — two of which do not exist on disk, while the third (VS Offline Packages) holds versions that do not match this project. None of this project's key dependencies (`patchright`, `microsoft.playwright`, `reactiveui`, `serilog`) is in your global package cache.

So `dotnet restore --locked-mode` will fail to resolve essentially everything. **The security risk here is the workaround:** deleting the lock files, passing `--ignore-failed-sources`, or adding an arbitrary feed would each discard the integrity guarantee that currently makes this restore safe. Do it this way instead:

```
dotnet restore --locked-mode -s https://api.nuget.org/v3/index.json
```

or `dotnet nuget enable source nuget.org`. **Keep `--locked-mode`** — with it, even a hostile feed cannot substitute package content, because all 137 resolved entries are hash-pinned.

### 7. LOW — remaining items, for completeness

- **No third-party GitHub Action is SHA-pinned.** All 12 use mutable tags. Each was resolved live and all are legitimate, but `ncipollo/release-action` runs with a `contents: write` token and `sarisia/actions-status-discord` receives the `DISCORD_BOT` webhook secret — a force-moved tag would get those. Affects CI only, not a local build.
- **Release-note gist is owned by the upstream author**, so whoever controls `vinaghost`'s account controls this fork's release body and Discord announcement text forever, with no review. The heredoc delimiter is also unsanitised (`CHANGELOG_BODY_EOF`), and `curl` has no `--fail`/`--silent`/`--location`. Bounded to release-notes text, not RCE.
- **The .NET SDK is cached under a content-independent key**; on a cache hit `actions/setup-dotnet` is skipped entirely, so the SDK comes from the Actions cache rather than Microsoft and is prepended to `PATH`. Requires repo write access to exploit. The NuGet cache, by contrast, is correctly keyed on `hashFiles('**/packages.lock.json')`.
- **Opening a PR mints a git tag** (`test.yml:52-81`, `rickstaa/action-create-tag`). Surprising pipeline behaviour, not a code-execution issue. Note the `Test` job is gated off with `if: false` — tests do not actually run in CI.
- **Page-sourced JavaScript is executed** at `ToBuildingByLocationCommand.cs:22-27`: the `onclick` attribute of a Travian element is read and passed to `EvaluateAsync`. This is the only executed code in the app not from a repo string literal. Benign — it is the handler the page already ships, run in that page's own JS sandbox, exactly as a real click would, and there is no `ExposeFunction`/`ExposeBinding` anywhere, so page JS has no callback into the host process. The other 14 injection sites are all hardcoded literals that only read the DOM. Worth flagging because it depends on remote markup staying honest, and finding 2 removes the TLS guarantee that it is.
- **String-interpolated SQL** in `AppDbContext.cs:267-283` (`EnsureColumn`). Not exploitable — all six callers pass compile-time literals, and everything else is parameterized LINQ. A pattern that would become a real bug if someone later passed a variable.
- **Dead setting:** `ChromeSetting.UserAgent` is populated (`OpenBrowserCommand.cs:17`) but never passed to `LaunchPersistentContextAsync`, so the user-agent field in the UI has no effect. Functional bug.
- **`StronglyTypedId` 1.0.0-beta06** is the only prerelease dependency in a shipping build. **`RunAnalyzersDuringBuild=False`** disables Sonar rules locally.

---

## `Examples HTML/` — 17.2 MB of saved web pages, inert

This folder dominates the repo by size and looks alarming (a file literally named `crypt.js.download`). It is **unreferenced sample data and nothing loads it.**

**Proof it is unreachable:** no `.csproj` names it as `Content`/`Resource`/`None`; no `CopyToOutputDirectory` touches it; no SDK implicit glob can reach it (it sits at the repo root, outside all three project directories — verified 0 `.html`/`.js`/`.download` files under `MainCore`, `MainCore.Test`, `WPFUI`); zero C# references to it and zero `file://` anywhere; all 14 `EvaluateAsync` sites pass inline C# literals; no `AddScriptTag`/`AddInitScript`/`AddStyleTag` exists; the test job is `if: false` and `release.yml` ships only the WPFUI publish output. The one fixture loader (`MainCore.Test/Parsers/ParserTestBase.cs:18-20`) reads `AppContext.BaseDirectory/HtmlFiles/`, a folder that does not exist, and has no callers.

**Every file matches its claimed identity.** `jquery-4.0.0.min.js` is genuine jQuery 4.0.0 (internal version literal, Sizzle removed, all jQuery-4-removed APIs absent); `d3.min.js` is d3 3.5.5; plus verified Chart.js 2.9.4, PixiJS 8.15.0, popper 2.11.6, SimpleBar 5.3.6, TweenMax 1.19.1, MorphSVGPlugin 0.8.10, d3pie 0.1.8, tippy v6. The two suspicious-looking names are benign: **`crypt.js` is Travian's own legacy jQuery game bundle** (1,322 `Travian.*` references, and zero hits for `coinhive`/`monero`/`stratum`/`miner`/`hashrate`), and `crossdomain.html` is consentmanager.net's cross-domain localStorage bridge iframe.

**Swept and found nothing:** zero `WebSocket`, zero `navigator.sendBeacon`, zero `document.write` in any file. All 29 `fetch` sites are same-origin or relative; all 5 `XMLHttpRequest` sites are library transports. All 11 `new Function` sites are legitimate codegen (PixiJS shader compilation, ajv schema compilation, d3 CSV accessors). No packer or obfuscator preamble (`eval(function(p,a,c,k,e` -> 0; `_0x[0-9a-f]{4,}` -> 0). No keylogging — all 51 key-event handlers read in context are React plumbing, input filtering, Esc-to-close and map panning, with no keystroke buffer and no handler reaching a network call. Zero `type="password"`, zero `navigator.credentials`, zero card-number patterns. All 25 images verified by magic bytes as genuine PNG/JPEG/GIF. **Every domain-shaped string in the folder was attributed and classified** — all resolve to Travian infrastructure, the consentmanager CMP (whose ~90 ad-vendor domains are an inert IAB TCF disclosure table, not fetch targets), Google reCAPTCHA, a user-initiated Facebook share, or licence/XML-namespace/docs text. No unexpected host at all.

Three LOW/informational findings, all upstream-vendor or hygiene, all unreachable: `crossdomain.html`'s `postMessage` handler has no `event.origin` check and posts its whole `localStorage` to `parent` with `'*'`; Travian's `crypt.js` contains the folder's single `eval(`, fed from same-origin tooltip markup (self-trust, not remote loading); and a `Rally Point Level 1_files/` directory was committed whose parent `.html` never was, so it is referenced by nothing.

---

## Residual risk — the honest limits of this audit

1. **Minified bundles.** `main.js` (2.9 MB), `pixi.min.js` (719 KB) and `cmp_es.min.js` (499 KB) are too large to read line by line. Coverage there is pattern-based plus in-context review of every hit. A sufficiently novel implant hiding behind computed property names with no literal host would not be caught. Two things make this small: nothing in that folder is on any execution path, and the fully-auditable surface where an attacker would most plausibly plant something — the 42 inline `<script>` blocks in the saved HTML and the unminified `crossdomain.html` — is clean.
2. **Dependency contents were not decompiled.** All 40 direct packages were verified as genuine, non-typosquatted, vulnerability-free and hash-pinned, but their IL was not reviewed. Patchright (finding 4) is the material exposure here.
3. **Nothing was executed.** This is a static review. It cannot observe behaviour that only appears at runtime, though the absence of any dynamic-code, native-interop or obfuscation primitive makes a runtime-only payload hard to hide.

---

## Bottom line

Running this app does nothing malicious to your machine. It writes only into its own folder, reads the registry once to find Chrome, installs no persistence, loads no code it did not ship, and sends nothing anywhere except the Travian server you configure, a bare liveness ping to `travian.com`, and — only when you click "find oasis" — your server name and village coordinates to `oasis.travibot.com`.

Before you run it: use a throwaway Travian password, because it is stored in cleartext in `TBS.db` and `--ignore-certificate-errors` removes the TLS protection around the login where it is typed — especially if you route through third-party proxies. And build the binary from this tree rather than downloading a release, because the documented download links point at upstream's artifacts, not at the code reviewed here.
