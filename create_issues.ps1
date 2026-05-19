param(
    [int]$StartTask = 1,
    [int]$EndTask = 227,
    [switch]$DryRun = $false
)

# Load parsed tasks
$tasksJson = Get-Content '.\parsed_tasks.json' | ConvertFrom-Json
$tasks = @($tasksJson)

Write-Host "Creating GitHub issues from tasks..."
Write-Host "Total tasks: $($tasks.Count)"
Write-Host "Task range: T-$StartTask to T-$EndTask"
if ($DryRun) { Write-Host "DRY RUN MODE - no issues will be created" }
Write-Host ""

$createdCount = 0
$errorCount = 0

foreach ($task in $tasks) {
    $taskNum = [int]($task.Id -replace 'T-', '')
    if ($taskNum -lt $StartTask -or $taskNum -gt $EndTask) {
        continue
    }
    
    # Prepare issue title - include task ID for easy reference
    $title = "$($task.Id): $($task.Description.Substring(0, [Math]::Min(60, $task.Description.Length)))"
    if ($task.Description.Length -gt 60) { $title += "..." }
    
    # Prepare issue body
    $body = "**Task**: $($task.Id)`n"
    $body += "**Phase**: $($task.Phase)`n`n"
    $body += "## Description`n`n"
    $body += $task.Description + "`n`n"
    $body += "## Acceptance Criteria`n`n"
    $body += "- [ ] Task completed per specification`n"
    $body += "- [ ] Tests written and passing`n"
    $body += "- [ ] Code review approved`n"
    $body += "- [ ] Documentation updated`n"
    
    # Create the issue
    if ($DryRun) {
        Write-Host "[$taskNum/$($EndTask)] Would create: $title"
    } else {
        Write-Host "[$taskNum/$($EndTask)] Creating: $title"
        
        try {
            $issueJson = gh issue create `
                --title $title `
                --body $body 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                $createdCount++
                Write-Host "  ✓ Created successfully"
            } else {
                $errorCount++
                Write-Host "  ✗ Error: $issueJson"
            }
        }
        catch {
            $errorCount++
            Write-Host "  ✗ Exception: $_"
        }
    }
}

Write-Host ""
Write-Host "Summary: Created $createdCount issues" $(if (-not $DryRun) { "with $errorCount errors" } else { "(dry run)" })
