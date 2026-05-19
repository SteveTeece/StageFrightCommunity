#!/usr/bin/env python3
import re
import json

# Read tasks.md
with open('specs/001-initial-mvp/tasks.md', 'r', encoding='utf-8') as f:
    content = f.read()

# Split by lines and extract tasks
tasks = []
lines = content.split('\n')

for line in lines:
    # Match task lines: "- [ ] T-### Description"
    # Task ID can have [P] marker after it
    match = re.match(r'^- \[ \] (T-\d+)(?:\s+\[P\])?\s+(.*?)$', line)
    if match:
        task_id = match.group(1)
        task_desc = match.group(2).strip()
        
        # Extract phase from context - look backwards for phase header
        phase = "Unknown"
        for i in range(len(lines) - 1, -1, -1):
            if lines[i].startswith('## Phase'):
                phase = lines[i].replace('## ', '')
                break
            # If we find another task, stop looking back
            if i < lines.index(line) and re.match(r'^- \[ \] T-\d+', lines[i]):
                break
        
        tasks.append({
            'id': task_id,
            'description': task_desc,
            'phase': phase
        })

print(f"Parsed {len(tasks)} tasks")
print("\nFirst 10 tasks:")
for task in tasks[:10]:
    print(f"{task['id']}: {task['description'][:70]}...")

# Save to JSON for use in creating issues
with open('parsed_tasks.json', 'w', encoding='utf-8') as f:
    json.dump(tasks, f, indent=2, ensure_ascii=False)

print(f"\nSaved {len(tasks)} tasks to parsed_tasks.json")
