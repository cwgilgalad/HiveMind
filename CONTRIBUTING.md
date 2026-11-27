# Contributing to HiveMind

Thanks for helping. This page covers what a contribution needs before it can be merged.

## Sign off your commits (DCO)

HiveMind uses the [Developer Certificate of Origin](https://developercertificate.org/) (DCO).
External contributors must sign off every commit:

```powershell
git commit -s
```

That adds a `Signed-off-by:` line with the name and email from your Git config. It certifies that
you wrote the change or otherwise have the right to submit it under the project's license. A CI
check fails a pull request from a fork if any of its commits lacks the line. To sign off commits
you've already made, run `git rebase --signoff main` and force-push your branch.

## Contribution license

Contributions come in under the [Apache License 2.0](LICENSE), the same license the project goes out
under (inbound = outbound). Knowledge packs will use CC BY 4.0.

## Set up your machine

Follow [docs/development.md](docs/development.md). It covers the tools you need, the setup script,
building, testing and dev signing.

## Code standards

- Nullable reference types are on everywhere. Don't suppress nullable warnings to make the build
  pass.
- The build has zero warnings. Analyzers run with warnings as errors, so a warning fails the build.
- Tests that need Windows call `Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only")`
  first, so the suite still passes, with skips, on other systems.

## The clean-room rule

Never copy code, text or assets from RegKit (AGPL-3.0), FluentRegEdit (GPL-3.0) or any
closed-source tool. Design features from public descriptions only. Projects under MIT or Apache
licenses may be referenced, with attribution.

## Banned APIs and EDR rules

HiveMind must never look like an attack to security tools. The full rules are in
[section 8.2 of the design spec](docs/superpowers/specs/2026-11-24-hivemind-design.md#82-edr-conscious-design-rules).
In short, product code never:

- opens, duplicates, impersonates or uses another process's token, or offers a
  SYSTEM/TrustedInstaller mode
- injects code, hooks, or duplicates handles out of other processes
- launches `reg.exe`, `regedit.exe`, `cmd.exe`, `powershell.exe -EncodedCommand` or other
  living-off-the-land binaries
- uses `Microsoft.Win32.Registry` or `RegistryKey` (tests and benchmarks may)
- saves the SAM, SECURITY or SYSTEM hives with `RegSaveKey(Ex)`
- creates persistence: services, scheduled tasks, Run keys, WMI subscriptions or IFEO `Debugger`
  values
- sends telemetry or makes network calls the user didn't ask for

The banned-API analyzer (HM0001) and the banned-symbol list (`eng/BannedSymbols.txt`, rule RS0030)
enforce much of this in the build. Work through the
[pull request checklist](.github/pull_request_template.md) before you ask for review.
