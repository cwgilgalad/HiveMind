# HiveMind: Phase 0 Laptop Handoff

| | |
|---|---|
| **Date** | 2026-11-24 |
| **From** | The cloud planning session that wrote the spec, the audit and the plan |
| **To** | A Claude Code session on the owner's Windows dev box |
| **Repo** | <https://github.com/cwgilgalad/HiveMind> (private until the owner makes it public) |

## 1. What you're picking up

HiveMind is an open-source (Apache-2.0), safety-first Windows Registry editor written in C# on
.NET 10, with a WinUI 3 interface. The design is approved.

Phase 0 (Foundations) builds the guardrails before any feature work:
- the solution, its coding standards, and CI
- the banned-API analyzer
- crypto and the audit journal
- signing and license gates
- test harnesses
- governance docs, the threat model, and UX mockups

**Your job is to execute the Phase 0 plan.** No product code exists yet.

## 2. Read these first, in order

1. **This handoff.**
2. **The plan you'll execute:** `docs/superpowers/plans/2026-11-24-phase-0-foundations.md`. It has
   18 test-first tasks with exact interfaces, file formats and values.
3. **The approved design:** `docs/superpowers/specs/2026-11-24-hivemind-design.md`. Phase 0 is in
   §10, and the security rules in §8 aren't negotiable.
4. **The competitive feature audit:** `docs/superpowers/specs/2026-11-24-hivemind-feature-audit.md`.
   Reference only.

## 3. Decisions already made (don't reopen them)

- **Name:** HiveMind.
- **License:** Apache-2.0. Knowledge packs will use CC BY 4.0.
- **UI:** WinUI 3 (Windows App SDK). The GUI doesn't need to run on Windows Server 2016.
- **Code signing:** SignPath Foundation's open-source program. Its certificate is issued to
  "SignPath Foundation" and shared by every project in the program. That's why Tasks 10–11 verify a
  signed release catalog, not just the publisher name.
- **Claude's other calls** (spec §14) are approved by the owner "for the time being".
- **Visibility:** the repo stays private until the owner makes it public.

## 4. Repository state at handoff

| Branch | Contents |
|---|---|
| `main` | The first version of the spec, the audit and the README |
| `docs/owner-decisions` | The owner's decisions folded into the spec, `LICENSE`, the Phase 0 plan, and this handoff. Not merged yet. |

- **Branching:** start Phase 0 on a new branch, `phase-0/foundations`, created from
  `docs/owner-decisions` (plan Task 1, Step 1). Both branches merge to `main` together once Phase 0
  is done and the owner approves.
- **Source of truth:** from now on, the laptop is the source of truth. Don't work on the same branch
  from a cloud session and the laptop at the same time.

## 5. Set up the dev box

### 5.1 Pick the machine

- **Recommended: a Windows 11 virtual machine on the laptop.** Hyper-V comes with Windows 11 Pro,
  Enterprise and Education; other hypervisors work too.
  - **Why:** Phase 0 installs self-signed test certificates as trusted roots and runs some tests
    elevated. A VM keeps that off the owner's daily machine, and snapshots make it easy to reset.
    The Defender scenarios later need clean VMs anyway.
  - **Size:** at least 8 GB of RAM (16 GB is better), 4 cores, and 100 GB of disk.
- **Acceptable: the laptop itself,** if it runs Windows 10 1809+ or Windows 11.
  - Use the CurrentUser dev certificate. Windows asks the owner to confirm trusting it.
  - Leave the elevated steps to CI: the trust fixtures in Task 11 and the broker self-test in Task 9.
  - If the laptop is managed by an employer, use a personal VM instead. Test certificates and
    elevated test runs can trip corporate security tools.
- **Not suitable: a Linux container or dev container.** It can't build the WinUI app or run any
  Windows-only test (registry hives, DPAPI, signing, UI tests). Windows containers have no desktop,
  UAC or Defender.
- **On a Mac:** a Windows 11 on Arm VM works the same way. ARM64 builds and tests run locally, and
  x64 runs under emulation or in CI.

### 5.2 Install the tools

Run these in PowerShell as the owner. None needs an elevated prompt.

```powershell
winget install --id Git.Git -e
winget install --id GitHub.cli -e
winget install --id Microsoft.PowerShell -e
winget install --id Microsoft.DotNet.SDK.10 -e
winget search Microsoft.WindowsSDK         # then install the 10.0.26100 entry it lists
winget install --id OpenJS.NodeJS.LTS -e   # only for the mockup screenshots in Task 18
irm https://claude.ai/install.ps1 | iex    # Claude Code
```

**Optional extras:**
- **Visual Studio 2026** with WinUI application development, for debugging XAML. It isn't needed to
  build: the .NET SDK plus the Windows SDK are enough.
- **A Dev Drive** for the repo, which makes builds faster.

### 5.3 Get the code

```powershell
gh auth login
git config --global user.name "Your Name"
git config --global user.email 16431217+cwgilgalad@users.noreply.github.com   # keeps your personal email out of public history
gh repo clone cwgilgalad/HiveMind
cd HiveMind
git switch docs/owner-decisions
```

### 5.4 Check before writing code

```powershell
dotnet --list-sdks     # expect a 10.0.x SDK
gh auth status         # expect: logged in as cwgilgalad
Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe", "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makecat.exe"
```

If any check fails, stop and tell the owner what's missing.

## 6. How to run the plan

- **Approach:** the owner picks one when starting you.
  - **Subagent-driven** (recommended): each task gets an independent review, which suits the
    security-critical tasks.
  - **Native:** you implement every task in this session, with one review at the end.
- **Skills:** if the superpowers skills are available, use `superpowers:subagent-driven-development`
  or `superpowers:executing-plans`. If they aren't, follow the plan's steps exactly: write the
  failing test, run it, implement, run it again, commit.
- **One task at a time, in order.** Tick the plan's checkboxes as steps complete, and commit them
  with the task.
- **Test locally first.** On Windows, run every test locally, including the Windows-only ones, before
  you push. Wherever the plan says "push and watch CI", run it locally first, then push:

  ```powershell
  dotnet build HiveMind.slnx
  dotnet test HiveMind.slnx
  ```

- **CI is the second check.** Push after each task, then watch the run with
  `gh run watch --repo cwgilgalad/hivemind`.
- **CI-only work:** the Linux test job, ARM64 tests if the dev box is x64, and the release workflow's
  dry run (Task 13).
- **CI minutes:** Windows CI minutes on a private repo count double against GitHub Free's monthly
  allowance. Batch your pushes until the repo is public; public repos run Actions for free.

## 7. Ask the owner first

- Running anything elevated, or installing certificates into trusted stores (the Task 10–11
  scripts). Windows also shows a confirmation dialog that the owner has to answer.
- Changing GitHub settings, making the repo public, or opening or merging pull requests.
- Adding a license override (Task 12), or any dependency outside the allowed licenses.
- Anything that would write to the live registry. Phase 0 never should: tests use throwaway app
  hives (Task 8), and the benchmarks only read.

## 8. Gotchas found while planning

- **Test hives without admin rights:** `RegLoadAppKey` creates an empty hive when the file doesn't
  exist (it's in Microsoft's docs). Task 8 relies on this.
- **CA1416:** test projects that call Windows-only code need
  `[assembly: SupportedOSPlatform("windows10.0.17763")]`. Otherwise CA1416 breaks the build, because
  warnings are errors.
- **Release dry runs:** GitHub only offers `workflow_dispatch` once a workflow is on the default
  branch. Until it's merged, `release.yml` runs its dry run through push and PR path filters.
- **Shared certificate:** the SignPath certificate is shared across projects. Peer checks therefore
  rely on the signed catalog and the install folder, never on the publisher name alone.
- **Trusting a test root:** adding to `CurrentUser\Root` pops up a Windows dialog, and `LocalMachine`
  needs elevation. CI uses LocalMachine.
- **Broker self-test:** it only runs elevated, because of the `requireAdministrator` manifest, and
  skips otherwise.
- **Binary files:** `.reg`, `.pol` and hive files are marked binary in `.gitattributes`.
- **Screenshots (Task 18):** the step needs Playwright. On Windows, either install Playwright in a
  scratch folder and point `NODE_PATH` at its `node_modules`, or skip the screenshots and review the
  pages in Edge.
- **Banned registry API:** `Microsoft.Win32.Registry` is banned in `src/` (rule RS0030). Tests and
  benchmarks may use it.

## 9. Owner actions (remind; don't do them)

The plan's final section lists them:
- two-factor authentication
- repo settings: private vulnerability reporting, code of conduct, branch protection
- usability sessions
- Defender VMs
- the laptop benchmark numbers
- making the repo public
- the SignPath application

## 10. What "done" means

These are the plan's Phase 0 exit criteria:
- a CI build of the empty shell, signed with the dev certificate
- the analyzers and gates enforced
- a benchmark baseline, including the laptop row
- the mockups approved
- the SignPath application submitted (by the owner)

Then summarize what changed and ask the owner how they want to merge.

## 11. Prompt for the owner to paste

```
Read docs/superpowers/handoffs/2026-11-24-phase-0-laptop-handoff.md, then the Phase 0 plan and the
spec it points to. Run the checks in section 5.4 and tell me anything missing before writing code.
Then execute the plan task by task using the <subagent-driven | native> approach, on branch
phase-0/foundations created from docs/owner-decisions. Ask me before anything in section 7.
```
