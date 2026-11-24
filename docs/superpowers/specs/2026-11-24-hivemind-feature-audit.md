# HiveMind: Competitive Feature Audit

| | |
|---|---|
| **Date** | 2026-11-24 |
| **Companion to** | [Design spec](2026-11-24-hivemind-design.md) (feature IDs refer to its §10 roadmap; X-nn refers to its §11 exclusions) |

## How to read this

- **Sources:** each tool's features are listed as the vendor publishes them. Items marked † come
  from third-party listings because the official page couldn't be read during the audit.
- **Mapping:** each feature maps to a HiveMind feature ID and phase, or to a deliberate exclusion
  (X-nn) with its replacement.
- **Not hands-on tested.** Versions are as seen on 2026-11-24, and feature lists change, so re-check
  before quoting this externally.
- **Pages that couldn't be read:** torchsoft.com (Registry Workshop) returned HTTP 503, and
  kurtzimmermann.com (RegCool) returned 403 and 404. Their entries rely on third-party listings.

## Tools covered

| Tool | Vendor | Kind | License / price | Latest seen |
|---|---|---|---|---|
| Registry Editor (regedit) | Microsoft | Built-in editor | Windows | Windows 11; unchanged |
| Registry Finder | Sergey Filippov | Editor | Freeware | 2.63 (2026-10-11) |
| RegKit | nohuto | Editor | Open source, AGPL-3.0 | active (358 commits) |
| TotalRegistry | Pavel Yosifovich | Editor | Open source, MIT | 0.9.8† (Jan 2025) |
| Registrar Registry Manager | Resplendence | Editor | Home: free for home use; Professional: paid | v8 (chart updated 2026-06-18) |
| O&O RegEditor | O&O Software | Editor | Freeware | 16.1.6660 (2026-07-16) |
| Regedix | Webrox | Editor | Free | 2.0.0.0 (2025-11-04) |
| RegAlyzer | Safer-Networking | Editor | Free download; RegAlyzer Plus subscription; v2 release candidate | — |
| RegCool † | Kurt Zimmermann | Editor | Freeware | 2.x |
| Registry Workshop † | TorchSoft | Editor | Paid (30-day trial) | 5.1.0 (Sep 2022) |
| SetACL Studio | Helge Klein | Permissions manager | Freeware | — |
| NirSoft RegScanner, RegistryChangesView, OfflineRegistryFinder/View, RegFromApp | NirSoft | Utilities | Freeware | RegScanner 2.75†, RegistryChangesView 1.31† |
| Regshot | open-source project | Snapshot compare | Open source | — |
| Sysinternals Process Monitor, RegDelNull, RegJump, AccessChk, Autoruns, RU | Microsoft | Utilities | Free | — |
| Registry Explorer and RECmd | Eric Zimmerman | Forensic | Free | — |
| RegRipper | Harlan Carvey | Forensic | Open source | 3.0 |
| PowerToys Registry Preview | Microsoft | `.reg` previewer | Open source, MIT | PowerToys 0.97.x (2026) |
| Windows Admin Center (Registry tool) | Microsoft | Web-based server management | Free | — |
| Policy Plus | Fleex255 | Group Policy editor | Open source | — |
| WinUI 3 Registry Editor sample | Microsoft | Developer sample | — | Nov 2026; "a sample", not a product |

---

## 1. regedit parity list

Phase 1 must deliver every item marked Phase 1 here.

| regedit capability | HiveMind | Phase |
|---|---|---|
| Key tree and value list; address bar with `HKLM`-style abbreviations and `Computer\…` paths | NAV-01, NAV-03, NAV-04 | 1 |
| New key; new String, Binary, DWORD, QWORD, Multi-String and Expandable String values | EDIT-01, EDIT-04 | 1 |
| Modify; Modify Binary Data | EDIT-02 | 1 |
| Viewers for `REG_RESOURCE_LIST`, `REG_FULL_RESOURCE_DESCRIPTOR`, `REG_RESOURCE_REQUIREMENTS_LIST` | EDIT-02 (raw hex) in Phase 1; EDIT-10 (decoded) in Phase 2 | 1/2 |
| Rename; Delete | EDIT-04 | 1 |
| Copy Key Name | NAV-07 | 1 |
| Find (keys, values, data; match whole string) and Find Next (F3) | SRCH-01 to SRCH-03, UX-02 | 1 |
| Favorites | NAV-06, including import of regedit favorites | 1 |
| Permissions: DACL, auditing (SACL), owner, Effective Access | SEC-01 (classic dialog, including Effective Access) | 1 |
| Import `.reg` | SAFE-05 (preview first; no blind merge) | 1 |
| Export `.reg` (REGEDIT5) and Win9x/NT4 (REGEDIT4) | IO-01 | 1 |
| Export as text (with last write time and class name) | IO-02 (CSV/JSON) in Phase 1; IO-02b (TXT/HTML) in Phase 2 | 1/2 |
| Export as registry hive file | OFF-06 | 2 |
| Load Hive / Unload Hive | OFF-04 (tracked, with warnings) | 1 |
| Connect / Disconnect Network Registry | REM-01 | 2 |
| Print | IO-05 | 2 |
| View: status bar, address bar, font; F5 refresh | VIEW-06, UX-02 | 1 |
| Remembers the last key | NAV-13 | 1 |
| Multiple instances (`regedit /m`) | NAV-02 (tabs, tear-out windows) | 1 |
| Command line `/e` (export) | DEV-01 | 2 |
| Command line `/s` (silent import) | X-06: replaced by `hm apply` with explicit flags, journaled | 2 |

## 2. Full registry editors

### Registry Finder 2.63 (freeware)

| Feature (as published) | HiveMind | Phase |
|---|---|---|
| Multiple local and remote registry windows; tabs | NAV-02; REM-01 | 1; 2 |
| Cut, copy and paste keys and values | EDIT-05 | 1 |
| Undo/redo of modifications (except permission changes) | SAFE-03 (permission changes are revertible too) | 1 |
| History window | SAFE-04 | 1 |
| Fast, non-blocking search; whole results in a list; multiple find-results windows; search within results | SRCH-01, SRCH-05 | 1 |
| Regular expressions (Perl syntax); wildcards not supported | SRCH-02 (regex and wildcards) | 1 |
| Search for hidden keys (NUL characters in names) | SRCH-03 | 1 |
| Date-range and size search; Date Modified column | SRCH-04, VIEW-01 | 1 |
| Replace, performed on items in the results window | SRCH-06 (staged, previewed) | 1 |
| Save results as `.reg` or tab-separated `.txt` | IO-01, IO-02 | 1 |
| Favorites window; Ctrl+0..9 shortcuts | NAV-06 | 1 |
| Address bar (Ctrl+L); navigate from a result (Ctrl+G) | NAV-03, SRCH-05 | 1 |
| Offline registry from hive sets (SAM, SECURITY, SOFTWARE, SYSTEM, .DEFAULT; no HKCU/HKCR); rename keys offline | OFF-03, OFF-05 (adds user hives and merged HKCR; SAM/SECURITY excluded by default) | 2 |
| Permissions (Properties dialog) | SEC-01 | 1 |
| Run as Local System / Restart as System | X-01 (replaced by PRIV-02) | — |
| Dark mode; toolbar customization; translations | UX-01, VIEW-06; UX-07 | 1; 5 |
| Navigation from PowerToys Run when set as the regedit replacement | NAV-12 (alias and links); X-02 (no IFEO replacement) | 2 |

### RegKit (AGPL-3.0; ideas only, no code reuse)

| Feature (as published) | HiveMind | Phase |
|---|---|---|
| Switch between User, Admin, SYSTEM and TrustedInstaller rights; restart as SYSTEM/TI | X-01 (PRIV-01 and PRIV-02 instead) | — |
| Use backup/restore privileges | PRIV-02 | 1 |
| HKCU follows the signed-in user | PRIV-03 | 1 |
| Find with options (source, anomalies, regex via PCRE2) | SRCH-02; SEC-04 (anomalies) | 1; 3 |
| Replace | SRCH-06 | 1 |
| Compare registries | CMP-01 | 2 |
| Undo/redo, copy/paste | SAFE-03, EDIT-05 | 1 |
| Tabs with session restore (including cached find results) | NAV-02 | 1 |
| Local (64- and 32-bit views), remote and offline registries | NAV-08; REM-01; OFF-03 | 1; 2; 2 |
| Load/unload hives; save keys as hive files and restore keys from them | OFF-04; OFF-06; SAFE-09 (restore as a staged diff) | 1; 2; 2 |
| File conversion between `.reg`, `.bat`/`.cmd` and `.ps1` | IO-03 (plus many more formats, with reverts) | 2 |
| Favorites import/export | NAV-06 | 1 |
| Key Information window (including UAC virtualization flags) | VIEW-04 | 1 |
| Create symbolic links and volatile keys | EDIT-08 | 2 |
| Read-only mode | SAFE-07 | 1 |
| Auto-refresh of the open key | NAV-09 | 1 |
| Simulated keys from traces (presets per Windows release) | GUIDE-04 (ADMX ghosts); REC-05 (your own captures) | 1; 3 |
| History view; save/forget tree state | SAFE-04; NAV-13 | 1 |
| Theme modes and presets, icon sets, 10 UI languages | UX-01; UX-07 | 1; 5 |
| Value decoding (Base64, hex…); bit editor | EDIT-10 | 2 |
| Filter bar for the value list | VIEW-02 | 1 |
| Comment column for keys and values | GUIDE-07; GUIDE-08 (shared team packs) | 1; 2 |
| Simple mode | VIEW-06 (Simple/Expert) | 1 |
| Drag and drop `.reg` and hive files; "Edit with RegKit" context menu | SAFE-05, OFF-03; UX-10 | 1/2; 2 |
| CLI (import, export, convert, compare, save, load) | DEV-01 | 2 |
| Install as regedit replacement | X-02 | — |

### TotalRegistry (MIT)

| Feature (as published) | HiveMind | Phase |
|---|---|---|
| Show the real registry, not just the standard roots | NAV-10 | 2 |
| Sort the list view by any column | VIEW-01 | 1 |
| Key icons for hives, inaccessible keys and links | NAV-01 | 1 |
| Key details: last write time and number of keys/values | VIEW-04 | 1 |
| Display MUI and `REG_EXPAND_SZ` expanded values | VIEW-03 | 1 |
| Full search (Find All) | SRCH-01 | 1 |
| Enhanced hex editor for binary values | EDIT-02 | 1 |
| Undo/redo | SAFE-03 | 1 |
| Copy/paste keys and values | EDIT-05 | 1 |
| Optionally replace RegEdit | X-02 | — |
| Connect to a remote registry | REM-01 | 2 |
| View open key handles | X-05 (SEC-07 instead) | 3 |
| Dark mode†; portable mode† | UX-01; UX-09 | 1 |

### Registrar Registry Manager: Home (free for home use) / Professional (paid)

Edition marks come from the vendor's comparison chart: H = Home, P = Professional.

| Feature (as published) | Ed. | HiveMind | Phase |
|---|---|---|---|
| Address bar; registry tree and view; back/forward; navigational history lists | H P | NAV-01, NAV-03, NAV-05 | 1 |
| Complete expansion of a key and all sub-branches | H P | UX-02 (Num \*, capped) | 1 |
| Navigational display of permissions and ownership | H P | VIEW-05 | 1 |
| Multiple registry views (document interface); configurable tabbing | H P / P | NAV-02 | 1 |
| Remote registry editing in separate windows | P | REM-01 | 2 |
| Navigate between registry and tool windows from the main menu | H P | NAV-11 (command palette) | 1 |
| Create, delete and rename keys and values | H P | EDIT-04 | 1 |
| Copy and move keys/values; drag and drop; clipboard cut/copy/paste of keys | H P | EDIT-05 | 1 |
| Clipboard support for key names | H P | NAV-07 | 1 |
| Value editors for `REG_SZ`, `REG_DWORD`, `REG_MULTI_SZ`, `REG_BINARY` | H P | EDIT-02 | 1 |
| Change the type of existing values; custom value types | H P | EDIT-01 | 1 |
| Import and export keys; REGEDIT4, REGEDIT5 and hive files | H P | IO-01, SAFE-05; OFF-06 | 1; 2 |
| Import and export value data to a file | H P | EDIT-09 | 2 |
| Advanced key property pages; key statistics (subkeys, amount of data) | H P | VIEW-04 | 1 |
| Multithreaded background search; results view | P | SRCH-01, SRCH-05 | 1 |
| Search for text | H P | SRCH-02 | 1 |
| Search for binary data; regular expressions | P | SRCH-02 | 1 |
| Data size, value type and last-write filters | P | SRCH-04 | 1 |
| Replace/rename matching keys, values and data; delete matches | P | SRCH-06 | 1 |
| Export matches as `.reg` or text | P | SRCH-06, IO-01, IO-02 | 1 |
| Search for hidden keys (features page) | — | SRCH-03 | 1 |
| Monitor registry activity by the system and applications | H P | REC-02 | 3 |
| Advanced process filtering for the monitor | P | REC-02 | 3 |
| Advanced registry backup and restore | H P | SAFE-08, SAFE-09 | 2 |
| Keep track of all changes; undo individual changes | P | SAFE-03, SAFE-04, ENT-01 | 1 |
| Key and value bookmarks | H P | NAV-06 | 1 |
| Descriptions for keys and values in all views; internet links | H P | GUIDE-07; GUIDE-08 (pack `refs`) | 1; 2 |
| Standard bookmarks describing hundreds of keys | H P | GUIDE-06 (cited starter pack) | 1 |
| Standard set of thousands of tweaks | H P | GUIDE-09 (curated, cited recipes); X-07 | 2 |
| Standard descriptions of account names | H P | GUIDE-02 (SIDs shown as accounts) | 1 |
| Bookmark manager with categories; optional coloring | H P | NAV-06 | 1 |
| Search through bookmarks and descriptions | P | SRCH-10 | 2 |
| Permission, auditing and ownership editors | H P | SEC-01; SEC-02 | 1; 3 |
| Display key security while browsing | H P | VIEW-05 | 1 |
| Edit through the privileged SYSTEM account | P | X-01 | — |
| "Tough delete" | P | X-09 (EDIT-04 force delete instead) | 1 |
| Registry defragmenter | H P | X-03 | — |
| CLSID utility | P | GUIDE-02 (COM provider); GUIDE-10 | 1; 2 |
| File reference editor | P | GUIDE-10 (find and fix file references, staged) | 2 |
| Registry compare | P | CMP-01 | 2 |
| `reg://` URLs, registry shortcuts, shell extensions | H P | NAV-12, UX-10 | 2 |
| Registry file editing in separate windows (`.reg` and hive files) | P | OFF-01, OFF-03 | 2 |
| Repair the registry of a non-running Windows installation | P | OFF-05 (view), OFF-07 (repair) | 2; 5 |
| Include/exclude filters in all views | P | VIEW-02b | 2 |
| Optional read-only mode | P | SAFE-07 | 1 |
| Key abbreviations; fonts; tooltips; grid lines; navigation behaviour | P | VIEW-06 | 1 |
| Remember the last key, tool windows and views | P | NAV-13 | 1 |
| Configurable prompts | P | SAFE-06 | 1 |
| Configurable data representation | P | VIEW-03 | 1 |
| Command-line edition (features page) | — | DEV-01 | 2 |
| Separate 32- and 64-bit editions | P | n/a (native x64 and ARM64 builds, UX-09) | 1 |

### O&O RegEditor 16.1 (freeware)

The vendor lists 124 features in 13 groups. Grouped mapping:

| Feature group (as published) | HiveMind | Phase |
|---|---|---|
| All common hives and value types; editing large binary values; correct `REG_MULTI_SZ` handling | EDIT-01, EDIT-02 | 1 |
| Light/dark design following Windows; high-DPI support | UX-01 | 1 |
| Regex; whole words; live search as you type; search a subsection or whole hives; export results as `.reg` or tab-delimited `.txt` | SRCH-02, VIEW-02, SRCH-04, IO-01, IO-02 | 1 |
| Copy whole keys with subkeys; duplicate key structures | EDIT-05 | 1 |
| Favorites: import, export, share, print | NAV-06 | 1 |
| Address bar with a history of recent paths | NAV-03 | 1 |
| Export the entire registry as XML | IO-02b | 2 |
| Quick `.reg` backups before changes | SAFE-01 (automatic before-images), IO-01 | 1 |
| Confirmation prompts | SAFE-06 (risk-based) | 1 |
| Keyboard-friendly operation; context menus; saved window layout | UX-02, NAV-13 | 1 |
| Portable use; settings stored in a file, not the registry | UX-09, UX-11 | 1 |
| Windows 11 on x64 and ARM | UX-09 | 1 |
| German and English | UX-07 | 5 |

### Regedix 2.0 (free)

| Feature (as published) | HiveMind | Phase |
|---|---|---|
| High-performance regex search | SRCH-01, SRCH-02 | 1 |
| Import/export with progress | UX-06 | 1 |
| Browser-like tabs and history | NAV-02, NAV-05 | 1 |
| Address-bar navigation | NAV-03 | 1 |
| Favorite keys and values | NAV-06 | 1 |
| Dark mode; icons, descriptions and timestamps | UX-01, VIEW-08 | 1 |
| Localized string resolution | VIEW-03 | 1 |
| Context actions: open the parent, jump to GUID roots | NAV-05; GUIDE-10 | 1; 2 |
| Cut, copy and paste keys and values | EDIT-05 | 1 |

### RegAlyzer (Safer-Networking; Plus subscription; RegAlyzer 2 is a release candidate)

| Feature (as published) | HiveMind | Phase |
|---|---|---|
| Multiple browsers (tabs) | NAV-02 | 1 |
| Background search; regular expressions | SRCH-01, SRCH-02 | 1 |
| Live bookmarks; hierarchical bookmark manager with live preview | NAV-06, NAV-06b | 1, 2 |
| Disassembler | SEC-04 (embedded payload markers); OFF-08 | 3; 5 |
| Value interpretation; constant names for values | EDIT-10; EDIT-03 | 2; 1 |
| Value documentation | GUIDE-01, GUIDE-06 | 1 |
| Tweak database | GUIDE-09 | 2 |
| Hive snapshots; track changes by comparing to snapshots | SAFE-08, CMP-01 | 2 |
| Hive Manager | OFF-04, OFF-04b | 1, 2 |

### RegCool † (freeware)

| Feature (third-party listings) | HiveMind | Phase |
|---|---|---|
| Multiple undo/redo; history saved to disk across restarts | SAFE-03, SAFE-04 | 1 |
| Cut, copy, paste; drag and drop | EDIT-05 | 1 |
| Search and replace | SRCH-01 to SRCH-06 | 1 |
| Compare registries (keys or whole hives) | CMP-01 | 2 |
| Registry snapshots and comparison | SAFE-08, CMP-03 | 2 |
| Backup and restore | SAFE-09 | 2 |
| Registry defragmentation | X-03 | — |
| Open secure keys when running as administrator | PRIV-02 | 1 |

### Registry Workshop † (paid; last release 5.1.0, Sep 2022; appears dormant)

| Feature (third-party listings) | HiveMind | Phase |
|---|---|---|
| Multi-level undo; undo history saved and reloaded across restarts | SAFE-03, SAFE-04 | 1 |
| Tabs, including network registries | NAV-02, REM-01 | 1, 2 |
| `.reg` and `.pol` files edited in tabs | OFF-01, OFF-02 | 2 |
| Searching, comparing, maintenance (details unverified) | SRCH-*, CMP-01 | 1, 2 |
| Defragment and backup/restore (removed on Windows 10+ in 5.1.0) | X-03; SAFE-08, SAFE-09 | —; 2 |

## 3. Specialist utilities

### SetACL Studio (freeware)

| Feature (as published) | HiveMind | Phase |
|---|---|---|
| One tool for permissions on files, registry, services, printers, shares and WMI | SEC-02 (registry only; other object types are out of scope) | 3 |
| Local and multiple remote computers at once | REM-01, REM-02 | 2, 4 |
| Full replacement for the Windows ACL editor: owner, DACL, SACL, inheritance on/off, reset child objects | SEC-01, SEC-02 | 1, 3 |
| Unlimited multi-level undo | SAFE-03 (permission changes are changesets) | 1 |
| "Bypassing security" with admin rights; create/delete/rename anywhere | PRIV-02 | 1 |
| Long paths | §7.3 (no MAX_PATH assumptions) | 1 |

### NirSoft

| Tool and feature | HiveMind | Phase |
|---|---|---|
| RegScanner: all matches in a list; time-based filters; export as `.reg`, CSV, HTML or XML; no admin needed | SRCH-01, SRCH-04, IO-01, IO-02, IO-02b | 1, 2 |
| RegistryChangesView: compare current registry, saved snapshots and VSS shadow copies | CMP-01, SAFE-08, SAFE-11 | 2, 3 |
| RegistryChangesView: warns that snapshots contain secrets | §7.9 encryption, §8.4 | 0–2 |
| OfflineRegistryFinder / OfflineRegistryView: search and view offline hives | OFF-03, SRCH-12 | 2 |
| RegFromApp: record one app's changes as `.reg` | REC-03 | 3 |

### Regshot (open source)

| Feature | HiveMind | Phase |
|---|---|---|
| Before/after snapshots with a text/HTML comparison report | CMP-03, SAFE-08, IO-05 | 2 |

### Sysinternals (Microsoft)

| Tool and feature | HiveMind | Phase |
|---|---|---|
| Process Monitor: live registry trace with process, stacks and filters | REC-02; REC-06 (import its CSV/XML) | 3 |
| Process Monitor: boot logging | REC-08 | 5 |
| RegDelNull: find and delete keys with NUL characters in their names | SRCH-03, EDIT-04 | 1 |
| RegJump: open regedit at a path | NAV-04 | 1 |
| AccessChk: effective permissions | SEC-02, SEC-03 | 3 |
| Autoruns: autostart locations | SEC-05 | 3 |
| RU (Registry Usage): space usage | UX-08 | 3 |

## 4. Forensic tools

### Registry Explorer and RECmd (Eric Zimmerman)

| Feature | HiveMind | Phase |
|---|---|---|
| Offline, read-only analysis of multiple hives | OFF-03, OFF-08 | 2, 5 |
| Recovery of deleted keys and values; slack space | OFF-08 | 5 |
| Transaction log (`.LOG1`/`.LOG2`) handling | §7.7 (kernel replays logs on the copy); OFF-08 | 2, 5 |
| Plugins that decode binary artifacts | EDIT-10; OFF-08; DEV-04 | 2; 5; 4 |
| Batch extraction (RECmd) | DEV-01, OFF-08 | 2, 5 |

### RegRipper

| Feature | HiveMind | Phase |
|---|---|---|
| Plugin-based extraction reports from offline hives | OFF-08 | 5 |

## 5. Microsoft and adjacent tools

| Tool and feature | HiveMind | Phase |
|---|---|---|
| PowerToys Registry Preview: visualize and edit `.reg` files before writing; can be the default `.reg` handler (10 MB limit) | SAFE-05, OFF-01 (no size limit) | 1, 2 |
| Windows Admin Center: browse, add, change and delete values on remote servers | REM-01, REM-02 | 2, 4 |
| Policy Plus: Group Policy on every edition; find policies by registry key or value | GUIDE-03 (reverse lookup), OFF-02 (`.pol`) | 1, 2 |
| WinUI 3 Registry Editor sample: create, rename, delete keys; add values; Ctrl+F; no permissions or `.reg` import | Baseline only; confirms the Fluent direction (UX-01) | 1 |

## 6. Coverage summary

**Coverage:** every audited capability maps to a phased HiveMind feature, an improved equivalent, or
a deliberate exclusion with a replacement (X-01 to X-10).

**HiveMind originals,** offered by none of the audited tools:

| ID | Feature |
|---|---|
| SAFE-01, SAFE-02 | Staged changesets with conflict detection and all-or-nothing apply with compensation |
| ENT-01, ENT-02 | Encrypted, hash-chained audit journals (per user and machine), with Event Log anchors |
| PRIV-01, PRIV-04, PRIV-05 | Unelevated UI with a per-change, signed, verified broker; "why was this denied?" explanations |
| GUIDE-03, GUIDE-04, GUIDE-05 | In-place ADMX explanations, ghost settings and policy-enforcement warnings inside a registry editor |
| GUIDE-08, GUIDE-11 | Shareable, signed team knowledge packs; a risk model that drives friction |
| SRCH-07 to SRCH-11, SRCH-13 | Persistent encrypted index, query language, smart folders, search by meaning, decoded-data search, fuzzy go-to |
| EDIT-07 | Edit a live subtree as text, with a diff to staging |
| REC-04, REC-05, REC-06 | Capture → changeset/revert; settings discovery from your own captures; Process Monitor import |
| SAFE-11 | Per-key time travel across the journal, snapshots and VSS |
| REM-03 to REM-06, CMP-04 | Fleet query/compare/apply in waves; every-user apply; baselines |
| IO-03, IO-04, DEV-02, DEV-03 | Deployable code exports with reverts; PowerShell module; "show equivalent command" |
| SEC-04 (integrated), SEC-07 | Security-lens overlays on the tree; ETW-based "who touches this key" |
| ENT-03 to ENT-05 | HiveMind's own Group Policy templates; group-protected journals; a dedicated audit channel |
| OFF-09 to OFF-11 | MSIX-private hives, cluster-hive awareness, VHD(X)/FSLogix profile hives |
| GUIDE-12 | Grounded, opt-in AI explanations that can only stage changes |

## Sources

- Registry Finder: <https://registry-finder.com/> · <https://registry-finder.com/version_history.html>
- RegKit: <https://github.com/nohuto/regkit>
- TotalRegistry: <https://github.com/zodiacon/TotalRegistry>
- Registrar Registry Manager: <https://resplendence.com/registrar_compchart> · <https://resplendence.com/registrar_features>
- O&O RegEditor: <https://www.oo-software.com/en/ooregeditor/features>
- Regedix: <https://sourceforge.net/app/regedix/>
- RegAlyzer: <https://www.safer-networking.org/products/regalyzer/>
- SetACL Studio: <https://helgeklein.com/setacl-studio/> · <https://helgeklein.com/setacl-studio/feature-set/>
- RegCool †: <https://www.techspot.com/downloads/466-regcool.html> · <https://www.softpedia.com/get/Tweak/Registry-Tweak/RegCool.shtml>
- Registry Workshop †: <https://www.softpedia.com/progChangelog/Registry-Workshop-Changelog-12688.html> · <https://www.snapfiles.com/get/regworkshop.html>
- NirSoft: <https://www.nirsoft.net/utils/regscanner.html> · <https://www.nirsoft.net/utils/registry_changes_view.html> · <https://www.nirsoft.net/utils/offline_registry_finder.html>
- Registry Explorer plugins: <https://github.com/AndrewRathbun/RegistryPlugins> · SANS webcast: <https://sans.org/webcasts/exploring-registry-explorer-106435>
- PowerToys Registry Preview: <https://learn.microsoft.com/windows/powertoys/registry-preview>
- Windows Admin Center: <https://learn.microsoft.com/windows-server/manage/windows-admin-center/use/manage-servers>
- Policy Plus (find by registry): <https://fleexlab.blogspot.com/2016/08/policy-plus-find-by-registry.html>
- WinUI 3 Registry Editor sample: <https://www.windowslatest.com/2026/11/08/microsoft-built-a-native-winui-registry-editor-that-makes-windows-11s-regedit-look-like-windows-95-and-i-tried-it/> · <https://windowsreport.com/?p=1515114>
- Offline Registry Library: <https://learn.microsoft.com/windows/win32/devnotes/offline-registry-library-portal>
- `RegLoadAppKey`: <https://learn.microsoft.com/windows/win32/api/winreg/nf-winreg-regloadappkeyw>
- SignPath Foundation (open-source code signing): <https://signpath.org/> · <https://signpath.org/terms> · <https://about.signpath.io/product/open-source>
