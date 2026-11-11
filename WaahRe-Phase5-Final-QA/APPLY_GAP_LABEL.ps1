$ErrorActionPreference = 'Stop'

# Only edits the ATS review heading, with a backup and a strict unique-text check.
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$relative = 'frontend/rolefit-ui/src/components/CandidateReview.tsx'
$target = Join-Path $root $relative
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "SAFE STOP: CandidateReview.tsx not found at $relative. Place this update folder beside backend and frontend."
}

$old = '<h3>Missing or weak evidence ({result.missingSkills.length + deductions.length})</h3>'
$new = '<h3>{result.missingSkills.length} missing {result.missingSkills.length === 1 ? "skill" : "skills"} &middot; {deductions.length} {deductions.length === 1 ? "area" : "areas"} to improve</h3>'
$source = [System.IO.File]::ReadAllText($target)

if ($source.Contains($new)) {
    Write-Host 'ALREADY APPLIED: The clearer label is present. No changes made.' -ForegroundColor Green
    exit 0
}

$first = $source.IndexOf($old, [System.StringComparison]::Ordinal)
if ($first -lt 0 -or $source.LastIndexOf($old, [System.StringComparison]::Ordinal) -ne $first) {
    throw 'SAFE STOP: The expected original heading was not found exactly once. Current file is unchanged; do not force an overwrite.'
}

Write-Host "Target: $relative" -ForegroundColor Cyan
Write-Host 'Change: clarify missing-skill count versus scoring category deductions.'
Write-Host 'No ATS logic, SQL, resume parsing, or export logic will change.'
$answer = Read-Host 'Type YES to apply this one-line UI change'
if ($answer -cne 'YES') { Write-Host 'Cancelled. No changes made.'; exit 0 }

$backupDirectory = Join-Path $root ('Backups/Phase5GapLabel-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
Copy-Item -LiteralPath $target -Destination (Join-Path $backupDirectory 'CandidateReview.tsx') -ErrorAction Stop

$updated = $source.Replace($old,$new)
[System.IO.File]::WriteAllText($target, $updated, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "APPLIED: $relative" -ForegroundColor Green
Write-Host "Backup: $backupDirectory"
Write-Host 'Next: cd frontend/rolefit-ui; npm.cmd run build'
