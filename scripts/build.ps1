[CmdletBinding()]
param([switch]$FrameworkDependent)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$rootPrefix = $projectRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.nuget\packages'
$project = Join-Path $projectRoot 'src\SesPlus\SesPlus.csproj'
$verifier = Join-Path $PSScriptRoot 'verify-licenses.ps1'

# Fail before publishing if legal files or the pinned runtime inventory have changed.
& $verifier
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version').InnerText.Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid application version for packaging.' }
$assemblyNameNodes = @($projectXml.SelectNodes('/Project/PropertyGroup/AssemblyName'))
if ($assemblyNameNodes.Count -ne 1) { throw 'Expected one AssemblyName in the application project.' }
$assemblyName = $assemblyNameNodes[0].InnerText.Trim()
if ($assemblyName -notmatch '^[A-Za-z][A-Za-z0-9._-]{0,127}$' -or $assemblyName.EndsWith('.') -or $assemblyName -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)') {
    throw 'AssemblyName must be a safe Windows file basename for packaging.'
}
$releaseName = "$assemblyName-v$version"
$dist = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
$output = [System.IO.Path]::GetFullPath((Join-Path $dist $releaseName))
if (-not $output.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Parent $output) -ne $dist) {
    throw 'The generated publish path is outside the expected dist directory.'
}

function Assert-NoLinks([string]$Path) {
    $current = [System.IO.Path]::GetFullPath($Path)
    while ($current.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "Packaging refuses linked paths: $current"
            }
        }
        $current = Split-Path -Parent $current
    }
}

function Assert-NoLinkedDescendants([string]$Directory) {
    Assert-NoLinks $Directory
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $pending.Push($Directory)
    while ($pending.Count -gt 0) {
        foreach ($child in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            if ($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "Packaging refuses linked paths: $($child.FullName)"
            }
            if ($child.PSIsContainer) { $pending.Push($child.FullName) }
        }
    }
}

Assert-NoLinks $output
if (Test-Path -LiteralPath $output) {
    # Only this validated generated directory is removed; no source directory is touched.
    Assert-NoLinkedDescendants $output
    Remove-Item -LiteralPath $output -Recurse -Force
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
& dotnet publish $project -c Release -r win-x64 --self-contained $selfContained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:NuGetAudit=false -p:UseSharedCompilation=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed; no release archives were created.' }
if (-not (Test-Path -LiteralPath (Join-Path $output "$assemblyName.exe") -PathType Leaf)) { throw "Publish did not produce $assemblyName.exe." }
if (-not $FrameworkDependent) { & $verifier -VerifyRuntimePacks }

foreach ($document in @('LICENSE', 'NOTICE.md', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $output
}
$outputDocs = Join-Path $output 'docs'
New-Item -ItemType Directory -Path $outputDocs -Force | Out-Null
foreach ($document in @('LICENSING.md', 'PRIVACY.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "docs/$document") -Destination $outputDocs
}
Assert-NoLinkedDescendants (Join-Path $projectRoot 'third-party')
Copy-Item -LiteralPath (Join-Path $projectRoot 'third-party') -Destination $output -Recurse
& $verifier -PublishDirectory $output

Add-Type -AssemblyName System.IO.Compression

function Get-RelativeArchivePath([string]$BaseDirectory, [string]$FilePath) {
    $basePrefix = [System.IO.Path]::GetFullPath($BaseDirectory).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath($FilePath)
    if (-not $fullPath.StartsWith($basePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Archive file is outside its base directory: $fullPath"
    }
    return $fullPath.Substring($basePrefix.Length).Replace('\', '/')
}

function Test-ExcludedSourcePath([string]$RelativePath) {
    # Curated top-level inputs plus these filters keep machine state and credentials out.
    $excludedDirectories = 'bin|obj|dist|artifacts|node_modules|\.git|\.nuget|\.dotnet-home|\.vs|\.idea|\.codex|\.agents|\.aws|\.cache|cache|caches|__pycache__|\.pytest_cache|\.mypy_cache|TestResults|logs|preferences'
    if ($RelativePath -match "(?i)(^|/)($excludedDirectories)(/|$)") { return $true }
    $fileName = ($RelativePath -split '/')[-1]
    return $fileName -match '(?i)^(\.env(?:\..*)?|preferences\.json|prefs\.json|.*\.(log|user|suo|pfx|p12|pem|key|pyc|pdb))$'
}

function Get-ArchiveFiles([string]$BaseDirectory, [string[]]$RelativeInputs, [switch]$SourceArchive) {
    $queue = New-Object 'System.Collections.Generic.Stack[string]'
    foreach ($relativeInput in $RelativeInputs) {
        $inputPath = Join-Path $BaseDirectory $relativeInput
        if (-not (Test-Path -LiteralPath $inputPath)) { throw "Archive input is missing: $relativeInput" }
        $queue.Push([System.IO.Path]::GetFullPath($inputPath))
    }
    while ($queue.Count -gt 0) {
        $path = $queue.Pop()
        $relative = Get-RelativeArchivePath $BaseDirectory $path
        if ($SourceArchive -and (Test-ExcludedSourcePath $relative)) { continue }
        $item = Get-Item -LiteralPath $path -Force
        if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Archive input must not be a link: $relative"
        }
        if ($item.PSIsContainer) {
            foreach ($child in Get-ChildItem -LiteralPath $path -Force) { $queue.Push($child.FullName) }
        } else {
            $item
        }
    }
}

function New-ReleaseArchive([string]$ArchivePath, [string]$BaseDirectory, [System.IO.FileInfo[]]$Files) {
    Assert-NoLinks $ArchivePath
    $stream = [System.IO.File]::Open($ArchivePath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    $archive = $null
    try {
        $archive = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
        $names = @{}
        foreach ($file in ($Files | Sort-Object FullName)) {
            $relative = Get-RelativeArchivePath $BaseDirectory $file.FullName
            if ($names.ContainsKey($relative)) { throw "Duplicate archive entry: $relative" }
            $names[$relative] = $true
            $entry = $archive.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
            $input = [System.IO.File]::OpenRead($file.FullName)
            $entryStream = $null
            try {
                $entryStream = $entry.Open()
                $input.CopyTo($entryStream)
            } finally {
                if ($null -ne $entryStream) { $entryStream.Dispose() }
                $input.Dispose()
            }
        }
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $stream.Dispose()
    }
    $hash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = $ArchivePath + '.sha256'
    Assert-NoLinks $checksumPath
    [System.IO.File]::WriteAllText($checksumPath, $hash + '  ' + [System.IO.Path]::GetFileName($ArchivePath) + "`n", [System.Text.Encoding]::ASCII)
    Write-Output "Created: $ArchivePath"
    Write-Output "Checksum: $checksumPath"
}

$binaryInputs = @(Get-ChildItem -LiteralPath $output -Force | ForEach-Object { $_.Name })
$binaryFiles = @(Get-ArchiveFiles $output $binaryInputs)
$sourceInputs = @('LICENSE', 'README.md', 'NOTICE.md', '.gitignore', '.gitattributes', 'src', 'tests', 'scripts', 'docs', '.github', 'third-party')
$sourceFiles = @(Get-ArchiveFiles $projectRoot $sourceInputs -SourceArchive)
$binaryName = if ($FrameworkDependent) { "$releaseName-win-x64-framework-dependent.zip" } else { "$releaseName-win-x64.zip" }
New-ReleaseArchive (Join-Path $dist $binaryName) $output $binaryFiles
New-ReleaseArchive (Join-Path $dist "$releaseName-source.zip") $projectRoot $sourceFiles
Write-Output "Ready: $output\$assemblyName.exe"
