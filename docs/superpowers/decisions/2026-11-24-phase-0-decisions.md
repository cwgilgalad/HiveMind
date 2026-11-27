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
