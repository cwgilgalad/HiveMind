# HiveMind: Design Spec

| | |
|---|---|
| **Status** | Draft for owner review. See [Open questions](#13-open-questions-for-the-owner). |
| **Date** | 2026-11-24 |
| **Owner** | cwgilgalad |
| **Companion** | [Competitive feature audit](2026-11-24-hivemind-feature-audit.md): every audited tool's features, mapped to the IDs used below |

---

## 1. Summary

HiveMind is a Windows Registry editor written in C# for people who live in the registry. Windows
Server administrators come first, then power users, developers and incident responders. It
competes with `regedit` and the third-party editors on capability, not on looks alone. It also aims
to be the most pleasant registry tool on Windows to use.

Five promises:

1. **Find anything, fast.** One search box covers keys, values, data and *meaning*. Results stream
   in, and once indexed, search is instant.
2. **Every key explains itself.** Policy templates, curated knowledge and live system context tell
   you what a value does, what's allowed, what's risky and what enforces it, always with the source.
3. **Nothing changes until you've seen the diff, and everything can be undone.** All writes are
   staged, applied all-or-nothing, journaled and revertible, even after a restart.
4. **Least privilege, and nothing that looks like an attack.** The UI never runs elevated. A small
   signed broker performs the writes you approve. HiveMind never borrows tokens, uses no stealth,
   and logs every action.
5. **Built for admins.** Offline hives, compare, snapshots, recording, remote and fleet operations,
   every-user changes, and export as deployable code.

## 2. Intent and the why

In the owner's words: *"I hate that regedit doesn't give you any guidance on the keys and I hate its
poor search functionality."* Those two pains define the first release. Beyond them, the owner wants:

- every useful capability found in existing tools (free, pro and paid), as audited in the companion
  document
- the capabilities none of those tools offer
- a sleek, attractive and useful UX

Microsoft will probably modernize regedit eventually. A WinUI 3 Registry Editor appeared in November
2026, and Microsoft called it "a sample", not a preview of a new regedit. The owner is fine with
that. HiveMind competes on depth: guidance, search, safety, auditability and admin scale.

### Who it's for

| Persona | Top jobs |
|---|---|
| **Windows Server admin** (primary) | Find where a setting lives and understand it. Change it safely with a ticket reference and prove what changed. Repeat across servers. Fix an unbootable server's hives offline. Apply an HKCU change for every user. |
| **Power user** | Find and understand a tweak, try it, and undo it later. |
| **Developer** | See what an app or installer writes. Export it as installer or script code. Test against scratch hives. |
| **Security / IR** (secondary) | Spot persistence, hidden keys and weak ACLs. Review history. Examine offline evidence. |

### Success looks like

- A first-time user finds a value by name, data or meaning in seconds. The Inspector tells them what
  it does and where that explanation comes from.
- 100% of writes are staged, journaled and revertible. Nobody ends up with "I broke it and can't
  get back."
- Default usage causes no Microsoft Defender detections and no Defender for Endpoint alerts. This is
  validated every release (§8.2).
- In usability tests with admins: at least 90% task success on core find, understand and change
  tasks, and a System Usability Scale score of 80 or more.
- Admins prefer HiveMind to regedit for daily work by the end of Phase 1. They prefer it to their
  scripts for multi-server registry work by the end of Phase 4.

## 3. Goals, non-goals, anti-goals

**Goals**
- Best-in-class search and guidance (the focus of Phases 1 and 2).
- Parity with everything regedit does, and with the useful features of every audited tool.
- Safe-by-default editing: staging, conflict detection, journal and revert.
- A security posture that endpoint protection products and SOC teams can trust.
- A fast, accessible UI that looks native on Windows 11.

**Non-goals**
- Registry "cleaning" or automatic deletion of "orphans". HiveMind only reports these.
- Live hive defragmentation.
- Running as SYSTEM or TrustedInstaller.
- Replacing Group Policy, Intune or configuration management. HiveMind exports to them instead.
- A cross-platform GUI.
- Telemetry.

**Anti-goals** (failure even if the feature "works")
- Any default behaviour that triggers an EDR alert or resembles an attack technique.
- Any write that can't be reverted, or that leaves the system less secure. An example is ownership
  or ACL changes left behind.
- Silent or unreviewed writes, such as double-click `.reg` merges or URL-triggered actions.
- Secrets in plaintext on disk, apart from files the user explicitly exports.
- A UI that freezes on large keys or long searches.
- Guidance presented as fact without a cited source.

## 4. Constraints

- **Language and runtime:** C# on .NET 10 (LTS).
- **Operating systems:**
  - **GUI:** Windows 11, Windows 10 1809 or later, and Windows Server 2019, 2022 and 2025 with
    Desktop Experience, on x64 and ARM64.
  - **CLI and PowerShell module:** also run on Server Core.
- **Security requirements set by the owner:**
  1. Binaries, scripts, modules, installers and official knowledge packs are all code-signed.
  2. HiveMind never borrows another process's security token. That means no duplication,
     impersonation or token theft of any kind.
  3. HiveMind logs every action (defined in §7.10).
  4. Snapshots and logs may contain sensitive data, but are encrypted at rest.
  5. The SAM and SECURITY hives are excluded by default.
- **Licensing:** permissive dependencies only, and no code from GPL or AGPL projects (§9).
- **Recording:** must degrade gracefully and never surprise the user. That covers no admin rights, a
  busy kernel logger, or a policy block.

## 5. Approaches considered

The owner delegated the technical choices, so these are Claude's calls and open to override.

### A. WinUI 3 shell + shared core + on-demand elevated broker (chosen)

WinUI 3 (Windows App SDK) on .NET 10. It ships unpackaged and self-contained, with UI-agnostic view
models and a separate elevated broker process.

- **For:**
  - The native Windows 11 look the owner asked for: Mica, Fluent 2 controls, tabs in the title bar.
    Microsoft's modernized admin tools use the same look.
  - NativeAOT-capable, so it starts fast.
  - ARM64 support.
  - The UI never runs elevated, which avoids WinUI's elevated-process rough edges.
- **Against:**
  - There's no first-party data grid. Use a permissive community grid such as WinUI.TableView, or a
    custom virtualized list.
  - `TreeView` is slow on very wide keys, so HiveMind uses a custom flattened, virtualized tree.
  - There's no AvalonEdit equivalent. A text editor control gets chosen in Phase 2.
  - The GUI needs Windows 10 1809 or Server 2019 or later.

### B. WPF + Fluent theme (fallback)

Same process model, with a WPF UI layer.

- **For:**
  - The most mature data controls: DataGrid and AvalonEdit.
  - The GUI runs on Server 2016.
- **Against:**
  - The built-in Fluent theme is still experimental in .NET 10 (diagnostic `WPF0001`), so Mica needs
    the MIT-licensed WPF UI library.
  - It looks *almost* native.
  - No NativeAOT.
- **Switch to B if** the GUI must run on Server 2016, or if WinUI gaps cost more than one phase of
  effort. View models stay UI-agnostic, so switching replaces views only.

### C. Rejected

- **Web stack (Electron, Tauri, or a UI built only in WebView2):** heavier, less native, and more
  processes and attack surface.
- **Avalonia:** cross-platform isn't needed, and it doesn't look native on Windows.
- **A single elevated process (the regedit model):** violates least privilege, because every write
  would be implicitly approved.
- **A Windows service backend:** a persistent elevated attack surface that looks like persistence to
  EDR products.

## 6. UX design

### 6.1 Principles

1. **Reversibility over nagging.** Staging and revert replace most confirmation dialogs. Confirm
   only what's irreversible or high-risk.
2. **Explain in place.** Guidance sits next to the value you're editing, in the Inspector, tooltips
   and typed editors, always with its source.
3. **Recognition over recall.** Use autocomplete, breadcrumbs, favorites, smart folders and a
   command palette.
4. **Progressive disclosure.** Simple by default. Expert columns, panes and options are one click
   away (Simple/Expert mode).
5. **Speed is a feature.** Every long operation is async, streamed and cancellable, within strict
   performance budgets (§12.1).
6. **Keyboard-first, mouse-friendly, touch-tolerant.** regedit muscle memory keeps working.
7. **Status is always visible.** Elevation, connection, pending changes, index and recording.
8. **Errors explain cause and fix.** Never a bare "Access is denied".
9. **Accessible by construction.** UIA names, keyboard paths, contrast themes, and no meaning
   carried by color alone.
10. **Calm, native visual design.** Mica, Fluent typography and icons, purposeful motion, and
    compact and comfortable densities.

### 6.2 Main window

```
+-------------------------------------------------------------------------------------+
| [HM] | W32Time  x | Search: tls  x | +                                   _  []  X   |  tabs in a Mica title bar
+-------------------------------------------------------------------------------------+
| <  >  ^  (R)  Computer > HKLM > SYSTEM > CurrentControlSet > Services > W32Time  v   |  breadcrumbs; type or paste
|                                                     [ Search everything   Ctrl+K ]  |  any path format
+-----------------+--------------------------------------------+----------------------+
| * Favorites     | Filter values...                [Columns]  | Start                |
| # Smart folders | Name          Type       Data              | Service start type   |
| This PC         | (Default)     REG_SZ     (value not set)   |                      |
|  > HKCR         | DisplayName   REG_SZ     Windows Time      | 3 = Manual (current) |
|  > HKCU         | Start         REG_DWORD  3 - Manual   [v]  | 2 = Automatic        |
|  v HKLM         | Type          REG_DWORD  0x20 - Shared     | 4 = Disabled  ...    |
|    > SOFTWARE   | ObjectName    REG_SZ     NT AUTHORITY\...  | Risk: medium         |
|    v SYSTEM     | ~ DelayedAutostart       (not set)   (i)   | Takes effect after a |
| Remote          |                                            |   service restart    |
| Offline         |                                            | Source: built-in     |
| Snapshots       |                                            |   pack (cited)       |
|                 |                                            | Notes | History      |
+-----------------+--------------------------------------------+----------------------+
| (2) pending changes - Review  Ctrl+Shift+Enter  | Standard user | Index: ready | Rec: off |
+-------------------------------------------------------------------------------------+
```

- **Left:** navigation pane. It holds favorites, smart folders (saved searches) and sources: This PC,
  Remote machines, Offline hives and installations, and Snapshots.
- **Centre:** tabbed workspace. Each tab is a key view, search results, a compare, a capture review
  or a history view.
- **Right:** the Inspector, a contextual guidance pane that can be collapsed (Ctrl+I).
- **Bottom:**
  - The *Pending changes* tray, which expands into a full diff review.
  - The status bar: elevation, connection, index state and recording state.
- **Ghost rows** (`~`): settings that are known but not set, from policy templates and knowledge
  packs. They're dimmed, explained on hover, and one click stages them.

### 6.3 Search experience

```
+-- Search everything ------------------------------------------------------------------+
| tls   [in: HKLM\SYSTEM x] [type: dword x] [modified: last 30 days x]     Save   ...   |
+---------------------------------------------------------------------------------------+
| SETTINGS (from policy templates and knowledge packs)                                  |
|   SCHANNEL > Protocols > TLS 1.0 > Server > Enabled    "Allows inbound TLS 1.0"       |
|   SSL Cipher Suite Order (policy)   Computer > Admin Templates > Network > SSL ...    |
| KEYS AND VALUES      1,284 found  -  scanning HKU... 63%                     [Stop]   |
|   HKLM\...\SCHANNEL\Protocols\TLS 1.0\Server     Enabled = 0        2 days ago        |
|   HKLM\...\SCHANNEL\Protocols\TLS 1.1\Server     Enabled = 0        2 days ago        |
|   ...                                                                                 |
| [Select all] [Export...] [Replace...] [Delete...]      all of these go to Pending     |
+---------------------------------------------------------------------------------------+
```

- **Two entry points:**
  - **Ctrl+F** searches the current key and below, as regedit does.
  - **Ctrl+K** searches everything.
- **Results:**
  - Stream in with live counts and progress.
  - Group by key.
  - Have a preview pane, so you can inspect a result without leaving the list.
  - Each search can open in its own tab, and you can search within results.
- **Filters are chips.** You build them from the query language (§7.5) or from a filter flyout, and
  autocomplete suggests paths, types and dates.
- **Meaning results** come first when the query matches a policy or pack description. A meaning hit
  can point at a value that doesn't exist yet. It's shown as a ghost row that you can stage.
- **Bulk actions** (export, replace, delete) never write directly. They produce a changeset in
  *Pending changes*.
- **Saved searches** become smart folders in the navigation pane.

### 6.4 Guidance experience (Inspector, typed editors, risk)

**Inspector anatomy:**
- **Header:** name, type, and the current value decoded.
- **What it does:** a plain-language description with source chips, for example "Policy: *Turn off
  …*", "Pack: Built-in Server", "Live: Service Control Manager". Clicking a chip opens the source.
- **Allowed values:** each value with its meaning. The current value is highlighted and the default
  is marked.
- **Risk badge with the reason**, and **when the change takes effect**: immediately, after an app
  restart, after a service restart (the service is named), after sign-out, or after a reboot.
- **Managed by:** Group Policy (with the GPO name in Phase 2), MDM, or local. Includes an explicit
  "your change will be reverted" warning when that applies.
- **Related:** values, keys and references (links).
- **Details tab:**
  - last write time, class name, flags (volatile, link, virtualization) and sizes
  - security summary (owner, who can write)
- **Notes tab:** personal notes now, team notes in Phase 2. **History tab:** journal entries,
  snapshots and captures for this key.

**Typed editors:**
- When a value has a schema (from ADMX or a pack), editing uses the right control: dropdown, flag
  checkboxes, a numeric range with units, a toggle, a path picker with an existence check, or a date
  picker.
- An "Edit raw" escape hatch is always available.

**Risk and friction** (the levels come from built-in rules and packs; §7.6):

| Risk | Examples | Friction |
|---|---|---|
| Low | HKCU preferences, app settings | None beyond staging |
| Medium | service start types, network settings | Inspector warning; flagged in review |
| High | LSA, Schannel, Winlogon, boot-start drivers | Explicit confirmation at apply, with the reason shown |
| Critical | deleting large subtrees under `HKLM\SYSTEM`, `CurrentControlSet\Control`, boot/system driver keys | Typed confirmation (the key name), plus a snapshot or restore-point suggestion |

### 6.5 Pending changes and history

```
+-- Pending changes (3) ------------------------------------------------------------------+
|  ~  HKLM\SYSTEM\...\Services\W32Time   Start         3 (Manual) -> 2 (Automatic)  med   |
|  +  HKCU\...\Explorer\Advanced         HideFileExt   (new) 0                      low   |
|  -  HKLM\SOFTWARE\Contoso\Legacy       key + 14 values                            low   |
|  Note / ticket: [ CHG-1042 enable time sync autostart                             ]     |
|  1 change needs admin rights.          [Discard]  [Save as file]  [Apply (shield)]      |
+-----------------------------------------------------------------------------------------+
```

- **Type-aware diffs:**
  - DWORDs show both decimal and hex.
  - `REG_MULTI_SZ` gets a line diff, and binary data gets a hex diff with highlights.
  - Subtree deletes show their full content.
- **Notes and tickets** are optional by default. Enterprise policy can make them mandatory (Phase 4).
- **The History panel** is a searchable timeline of applied changesets. It can show the
  before/after view and revert any changeset (the revert is itself a changeset). It can also export
  a revert script.
- **Undo/redo (Ctrl+Z/Ctrl+Y)** work on staged edits. Reverting applied changes goes through History.

### 6.6 Elevation and error UX

- **Status bar:** shows **Standard user** or **Elevated session**. If the user starts HiveMind
  elevated, a banner suggests running normally. Everything still works and is still journaled.
- **Starting the broker:**
  - When any staged change needs admin rights, **Apply** shows the shield.
  - One UAC prompt starts the broker.
  - The broker stays for a configurable idle period (default 5 minutes) or until **Drop admin
    rights** is clicked.
- **"Why was this denied?"** classifies the cause and suggests the next step. The causes are:
  - permissions (shows who *can* write)
  - integrity level
  - protected by a security product's registry filter, such as Defender Tamper Protection. HiveMind
    detects this when the DACL grants access but the write still fails. Nobody can change these keys
    from user mode, and HiveMind says so.
  - the key is a symbolic link or volatile
  - the value is policy-managed
- **InfoBars instead of modal error dialogs.** Background completions show toasts, and every error
  has a "Copy details" action.

### 6.7 Recording UX (Phase 3)

- **Setup:** the Record button opens a panel to choose:
  - scope: whole registry or chosen keys
  - processes: all, picked processes, or **Launch an app and follow its process tree**
  - mode: Watch or Trace (§7.8)
  - whether to include reads (for discovery)
  - limits on time and events
- **While recording:** a red **REC** pill in the title bar shows elapsed time, event count and a
  lost-event warning. Stop and Pause are one click away.
- **After stopping:** a **Capture review** tab opens with:
  - a diff tree, a timeline and a by-process view
  - a discovery list of values that were looked up but not found
  - actions: Create changeset, Revert captured changes, Export, and Save capture (encrypted)
- **Fallback:** if Trace isn't available, recording continues in Watch mode. An InfoBar explains why
  and how to enable attribution.

### 6.8 Keyboard

| Action | Shortcut | Notes |
|---|---|---|
| Find in current key / Find next | Ctrl+F / F3 | regedit-compatible |
| Search everything | Ctrl+K | |
| Go to anything (fuzzy) | Ctrl+P | Phase 2 |
| Command palette | Ctrl+Shift+P | |
| Address bar | Ctrl+L, Alt+D | |
| Refresh / Rename / Delete | F5 / F2 / Del | regedit-compatible |
| New tab / Close tab / Switch tab | Ctrl+T / Ctrl+W / Ctrl+Tab, Ctrl+Shift+Tab | |
| Back / Forward / Parent | Alt+Left / Alt+Right / Alt+Up | |
| Undo / Redo staged edit | Ctrl+Z / Ctrl+Y | |
| Review and apply | Ctrl+Shift+Enter | |
| Add favorite / Go to favorite | Ctrl+D / Ctrl+1..9 | |
| Copy path | Ctrl+Shift+C | "Copy path as..." submenu for other formats |
| Toggle Inspector | Ctrl+I | |
| Export | Ctrl+E | |
| Record | Ctrl+R | Phase 3 |
| Expand all below | Num * | regedit-compatible; capped and cancellable |

### 6.9 Visual design

- **Fluent 2:**
  - Mica backdrop (Mica Alt behind the title-bar tabs) on Windows 11 and Server 2025, with a solid
    fallback elsewhere
  - rounded corners and Segoe UI Variable
  - Fluent UI System Icons (MIT) and Segoe Fluent Icons
  - the system accent color; light, dark and high-contrast themes that follow the system
- **Data fonts:** Cascadia Mono where installed, with Consolas as the fallback, for data and hex.
- **Density:** Compact by default, because this is a data-dense tool. Comfortable density is a toggle.
- **Semantic colors** (risk and diff added/removed/changed) are always paired with an icon and text,
  and are verified for contrast in every theme.
- **Key icon overlays:** hive, link, volatile, inaccessible (lock), policy-managed, has notes,
  changed externally.
- **Motion** is subtle (pane transitions, progress) and respects the Windows "Animation effects"
  setting.
- **Design QA:** clickable mockups and a short usability test with 3 to 5 admins happen before
  Phase 1 UI work (Phase 0). The visual direction is fixed there.

### 6.10 Accessibility and localization

- UIA names and patterns on every custom control, including the virtualized tree and grid.
- Tested with Narrator and NVDA.
- Full keyboard operation with visible focus, high-contrast themes, text scaling from 100% to 225%,
  and reduced motion.
- Accessibility Insights for Windows checks on the main surfaces every release.
- Strings live in `.resw` from day one. A pseudo-localized build catches truncation. Translations
  arrive in Phase 5.

### 6.11 Onboarding and empty states

- **First run:**
  - Four teaching tips: Search everything, the Inspector, Pending changes, and the elevation shield.
  - Offers to import regedit favorites, choose a density, and build the search index now. The index
    offer explains encryption and the SAM/SECURITY exclusion.
  - Explains that history lives in an encrypted journal tied to the Windows profile, and offers a
    recovery-key backup.
- **Empty states always suggest a next action.** For example, an empty search shows example queries.

## 7. Architecture

### 7.1 Process model

```
HiveMind.App.exe  (WinUI 3, medium integrity, never requests elevation)
  |-- ViewModels  (UI-agnostic, CommunityToolkit.Mvvm)
  |-- Core        (in-process library)
  |     Sources | ChangeEngine | Journal | Search/Index | Knowledge | Export | Crypto | Capture
  '-- Broker client ===== named pipe (DACL: interactive user's SID only;
                                      peer image path + Authenticode publisher checked) ====+
                                                                                            |
HiveMind.Broker.exe  (requireAdministrator manifest; started on demand through UAC)  <======+
  |-- Fixed, versioned command set: apply changeset ops, read protected keys,
  |   load/save hive files, host ETW capture (Phase 3), register the event source
  |-- Enables privileges on ITS OWN token just-in-time (SeBackup, SeRestore, SeSecurity)
  |-- Machine audit journal (%ProgramData%\HiveMind\Audit, Administrators + SYSTEM only)
  |   and Windows Event Log entries
  '-- Loads no plugins, hosts no UI, runs no scripts; exits after an idle timeout

HiveMind.Remote.exe  (Phase 4, unelevated worker) - PowerShell SDK remoting (WinRM/SSH);
                     isolates heavy dependencies so the UI can stay NativeAOT
hm.exe (CLI) and the HiveMind PowerShell module (Phase 2) - same Core; see elevation rules below
```

- **CLI and module elevation:**
  - **Already elevated** (an admin shell or automation): privileged operations run in-process
    through the same audited code path, with the same journals and Event Log entries. No UAC.
  - **Unelevated in an interactive session:** they start the broker through UAC.
  - **Unelevated in a non-interactive session:** they fail with a clear "run elevated" message.
- **Several admins on one machine** (RDS servers, for example): each client gets its own broker
  instance and a randomly named pipe. The machine journal tolerates concurrent writers, with one
  append-only segment per broker session, chained by hash.

- **Broker identity checks:**
  - The broker learns the interactive user's identity from the client's session, using
    `ProcessIdToSessionId` and the WTS APIs, and compares it with what the UI declares.
  - It never opens or impersonates the client's token.
  - It verifies the client process image path and Authenticode publisher
    (`GetNamedPipeClientProcessId`, `QueryFullProcessImageName` with query-limited access, and
    `WinVerifyTrust`).
- **Over-the-shoulder elevation** (a different admin account approves UAC): "HKCU" operations in the
  broker target `HKEY_USERS\<interactive user SID>`, so changes land in the signed-in user's hive,
  not the admin's.

### 7.2 Core modules

| Module | Responsibility |
|---|---|
| **Sources** | The `IRegistrySource` abstraction with capability flags: read, write, watch, last-write time, security, transactions. Implementations: `LiveSource`, `AppHiveSource` (hive documents), `OfflineInstallSource`, `SnapshotSource` (read-only), `RegFileSource` / `PolFileSource` (virtual trees), `RemoteRegistrySource` (Phase 2), `PsRemoteSource` (Phase 4) |
| **ChangeEngine** | Changeset model, preconditions, apply and compensate, revert generation (§7.4) |
| **Journal** | Encrypted, hash-chained audit store (§7.10) |
| **Search** | Live scanner, index, query parser and ranking (§7.5) |
| **Knowledge** | ADMX/ADML reader, context providers, packs, recipes, risk rules (§7.6) |
| **Export** | Formatters for `.reg`, scripts, installers and data formats (Phase 2 list in §10) |
| **Crypto** | Key management, AES-GCM, DPAPI wrappers (§7.9) |
| **Capture** | Watch, trace and import pipelines (§7.8) |

### 7.3 Registry access layer

- **A thin interop layer of our own:**
  - CsWin32-generated P/Invoke for the documented Win32 APIs: `RegOpenKeyEx`, `RegCreateKeyEx`,
    `RegEnumKeyEx`, `RegEnumValue`, `RegQueryInfoKey` (last write time, class name), `RegGetValue`,
    `RegSetKeyValue`, `RegDeleteTree`, `RegRenameKey`, `RegCopyTree`, `RegNotifyChangeKeyValue`,
    `RegLoadAppKey`, `RegSaveKeyEx`, `RegGetKeySecurity`, `RegSetKeySecurity`, `RegConnectRegistry`
    and others.
  - Hand-written declarations for the few native calls needed for names containing NUL characters,
    symbolic link inspection and key flags: `NtOpenKeyEx`, `NtCreateKey`, `NtEnumerateKey`,
    `NtQueryKey`, `NtQueryValueKey`, `NtSetValueKey`, `NtDeleteKey`, `NtDeleteValueKey`,
    `NtRenameKey`. Sysinternals RegDelNull uses the same approach.
- **Don't ship offensive-research toolkits** such as NtCoreLib/NtObjectManager in the product. They
  bundle token-manipulation code that conflicts with the "never borrow tokens" rule, and they can
  attract AV heuristics. Read them as reference only; their Apache-2.0 license allows that with
  attribution.
- **Don't use `Microsoft.Win32.Registry` in Core.** It can't expose last-write times, class names
  or NUL-containing names, and it doesn't support backup/restore semantics.
- **Limits:** no MAX_PATH assumptions anywhere. Key names can be up to 255 characters, value names
  up to 16,383, and data any size.
- **Protected keys** (including those owned by TrustedInstaller):
  - The broker opens them with `REG_OPTION_BACKUP_RESTORE`, with SeRestorePrivilege enabled only
    for the duration of the call.
  - Ownership and DACLs are never changed implicitly.
  - Keys guarded by kernel registry filters stay protected, and HiveMind explains why (§6.6).
- **32/64-bit:** an explicit `KEY_WOW64_64KEY` or `KEY_WOW64_32KEY` per tab, with indicators for
  redirected and shared keys.
- **Changes made outside HiveMind:** visible keys are watched with `RegNotifyChangeKeyValue`. They
  refresh in place without stealing focus, and get a "changed externally" badge.

### 7.4 Change engine

**Model.**
- `Changeset { Id, Author (SID), Note/Ticket, Source, Target, Operations[], CreatedUtc }`.
  - `Source` is one of Manual, Import, Replace, Recipe, CaptureReplay, Fleet or Revert.
- **Operations:**
  - `CreateKey` (with optional class and volatile flag), `DeleteKey` (subtree), `RenameKey`
  - `SetValue` (name, type, bytes), `DeleteValue`, `RenameValue`
  - `SetSecurity` (owner, DACL, SACL; any subset)
  - `CreateLink` (Phase 2)
- **Preconditions:** every operation stores the expected state from when it was staged:
  - the value's type and data hash
  - for subtree operations, the key's last write time and subkey/value counts
  - the security descriptor hash

**Apply algorithm.**
1. **Validate** types, sizes and names, plus read-only mode and policy constraints.
2. **Check preconditions** against the current state. On a mismatch, show the **conflict view**
   (what changed externally). The options are to rebase (re-stage on the new baseline), overwrite,
   or drop the operation.
3. **Back up** the full before-image of every touched value and key (whole subtrees for deletes)
   into the journal, encrypted.
4. **Apply** in dependency order: parents before children for creates, children before parents for
   deletes.
5. **Verify** each operation by reading it back.
6. **On failure at operation k**, compensate operations k-1 down to 1 in reverse, using the
   before-images. Report exactly what happened and journal it.
7. **Journal** the result. Privileged applies also get an Event Log entry.

**Revert** generates a new changeset from the before-images, so reverts are journaled and
revertible too. It first checks that the current state still equals the after-image. If it doesn't,
the conflict view opens.

**Notes.**
- Pending changes survive app restarts, kept in an encrypted draft store.
- `.reg` imports parse into changesets. `[-KEY]` and `"value"=-` become delete operations, and
  unsupported constructs are flagged, never ignored.
- Kernel transacted registry APIs exist, but compensation works the same way for local, offline,
  remote and fleet targets. Local transactions can be an optimization if a spike shows a benefit
  (builder's call).

### 7.5 Search engine

**Live scanner (Phase 1).**
- Enumerates in parallel per hive and per top-level subtree, with a bounded worker pool, using the
  native enumeration APIs.
- Streams results to the UI through a channel, with cancellation and progress.
- Searches each underlying hive once (`HKLM`, `HKU\*` including the Classes hives) and shows results
  under the most familiar alias (`HKCU`, `HKCR`), so the HKCR/HKCU views don't produce duplicate
  hits.
- Inaccessible keys are counted and listable, never silently skipped. SAM and SECURITY are skipped
  unless explicitly included (§8.4).
- Regex uses .NET `Regex`:
  - `RegexOptions.NonBacktracking` when the pattern allows
  - otherwise backtracking with a match timeout, so patterns can't hang the search (ReDoS)
- Match modes:
  - substring, whole word or whole value, case-sensitive or not
  - wildcards (`*`, `?`) and regex
  - hex byte patterns
  - numbers in decimal or hex, for DWORD and QWORD
- Targets: key names, value names and data. Data covers strings, `REG_MULTI_SZ` items, numbers and
  binary. Names containing NUL characters are included.
- Filters: scope (include and exclude paths), value type, data size, and last-write time range.

**Index (Phase 2).**
- Built per source and encrypted at rest. It holds:
  - key paths, value names, types, sizes and last-write times
  - string-like data up to a configurable length
  - decoded text: UTF-16/ANSI strings inside binaries, resolved MUI strings, expanded variables
- **Incremental refresh** compares each key's last-write time and counts, and re-reads values only
  for keys that changed.
- Builds in the background at low priority and can be paused.
- **Suggested storage:** SQLite with the FTS5 trigram tokenizer for substring search, encrypted at
  page level (SQLCipher Community Edition, BSD-3). Any alternative must keep everything encrypted
  at rest and stay permissively licensed (builder's call).

**Query language (Phase 2).**
```
query   := term (SPACE term)*
term    := ["-"] (filter | text)
filter  := field ":" value
field   := in | key | name | data | type | size | modified | is | has | policy | source | acl
value   := word | "quoted text" | /regex/ | ("<" | ">" | "<=" | ">=") (number | size | date | duration)
```

Examples:
- `tls in:HKLM\SYSTEM type:dword`
- `data:/^C:\\Program Files/ modified:<7d`
- `is:hidden`, `is:link`, `is:volatile`, `is:policy`
- `has:note`, `policy:"Turn off"`, `size:>1MB`, `-in:HKCR`
- `acl:writable-by-users` (Phase 3)

**Search by meaning (Phase 2)** queries the knowledge sources and maps hits to keys and values,
whether they exist or are ghosts. The sources are ADMX display names and explanations, pack
descriptions, and context-provider metadata such as service display names.

**Ranking:** exact path or name, then prefix, then meaning, then data matches. Recently visited keys
get a boost, and there's a "path order" toggle.

### 7.6 Knowledge system

**Sources**, highest precedence first. Every piece of guidance shows its source.

1. **Enforcement facts** (who controls the value, not what it means):
   - **Phase 1:** a heuristic that flags `...\Policies\...` paths
   - **Phase 2:** the GPO name from RSoP (`RSOP_RegistryPolicySetting`) and local `Registry.pol`
   - **Phase 3:** MDM (`PolicyManager`)
2. **ADMX/ADML** from `%WINDIR%\PolicyDefinitions` and the domain Central Store when reachable. These
   are read at runtime and never bundled.
3. **Team packs**, from sources the owner configures.
4. **Built-in packs**, curated by the project, cited, and licensed CC BY 4.0.
5. **Community packs:** opt-in, signed, and shown with a lower trust badge.
6. **Context providers:** facts from the live system (below).
7. **AI explanation** (Phase 5, opt-in): always labelled, and always cites what grounded it.

**Context providers**
- **Phase 1:**
  - services and drivers (Service Control Manager status, display name, binary, meaning of the
    start type)
  - COM (CLSID, ProgID, AppID, server binary and its signer)
  - uninstall entries
  - Run and RunOnce (target and signer)
  - `ProfileList` and `HKU` SIDs, shown as account names
  - device `Enum` entries (friendly names)
  - values that hold file paths (exists, signer, version)
- **Phase 2:**
  - network interface GUIDs, shown as adapter names
  - scheduled task cache entries, shown as task names
  - printers, AppX packages, TypeLibs and interfaces

**Pack format** (YAML validated against a JSON Schema; distributed as a folder or a signed
`.hmpack` archive):

```yaml
pack: contoso-server-baseline
version: 1.2.0
license: CC-BY-4.0
publisher: Contoso IT
entries:
  - path: HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Server
    values:
      - name: Enabled
        type: REG_DWORD
        summary: Allows inbound TLS 1.0 connections.
        allowed:
          - { value: 0, label: Disabled, note: Recommended }
          - { value: 1, label: Enabled }
        default: not set (operating system default)
        risk: high
        effect: reboot
        sensitive: false
        refs: [ "https://learn.microsoft.com/windows-server/security/tls/tls-registry-settings" ]
recipes:
  - id: disable-legacy-tls-server
    title: Disable TLS 1.0 and 1.1 for inbound connections
    risk: high
    changes: [ ...staged operations... ]
    revert: automatic
```

- **Path patterns** support per-instance wildcards (`Services\*\Start`), control-set aliasing
  (`CurrentControlSet` ↔ `ControlSet00n`) and per-user placeholders (`HKU\{sid}\...`).
- **Packs never execute code.** They describe values and propose staged changes, nothing more.
  - Signed packs show their publisher, and unsigned packs show "unverified".
  - Enterprise policy can restrict pack sources (Phase 4).

**Risk rules** combine built-in path rules with the risk a pack declares. For example, `Start` under
a boot or system driver's service key is critical. Unknown paths default to medium under HKLM and
low under HKCU.

**Starter pack (Phase 1).** The builder drafts each entry from Microsoft documentation, with
citations, and the owner reviews the server entries. It covers roughly 300 high-value keys:
- **Client essentials:** Explorer, Winlogon, Session Manager, Windows Update/WSUS, PowerShell logging,
  Defender (read-only), event log sizes.
- **Server essentials:**
  - Schannel/TLS, SMB client and server, NTLM/LSA (`LmCompatibilityLevel`, `RunAsPPL`)
  - RDP, WinRM, TCP/IP, Netlogon/Kerberos
  - HTTP.sys parameters, .NET strong-crypto settings, Credential Guard/VBS

### 7.7 Hive documents and offline installations (Phase 2)

- **Open hive files with `RegLoadAppKey` on a working copy.** The original stays untouched until
  Save.
  - Copy the `.LOG1`/`.LOG2` files along with the hive, so the kernel replays pending logs.
  - Opening doesn't need admin rights, apart from read access to the file itself.
  - The hive never appears under HKLM/HKU, and Windows unloads it automatically when the last
    handle closes, even after a crash.
- **Saving** has two possible mechanisms:
  - flush, unload and verify the working copy, then replace the original
  - `RegSaveKeyEx` through the broker for a compacted copy

  The builder picks after a spike. **Acceptance:** the original is never modified before Save; the
  saved file loads cleanly in regedit and in a forensic parser with no pending logs; the previous
  original and its logs are kept as `.bak`.
- **Offline Windows installation view:** pick a Windows directory, volume or mounted VHD(X).
  HiveMind then:
  - opens SYSTEM, SOFTWARE and DEFAULT, and the user hives (`NTUSER.DAT`, `UsrClass.dat`) it finds
    through the offline `ProfileList`
  - synthesizes `CurrentControlSet` from `SYSTEM\Select\Current`
  - builds a read-only merged HKCR view
  - leaves SAM and SECURITY out by default
- **Legacy Load Hive** (mounting into HKLM/HKU) ships in Phase 1 (OFF-04) for regedit parity and
  for tools that need a globally visible hive. HiveMind tracks every hive it mounted and prompts to unload on exit. It also warns
  that a mounted `NTUSER.DAT` blocks that user's next sign-in.

### 7.8 Recording subsystem (Phase 3)

| Tier | Needs | Gives | Mechanism |
|---|---|---|---|
| **Watch** | nothing | what changed (not who); near-zero overhead | `RegNotifyChangeKeyValue` on chosen keys/subtrees, plus a diff against a baseline |
| **Trace** | admin (broker) | who, what and when for every operation; optional stacks | ETW kernel registry events in a private, HiveMind-named session hosted by the broker (TraceEvent) |
| **Import** | a log file | the same review UI | Process Monitor CSV/XML exports |

**Graceful behaviour (requirements).**
- **Controls:** start, stop and pause, with an always-visible REC indicator.
- **Auto-stop limits:** 10 minutes, 2 million events or 512 MB by default. A ring-buffer option keeps
  "the last N minutes".
- **Filters** apply as early as possible: process IDs or process tree, key prefixes and operation
  types.
  - Reads are excluded unless discovery is on, and are de-duplicated when included.
  - HiveMind's own processes are excluded automatically.
- **Lost events** are counted and shown. A capture is never silently incomplete.
- **Fallback:** if a Trace session can't start (no admin, policy, resource limits, conflicts),
  HiveMind switches to Watch and says why.
- **Crash safety:** sessions get unique names (`HiveMind-Trace-<guid>`). When the broker starts, it
  stops orphaned sessions with the `HiveMind-` prefix, and only those.
- **Privacy:**
  - Captures are encrypted.
  - Capturing value data is optional.
  - SAM and SECURITY are excluded.
  - The discovery list keeps names only.
- **Transparency:** every capture start and stop is journaled and written to the Event Log.
- **Outputs:**
  - a diff tree, turned into a changeset to replay elsewhere
  - a revert changeset
  - a `.reg` or PowerShell export
  - discovery results, turned into ghost entries you can stage

**Spike at the start of Phase 3:** choose between the kernel logger's registry events in a
system-logger-mode session and the `Microsoft-Windows-Kernel-Registry` manifest provider.

Acceptance:
- at least 99% of write events resolve to full key paths on a scripted workload
- CPU overhead under 5% on a busy test VM
- no conflict with WPR or Process Monitor running at the same time

### 7.9 Storage and encryption

- **Data roots:**
  - `%LOCALAPPDATA%\HiveMind` per user, or next to the exe in portable mode
  - `%PROGRAMDATA%\HiveMind` for the broker's machine store, with an ACL of SYSTEM and
    Administrators only
- **Key hierarchy:**
  - a random 256-bit master key per user, wrapped with DPAPI (CurrentUser scope)
  - per-store keys derived with HKDF-SHA256, for the journal, index, snapshots, captures, notes and
    drafts
- **Algorithms:** all from .NET's built-in cryptography, with no third-party crypto.
  - AES-256-GCM for records and for chunked streams. The chunk index and a final-chunk flag go in
    the associated data, which prevents truncation and reordering.
  - SHA-256 hash chains.
- **Machine store key:** wrapped with DPAPI LocalMachine scope plus the restrictive ACL. Phase 4 adds
  an optional DPAPI-NG protection descriptor tied to an AD group, so a team of admins can read server
  journals.
- **Exports** offer two forms of encryption:
  - a passphrase: PBKDF2-HMAC-SHA256 with at least 600,000 iterations, then AES-256-GCM
  - recipient certificates: CMS `EnvelopedCms`
- **Recovery:** an optional, user-initiated master-key backup protected by a recovery passphrase.
  Without it, resetting the Windows profile loses history. Onboarding says so.
- **Crypto-shred:** "Erase all HiveMind data" deletes the keys and the stores.
- **Documented plaintext exceptions:**
  - Hive working copies while they're open, because the kernel needs plaintext. They live in a
    folder with an owner-only ACL, are deleted on close, and leftovers are cleaned up at startup.
  - Files the user explicitly exports unencrypted. HiveMind warns if those include sensitive areas.
- **Diagnostic logs** are separate from the audit journal and never contain value data.

### 7.10 Audit: "log every action"

**Definition** (Claude's call, open to change): an **action** is anything that does one of these:
- changes state
- uses privilege
- moves data out of HiveMind
- crosses a boundary: an offline file, a remote machine, a plugin or pack, or the network

All actions are logged by default. Navigation and searches are logged only in **Verbose audit**
mode, which is off by default and can be enforced by enterprise policy. They're high-volume, and
logging them would turn the journal into a map of everything you looked at.

| Always logged | What's recorded |
|---|---|
| **Registry writes** | create, set, rename, delete, ACL and owner changes; before/after images; changeset ID; source (manual, import, replace, recipe, capture replay, fleet, revert); note/ticket |
| **Privilege** | broker start/stop and the UAC outcome; privileges enabled and disabled per operation; opt-ins to protected hives |
| **Data movement** | exports (scope, format, destination); values copied to the clipboard (path only); snapshot create/restore/delete; capture start/stop/save |
| **Boundaries** | offline hive open/save (path and SHA-256); remote sessions (target, auth method, account); fleet runs; plugin and pack loads (identity, signature); AI requests (what was sent, after redaction) |
| **Configuration** | settings and policy changes; key rotation; data erase |

**Record format**
- **Clear header:** sequence number, UTC time, action type, actor SID, elevation flag, previous hash
  and hash. Keeping it in the clear means integrity can be verified without decrypting.
- **Encrypted body:** paths, before/after data and notes.

**Where records go**
1. **The per-user journal:** every action by that user.
2. **The machine journal:** written by the broker, and covers every privileged action. A standard-user
   process can't erase it.
3. **The Windows Event Log:** a summary of each privileged action, without values.
   - Phase 1 writes to the Application log under the source "HiveMind". That log is broadly
     readable, so entries stay summaries.
   - Phase 4 adds a dedicated `HiveMind/Audit` channel with an admin-only read ACL, ready for event
     forwarding (WEF).

**Hash anchors:** every N records, and at the end of each session, the latest hash goes to the
Event Log. Tampering with the journal is then detectable.

**Viewer:** the History panel filters by time, user, key, action and risk. It opens before/after
views and offers revert.

**Retention:** nothing is deleted automatically by default. When a journal passes 1 GB, HiveMind
offers to archive older segments to an encrypted export. Enterprise policy can set retention
periods (Phase 4). Before-images are kept as long as their journal entry, so any retained change
stays revertible.

### 7.11 Remote and fleet

- **Phase 2, classic remote:** `RegConnectRegistry`, using the RemoteRegistry service.
  - If the service is stopped, HiveMind explains what's needed.
  - Starting the service remotely is opt-in and journaled, because it resembles attacker tradecraft.
    The service's original state is restored on disconnect.
- **Phase 4, modern remote:** PowerShell remoting through the PowerShell SDK inside
  `HiveMind.Remote.exe`.
  - **Transports:** WinRM over HTTP or HTTPS with Kerberos/Negotiate, and SSH for PowerShell 7.
    Never CredSSP.
  - **Remote commands:** a small, readable command module. It's never obfuscated and never uses
    encoded commands.
  - **Optional JEA package** (role capability plus session configuration): helpdesk staff can be
    allowed HiveMind's registry operations on servers and nothing else.
- **Fleet (Phase 4):**
  - **Targets:** an AD OU or group, a CSV file, or an ad hoc list.
  - **Queries and compares:** throttled parallelism, per-machine status, a results matrix (value
    across machines), and drift from a baseline.
  - **Changesets** apply in waves (a canary first, then the rest), with stop-on-failure thresholds,
    per-machine rollback bundles, and HTML/CSV reports.
- **Every-user changes (Phase 4):**
  - Profiles are enumerated from `ProfileList`.
  - Signed-in users are changed through `HKU\<SID>`.
  - For signed-out users, HiveMind loads the real `NTUSER.DAT` and `UsrClass.dat` with
    `RegLoadAppKey`. It backs each file up first, holds it for as short a time as possible, and
    skips profiles that are locked.
  - The Default profile covers future users.
  - Each user gets a preview and a result.

### 7.12 Extensibility (internal from Phase 1; public SDK in Phase 4)

- **Extension points:**
  - value decoders and editors
  - context providers and knowledge sources
  - exporters
  - analyzers (security lens rules)
  - sources
- **Loading:** each plugin gets an `AssemblyLoadContext` in the UI or worker processes, and never
  in the broker.
  - An `AssemblyLoadContext` isn't a security boundary, and plugins run with the user's rights. So
    by default only signed plugins from trusted publishers load. Users can trust a publisher, and
    enterprises can lock this down.
- **Dogfooding:** first-party features are built on the same interfaces.

### 7.13 Technology stack (licenses re-verified by the Phase 0 license gate)

| Purpose | Choice | License |
|---|---|---|
| Runtime | .NET 10 (LTS) | MIT |
| UI | Windows App SDK / WinUI 3 | MIT (WinUI); Windows App SDK runtime is redistributable |
| MVVM | CommunityToolkit.Mvvm | MIT |
| Extra controls | Windows Community Toolkit; WinUI.TableView or a custom virtualized grid | MIT |
| Icons | Fluent UI System Icons; Segoe Fluent Icons (system font) | MIT; OS font |
| Interop | Microsoft.Windows.CsWin32, plus our own declarations | MIT |
| Storage | Microsoft.Data.Sqlite / SQLitePCLRaw (+ SQLCipher Community Edition if chosen) | MIT / Apache-2.0 (/ BSD-3) |
| YAML | YamlDotNet | MIT |
| ETW | Microsoft.Diagnostics.Tracing.TraceEvent | MIT |
| Remoting | Microsoft.PowerShell.SDK | MIT |
| Forensic parsing (Phase 5) | Eric Zimmerman's `Registry` library | to verify (believed MIT) |
| Offline hive creation (optional) | `offreg.dll` (Microsoft redistributable) | Microsoft redistribution terms (verify) |
| Tests | xUnit or MSTest, FlaUI, SharpFuzz, BenchmarkDotNet | Apache-2.0 / MIT |
| SBOM | CycloneDX for .NET | Apache-2.0 |
| Installers | MSIX tooling; MSI via WiX (check its current maintenance-fee terms if commercial) or an alternative | various |

## 8. Security engineering

### 8.1 Threat model (summary; the full STRIDE document is a Phase 0 deliverable)

**Assets:**
- system integrity (the registry)
- secrets held in the registry, snapshots and journals
- admin credentials
- the user's trust in what HiveMind shows

| Threat | Mitigation |
|---|---|
| Malware drives the elevated broker | The broker starts only through UAC. The pipe DACL admits only the interactive user's SID. The peer's image path and Authenticode publisher are pinned. The command schema is fixed, and every command is journaled. |
| Malicious `.reg`, changeset, pack or link | Always previewed and staged, never auto-applied. `hivemind://` links only navigate. Packs can't run code. Signature badges throughout. |
| DLL planting or search-order hijacking | `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32)` at startup. App-local DLLs are signed and verified. Per-machine installs go under Program Files. |
| Plugin abuse | Signed-publisher policy, never loaded in the broker, capability manifest |
| Journal tampering | A broker-owned machine journal, hash chains, and Event Log anchors |
| Secret leakage | Encryption at rest, SAM/SECURITY excluded, redacted diagnostics, and AI opt-in with redaction |
| Credential exposure in remoting | Kerberos by default, no CredSSP, and no stored passwords unless the user opts into Credential Manager |
| Misleading guidance | Every statement cites its source; trust levels; AI output is labelled |

### 8.2 EDR-conscious design rules

**Never:**
- **Use another process's token.** That covers opening, duplicating, impersonating or using one:
  `DuplicateTokenEx`, `ImpersonateLoggedOnUser`, `ImpersonateNamedPipeClient`, `SetThreadToken`,
  `CreateProcessWithTokenW` / `CreateProcessAsUser` with a foreign token. There's also no "Run as
  SYSTEM/TrustedInstaller" mode. A banned-API analyzer in CI enforces this.
- **Interfere with other processes:** no code injection, no hooking, and no duplicating handles out
  of other processes. So there's no "open handles" view built that way (see X-05).
- **Interfere with security tooling:** no touching, disabling or tampering with security products,
  AMSI, Defender settings, or ETW sessions HiveMind didn't create. Defender's keys are read-only in
  HiveMind.
- **Shell out:** HiveMind never launches `reg.exe`, `regedit.exe`, `cmd.exe`, `powershell.exe
  -EncodedCommand` or other "living off the land" binaries to do its work.
- **Save the SAM, SECURITY or SYSTEM hives with `RegSaveKey(Ex)`.** That's a classic
  credential-dumping indicator (MITRE ATT&CK T1003.002). Snapshots of SYSTEM use API enumeration,
  and SAM and SECURITY are excluded by default everywhere.
- **Create persistence:** no services, scheduled tasks, Run keys, WMI subscriptions, or Image File
  Execution Options `Debugger` values. IFEO `Debugger` (T1546.012) is the usual way "replace
  regedit" options work. Shell integration (file associations, an App Paths alias, the protocol
  handler) is opt-in and fully removable.
- **Phone home:** no telemetry, and update checks are opt-in.

**Always:**
- **Sign everything:** every binary, script, module, installer and official pack. Timestamp the
  signatures and verify peers at runtime.
- **Prefer documented APIs.** Native calls are limited to the short list in §7.3.
- **Report privileged actions** to the Event Log, where SOC and SIEM tools see them, as well as the
  journal.
- **Keep ETW sessions tidy:** name them `HiveMind-*`, scope them, and stop them on exit.
- **Ask first, then log** before anything that resembles attacker tradecraft, even when it's
  legitimate. Examples are starting RemoteRegistry remotely and reading protected hives.

**Release gate:** every release runs a scripted scenario suite on a VM with Microsoft Defender, and
with Defender for Endpoint where available. Any detection blocks the release until it's resolved.

### 8.3 Signing and supply chain

- **Code-signing options** (the choice is Open question 3). SmartScreen reputation builds over time
  with any of them.
  - **Azure Artifact Signing** (formerly Trusted Signing): the Basic plan is about $9.99 per month.
    Individual developers currently need to be in the US or Canada.
  - **An OV certificate:** roughly $150–300 per year.
  - **SignPath Foundation:** free for qualifying open-source projects.
- **What gets signed:**
  - every PE file HiveMind produces, plus the MSI and MSIX packages
  - PowerShell modules and scripts, including the JEA package
  - official knowledge packs (detached signatures)
  - release checksums
- **Runtime verification:**
  - the UI verifies the broker before launching it, and the broker verifies the UI
  - plugin and pack loaders verify signatures
  - Debug builds trust a local test certificate; Release builds pin the production publisher
- **Supply chain:**
  - locked package versions (`packages.lock.json`) and Dependabot
  - the license gate and an SBOM for every release
  - GitHub artifact attestations where the plan and repo visibility allow
  - deterministic builds and SourceLink

### 8.4 Sensitive data

- **SAM and SECURITY:**
  - **Default:** shown locked, and excluded from search, the index, snapshots, captures, compare,
    exports, fleet queries and AI context.
  - **Opt-in** (the Phase 3 viewer) requires all of these:
    - elevation and a typed confirmation
    - a per-session scope
    - a journal entry and an Event Log entry
    - a warning that EDR products may alert
- **Packs can mark values as sensitive**, for example stored credentials in third-party app keys.
  Sensitive values are:
  - masked in the UI by default
  - excluded from AI context
  - redacted in diagnostic logs

## 9. Licensing guardrails

- **Project license:** the owner decides (Open question 1). Until then, the repo is private with no
  license, which means all rights reserved.
- **Dependencies:**
  - **Allowed:** MIT, Apache-2.0, BSD-2/3, ISC and Zlib; MS-PL case by case.
  - **Denied:** GPL, AGPL, LGPL, SSPL, BUSL, and non-commercial Creative Commons licenses.
  - CI fails on a violation, and `THIRD-PARTY-NOTICES` is generated automatically.
- **Clean-room rule:** don't copy code, text or assets from copyleft competitors (RegKit is
  AGPL-3.0, FluentRegEdit is GPL-3.0) or from closed-source tools. Features are designed from public
  descriptions only. Permissively licensed projects (for example TotalRegistry, MIT) can be
  referenced with attribution.
- **Microsoft content:** ADMX/ADML files are read from the machine at runtime and never
  redistributed. Packs link to Microsoft Learn rather than copying it, and quote it only where its
  license terms allow, with attribution.
- **Knowledge packs:** built-in and community packs are licensed CC BY 4.0, separately from the code.
- **Name:**
  - No registry tool named "HiveMind" was found as of November 2026, but unrelated software uses
    "Hivemind".
  - Run a trademark search before any commercial release.
- **Contributions:** if the project is open-sourced, contributors sign off under the Developer
  Certificate of Origin (DCO).

## 10. Roadmap

**Column key:**
- IDs match the [feature audit](2026-11-24-hivemind-feature-audit.md).
- *Inspired by* lists tools that already offer something similar. **New** means no audited tool
  offers it.

**What changed from the first proposal:**
- Search and guidance moved into v0.1, because they're the owner's two pain points.
- Phase 0 was added, because the security requirements must exist before any feature.
- Phase 5 was added for expert features.

### Phase 0: Foundations (no user-facing features)

- Solution layout and standards: nullable reference types, analyzers, warnings as errors,
  `.editorconfig`.
- CI on Windows runners: x64 and ARM64 builds, unit and integration tests against app-hive fixtures.
- Signing pipeline and runtime signature verification, with dev-certificate trust in Debug builds.
- License gate, `THIRD-PARTY-NOTICES`, SBOM.
- Banned-API analyzer (§8.2) and a PR checklist for EDR-sensitive behaviour.
- Crypto and key-management module and the journal format, with tests.
- STRIDE threat model document.
- Benchmark harness for enumeration and search, which validates the §12.1 budgets.
- Defender scenario VM and scripts for the release gate.
- **UX:** clickable mockups of the main window, search and pending changes, and a usability test
  with 3 to 5 admins.

**Exit:** a signed CI build of an empty shell, the analyzers and gates enforced, a benchmark
baseline, and the mockups approved.

### Phase 1: v0.1 "Find & Understand" (MVP; replaces regedit for daily use)

**Milestones:** M1 read-only browsing, then M2 safe editing (staging, broker, journal), then M3
search, then M4 guidance.

| ID | Feature | Inspired by |
|---|---|---|
| NAV-01 | Native-API tree and value list: lazy and virtualized; shows NUL-named, symlink, volatile and inaccessible keys with badges | regedit, TotalRegistry, Registry Finder |
| NAV-02 | Tabs with session restore and tear-out windows | Registry Finder, RegKit, Registrar, RegAlyzer, Regedix, Registry Workshop |
| NAV-03 | Breadcrumb address bar with sibling drop-downs, autocomplete and path history | regedit, Registrar, O&O RegEditor, Regedix |
| NAV-04 | Paste-anything navigation: every path format (`HKLM\`, `HKEY_…`, `Computer\HKEY_…`, `HKLM:\`, `Registry::`, `\REGISTRY\…`), `.reg` headers, `reg.exe` and PowerShell lines; a clipboard "Go to" chip; `hivemind <path>` on the command line | RegJump, Registry Finder, Regedix |
| NAV-05 | Back, forward, parent, and navigation history | Registrar, Registry Finder, Regedix |
| NAV-06 | Favorites: folders, notes, colors, hotkeys; import from regedit; JSON import/export | regedit, Registry Finder, O&O RegEditor, Registrar, RegKit |
| NAV-07 | Copy path as: full, abbreviated, PowerShell, native, `.reg` header, `reg.exe` | regedit, Registrar |
| NAV-08 | 32/64-bit view per tab; WOW6432Node redirection indicators | RegKit |
| NAV-09 | Auto-refresh of visible keys and a "changed externally" badge | RegKit |
| NAV-11 | Command palette (Ctrl+Shift+P) | **New** |
| NAV-13 | Restore the last key, tabs and layout | regedit, Registrar, O&O RegEditor, RegKit |
| VIEW-01 | Sortable, resizable columns: Name, Type, Data, Decoded, Size, Last write, Note | TotalRegistry, Registrar |
| VIEW-02 | Filter-as-you-type in the current key | RegKit, O&O RegEditor |
| VIEW-03 | Decoded display: MUI strings, expanded variables, decimal/hex numbers, `REG_MULTI_SZ` preview | TotalRegistry, Regedix |
| VIEW-04 | Key details: last write time, class name, counts, data size, flags (including virtualization), security summary | TotalRegistry, Registrar, RegKit |
| VIEW-05 | Security indicators while browsing | Registrar |
| VIEW-06 | Display options: abbreviations, density, fonts, Simple/Expert mode | Registrar, RegKit |
| VIEW-08 | Optional last-write timestamps in the tree | Regedix |
| EDIT-01 | Every value type, including custom type numbers; changing a value's type with a conversion preview | regedit, Registrar |
| EDIT-02 | Editors: `REG_MULTI_SZ` list, DWORD/QWORD in decimal or hex, virtualized hex editor for large binaries | regedit, O&O RegEditor, TotalRegistry |
| EDIT-03 | Schema-aware editors (dropdowns, flags, ranges) from ADMX and the starter pack | RegAlyzer (constant names) |
| EDIT-04 | Create, rename and delete keys and values, including NUL-named ones; "force delete" through the restore privilege | regedit, RegDelNull, Registry Finder, Registrar |
| EDIT-05 | Copy, cut, paste and duplicate keys and subtrees; drag-and-drop | Registrar, RegCool, Registry Finder, TotalRegistry, O&O RegEditor |
| EDIT-06 | Multi-select bulk set, delete and export | **New** |
| SRCH-01 | Search everything: background, multi-threaded, streaming results, progress, cancel | Registry Finder, Registrar, RegAlyzer, O&O RegEditor, RegScanner |
| SRCH-02 | Matching: substring, whole, case, wildcards, regex with timeout, hex bytes, numbers | Registry Finder, Registrar, O&O RegEditor, RegKit |
| SRCH-03 | Targets: keys, value names, data (strings, `REG_MULTI_SZ` items, numbers, binary), NUL-named keys | Registry Finder, Registrar, RegDelNull |
| SRCH-04 | Filters: scope include/exclude, type, size, last-write range | Registrar, Registry Finder, RegScanner |
| SRCH-05 | Results UX: grouped, preview pane, open in tab, multiple result tabs, search within results | Registry Finder |
| SRCH-06 | Bulk actions on results, all staged: delete; export (`.reg`, CSV, JSON); replace with a preview and regex groups | Registrar, Registry Finder, RegKit, RegScanner |
| GUIDE-01 | Inspector: description, allowed values, default, risk, when it takes effect, source citation | Registrar (descriptions), RegAlyzer (value documentation) |
| GUIDE-02 | Context providers (Phase 1 set, §7.6) | Registrar (account names, CLSID utility) |
| GUIDE-03 | ADMX/ADML reader (local and Central Store); reverse lookup from key to policy | Policy Plus |
| GUIDE-04 | Ghost settings for policy keys (from ADMX) | RegKit (trace-simulated keys) |
| GUIDE-05 | Policy-path enforcement warning | **New** |
| GUIDE-06 | Starter knowledge pack, about 300 cited client and server keys | Registrar (standard bookmarks), RegAlyzer (tweak database) |
| GUIDE-07 | Personal notes on keys and values (encrypted, searchable) | RegKit (comments), Registrar (descriptions) |
| GUIDE-11 | Risk model that drives confirmations | **New** |
| SAFE-01 | Change engine: preconditions, backup, apply, verify, compensate (§7.4) | **New** |
| SAFE-02 | Pending changes tray with a type-aware diff, risk, note/ticket, apply/discard | PowerToys Registry Preview (for `.reg` only) |
| SAFE-03 | Undo/redo of staged edits; revert any applied changeset, including after a restart | Registry Finder, RegCool, Registry Workshop, TotalRegistry, RegKit, Registrar, SetACL Studio |
| SAFE-04 | History panel: searchable timeline, before/after views, revert-script export | Registrar (tracks all changes), Registry Finder (History) |
| SAFE-05 | `.reg` import shown as a staged diff against the live registry; never a blind merge | PowerToys Registry Preview |
| SAFE-06 | Risk-based confirmations (configurable); typed confirmation for critical deletes | Registrar (configurable prompts), O&O RegEditor |
| SAFE-07 | Read-only mode, global or per tab | Registrar, RegKit |
| SEC-01 | Classic permissions dialog (DACL, SACL, owner, Effective Access); changes are journaled and revertible | regedit, Registrar |
| OFF-04 | Load/Unload Hive into HKLM/HKU, the regedit way: every mount is tracked, HiveMind prompts to unload on exit, and it warns that a mounted `NTUSER.DAT` blocks that user's sign-in | regedit, RegKit |
| SEC-06 | SAM and SECURITY locked by default | **New** |
| PRIV-01 | Unelevated UI with an on-demand, signed, verified broker | **New** |
| PRIV-02 | Writes through backup/restore privileges, with no ownership or ACL changes, for protected and TrustedInstaller-owned keys | RegKit, SetACL Studio |
| PRIV-03 | HKCU follows the signed-in user under over-the-shoulder elevation | RegKit |
| PRIV-04 | "Why was this denied?" explainer | **New** |
| PRIV-05 | Elevated-session mode with a banner when the user starts HiveMind elevated | **New** |
| IO-01 | `.reg` export (REGEDIT5 UTF-16 and REGEDIT4) of keys, selections and results | regedit, all editors |
| IO-02 | Export results or a selection as CSV or JSON | RegScanner, O&O RegEditor, Registry Finder |
| ENT-01 | Per-user audit journal: encrypted, hash-chained, every action (§7.10) | **New** |
| ENT-02 | Machine audit journal written by the broker, plus Windows Event Log entries and hash anchors | **New** |
| UX-01 | WinUI 3 Fluent shell: Mica, tabs in the title bar, light/dark/high contrast, accent color, density | O&O RegEditor, Regedix, Registry Finder (dark mode) |
| UX-02 | regedit-compatible and modern shortcuts | regedit |
| UX-03 | Accessibility baseline (§6.10) | — |
| UX-04 | Onboarding and empty states | — |
| UX-06 | Long operations: progress, cancel, background, toasts | Regedix (progress) |
| UX-09 | Portable mode; x64 and ARM64 builds | O&O RegEditor, TotalRegistry |
| UX-11 | Settings UI and JSON settings; HiveMind keeps its own settings out of the registry | O&O RegEditor |

**Exit criteria:**
- Every item marked Phase 1 in the audit's regedit parity list works.
- Search returns its first result within 250 ms and scans the whole registry within budget.
- The Inspector explains every ADMX-backed value and every starter-pack key, with sources.
- 100% of writes are journaled and revertible.
- The Defender release gate is clean, and the accessibility checks pass.

### Phase 2: v0.2 "Instant & Connected"

| ID | Feature | Inspired by |
|---|---|---|
| SRCH-07 | Persistent encrypted index, instant results, incremental refresh | **New** |
| SRCH-08 | Query language with chips and autocomplete | **New** |
| SRCH-09 | Saved searches shown as smart folders | **New** |
| SRCH-10 | Search by meaning (policy text, pack descriptions, provider metadata) | Registrar (searches bookmark descriptions) |
| SRCH-11 | Search inside decoded data (strings within binaries, MUI text, expanded variables) | **New** |
| SRCH-12 | Search offline documents and snapshots (fleet search arrives in Phase 4) | NirSoft OfflineRegistryFinder |
| SRCH-13 | Fuzzy go-to-anything (Ctrl+P) | **New** |
| NAV-06b | Favorites with live value preview | RegAlyzer (live bookmarks) |
| NAV-10 | Native `\REGISTRY` namespace view | TotalRegistry |
| NAV-12 | `hivemind://` navigation-only links, App Paths alias, jump lists | Registrar (`reg://` links, shortcuts) |
| VIEW-02b | Include/exclude view filters | Registrar |
| VIEW-07 | Split view (side-by-side panes) | **New** |
| EDIT-03b | Schema-aware editors from every pack | — |
| EDIT-07 | Edit a subtree as text (`.reg` or YAML); the minimal diff goes to staging | **New** |
| EDIT-08 | Create symbolic links and volatile keys (with warnings) | RegKit |
| EDIT-09 | Import a value's data from a file, or export it to one | Registrar |
| EDIT-10 | Structured decoders and editors: FILETIME, SYSTEMTIME, GUID, SID, security descriptors as SDDL, MRUListEx, WINDOWPLACEMENT, LOGFONT, device property types, `REG_RESOURCE_LIST` family, Base64, bit flags; user-defined templates | RegKit, RegAlyzer, Registry Explorer plugins, regedit (resource lists) |
| GUIDE-02b | More context providers (§7.6) | — |
| GUIDE-04b | Ghost settings from packs | — |
| GUIDE-05b | Group Policy attribution: GPO name from RSoP, local `Registry.pol` | **New** |
| GUIDE-08 | Knowledge packs: team and community sources, signing, trust levels, "Document this key" editor | **New** |
| GUIDE-09 | Recipes: curated, cited change bundles with risk, prerequisites and automatic revert, always staged | Registrar (standard tweaks), RegAlyzer (tweak database) |
| GUIDE-10 | Reference links (CLSID, ProgID, AppID, TypeLib, Interface, SID, paths, services, variables, packages); "find references"; a report-only dangling-reference check; find and fix file references through staged replace | Registrar (CLSID Utility, File Reference Editor), Regedix (jump to GUID roots), TotalRegistry |
| SAFE-08 | Snapshots: manual, encrypted, enumeration-based, scoped; SAM/SECURITY excluded | RegCool, RegAlyzer, Regshot, RegistryChangesView |
| SAFE-09 | Restore a value or subtree from a snapshot or the journal, through a changeset | Registrar (backup/restore), RegCool |
| SAFE-10 | Create a System Restore point before apply (client editions only; Windows Server has no System Restore) | **New** |
| SAFE-12 | Changeset files (`.hmchange`): save, open, review, apply | **New** |
| CMP-01 | Compare any two sources: keys and subtrees, hives, 32- vs 64-bit, control sets, users, live vs `.reg`, snapshot, offline or remote; side-by-side and unified; type-aware | RegCool, Registrar, RegKit, Regshot, RegistryChangesView |
| CMP-02 | "Make A like B", producing a changeset | **New** |
| CMP-03 | Before/after wizard: snapshot, do something, compare | Regshot, RegCool |
| OFF-01 | `.reg` workbench: virtual tree, validation, diff against live, apply through staging, convert REGEDIT4/5; optional default `.reg` handler that always previews first | PowerToys Registry Preview, Registrar |
| OFF-02 | `.pol` (PReg) open, edit and save; `.reg` ↔ `.pol` conversion | Registry Workshop, Policy Plus |
| OFF-03 | Hive documents through app hives and working copies (§7.7) | Registry Finder, RegKit, Registrar, NirSoft OfflineRegistryView |
| OFF-04b | Hive manager: every loaded hive with its file path, size and flags | RegAlyzer (Hive Manager), RegKit |
| OFF-05 | Offline Windows installation view (§7.7) | Registry Finder (partial), Registrar |
| OFF-06 | Save a key as a hive file (never SAM, SECURITY or SYSTEM); compact an offline copy | regedit, RegKit |
| IO-02b | XML and HTML exports | O&O RegEditor, RegScanner |
| IO-03 | Export as code: PowerShell (idempotent, `-WhatIf`, with revert), `reg.exe`/batch, C#, C++, Python, Group Policy Preferences `Registry.xml`, `Registry.pol`, Intune remediation detect/fix pairs, DSC, Ansible, WiX, Inno Setup, NSIS | RegKit (`.reg` ↔ `.cmd` ↔ `.ps1`) |
| IO-04 | "Copy as…" any of the above for a value or key | **New** |
| IO-05 | Print or HTML report | regedit (Print) |
| DEV-01 | CLI `hm`: query, search, diff, snapshot, apply, revert, export; JSON output; stable exit codes | Registrar (command-line edition), RegKit, `reg.exe` |
| DEV-02 | Signed PowerShell module | **New** |
| DEV-03 | "Show equivalent command" for any GUI action | **New** |
| REM-01 | Classic remote registry for several machines at once (§7.11) | regedit, TotalRegistry, Registry Finder, RegKit, Registrar, SetACL Studio |
| UX-05b | Diff visualizations for compare | — |
| UX-10 | Opt-in shell integration: file associations, "Open with HiveMind" | Registrar (shell extensions), RegKit ("Edit with") |

**Exit criteria:**
- Indexed queries return within 100 ms.
- Compare, snapshots and offline documents round-trip cleanly.
- The CLI and the module can script every Phase 1 and Phase 2 action.

### Phase 3: v0.3 "Observe & Secure"

| ID | Feature | Inspired by |
|---|---|---|
| REC-01 | Watch mode (no admin) | **New** |
| REC-02 | Trace mode: ETW through the broker; attribution; filters; optional stacks | Registrar (monitor and process filters), Process Monitor |
| REC-03 | Record an app (launch and follow its process tree), producing a changeset or `.reg` | NirSoft RegFromApp |
| REC-04 | Capture review: diff tree, timeline, attribution; turn into a changeset, revert or export | **New** |
| REC-05 | Settings discovery: failed lookups become ghost entries you can stage | RegKit (static trace presets) |
| REC-06 | Import Process Monitor CSV/XML | **New** |
| REC-07 | Watch list notifications for pinned values | RegAlyzer (live bookmarks) |
| SAFE-11 | Per-key time travel across the journal, snapshots and VSS shadow copies; side-by-side; restore | NirSoft RegistryChangesView (VSS compare) |
| SEC-02 | Permissions Studio: effective access per principal, inheritance view, SDDL editor, ACL copy/paste/diff, bulk apply with preview, owner management, SACL presets; all as changesets | SetACL Studio, AccessChk, Registrar |
| SEC-03 | Weak-ACL audit, for example service keys writable by Users | AccessChk |
| SEC-04 | Security lens overlays: bidi, zero-width, look-alike, trailing-space and NUL names; oversized or encoded blobs; embedded PE or script markers | RegKit (anomaly search), RegDelNull, RegAlyzer (disassembler) |
| SEC-05 | Startup and persistence view (autostart locations) with signer status | Autoruns |
| SEC-06b | Opt-in viewer for protected hives (§8.4) | **New** |
| SEC-07 | "Who touches this key", using ETW | replaces TotalRegistry's open-handles view (see X-05) |
| GUIDE-05c | MDM (`PolicyManager`) awareness | **New** |
| UX-08 | Registry space analyzer: treemap of key and value sizes, hive sizes | Sysinternals RU |

**Exit criteria:**
- Recording degrades gracefully in every failure mode listed in §7.8.
- The time-travel restore round-trips.
- The security lens has no false positives on a clean Windows 11 or Server 2025 reference image
  (or each one is documented).

### Phase 4: v0.4 "Scale"

| ID | Feature | Inspired by |
|---|---|---|
| REM-02 | PowerShell remoting over WinRM or SSH through the worker; Kerberos; JEA package | Windows Admin Center (registry tool) |
| REM-03 | Fleet targets (AD, CSV, ad hoc); parallel queries; results matrix; drift | **New** |
| REM-04 | Fleet apply in waves (canary first) with rollback bundles and reports | **New** |
| REM-05 | Every-user apply: existing profiles plus the Default profile, previewed per user | **New** |
| REM-06 | Baselines: import (packs, GPO backups, security baselines), compliance overlay, remediation changesets | **New** |
| CMP-04 | Fleet matrix compare | **New** |
| SRCH-12b | Fleet search | **New** |
| SAFE-12b | Signed changeset review: author and reviewer signatures, optional two-person rule | **New** |
| ENT-03 | HiveMind's own ADMX: enforce read-only mode, disable features (remote, AI, plugins), require notes, pack allowlists, audit destinations, verbose audit | **New** |
| ENT-04 | DPAPI-NG group-protected machine journals | **New** |
| ENT-05 | Dedicated `HiveMind/Audit` Event Log channel (admin-only read; ready for event forwarding) | **New** |
| DEV-04 | Public plugin SDK (NuGet) | Registry Explorer (forensic plugins) |

**Exit criteria:**
- A 50-server fleet query and wave apply complete with rollback in a test lab.
- The JEA endpoint is verified.
- Every enterprise policy is honored.

### Phase 5: v1.0 "Expert & Polish"

| ID | Feature | Inspired by |
|---|---|---|
| OFF-07 | Offline repair mode: edit in place with automatic backups | Registrar (repair a non-running Windows) |
| OFF-08 | Forensics mode: hashed evidence intake, read-only working copy, log replay on the copy, deleted keys and values, slack, timeline, artifact decoders (UserAssist, ShellBags, BAM, AppCompatCache, MRUs), case notes, report | Eric Zimmerman's Registry Explorer/RECmd, RegRipper |
| OFF-09 | MSIX/app-private registry hives | **New** |
| OFF-10 | Cluster hive awareness: read-only by default, with guidance to use the cluster cmdlets | **New** |
| OFF-11 | VHD(X) and FSLogix profile-container hives | **New** |
| REC-08 | One-shot boot-time trace, removed automatically after use | Process Monitor (boot logging) |
| GUIDE-12 | Opt-in AI "Explain this key" and natural-language search (rules below) | **New** |
| UX-07 | Localization | O&O RegEditor, RegKit |
| — | MSI installer, MSIX/Store, winget; accessibility audit; performance hardening | — |

GUIDE-12 rules:
- **Pluggable provider:** an Anthropic Claude model by default, with enterprise and local-model
  options for air-gapped environments.
- **Grounded:** in the context providers, ADMX text and packs.
- **Redaction:** values are redacted by default.
- **Never writes:** suggestions can only land in *Pending changes*.

**Exit criteria:**
- v1.0 quality bar: every budget in §12.1 is met.
- An external accessibility review passes.
- The documentation site is complete.

### 11. Deliberately excluded (with replacements)

| # | Excluded capability (seen in) | Why | Instead |
|---|---|---|---|
| X-01 | Run as SYSTEM / TrustedInstaller (RegKit, Registry Finder, Registrar) | Requires borrowing tokens or temporary services; EDR-flagged; violates owner rule 2 | PRIV-02 backup/restore privileges |
| X-02 | "Replace regedit" through Image File Execution Options `Debugger` (TotalRegistry, RegKit and Registry Finder offer a replace option) | A persistence and hijack technique (T1546.012) that EDR products alert on | `hivemind` App Paths alias, jump lists, file associations, `hivemind://` links |
| X-03 | Live registry defragmentation (Registrar, RegCool; Registry Workshop removed it on Windows 10+) | Risky replacement of live hives at boot for little benefit; modern Windows maintains hives itself | UX-08 space analyzer, OFF-06 compact offline copy |
| X-04 | Registry cleaning (auto-deleting "orphans") | Frequent breakage; Microsoft doesn't support cleaners | GUIDE-10 report-only dangling-reference check |
| X-05 | Open key handles through cross-process handle duplication (TotalRegistry) | `PROCESS_DUP_HANDLE` across processes is a credential-theft pattern | SEC-07 ETW-based "who touches this key" |
| X-06 | Silent `.reg` merge (`regedit /s`, double-click merge) | Unreviewed writes | SAFE-05 preview-first; `hm apply` with explicit flags, journaled |
| X-07 | Thousands of uncurated tweaks (Registrar) | Unverifiable advice is harmful | GUIDE-09 curated, cited recipes and team packs |
| X-08 | Background snapshot services or scheduled tasks | Looks like persistence; more attack surface | Snapshots on demand and automatic pre-change backups |
| X-09 | "Tough delete" by resetting ownership and ACLs (Registrar) | Leaves the system weakened | EDIT-04 force delete through the restore privilege, journaled |
| X-10 | Telemetry | Privacy, and trust in an admin tool | None; local diagnostics only |

## 12. Quality

### 12.1 Performance budgets (targets; validated in Phase 0)

| Scenario | Target |
|---|---|
| Cold start to interactive | ≤ 1.0 s (NativeAOT) or ≤ 1.5 s (ReadyToRun) on a mid-range SSD laptop |
| Navigate to an existing key | ≤ 50 ms |
| Expand a key with 10,000 subkeys | first rows ≤ 150 ms, complete ≤ 500 ms, UI never blocks |
| Live search, first result | ≤ 250 ms |
| Live full-registry scan | ≤ 20 s on a typical Windows 11 machine (8 cores, SSD) |
| Indexed search | ≤ 100 ms for typical queries |
| Apply a 100-operation changeset | ≤ 1 s, excluding UAC |
| Memory | ≤ 300 MB working set in typical use (the index lives on disk) |

### 12.2 Testing

- **Unit tests** run Core against throwaway app hives. Registry roots are injectable, so tests never
  touch the real registry.
- **Integration tests** for broker paths run on GitHub-hosted Windows runners, which run as admin.
  Windows Sandbox or a VM covers UAC and Defender scenarios.
- **Golden-file tests** for every exporter and for the `.reg` and `.pol` parsers, plus round-trip
  tests (export, import, compare).
- **Fuzzing** (SharpFuzz) for the `.reg`, `.pol`, ADMX, pack, changeset and hive parsers.
- **Property-based tests** for the change engine's apply, compensate and revert invariants.
- **UI automation** (FlaUI) for critical flows, plus accessibility checks.
- **Test matrix:**
  - Windows 11 (current and previous feature updates, x64 and ARM64)
  - Windows 10 22H2 and LTSC 2021
  - Windows Server 2019, 2022 and 2025 (Desktop Experience)
  - Server Core for the CLI and module
- **ARM64 tests** run on GitHub's Windows ARM64 runners where the plan allows. Otherwise ARM64 is
  cross-compiled and smoke-tested on an ARM64 device or VM.

### 12.3 CI/CD and release

- **CI:** GitHub Actions with a protected `main`. PR checks cover build, tests, analyzers and the
  license gate, plus CodeQL where the plan allows. CodeQL is free on public repos and a paid add-on
  for private ones.
- **Releases:** SemVer. Signed artifacts, checksums, an SBOM, and attestations where available.
- **Distribution channels:** portable ZIP (Phase 1), MSIX (Phase 2), and MSI and winget (Phase 5).
  There's no silent self-update.

### 12.4 Diagnostics

- Diagnostic logs stay local and never include values.
- Crash dumps are opt-in, for the user to attach to an issue.
- No telemetry.

## 13. Open questions for the owner

1. **Open source or commercial?** The answer decides:
   - the license
   - when the repo can go public
   - the signing route (SignPath Foundation is free for qualifying open-source projects)
   - free CodeQL and attestations, which need a public repo
   - how contributions work

   Recommendation: Apache-2.0 if open source, because it includes a patent grant. If you want a
   paid Pro edition later, open core works with either choice.
2. **Must the GUI run directly on Windows Server 2016?** If yes, switch to Approach B (WPF).
   Otherwise, WinUI 3 stays and Server 2016 gets the CLI and module, plus remote management from a
   newer machine.
3. **Code-signing route:** Azure Artifact Signing (individuals currently in the US or Canada, or an
   organization), an OV certificate, or SignPath (open source only)?

## 14. Calls made by Claude (please check)

1. **WinUI 3 over WPF**, for the native, sleek look. WPF stays the documented fallback.
2. **Multi-process design:** an unelevated UI, an on-demand elevated broker, and an unelevated
   remote worker.
3. **No SYSTEM or TrustedInstaller modes.** Backup/restore privileges cover protected keys.
4. **Search and guidance moved into v0.1.** Phase 0 (security foundations) and Phase 5 (expert
   features) were added.
5. **"Log every action"** means state changes, privilege use, data movement and boundary crossings.
   Navigation and searches are logged only in Verbose audit mode.
6. **Our own thin interop layer** instead of NtCoreLib, to avoid AV heuristics and keep the API
   surface hygienic.
7. **SYSTEM snapshots use enumeration**, never `RegSaveKey`. SAM and SECURITY are excluded
   everywhere by default.
8. **No telemetry.** Update checks are opt-in, and there's no silent self-update.
9. **Knowledge packs are licensed CC BY 4.0**, separately from the code license.
10. **No IFEO-based "replace regedit"** option.
11. **The repo stays private, with no license, until Open question 1 is answered.**
12. **AI explanations are a Phase 5 opt-in.** The provider is pluggable, and suggestions can only be
    staged.
13. **Compact density is the default.**
14. **Starter-pack content** is drafted from Microsoft documentation, with citations, for the owner
    to review.
15. **Journal retention:** nothing is auto-deleted. HiveMind offers to archive at 1 GB, and policy
    controls retention in Phase 4.
16. **An already-elevated CLI** applies changes in-process, with full auditing.
17. **The test matrix** is in §12.2.

## 15. Left to the builder

- **Controls:** the specific data-grid and text-editor controls, within the license and performance
  constraints.
- **Index encryption:** SQLCipher or an equivalent that keeps everything encrypted at rest.
- **ETW approach,** after the Phase 3 spike.
- **Hive document save mechanism,** after a spike.
- **Kernel transactions:** whether to use them as a local-apply optimization.
- **Tooling:** the test frameworks and installer tooling.
- **Shortcuts and visuals:** shortcuts beyond §6.8, and visual details within Fluent guidance and
  the approved mockups.
- **Code structure:** internal module boundaries and naming.
