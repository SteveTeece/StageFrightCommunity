param(
    [int]$Count = 227
)

# Read the tasks.md file
$tasksPath = '.\specs\001-initial-mvp\tasks.md'
$content = Get-Content $tasksPath -Raw

# Parse tasks - simple line-by-line approach
$tasks = @()
$currentPhase = "Phase 0: Project Setup"

foreach ($line in $content -split "`n") {
    # Update phase if we see a phase header
    if ($line -match '^## Phase \d+:') {
        $currentPhase = $line -replace '^## ', ''
    }
    
    # Match task lines
    if ($line -match '^- \[ \] (T-\d+)(?:\s+\[P\])?\s+(.+)$') {
        $taskId = $matches[1]
        $taskDesc = $matches[2].Trim()
        
        # Clean up description - remove any markdown artifacts
        $taskDesc = $taskDesc -replace '`([^`]+)`', '$1'
        
        $tasks += @{
            Id = $taskId
            Description = $taskDesc
            Phase = $currentPhase
        }
    }
}

Write-Host "Parsed $($tasks.Count) tasks"
Write-Host ""
Write-Host "First 10 tasks:"
for ($i = 0; $i -lt [Math]::Min(10, $tasks.Count); $i++) {
    $task = $tasks[$i]
    Write-Host "$($task.Id): $($task.Description.Substring(0, [Math]::Min(70, $task.Description.Length)))..."
}

# Export to JSON
$tasks | ConvertTo-Json | Set-Content '.\parsed_tasks.json'
Write-Host ""
Write-Host "Exported to parsed_tasks.json"
