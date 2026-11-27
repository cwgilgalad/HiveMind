# Code signing policy

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

Release signing will go through SignPath Foundation's free program for open-source projects.
SignPath hasn't approved the project yet, and no release has been signed. Until it does, there are
no binary releases, and developers sign local builds with their own test certificate.

## Roles

- Committers and reviewers: cwgilgalad
- Approvers: cwgilgalad

## Privacy statement

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

Examples of requested transfers are remote registry connections, opt-in update checks and opt-in AI
explanations. HiveMind sends no telemetry.

## What gets signed

- every first-party PE file (`.exe` and `.dll`) that HiveMind builds
- MSI and MSIX packages, once they exist
- PowerShell modules and scripts
- the release catalog (`HiveMind.cat`), which lists the SHA-256 hash of every file in a release
## How releases are signed

Signing will run in a GitHub Actions workflow, `release.yml`, that builds from checked-in source
and submits the build to SignPath. It uses two artifact configurations, `binaries` and `catalog`,
and the signing policy `release-signing`. The workflow is still to come.

## How to verify a download

Check a file's Authenticode signature in PowerShell. The signer should be SignPath Foundation, and
the status `Valid`:

```powershell
Get-AuthenticodeSignature .\HiveMind.exe | Format-List Status, SignerCertificate
```

The SignPath Foundation certificate is shared by every project in the program, so the signer alone
doesn't prove a file came from HiveMind. Also check that the file's hash is in the signed release
catalog, using `signtool.exe` from the Windows SDK:

```powershell
signtool verify /pa /c HiveMind.cat <file>
```
