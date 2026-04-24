param(
    [string]$SourceRoot,
    [string]$OutputPath,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    Split-Path -Parent $scriptRoot
} else {
    $SourceRoot
}

$repoRoot = [System.IO.Path]::GetFullPath($repoRoot)
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputPath = Join-Path ([System.IO.Path]::GetTempPath()) "zavod-clean-$stamp.zip"
}

$excludedRootNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($name in @(".git", ".zavod.local", ".codex", ".vs", ".idea", ".vscode", "bin", "obj", "zavod-import-test")) {
    [void]$excludedRootNames.Add($name)
}

$excludedRelativePrefixes = @(
    ".zavod\lab\",
    "tools\pdf-tools\",
    "tools\image-tools\"
)

$excludedRelativeFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($path in @(
    "app\config\openrouter.local.json",
    "tools\7za.exe"
)) {
    [void]$excludedRelativeFiles.Add($path)
}

function Get-RelativePath([string]$Path) {
    $rootWithSlash = if ($repoRoot.EndsWith("\") -or $repoRoot.EndsWith("/")) { $repoRoot } else { $repoRoot + [System.IO.Path]::DirectorySeparatorChar }
    $rootUri = [System.Uri]::new($rootWithSlash)
    $pathUri = [System.Uri]::new([System.IO.Path]::GetFullPath($Path))
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace("/", "\")
}

function Test-ExcludedPath([string]$RelativePath) {
    $segments = $RelativePath.Split("\", [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($segment in $segments) {
        if ($excludedRootNames.Contains($segment)) {
            return $true
        }
    }

    if ($segments.Count -gt 0 -and $excludedRootNames.Contains($segments[0])) {
        return $true
    }

    if ($excludedRelativeFiles.Contains($RelativePath)) {
        return $true
    }

    if ($RelativePath -like "app\config\*.local.json") {
        return $true
    }

    foreach ($prefix in $excludedRelativePrefixes) {
        if ($RelativePath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Test-SecretLikeContent([string]$Path) {
    $extension = [System.IO.Path]::GetExtension($Path)
    if ($extension -notin @(".cs", ".csproj", ".json", ".md", ".ps1", ".sln", ".slnx", ".txt", ".xaml", ".xml", ".yaml", ".yml")) {
        return $false
    }

    $text = [System.IO.File]::ReadAllText($Path)
    if ($text -match "sk-or-[A-Za-z0-9_-]{12,}") {
        return $true
    }

    $apiKeyMatch = [System.Text.RegularExpressions.Regex]::Match($text, '"apiKey"\s*:\s*"([^"]{8,})"')
    if ($apiKeyMatch.Success) {
        $value = $apiKeyMatch.Groups[1].Value
        if ($value -notin @("example-key", "file-key", "test-key")) {
            return $true
        }
    }

    return $text -match "OPENROUTER_API_KEY\s*=\s*\S{8,}" -or
        $text -match "Bearer\s+[A-Za-z0-9._-]{16,}"
}

$files = Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Force |
    Where-Object {
        $relative = Get-RelativePath $_.FullName
        -not (Test-ExcludedPath $relative)
    }

$secretHits = foreach ($file in $files) {
    if (Test-SecretLikeContent $file.FullName) {
        Get-RelativePath $file.FullName
    }
}

if ($secretHits.Count -gt 0) {
    throw "Clean export blocked: secret-like content found in:`n$($secretHits -join "`n")"
}

if ($DryRun) {
    Write-Output "Clean export dry run passed. Files selected: $($files.Count). Output would be: $OutputPath"
    exit 0
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("zavod-clean-export-" + [System.Guid]::NewGuid().ToString("N"))
try {
    foreach ($file in $files) {
        $relative = Get-RelativePath $file.FullName
        $target = Join-Path $tempRoot $relative
        $targetDirectory = Split-Path -Parent $target
        if (-not [System.IO.Directory]::Exists($targetDirectory)) {
            [System.IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
        }

        Copy-Item -LiteralPath $file.FullName -Destination $target
    }

    if (Test-Path -LiteralPath $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Force
    }

    Compress-Archive -LiteralPath (Join-Path $tempRoot "*") -DestinationPath $OutputPath -Force
    Write-Output "Clean export written: $OutputPath"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
