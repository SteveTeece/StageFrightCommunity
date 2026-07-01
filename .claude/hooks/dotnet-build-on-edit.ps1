<#
Builds just the nearest .csproj to an edited .cs file so compile errors
surface immediately (PostToolUse on Edit|Write), rather than waiting until
the end of the task. Skips StageFright.App because it multi-targets MAUI
platforms and a per-edit build there would be too slow.
#>

try {
    $raw = [Console]::In.ReadToEnd()
    $json = $raw | ConvertFrom-Json
} catch {
    exit 0
}

$filePath = $json.tool_input.file_path
if (-not $filePath -or $filePath -notmatch '\.cs$') { exit 0 }
if ($filePath -match '[\\/](obj|bin)[\\/]') { exit 0 }
if (-not (Test-Path -LiteralPath $filePath)) { exit 0 }

# Walk up from the edited file to find the nearest ancestor .csproj.
$dir = Split-Path -Parent $filePath
$csproj = $null
while ($dir -and -not $csproj) {
    $found = Get-ChildItem -LiteralPath $dir -Filter '*.csproj' -File -ErrorAction SilentlyContinue
    if ($found) { $csproj = $found[0].FullName }
    else {
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
}
if (-not $csproj) { exit 0 }
if ($csproj -match 'StageFright\.App\.csproj$') { exit 0 }

$buildOutput = & dotnet build "$csproj" --nologo "-clp:ErrorsOnly" -v quiet 2>&1
$exitCode = $LASTEXITCODE
if ($exitCode -eq 0) { exit 0 }

$errorText = ($buildOutput | Out-String).Trim()
if ($errorText.Length -gt 3000) {
    $errorText = $errorText.Substring(0, 3000) + "`n... (truncated)"
}

$projName = Split-Path -Leaf $csproj
$fileName = Split-Path -Leaf $filePath
$reason = "dotnet build failed for $projName after editing $fileName`:`n`n$errorText"
$result = @{ decision = 'block'; reason = $reason } | ConvertTo-Json -Compress
Write-Output $result
exit 0
