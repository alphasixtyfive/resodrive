param(
    [ValidateSet('win-x64')]
    [string] $Runtime = 'win-x64',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [bool] $BuildMsi = $true
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$artifactRoot = Join-Path $projectRoot "artifacts\$Runtime"
$stageRoot = Join-Path $artifactRoot '.stage'
$appOutput = Join-Path $stageRoot 'app'
$installerOutput = Join-Path $stageRoot 'installer'
$buildProperties = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)
$versionNode = $buildProperties.SelectSingleNode('/Project/PropertyGroup/VersionPrefix')
$releaseVersion = if ($null -eq $versionNode) { '' } else { $versionNode.InnerText.Trim() }
$productDisplayName = $buildProperties.SelectSingleNode('/Project/PropertyGroup/ProductDisplayName').InnerText.Trim()
$productPublisher = $buildProperties.SelectSingleNode('/Project/PropertyGroup/ProductPublisher').InnerText.Trim()
$productDescription = $buildProperties.SelectSingleNode('/Project/PropertyGroup/ProductDescription').InnerText.Trim()
$executableBaseName = $buildProperties.SelectSingleNode('/Project/PropertyGroup/ExecutableBaseName').InnerText.Trim()
$packageOutput = Join-Path $artifactRoot $executableBaseName
if ([string]::IsNullOrWhiteSpace($releaseVersion)) {
    throw 'VersionPrefix is missing from Directory.Build.props.'
}
if ([string]::IsNullOrWhiteSpace($productDisplayName) -or
    [string]::IsNullOrWhiteSpace($productPublisher) -or
    [string]::IsNullOrWhiteSpace($productDescription)) {
    throw 'Product metadata is incomplete in Directory.Build.props.'
}
if ($executableBaseName -notmatch '^[a-z0-9-]+$') {
    throw "ExecutableBaseName '$executableBaseName' must contain only lowercase letters, digits, or hyphens."
}
if ($releaseVersion -notmatch '^(\d{1,3})\.(\d{1,3})\.(\d{1,5})$' -or
    [int]$Matches[1] -gt 255 -or [int]$Matches[2] -gt 255 -or [int]$Matches[3] -gt 65535) {
    throw "VersionPrefix '$releaseVersion' is not compatible with Windows Installer. Use major.minor.build (255.255.65535 maximum)."
}
$archiveOutput = Join-Path $artifactRoot "$executableBaseName-$Runtime-$releaseVersion.zip"
$archiveChecksumOutput = "$archiveOutput.sha256"
$msiOutput = Join-Path $artifactRoot "$executableBaseName-$Runtime-$releaseVersion.msi"
$msiChecksumOutput = "$msiOutput.sha256"
$msiUpgradeCode = '8D0BD004-119E-4589-B816-7D5A27D94561'
if ($BuildMsi -and $Runtime -ne 'win-x64') {
    throw 'MSI packaging currently supports win-x64 only. Use -BuildMsi $false for win-arm64 portable builds.'
}

$resolvedArtifacts = [IO.Path]::GetFullPath($artifactRoot)
$resolvedArtifactParent = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
if (-not $resolvedArtifacts.StartsWith($resolvedArtifactParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an artifact path outside $resolvedArtifactParent."
}
if (Test-Path -LiteralPath $resolvedArtifacts) {
    # Keep the runtime directory itself so a File Explorer window or terminal whose
    # current location is this folder does not prevent otherwise safe cleanup.
    Get-ChildItem -LiteralPath $resolvedArtifacts -Force |
        Remove-Item -Recurse -Force
}

foreach ($generatedPath in @(
    $stageRoot,
    $packageOutput,
    $archiveOutput,
    $archiveChecksumOutput,
    $msiOutput,
    $msiChecksumOutput
)) {
    $resolvedParent = [IO.Path]::GetFullPath((Split-Path -Parent $generatedPath))
    $resolvedArtifacts = [IO.Path]::GetFullPath($artifactRoot)
    if (-not $resolvedParent.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a build path outside $artifactRoot."
    }
    if (Test-Path -LiteralPath $generatedPath) {
        Remove-Item -LiteralPath $generatedPath -Recurse -Force
    }
}

dotnet test (Join-Path $projectRoot 'resodrive.slnx') --configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
dotnet publish (Join-Path $projectRoot 'src\ResoDrive.App\ResoDrive.App.csproj') `
    --configuration $Configuration --runtime $Runtime --self-contained true `
    --output $appOutput
if ($LASTEXITCODE -ne 0) { throw "App publish failed with exit code $LASTEXITCODE." }

New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $appOutput "$executableBaseName.exe") -Destination $packageOutput -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'profiles.sample.json') -Destination $packageOutput -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $packageOutput -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination $packageOutput -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $packageOutput -Force

Get-ChildItem -LiteralPath $packageOutput -File |
    Where-Object Name -ne 'SHA256SUMS.txt' |
    Sort-Object Name |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } |
    Set-Content -LiteralPath (Join-Path $packageOutput 'SHA256SUMS.txt') -Encoding ascii

if ($BuildMsi) {
    dotnet build (Join-Path $projectRoot 'installer\ResoDrive.Installer.wixproj') `
        --configuration $Configuration `
        --output $installerOutput `
        -p:PackageSource=$packageOutput `
        -p:ResoDriveVersion=$releaseVersion `
        -p:ResoDriveRuntime=$Runtime `
        -p:ResoDriveUpgradeCode=$msiUpgradeCode `
        "-p:ResoDriveProductName=$productDisplayName" `
        "-p:ResoDrivePublisher=$productPublisher" `
        "-p:ResoDriveDescription=$productDescription" `
        "-p:ResoDriveExecutableBaseName=$executableBaseName" `
        "-p:ResoDriveExecutableName=$executableBaseName.exe"
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed with exit code $LASTEXITCODE." }

    $builtMsi = Join-Path $installerOutput "$executableBaseName-$Runtime-$releaseVersion.msi"
    if (-not (Test-Path -LiteralPath $builtMsi -PathType Leaf)) {
        throw "The MSI build did not produce '$builtMsi'."
    }
    $windowsInstaller = New-Object -ComObject WindowsInstaller.Installer
    $database = $windowsInstaller.GetType().InvokeMember(
        'OpenDatabase',
        'InvokeMethod',
        $null,
        $windowsInstaller,
        @([string] $builtMsi, [int] 0))
    $view = $database.GetType().InvokeMember(
        'OpenView',
        'InvokeMethod',
        $null,
        $database,
        @('SELECT `FileName` FROM `File`'))
    $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null) | Out-Null
    $packagedFiles = @()
    while ($record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)) {
        $msiFileName = $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, 1)
        $packagedFiles += ($msiFileName -split '\|')[-1]
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($record) | Out-Null
    }
    $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($windowsInstaller) | Out-Null
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    foreach ($requiredFile in @("$executableBaseName.exe", 'profiles.sample.json')) {
        if ($requiredFile -notin $packagedFiles) {
            throw "MSI validation could not find '$requiredFile' in the File table."
        }
    }
    if ('rclone.exe' -in $packagedFiles) {
        throw "MSI validation found rclone.exe, which must remain an app-managed per-user component."
    }

    Copy-Item -LiteralPath $builtMsi -Destination $msiOutput -Force
    $msiHash = (Get-FileHash -LiteralPath $msiOutput -Algorithm SHA256).Hash.ToLowerInvariant()
    "$msiHash  $(Split-Path -Leaf $msiOutput)" |
        Set-Content -LiteralPath $msiChecksumOutput -Encoding ascii
}

Remove-Item -LiteralPath $stageRoot -Recurse -Force
Compress-Archive -Path (Join-Path $packageOutput '*') -DestinationPath $archiveOutput -CompressionLevel Optimal
$archiveHash = (Get-FileHash -LiteralPath $archiveOutput -Algorithm SHA256).Hash.ToLowerInvariant()
"$archiveHash  $(Split-Path -Leaf $archiveOutput)" |
    Set-Content -LiteralPath $archiveChecksumOutput -Encoding ascii
Write-Host "Published $productDisplayName to $packageOutput"
Write-Host "Release archive: $archiveOutput"
if ($BuildMsi) {
    Write-Host "Windows installer: $msiOutput"
}
