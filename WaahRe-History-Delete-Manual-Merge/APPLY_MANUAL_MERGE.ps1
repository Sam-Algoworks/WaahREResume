$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$package = $PSScriptRoot
$project = Split-Path -Parent $package
$expected = Get-Content -Raw (Join-Path $package 'expected-ui-sha256.json') | ConvertFrom-Json
$ui = @('frontend/rolefit-ui/src/App.tsx', 'frontend/rolefit-ui/src/components/TalentLibrary.tsx')
$others = @(
  'frontend/rolefit-ui/src/api.ts',
  'backend/RoleFit.Api/Services/ResumeArchiveStore.cs',
  'backend/RoleFit.Api/Services/SqlHistoryRepository.cs',
  'backend/RoleFit.Api/Controllers/ArchiveController.cs'
)
$all = @($ui) + @($others)
$utf8 = New-Object System.Text.UTF8Encoding($false)
$stage = Join-Path $env:TEMP ('WaahReManualHistoryMerge-' + [Guid]::NewGuid().ToString('N'))
$changed = New-Object System.Collections.Generic.List[string]
$lineEndings = @{}

function Normalize-Text([string] $raw) { return $raw.Replace("`r`n", "`n") }
function Hash-Text([string] $raw) {
  # Windows PowerShell 5.1 runs on .NET Framework; avoid .NET 5+ HashData/ToHexString APIs.
  $bytes = [System.Text.Encoding]::UTF8.GetBytes((Normalize-Text $raw))
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try { $digest = $sha.ComputeHash($bytes) } finally { $sha.Dispose() }
  return ([System.BitConverter]::ToString($digest) -replace '-', '').ToLowerInvariant()
}
function Write-Staged([string] $relative, [string] $value) {
  $path = Join-Path $stage $relative
  New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
  [System.IO.File]::WriteAllText($path, (Normalize-Text $value), $utf8)
}

Write-Host 'Waah Re Resume - History DELETE manual merge for your actual App.tsx and TalentLibrary.tsx' -ForegroundColor Cyan
Write-Host 'Project root: ' $project
if (!(Test-Path -LiteralPath (Join-Path $project 'backend/RoleFit.Api') -PathType Container) -or
    !(Test-Path -LiteralPath (Join-Path $project 'frontend/rolefit-ui') -PathType Container)) {
  throw 'Put this extracted folder in the project root containing backend and frontend. Nothing changed.'
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
  # Phase A: stage every file. Do not change any project file until all six pass.
  foreach ($relative in $all) {
    $target = Join-Path $project $relative
    if (!(Test-Path -LiteralPath $target -PathType Leaf)) { throw "SAFE STOP: Missing $relative. No files changed." }
    $current = [System.IO.File]::ReadAllText($target)
    $lineEndings[$relative] = if ($current.Contains("`r`n")) { "`r`n" } else { "`n" }
    if ($ui -contains $relative) {
      $incomingFile = Join-Path $package ('files/' + $relative)
      $incoming = [System.IO.File]::ReadAllText($incomingFile)
      if ((Normalize-Text $current) -ceq (Normalize-Text $incoming)) {
        Write-Host "ALREADY INSTALLED: $relative" -ForegroundColor DarkGreen
        continue
      }
      $expectedHash = if ($relative -eq $ui[0]) { $expected.'frontend/rolefit-ui/src/App.tsx' }
                      else { $expected.'frontend/rolefit-ui/src/components/TalentLibrary.tsx' }
      if ((Hash-Text $current) -cne $expectedHash) {
        throw "SAFE STOP: $relative changed since you uploaded it. Your project is UNCHANGED. Upload its latest version for an exact merge."
      }
      Write-Staged $relative $incoming
      $changed.Add($relative)
      Write-Host "PREPARED exact manual UI merge: $relative" -ForegroundColor Green
      continue
    }

    $baseFile = Join-Path $package ('base/' + $relative)
    $incomingFile = Join-Path $package ('incoming/' + $relative)
    $base = [System.IO.File]::ReadAllText($baseFile)
    $incoming = [System.IO.File]::ReadAllText($incomingFile)
    if ((Normalize-Text $current) -ceq (Normalize-Text $incoming)) {
      Write-Host "ALREADY INSTALLED: $relative" -ForegroundColor DarkGreen
      continue
    }
    if ((Normalize-Text $current) -ceq (Normalize-Text $base)) {
      Write-Staged $relative $incoming
      $changed.Add($relative)
      Write-Host "PREPARED known backend/API source: $relative" -ForegroundColor Green
      continue
    }
    if (!(Get-Command git -ErrorAction SilentlyContinue)) { throw "SAFE STOP: Git is needed to merge a newer $relative. Project unchanged." }
    $work = Join-Path $stage $relative
    $b = Join-Path $stage ('_base/' + $relative)
    $i = Join-Path $stage ('_incoming/' + $relative)
    foreach ($p in @($work,$b,$i)) { New-Item -ItemType Directory -Path (Split-Path -Parent $p) -Force | Out-Null }
    [System.IO.File]::WriteAllText($work, (Normalize-Text $current), $utf8)
    [System.IO.File]::WriteAllText($b, (Normalize-Text $base), $utf8)
    [System.IO.File]::WriteAllText($i, (Normalize-Text $incoming), $utf8)
    & git merge-file --diff3 -- $work $b $i 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "SAFE STOP: $relative has a real merge conflict. Project UNCHANGED." }
    $result = [System.IO.File]::ReadAllText($work)
    if ($result.Contains('<<<<<<< ') -or $result.Contains('>>>>>>> ')) { throw "SAFE STOP: Merge markers in $relative. Project UNCHANGED." }
    $changed.Add($relative)
    Write-Host "PREPARED automatic clean merge: $relative" -ForegroundColor Green
  }

  if ($changed.Count -eq 0) { Write-Host 'No changes needed; patch already installed.' -ForegroundColor Green; exit 0 }
  Write-Host ''
  Write-Host 'PRECHECK OK. No changes have been applied yet.' -ForegroundColor Green
  Write-Host 'Before YES: verify the four SQL tables and existing counts; privately back up WaahReHistory and App_Data, including Data Protection keys.' -ForegroundColor Yellow
  Write-Host 'Important: this update disables old JSON auto-reimport so deleted SQL records cannot reappear.' -ForegroundColor Yellow
  $answer = Read-Host 'After checking your PRIVATE backup, type YES to apply'
  if ($answer -cne 'YES') { Write-Host 'Cancelled; no project files changed.' -ForegroundColor Yellow; exit 0 }

  $backup = Join-Path $project ('HistoryManualMergeBackup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
  foreach ($relative in $changed) {
    $original = Join-Path $project $relative
    $backupPath = Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $backupPath) -Force | Out-Null
    Copy-Item -LiteralPath $original -Destination $backupPath -Force
  }
  $applied = New-Object System.Collections.Generic.List[string]
  try {
    foreach ($relative in $changed) {
      $staged = Join-Path $stage $relative
      $target = Join-Path $project $relative
      $value = [System.IO.File]::ReadAllText($staged)
      if ($lineEndings[$relative] -eq "`r`n") { $value = $value.Replace("`n", "`r`n") }
      # Register before the write so rollback also covers a partially written file.
      $applied.Add($relative)
      [System.IO.File]::WriteAllText($target, $value, $utf8)
    }
  } catch {
    foreach ($relative in $applied) {
      Copy-Item -LiteralPath (Join-Path $backup $relative) -Destination (Join-Path $project $relative) -Force
    }
    throw "Apply failed; overwritten files rolled back. Error: $($_.Exception.Message)"
  }
  Write-Host "SUCCESS. Applied $($changed.Count) file(s). Source backup at: $backup" -ForegroundColor Green
  Write-Host 'Now rebuild backend and frontend. Test deletion on a synthetic archived resume only.' -ForegroundColor Cyan
} finally {
  if (Test-Path $stage) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
