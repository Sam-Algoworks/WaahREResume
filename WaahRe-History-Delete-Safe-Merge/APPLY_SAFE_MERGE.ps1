$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$package = $PSScriptRoot
$project = Split-Path -Parent $package
$files = @(
  'backend/RoleFit.Api/Services/SqlHistoryRepository.cs',
  'backend/RoleFit.Api/Services/ResumeArchiveStore.cs',
  'backend/RoleFit.Api/Controllers/ArchiveController.cs',
  'frontend/rolefit-ui/src/api.ts',
  'frontend/rolefit-ui/src/components/TalentLibrary.tsx',
  'frontend/rolefit-ui/src/App.tsx'
)

Write-Host 'Waah Re Resume - History delete SAFE MERGE (no force overwrite)' -ForegroundColor Cyan
Write-Host 'Project root:' $project
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
  throw 'Git is not in PATH. Install Git for Windows or ask for a manual merge; no source files have changed.'
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
$stage = Join-Path $env:TEMP ('WaahRe-History-Merge-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$changed = New-Object System.Collections.Generic.List[string]
$lineEndings = @{}

try {
  foreach ($relative in $files) {
    $target = Join-Path $project $relative
    $base = Join-Path $package ('base/' + $relative)
    $incoming = Join-Path $package ('incoming/' + $relative)
    if (!(Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing project file: $relative. No files changed." }
    if (!(Test-Path -LiteralPath $base) -or !(Test-Path -LiteralPath $incoming)) { throw "Damaged update package: $relative. No files changed." }
    $currentText = [System.IO.File]::ReadAllText($target)
    $baseText = [System.IO.File]::ReadAllText($base)
    $incomingText = [System.IO.File]::ReadAllText($incoming)
    $eol = if ($currentText.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lineEndings[$relative] = $eol
    $normalizedCurrent = $currentText.Replace("`r`n", "`n")
    $normalizedBase = $baseText.Replace("`r`n", "`n")
    $normalizedIncoming = $incomingText.Replace("`r`n", "`n")
    if ($normalizedCurrent -ceq $normalizedIncoming) { Write-Host "SKIP (already installed): $relative" -ForegroundColor DarkGreen; continue }
    $changed.Add($relative)
    $working = Join-Path $stage ('merged/' + $relative)
    $b = Join-Path $stage ('base/' + $relative)
    $i = Join-Path $stage ('incoming/' + $relative)
    foreach ($p in @($working,$b,$i)) { New-Item -ItemType Directory -Path (Split-Path -Parent $p) -Force | Out-Null }
    [System.IO.File]::WriteAllText($working, $normalizedCurrent, $utf8)
    [System.IO.File]::WriteAllText($b, $normalizedBase, $utf8)
    [System.IO.File]::WriteAllText($i, $normalizedIncoming, $utf8)
    if ($normalizedCurrent -ceq $normalizedBase) {
      [System.IO.File]::WriteAllText($working, $normalizedIncoming, $utf8)
      Write-Host "PREPARED (exact baseline): $relative" -ForegroundColor Green
    } else {
      & git merge-file --diff3 -- $working $b $i 2>&1 | Out-Null
      $exitCode = $LASTEXITCODE
      if ($exitCode -ne 0) {
        throw "SAFE STOP: $relative has edits that conflict with this update (merge exit $exitCode). Your source is UNCHANGED. Upload the current file for a manual merge."
      }
      Write-Host "PREPARED (merged user changes): $relative" -ForegroundColor Green
    }
    # Basic sanity: no unresolved merge markers.
    $result = [System.IO.File]::ReadAllText($working)
    if ($result.Contains('<<<<<<< ') -or $result.Contains('>>>>>>> ')) {
      throw "SAFE STOP: unresolved conflict in $relative. Your source is unchanged."
    }
  }

  if ($changed.Count -eq 0) {
    Write-Host 'All six files already contain the update. Nothing changed.' -ForegroundColor Green
    exit 0
  }
  Write-Host ''
  Write-Host 'All edits merged with no text conflicts. This is NOT a guarantee of a successful build.' -ForegroundColor Green
  Write-Host 'REQUIRED: SQL connected, SQL history already imported, and private backups of WaahReHistory + App_Data.' -ForegroundColor Yellow
  Write-Host 'IMPORTANT: This update stops re-importing old JSON indexes into SQL after deletions.' -ForegroundColor Yellow
  $confirmation = Read-Host 'After backing up, type YES to apply the prepared merged files'
  if ($confirmation -cne 'YES') { Write-Host 'Cancelled; no source changes'; exit 0 }

  $backup = Join-Path $project ('HistoryDeleteSourceBackup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
  $applied = New-Object System.Collections.Generic.List[string]
  foreach ($relative in $changed) {
    $src = Join-Path $project $relative
    $dest = Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
    Copy-Item -LiteralPath $src -Destination $dest -Force
  }
  try {
    foreach ($relative in $changed) {
      $src = Join-Path $stage ('merged/' + $relative)
      $dst = Join-Path $project $relative
      $merged = [System.IO.File]::ReadAllText($src).Replace("`r`n", "`n")
      if ($lineEndings[$relative] -eq "`r`n") { $merged = $merged.Replace("`n", "`r`n") }
      [System.IO.File]::WriteAllText($dst, $merged, $utf8)
      $applied.Add($relative)
    }
  } catch {
    foreach ($relative in $applied) { Copy-Item -LiteralPath (Join-Path $backup $relative) -Destination (Join-Path $project $relative) -Force }
    throw "Apply failed and source files were rolled back: $($_.Exception.Message)"
  }
  Write-Host "APPLIED. Original files backed up at $backup" -ForegroundColor Green
  Write-Host 'Rebuild backend and frontend; test DELETE with a synthetic resume only.' -ForegroundColor Cyan
}
finally {
  # Temporary merge candidates can contain candidate identifiers in UI source. Delete them.
  if (Test-Path $stage) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
