[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$BuildOnly,
    [switch]$PromptForApiKey,
    [string]$BuildOutputRoot
)

$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot '.local/App'))
$nodeLockPath = Join-Path $sourceRoot 'runtime/generated-tool-node-lock.json'
$capabilityPath = Join-Path $sourceRoot 'docs/release/MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA_20261004.json'
$hostProject = Join-Path $sourceRoot 'host/NeoBabylon.Host/NeoBabylon.Host.csproj'
$adapterProject = Join-Path $sourceRoot 'host/NeoBabylon.GeneratedToolMcp/NeoBabylon.GeneratedToolMcp.csproj'
$nvidiaAdapterProject = Join-Path $sourceRoot 'host/NeoBabylon.NvidiaAdapter/NeoBabylon.NvidiaAdapter.csproj'
$uiRoot = Join-Path $sourceRoot 'ui/diagnostic'
$buildRoot = if ([string]::IsNullOrWhiteSpace($BuildOutputRoot)) { Join-Path $appRoot 'Build' } else { [IO.Path]::GetFullPath($BuildOutputRoot) }
$hostOutputRoot = Join-Path $buildRoot 'bin/NeoBabylon.Host/Release'
$adapterOutputRoot = Join-Path $buildRoot 'bin/NeoBabylon.GeneratedToolMcp/Release'
$nvidiaAdapterOutputRoot = Join-Path $buildRoot 'bin/NeoBabylon.NvidiaAdapter/release'
$adapterRoot = Join-Path $appRoot 'Adapters'
$credentialRoot = Join-Path $appRoot 'Data/Credentials'
Import-Module (Join-Path $sourceRoot 'scripts/DevelopmentCredentialStore.psm1') -Force
if ($BuildOnly) { $Build = $true }

function Find-BuiltExecutable([string]$SearchRoot, [string]$Name) {
    if (-not [IO.Directory]::Exists($SearchRoot)) { return $null }
    $matches = @(Get-ChildItem -LiteralPath $SearchRoot -Filter $Name -File -Recurse)
    if ($matches.Count -gt 1) { throw "More than one $Name was found under the isolated build root." }
    if ($matches.Count -eq 0) { return $null }
    return $matches[0].FullName
}

function Require-Files([string]$Root, [string[]]$Names, [string]$Label) {
    foreach ($name in $Names) {
        $path = Join-Path $Root $name
        if (-not [IO.File]::Exists($path)) { throw "$Label output is missing: $path. Run this script with -Build." }
    }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash.ToLowerInvariant()
}

function Assert-NoReparseComponents([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $cursor = [IO.Path]::GetPathRoot($fullPath)
    $components = $fullPath.Substring($cursor.Length).Split(
        [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
        [StringSplitOptions]::RemoveEmptyEntries)
    foreach ($component in $components) {
        $cursor = [IO.Path]::Combine($cursor, $component)
        try {
            $attributes = [IO.File]::GetAttributes($cursor)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Pinned Node runtime path contains a reparse point: $cursor"
            }
        }
        catch [IO.FileNotFoundException] { }
        catch [IO.DirectoryNotFoundException] { }
    }
}

function Read-NodeRuntimePin {
    if (-not [IO.File]::Exists($nodeLockPath)) { throw "Product-owned Node runtime lock is missing: $nodeLockPath" }
    try { $pin = Get-Content -LiteralPath $nodeLockPath -Raw | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "Product-owned Node runtime lock is invalid JSON: $nodeLockPath" }
    if ($pin.schemaVersion -ne 1 -or $pin.kind -cne 'NodeJS' -or $pin.version -cnotmatch '^v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$' -or $pin.sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw 'Product-owned Node runtime lock has an unsupported schema, kind, version, or SHA-256.'
    }
    $expectedRelativePath = "Runtimes/Node/$($pin.version)/node.exe"
    if ($pin.applicationRelativePath -cne $expectedRelativePath) {
        throw 'Product-owned Node runtime lock path must be the fixed versioned app-private node.exe path.'
    }
    return $pin
}

function Ensure-PinnedNodeRuntime([object]$Pin, [switch]$CopyInstalledNode) {
    $nodeRelativePath = $Pin.applicationRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $destination = [IO.Path]::GetFullPath([IO.Path]::Combine($appRoot, $nodeRelativePath))
    $appRootPrefix = [IO.Path]::TrimEndingDirectorySeparator($appRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $destination.StartsWith($appRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Pinned Node runtime destination escapes the application root.'
    }
    Assert-NoReparseComponents $destination

    if ([IO.File]::Exists($destination)) {
        $existingHash = Get-Sha256 $destination
        if ($existingHash -cne $Pin.sha256) {
            throw "Existing app-private Node destination differs from the product source pin; refusing overwrite: $destination"
        }
        return $destination
    }
    if ([IO.Directory]::Exists($destination)) {
        throw "App-private Node destination exists as a directory; refusing to replace it: $destination"
    }
    if (-not $CopyInstalledNode) {
        throw "Pinned app-private Node runtime is missing: $destination. Run this script with -Build."
    }

    $installedNode = Get-Command node.exe -ErrorAction Stop
    if ($installedNode.CommandType -ne [Management.Automation.CommandTypes]::Application) {
        throw 'Get-Command node.exe did not resolve to an installed Node application.'
    }
    $installedNodePath = $installedNode.Source
    if ([string]::IsNullOrWhiteSpace($installedNodePath) -or -not [IO.File]::Exists($installedNodePath)) {
        throw 'Get-Command node.exe did not resolve to an installed Node executable.'
    }
    $installedVersion = (& $installedNodePath --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $installedVersion -cne $Pin.version) {
        throw "Installed Node version does not match the product source pin ($($Pin.version)); no runtime was copied."
    }
    $installedHash = Get-Sha256 $installedNodePath
    if ($installedHash -cne $Pin.sha256) {
        throw 'Installed Node executable SHA-256 does not match the product source pin; no runtime was copied.'
    }

    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    Assert-NoReparseComponents $destination
    try { [IO.File]::Copy($installedNodePath, $destination, $false) }
    catch [IO.IOException] {
        if (-not [IO.File]::Exists($destination)) { throw }
        $racedHash = Get-Sha256 $destination
        if ($racedHash -cne $Pin.sha256) {
            throw "App-private Node destination appeared with an unpinned hash; refusing overwrite: $destination"
        }
    }
    Assert-NoReparseComponents $destination
    if ((Get-Sha256 $destination) -cne $Pin.sha256) {
        throw "Copied app-private Node executable failed SHA-256 readback: $destination"
    }
    return $destination
}

function Invoke-CheckedCommand([string]$Command, [string[]]$Arguments, [string]$WorkingDirectory) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Command
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    Remove-DevelopmentCredentialEnvironment $startInfo

    $process = $null
    try {
        $process = [Diagnostics.Process]::Start($startInfo)
        if ($null -eq $process) { throw "Build command could not be started: $Command" }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($stdout.Length -gt 0) { [Console]::Out.Write($stdout) }
        if ($stderr.Length -gt 0) { [Console]::Error.Write($stderr) }
        if ($process.ExitCode -ne 0) { throw "Build command failed with exit code $($process.ExitCode): $Command $($Arguments -join ' ')" }
    }
    finally {
        if ($null -ne $process) { $process.Dispose() }
    }
}

if (-not [IO.File]::Exists($capabilityPath)) { throw "The explicitly selected capability record is missing: $capabilityPath" }
$nodePin = Read-NodeRuntimePin
$capability = Get-Content -LiteralPath $capabilityPath -Raw | ConvertFrom-Json
if ($capability.providerId -cne 'openrouter' -or $capability.modelIdentifier -cne 'stealth/space-bunny-alpha') {
    throw 'The selected capability does not match OpenRouter stealth/space-bunny-alpha; no model substitution is permitted.'
}
if (-not [IO.Directory]::Exists((Join-Path $uiRoot 'node_modules'))) {
    throw "Cached UI dependencies are unavailable at $(Join-Path $uiRoot 'node_modules'); this development launcher will not install packages."
}

if ($Build) {
    $nodeCommand = (Get-Command node.exe -ErrorAction Stop).Source
    $npmCommand = (Get-Command npm.cmd -ErrorAction Stop).Source
    $npmCli = Join-Path (Split-Path -Parent $npmCommand) 'node_modules/npm/bin/npm-cli.js'
    if (-not [IO.File]::Exists($npmCli)) { throw "Cached npm CLI is unavailable beside Node.js: $npmCli" }
    $dotnetCommand = (Get-Command dotnet -ErrorAction Stop).Source

    Invoke-CheckedCommand $nodeCommand @($npmCli, 'run', 'typecheck') $uiRoot
    Invoke-CheckedCommand $nodeCommand @($npmCli, 'run', 'build') $uiRoot

    Invoke-CheckedCommand $dotnetCommand @(
        'build', $hostProject, '--configuration', 'Release', '--artifacts-path', $buildRoot, '--verbosity', 'minimal'
    ) $sourceRoot
    Invoke-CheckedCommand $dotnetCommand @(
        'build', $adapterProject, '--configuration', 'Release', '--artifacts-path', $buildRoot, '--verbosity', 'minimal'
    ) $sourceRoot
    $disabledAdapterExecutable = Find-BuiltExecutable $adapterOutputRoot 'NeoBabylon.GeneratedToolMcp.exe'
    if ($null -eq $disabledAdapterExecutable) { throw "Disabled MCP adapter build output is missing below $adapterOutputRoot." }
    $disabledAdapterBuildDirectory = Split-Path -Parent $disabledAdapterExecutable
    New-Item -ItemType Directory -Force -Path $adapterRoot | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $disabledAdapterBuildDirectory -File) {
        Copy-Item -LiteralPath $file.FullName -Destination $adapterRoot -Force
    }
    if (-not [IO.File]::Exists($nvidiaAdapterProject)) { throw "Approved private NVIDIA adapter project is missing: $nvidiaAdapterProject" }
    New-Item -ItemType Directory -Force -Path $nvidiaAdapterOutputRoot | Out-Null
    Invoke-CheckedCommand $dotnetCommand @(
        'publish', $nvidiaAdapterProject, '--configuration', 'Release', '--output', $nvidiaAdapterOutputRoot,
        '-p:UseAppHost=true', '--verbosity', 'minimal'
    ) $sourceRoot

    $null = Ensure-PinnedNodeRuntime $nodePin -CopyInstalledNode
}

$nodeRuntimePath = Ensure-PinnedNodeRuntime $nodePin

$hostExecutable = Find-BuiltExecutable $hostOutputRoot 'NeoBabylon.Host.exe'
if ($null -eq $hostExecutable) { throw "WPF host build output is missing below $hostOutputRoot. Run this script with -Build." }
$hostOutputDirectory = Split-Path -Parent $hostExecutable
Require-Files $hostOutputDirectory @('NeoBabylon.Host.dll', 'NeoBabylon.Host.deps.json', 'NeoBabylon.Host.runtimeconfig.json') 'WPF host'
$adapterExecutable = Find-BuiltExecutable $adapterOutputRoot 'NeoBabylon.GeneratedToolMcp.exe'
if ($null -eq $adapterExecutable) { throw "Disabled MCP adapter build output is missing below $adapterOutputRoot. Run this script with -Build." }
Require-Files (Split-Path -Parent $adapterExecutable) @('NeoBabylon.GeneratedToolMcp.exe', 'NeoBabylon.GeneratedToolMcp.dll', 'NeoBabylon.GeneratedToolMcp.deps.json', 'NeoBabylon.GeneratedToolMcp.runtimeconfig.json') 'Disabled MCP adapter'
$nvidiaAdapterExecutable = Find-BuiltExecutable $nvidiaAdapterOutputRoot 'NeoBabylon.NvidiaAdapter.exe'
if ($null -eq $nvidiaAdapterExecutable) { throw "Private NVIDIA adapter build output is missing below $nvidiaAdapterOutputRoot. Run this script with -Build." }
Require-Files $nvidiaAdapterOutputRoot @('NeoBabylon.NvidiaAdapter.exe', 'NeoBabylon.NvidiaAdapter.dll', 'NeoBabylon.NvidiaAdapter.deps.json', 'NeoBabylon.NvidiaAdapter.runtimeconfig.json') 'Private NVIDIA adapter'

$uiDist = Join-Path $uiRoot 'dist'
$uiIndex = Join-Path $uiDist 'index.html'
if (-not [IO.File]::Exists($uiIndex)) { throw "Built UI is missing: $uiIndex. Run this script with -Build." }
if (@(Get-ChildItem -LiteralPath $uiDist -Filter '*.map' -File -Recurse).Count -eq 0) {
    throw "Source-mapped development UI output is missing below $uiDist; rebuild with the repository Vite configuration."
}

Write-Host "Source root: $sourceRoot"
Write-Host "Application root: $appRoot"
Write-Host "Capability: $capabilityPath"
Write-Host 'Selected model: OpenRouter / stealth/space-bunny-alpha (no provider or route qualification is performed by this launcher).'
Write-Host "Host binary: $hostExecutable"
Write-Host "Disabled MCP adapter: $adapterExecutable"
Write-Host "Private NVIDIA adapter: $nvidiaAdapterExecutable (local transport only; no provider request made by launcher)"
Write-Host "Pinned app-private Node: $nodeRuntimePath (version $($nodePin.version), SHA-256 $($nodePin.sha256))"
Write-Host "Development UI: $uiDist (source maps enabled; development-only output)"
if ($BuildOnly) {
    Write-Host 'Build-only mode: no application process was started, no credential was requested, and no provider request was made.'
    return
}

$hasInheritedOpenRouterKey = -not [string]::IsNullOrWhiteSpace(
    [Environment]::GetEnvironmentVariable('OPENROUTER_API_KEY', [EnvironmentVariableTarget]::Process))
$hasInheritedNvidiaKey = -not [string]::IsNullOrWhiteSpace(
    [Environment]::GetEnvironmentVariable('NVIDIA_API_KEY', [EnvironmentVariableTarget]::Process))
$openRouterKey = if ($hasInheritedOpenRouterKey) {
    [Environment]::GetEnvironmentVariable('OPENROUTER_API_KEY', [EnvironmentVariableTarget]::Process)
} else { Read-DevelopmentCredential -Provider 'openrouter' -CredentialRoot $credentialRoot }
$nvidiaKey = if ($hasInheritedNvidiaKey) {
    [Environment]::GetEnvironmentVariable('NVIDIA_API_KEY', [EnvironmentVariableTarget]::Process)
} else { Read-DevelopmentCredential -Provider 'nvidia' -CredentialRoot $credentialRoot }
$promptedKey = $null
if (-not $hasInheritedOpenRouterKey -and [string]::IsNullOrWhiteSpace($openRouterKey) -and $PromptForApiKey) {
    $secureKey = Read-Host -AsSecureString -Prompt 'OpenRouter API key for this launched process (input is not saved)'
    try {
        if ($secureKey.Length -gt 0) {
            $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
            try { $promptedKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer) }
            finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer) }
        }
    }
    finally {
        $secureKey.Dispose()
    }
}

if ($hasInheritedOpenRouterKey) {
    Write-Host 'OpenRouter API key: inherited process credential present; value withheld. Live inference readiness is not verified.'
}
elseif (-not [string]::IsNullOrWhiteSpace($openRouterKey)) {
    Write-Host 'OpenRouter API key: app-private current-user protected credential present; value withheld. Live inference readiness is not verified.'
}
elseif (-not [string]::IsNullOrWhiteSpace($promptedKey)) {
    Write-Host 'OpenRouter API key: supplied securely for the new process only; value withheld. Live inference readiness is not verified.'
}
else {
    Write-Host 'OpenRouter API key: absent. The host will launch for diagnostics; live inference is not ready.'
}
if ($hasInheritedNvidiaKey) {
    Write-Host 'NVIDIA API key: inherited process credential present; value withheld. Provider readiness is not verified.'
}
elseif (-not [string]::IsNullOrWhiteSpace($nvidiaKey)) {
    Write-Host 'NVIDIA API key: app-private current-user protected credential present; value withheld. Provider readiness is not verified.'
}
else {
    Write-Host 'NVIDIA API key: absent. NVIDIA requests will remain unavailable.'
}

$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $hostExecutable
$startInfo.WorkingDirectory = $sourceRoot
$startInfo.UseShellExecute = $false
$startInfo.Environment['NEOBABYLON_SOURCE_ROOT'] = $sourceRoot
$startInfo.Environment['NEOBABYLON_APPLICATION_ROOT'] = $appRoot
$startInfo.Environment['NEOBABYLON_MODEL_CAPABILITY_PATH'] = $capabilityPath
if (-not $hasInheritedOpenRouterKey) {
    $launchOpenRouterKey = if (-not [string]::IsNullOrWhiteSpace($promptedKey)) { $promptedKey } else { $openRouterKey }
    if ([string]::IsNullOrWhiteSpace($launchOpenRouterKey)) { $startInfo.Environment.Remove('OPENROUTER_API_KEY') | Out-Null }
    else { $startInfo.Environment['OPENROUTER_API_KEY'] = $launchOpenRouterKey }
}
if (-not $hasInheritedNvidiaKey) {
    if ([string]::IsNullOrWhiteSpace($nvidiaKey)) { $startInfo.Environment.Remove('NVIDIA_API_KEY') | Out-Null }
    else { $startInfo.Environment['NVIDIA_API_KEY'] = $nvidiaKey }
}

try {
    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) { throw 'The WPF development host could not be started.' }
    Write-Host "Development host started (PID $($process.Id)); no provider request was made by this script."
}
finally {
    if (-not $hasInheritedOpenRouterKey) {
        $startInfo.Environment.Remove('OPENROUTER_API_KEY') | Out-Null
        $promptedKey = $null
        $openRouterKey = $null
    }
    if (-not $hasInheritedNvidiaKey) {
        $startInfo.Environment.Remove('NVIDIA_API_KEY') | Out-Null
        $nvidiaKey = $null
    }
}
