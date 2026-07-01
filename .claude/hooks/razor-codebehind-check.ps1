<#
Enforces two CLAUDE.md rules for Blazor components:
  - Every .razor file must have a paired .razor.cs code-behind file.
  - @code { } blocks are prohibited inside .razor files.
Fires as a PostToolUse hook on Edit|Write. Advisory only (block adds a note
next to the tool result so Claude sees it and can fix the file).
#>

try {
    $raw = [Console]::In.ReadToEnd()
    $json = $raw | ConvertFrom-Json
} catch {
    exit 0
}

$filePath = $json.tool_input.file_path
if (-not $filePath -or $filePath -notmatch '\.razor$') {
    exit 0
}
if (-not (Test-Path -LiteralPath $filePath)) {
    exit 0
}

$content = Get-Content -Raw -LiteralPath $filePath -ErrorAction SilentlyContinue
$issues = @()

if ($content -match '@code\s*\{') {
    $issues += "contains an @code block (CLAUDE.md prohibits inline @code in .razor files - move logic to the paired .razor.cs code-behind file)"
}

$codeBehindPath = "$filePath.cs"
if (-not (Test-Path -LiteralPath $codeBehindPath)) {
    $issues += "has no paired code-behind file at $(Split-Path -Leaf $codeBehindPath) (CLAUDE.md requires every .razor component to have a .razor.cs code-behind file)"
}

if ($issues.Count -gt 0) {
    $reason = "CLAUDE.md violation in $(Split-Path -Leaf $filePath): " + ($issues -join '; ')
    $result = @{ decision = 'block'; reason = $reason } | ConvertTo-Json -Compress
    Write-Output $result
}

exit 0
