#Requires -Version 5.1
<#
.SYNOPSIS
    Checks that this machine can build HiveMind, then restores the repository's tools and packages.

.DESCRIPTION
    Required checks: Windows build 17763 (Windows 10 version 1809) or later, git, a .NET SDK that
    satisfies global.json, and the Windows SDK tools signtool.exe and makecat.exe.
    Optional checks, which only warn: the GitHub CLI (gh) and Visual Studio (found with vswhere).

    When every required check passes and -CheckOnly is not given, the script runs
    "dotnet tool restore" and "dotnet restore HiveMind.slnx".

    It prints a table of results and exits with 1 if anything required failed, otherwise 0.
    It runs on Windows PowerShell 5.1 and PowerShell 7.

.PARAMETER CheckOnly
    Run the checks only. Nothing is restored or installed.

.PARAMETER InstallDevCertificate
    After restoring, also run eng\signing\New-DevCertificate.ps1 -Scope CurrentUser, which creates or
    reuses the dev code-signing certificate and trusts it for the current user.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File eng\Setup-DevMachine.ps1 -CheckOnly
#>
[CmdletBinding(DefaultParameterSetName = 'Setup')]
param(
    [Parameter(ParameterSetName = 'CheckOnly')]
    [switch] $CheckOnly,

    [Parameter(ParameterSetName = 'Setup')]
    [switch] $InstallDevCertificate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$MinimumWindowsBuild = 17763
$DevCertificateScript = Join-Path $PSScriptRoot 'signing\New-DevCertificate.ps1'
$Results = New-Object 'System.Collections.Generic.List[object]'

function Add-Result {
    param(
        [Parameter(Mandatory)] [string] $Check,
        [Parameter(Mandatory)] [ValidateSet('OK', 'WARN', 'FAIL', 'SKIP')] [string] $Status,
        [string] $Detail = ''
    )
    $Results.Add([pscustomobject]@{ Check = $Check; Status = $Status; Detail = $Detail })
}

function Test-AnyFailure {
    return [bool]($Results | Where-Object { $_.Status -eq 'FAIL' })
}

# Runs a native program and returns its exit code and output lines. Error output is captured as
# text, so that Windows PowerShell 5.1 cannot turn it into a terminating error.
function Invoke-Tool {
    param(
        [Parameter(Mandatory)] [string] $FilePath,
        [string[]] $ArgumentList = @(),
        [switch] $Echo
    )
    $ErrorActionPreference = 'Continue'
    $lines = @(& $FilePath @ArgumentList 2>&1 | ForEach-Object {
            $line = "$_"
            if ($Echo) { Write-Host $line }
            $line
        })
    [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $lines }
}

function Test-WindowsBuild {
    $build = [Environment]::OSVersion.Version.Build
    if ($build -ge $MinimumWindowsBuild) {
        Add-Result -Check 'Windows build' -Status OK -Detail "$build"
    }
    else {
        Add-Result -Check 'Windows build' -Status FAIL -Detail "$build; HiveMind needs $MinimumWindowsBuild (Windows 10 version 1809) or later"
    }
}

function Test-Program {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [switch] $Optional
    )
    $check = if ($Optional) { "$Name (optional)" } else { $Name }
    $program = Get-Command -Name $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $program) {
        $status = if ($Optional) { 'WARN' } else { 'FAIL' }
        Add-Result -Check $check -Status $status -Detail 'not found on PATH'
        return
    }
    $run = Invoke-Tool -FilePath $program.Path -ArgumentList '--version'
    Add-Result -Check $check -Status OK -Detail ($run.Output | Select-Object -First 1)
}

# Inside the repository the dotnet host applies global.json, and "dotnet --version" fails when no
# installed SDK satisfies it. The script runs from the repository root, so this checks global.json.
function Test-DotnetSdk {
    $sdk = (Get-Content -Raw -LiteralPath (Join-Path $RepoRoot 'global.json') | ConvertFrom-Json).sdk
    $wanted = "global.json: $($sdk.version), rollForward $($sdk.rollForward)"
    if (-not (Get-Command -Name dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
        Add-Result -Check '.NET SDK' -Status FAIL -Detail "dotnet not found on PATH ($wanted)"
        return
    }
    $run = Invoke-Tool -FilePath dotnet -ArgumentList '--version'
    if ($run.ExitCode -eq 0) {
        Add-Result -Check '.NET SDK' -Status OK -Detail "$($run.Output | Select-Object -Last 1) ($wanted)"
    }
    else {
        Add-Result -Check '.NET SDK' -Status FAIL -Detail "no installed SDK satisfies $wanted"
    }
}

# The newest Windows Kits 10 version folder whose x64 folder has the tool. Older SDK folders can
# lack signtool.exe, so each one is checked. eng\signing\Find-WindowsSdkTool.ps1 will replace this
# search when dev signing is added.
function Find-WindowsKitsTool {
    param([Parameter(Mandatory)] [string] $Name)
    $kitsBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (-not (Test-Path -LiteralPath $kitsBin -PathType Container)) {
        return $null
    }
    $candidates = foreach ($folder in Get-ChildItem -LiteralPath $kitsBin -Directory) {
        $version = $null
        $tool = Join-Path $folder.FullName "x64\$Name"
        if ([version]::TryParse($folder.Name, [ref] $version) -and (Test-Path -LiteralPath $tool -PathType Leaf)) {
            [pscustomobject]@{ Version = $version; Path = $tool }
        }
    }
    return $candidates | Sort-Object -Property Version -Descending | Select-Object -First 1 -ExpandProperty Path
}

function Test-VisualStudio {
    $check = 'Visual Studio (optional)'
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
        Add-Result -Check $check -Status WARN -Detail 'not installed (vswhere.exe not found)'
        return
    }
    $run = Invoke-Tool -FilePath $vswhere -ArgumentList '-latest', '-products', '*', '-property', 'displayName'
    $name = $run.Output | Where-Object { $_ } | Select-Object -First 1
    if ($run.ExitCode -eq 0 -and $name) {
        Add-Result -Check $check -Status OK -Detail $name
    }
    else {
        Add-Result -Check $check -Status WARN -Detail 'vswhere.exe found no installation'
    }
}

function Invoke-DotnetStep {
    param([Parameter(Mandatory)] [string[]] $ArgumentList)
    $step = "dotnet $($ArgumentList -join ' ')"
    Write-Host "> $step"
    $run = Invoke-Tool -FilePath dotnet -ArgumentList $ArgumentList -Echo
    if ($run.ExitCode -eq 0) {
        Add-Result -Check $step -Status OK
    }
    else {
        Add-Result -Check $step -Status FAIL -Detail "exit code $($run.ExitCode); see the output above"
    }
}

function Install-DevCertificate {
    $check = 'dev certificate (CurrentUser)'
    try {
        $thumbprint = & $DevCertificateScript -Scope CurrentUser | Select-Object -Last 1
        Add-Result -Check $check -Status OK -Detail "thumbprint $thumbprint"
    }
    catch {
        Add-Result -Check $check -Status FAIL -Detail $_.Exception.Message
    }
}

if ($InstallDevCertificate -and -not (Test-Path -LiteralPath $DevCertificateScript -PathType Leaf)) {
    [Console]::Error.WriteLine('-InstallDevCertificate needs eng\signing\New-DevCertificate.ps1, which this checkout does not have yet. Nothing was checked or changed.')
    exit 1
}

Push-Location -LiteralPath $RepoRoot
try {
    Test-WindowsBuild
    Test-Program -Name git
    Test-DotnetSdk
    foreach ($tool in 'signtool.exe', 'makecat.exe') {
        $path = Find-WindowsKitsTool -Name $tool
        if ($path) {
            Add-Result -Check $tool -Status OK -Detail $path
        }
        else {
            Add-Result -Check $tool -Status FAIL -Detail 'not found in Windows Kits\10\bin\<version>\x64; install the Windows SDK'
        }
    }
    Test-Program -Name gh -Optional
    Test-VisualStudio

    if (-not $CheckOnly) {
        if (Test-AnyFailure) {
            Add-Result -Check 'restore' -Status SKIP -Detail 'fix the failed checks first'
        }
        else {
            Invoke-DotnetStep -ArgumentList 'tool', 'restore'
            Invoke-DotnetStep -ArgumentList 'restore', 'HiveMind.slnx'
            if ($InstallDevCertificate) {
                Install-DevCertificate
            }
        }
    }
}
finally {
    Pop-Location
}

$Results | Format-Table -Property Check, Status, Detail -AutoSize -Wrap | Out-Host
if (Test-AnyFailure) {
    exit 1
}
exit 0
