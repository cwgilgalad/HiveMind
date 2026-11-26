# Developing HiveMind

This page is for contributors who work on Windows. It covers setting up a machine, building,
testing, publishing and dev signing.

## What you need

- Windows 10 version 1809 (build 17763) or later, or Windows 11, on x64 or ARM64
- Git
- The .NET 10 SDK: version 10.0.100 or a later 10.0 feature band, as `global.json` pins it
- The Windows SDK, which provides `signtool.exe` and `makecat.exe`
- Optional: the GitHub CLI (`gh`) and Visual Studio

## Get the code

```powershell
gh repo clone cwgilgalad/HiveMind
cd HiveMind
```

## Set up your machine

Run the setup script from the repository folder. It works in Windows PowerShell 5.1 and in
PowerShell 7.

```powershell
powershell -ExecutionPolicy Bypass -File eng\Setup-DevMachine.ps1
```

The script checks for everything in the list above and prints a table of results. A missing optional
tool shows as a warning. When every required check passes, it runs `dotnet tool restore` and
`dotnet restore HiveMind.slnx`. It exits with 1 if anything required failed.

- `-CheckOnly` runs the checks and changes nothing.
- `-InstallDevCertificate` also sets up your dev signing certificate. See [Dev signing](#dev-signing).

## Build

```powershell
dotnet build HiveMind.slnx
```

Add `-c Release` for a release build. Warnings are errors, and the analyzers and the code style rules
in `.editorconfig` run on every build.

## Test

```powershell
dotnet test HiveMind.slnx
```

To test one project, name it instead: `dotnet test tests/HiveMind.Core.Tests`. The tests use xUnit v3
on Microsoft.Testing.Platform, and `global.json` turns on that runner for `dotnet test`.

The test platform includes Microsoft's telemetry extension. To turn it off, set the environment
variable `TESTINGPLATFORM_TELEMETRY_OPTOUT` (or `DOTNET_CLI_TELEMETRY_OPTOUT`) to `1`.

## Publish

```powershell
pwsh eng/Publish-HiveMind.ps1 -RuntimeIdentifier win-x64 -OutputDirectory artifacts/publish/win-x64
```

This publishes the app and its broker, self-contained, into one folder. Use `win-arm64` for ARM64.
`-Configuration` defaults to `Release`.

## Dev signing

Releases are signed through SignPath Foundation. On your own machine you sign with a self-signed dev
certificate instead. Dev-signed builds are for testing on your machine only.

1. Run `pwsh eng/signing/New-DevCertificate.ps1 -Scope CurrentUser`. It creates the certificate, or
   reuses an unexpired one, and trusts it for your user account. Windows asks you once to confirm
   trusting it. `eng\Setup-DevMachine.ps1 -InstallDevCertificate` runs the same command.
2. Publish, then sign HiveMind's own files and build the catalog with
   `eng/signing/Invoke-DevSign.ps1` and `eng/signing/New-ReleaseCatalog.ps1`.

## What builds on Linux

The platform-neutral projects build and test on Linux as well as on Windows. Today these are
`HiveMind.Core`, `HiveMind.Testing` and `HiveMind.Core.Tests`; the analyzers, the license gate and the
policy tests will join them. The Windows-only projects need Windows: the app, the broker, the interop
tests, the test kit, the UI tests and the benchmarks. CI runs both kinds.

To install .NET on Linux, download Microsoft's `dotnet-install.sh` into an empty folder of its own.
Run `bash <folder>/dotnet-install.sh --jsonfile global.json`, then add `~/.dotnet` to your `PATH`.
