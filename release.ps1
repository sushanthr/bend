[CmdletBinding(PositionalBinding=$false)]
param (
    [switch]$OnlyBuild=$false
)

$appName = "Bend"
$projDir = "Bend"
$platform = "x64"
Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

Write-Output "Working directory: $pwd"

# Find MSBuild.
$msBuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
    -prerelease | select-object -first 1
if ([string]::IsNullOrWhiteSpace($msBuildPath)) {
    throw "MSBuild could not be found."
}
Write-Output "MSBuild: $msBuildPath"

# Load current Git tag.
$tag = $(git describe --tags)
Write-Output "Tag: $tag"

# Parse tag into a three-number version.
$base = $tag.Split('-')[0].TrimStart('v')
$parts = $base.Split('.')
while ($parts.Count -lt 4) { $parts += "0" }
$version = ($parts[0..3] -join '.')
Write-Output "Version: $version"

# Clean output directory.
$publishDir = "bin/publish"
$outDir = "$projDir/$publishDir"
if (Test-Path $outDir) {
    Remove-Item -Path $outDir -Recurse
}

# Publish the application.
Push-Location $projDir
try {
    Write-Output "Publishing:"
    $msBuildVerbosityArg = "/v:m"
    if ($env:CI) {
        $msBuildVerbosityArg = ""
    }
    & $msBuildPath "../BendConsoleHost/BendConsoleHost.csproj" /target:Build `
        /p:Configuration=Release /p:Platform=$platform $msBuildVerbosityArg
    if ($LASTEXITCODE -ne 0) {
        throw "BendConsoleHost build failed with exit code $LASTEXITCODE."
    }
    & $msBuildPath /target:publish /p:PublishProfile=ClickOnceProfile `
        /p:ApplicationVersion=$version /p:Configuration=Release /p:Platform=$platform `
        /p:BootstrapperEnabled=false `
        /p:PublishDir=$publishDir /p:PublishUrl=$publishDir `
        $msBuildVerbosityArg
    if ($LASTEXITCODE -ne 0) {
        throw "Bend publish failed with exit code $LASTEXITCODE."
    }

    $publishedHost = Get-ChildItem -Path "$publishDir/Application Files" `
        -Filter "BendConsoleHost.exe*" -Recurse | Select-Object -First 1
    if ($null -eq $publishedHost) {
        throw "Published application does not contain BendConsoleHost.exe."
    }
    Write-Output "Console host: $($publishedHost.FullName)"

    # Measure publish size.
    $publishSize = (Get-ChildItem -Path "$publishDir/Application Files" -Recurse |
        Measure-Object -Property Length -Sum).Sum / 1Mb
    Write-Output ("Published size: {0:N2} MB" -f $publishSize)
}
finally {
    Pop-Location
}

if ($OnlyBuild) {
    Write-Output "Build finished."
    exit
}

# Clone `gh-pages` branch.
$ghPagesDir = "gh-pages"
if (-Not (Test-Path $ghPagesDir)) {
    git clone $(git config --get remote.origin.url) -b gh-pages `
        --depth 1 --single-branch $ghPagesDir
}

Push-Location $ghPagesDir
try {
    # Remove previous application files.
    Write-Output "Removing previous files..."
    if (Test-Path "Application Files") {
        Remove-Item -Path "Application Files" -Recurse
    }
    if (Test-Path "$appName.application") {
        Remove-Item -Path "$appName.application"
    }

    # Copy new application files.
    Write-Output "Copying new files..."
    Copy-Item -Path "../$outDir/Application Files","../$outDir/$appName.application" `
        -Destination . -Recurse

    # Stage and commit.
    Write-Output "Staging..."
    git add -A
    Write-Output "Committing..."
    git commit -m "Update to v$version"

    # Push.
    git push
} finally {
    Pop-Location
}
