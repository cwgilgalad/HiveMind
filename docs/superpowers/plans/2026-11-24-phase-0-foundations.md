# HiveMind Phase 0 (Foundations) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build HiveMind's guardrails before any feature. That means the solution and its coding
standards, CI, the security core (banned-API analyzer, crypto, audit journal, peer verification),
signing and license gates, test harnesses, governance docs, the threat model and UX mockups.
Phase 1 then lands on rules that are already enforced.

**Architecture:**
- **Projects:**
  - `HiveMind.Core`, platform-neutral: crypto and the journal.
  - `HiveMind.Interop`, Windows-only: process hardening, DPAPI, and signature and catalog checks.
  - An empty WinUI 3 shell and a skeleton elevated broker.
  - A Roslyn analyzer that blocks token-borrowing and injection APIs.
- **Tooling:** a license gate, plus scripts for dev signing and the release catalog.
- **GitHub Actions:**
  - builds x64 and ARM64 on Windows, and runs the platform-neutral tests on Linux too
  - signs builds with a throwaway dev certificate
  - includes a SignPath-ready release workflow that refuses to publish unsigned binaries

**Tech Stack:** C# / .NET 10 (LTS); WinUI 3 (Windows App SDK, unpackaged and self-contained);
CsWin32; xUnit v3; Microsoft.CodeAnalysis (analyzer and analyzer testing); BenchmarkDotNet; FlaUI;
System.Text.Json source generation; PowerShell 5.1 and 7; GitHub Actions; Windows SDK `signtool`
and `makecat`; CycloneDX for .NET.

**Spec:** `docs/superpowers/specs/2026-11-24-hivemind-design.md`. Phase 0 is in §10. The
constraints come from §4, §7.9, §7.10, §8, §9 and §12.

## Global Constraints

**Platform**
- C# on .NET 10 (LTS). `global.json` pins SDK `10.0.100` with `"rollForward": "latestFeature"`.
- The GUI targets Windows 11, Windows 10 1809+ (`TargetPlatformMinVersion` `10.0.17763.0`) and
  Windows Server 2019/2022/2025 with Desktop Experience, on x64 and ARM64. There's no Server 2016
  GUI.

**Security rules (§8.2)**
- Never open, duplicate, impersonate or use another process's token, and offer no
  SYSTEM/TrustedInstaller mode. No injection, hooking or cross-process handle duplication.
  Analyzer HM0001 and banned-symbol rule RS0030 enforce this.
- Product code never launches `reg.exe`, `regedit.exe`, `cmd.exe`, `powershell.exe
  -EncodedCommand` or other LOLBins. Product code (`src/`) never uses `Microsoft.Win32.Registry` or
  `RegistryKey`.
- Every executable starts with `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 |
  LOAD_LIBRARY_SEARCH_APPLICATION_DIR)`. That's §8.1's System32 flag plus the app folder, so the
  signed app-local DLLs still load. It removes the current directory and PATH from DLL search.
- No telemetry, and no network calls the user didn't ask for.

**Crypto and audit**
- Crypto uses .NET's built-in APIs only:
  - AES-256-GCM
  - HKDF-SHA256 per-store keys
  - a random 256-bit master key wrapped with DPAPI (CurrentUser)
  - PBKDF2-HMAC-SHA256 with at least 600,000 iterations
- Journal records have a clear header (sequence, UTC time, action type, actor SID, elevation flag,
  previous hash, hash) and an encrypted body, linked in a SHA-256 hash chain (§7.10).

**Licensing and releases**
- **Dependency licenses:**
  - allowed: `MIT`, `Apache-2.0`, `BSD-2-Clause`, `BSD-3-Clause`, `ISC`, `Zlib`
  - MS-PL: only through a reviewed override
  - denied: GPL, AGPL, LGPL, SSPL, BUSL, and non-commercial Creative Commons
- **Clean room:** no code, text or assets from RegKit (AGPL-3.0), FluentRegEdit (GPL-3.0) or
  closed-source tools.
- **No proprietary binaries in release artifacts.** Never ship `offreg.dll`. Exclude
  `KernelTraceControl.dll`, `KernelTraceControl.Win61.dll` and `msdia140.dll` unless SignPath
  confirms they're acceptable.
- **No binary release ships unsigned.** Signing goes through SignPath Foundation, and releases carry
  exactly this line: `Free code signing provided by SignPath.io, certificate by SignPath Foundation.`

**Performance and workflow**
- Budgets measured in Phase 0 (§12.1): a live full-registry scan within 20 s, and the first live
  search result within 250 ms.
- Work on branch `phase-0/foundations`, created from `docs/owner-decisions`.

## Review Focus

1. **A master key that can't be unwrapped.** Causes include a damaged file, a profile reset, or a
   DPAPI failure. A person expects an error that leads to recovery, never a silently regenerated
   key that orphans all encrypted history. Tests (Task 4):
   - `LoadOrCreate_CorruptFile_ThrowsAndLeavesFileUntouched`
   - `LoadOrCreate_UnprotectFails_ThrowsMasterKeyUnavailable`
2. **A crash in the middle of a journal append** leaves a partial record. The next start must
   detect it and must not keep appending silently. Tests (Task 6):
   - `Verify_TruncatedTail_ReportsLastGoodRecord`
   - `OpenForAppend_TruncatedTail_Throws`
3. **Paths with spaces, non-ASCII characters or more than 260 characters**, such as
   `C:\Users\José Müller\OneDrive\…`. The key store, journal, temp hives and install folders must
   all work. Tests:
   - Task 4: `LoadOrCreate_UnicodePathWithSpaces_Works`, `LoadOrCreate_LongPath_Works`
   - Task 6: `Journal_LongUnicodePath_Works`
   - Task 8: `Create_UnderPathWithSpacesAndUnicode_Works`
   - Task 11: fixtures live under `Install Dir Ü`
4. **A peer reached through a junction or symlink** into the install folder must fail the folder
   check. Test: Task 11, `PeerReachedThroughJunction_FailsNotInInstallDirectory`.
5. **Banned-API evasion** must still be flagged: renamed entry points, A/W suffixes, ordinals, and
   dynamic resolution. Tests:
   - Task 3: `DllImport_EntryPointAlias_ReportsHM0001`, `DllImport_AnsiWideSuffix_ReportsHM0001`,
     `DllImport_OrdinalEntryPoint_ReportsHM0001`, `DllImport_GetProcAddress_ReportsHM0001`
   - Task 3: the policy test row for `NativeLibrary.GetExport`

## Working environment

- **Windows dev box:** follow `docs/superpowers/handoffs/2026-11-24-phase-0-laptop-handoff.md`.
  Everything below runs locally there; CI is the second check, and the only place the Linux job,
  ARM64 tests on x64 machines and the release dry run run.
- **Where things build:**
  - Platform-neutral projects (Core, Analyzers, LicenseGate, Testing, Policy tests) build and test
    on Linux and on Windows.
  - Windows-only projects (Interop tests, TestKit, App, Broker, UI tests, benchmarks) need Windows.
    Run them on a Windows machine, or push the branch and watch CI with
    `gh run watch --repo cwgilgalad/hivemind`.
- **Installing .NET on Linux:** download Microsoft's `dotnet-install.sh` into its own empty folder.
  Run `bash <folder>/dotnet-install.sh --jsonfile global.json`, then add `~/.dotnet` to `PATH`.
- **Skipping:** Windows-only tests call `Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only")`
  first, so `dotnet test` passes with skips elsewhere.
- **Package versions:** add every package to `Directory.Packages.props` at its latest stable
  version, except where a task pins one. Commit the `packages.lock.json` files that restore
  produces.

---

### Task 1: Solution skeleton, standards and dev setup

**Files:**
- Create: `global.json`, `NuGet.config`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `HiveMind.slnx`, `.config/dotnet-tools.json`
- Create: `src/HiveMind.Core/HiveMind.Core.csproj`, `src/HiveMind.Core/BuildInfo.cs`
- Create: `tests/HiveMind.Testing/HiveMind.Testing.csproj`, `tests/HiveMind.Testing/TempDirectory.cs`, `tests/HiveMind.Testing/RepoFiles.cs`
- Create: `tests/HiveMind.Core.Tests/HiveMind.Core.Tests.csproj`, `tests/HiveMind.Core.Tests/BuildInfoTests.cs`
- Create: `eng/Setup-DevMachine.ps1`, `docs/development.md`

**Interfaces:**
- Produces:
  - `HiveMind.Core.BuildInfo`: `public const string ProductName = "HiveMind";` and
    `public static string InformationalVersion { get; }` (read from
    `AssemblyInformationalVersionAttribute`)
  - `HiveMind.Testing.TempDirectory : IDisposable`: `string Path` is a unique new folder under
    `System.IO.Path.GetTempPath()`, deleted recursively on dispose
  - `HiveMind.Testing.RepoFiles`: `static string Root` (walks up from `AppContext.BaseDirectory`
    to the folder containing `HiveMind.slnx`), `static string Path(string relative)`,
    `static string[] ReadLines(string relative)`

- [x] **Step 1: Create branch**

```bash
git switch docs/owner-decisions && git switch -c phase-0/foundations
```

- [x] **Step 2: Write the failing test** in `tests/HiveMind.Core.Tests/BuildInfoTests.cs`

```csharp
public sealed class BuildInfoTests
{
    [Fact]
    public void ProductName_IsHiveMind() => Assert.Equal("HiveMind", BuildInfo.ProductName);

    [Fact]
    public void InformationalVersion_StartsWithDevVersion() =>
        Assert.StartsWith("0.0.0-dev", BuildInfo.InformationalVersion);
}
```

- [x] **Step 3: Create the build infrastructure with these exact values**

`global.json`: `{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}`.

`NuGet.config`: `<clear/>`, nuget.org as the only source, and a `packageSourceMapping` mapping `*`
to nuget.org.

`Directory.Build.props`:
- `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`
- `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`
- `GenerateDocumentationFile=true`, with `NoWarn` covering `CS1591` only
- `Deterministic=true`, and `ContinuousIntegrationBuild=true` when `'$(GITHUB_ACTIONS)'=='true'`
- `PublishRepositoryUrl=true`, `EmbedUntrackedSources=true` (SourceLink, spec §8.3)
- `RestorePackagesWithLockFile=true`, `EnableWindowsTargeting=true`
- `VersionPrefix=0.0.0`, `VersionSuffix=dev`, `Product=HiveMind`,
  `Copyright=Copyright (c) HiveMind contributors`
- `IsProductProject=true` for any project whose directory is under `src/`

`Directory.Packages.props`: `ManagePackageVersionsCentrally=true` and
`CentralPackageTransitivePinningEnabled=true`. Add xunit.v3, xunit.runner.visualstudio and
Microsoft.NET.Test.Sdk.

`.editorconfig`:
- file-scoped namespaces at warning level
- `_camelCase` private fields
- `dotnet_diagnostic.CA1416.severity = error`
- `dotnet_diagnostic.CA2000.severity = warning`
- `dotnet_diagnostic.IDE0005.severity = warning`

Then:
- `HiveMind.Core` targets `net10.0`. `HiveMind.Testing` is a `net10.0` class library.
  `HiveMind.Core.Tests` targets `net10.0` and references both.
- Create `HiveMind.slnx` with `dotnet new sln --format slnx -n HiveMind` (or `dotnet new sln -n HiveMind` if the SDK already defaults to `.slnx`), then add every project with
  `dotnet sln HiveMind.slnx add`.
- Create `.config/dotnet-tools.json` with `dotnet new tool-manifest`.

- [x] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/HiveMind.Core.Tests`
Expected: build error `CS0103: The name 'BuildInfo' does not exist`.

- [x] **Step 5: Implement `BuildInfo` in `src/HiveMind.Core/BuildInfo.cs`**

- [x] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/HiveMind.Core.Tests`
Expected: 2 passed.

- [x] **Step 7: Write `eng/Setup-DevMachine.ps1` and `docs/development.md`**

`Setup-DevMachine.ps1` takes `[-CheckOnly] [-InstallDevCertificate]` and runs on Windows
PowerShell 5.1 and PowerShell 7.

**Required checks:**
- Windows build ≥ 17763
- `git` present
- `dotnet --version` satisfies `global.json`
- Windows SDK `signtool.exe` and `makecat.exe` present (found through
  `eng/signing/Find-WindowsSdkTool.ps1` once Task 10 exists; until then, search
  `${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64`)

**Optional checks** (warnings only): `gh`, and Visual Studio via `vswhere`.

**Behaviour:**
- Without `-CheckOnly`: runs `dotnet tool restore` and `dotnet restore HiveMind.slnx`.
- With `-InstallDevCertificate`: calls `eng/signing/New-DevCertificate.ps1 -Scope CurrentUser`.
- Prints a table of results. Exits 1 if any required check fails, otherwise 0.

`docs/development.md` covers:
- cloning with `gh repo clone cwgilgalad/HiveMind`
- running the setup script
- building (`dotnet build HiveMind.slnx`), testing (`dotnet test HiveMind.slnx`) and publishing
  (`eng/Publish-HiveMind.ps1`, from Task 9)
- dev signing (Task 10)
- which projects build on Linux

- [x] **Step 8: Commit**

```bash
git add global.json NuGet.config Directory.Build.props Directory.Packages.props .editorconfig HiveMind.slnx .config src tests eng docs/development.md
git commit -m "build: add solution skeleton, standards and dev setup"
```

---

### Task 2: CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`, `.github/dependabot.yml`

**Interfaces:**
- Produces: CI jobs `linux-tests` and `windows`, which later tasks extend and which branch
  protection will require.

- [x] **Step 1: Write `.github/workflows/ci.yml`**

**Workflow settings:**
- Triggers: `push` to `main` and `phase-*/**`, and `pull_request`.
- Top-level `permissions: contents: read`.
- `concurrency: ci-${{ github.ref }}` with `cancel-in-progress: true`.
- Every action pinned to a full commit SHA, with a `# vX.Y.Z` comment.
- `timeout-minutes: 30` per job.

**Jobs:**
- **`linux-tests`** (`ubuntu-latest`): checkout, then `actions/setup-dotnet` with
  `global-json-file: global.json`, then `dotnet test tests/HiveMind.Core.Tests -c Release`.
- **`windows`** (`windows-latest`), in order:
  1. checkout and setup-dotnet
  2. `pwsh -File eng/Setup-DevMachine.ps1 -CheckOnly`
  3. `dotnet restore HiveMind.slnx --locked-mode`
  4. `dotnet build HiveMind.slnx -c Release --no-restore`
  5. `dotnet test HiveMind.slnx -c Release --no-build --logger trx --results-directory artifacts/test-results`
  6. on failure, upload `artifacts/test-results`
- **`windows-arm64-tests`** (`windows-11-arm`): the same restore, build and test steps as
  `windows`, run only when `${{ !github.event.repository.private }}`. ARM64 tests (spec §12.2)
  start once the repo is public; until then ARM64 is build-only.

- [x] **Step 2: Write `.github/dependabot.yml`**

Two ecosystems, both checked weekly:
- `nuget` at `/`, with minor and patch updates grouped
- `github-actions` at `/`

- [x] **Step 3: Push and verify**

Run: `git add .github && git commit -m "ci: build and test on Windows and Linux" && git push -u origin phase-0/foundations && gh run watch --repo cwgilgalad/hivemind`
Expected: `linux-tests` and `windows` both succeed.

---

### Task 3: Banned-API enforcement

**Files:**
- Create: `src/HiveMind.Analyzers/HiveMind.Analyzers.csproj`, `BannedNativeApis.cs`, `BannedNativeApiAnalyzer.cs`, `AnalyzerReleases.Shipped.md`, `AnalyzerReleases.Unshipped.md`
- Create: `tests/HiveMind.Analyzers.Tests/HiveMind.Analyzers.Tests.csproj`, `BannedNativeApiAnalyzerTests.cs`
- Create: `eng/BannedSymbols.txt`
- Create: `tests/HiveMind.Policy.Tests/HiveMind.Policy.Tests.csproj`, `BannedSymbolsPolicyTests.cs`
- Create: `.github/pull_request_template.md`
- Modify: `Directory.Build.props`, `.github/workflows/ci.yml`

**Interfaces:**
- **Diagnostic `HM0001`:**
  - severity Error, category `Security`, title `Banned native API`
  - message `'{0}' is banned in HiveMind: {1}`
  - reported on the identifier of a method declaration with `DllImportAttribute` or
    `LibraryImportAttribute` whose entry point is banned
  - the entry point is the `EntryPoint` named argument if present, otherwise the method name
- **`public static class BannedNativeApis`:**
  `public static bool TryGetReason(string entryPoint, out string reason)`
  - matching is case-insensitive: the exact name, or the name without a trailing `A` or `W`
  - any entry point starting with `#` (an ordinal import) is banned
- **The analyzer** analyzes generated code too:
  `GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics`. That catches
  CsWin32 output.

**Banned entry points (exact list), with reasons:**
- **Token borrowing** (reason "borrows or forges a security context (spec §8.2)"):
  `DuplicateToken`, `DuplicateTokenEx`, `ImpersonateLoggedOnUser`, `ImpersonateNamedPipeClient`,
  `ImpersonateAnonymousToken`, `ImpersonateDdeClientWindow`, `SetThreadToken`,
  `CreateProcessWithTokenW`, `CreateProcessAsUser`, `CreateProcessWithLogonW`, `LogonUser`,
  `LogonUserEx`, `NtDuplicateToken`, `NtImpersonateThread`, `NtSetInformationThread`,
  `NtCreateToken`, `NtCreateTokenEx`, `NtOpenProcessToken`, `CoImpersonateClient`,
  `RpcImpersonateClient`.
- **Injection or memory tampering** (reason "process injection or memory tampering (spec §8.2)"):
  `WriteProcessMemory`, `ReadProcessMemory`, `VirtualAllocEx`, `VirtualProtect`,
  `VirtualProtectEx`, `CreateRemoteThread`, `CreateRemoteThreadEx`, `NtCreateThreadEx`,
  `RtlCreateUserThread`, `QueueUserAPC`, `NtQueueApcThread`, `NtQueueApcThreadEx`,
  `SetWindowsHookEx`, `NtWriteVirtualMemory`, `NtReadVirtualMemory`, `NtMapViewOfSection`.
- **Handle duplication** (reason "duplicates handles across processes (spec §8.2, X-05)"):
  `DuplicateHandle`, `NtDuplicateObject`.
- **Dynamic resolution** (reason "hides imports from review; declare the API with CsWin32 or
  LibraryImport"): `GetProcAddress`, `LoadLibrary`, `LoadLibraryEx`, `LdrLoadDll`,
  `LdrGetProcedureAddress`.
- **Memory dumping** (reason "process memory dumping"): `MiniDumpWriteDump`.
- **Persistence** (reason "installs a service (spec §8.2)"): `CreateService`.

**`eng/BannedSymbols.txt`** (format `DocId;Message`) must contain at least:

```
M:System.Security.Principal.WindowsIdentity.RunImpersonated(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle,System.Action);Token impersonation is banned (spec 8.2)
M:System.Security.Principal.WindowsIdentity.RunImpersonated``1(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle,System.Func{``0});Token impersonation is banned (spec 8.2)
M:System.Security.Principal.WindowsIdentity.RunImpersonatedAsync(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle,System.Func{System.Threading.Tasks.Task});Token impersonation is banned (spec 8.2)
M:System.Security.Principal.WindowsIdentity.RunImpersonatedAsync``1(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle,System.Func{System.Threading.Tasks.Task{``0}});Token impersonation is banned (spec 8.2)
M:System.IO.Pipes.NamedPipeServerStream.RunAsClient(System.IO.Pipes.PipeStreamImpersonationWorker);Pipe-client impersonation is banned (spec 8.2)
M:System.Diagnostics.Process.Start;Process launches need a reviewed exception (spec 8.2)
M:System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo);Process launches need a reviewed exception (spec 8.2)
M:System.Diagnostics.Process.Start(System.String);Process launches need a reviewed exception (spec 8.2)
M:System.Diagnostics.Process.Start(System.String,System.String);Process launches need a reviewed exception (spec 8.2)
M:System.Diagnostics.Process.Start(System.String,System.Collections.Generic.IEnumerable{System.String});Process launches need a reviewed exception (spec 8.2)
M:System.Diagnostics.Process.Start(System.String,System.String,System.Security.SecureString,System.String);Running as another user is banned (spec 8.2)
M:System.Diagnostics.Process.Start(System.String,System.String,System.String,System.Security.SecureString,System.String);Running as another user is banned (spec 8.2)
T:Microsoft.Win32.Registry;Use HiveMind's registry access layer (spec 7.3)
T:Microsoft.Win32.RegistryKey;Use HiveMind's registry access layer (spec 7.3)
M:System.Runtime.InteropServices.NativeLibrary.GetExport(System.IntPtr,System.String);Dynamic export lookup hides imports (spec 8.2)
M:System.Runtime.InteropServices.NativeLibrary.TryGetExport(System.IntPtr,System.String,System.IntPtr@);Dynamic export lookup hides imports (spec 8.2)
M:System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(System.IntPtr,System.Type);Dynamic export lookup hides imports (spec 8.2)
M:System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer``1(System.IntPtr);Dynamic export lookup hides imports (spec 8.2)
```

- [x] **Step 1: Write the failing analyzer tests** in `tests/HiveMind.Analyzers.Tests/BannedNativeApiAnalyzerTests.cs`

Use `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`
(`CSharpAnalyzerTest<BannedNativeApiAnalyzer, DefaultVerifier>`) with
`ReferenceAssemblies = ReferenceAssemblies.Net.Net80`. `{|HM0001:Name|}` marks the expected
diagnostic span.

```csharp
public sealed class BannedNativeApiAnalyzerTests
{
    private static Task Verify(string source) =>
        new CSharpAnalyzerTest<BannedNativeApiAnalyzer, DefaultVerifier>
        { TestCode = source, ReferenceAssemblies = ReferenceAssemblies.Net.Net80 }.RunAsync();

    [Fact]
    public Task DllImport_DuplicateTokenEx_ReportsHM0001() => Verify("""
        using System.Runtime.InteropServices;
        static class N { [DllImport("advapi32.dll")] static extern bool {|HM0001:DuplicateTokenEx|}(nint a, uint b, nint c, int d, int e, out nint f); }
        """);

    [Fact]
    public Task DllImport_EntryPointAlias_ReportsHM0001() => Verify("""
        using System.Runtime.InteropServices;
        static class N { [DllImport("advapi32.dll", EntryPoint = "ImpersonateNamedPipeClient")] static extern bool {|HM0001:Harmless|}(nint pipe); }
        """);

    [Fact]
    public Task DllImport_AnsiWideSuffix_ReportsHM0001() => Verify("""
        using System.Runtime.InteropServices;
        static class N { [DllImport("user32.dll")] static extern nint {|HM0001:SetWindowsHookExW|}(int id, nint fn, nint mod, uint tid); }
        """);

    [Fact]
    public Task DllImport_OrdinalEntryPoint_ReportsHM0001() => Verify("""
        using System.Runtime.InteropServices;
        static class N { [DllImport("kernel32.dll", EntryPoint = "#123")] static extern int {|HM0001:Anything|}(); }
        """);

    [Fact]
    public Task DllImport_GetProcAddress_ReportsHM0001() => Verify("""
        using System.Runtime.InteropServices;
        static class N { [DllImport("kernel32.dll")] static extern nint {|HM0001:GetProcAddress|}(nint module, string name); }
        """);

    [Fact]
    public Task LibraryImport_CreateProcessAsUserW_ReportsHM0001() => Verify("""
        using System.Runtime.InteropServices;
        static partial class N
        {
            [LibraryImport("advapi32.dll", EntryPoint = "CreateProcessAsUserW")] private static partial int {|HM0001:Run|}();
            private static partial int Run() => 0;
        }
        """);

    [Fact]
    public Task DllImport_LowercaseName_ReportsHM0001() => Verify("""
        using System.Runtime.InteropServices;
        static class N { [DllImport("advapi32.dll")] static extern bool {|HM0001:duplicatetokenex|}(); }
        """);

    [Fact]
    public Task GeneratedCode_DuplicateTokenEx_ReportsHM0001() => Verify("""
        // <auto-generated/>
        using System.Runtime.InteropServices;
        static class N { [DllImport("advapi32.dll")] static extern bool {|HM0001:DuplicateTokenEx|}(); }
        """);

    [Fact]
    public Task DllImport_RegOpenKeyExW_NoDiagnostic() => Verify("""
        using System.Runtime.InteropServices;
        static class N { [DllImport("advapi32.dll")] static extern int RegOpenKeyExW(nint key, string sub, uint opt, uint sam, out nint result); }
        """);
}
```

- [x] **Step 2: Write the failing policy test** in `tests/HiveMind.Policy.Tests/BannedSymbolsPolicyTests.cs` (`HiveMind.Policy.Tests` targets `net10.0` and references `HiveMind.Testing`)

```csharp
public sealed class BannedSymbolsPolicyTests
{
    [Theory]
    [InlineData("M:System.IO.Pipes.NamedPipeServerStream.RunAsClient(System.IO.Pipes.PipeStreamImpersonationWorker)")]
    [InlineData("M:System.Security.Principal.WindowsIdentity.RunImpersonated(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle,System.Action)")]
    [InlineData("M:System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)")]
    [InlineData("M:System.Diagnostics.Process.Start(System.String,System.String,System.Security.SecureString,System.String)")]
    [InlineData("T:Microsoft.Win32.Registry")]
    [InlineData("T:Microsoft.Win32.RegistryKey")]
    [InlineData("M:System.Runtime.InteropServices.NativeLibrary.GetExport(System.IntPtr,System.String)")]
    public void BannedSymbols_ContainsRequiredEntry(string docId) =>
        Assert.Contains(docId, RepoFiles.ReadLines("eng/BannedSymbols.txt").Select(l => l.Split(';')[0].Trim()));

    [Fact]
    public void BuildProps_WireBannedSymbolsIntoProductProjects() =>
        Assert.Contains("eng/BannedSymbols.txt", File.ReadAllText(RepoFiles.Path("Directory.Build.props")).Replace('\\', '/'));
}
```

- [x] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/HiveMind.Analyzers.Tests && dotnet test tests/HiveMind.Policy.Tests`
Expected: compile errors (no analyzer type yet), then policy failures (the file is missing).

- [x] **Step 4: Implement the analyzer**

Implement `BannedNativeApis` and `BannedNativeApiAnalyzer`. The analyzer project targets
`netstandard2.0` with `IsRoslynComponent=true` and `EnforceExtendedAnalyzerRules=true`. Pin
`Microsoft.CodeAnalysis.CSharp` to the newest version that isn't newer than the Roslyn compiler in
the pinned .NET 10 SDK. Register `SymbolKind.Method`, read the method's attributes, and resolve the
entry point.

- [x] **Step 5: Write `eng/BannedSymbols.txt` and wire both analyzers into product projects**

In `Directory.Build.props`, when `IsProductProject` is true and the project isn't
`HiveMind.Analyzers`, add:
- `<ProjectReference Include="$(MSBuildThisFileDirectory)src/HiveMind.Analyzers/HiveMind.Analyzers.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`
- a `PackageReference` to `Microsoft.CodeAnalysis.BannedApiAnalyzers`
- `<AdditionalFiles Include="$(MSBuildThisFileDirectory)eng/BannedSymbols.txt" />`

- [x] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/HiveMind.Analyzers.Tests && dotnet test tests/HiveMind.Policy.Tests`
Expected: all pass.

- [x] **Step 7: Write `.github/pull_request_template.md`**

A checklist with these items:
- No new native imports, or each new import is listed in CsWin32's `NativeMethods.txt` and reviewed
  (HM0001 is clean).
- No process launches, token use, impersonation, injection, handle duplication or dynamic API
  resolution.
- No persistence (services, scheduled tasks, Run keys, IFEO, WMI subscriptions) and no new network
  calls.
- Every new action that changes state, uses privilege, moves data or crosses a boundary writes a
  journal entry.
- SAM and SECURITY stay excluded, and there's no `RegSaveKey` of SAM, SECURITY or SYSTEM.
- New dependencies pass the license gate, and no proprietary binaries are added to release output.
  New license overrides are listed for owner review.
- Tests cover the change, and Windows-only tests skip elsewhere.
- External contributors only: every commit carries `Signed-off-by:` (DCO).

- [x] **Step 8: Add the new test projects to CI and commit**

Add `tests/HiveMind.Analyzers.Tests` and `tests/HiveMind.Policy.Tests` to the `linux-tests` job.

```bash
git add src/HiveMind.Analyzers tests/HiveMind.Analyzers.Tests tests/HiveMind.Policy.Tests eng/BannedSymbols.txt Directory.Build.props Directory.Packages.props .github HiveMind.slnx
git commit -m "feat(security): ban token-borrowing, injection and dynamic-import APIs (HM0001, RS0030)"
```

---

### Task 4: Key hierarchy

**Files:**
- Create: `src/HiveMind.Core/Crypto/IKeyProtector.cs`, `MasterKey.cs`, `StoreKind.cs`, `KeyDerivation.cs`, `MasterKeyStore.cs`, `MasterKeyUnavailableException.cs`, `PassphraseKeyWrap.cs`, `MasterKeyBackup.cs`
- Test: `tests/HiveMind.Core.Tests/Crypto/FakeKeyProtector.cs`, `MasterKeyTests.cs`, `KeyDerivationTests.cs`, `MasterKeyStoreTests.cs`, `PassphraseKeyWrapTests.cs`

**Interfaces:**
- Produces:

```csharp
public interface IKeyProtector { string Id { get; } byte[] Protect(ReadOnlySpan<byte> secret); byte[] Unprotect(ReadOnlySpan<byte> protectedData); }
public sealed class MasterKey : IDisposable { public const int SizeInBytes = 32; public static MasterKey Generate(); public static MasterKey FromBytes(ReadOnlySpan<byte> keyBytes); public void Dispose(); }
public enum StoreKind { Journal, Index, Snapshots, Captures, Notes, Drafts }
public static class KeyDerivation { public static byte[] DeriveStoreKey(MasterKey masterKey, StoreKind store); }
public static class MasterKeyStore { public static MasterKey LoadOrCreate(string path, IKeyProtector protector); }
public sealed class MasterKeyUnavailableException : Exception { public MasterKeyUnavailableException(string message, Exception? innerException = null); }
public static class PassphraseKeyWrap { public const int MinimumIterations = 600_000; public static byte[] Wrap(ReadOnlySpan<byte> secret, string passphrase, int iterations = MinimumIterations); public static byte[] Unwrap(ReadOnlySpan<byte> wrapped, string passphrase); }
public static class MasterKeyBackup { public static byte[] Export(MasterKey masterKey, string recoveryPassphrase); public static MasterKey Import(ReadOnlySpan<byte> backup, string recoveryPassphrase); }
```

**Values and rules:**
- **`MasterKey`:**
  - keeps its 32 bytes in `GC.AllocateArray<byte>(32, pinned: true)`
  - zeroes them on dispose with `CryptographicOperations.ZeroMemory`
  - throws `ObjectDisposedException` when used after dispose
  - `FromBytes` throws `ArgumentException` unless given exactly 32 bytes
- **HKDF:** SHA-256 with 32-byte output and an empty salt. `info` is the UTF-8 of
  `hivemind/v1/store/<store in lowercase>`.
- **Master key file:**
  - layout: ASCII `HMMK`, version byte `1`, protector-id length (1 byte), protector id (ASCII),
    protected-blob length (int32 little-endian), protected blob
  - writes are atomic: a temp file in the same folder, then `File.Move(temp, path, overwrite: true)`,
    creating parent folders as needed
- **`LoadOrCreate`** creates a key only when the file doesn't exist. Any other problem throws
  `MasterKeyUnavailableException`, and the file is never modified or replaced. Problems include a
  bad magic or version, a protector-id mismatch, a length mismatch, or `Unprotect` throwing.
- **Passphrase wrap:**
  - layout: ASCII `HMPW`, version `1`, iterations (int32 little-endian), salt (16), nonce (12),
    ciphertext, tag (16)
  - the key is `Rfc2898DeriveBytes.Pbkdf2` with HMAC-SHA256, the UTF-8 passphrase, and a 32-byte
    output
  - the associated data is every byte before the nonce
  - `Wrap` throws `ArgumentOutOfRangeException` when `iterations < 600_000`
  - `Unwrap` throws `CryptographicException` when the stored iterations are below 600,000 or the
    tag doesn't verify
- **`MasterKeyBackup`** = `PassphraseKeyWrap` over the 32 key bytes.

- [x] **Step 1: Write the failing tests**

`FakeKeyProtector(string id = "fake", bool failUnprotect = false)` XORs bytes with `0x5A`. When
`failUnprotect` is set, `Unprotect` throws `CryptographicException`. Tests compare keys through
`KeyDerivation` output, never through raw bytes.

```csharp
public sealed class MasterKeyStoreTests
{
    private static byte[] Fingerprint(MasterKey key) => KeyDerivation.DeriveStoreKey(key, StoreKind.Journal);

    [Fact]
    public void LoadOrCreate_MissingFile_CreatesKeyThatReloadsIdentically()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "keys", "masterkey.bin");
        using var created = MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector());
        using var reloaded = MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector());
        Assert.Equal(Fingerprint(created), Fingerprint(reloaded));
        Assert.Equal("HMMK"u8.ToArray(), File.ReadAllBytes(path)[..4]);
    }

    [Fact]
    public void LoadOrCreate_CorruptFile_ThrowsAndLeavesFileUntouched()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "masterkey.bin");
        File.WriteAllBytes(path, [0x48, 0x4D, 0x00]);
        Assert.Throws<MasterKeyUnavailableException>(() => MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector()));
        Assert.Equal(new byte[] { 0x48, 0x4D, 0x00 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void LoadOrCreate_UnprotectFails_ThrowsMasterKeyUnavailable()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "masterkey.bin");
        MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector()).Dispose();
        var before = File.ReadAllBytes(path);
        Assert.Throws<MasterKeyUnavailableException>(() => MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector(failUnprotect: true)));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void LoadOrCreate_DifferentProtectorId_Throws()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "masterkey.bin");
        MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector("dpapi-user")).Dispose();
        Assert.Throws<MasterKeyUnavailableException>(() => MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector("dpapi-machine")));
    }

    [Fact]
    public void LoadOrCreate_UnicodePathWithSpaces_Works()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "José Müller", "key store", "masterkey.bin");
        using var key = MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LoadOrCreate_LongPath_Works()
    {
        using var dir = new TempDirectory();
        var folder = dir.Path;
        while (folder.Length < 300) folder = Path.Combine(folder, "nested-folder-segment");
        using var key = MasterKeyStore.LoadOrCreate(Path.Combine(folder, "masterkey.bin"), new FakeKeyProtector());
        Assert.True(File.Exists(Path.Combine(folder, "masterkey.bin")));
    }
}
```

Also write these tests:
- `MasterKeyTests`:
  - `Generate_TwoKeys_Differ`
  - `FromBytes_WrongLength_Throws` (31 and 33 bytes)
  - `UseAfterDispose_ThrowsObjectDisposed`
- `KeyDerivationTests`:

```csharp
[Fact]
public void DeriveStoreKey_MatchesHkdfWithPinnedLabel()
{
    var ikm = Enumerable.Repeat((byte)0x01, 32).ToArray();
    using var key = MasterKey.FromBytes(ikm);
    var expected = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, salt: [], info: "hivemind/v1/store/journal"u8.ToArray());
    Assert.Equal(expected, KeyDerivation.DeriveStoreKey(key, StoreKind.Journal));
}

[Fact]
public void DeriveStoreKey_EachStore_IsDistinct()
{
    using var key = MasterKey.Generate();
    var keys = Enum.GetValues<StoreKind>().Select(s => Convert.ToHexString(KeyDerivation.DeriveStoreKey(key, s)));
    Assert.Equal(6, keys.Distinct().Count());
}
```

- `PassphraseKeyWrapTests`:
  - `Wrap_ThenUnwrap_RoundTrips`
  - `Unwrap_WrongPassphrase_ThrowsCryptographicException`
  - `Wrap_IterationsBelowMinimum_Throws` (599,999)
  - `Wrapped_StartsWithMagicAndVersion` (`HMPW`, `1`)
  - `MasterKeyBackup_ExportThenImport_RestoresSameKey`

- [x] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/HiveMind.Core.Tests --filter "FullyQualifiedName~Crypto"`
Expected: compile errors for the missing types.

- [x] **Step 3: Implement the eight types** in `src/HiveMind.Core/Crypto/`, per the values above

- [x] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/HiveMind.Core.Tests --filter "FullyQualifiedName~Crypto"`
Expected: all pass.

- [x] **Step 5: Commit**

```bash
git add src/HiveMind.Core/Crypto tests/HiveMind.Core.Tests/Crypto
git commit -m "feat(crypto): master key, store-key derivation and passphrase backup"
```

---

### Task 5: Record and stream encryption

**Files:**
- Create: `src/HiveMind.Core/Crypto/RecordCipher.cs`, `StreamCipher.cs`
- Test: `tests/HiveMind.Core.Tests/Crypto/RecordCipherTests.cs`, `StreamCipherTests.cs`

**Interfaces:**
- Consumes: nothing from Task 4 except that store keys are 32 bytes.
- Produces:

```csharp
public sealed class RecordCipher : IDisposable { public const int Overhead = 28; public RecordCipher(ReadOnlySpan<byte> key); public byte[] Seal(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData); public byte[] Open(ReadOnlySpan<byte> sealedRecord, ReadOnlySpan<byte> associatedData); public void Dispose(); }
public static class StreamCipher { public const int DefaultChunkSize = 65_536; public static void Encrypt(Stream plaintext, Stream ciphertext, ReadOnlySpan<byte> storeKey, int chunkSize = DefaultChunkSize); public static void Decrypt(Stream ciphertext, Stream plaintext, ReadOnlySpan<byte> storeKey); }
```

**`RecordCipher`:**
- AES-256-GCM (`new AesGcm(key, 16)`) with a random 12-byte nonce per `Seal`.
- Output layout: `nonce | ciphertext | tag`.
- A key that isn't 32 bytes throws `ArgumentException`. Every authentication or length failure
  throws `CryptographicException`.
- Document the limit in an XML comment: at most 2^32 records per key, because nonces are random.

**`StreamCipher`:**
- **Header (41 bytes):** ASCII `HMS1`, version `1`, chunk size (int32 little-endian, 1,024 to
  16,777,216), 32 random salt bytes.
- **Stream key:** `HKDF.DeriveKey(SHA256, storeKey, 32, salt, UTF-8 "hivemind/v1/stream")`.
- **Chunk *i* (from 0):**
  - nonce: 4 zero bytes, then *i* as a uint64 big-endian
  - associated data: the header, then *i* as a uint64 big-endian, then a final-flag byte (`1` for
    the last chunk, otherwise `0`)
  - stored as ciphertext followed by a 16-byte tag
- **Chunk sizes:** every chunk except the last holds exactly `chunkSize` plaintext bytes. If the
  plaintext length is a multiple of `chunkSize` (including 0), write an empty final chunk.
- **`Decrypt`** treats the chunk followed by end-of-stream as the final chunk. Truncation,
  reordering, trailing bytes, header tampering and a wrong key all throw `CryptographicException`.

- [x] **Step 1: Write the failing tests**

```csharp
public sealed class StreamCipherTests
{
    private const int Chunk = 1024, Header = 41, Stored = Chunk + 16;
    private static byte[] Enc(byte[] data, byte[] key) { using var o = new MemoryStream(); StreamCipher.Encrypt(new MemoryStream(data), o, key, Chunk); return o.ToArray(); }
    private static byte[] Dec(byte[] data, byte[] key) { using var o = new MemoryStream(); StreamCipher.Decrypt(new MemoryStream(data), o, key); return o.ToArray(); }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(1023)] [InlineData(1024)] [InlineData(1025)] [InlineData(3072)]
    public void EncryptThenDecrypt_RoundTrips(int length)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var data = RandomNumberGenerator.GetBytes(length);
        Assert.Equal(data, Dec(Enc(data, key), key));
    }

    [Fact]
    public void Decrypt_TruncatedAtChunkBoundary_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ct = Enc(RandomNumberGenerator.GetBytes(3072), key);
        Assert.ThrowsAny<CryptographicException>(() => Dec(ct[..(Header + 2 * Stored)], key));
    }

    [Fact]
    public void Decrypt_SwappedChunks_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ct = Enc(RandomNumberGenerator.GetBytes(3072), key);
        var swapped = ct[..Header].Concat(ct[(Header + Stored)..(Header + 2 * Stored)]).Concat(ct[Header..(Header + Stored)]).Concat(ct[(Header + 2 * Stored)..]).ToArray();
        Assert.ThrowsAny<CryptographicException>(() => Dec(swapped, key));
    }
}
```

Also write these tests:
- `Decrypt_FlippedBit_Throws`
- `Decrypt_TrailingGarbage_Throws`
- `Decrypt_TamperedHeaderChunkSize_Throws`
- `Decrypt_WrongKey_Throws`
- `Encrypt_SameInputTwice_DiffersBecauseOfSalt`
- `Encrypt_ChunkSizeOutOfRange_Throws` (1,023 and 16,777,217)

`RecordCipherTests`:
- `SealThenOpen_RoundTrips`
- `Open_WrongAssociatedData_Throws`
- `Open_FlippedByte_Throws`
- `Seal_SameInputTwice_UsesDifferentNonces`
- `Constructor_KeyNot32Bytes_Throws`
- `Open_ShorterThanOverhead_Throws`
- `SealedLength_IsPlaintextPlus28`

- [x] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/HiveMind.Core.Tests --filter "FullyQualifiedName~Cipher"`
Expected: compile errors.

- [x] **Step 3: Implement `RecordCipher` and `StreamCipher`** per the format above

- [x] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/HiveMind.Core.Tests --filter "FullyQualifiedName~Cipher"`
Expected: all pass.

- [x] **Step 5: Commit**

```bash
git add src/HiveMind.Core/Crypto tests/HiveMind.Core.Tests/Crypto
git commit -m "feat(crypto): AES-GCM record and chunked stream encryption"
```

---

### Task 6: Audit journal

**Files:**
- Create: `src/HiveMind.Core/Journal/JournalAction.cs`, `JournalEntry.cs`, `JournalRecord.cs`, `JournalSegmentHeader.cs`, `JournalFormat.cs`, `JournalWriter.cs`, `JournalReader.cs`, `JournalVerifier.cs`, `JournalCorruptException.cs`, `JournalJsonContext.cs`
- Test: `tests/HiveMind.Core.Tests/Journal/JournalWriterReaderTests.cs`, `JournalVerifierTests.cs`
- Modify: `Directory.Packages.props` (add `Microsoft.Extensions.TimeProvider.Testing` for tests)

**Interfaces:**
- Consumes: `RecordCipher` (Task 5).
- Produces:

```csharp
public enum JournalAction : ushort { RegistryWrite = 1, Privilege = 2, DataMovement = 3, Boundary = 4, Configuration = 5, Navigation = 100, Search = 101 }
public sealed record JournalBody(string Kind, string? Target = null, string? Note = null, IReadOnlyDictionary<string, string>? Details = null);
public sealed record JournalEntry(JournalAction Action, string ActorSid, bool Elevated, JournalBody Body);
public sealed record JournalRecord(ulong Sequence, DateTimeOffset TimestampUtc, JournalAction Action, string ActorSid, bool Elevated, byte[] PreviousHash, byte[] EncryptedBody, byte[] Hash, long FileOffset, int TotalLength);
public sealed record JournalSegmentHeader(Guid SegmentId, byte[] PreviousSegmentHash, DateTimeOffset CreatedUtc);
public enum JournalProblem { None, BadHeader, TruncatedTail, SequenceGap, BrokenChain, HashMismatch }
public sealed record JournalVerification(bool IsIntact, ulong RecordCount, ulong? FirstBadSequence, JournalProblem Problem, byte[] LastGoodHash);
public sealed class JournalCorruptException : Exception { public JournalProblem Problem { get; } public ulong LastGoodSequence { get; } }
public sealed class JournalWriter : IDisposable { public static JournalWriter Create(string segmentPath, ReadOnlySpan<byte> journalKey, byte[]? previousSegmentHash = null, TimeProvider? timeProvider = null); public static JournalWriter OpenForAppend(string segmentPath, ReadOnlySpan<byte> journalKey, TimeProvider? timeProvider = null); public JournalRecord Append(JournalEntry entry); public void Dispose(); }
public static class JournalReader { public static JournalSegmentHeader ReadHeader(string segmentPath); public static IEnumerable<JournalRecord> ReadRecords(string segmentPath); public static JournalBody DecryptBody(JournalRecord record, ReadOnlySpan<byte> journalKey); }
public static class JournalVerifier { public static JournalVerification Verify(string segmentPath); }
```

**Segment format** (all little-endian):
- **Header (62 bytes):** ASCII `HMJ1`, format version (uint16) `1`, segment id (16 bytes,
  `Guid.ToByteArray()`), previous-segment hash (32 bytes, all zero if none), created time
  (int64 UTC ticks).
- **Record fields, in order:**
  1. `recordLength` (uint32: bytes after this field, including the hash)
  2. `sequence` (uint64; starts at 1)
  3. UTC ticks (int64)
  4. action (uint16)
  5. flags (uint8; bit 0 = elevated)
  6. actor-SID length (uint16, at most 256), then the actor SID in UTF-8
  7. previous hash (32 bytes)
  8. body length (uint32, at most 16 MiB), then the body
  9. hash (32 bytes)

**Rules:**
- **`hash`** = SHA-256 over the bytes from `sequence` through the end of the body.
- **The body** is `RecordCipher.Seal` applied to the UTF-8 JSON of `JournalBody` (serialized
  through `JournalJsonContext` in camelCase, omitting nulls). Its associated data is the bytes from
  `sequence` through the previous hash.
- **The first record's previous hash** is the header's previous-segment hash.
- **Limits:** if the actor SID or body exceeds its limit, `Append` throws `ArgumentException`.
- **Durability:** `Append` writes each record in a single write call and calls
  `Flush(flushToDisk: true)` before returning.
- **Timestamps** are stored as UTC from `TimeProvider.GetUtcNow()`.
- **`ReadRecords`** stops at the first malformed record.
- **`Verify`:**
  - checks the header, sequence continuity, the previous-hash links and each hash, and reports the
    first problem
  - `RecordCount` = the number of valid records before the problem
  - `FirstBadSequence` = `RecordCount + 1`
- **`OpenForAppend`** runs `Verify` first. On any problem it throws `JournalCorruptException` with
  `LastGoodSequence = RecordCount`.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class JournalWriterReaderTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    private const string Sid = "S-1-5-21-1000-2000-3000-1001";

    [Fact]
    public void Append_ThenRead_ChainsHashesAndEncryptsBody()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "segment-0001.hmj");
        using (var writer = JournalWriter.Create(path, Key))
        {
            writer.Append(new JournalEntry(JournalAction.RegistryWrite, Sid, false, new JournalBody("registry.set", @"HKCU\Software\Contoso", "CHG-1042")));
            writer.Append(new JournalEntry(JournalAction.Privilege, Sid, true, new JournalBody("broker.start")));
        }
        var records = JournalReader.ReadRecords(path).ToList();
        Assert.Equal(new ulong[] { 1, 2 }, records.Select(r => r.Sequence));
        Assert.Equal(new byte[32], records[0].PreviousHash);
        Assert.Equal(records[0].Hash, records[1].PreviousHash);
        Assert.True(records[1].Elevated);
        Assert.Equal(JournalAction.Privilege, records[1].Action);
        Assert.Equal(-1, File.ReadAllBytes(path).AsSpan().IndexOf("CHG-1042"u8));
        Assert.Equal("CHG-1042", JournalReader.DecryptBody(records[0], Key).Note);
    }

    [Fact]
    public void DecryptBody_MovedToAnotherRecord_Throws()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "s.hmj");
        using (var w = JournalWriter.Create(path, Key)) { w.Append(new(JournalAction.Configuration, Sid, false, new("a"))); w.Append(new(JournalAction.Configuration, Sid, false, new("b"))); }
        var r = JournalReader.ReadRecords(path).ToList();
        Assert.ThrowsAny<CryptographicException>(() => JournalReader.DecryptBody(r[1] with { EncryptedBody = r[0].EncryptedBody }, Key));
    }

    [Fact]
    public void Append_StoresUtcTimestamp()
    {
        using var dir = new TempDirectory();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 15, 30, 0, TimeSpan.FromHours(-5)));
        using var w = JournalWriter.Create(Path.Combine(dir.Path, "s.hmj"), Key, timeProvider: clock);
        var record = w.Append(new(JournalAction.Configuration, Sid, false, new("settings.change")));
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 20, 30, 0, TimeSpan.Zero), record.TimestampUtc);
    }
}
```

Also write these tests:
- `JournalWriterReaderTests`:
  - `DecryptBody_WrongKey_Throws`
  - `Header_ActionSidAndTimeReadableWithoutKey`
  - `Create_WithPreviousSegmentHash_FirstRecordLinksToIt`
  - `OpenForAppend_ContinuesSequenceAndChain` (append 2, reopen, append 1: sequence 3 links to
    record 2, and `Verify` is intact with 3 records)
  - `Append_BodyOver16MiB_ThrowsArgumentException`
  - `Append_ActorSidOver256Bytes_ThrowsArgumentException`
  - `Journal_LongUnicodePath_Works` (path longer than 300 characters, under a `José Müller` folder)
- `JournalVerifierTests`, built from 3-record segments and edited with `FileOffset` /
  `TotalLength`:
  - `Verify_Intact_ReturnsCountAndLastHash`
  - `Verify_FlippedBodyByte_ReportsHashMismatchAtSequence2`
  - `Verify_DeletedMiddleRecord_ReportsSequenceGapAt2`
  - `Verify_SwappedRecords_ReportsSequenceGap`
  - `Verify_EditedPreviousHash_ReportsBrokenChain`
  - `Verify_BadMagic_ReportsBadHeader`
  - `Verify_TruncatedTail_ReportsLastGoodRecord`: cut 10 bytes; expect `TruncatedTail`,
    `RecordCount` 2, `FirstBadSequence` 3
  - `OpenForAppend_TruncatedTail_Throws`: expect `JournalCorruptException` with
    `Problem == TruncatedTail` and `LastGoodSequence == 2`

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/HiveMind.Core.Tests --filter "FullyQualifiedName~Journal"`
Expected: compile errors.

- [ ] **Step 3: Implement the journal types** per the format above

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/HiveMind.Core.Tests --filter "FullyQualifiedName~Journal"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/HiveMind.Core/Journal tests/HiveMind.Core.Tests/Journal Directory.Packages.props
git commit -m "feat(audit): hash-chained, encrypted journal segment format"
```

---

### Task 7: Windows interop library (process hardening and DPAPI)

**Files:**
- Create: `src/HiveMind.Interop/HiveMind.Interop.csproj`, `NativeMethods.txt`, `NativeMethods.json`, `Properties/AssemblyInfo.cs`, `ProcessHardening.cs`, `Crypto/DpapiKeyProtector.cs`
- Test: `tests/HiveMind.Interop.Tests/HiveMind.Interop.Tests.csproj`, `ProcessWideCollection.cs`, `ProcessHardeningTests.cs`, `DpapiKeyProtectorTests.cs`

**Interfaces:**
- Consumes: `IKeyProtector` (Task 4).
- Produces:
  - `public static class ProcessHardening { public static bool Apply(); }` calls
    `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_APPLICATION_DIR)`.
    It returns true on success and is idempotent.
  - `public sealed class DpapiKeyProtector : IKeyProtector`, constructed with
    `DpapiKeyProtector(DataProtectionScope scope)`:
    - `Id` is `dpapi-user` for CurrentUser and `dpapi-machine` for LocalMachine
    - optional entropy is the UTF-8 of `HiveMind master key v1`

**Project settings:**
- targets `net10.0` with `[assembly: SupportedOSPlatform("windows10.0.17763")]`
- references `HiveMind.Core`
- packages: `Microsoft.Windows.CsWin32` (`PrivateAssets=all`) and
  `System.Security.Cryptography.ProtectedData`
- `NativeMethods.txt` lists `SetDefaultDllDirectories`

The test project references `HiveMind.Interop` and `HiveMind.Testing`. It declares
`LoadLibraryW` itself with `[LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]`.
Test projects are exempt from HM0001.

`HiveMind.Interop.Tests`, `HiveMind.TestKit`, `HiveMind.App.UITests` and `HiveMind.Benchmarks` each
declare `[assembly: SupportedOSPlatform("windows10.0.17763")]`. Without it, CA1416 (an error
under warnings-as-errors) rejects their calls into Windows-only code. The runtime
`Assert.SkipUnless` checks still skip the tests on other platforms.

- [ ] **Step 1: Write the failing tests**

```csharp
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessWideCollection { public const string Name = "ProcessWide"; }

[Collection(ProcessWideCollection.Name)]
public sealed partial class ProcessHardeningTests
{
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibraryW(string name);

    [Fact]
    public void Apply_ReturnsTrue_AndIsIdempotent()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        Assert.True(ProcessHardening.Apply());
        Assert.True(ProcessHardening.Apply());
    }

    [Fact]
    public void AfterApply_DllInCurrentDirectory_IsNotLoadedByName()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        using var dir = new TempDirectory();
        File.Copy(typeof(BuildInfo).Assembly.Location, Path.Combine(dir.Path, "hm-plant.dll"));
        var previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir.Path;
            Assert.True(ProcessHardening.Apply());
            Assert.Equal(nint.Zero, LoadLibraryW("hm-plant.dll"));
            Assert.Equal(126, Marshal.GetLastPInvokeError()); // ERROR_MOD_NOT_FOUND
        }
        finally { Environment.CurrentDirectory = previous; }
    }
}
```

`DpapiKeyProtectorTests` (each calls `Assert.SkipUnless(OperatingSystem.IsWindows(), ...)`):
- `CurrentUser_ProtectThenUnprotect_RoundTrips`
- `LocalMachine_ProtectThenUnprotect_RoundTrips`
- `Unprotect_TamperedBlob_ThrowsCryptographicException`
- `Ids_AreDpapiUserAndDpapiMachine`
- `WithMasterKeyStore_CreatesAndReloads`: `MasterKeyStore.LoadOrCreate` with a CurrentUser
  protector round-trips

- [ ] **Step 2: Run the tests to verify they fail**

Run on Windows or CI: `dotnet test tests/HiveMind.Interop.Tests`
Expected: compile errors. On Linux, after implementation, all of these tests are skipped.

- [ ] **Step 3: Implement `ProcessHardening` and `DpapiKeyProtector`**

- [ ] **Step 4: Run the tests to verify they pass**

Run: `git push` and `gh run watch --repo cwgilgalad/hivemind` (or `dotnet test tests/HiveMind.Interop.Tests` on Windows)
Expected: the `windows` job passes these tests, and `linux-tests` is unaffected.

- [ ] **Step 5: Commit**

```bash
git add src/HiveMind.Interop tests/HiveMind.Interop.Tests HiveMind.slnx Directory.Packages.props
git commit -m "feat(interop): DLL search hardening and DPAPI key protector"
```

---

### Task 8: App-hive test kit

**Files:**
- Create: `tests/HiveMind.TestKit/HiveMind.TestKit.csproj`, `NativeMethods.txt`, `TempHive.cs`, `HiveFixtureBuilder.cs`
- Test: `tests/HiveMind.Interop.Tests/Integration/TempHiveTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed class TempHive : IDisposable { public static TempHive Create(string? parentDirectory = null); public string HivePath { get; } public SafeRegistryHandle Root { get; } public RegistryKey OpenRoot(); public void Dispose(); }
public sealed record FixtureShape(int Keys, int Values, string Fingerprint);
public static class HiveFixtureBuilder { public static FixtureShape Populate(RegistryKey root, int keyCount, int valuesPerKey, int seed); }
```

**`TempHive`:**
- `Create` makes a unique folder under `parentDirectory` (or `Path.GetTempPath()`). It calls
  `RegLoadAppKeyW(path, out hKey, KEY_ALL_ACCESS, REG_PROCESS_APPKEY, 0)` on a file that doesn't
  exist yet; Windows creates an empty hive in that case.
- `OpenRoot` returns a writable `RegistryKey.FromHandle(...)` over a duplicate of `Root`. Test
  projects may use `Microsoft.Win32.Registry`.
- `Dispose` closes all handles, then deletes the folder, including `.LOG1`/`.LOG2` files. It retries
  for up to 2 s on `IOException` or `UnauthorizedAccessException`.

**`HiveFixtureBuilder.Populate`:**
- Builds `g{group:D4}\k{index:D6}` keys in groups of 100.
- Each key gets `valuesPerKey` values named `v{n}`, cycling through `REG_SZ`, `REG_DWORD`,
  `REG_QWORD`, `REG_BINARY` and `REG_MULTI_SZ`.
- Every 97th value's string data contains the token `tls`.
- `Keys` counts group keys plus leaf keys, not the root. `Fingerprint` is the uppercase hex SHA-256
  of all key paths and value names, joined with `\n` in creation order.

- [ ] **Step 1: Write the failing tests** (each starts with `Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only")`)

```csharp
public sealed class TempHiveTests
{
    [Fact]
    public void Create_ThenWriteAndReadValue_RoundTrips()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        using var hive = TempHive.Create();
        using (var root = hive.OpenRoot()) using (var key = root.CreateSubKey(@"Software\Contoso")) key.SetValue("Mode", 2, RegistryValueKind.DWord);
        using var reopened = hive.OpenRoot();
        Assert.Equal(2, reopened.OpenSubKey(@"Software\Contoso")!.GetValue("Mode"));
    }

    [Fact]
    public void Dispose_DeletesHiveFolderIncludingLogs()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        string folder;
        using (var hive = TempHive.Create()) { folder = Path.GetDirectoryName(hive.HivePath)!; using var r = hive.OpenRoot(); r.SetValue("x", "y"); }
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void Create_UnderPathWithSpacesAndUnicode_Works()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        using var parent = new TempDirectory();
        var target = Directory.CreateDirectory(Path.Combine(parent.Path, "Hive Fixtures Ü")).FullName;
        using var hive = TempHive.Create(target);
        Assert.StartsWith(target, hive.HivePath);
        using var r = hive.OpenRoot(); r.SetValue("name", "value");
    }

    [Fact]
    public void Populate_SameSeed_ProducesSameShape()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        using var a = TempHive.Create(); using var b = TempHive.Create();
        using var ra = a.OpenRoot(); using var rb = b.OpenRoot();
        var shapeA = HiveFixtureBuilder.Populate(ra, 500, 3, seed: 7);
        Assert.Equal(new FixtureShape(505, 1500, shapeA.Fingerprint), shapeA);
        Assert.Equal(shapeA, HiveFixtureBuilder.Populate(rb, 500, 3, seed: 7));
    }
}
```

Also write `TwoHives_AreIsolated`: a value written in one hive is absent from the other.

- [ ] **Step 2: Run the tests to verify they fail**

Run on Windows or CI: `dotnet test tests/HiveMind.Interop.Tests --filter "FullyQualifiedName~TempHive"`
Expected: compile errors.

- [ ] **Step 3: Implement `TempHive` and `HiveFixtureBuilder`**

`TestKit` targets `net10.0` and references CsWin32, with `RegLoadAppKey` and
`REG_PROCESS_APPKEY` in `NativeMethods.txt`. `HiveMind.Interop.Tests` references `TestKit`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: push and `gh run watch --repo cwgilgalad/hivemind`
Expected: the five `TempHive` tests pass on `windows`.

- [ ] **Step 5: Commit**

```bash
git add tests/HiveMind.TestKit tests/HiveMind.Interop.Tests HiveMind.slnx
git commit -m "test: throwaway app-hive fixtures for integration tests"
```

---

### Task 9: Empty WinUI shell and broker skeleton

**Files:**
- Create: `src/HiveMind.App/HiveMind.App.csproj`, `app.manifest`, `Program.cs`, `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`
- Create: `src/HiveMind.Broker/HiveMind.Broker.csproj`, `app.manifest`, `Program.cs`, `BrokerExitCodes.cs`
- Create: `eng/Publish-HiveMind.ps1`
- Test: `tests/HiveMind.Policy.Tests/ManifestPolicyTests.cs`, `tests/HiveMind.App.UITests/HiveMind.App.UITests.csproj`, `ShellSmokeTests.cs`, `BrokerSmokeTests.cs`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `ProcessHardening.Apply()` (Task 7) and `BuildInfo.InformationalVersion` (Task 1).
- Produces:
  - **The app:** `HiveMind.exe`. The window title is exactly `HiveMind`. The title bar shows
    `HiveMind {BuildInfo.InformationalVersion}`, with a `MicaBackdrop` and
    `ExtendsContentIntoTitleBar = true`.
  - **The broker:** `HiveMind.Broker.exe`, with
    `public enum BrokerExitCodes { Ok = 0, UsageError = 2, HardeningFailed = 3 }`. Passing
    `--self-test` returns `Ok`. Any other arguments return `UsageError`.
  - **`eng/Publish-HiveMind.ps1`** takes
    `-RuntimeIdentifier win-x64|win-arm64 -OutputDirectory <dir> [-Configuration Release]` and
    publishes both programs, self-contained, into the same folder.

**App project:**
- `OutputType=WinExe`, `AssemblyName=HiveMind`
- `TargetFramework=net10.0-windows10.0.26100.0`, `TargetPlatformMinVersion=10.0.17763.0`
- `UseWinUI=true`, `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `SelfContained=true`
- `RuntimeIdentifiers=win-x64;win-arm64`, `Platforms=x64;ARM64`
- `Platform` defaults to `x64` when empty or `AnyCPU`, so a plain `dotnet build HiveMind.slnx`
  works on Windows
- `DefineConstants` includes `DISABLE_XAML_GENERATED_MAIN`
- `PublishReadyToRun=true`
- `app.manifest`: `asInvoker`, DPI awareness `PerMonitorV2`

**Broker project:**
- `OutputType=WinExe`, `TargetFramework=net10.0-windows`
- the same runtime identifiers, and `SelfContained=true`
- `app.manifest`: `requireAdministrator`, with `uiAccess="false"`

**Both programs:** the first statement of `Main` is the `ProcessHardening.Apply()` call.

- [ ] **Step 1: Write the failing policy tests** (cross-platform; uses Roslyn syntax parsing, so add `Microsoft.CodeAnalysis.CSharp` to Policy.Tests)

```csharp
public sealed class ManifestPolicyTests
{
    private static string Level(string manifest) =>
        XDocument.Load(RepoFiles.Path(manifest)).Descendants().Single(e => e.Name.LocalName == "requestedExecutionLevel").Attribute("level")!.Value;

    [Fact] public void BrokerManifest_RequestsRequireAdministrator() => Assert.Equal("requireAdministrator", Level("src/HiveMind.Broker/app.manifest"));
    [Fact] public void AppManifest_RequestsAsInvoker() => Assert.Equal("asInvoker", Level("src/HiveMind.App/app.manifest"));

    [Theory]
    [InlineData("src/HiveMind.App/Program.cs")]
    [InlineData("src/HiveMind.Broker/Program.cs")]
    public void Main_CallsProcessHardeningFirst(string path)
    {
        var main = CSharpSyntaxTree.ParseText(File.ReadAllText(RepoFiles.Path(path))).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Main");
        Assert.Contains("ProcessHardening.Apply()", main.Body!.Statements.First().ToString());
    }
}
```

- [ ] **Step 2: Write the failing UI smoke tests** in `tests/HiveMind.App.UITests` (FlaUI.UIA3)

- **`Shell_Launches_ShowsWindowTitledHiveMind_AndClosesCleanly`:**
  - skip unless on Windows with `HIVEMIND_PUBLISH_DIR` set
  - launch `HiveMind.exe` and get the main window within 30 s
  - assert `Title == "HiveMind"`
  - close the window, and assert the process exits within 10 s with exit code 0
- **`Broker_SelfTest_ExitsZero`:**
  - skip unless the process is elevated
    (`new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)`)
  - run `HiveMind.Broker.exe --self-test` and assert exit code 0 within 10 s

- [ ] **Step 3: Run the policy tests to verify they fail**

Run: `dotnet test tests/HiveMind.Policy.Tests --filter "FullyQualifiedName~Manifest"`
Expected: `FileNotFoundException` for the manifests.

- [ ] **Step 4: Implement both projects and `eng/Publish-HiveMind.ps1`**

`Program.Main` in the app follows the standard WinUI custom-`Main` pattern:
1. `ProcessHardening.Apply();`
2. `WinRT.ComWrappersSupport.InitializeComWrappers();`
3. `Application.Start(...)`, setting a `DispatcherQueueSynchronizationContext`

- [ ] **Step 5: Extend CI**

In the `windows` job, after the tests:
1. run `eng/Publish-HiveMind.ps1` for `win-x64` into `artifacts/publish/win-x64`, and for
   `win-arm64` into `artifacts/publish/win-arm64`
2. set `HIVEMIND_PUBLISH_DIR=${{ github.workspace }}/artifacts/publish/win-x64`
3. run `dotnet test tests/HiveMind.App.UITests -c Release`

Exclude `HiveMind.App.UITests` from the earlier solution-wide test step, with a
`--filter "FullyQualifiedName!~UITests"` filter.

- [ ] **Step 6: Run everything to verify it passes**

Run: `dotnet test tests/HiveMind.Policy.Tests` locally, then push and `gh run watch --repo cwgilgalad/hivemind`
Expected: the manifest policy tests pass, and on CI both publishes succeed and both UI smoke tests
pass. GitHub's Windows runners are elevated.

- [ ] **Step 7: Commit**

```bash
git add src/HiveMind.App src/HiveMind.Broker eng/Publish-HiveMind.ps1 tests/HiveMind.Policy.Tests tests/HiveMind.App.UITests .github HiveMind.slnx Directory.Packages.props
git commit -m "feat: empty WinUI shell and elevated broker skeleton with hardening"
```

---

### Task 10: Dev signing and release catalog

**Files:**
- Create: `eng/signing/Find-WindowsSdkTool.ps1`, `New-DevCertificate.ps1`, `Invoke-DevSign.ps1`, `New-ReleaseCatalog.ps1`
- Modify: `.github/workflows/ci.yml`, `docs/development.md`, `eng/Setup-DevMachine.ps1` (use `Find-WindowsSdkTool.ps1`)

**Interfaces:**
- Produces these script contracts, which later tasks and the release workflow call:
  - **`Find-WindowsSdkTool.ps1 -Name <signtool.exe|makecat.exe>`** returns the full path of the
    newest x64 copy under `${env:ProgramFiles(x86)}\Windows Kits\10\bin\<version>\x64\`, and throws
    if there's none.
  - **`New-DevCertificate.ps1 [-Subject "CN=HiveMind Dev Signing"] -Scope CurrentUser|LocalMachine`:**
    - reuses an unexpired certificate with that subject in `Cert:\<Scope>\My`, or creates one with
      `New-SelfSignedCertificate -Type CodeSigningCert -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -NotAfter (Get-Date).AddYears(1)`
    - makes sure the public certificate is in `<Scope>\Root` and `<Scope>\TrustedPublisher`
    - writes the thumbprint to the pipeline
  - **`Invoke-DevSign.ps1 -Directory <dir> -Thumbprint <tp>`:**
    - signs only first-party PE files (`HiveMind.exe`, `HiveMind.*.exe`, `HiveMind.dll`,
      `HiveMind.*.dll`)
    - uses `signtool sign /fd SHA256 /sha1 <tp>` with no timestamp server (dev certificates only)
    - fails on any signtool error
  - **`New-ReleaseCatalog.ps1 -Directory <dir> [-CatalogName HiveMind.cat]`:**
    - writes a CDF with `CatalogVersion=2` and `HashAlgorithms=SHA256`
    - lists every file under the directory except the catalog
    - runs `makecat` and returns the catalog path
    - always runs after binaries are signed

- [ ] **Step 1: Write the four scripts** per the contracts above

They must run on Windows PowerShell 5.1 and PowerShell 7.

- [ ] **Step 2: Add CI steps after publish** (x64 and ARM64 folders)

`New-DevCertificate.ps1 -Scope LocalMachine` (CI runs elevated, so trusting a root shows no
prompt) → `Invoke-DevSign.ps1` → `New-ReleaseCatalog.ps1` → sign the catalog with
`signtool sign /fd SHA256 /sha1 <tp> HiveMind.cat` → verification:
- `signtool verify /pa` on every first-party file
- `signtool verify /pa /c HiveMind.cat <file>` on every first-party file
- `(Get-AuthenticodeSignature HiveMind.cat).Status -eq 'Valid'`

- [ ] **Step 3: Push and verify**

Run: `git push` and `gh run watch --repo cwgilgalad/hivemind`
Expected: every verification exits 0 for both architectures, and the catalog status is `Valid`.

- [ ] **Step 4: Document local dev signing** in `docs/development.md`

On a dev machine:
1. `pwsh eng/signing/New-DevCertificate.ps1 -Scope CurrentUser` (Windows asks once to trust the
   root)
2. then publish, sign and build the catalog with the same three scripts

- [ ] **Step 5: Commit**

```bash
git add eng/signing eng/Setup-DevMachine.ps1 .github docs/development.md
git commit -m "build: dev-certificate signing and signed release catalog"
```

---

### Task 11: Peer verification

**Files:**
- Create: `src/HiveMind.Interop/Trust/PeerTrustPolicy.cs`, `PeerVerifier.cs`, `PeerVerificationResult.cs`
- Modify: `src/HiveMind.Interop/NativeMethods.txt`, `.github/workflows/ci.yml`
- Create: `tests/Fixtures/TrustProbe/TrustProbe.csproj`, `tests/Fixtures/TrustProbe/Program.cs`, `eng/signing/New-TrustFixtures.ps1`
- Test: `tests/HiveMind.Interop.Tests/Trust/PeerVerifierTests.cs`

**Interfaces:**
- Consumes: Task 10's scripts.
- Produces:

```csharp
public sealed record PeerTrustPolicy(string InstallDirectory, string CatalogPath, string ExpectedSignerCommonName)
{
    public const string ReleaseSigner = "SignPath Foundation";
    public const string DevSigner = "HiveMind Dev Signing";
    // Catalog = <installDirectory>\HiveMind.cat; signer = DevSigner under #if DEBUG, otherwise ReleaseSigner.
    public static PeerTrustPolicy ForInstallDirectory(string installDirectory);
}
public enum PeerTrustFailure { None, NotInInstallDirectory, SignatureInvalid, UnexpectedPublisher, CatalogInvalid, NotInCatalog }
public sealed record PeerVerificationResult(PeerTrustFailure Failure, string? Detail) { public bool IsTrusted => Failure == PeerTrustFailure.None; }
public static class PeerVerifier { public static PeerVerificationResult Verify(string peerImagePath, PeerTrustPolicy policy); }
```

**Check order** (the first failure wins):
1. **Folder:** open the peer and the install folder and resolve both with
   `GetFinalPathNameByHandle`. The peer's parent folder must equal the install folder,
   case-insensitive, with no subfolders. Otherwise: `NotInInstallDirectory`.
2. **Signature:** `WinVerifyTrust` with `WINTRUST_ACTION_GENERIC_VERIFY_V2`, the embedded file
   signature, `WTD_REVOKE_NONE` and `WTD_UI_NONE`. Otherwise: `SignatureInvalid`.
3. **Publisher:** the signer's common name (from the WinVerifyTrust provider data, read with
   `CertGetNameString` and the common-name OID) must equal `ExpectedSignerCommonName` (ordinal
   comparison). Otherwise: `UnexpectedPublisher`.
4. **Catalog signature:** the catalog file must verify the same way and have the same signer CN.
   Otherwise: `CatalogInvalid`.
5. **Catalog membership:** compute the file's catalog hash with `CryptCATAdminAcquireContext2`
   (SHA256) and `CryptCATAdminCalcHashFromFileHandle2`. Verify it with `WinVerifyTrust` using
   `WTD_CHOICE_CATALOG`, the catalog path, and the uppercase-hex hash as the member tag. Otherwise:
   `NotInCatalog`.

Revocation isn't checked, because servers are often offline. Say so in the XML docs.

**Fixtures:** `New-TrustFixtures.ps1 -OutputDirectory <root> -ProbeProject tests/Fixtures/TrustProbe`
publishes TrustProbe framework-dependent for `win-x64`. It uses certificate A (`CN=HiveMind Dev
Signing`) and certificate B (`CN=Other Publisher`), both in LocalMachine, and creates:
- `<root>\Install Dir Ü\TrustProbe.exe` and `TrustProbe.dll`, signed with A, plus `HiveMind.cat`,
  built and then signed with A
- added after the catalog, in the same folder:
  - `Late.exe` (published with `-p:AssemblyName=Late`, signed with A)
  - `Unsigned.exe` (`-p:AssemblyName=Unsigned`, unsigned)
  - `OtherPublisher.exe` (`-p:AssemblyName=OtherPublisher`, signed with B)
- `<root>\Outside\TrustProbe.exe`, a byte-identical copy of the signed probe
- `<root>\Install Dir Ü\Linked`, a directory junction to `<root>\Outside`
- `<root>\Tampered\TrustProbe.exe`, plus a copy of `HiveMind.cat` with one byte flipped 64 bytes
  before the end

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class PeerVerifierTests
{
    private static string? Root => Environment.GetEnvironmentVariable("HIVEMIND_TRUST_FIXTURES");
    private static void RequireFixtures() => Assert.SkipUnless(OperatingSystem.IsWindows() && Root is not null, "Run eng/signing/New-TrustFixtures.ps1 and set HIVEMIND_TRUST_FIXTURES");
    private static PeerTrustPolicy Policy(string folder = "Install Dir Ü") =>
        new(Path.Combine(Root!, folder), Path.Combine(Root!, folder, "HiveMind.cat"), "HiveMind Dev Signing");
    private static PeerTrustFailure Check(string relative, string folder = "Install Dir Ü") =>
        PeerVerifier.Verify(Path.Combine(Root!, relative), Policy(folder)).Failure;

    [Fact] public void TrustedPeer_IsTrusted() { RequireFixtures(); Assert.Equal(PeerTrustFailure.None, Check(@"Install Dir Ü\TrustProbe.exe")); }
    [Fact] public void PeerOutsideInstallDirectory_FailsNotInInstallDirectory() { RequireFixtures(); Assert.Equal(PeerTrustFailure.NotInInstallDirectory, Check(@"Outside\TrustProbe.exe")); }
    [Fact] public void PeerReachedThroughJunction_FailsNotInInstallDirectory() { RequireFixtures(); Assert.Equal(PeerTrustFailure.NotInInstallDirectory, Check(@"Install Dir Ü\Linked\TrustProbe.exe")); }
    [Fact] public void UnsignedPeer_FailsSignatureInvalid() { RequireFixtures(); Assert.Equal(PeerTrustFailure.SignatureInvalid, Check(@"Install Dir Ü\Unsigned.exe")); }
    [Fact] public void OtherPublisher_FailsUnexpectedPublisher() { RequireFixtures(); Assert.Equal(PeerTrustFailure.UnexpectedPublisher, Check(@"Install Dir Ü\OtherPublisher.exe")); }
    [Fact] public void SignedButNotInCatalog_FailsNotInCatalog() { RequireFixtures(); Assert.Equal(PeerTrustFailure.NotInCatalog, Check(@"Install Dir Ü\Late.exe")); }
    [Fact] public void TamperedCatalog_FailsCatalogInvalid() { RequireFixtures(); Assert.Equal(PeerTrustFailure.CatalogInvalid, Check(@"Tampered\TrustProbe.exe", "Tampered")); }

    [Fact]
    public void ForInstallDirectory_UsesHiveMindCatalogAndBuildSpecificSigner()
    {
        var installDirectory = Path.Combine(Path.GetTempPath(), "HiveMind Install");
        var policy = PeerTrustPolicy.ForInstallDirectory(installDirectory);
        Assert.Equal(Path.Combine(installDirectory, "HiveMind.cat"), policy.CatalogPath);
#if DEBUG
        Assert.Equal(PeerTrustPolicy.DevSigner, policy.ExpectedSignerCommonName);
#else
        Assert.Equal(PeerTrustPolicy.ReleaseSigner, policy.ExpectedSignerCommonName);
#endif
    }
}
```

`ForInstallDirectory` provides spec §10's "dev-certificate trust in Debug builds". Its test is pure
string logic and doesn't need fixtures.

- [ ] **Step 2: Write `TrustProbe` and `New-TrustFixtures.ps1`**

`TrustProbe`: a `net10.0` console app whose `Main` returns 0. Add it to `HiveMind.slnx`.

- [ ] **Step 3: Run the tests to verify they fail**

Run: push and `gh run watch --repo cwgilgalad/hivemind`. CI gets a step before the tests that runs
`New-TrustFixtures.ps1 -OutputDirectory "${{ runner.temp }}\trust"` and exports
`HIVEMIND_TRUST_FIXTURES`.
Expected: compile errors, because the `PeerVerifier` types don't exist yet.

- [ ] **Step 4: Implement `PeerVerifier`**

Add `WinVerifyTrust`, `WTHelperProvDataFromStateData`, `WTHelperGetProvSignerFromChain`,
`CertGetNameString`, `CryptCATAdminAcquireContext2`, `CryptCATAdminCalcHashFromFileHandle2`,
`CryptCATAdminReleaseContext` and `GetFinalPathNameByHandle` to `NativeMethods.txt`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: push and `gh run watch --repo cwgilgalad/hivemind`
Expected: all 7 `PeerVerifier` tests pass on `windows` (and are skipped on Linux).

- [ ] **Step 6: Commit**

```bash
git add src/HiveMind.Interop tests/Fixtures tests/HiveMind.Interop.Tests eng/signing/New-TrustFixtures.ps1 .github HiveMind.slnx
git commit -m "feat(security): verify peers by folder, signature, publisher and signed catalog"
```

---

### Task 12: License gate, notices and SBOM

**Files:**
- Create: `tools/HiveMind.LicenseGate/HiveMind.LicenseGate.csproj`, `Program.cs`, `AssetsReader.cs`, `NuspecReader.cs`, `SpdxExpression.cs`, `LicensePolicy.cs`, `NoticesWriter.cs`, `ArtifactScanner.cs`
- Test: `tests/HiveMind.LicenseGate.Tests/HiveMind.LicenseGate.Tests.csproj` (references `HiveMind.Testing`), `SpdxExpressionTests.cs`, `LicensePolicyTests.cs`, `ArtifactScannerTests.cs`, `ProgramTests.cs`
- Create: `eng/licenses/allowed.json`, `eng/licenses/overrides.json`, `eng/licenses/forbidden-binaries.txt`
- Test: `tests/HiveMind.Policy.Tests/LicenseFilesPolicyTests.cs`
- Modify: `.config/dotnet-tools.json` (add the `CycloneDX` tool), `.github/workflows/ci.yml`

**Interfaces:**
- Produces:

```csharp
public sealed record PackageRef(string Id, string Version);
public sealed record PackageLicense(PackageRef Package, string? Expression, string? LicenseUrl, string? ProjectUrl);
public sealed record LicenseDecision(PackageRef Package, bool Allowed, string EffectiveLicense, string Reason);
public sealed record ArtifactScanResult(IReadOnlyList<string> ForbiddenFound, IReadOnlyList<string> NativeBinaries);
public static class AssetsReader { public static IReadOnlySet<PackageRef> ReadPackages(string assetsJsonPath); }
public static class NuspecReader { public static PackageLicense Read(PackageRef package, string packagesRoot); }
public static class SpdxExpression { public static bool IsSatisfiedBy(string expression, IReadOnlySet<string> allowed); }
public sealed class LicensePolicy { public static LicensePolicy Load(string allowedJsonPath, string overridesJsonPath); public LicenseDecision Evaluate(PackageLicense license); }
public static class NoticesWriter { public static string Render(IEnumerable<LicenseDecision> decisions, IEnumerable<PackageLicense> licenses); }
public static class ArtifactScanner { public static ArtifactScanResult Scan(string directory, IReadOnlyCollection<string> forbiddenFileNames); }
```

**CLI** (`Program.Main` returns `int`):
- **`check-packages`** with arguments
  `--assets <path>... --packages-root <dir> --allowed <json> --overrides <json> --notices <md>`
  - `--packages-root` defaults to `NUGET_PACKAGES`, otherwise `~/.nuget/packages`
- **`check-artifacts`** with `--dir <dir> --forbidden <txt>`
- **Exit codes:** 0 = OK, 1 = violations (each printed on its own line), 2 = usage error

**Rules:**
- **`AssetsReader`** keeps only `libraries` entries whose `type` is `package`.
- **`NuspecReader`** reads `<root>/<id lowercase>/<version lowercase>/<id lowercase>.nuspec`.
- **`IsSatisfiedBy`:** `OR` passes if any branch passes, `AND` passes if all do, and parentheses
  nest. `A WITH B` passes only if the whole string `A WITH B` is in `allowed`.
- **An override wins over the nuspec.** A package with no expression and no override is a
  violation: `no SPDX license expression; add a reviewed override to eng/licenses/overrides.json`.
- **Denied licenses** produce the reason `denied license <id>`.
- **The notices file** is Markdown: `# Third-party notices`, then the table
  `| Package | Version | License | Project |`, sorted by package id (ordinal, case-insensitive).
- **`ArtifactScanner`** matches forbidden names case-insensitively in any subfolder. It lists PE
  files without CLI metadata (`PEReader.HasMetadata == false`) as native.

**The `eng/licenses` files:**
- `allowed.json`:
  `{"allowed":["MIT","Apache-2.0","BSD-2-Clause","BSD-3-Clause","ISC","Zlib"],"denied":["GPL-2.0-only","GPL-2.0-or-later","GPL-3.0-only","GPL-3.0-or-later","AGPL-3.0-only","AGPL-3.0-or-later","LGPL-2.1-only","LGPL-2.1-or-later","LGPL-3.0-only","LGPL-3.0-or-later","SSPL-1.0","BUSL-1.1","CC-BY-NC-4.0","CC-BY-NC-SA-4.0"]}`
- `overrides.json`: `{"overrides":[]}`. Each entry has `id`, `version` (`*` allowed), `license` and
  `justification`.
- `forbidden-binaries.txt` lists `offreg.dll`, `KernelTraceControl.dll`,
  `KernelTraceControl.Win61.dll` and `msdia140.dll`, one per line.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class SpdxExpressionTests
{
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string> { "MIT", "Apache-2.0", "BSD-3-Clause" };

    [Theory]
    [InlineData("MIT", true)]
    [InlineData("Apache-2.0", true)]
    [InlineData("MIT OR GPL-3.0-only", true)]
    [InlineData("MIT AND GPL-3.0-only", false)]
    [InlineData("(BSD-3-Clause OR MIT) AND Apache-2.0", true)]
    [InlineData("GPL-2.0-only WITH Classpath-exception-2.0", false)]
    [InlineData("LicenseRef-Proprietary", false)]
    public void IsSatisfiedBy_EvaluatesExpressions(string expression, bool expected) =>
        Assert.Equal(expected, SpdxExpression.IsSatisfiedBy(expression, Allowed));
}
```

Also write these tests:
- `LicensePolicyTests`:
  - `Evaluate_AllowedExpression_IsAllowed`
  - `Evaluate_DeniedLicense_ReasonNamesIt` (`denied license GPL-3.0-only`)
  - `Evaluate_NoExpressionNoOverride_IsViolationWithOverrideHint`
  - `Evaluate_Override_AllowsWithJustificationAsReason`
  - `AssetsReader_ReadsOnlyPackageLibraries`: a fixture JSON with one `package` and one `project`
    entry
  - `NuspecReader_ReadsExpressionFromLowercasePath`
  - `Notices_SortedByIdAndIncludeLicense`
- `ArtifactScannerTests`:
  - `Scan_FindsForbiddenBinaryCaseInsensitively` (`runtimes/win-x64/native/MSDIA140.DLL`)
  - `Scan_ManagedAssembly_IsNotListedAsNative` (copy `typeof(object).Assembly.Location`)
  - `Scan_NativeDll_IsListedAsNative` (Windows only; copy `C:\Windows\System32\kernel32.dll`)
- `ProgramTests`:
  - `CheckPackages_Violation_ReturnsOne`
  - `CheckPackages_Clean_ReturnsZeroAndWritesNotices`
  - `UnknownCommand_ReturnsTwo`
- `LicenseFilesPolicyTests`:
  - `AllowedLicenses_MatchSpecExactly` (the six SPDX ids above)
  - `ForbiddenBinaries_ContainProprietaryRuntimeHelpers` (the four names)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/HiveMind.LicenseGate.Tests && dotnet test tests/HiveMind.Policy.Tests --filter "FullyQualifiedName~License"`
Expected: compile errors and missing files.

- [ ] **Step 3: Implement the tool and create the `eng/licenses` files**

- [ ] **Step 4: Run the tests to verify they pass**

Run: the same commands as Step 2
Expected: all pass. The Windows-only native test is skipped on Linux.

- [ ] **Step 5: Wire it into CI**

**`linux-tests` job:** add `tests/HiveMind.LicenseGate.Tests`.

**`windows` job, after publish:**
1. Run `check-packages`:
   - with `--assets` for `src/HiveMind.App`, `src/HiveMind.Broker`, `src/HiveMind.Core` and
     `src/HiveMind.Interop` (each `obj/project.assets.json`)
   - writing `artifacts/THIRD-PARTY-NOTICES.md`
2. Run `check-artifacts` on both publish folders.
3. Run `dotnet tool run dotnet-CycloneDX` (check the exact command with `dotnet tool run --help`
   after adding the tool) for `src/HiveMind.App/HiveMind.App.csproj` and
   `src/HiveMind.Broker/HiveMind.Broker.csproj`, with JSON output, into `artifacts/sbom/`.
4. Upload the notices and SBOM as artifacts.

If the first CI run flags Microsoft runtime packages that publish license URLs instead of SPDX
expressions, add override entries that say what each license is and why it's acceptable. List
every override for owner review.

- [ ] **Step 6: Push and verify**

Run: `git push` and `gh run watch --repo cwgilgalad/hivemind`
Expected: both gates exit 0, and the notices and SBOM artifacts are present.

- [ ] **Step 7: Commit**

```bash
git add tools tests/HiveMind.LicenseGate.Tests tests/HiveMind.Policy.Tests eng/licenses .config .github HiveMind.slnx
git commit -m "build: license gate, third-party notices, forbidden-binary scan and SBOM"
```

---

### Task 13: Release workflow (SignPath-ready, with a dry run)

**Files:**
- Create: `.github/workflows/release.yml`, `docs/release/release-process.md`

**Interfaces:**
- Consumes: `eng/Publish-HiveMind.ps1`, the `eng/signing/*` scripts, and the license gate commands.
- Produces: a workflow that can publish a signed draft release, and refuses to publish anything
  unsigned.

- [ ] **Step 1: Write `.github/workflows/release.yml`**

**Triggers and permissions:**
- **Triggers:**
  - `push` of tags `v*`
  - `workflow_dispatch` with input `dry_run` (boolean, default `true`)
  - `push` to `phase-*/**` and `pull_request`, both filtered to `paths: [.github/workflows/release.yml, eng/signing/**, eng/release/**, eng/Publish-HiveMind.ps1]`
- **Dry run is the default.** The workflow-level
  `env: DRY_RUN: ${{ !(startsWith(github.ref, 'refs/tags/v') || (github.event_name == 'workflow_dispatch' && inputs.dry_run == false)) }}`
  makes every branch and PR run a dry run. GitHub only offers `workflow_dispatch` once the workflow
  is on the default branch.
- Permissions are set per job: `build` gets `contents: read`. `sign` gets `contents: read` and
  `actions: read`. `publish` gets `contents: write`, `id-token: write` and `attestations: write`.

**Job `build`** (`windows-latest`):
1. restore with `--locked-mode`, then test
2. publish `win-x64` and `win-arm64`
3. run the license gate and generate the SBOM
4. upload each publish folder as artifact `unsigned-<rid>`

**Job `sign`** (needs `build`):
- **When `DRY_RUN` is true:** sign through the dev-certificate path (Task 10) and upload
  `signed-<rid>` artifacts.
- **Otherwise:**
  1. A guard step runs `eng/release/Assert-SignPathConfigured.ps1 -OrganizationId "${{ vars.SIGNPATH_ORGANIZATION_ID }}"`.
     The script exits 1 with exactly `SignPath is not configured; refusing to publish unsigned
     binaries.` when the value is empty, and exits 0 otherwise.
  2. Then, per architecture, submit a signing request with
     `signpath/github-action-submit-signing-request` (v3, pinned by commit SHA). Inputs:
     - `api-token: ${{ secrets.SIGNPATH_API_TOKEN }}`
     - `organization-id: ${{ vars.SIGNPATH_ORGANIZATION_ID }}`
     - `project-slug: hivemind`
     - `signing-policy-slug: release-signing`
     - `artifact-configuration-slug: binaries`
     - `github-artifact-id` from the upload step
     - `wait-for-completion: true`
     - an output directory `signed/<rid>`
  3. Run `New-ReleaseCatalog.ps1` on the signed folder.
  4. Submit a second request with `artifact-configuration-slug: catalog` for `HiveMind.cat`.
  5. Verify every first-party PE file and the catalog. `Get-AuthenticodeSignature` must return
     `Valid`, with signer CN `SignPath Foundation`.

**Job `publish`** (needs `sign`; skipped when `DRY_RUN` is true):
1. Build `HiveMind-<version>-<rid>.zip` containing the signed files, `HiveMind.cat`, `LICENSE`,
   `THIRD-PARTY-NOTICES.md` and the SBOM.
2. Write `SHA256SUMS`.
3. Run `actions/attest-build-provenance` (pinned SHA) only when
   `github.event.repository.private == false`.
4. Create a draft release with `gh release create --draft`. The notes include the attribution line
   verbatim.

- [ ] **Step 2: Write `docs/release/release-process.md`**

Cover:
- the dry-run procedure
- the SignPath configuration to create after approval: project `hivemind`, artifact configurations
  `binaries` (first-party PE files only) and `catalog`, signing policy `release-signing`, secret
  `SIGNPATH_API_TOKEN`, variable `SIGNPATH_ORGANIZATION_ID`
- the release checklist: CI green, Defender gate clean (Task 15), notices and SBOM attached,
  attribution line present

- [ ] **Step 3: Write `eng/release/Assert-SignPathConfigured.ps1` and test the guard in CI**

Add a step to `ci.yml`'s `windows` job that runs
`pwsh -File eng/release/Assert-SignPathConfigured.ps1 -OrganizationId ""` and asserts exit code 1
plus the exact message on stderr. Run it again with `-OrganizationId "example"` and assert exit
code 0.

- [ ] **Step 4: Push and verify the dry run**

Run: `git push` (the paths filter starts `release.yml` on the branch) and `gh run watch --repo cwgilgalad/hivemind`
Expected:
- `build` and `sign` succeed, and `publish` is skipped
- `signed-win-x64` and `signed-win-arm64` artifacts exist
- no release is created
- the guard checks in `ci.yml` pass

After this branch merges to `main`, `gh workflow run release.yml -f dry_run=true` works too.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows eng/release docs/release/release-process.md
git commit -m "ci: SignPath-ready release workflow with dry run and unsigned-release guard"
```

---

### Task 14: Benchmark harness and budget baseline

**Files:**
- Create: `benchmarks/HiveMind.Benchmarks/HiveMind.Benchmarks.csproj`, `NativeMethods.txt`, `Program.cs`, `Win32RegistryWalker.cs`, `EnumerationBenchmarks.cs`, `BudgetCheck.cs`
- Create: `docs/benchmarks/phase-0-baseline.md`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `TempHive` and `HiveFixtureBuilder` (Task 8).
- Produces:

```csharp
public sealed record WalkResult(long Keys, long Values, long Inaccessible, TimeSpan Elapsed, TimeSpan? FirstMatch);
public static class Win32RegistryWalker { public static WalkResult Walk(SafeRegistryHandle root, string? matchToken, CancellationToken cancellationToken); }
```

**`Win32RegistryWalker.Walk`:**
- recurses with `RegEnumKeyExW` and `RegEnumValueW`, using CsWin32 (not `RegistryKey`)
- counts keys below the root, excluding the root
- counts keys it can't open in `Inaccessible`, without throwing
- records the first time `matchToken` occurs in a key name, a value name or `REG_SZ` data
  (case-insensitive)

**Commands:**
- **No arguments:** the BenchmarkDotNet switcher, with
  `EnumerationBenchmarks.Walk_Fixture(matchToken: null | "tls")` over a 10,000-key fixture built in
  `[GlobalSetup]`.
- **`self-check`:**
  - builds a `TempHive` populated with `keyCount: 10_000, valuesPerKey: 3, seed: 42`, then walks it
  - exits 0 only if `Keys == 10_100` and `Values == 30_000`
- **`budget [--out <file>]`:**
  - walks the top-level subtrees of `HKLM` (skipping `SAM` and `SECURITY`) and of `HKU` in
    parallel, with degree `Environment.ProcessorCount` and token `tls`
  - writes JSON with these fields:
    - `machine`, `osVersion`, `processorCount`, `keys`, `values`, `inaccessible`
    - `fullScanSeconds`, `firstResultMilliseconds`
    - `budgets` = `{"fullScanSeconds":20,"firstResultMilliseconds":250}`
    - `pass`
  - prints `PASS` or `FAIL`; the result is informational and never fails CI

- [ ] **Step 1: Write the project and the walker**, with `self-check` as the verification gate

- [ ] **Step 2: Run the self-check**

Run on Windows or CI: `dotnet run -c Release --project benchmarks/HiveMind.Benchmarks -- self-check`
Expected: exit code 0.

- [ ] **Step 3: Add CI steps**

In the `windows` job:
- run `self-check`, which gates the build
- run `budget --out artifacts/benchmarks/budget-report.json`, which is informational, and upload
  the report

- [ ] **Step 4: Record the baseline**

Push, watch CI, and copy the CI runner's numbers into `docs/benchmarks/phase-0-baseline.md`. The
table has these columns: environment, keys, values, full scan (s), first result (ms), budget
pass/fail. Leave an empty row labelled `Owner laptop`; the owner fills it in.

- [ ] **Step 5: Commit**

```bash
git add benchmarks docs/benchmarks .github HiveMind.slnx Directory.Packages.props
git commit -m "perf: registry enumeration benchmarks and budget baseline"
```

---

### Task 15: Defender release gate

**Files:**
- Create: `eng/release-gate/Invoke-DefenderGate.ps1`, `eng/release-gate/scenarios/01-launch-shell.ps1`, `eng/release-gate/scenarios/02-broker-self-test.ps1`, `eng/release-gate/test-data/no-detections.json`, `eng/release-gate/test-data/one-detection.json`
- Modify: `docs/release/release-process.md`, `.github/workflows/ci.yml`

**Interfaces:**
- Produces:
  `Invoke-DefenderGate.ps1 -PublishDirectory <dir> [-ScenarioDirectory <dir>] [-DetectionsJsonPath <file>] [-ReportPath <file>]`
  - records a start time
  - runs every scenario script in name order, passing `-PublishDirectory`. With
    `-DetectionsJsonPath`, it skips the scenarios and reads detections from the JSON instead (test
    mode).
  - otherwise collects `Get-MpThreatDetection` entries with `InitialDetectionTime >= start`, plus
    Defender Operational events 1116 and 1117 since the start
  - writes report JSON:
    `{start, end, scenarios:[{name, exitCode}], detections:[{time, threat, resources}], pass}`
  - exits 1 on any detection or any non-zero scenario exit code; otherwise exits 0

**Scenarios:**
- `01-launch-shell.ps1` starts `HiveMind.exe`, waits up to 30 s for its main window, closes it, and
  returns the process exit code.
- `02-broker-self-test.ps1` runs `HiveMind.Broker.exe --self-test` with
  `Start-Process -Verb RunAs -Wait -PassThru` and returns its exit code.

- [ ] **Step 1: Write the script, the scenarios and the test-data files**

`one-detection.json` holds one detection object, threat `Test:Synthetic`, whose resources point at
`HiveMind.exe`.

- [ ] **Step 2: Run test mode to verify the logic**

Run (Windows): `pwsh eng/release-gate/Invoke-DefenderGate.ps1 -PublishDirectory artifacts/publish/win-x64 -DetectionsJsonPath eng/release-gate/test-data/no-detections.json`
Expected: exit 0. With `one-detection.json`: exit 1.

- [ ] **Step 3: Add both test-mode runs to CI**

In the `windows` job, run both test-mode commands and assert their exit codes (0 and 1). The real
gate runs on VMs (Step 4).

- [ ] **Step 4: Document the VM gate** in `docs/release/release-process.md`

Cover:
- **VMs:** Windows 11 and Windows Server 2025, with Defender real-time and cloud-delivered
  protection on, after `Update-MpSignature`, and a clean snapshot.
- **The run:** copy the release-candidate artifacts in, run the gate, and attach the report to the
  release checklist.
- **The rule:** any detection blocks the release (spec §8.2).

- [ ] **Step 5: Push, verify and commit**

```bash
git add eng/release-gate docs/release/release-process.md .github
git commit -m "build: Defender release-gate harness and runbook"
git push
```

Expected: the test-mode checks pass in CI.

---

### Task 16: Open-source governance

**Files:**
- Create: `CONTRIBUTING.md`, `SECURITY.md`, `CODE_SIGNING_POLICY.md`
- Modify: `README.md`, `.github/workflows/ci.yml`
- Test: `tests/HiveMind.Policy.Tests/GovernancePolicyTests.cs`

**Interfaces:**
- Produces: the documents SignPath's application needs, and a DCO check for fork PRs.

**Content requirements**

`CONTRIBUTING.md` covers:
- **DCO:** sign-off with `git commit -s`, required for external contributors.
- **Contribution license:** contributions come in under Apache-2.0 (inbound = outbound). Knowledge
  packs will use CC BY 4.0.
- **Setup:** a link to `docs/development.md`.
- **Standards:** nullable reference types, zero warnings, Windows-only tests that skip elsewhere.
- **The clean-room rule:** never copy from RegKit (AGPL-3.0), FluentRegEdit (GPL-3.0) or
  closed-source tools. MIT/Apache projects may be referenced with attribution.
- **The banned-API and EDR rules,** with a link to spec §8.2 and the PR checklist.

`SECURITY.md` covers:
- **Reporting:** through GitHub private vulnerability reporting ("Report a vulnerability" on the
  Security tab), never in public issues.
- **Response:** acknowledgment within 3 business days.
- **Supported versions:** none released yet; once releases exist, the latest release.
- **Scope:** HiveMind's code, build and release pipeline.

`CODE_SIGNING_POLICY.md` covers:
- **Attribution:** the exact line
  `Free code signing provided by SignPath.io, certificate by SignPath Foundation.`
- **Roles:** "Committers and reviewers: cwgilgalad"; "Approvers: cwgilgalad".
- **Privacy statement,** exactly: `This program will not transfer any information to other
  networked systems unless specifically requested by the user or the person installing or operating
  it.`
- **What's signed:** first-party PE files, MSI/MSIX once they exist, PowerShell modules and scripts,
  and the release catalog.
- **How:** `release.yml` → SignPath, with artifact configurations `binaries` and `catalog` and
  signing policy `release-signing`.
- **How users verify:** `Get-AuthenticodeSignature`, and `signtool verify /pa /c HiveMind.cat <file>`.

`README.md` changes:
- links to the three files and to `docs/development.md`
- a link to `CODE_SIGNING_POLICY.md` in the Code signing section

DCO job in `ci.yml`:
- runs only when `github.event.pull_request.head.repo.fork == true`, with `fetch-depth: 0`
- fails if any commit in `base..head` lacks a line starting with `Signed-off-by: `

- [ ] **Step 1: Write the failing policy tests**

```csharp
public sealed class GovernancePolicyTests
{
    [Fact] public void CodeSigningPolicy_ContainsExactAttributionLine() =>
        Assert.Contains("Free code signing provided by SignPath.io, certificate by SignPath Foundation.", File.ReadAllText(RepoFiles.Path("CODE_SIGNING_POLICY.md")));

    [Fact] public void CodeSigningPolicy_ContainsPrivacyStatement() =>
        Assert.Contains("will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it", File.ReadAllText(RepoFiles.Path("CODE_SIGNING_POLICY.md")));

    [Theory]
    [InlineData("CONTRIBUTING.md")] [InlineData("SECURITY.md")] [InlineData("CODE_SIGNING_POLICY.md")] [InlineData("docs/development.md")]
    public void Readme_LinksGovernanceDocument(string target) => Assert.Contains($"]({target})", File.ReadAllText(RepoFiles.Path("README.md")));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/HiveMind.Policy.Tests --filter "FullyQualifiedName~Governance"`
Expected: the files are missing, or the links aren't there.

- [ ] **Step 3: Write the documents, update `README.md`, and add the DCO job**

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/HiveMind.Policy.Tests --filter "FullyQualifiedName~Governance"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add CONTRIBUTING.md SECURITY.md CODE_SIGNING_POLICY.md README.md tests/HiveMind.Policy.Tests .github
git commit -m "docs: contributing, security and code-signing policies; DCO check for forks"
```

---

### Task 17: STRIDE threat model

**Files:**
- Create: `docs/security/threat-model.md`
- Test: `tests/HiveMind.Policy.Tests/ThreatModelPolicyTests.cs`

**Interfaces:**
- Produces: the threat model that later phases update whenever they add a component.

**Required content**
- **Sections:** `## Scope`, `## Assets`, `## Trust boundaries`, `## Data flow`,
  `## Threats by element`, `## Abuse cases`, `## Open risks`.
- **Elements** (each one gets a table row for every STRIDE category, with the threat, mitigation,
  spec section, phase and verification):
  - User
  - `HiveMind.App`
  - the UI↔broker named pipe
  - `HiveMind.Broker`
  - the kernel registry
  - the per-user data store (master key, journal, index, snapshots, captures, notes, drafts)
  - the machine audit store and Event Log
  - hive working copies
  - knowledge packs
  - plugins
  - the remote worker with WinRM/SSH
  - the build and release pipeline (GitHub Actions, SignPath)
  - distribution channels
- **Abuse cases:**
  - a malicious `.reg` file, a malicious pack, a malicious `hivemind://` link
  - malware launching the broker
  - a rogue plugin
  - a compromised dependency, a compromised CI run
  - a stolen journal file
- **Data flow:** an ASCII diagram of §7.1's processes, with the trust boundaries marked.

- [ ] **Step 1: Write the failing policy test**

```csharp
public sealed class ThreatModelPolicyTests
{
    [Theory]
    [InlineData("## Scope")] [InlineData("## Assets")] [InlineData("## Trust boundaries")] [InlineData("## Data flow")]
    [InlineData("## Threats by element")] [InlineData("## Abuse cases")] [InlineData("## Open risks")]
    public void ThreatModel_HasRequiredSection(string heading) =>
        Assert.Contains(heading, File.ReadAllLines(RepoFiles.Path("docs/security/threat-model.md")));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/HiveMind.Policy.Tests --filter "FullyQualifiedName~ThreatModel"`
Expected: FAIL (file not found).

- [ ] **Step 3: Write `docs/security/threat-model.md`** per the required content

- [ ] **Step 4: Run the test to verify it passes, then self-review**

Run: the same command
Expected: PASS. Then check the review criteria by reading:
- every element has six STRIDE rows; "not applicable" is allowed, with a reason
- every mitigation cites a spec section
- every Phase 0 mitigation names its test or CI check

- [ ] **Step 5: Commit**

```bash
git add docs/security/threat-model.md tests/HiveMind.Policy.Tests
git commit -m "docs(security): STRIDE threat model"
```

---

### Task 18: UX mockups and usability test plan

**Files:**
- Create: `design/mockups/index.html`, `search.html`, `pending.html`, `styles.css`, `app.js`, `screenshot.cjs`, `README.md`
- Create: `design/mockups/screenshots/` (generated PNGs)
- Create: `docs/ux/usability-test-plan.md`

**Interfaces:**
- Produces: clickable mockups for the owner's approval (part of the Phase 0 exit) and the script
  for the usability sessions.

**Mockup requirements**
- **Self-contained:** static files with no external requests (no CDNs). Fonts are
  `"Segoe UI Variable", "Segoe UI", system-ui, sans-serif`.
- **Themes:** light and dark, following `prefers-color-scheme`, plus a toggle. The toggle's choice
  is kept in `localStorage` inside `try/catch`.
- **Screens:**
  - **`index.html` (spec §6.2):** the tree, and the `W32Time` value list, including the
    `DelayedAutostart` ghost row. The Inspector shows the `Start` value's allowed values, the risk
    badge (icon plus text), "takes effect after a service restart" and the source chip. The status
    bar shows "Standard user | Index: ready | Rec: off".
  - **`search.html` (§6.3):** filter chips, a SETTINGS section for meaning results, and a KEYS AND
    VALUES section with a streaming counter and a Stop button.
  - **`pending.html` (§6.5):** three diff rows with risk levels, the note/ticket field, and "Apply
    (admin)" with a shield icon.
- **Navigation:** Ctrl+K and the search box open `search.html`. The pending tray opens
  `pending.html`. Apply shows a toast saying "Applied 3 changes · Undo".
- **Accessibility:** every control is reachable by keyboard with a visible focus ring, icon-only
  buttons have `aria-label`, and nothing is conveyed by color alone.

**Usability test plan contents**
- **Participants:** 3 to 5 Windows admins.
- **Five tasks:**
  1. Find where inbound TLS 1.0 is controlled, by meaning.
  2. Explain what `Start = 3` means for a service, using the Inspector.
  3. Stage and apply a change with a ticket note.
  4. Revert yesterday's change from History.
  5. Interpret an access-denied explanation.
- **Success criteria (spec §2):** at least 90% task success and a SUS score of 80 or more.
- **Materials:** a facilitator script, an observation sheet, and the 10-item System Usability Scale
  questionnaire on a 5-point scale (credit: SUS by John Brooke, 1986).
- **Results go in** `docs/ux/usability-results.md`, which the owner writes.

- [ ] **Step 1: Write the mockup files** per the requirements

- [ ] **Step 2: Write `screenshot.cjs`**

It's CommonJS, so `require('playwright')` resolves the preinstalled copy. It uses
`chromium.launch()`, viewport 1440×900 and `deviceScaleFactor: 2`. For each page in light and dark
color schemes, it fails if anything is logged to the console as an error, and writes
`screenshots/<page>-<scheme>.png`. The README says the owner can simply open the HTML files;
Playwright is only for generating screenshots.

- [ ] **Step 3: Generate the screenshots to verify**

Run: `node design/mockups/screenshot.cjs`
Expected: 6 PNGs created and no console errors. Inspect each one against §6.2–6.5.

- [ ] **Step 4: Write `docs/ux/usability-test-plan.md`**

- [ ] **Step 5: Commit**

```bash
git add design/mockups docs/ux
git commit -m "design: clickable mockups and usability test plan"
```

---

## Owner actions (not agent tasks)

1. **Accounts:** turn on two-factor authentication for GitHub now, and for SignPath once the account
   exists.
2. **Repository settings:**
   - enable private vulnerability reporting
   - add the Contributor Covenant through **Insights → Community standards**, with a project
     contact address of your choosing
   - protect `main`: require pull requests and the `linux-tests` and `windows` checks
3. **UX:** review the mockups (Task 18) and run 3 to 5 usability sessions. Record task success and
   SUS scores in `docs/ux/usability-results.md`.
4. **Defender gate:** run it on Windows 11 and Windows Server 2025 VMs (Task 15 runbook).
5. **Benchmarks:** run `dotnet run -c Release --project benchmarks/HiveMind.Benchmarks -- budget`
   on your laptop and fill in its row in `docs/benchmarks/phase-0-baseline.md`.
6. **Visibility:** make the repository public.
7. **SignPath:**
   - **Apply** to SignPath Foundation, citing `CODE_SIGNING_POLICY.md` and the release dry run.
   - **Ask** them to confirm that the Windows App SDK and .NET runtimes count as upstream libraries.
   - **After approval:** create project `hivemind`, artifact configurations `binaries` and
     `catalog`, and signing policy `release-signing`. Add secret `SIGNPATH_API_TOKEN` and variable
     `SIGNPATH_ORGANIZATION_ID`.

## Phase 0 exit (spec §10)

- A CI build of the empty shell, signed with the dev certificate (Tasks 9–10).
- The analyzers and gates enforced (Tasks 3, 12, 15).
- A benchmark baseline (Task 14).
- The mockups approved (Task 18 plus owner action 3).
- The SignPath application submitted (owner action 7).
