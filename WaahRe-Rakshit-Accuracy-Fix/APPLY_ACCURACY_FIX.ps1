$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$api = Join-Path $root 'backend/RoleFit.Api/Controllers/ResumesController.cs'
$studio = Join-Path $root 'frontend/rolefit-ui/src/components/ResumeParserPage.tsx'
$export = Join-Path $root 'frontend/rolefit-ui/src/utils/resumeExport.ts'
$helper = Join-Path $root 'backend/RoleFit.Api/Services/SourceProjectReconciler.cs'
$helperSource = Join-Path $PSScriptRoot 'files/backend/RoleFit.Api/Services/SourceProjectReconciler.cs'
$checkSource = Join-Path $PSScriptRoot 'tests/RoleFit.ProjectRecoveryChecks'
$checkTarget = Join-Path $root 'tests/RoleFit.ProjectRecoveryChecks'
$utf = New-Object System.Text.UTF8Encoding($false)

function Require-Replace([string]$text, [string]$old, [string]$new, [string]$label) {
    if (-not $text.Contains($old)) { throw "SAFE STOP: Cannot locate $label. Your source differs. Nothing changed." }
    if ($text.Split([string[]]@($old),[StringSplitOptions]::None).Length -ne 2) { throw "SAFE STOP: Ambiguous $label. Nothing changed." }
    return $text.Replace($old, $new)
}
function Read([string]$path) { return [IO.File]::ReadAllText($path) }

foreach ($f in @($api, $studio, $export, $helperSource)) {
    if (-not (Test-Path -LiteralPath $f)) { throw "SAFE STOP: Missing required file: $f. Nothing changed." }
}
if (Test-Path -LiteralPath $helper) { throw 'SAFE STOP: SourceProjectReconciler.cs already exists. Review before reinstalling.' }
if (Test-Path -LiteralPath $checkTarget) { throw 'SAFE STOP: RoleFit.ProjectRecoveryChecks already exists. Review before reinstalling.' }
if (-not (Test-Path -LiteralPath $checkSource)) { throw 'SAFE STOP: Regression test folder missing from ZIP.' }

# Stage ALL updates in memory. There are no project changes until the full precheck succeeds.
$a = Read $api
if ($a.Contains('SourceProjectReconciler.Apply(')) { throw 'SAFE STOP: Backend already contains this fix.' }
$a = Require-Replace $a 'var baseline = parser.Parse(source, file.FileName);' (
    'var baseline = parser.Parse(source, file.FileName);' + "`n" +
    '            SourceProjectReconciler.Apply(baseline, source);') 'standalone parse hook'
$a = Require-Replace $a 'var profile = await groq.EnrichAsync(baseline, source, null, false, ct);' (
    'var profile = await groq.EnrichAsync(baseline, source, null, false, ct);' + "`n" +
    '            SourceProjectReconciler.Apply(profile, source);') 'post-Groq parse hook'
$a = Require-Replace $a 'var baseline = parser.Parse(source, file.FileName, jdText);' (
    'var baseline = parser.Parse(source, file.FileName, jdText);' + "`n" +
    '            SourceProjectReconciler.Apply(baseline, source);') 'alignment parse hook'
$a = Require-Replace $a 'var aligned = await groq.EnrichAsync(baseline, source, null, false, ct);' (
    'var aligned = await groq.EnrichAsync(baseline, source, null, false, ct);' + "`n" +
    '            SourceProjectReconciler.Apply(aligned, source);') 'post-Groq alignment hook'
$a = Require-Replace $a 'var profile = await drafts.GetAsync(id, ct);' (
    'var profile = await drafts.GetAsync(id, ct);' + "`n" +
    '        if (profile is not null) SourceProjectReconciler.RemoveContact(profile);') 'saved draft privacy hook'
$a = Require-Replace $a 'profile.Status = "Edited draft - requires verification";' (
    'SourceProjectReconciler.RemoveContact(profile);' + "`n" +
    '        profile.Status = "Edited draft - requires verification";') 'save draft privacy hook'

$s = Read $studio
# Remove optional contact editor. Exact block replacement refuses unknown changes instead of guessing.
$detailsPattern = '(?s)<details className="studio-internal".*?</details>'
$hit = [regex]::Matches($s, $detailsPattern)
if ($hit.Count -gt 1) { throw 'SAFE STOP: Multiple contact editors found. Nothing changed.' }
if ($hit.Count -eq 1) {
    $s = [regex]::Replace($s, $detailsPattern, '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
}
# Remove state used only by the removed editor to keep TS noUnusedLocals clean.
$s = $s.Replace('  const [showInternal, setShowInternal] = useState(false);' + "`n", '')
$s = $s.Replace('  const [showInternal, setShowInternal] = useState(false);' + "`r`n", '')
if ($s.Contains('showInternal') -or $s.Contains('setShowInternal')) { throw 'SAFE STOP: Unexpected internal contact logic remains. Nothing changed.' }
# Protect old saved draft display in addition to the backend's sanitization.
if (-not $s.Contains('const publicDesignation =')) {
    $s = Require-Replace $s 'const wordCount =' (
      'const publicDesignation = (value: string) => /@|https?:|(?:\+\d|\b\d[\d .()\-]{8,}\d\b)/i.test(value) ? "" : value;' + "`n" + 'const wordCount =') 'designation display anchor'
}
$s = Require-Replace $s 'value={profile.designation}' 'value={publicDesignation(profile.designation)}' 'profile designation editor'

$e = Read $export
if (-not $e.Contains('const safeDesignation =')) {
    $e = Require-Replace $e 'const displayName = ' (
      'const safePublicText = (value: string) => (value || "").replace(/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/gi, "").replace(/(?:\+?\d{1,3}[\s.\-]?)?(?:\d[\s.\-]?){10,12}/g, "").replace(/\s{2,}/g, " ").trim();' + "`n" +
      'const safeDesignation = (value: string) => /@|https?:|(?:\+\d|\b\d[\d .()\-]{8,}\d\b)/i.test(value) ? "" : safePublicText(value);' + "`n" + 'const displayName = ') 'export sanitizer'
}
if (-not $e.Contains('[profile.designation, experienceText(profile)]')) { throw 'SAFE STOP: Export subtitle structure has changed. Nothing changed.' }
$e = $e.Replace('[profile.designation, experienceText(profile)]', '[safeDesignation(profile.designation), experienceText(profile)]')
$e = Require-Replace $e 'htmlEscape(profile.professionalSummary || "Professional summary not provided.")' 'htmlEscape(safePublicText(profile.professionalSummary || "Professional summary not provided."))' 'PDF summary redaction'
$e = Require-Replace $e 'htmlEscape(p.description)' 'htmlEscape(safePublicText(p.description))' 'PDF project redaction'
$e = Require-Replace $e 'run(profile.professionalSummary || "Professional summary not provided.")' 'run(safePublicText(profile.professionalSummary || "Professional summary not provided."))' 'Word summary redaction'
$e = Require-Replace $e 'paragraph(run(project.description),' 'paragraph(run(safePublicText(project.description)),' 'Word project redaction'
if ($e.Contains('[profile.designation, experienceText(profile)]')) { throw 'SAFE STOP: Export contact sanitizer could not be applied.' }

Write-Host 'PRECHECK PASSED. Project files have not yet been modified.' -ForegroundColor Green
Write-Host 'Scope: 1 new backend helper, 1 backend controller, 2 frontend source files, 1 new test project.'
Write-Host 'No changes to SQL tables, scores, Groq settings, original uploaded documents, or History.'
$approval = Read-Host 'Type YES to apply source files with backups'
if ($approval -cne 'YES') { Write-Host 'Cancelled; no files changed.'; exit 0 }
$backup = Join-Path $PSScriptRoot ('backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$targets = @(
    @{path=$api;text=$a;label='ResumesController.cs'},
    @{path=$studio;text=$s;label='ResumeParserPage.tsx'},
    @{path=$export;text=$e;label='resumeExport.ts'}
)
foreach ($t in $targets) { Copy-Item -LiteralPath $t.path -Destination (Join-Path $backup $t.label) -Force }
try {
    foreach ($t in $targets) { [IO.File]::WriteAllText($t.path, $t.text, $utf) }
    Copy-Item -LiteralPath $helperSource -Destination $helper -ErrorAction Stop
    New-Item -ItemType Directory -Path (Join-Path $root 'tests') -Force | Out-Null
    Copy-Item -LiteralPath $checkSource -Destination $checkTarget -Recurse -ErrorAction Stop
} catch {
    foreach ($t in $targets) {
        $old = Join-Path $backup $t.label
        if (Test-Path $old) { Copy-Item -LiteralPath $old -Destination $t.path -Force }
    }
    if (Test-Path $helper) { Remove-Item -LiteralPath $helper -Force }
    if (Test-Path $checkTarget) { Remove-Item -LiteralPath $checkTarget -Recurse -Force }
    throw ('Installation failed and source backups were restored. Details: ' + $_)
}
Write-Host ('SUCCESS. Source backup: ' + $backup) -ForegroundColor Green
Write-Host 'Next: dotnet build backend/RoleFit.Api/RoleFit.Api.csproj; dotnet run --project tests/RoleFit.ProjectRecoveryChecks/RoleFit.ProjectRecoveryChecks.csproj; npm.cmd run build in frontend/rolefit-ui.'
