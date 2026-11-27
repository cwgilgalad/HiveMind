# Phase 0 execution decisions

This file is the record of how the Phase 0 plan
(`docs/superpowers/plans/2026-11-24-phase-0-foundations.md`) is being carried out on the
`phase-0/foundations` branch. It lists every decision taken along the way and what each task
delivered. The spec (`docs/superpowers/specs/2026-11-24-hivemind-design.md`) is the authority and the
plan argues from it. A ruling settles something the plan leaves open, or gets wrong, and says what it
costs if it turns out to be wrong.

The file is updated and committed at every task boundary.

## How the plan is run

- One agent per plan task (R1). It works test-first, checks itself against the plan's Review Focus,
  ticks the plan's checkboxes, commits, pushes and watches CI. Before the next task starts, the
  controller inspects the diff and the test results.
- Task order (R14): 1–11, 14, 16, 17, 18, 12, 13, 15, then wrap-up.
- Before Task 1, every pair of tasks that shares a file or an interface was checked for conflicts.
  What that check found became R3, R7, R9, R13, R14, R15, R16 and R17.

## Rulings

### R1: One agent per task, no reviewer agents

- **Decision:** each plan task gets a single agent. No separate reviewer agents. The controller checks
  every diff and test result before the next task starts.
- **Why:** the owner chose the approach with the fewest agents.
- **Cost if wrong:** defects a separate reviewer would catch surface later, in CI, in the controller's
  check or in the final pass.

### R2: Repository-local commit identity

- **Decision:** commits use the repository-local noreply identity. The global git config is never
  changed, although the handoff's §5.3 said to set it globally.
- **Why:** the repository is public, and the global config serves other repositories.
- **Cost if wrong:** none.

### R3: Commit before pushing

- **Decision:** where a task's steps push before they commit (Tasks 7–14), the agent commits first and
  then pushes. CI fixes land as new commits.
- **Why:** CI can't check work that isn't committed.
- **Cost if wrong:** a few more commits in the history.

### R4: Full local build and test before every push

- **Decision:** `dotnet build HiveMind.slnx` with zero warnings and `dotnet test HiveMind.slnx` both run
  locally before every push.
- **Why:** the handoff's §6.
- **Cost if wrong:** slower tasks on a two-core laptop.

### R5: Nothing elevated, no trust-store changes on the laptop

- **Decision:** agents never run anything elevated and never touch a certificate trust store on the
  laptop. That rules out:
  - `New-DevCertificate` in either scope, and `-InstallDevCertificate`
  - `New-TrustFixtures`
  - the Defender gate outside test mode
  - the broker's RunAs

  Signing and peer verification are proven in CI only.
- **Why:** the handoff's §7 makes all of these ask-first.
- **Cost if wrong:** no local proof that signing works.

### R6: Commit every lock file a task touches

- **Decision:** commits include `Directory.Packages.props`, `HiveMind.slnx` and every
  `packages.lock.json` a task touches, even when the task's file list or `git add` line leaves them
  out.
- **Why:** CI restores with `--locked-mode`.
- **Cost if wrong:** none.

### R7: The arm64 job skips UI tests too

- **Decision:** Task 9's `--filter "FullyQualifiedName!~UITests"` also goes on the test step of the
  `windows-arm64-tests` job.
- **Why:** that job runs the same solution-wide test and has no publish folder.
- **Cost if wrong:** none.

### R8: Task 11's red step stays local

- **Decision:** Task 11's failing-first step, a compile error, is shown locally and never pushed.
- **Why:** a commit that doesn't compile would turn CI red.
- **Cost if wrong:** none.

### R9: Layout of the test-hive fixture

- **Decision:** `HiveFixtureBuilder` lays out keys and values this way:
  - Group keys are `g{group:D4}`, where group is the leaf index divided by 100.
  - Leaf keys are `k{leafIndex:D6}`, using the global leaf index.
  - Value names `v0` to `v{valuesPerKey-1}` start again for each key.
  - Value types cycle on a global zero-based value counter `i`. The cycle (`i % 5`) is REG_SZ,
    REG_DWORD, REG_QWORD, REG_BINARY, REG_MULTI_SZ.
  - Every value whose one-based global number is divisible by 97 is written as REG_SZ with data that
    contains `tls`. Task 14's walker matches REG_SZ data only.
  - The seed drives a `new Random(seed)` that is used for data payloads only, so names, structure and
    the fingerprint don't depend on it.
- **Why:** the plan's text is ambiguous here. This layout satisfies both Task 8's tests and Task 14's
  counts and first match.
- **Cost if wrong:** the fixture differs in detail from what the plan's author pictured.

### R10: The arm64 job runs

- **Decision:** the repository is public, so the `windows-arm64-tests` job runs. No change is needed.
- **Cost if wrong:** none.

### R11: Agents use the session's model

- **Decision:** task agents run on the same model as the session.
- **Why:** there is no reviewer seat to save for, and the code is security-critical.
- **Cost if wrong:** each task costs more.

### R12: Push after every task, and watch CI by run id

- **Decision:** from Task 2 on, every task pushes. The exception is Task 12, which doesn't push before
  its license overrides are approved. CI is watched by run id:
  1. List the runs with `gh run list --repo cwgilgalad/hivemind --branch phase-0/foundations --limit 10 --json databaseId,headSha,workflowName,status,conclusion,url`.
  2. Pick the runs for the pushed commit.
  3. Watch each with `gh run watch <id> --repo cwgilgalad/hivemind --exit-status`.

  A red run is fixed before the task counts as done.
- **Why:**
  - the handoff's §6
  - Actions minutes are free for a public repository
  - a watch that isn't interactive needs a run id
- **Cost if wrong:** more pushes.

### R13: Pin Roslyn to the floor SDK's compiler

- **Decision:** `Microsoft.CodeAnalysis.CSharp`, and every other `Microsoft.CodeAnalysis.*` package
  the repository pins, is pinned to the Roslyn version in SDK 10.0.100. That SDK is the floor
  `global.json` allows. The family is pinned together.
- **Why:**
  - an analyzer built against a newer Roslyn fails to load (CS9057) on the older SDKs that
    `global.json` allows
  - pinning the family together avoids NU1107
- **Cost if wrong:** slightly older Roslyn APIs.

### R14: Task order, with the one planned stop at the end

- **Decision:** the tasks run in the order 1–11, 14, 16, 17, 18, 12, 13, 15.
  1. Task 12 runs up to its license-override list, commits without the overrides, and doesn't push.
  2. The controller asks the owner to approve the exact list.
  3. After the answer, Task 12 is finished (overrides, push, CI), then Tasks 13 and 15.
- **Why:**
  - the handoff's §7 makes license overrides ask-first
  - Task 13's dry run needs Task 12's gate to pass
  - Task 15 extends the `release-process.md` that Task 13 creates
  - this order puts the only planned stop at the end
- **Cost if wrong:** Task 17 cites checks that land later. Their names come from the plan and are
  re-checked at wrap-up.

### R15: Playwright stays in a temporary folder

- **Decision:** Task 18 installs Playwright only inside the session's temporary folder. Both
  `PLAYWRIGHT_BROWSERS_PATH` and `NODE_PATH` point inside it, and nothing is installed globally. If
  the install fails, the screenshots are skipped and the report says so.
- **Why:** the handoff's §8.
- **Cost if wrong:** the screenshots may be missing.

### R16: Benchmark numbers come from CI

- **Decision:** Task 14 commits the harness with the plan's message, then records the CI runner's
  numbers in a follow-up commit. The "Owner laptop" row stays empty.
- **Why:** the handoff's §9 lists the laptop numbers as an owner action.
- **Cost if wrong:** none.

### R17: Shape of the release workflow

- **Decision:**
  - Task 13's `release.yml` puts the `v*` tag trigger and the `phase-*/**` branch trigger in one
    `push:` block. GitHub ignores `paths` for tag pushes.
  - A job-level `if:` can't read `env`, so the `publish` job repeats the DRY_RUN expression or reads
    a job output.
  - The SignPath action is pinned by commit SHA to the newest v3. If v3 doesn't exist, it is pinned to
    the newest major version, and that is noted as a deviation.
- **Why:** YAML can't repeat a key, and `env` isn't in scope for a job's `if`.
- **Cost if wrong:** none.

### R18: The final review is done inline

- **Decision:** the controller does the final whole-branch review, together with green CI, without an
  extra review agent. At wrap-up the owner is offered one.
- **Why:** the rule of using the fewest agents.
- **Cost if wrong:** a subtle defect spanning several tasks could reach the pull request.

### R19: CI turns off tooling telemetry (amended by R20)

- **Decision:** CI sets `DOTNET_CLI_TELEMETRY_OPTOUT=1` and `DOTNET_NOLOGO=1` at workflow level. It
  sets `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` only if R20's fallback is taken.
- **Why:** telemetry from CI tooling contradicts the project's no-telemetry stance.
- **Cost if wrong:** none.

### R20: Tests run through VSTest

- **Decision:** `xunit.v3.mtp-off` 4.0.3 (Apache-2.0) replaces `xunit.v3`, and `global.json` goes back
  to the plan's sdk-only content.
- **Why:**
  - `xunit.v3` 4.x makes a test project a Microsoft.Testing.Platform (MTP) app, and the .NET 10 SDK
    runs those only in MTP mode.
  - MTP mode accepts `--filter`, but it rejects the plan's `--logger trx` with exit code 5.
  - MTP mode adds `Microsoft.Testing.Extensions.Telemetry` and `Microsoft.ApplicationInsights` to the
    test graph, turned on by default for every contributor.
  - The plan's package trio and every test command in it are VSTest.
- **Cost if wrong:** Microsoft's recommended MTP runner is set aside for now. Moving back is a package
  rename plus one `global.json` section.

### R21: Decisions are kept under version control

- **Decision:** the decisions and each task's outcome are recorded in this tracked file, which is
  committed at every task boundary. The git-ignored working ledger under `.superpowers/` keeps only
  operational detail, such as run ids and retries.
- **Why:** on 2026-11-26 the owner asked for all work and every decision to be tracked in version
  control.
- **Cost if wrong:** a little more bookkeeping per task.

### R22: Two gaps in the banned lists are closed in Task 7

- **Decision:** Task 7 adds the following, each with tests:
  - RS0030 entries for the managed library loader: every overload of `NativeLibrary.Load` and
    `NativeLibrary.TryLoad`, plus `NativeLibrary.SetDllImportResolver`.
  - `LogonUserExEx` on HM0001's token list. It's added only if the installed Windows SDK headers
    declare `LogonUserExExW`.
- **Why:**
  - Spec §8.2 limits native calls to the short list in §7.3. Task 3's list bans `GetExport`, but not
    the managed loader. With a DLL-import resolver, the loader can point a reviewed import at
    another DLL.
  - `LogonUserExExW` creates a logon token. The trailing-A/W rule doesn't reach it, because it maps
    the name to `LogonUserExEx`, which isn't on the list.
  - Task 7 hardens DLL loading, so the bans belong with it. No code before Task 7 needs either API.
- **Cost if wrong:** a later task that really needs the managed loader has to add a reviewed
  suppression.

### R23: RS0030 doesn't flag a banned type that appears only in a signature

- **Decision:** no extra HM rule is added for this in Phase 0.
- **Why:**
  - RS0030 reports every member access, construction and `typeof` of a banned type.
  - A parameter, field or return type of type `RegistryKey` gets no diagnostic. But code can't get
    or use a key without a member access that is flagged.
  - Task 3's compile-only probe in a product project showed both behaviours.
- **Cost if wrong:** a declaration that names `RegistryKey` can reach product code without an
  error. Code review is the backstop, and an HM rule can close the gap later.

### R24: A NuGet audit failure is fixed with a patch pin, never with a suppression

- **Decision:** when a transitive package fails NuGet audit, pin it in `Directory.Packages.props`
  under "Security pins". Use the newest patch of the current .NET major, with a comment naming the
  advisories. Never suppress NU1901–NU1904.
- **Why:**
  - Warnings are errors, so an advisory blocks restore.
  - A patch pin keeps the dependency graph on the major the rest of the build uses.
  - Example: Task 3 pinned `System.Security.Cryptography.Pkcs` 10.0.15. It arrives through the
    analyzer-testing packages and was raised past GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx.
- **Cost if wrong:** each pin needs a later look. Dependabot proposes the next major as its own pull
  request.

### R25: A master key file that can't be read is never replaced

- **Decision:**
  - `MasterKeyStore.LoadOrCreate` treats the key file as missing only when opening it throws
    `FileNotFoundException` or `DirectoryNotFoundException`. It never asks `File.Exists`.
  - Every other open, read, format or unprotect failure throws `MasterKeyUnavailableException` and
    leaves the file unchanged.
  - Creating a key writes a uniquely named temp file in the same folder, flushes it to disk, and
    moves it into place with `overwrite: false`. If a key file appeared in the meantime, the temp
    file is deleted and that key is loaded through the normal path.
  - Task 4 adds one test beyond the plan: a key file held open by another handle throws and stays
    byte-identical.
- **Why:** this is the plan's first Review Focus item. `File.Exists` returns false when access is
  denied. With the plan's `File.Move(..., overwrite: true)`, the real key would then be replaced by a
  new one, silently orphaning every encrypted store.
- **Cost if wrong:**
  - Two simultaneous first runs end up sharing one key, which is the intended outcome.
  - Key rotation, in a later phase, needs to overwrite. The move sits in a helper that takes an
    overwrite flag.

### R26: PBKDF2 iteration counts are capped at 10,000,000

- **Decision:**
  - `PassphraseKeyWrap` gains `MaximumIterations = 10_000_000`.
  - `Wrap` throws `ArgumentOutOfRangeException` above the cap.
  - `Unwrap` throws `CryptographicException` before PBKDF2 runs.
  - This lands in Task 5's run, as its own commit.
- **Why:**
  - The stored count is checked only after PBKDF2 has run with it.
  - In a damaged or crafted backup, the count field can read near `int.MaxValue`; one flipped high
    bit is enough. An import of that backup would stay busy for hours before failing.
  - The cap is about 16 times the minimum, which leaves room to raise the work factor later.
- **Cost if wrong:** a later version that needs more iterations raises the constant. Older builds
  then refuse the newer backups, with a message saying why.

### R27: An empty passphrase is refused

- **Decision:**
  - `PassphraseKeyWrap.Wrap`, and so `MasterKeyBackup.Export`, throws `ArgumentException` for an
    empty passphrase.
  - Rules on passphrase strength (length, guessability) belong to the recovery and export screens in
    a later phase.
  - This lands with R26.
- **Why:** with an empty passphrase, anyone can open the backup. The spec says the backup is
  "protected by a recovery passphrase". Refusing an empty one in Core costs one line.
- **Cost if wrong:** none in practice. A caller that really wants an unprotected export would use a
  different format.

### R28: Task 4's error paths get committed tests in Task 5's run

- **Decision:** the first commit of Task 5's run adds tests for:
  - `Unwrap` and `Import` on input that is too short, has the wrong magic or version, or stores an
    iteration count below the minimum or above R26's cap
  - a tampered ciphertext
  - R27's empty passphrase
  - two first runs at once, using a protector that creates the key file from inside `Protect`
- **Why:**
  - Task 4 proved these paths only with a throwaway harness, because the controller had kept the
    wrap tests to the plan's list.
  - Most of these cases fail before PBKDF2 runs, so the tests take milliseconds.
  - Task 5 works in the same folder, so the tests need no extra agent (R1).
- **Cost if wrong:** Task 5's run has two commits instead of one.

### R29: A temp file left behind by a failed cleanup is accepted

- **Decision:** after an error, if deleting the temp file also fails, `masterkey.bin.<guid>.tmp`
  stays behind. Phase 0 adds no sweep for it.
- **Why:** the temp file holds only the protector's output, never the plaintext key, so it is exactly
  as protected as the key file itself. The delete is best effort, so it never hides the original
  error.
- **Cost if wrong:** a stray file sits in the key folder until someone deletes it.

## Task log

### Task 1: Solution skeleton, standards and dev setup (done)

- **Commit:** `eaee326` "build: add solution skeleton, standards and dev setup".
- **Plan:** Steps 1–8 ticked.
- **Deviations from the plan, accepted at inspection:**
  - CA1707 is turned off for `tests/**` only, because test names use underscores.
  - A PascalCase naming rule applies to private constants.
  - `Setup-DevMachine.ps1` has two parameter sets, and it searches the Windows Kits folders for the
    newest version folder that actually contains `signtool.exe`.
  - The test file uses a file-scoped namespace.
  - The test projects set `OutputType` Exe, a global `using Xunit` and `IsTestProject=false` where
    needed.
- **Not accepted:** switching `dotnet test` to Microsoft.Testing.Platform in `global.json`. That
  change is undone under R20.
- **Interruptions:** the first attempt was cut off by a usage limit, and a second attempt finished the
  task.

### Task 2: CI on Windows and Linux (done)

- **Commits:**
  - `0483371` "build: run xUnit v3 through VSTest (xunit.v3.mtp-off)" (R20)
  - `912c797` "ci: build and test on Windows and Linux"
- **Plan:** Steps 1–3 ticked.
- **CI:** [run 45919213485](https://github.com/cwgilgalad/HiveMind/actions/runs/45919213485).
  `linux-tests`, `windows` and `windows-arm64-tests` were all green on the first push, 2 tests each.
- **SDK in CI:** setup-dotnet with `global-json-file` installed SDK 10.0.404 on all three runners.
  R13 still pins Roslyn to the floor SDK 10.0.100, because contributors may build with any SDK that
  `global.json` allows.
- **Test platform:** after R20, no `Microsoft.Testing.*` or `Microsoft.ApplicationInsights` package
  remains in any lock file, and `--logger trx` writes a TRX file.
- **Deviations from the plan, accepted at inspection:**
  - `DOTNET_CLI_TELEMETRY_OPTOUT` and `DOTNET_NOLOGO` are set at workflow level (R19).
  - Each checkout sets `persist-credentials: false`, because no job pushes.
  - CI is watched by run id (R12). The commit is made with a message file, then pushed.
  - The workflow is named `CI` and its steps are named. The upload artifact is called
    `windows-test-results`. Job ids are exactly `linux-tests`, `windows` and `windows-arm64-tests`,
    with no `name:` overrides, so branch protection can require them by those names.
  - Dependabot groups NuGet minor and patch updates with `patterns: ['*']` and
    `update-types: [minor, patch]`.
- **Noted, not changed:**
  - `linux-tests` runs `dotnet test` with an implicit restore and no `--locked-mode`, as the plan
    writes it. The two Windows jobs restore the whole solution in locked mode, so lock drift is still
    caught.
  - `dependabot.yml` takes effect only once it reaches `main`.
- **For the owner, later:** when branch protection on `main` is set up, require `linux-tests`,
  `windows` and `windows-arm64-tests`.
- **Interruptions:** the first attempt was cut off by a usage limit after it made R20's edits. The
  controller checked those edits, and a second attempt committed them and finished the task.

### Task 3: Banned-API enforcement, HM0001 and RS0030 (done)

- **Commit:** `b1c53af` "feat(security): ban token-borrowing, injection and dynamic-import APIs
  (HM0001, RS0030)".
- **Plan:** Steps 1–8 ticked.
- **CI:** [run 45921531376](https://github.com/cwgilgalad/HiveMind/actions/runs/45921531376).
  All three jobs were green. `linux-tests` ran all three test projects: 20 tests (2 Core, 10
  analyzer, 8 policy).
- **Packages:**
  - The Roslyn family is pinned at 5.0.0 (R13). The built analyzer references only
    `Microsoft.CodeAnalysis` 5.0.0.0. No build produced CS8032, CS9057 or AD0001.
  - `Microsoft.CodeAnalysis.BannedApiAnalyzers` is 5.6.0. Its analyzer DLLs reference Roslyn
    4.12.0.0, so they load on the floor SDK.
  - `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` is 1.1.4.
  - `System.Security.Cryptography.Pkcs` is pinned at 10.0.15 (R24).
  - `Microsoft.CodeAnalysis.Analyzers` 3.11.0 comes in transitively and isn't pinned. Release
    tracking (RS2008) uses `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`.
- **Roslyn stays out of the product:**
  - `src/HiveMind.Core/packages.lock.json` holds only the BannedApiAnalyzers entry.
  - The Core build output has no `Microsoft.CodeAnalysis*.dll` and no `HiveMind.Analyzers.dll`.
- **Analyzers proven live:** a compile-only probe file in `HiveMind.Core` produced `error HM0001`
  and `error RS0030`. It was never executed and never committed.
- **Deviations from the plan, accepted at inspection:**
  - The analyzer also checks local functions, through an operation action plus a tenth test. C#
    accepts a `static extern` local function with `DllImport`, and symbol actions never see local
    functions.
  - Imports by ordinal get their own reason text.
  - The test files add `using` directives and file-scoped namespaces around the plan's class bodies,
    because the style rules require them.
  - The pull request template opens with a short "What this changes" section, above the plan's eight
    checklist items.
  - In the red step, the HM0001 release-tracking row was held back so that the first failure was
    the intended CS0246. It was restored before green.
  - The probe ran twice. The first run had a `RegistryKey` parameter only and produced HM0001
    alone (R23). The second run read a member, never executed, and produced both diagnostics.
    Neither file was ever staged.
- **Noted for later tasks:**
  - Task 7: the banned-list additions in R22.
  - Task 12: five packages declare a license URL instead of an SPDX expression, and the linked texts
    are all MIT. The license gate has to classify them:
    - `NETStandard.Library` 2.0.3 and `Microsoft.NETCore.Platforms` 1.1.0 are implicit SDK packages
      in the analyzer build. They're build-time only.
    - `Microsoft.VisualStudio.Validation` 15.0.82, `System.ComponentModel.Composition` 4.5.0 and
      `System.Security.Permissions` 4.5.0 are test-only.
  - The analyzer tests download `Microsoft.NETCore.App.Ref` 8.0 from nuget.org at test time. This
    worked locally and on all three CI jobs.

### Task 4: Key hierarchy (done)

- **Commit:** `cb01598` "feat(crypto): master key, store-key derivation and passphrase backup".
- **Plan:** Steps 1–5 ticked.
- **CI:** [run 45923422317](https://github.com/cwgilgalad/HiveMind/actions/runs/45923422317).
  All three jobs were green, with 38 tests each:
  - 20 Core: 18 new crypto tests and 2 build-info tests
  - 10 analyzer
  - 8 policy
- **Packages:** none were added and no lock file changed. Every primitive is built into .NET:
  AES-256-GCM with a 16-byte tag, HKDF-SHA256, PBKDF2-HMAC-SHA256 and `RandomNumberGenerator`.
- **Analyzers:**
  - HM0001, RS0030 and the CA53xx rules reported nothing on the new code, and nothing is suppressed.
  - One CA2000 warning in the green step was fixed by restructuring the code.
- **R25 applied:** the locked-file test also runs on Linux, with no skip. .NET's emulation of
  `FileShare.None` blocks the open there too.
- **Review Focus:**
  - Item 1 (a key that can't be unwrapped) is covered by four tests: corrupt file, unprotect
    failure, different protector, and locked file. Each one throws `MasterKeyUnavailableException`
    and leaves the file byte-identical.
  - Item 3 (awkward paths) is covered by two tests: Unicode with spaces, and a path longer than 300
    characters. Both pass on all three runners.
- **Deviations from the plan, accepted at inspection:**
  - The create path moves with `overwrite: false` (R25).
  - The six HKDF labels are written out as UTF-8 literals instead of being built from the enum
    names. The bytes are the same. Renaming a `StoreKind` member can't change a key, and an
    undefined store throws.
  - A tag mismatch in `Unwrap` is rethrown as a plain `CryptographicException`, with the original
    exception as the inner one. Every malformed-input case then throws the same type.
  - A failure while saving a new key also throws `MasterKeyUnavailableException`, so callers catch
    one type.
  - A protector id must be 1–255 ASCII characters, because the format stores a one-byte length. It
    is checked before any disk access.
  - A key file over 64 KiB is reported as damaged without being read.
  - `MasterKey` exposes its bytes only inside the assembly. Three buffers are zeroed after use: the
    `Unprotect` output, the `Unwrap` output inside `Import`, and the PBKDF2 wrap key.
- **Concerns raised by the agent, ruled on:** R26 (iteration cap), R27 (empty passphrase), R28
  (error-path tests) and R29 (stray temp file).
- **Checked inline:** `File.Exists` appears once, after a failed move. It decides only whether to
  load a key that another process has just created. If it wrongly returns false, the error
  propagates; no new key is written.
