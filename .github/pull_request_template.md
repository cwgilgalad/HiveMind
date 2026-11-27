## What this changes

<!-- A sentence or two: what changes and why. Link the issue if there is one. -->

## Checklist

- [ ] No new native imports, or each new import is listed in CsWin32's `NativeMethods.txt` and reviewed (HM0001 is clean).
- [ ] No process launches, token use, impersonation, injection, handle duplication or dynamic API resolution.
- [ ] No persistence (services, scheduled tasks, Run keys, IFEO, WMI subscriptions) and no telemetry or new network calls.
- [ ] Every new action that changes state, uses privilege, moves data or crosses a boundary writes a journal entry.
- [ ] SAM and SECURITY stay excluded, and there's no `RegSaveKey` of SAM, SECURITY or SYSTEM.
- [ ] New dependencies pass the license gate, and no proprietary binaries are added to release output. New license overrides are listed for owner review.
- [ ] Nothing is copied from RegKit, FluentRegEdit or closed-source tools (clean-room rule).
- [ ] The build has zero warnings.
- [ ] Tests cover the change, and Windows-only tests skip elsewhere.
- [ ] External contributors only: every commit carries `Signed-off-by:` (DCO).
