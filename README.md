# HiveMind

**A modern, safety-first Registry editor for Windows.**
Instant search, keys that explain themselves, and changes you can preview, audit and undo.

> **Status: design phase.** There is no code yet. Start with the
> [design spec](docs/superpowers/specs/2026-11-24-hivemind-design.md) and the
> [competitive feature audit](docs/superpowers/specs/2026-11-24-hivemind-feature-audit.md).

## Why

`regedit` gives you no guidance about what a key does, and its search shows one hit at a time.
HiveMind starts from those two problems and adds everything else that people who live in the
registry need: Windows Server admins first, then power users, developers and incident responders.

## Pillars

1. **Find anything, fast.** One search box for keys, values, data and *meaning*. Results stream in
   as they're found, and once the encrypted index is built, search is instant.
2. **Every key explains itself.** Policy templates (ADMX), curated knowledge packs and live system
   context tell you what a value does, which values are allowed, how risky a change is, and what
   enforces it.
3. **Preview, apply, undo.** Every change is staged as a diff and applied all-or-nothing. Every
   change is journaled and can be reverted, even after a restart.
4. **Least privilege, built to be trusted by security tools.** The UI never runs elevated. Every
   binary is signed. HiveMind never borrows another process's token, and every action is logged.
5. **See what changed, and who changed it.** Compare, snapshots, per-key history and recording.
6. **Built for admins.** Offline hives, remote and fleet operations, every-user changes, and export
   as deployable code.

## Roadmap at a glance

| Phase | Theme | Highlights |
|---|---|---|
| 0 | Foundations | Signing pipeline, license gate, banned-API analyzer, crypto and audit core, threat model, UX mockups |
| 1 (v0.1) | Find & Understand | Fast streaming search, Inspector guidance (ADMX, starter pack, live context), staged changes, journal, elevated broker |
| 2 (v0.2) | Instant & Connected | Encrypted index, query language, search by meaning, knowledge packs, compare, snapshots, offline hives, export as code, CLI and PowerShell module |
| 3 (v0.3) | Observe & Secure | Recording with graceful fallback, settings discovery, per-key time travel, security lens, permissions studio |
| 4 (v0.4) | Scale | WinRM/SSH remoting, fleet compare/apply, every-user changes, baselines, HiveMind's own Group Policy templates, plugin SDK |
| 5 (v1.0) | Expert & Polish | Forensics mode, offline repair, opt-in AI explanations, localization, installers |

## Security posture (summary)

- Signed binaries, scripts, installers and official knowledge packs, checked again at runtime.
- No token borrowing, impersonation, injection or "Run as SYSTEM/TrustedInstaller" modes. This is
  enforced in CI with a banned-API analyzer.
- Every action goes to an encrypted, hash-chained journal. Privileged actions are also written to
  the Windows Event Log.
- Snapshots, captures, journals and the search index are encrypted at rest.
- The SAM and SECURITY hives are excluded by default everywhere.

## Get the code on your machine

Once the GitHub repository exists, clone it from a terminal on your laptop:

```powershell
gh repo clone cwgilgalad/HiveMind
```

or without the GitHub CLI:

```powershell
git clone https://github.com/cwgilgalad/HiveMind.git
```

Development prerequisites, and a setup script that checks them, arrive in Phase 0. Expect
Windows 11, Git, the .NET 10 SDK, and Visual Studio with the WinUI application development workload.

## License

Apache License 2.0; see [LICENSE](LICENSE). Knowledge packs will be licensed separately under
CC BY 4.0.

## Code signing

Release binaries will be signed through SignPath Foundation's free program for open-source
projects. Signed releases will carry this line: *Free code signing provided by SignPath.io,
certificate by SignPath Foundation.* No binaries are published yet.
