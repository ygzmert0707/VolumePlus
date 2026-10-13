[CmdletBinding()]
param(
    [switch]$VerifyRuntimePacks,
    [string]$AssetsPath,
    [string]$DependencyManifestPath,
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$rootPrefix = $projectRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

function Get-WorkspacePath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Expected a repository-relative path: $RelativePath"
    }
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $RelativePath))
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path leaves the repository: $RelativePath"
    }
    return $fullPath
}

function Assert-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file is missing: $Path" }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -eq 0 -or ($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw "Required file must be nonempty and must not be a link: $Path"
    }
}

function Assert-Hash([string]$Path, [string]$ExpectedHash) {
    Assert-File $Path
    if ($ExpectedHash -notmatch '^[a-fA-F0-9]{64}$') { throw "Invalid SHA-256 inventory value for $Path" }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ne $ExpectedHash) {
        throw "SHA-256 mismatch: $Path. Restore the preserved upstream file or review and update the inventory."
    }
}

$requiredDocuments = @('LICENSE', 'NOTICE.md', 'README.md', 'docs/LICENSING.md', 'docs/PRIVACY.md')
foreach ($document in $requiredDocuments) { Assert-File (Get-WorkspacePath $document) }
$inventoryPath = Get-WorkspacePath 'third-party/license-sources.json'
Assert-File $inventoryPath
$inventory = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
if ($inventory.schema_version -ne 1 -or @($inventory.files).Count -eq 0) {
    throw 'The third-party license inventory has an unsupported schema or no files.'
}

$runtimeVersion = [string]$inventory.runtime.version
$runtimeIdentifier = [string]$inventory.runtime.runtime_identifier
if ($runtimeVersion -notmatch '^\d+\.\d+\.\d+$' -or $runtimeIdentifier -ne 'win-x64') {
    throw 'The inventory must identify an exact runtime version and the win-x64 runtime identifier.'
}
[xml]$projectXml = Get-Content -LiteralPath (Get-WorkspacePath 'src/SesPlus/SesPlus.csproj') -Raw
$runtimePins = @($projectXml.SelectNodes('/Project/PropertyGroup/RuntimeFrameworkVersion'))
if ($runtimePins.Count -ne 1 -or $runtimePins[0].InnerText.Trim() -ne $runtimeVersion) {
    throw "SesPlus.csproj must pin RuntimeFrameworkVersion to the inventoried version $runtimeVersion."
}
$versionNodes = @($projectXml.SelectNodes('/Project/PropertyGroup/Version'))
if ($versionNodes.Count -ne 1 -or $versionNodes[0].InnerText.Trim() -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'SesPlus.csproj must declare one valid application Version.'
}
$appVersion = $versionNodes[0].InnerText.Trim()
$assemblyNameNodes = @($projectXml.SelectNodes('/Project/PropertyGroup/AssemblyName'))
if ($assemblyNameNodes.Count -ne 1) { throw 'Expected one AssemblyName in the application project.' }
$assemblyName = $assemblyNameNodes[0].InnerText.Trim()
if ($assemblyName -notmatch '^[A-Za-z][A-Za-z0-9._-]{0,127}$' -or $assemblyName.EndsWith('.') -or $assemblyName -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)') {
    throw 'AssemblyName must be a safe Windows file basename for packaging.'
}
$frameworkNodes = @($projectXml.SelectNodes('/Project/PropertyGroup/TargetFramework'))
if ($frameworkNodes.Count -ne 1) { throw 'Expected one TargetFramework in SesPlus.csproj.' }
$targetFramework = $frameworkNodes[0].InnerText.Trim()
if ($targetFramework -notmatch '^net\d+\.\d+-windows(?:\d+\.\d+(?:\.\d+)*)?$') {
    throw "Unsupported Windows target framework: $targetFramework"
}

$fileInventory = @{}
foreach ($entry in $inventory.files) {
    $relativePath = [string]$entry.path
    if ($relativePath -notmatch '^third-party/' -or $relativePath.Contains('\') -or $relativePath -match '(^|/)\.\.?(/|$)') {
        throw "Invalid third-party inventory path: $relativePath"
    }
    if ($fileInventory.ContainsKey($relativePath)) { throw "Duplicate inventory path: $relativePath" }
    if ([string]::IsNullOrWhiteSpace([string]$entry.source_url)) { throw "Missing source URL: $relativePath" }
    $fileInventory[$relativePath] = $entry
    Assert-Hash (Get-WorkspacePath $relativePath) ([string]$entry.sha256)
}
$requiredThirdPartyFiles = @(
    'third-party/dotnet/LICENSE.txt', 'third-party/dotnet/THIRD-PARTY-NOTICES.txt',
    'third-party/wpf/LICENSE.txt', 'third-party/wpf/THIRD-PARTY-NOTICES.txt',
    'third-party/winforms/LICENSE.txt', 'third-party/winforms/THIRD-PARTY-NOTICES.txt',
    'third-party/microsoft/DOTNET-LIBRARY-LICENSE.html', 'third-party/microsoft/DOTNET-LIBRARY-LICENSE.txt',
    'third-party/microsoft/WINDOWS-SDK-LICENSE.html', 'third-party/microsoft/WINDOWS-SDK-LICENSE.md',
    'third-party/microsoft/WINDOWS-SDK-LICENSE.txt', 'third-party/microsoft/license-information-windows.md'
)
foreach ($requiredPath in $requiredThirdPartyFiles) {
    if (-not $fileInventory.ContainsKey($requiredPath)) { throw "Missing license inventory entry: $requiredPath" }
}

if ($VerifyRuntimePacks) {
    if ([string]::IsNullOrWhiteSpace($AssetsPath)) { $AssetsPath = Get-WorkspacePath 'src/SesPlus/obj/project.assets.json' }
    if ([string]::IsNullOrWhiteSpace($DependencyManifestPath)) {
        $DependencyManifestPath = Get-WorkspacePath "src/SesPlus/obj/Release/$targetFramework/$runtimeIdentifier/$assemblyName.deps.json"
    }
    Assert-File $AssetsPath
    Assert-File $DependencyManifestPath
    $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json
    $dependencies = Get-Content -LiteralPath $DependencyManifestPath -Raw | ConvertFrom-Json
    $expectedProject = Get-WorkspacePath 'src/SesPlus/SesPlus.csproj'
    if ([System.IO.Path]::GetFullPath([string]$assets.project.restore.projectPath) -ne $expectedProject -or $assets.project.version -ne $appVersion) {
        throw 'The restore assets do not belong to the current application project and version. Publish again.'
    }
    $frameworkProperty = $assets.project.frameworks.PSObject.Properties | Where-Object { $_.Name -eq $targetFramework }
    if ($null -eq $frameworkProperty -or $null -eq $assets.project.runtimes.PSObject.Properties[$runtimeIdentifier]) {
        throw "The restore assets must target $targetFramework/$runtimeIdentifier."
    }
    $runtimeTarget = [string]$dependencies.runtimeTarget.name
    if (-not $runtimeTarget.EndsWith("/$runtimeIdentifier", [System.StringComparison]::Ordinal)) {
        throw "The dependency manifest must target $runtimeIdentifier."
    }
    $target = $dependencies.targets.PSObject.Properties[$runtimeTarget]
    if ($null -eq $target -or $null -eq $target.Value.PSObject.Properties["$assemblyName/$appVersion"]) {
        throw 'The dependency manifest is missing the current application version. Publish again.'
    }

    $packs = @(
        @{ Id = 'Microsoft.NETCore.App.Runtime.win-x64'; License = 'LICENSE.TXT'; VendoredLicense = 'third-party/dotnet/LICENSE.txt'; Notices = 'THIRD-PARTY-NOTICES.TXT'; VendoredNotices = 'third-party/dotnet/THIRD-PARTY-NOTICES.txt' },
        @{ Id = 'Microsoft.WindowsDesktop.App.Runtime.win-x64'; License = 'LICENSE'; VendoredLicense = 'third-party/wpf/LICENSE.txt'; Notices = $null; VendoredNotices = $null }
    )
    foreach ($pack in $packs) {
        $resolved = @($frameworkProperty.Value.downloadDependencies | Where-Object { $_.name -eq $pack.Id })
        if ($resolved.Count -ne 1 -or $resolved[0].version -ne "[$runtimeVersion, $runtimeVersion]") {
            throw "Restore resolved an unexpected version of $($pack.Id); expected $runtimeVersion exactly."
        }
        $runtimePackKey = "runtimepack.$($pack.Id)/$runtimeVersion"
        $packKeys = @($target.Value.PSObject.Properties | Where-Object { $_.Name.StartsWith("runtimepack.$($pack.Id)/", [System.StringComparison]::OrdinalIgnoreCase) })
        if ($packKeys.Count -ne 1 -or $packKeys[0].Name -ne $runtimePackKey -or $null -eq $dependencies.libraries.PSObject.Properties[$runtimePackKey]) {
            throw "Publish resolved an unexpected runtime pack for $($pack.Id); expected $runtimeVersion exactly."
        }
        $packageRelativePath = $pack.Id.ToLowerInvariant() + '/' + $runtimeVersion
        $packDirectory = $null
        foreach ($packageFolder in $assets.packageFolders.PSObject.Properties) {
            $candidate = Join-Path $packageFolder.Name $packageRelativePath
            if (Test-Path -LiteralPath $candidate -PathType Container) { $packDirectory = $candidate; break }
        }
        if ($null -eq $packDirectory) { throw "Resolved runtime package is missing: $packageRelativePath" }
        $nuspecPath = Join-Path $packDirectory ($pack.Id.ToLowerInvariant() + '.nuspec')
        Assert-File $nuspecPath
        [xml]$nuspec = Get-Content -LiteralPath $nuspecPath -Raw
        $metadata = $nuspec.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
        $id = $metadata.SelectSingleNode('*[local-name()="id"]')
        $version = $metadata.SelectSingleNode('*[local-name()="version"]')
        $repository = $metadata.SelectSingleNode('*[local-name()="repository"]')
        if ($id.InnerText -ne $pack.Id -or $version.InnerText -ne $runtimeVersion -or $null -eq $repository -or $repository.GetAttribute('commit') -ne $inventory.runtime.repository_revision) {
            throw "Runtime package identity or source revision disagrees with the inventory: $($pack.Id)"
        }
        $license = $metadata.SelectSingleNode('*[local-name()="license"]')
        if ($null -eq $license -or $license.GetAttribute('type') -ne 'expression' -or $license.InnerText -ne 'MIT') {
            throw "Review changed package license metadata before distributing $($pack.Id)."
        }
        Assert-Hash (Join-Path $packDirectory $pack.License) ([string]$fileInventory[$pack.VendoredLicense].sha256)
        if ($null -ne $pack.Notices) {
            Assert-Hash (Join-Path $packDirectory $pack.Notices) ([string]$fileInventory[$pack.VendoredNotices].sha256)
        }
        $packageFileName = $pack.Id.ToLowerInvariant() + '.' + $runtimeVersion + '.nupkg'
        $packageRecord = @($inventory.runtime.packages | Where-Object { $_.name -eq $packageFileName })
        if ($packageRecord.Count -ne 1) { throw "Missing package provenance: $packageFileName" }
        Assert-Hash (Join-Path $packDirectory $packageFileName) ([string]$packageRecord[0].sha256)
    }
    Write-Output "Verified published win-x64 runtime packs $runtimeVersion against preserved licenses and package provenance."
}

if (-not [string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = [System.IO.Path]::GetFullPath($PublishDirectory)
    if (-not $PublishDirectory.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The publish directory must be inside this repository.'
    }
    foreach ($document in $requiredDocuments + @('third-party/license-sources.json')) {
        $sourcePath = Get-WorkspacePath $document
        Assert-Hash (Join-Path $PublishDirectory $document) ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash)
    }
    foreach ($entry in $inventory.files) {
        Assert-Hash (Join-Path $PublishDirectory $entry.path) ([string]$entry.sha256)
    }
    Write-Output 'Verified copied distribution licenses, notices, privacy disclosure, and provenance.'
}

Write-Output "Verified $($fileInventory.Count) preserved third-party files and required project legal documents."
