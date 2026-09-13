<#
.SYNOPSIS
    Writes regression.html - the matrix of WHICH regression assertions run on WHICH dataset,
    what each cell costs, and why the omissions are designed - and optionally verifies it
    against a real run or renders a proposal for comparison.

.DESCRIPTION
    The gate has four datasets and a dozen modes, and the modes are gated on per-dataset spec
    keys (ModelDiagnostics, AltPass2, FdrBench, SkipModes) scattered through regression.ps1.
    Nothing showed the resulting matrix, and the recurring mistake was adding an assertion to
    every dataset when one covered its property - a 4x wall-time multiplier for no coverage.

    Two sources of truth, deliberately:

      * The DATASET specs are read from regression.ps1 itself (the `$datasets` table, via the
        PowerShell parser), so a spec key changed there changes the page.
      * The MODE -> gate map lives in this file, next to the script, because the gates are
        `if` statements no parser can classify. It can rot, so -VerifyAgainst checks it: given
        one or more run logs (a lane log, or regression.ps1's own output), every summary line
        the map predicts for a dataset must appear and nothing unpredicted may - a mismatch is
        a non-zero exit, and names the line. Run it on a green -Dataset All whenever a mode is
        added or a spec key changes, then commit the regenerated page.

    -CostsFrom reads the "Phase cost" tables the same logs print and puts the seconds in each
    cell, with per-dataset and per-lane totals, so the page also says what a cell COSTS.

.PARAMETER Script
    Path to regression.ps1 (default: the sibling of this file's parent directory).

.PARAMETER OutPath
    Where to write the HTML (default: regression.html beside regression.ps1).

.PARAMETER VerifyAgainst
    One or more run logs. Their "<Dataset> modeN (...): PASS|FAIL|SKIP" lines are compared with
    what the map predicts for each dataset the logs cover.

.PARAMETER CostsFrom
    One or more run logs whose "Phase cost" tables supply per-cell seconds.

.PARAMETER Lanes
    How regression-parallel.ps1 splits the datasets, one comma-separated list per lane
    (default: Astral alone, the three Stellar variants together). Used for the lane totals
    and the wall estimate (the wall is the slower lane).

.PARAMETER SkipModesOverride
    PROPOSAL rendering: a hashtable of dataset -> mode numbers to skip, applied on top of the
    script's specs. Cells that run today and would not are drawn as cuts with the seconds
    saved, and the totals are recomputed, so the page can be compared side by side with the
    one generated from the script as it is. Nothing in the script changes.

.PARAMETER Title
    Page title override (a proposal page should say so).

.EXAMPLE
    .\Regression\Write-RegressionMatrix.ps1 -CostsFrom TestResults\regression-lane-*.log
    .\Regression\Write-RegressionMatrix.ps1 -VerifyAgainst TestResults\regression-lane-*.log
    .\Regression\Write-RegressionMatrix.ps1 -OutPath regression-proposal.html -Title 'PROPOSAL' `
        -SkipModesOverride @{ StellarGenDecoyEntrap = @(2,3,5,7,8,9,11); Astral = @(2,5,7,8,9,11); Stellar = @(8,9) } `
        -Lanes 'Astral,StellarGenDecoyEntrap', 'Stellar,StellarLibDecoy'
#>
param(
    [string]$Script,
    [string]$OutPath,
    [string[]]$VerifyAgainst,
    [string[]]$CostsFrom,
    [string[]]$Lanes = @('Astral', 'Stellar,StellarLibDecoy,StellarGenDecoyEntrap'),
    [hashtable]$SkipModesOverride,
    [string]$Title = 'Osprey regression gate: which assertion runs on which dataset'
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $PSCommandPath
if (-not $Script) { $Script = Join-Path (Split-Path -Parent $here) 'regression.ps1' }
if (-not $OutPath) { $OutPath = Join-Path (Split-Path -Parent $Script) 'regression.html' }

# ---- The dataset specs, read from regression.ps1's own table -------------------------
function Convert-AstValue($ast) {
    if ($ast -is [System.Management.Automation.Language.ConstantExpressionAst]) { return $ast.Value }
    if ($ast -is [System.Management.Automation.Language.StringConstantExpressionAst]) { return $ast.Value }
    if ($ast -is [System.Management.Automation.Language.VariableExpressionAst]) {
        switch ($ast.VariablePath.UserPath) {
            'true'  { return $true }
            'false' { return $false }
            default { return ('$' + $ast.VariablePath.UserPath) }
        }
    }
    if ($ast -is [System.Management.Automation.Language.ArrayExpressionAst]) {
        return @($ast.SubExpression.Statements | ForEach-Object {
            $_.PipelineElements[0].Expression } | ForEach-Object { Convert-AstValue $_ })
    }
    if ($ast -is [System.Management.Automation.Language.ArrayLiteralAst]) {
        return @($ast.Elements | ForEach-Object { Convert-AstValue $_ })
    }
    if ($ast -is [System.Management.Automation.Language.ParenExpressionAst]) {
        return Convert-AstValue $ast.Pipeline.PipelineElements[0].Expression
    }
    return $ast.Extent.Text
}

$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$tokens, [ref]$errors)
if ($errors -and $errors.Count) { throw "regression.ps1 does not parse: $($errors[0].Message)" }
$assign = $ast.Find({ param($n)
    $n -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $n.Left.Extent.Text -eq '$datasets' }, $true)
if (-not $assign) { throw 'regression.ps1: no `$datasets = ...` assignment found' }
$table = $assign.Right.Find({ param($n) $n -is [System.Management.Automation.Language.HashtableAst] }, $true)
$datasets = [ordered]@{}
foreach ($kv in $table.KeyValuePairs) {
    $name = Convert-AstValue $kv.Item1
    $specAst = $kv.Item2.Find({ param($n) $n -is [System.Management.Automation.Language.HashtableAst] }, $true)
    $spec = [ordered]@{}
    foreach ($skv in $specAst.KeyValuePairs) {
        $spec[(Convert-AstValue $skv.Item1)] = Convert-AstValue $skv.Item2.PipelineElements[0].Expression
    }
    $datasets[$name] = $spec
}
# The specs as the script has them, kept for the cut marking when an override is applied.
$baseline = [ordered]@{}
foreach ($n in $datasets.Keys) { $baseline[$n] = [ordered]@{} + $datasets[$n] }
if ($SkipModesOverride) {
    foreach ($n in $SkipModesOverride.Keys) {
        if (-not $datasets.Contains($n)) { throw "SkipModesOverride names a dataset the script does not have: $n" }
        $merged = @(@($datasets[$n].SkipModes) + @($SkipModesOverride[$n]) | Where-Object { $null -ne $_ } | Sort-Object -Unique)
        $datasets[$n].SkipModes = $merged
    }
}

# ---- The modes and what gates them (maintained HERE; verified by -VerifyAgainst) -----
# Lines = the summary labels the mode emits, minus the "<Dataset> " prefix. When = the spec
# predicate. Skip = labels that appear as an explicit SKIP line when When is false (mode 2's
# streamed-join assertion is the one case: it announces the leg it could not run). Cost = the
# mode number the "Phase cost" table files the leg's seconds under (a sub-row shares its
# parent's leg and has no cost of its own).
function Skipped($s, [int]$mode) { return (@($s.SkipModes) -contains $mode) }
$modes = @(
    @{ Id = '1';  Cost = '1'; Title = 'straight-through vs committed golden'
       Proves = 'The user-facing answer: Stage 7 protein-FDR dump, a deterministic precursor subset and the full-set summary at 1e-9 against osprey-regression.data. The straight-through run every other leg builds on.'
       Lines = @('mode1 (vs golden)'); When = { param($s) $true }; Gate = 'every dataset' }
    @{ Id = '1b'; Title = 'diagnostics report vs golden + FDR sanity bounds'
       Proves = 'The --model-diagnostics report matches its golden, and the tier-2 bounds (MaxPass1Fdp / MaxAbsTilt / CoinTolerance) hold. Free: a check on the straight run''s output.'
       Lines = @('mode1b (diagnostics vs golden)', 'mode1b (FDR sanity bounds)'); When = { param($s) [bool]$s.ModelDiagnostics }; Gate = 'ModelDiagnostics' }
    @{ Id = '1c'; Title = '2nd-pass sidecar carries a second-pass protein q'
       Proves = 'The pass-2 experiment sidecar''s protein q moved from pass 1 for shared records, and no gap-fill record is missing from pass 1. Free.'
       Lines = @('mode1c (2nd-pass protein q is pass-2)'); When = { param($s) $true }; Gate = 'every dataset' }
    @{ Id = '3';  Cost = '3'; Title = 'HPC 4-task worker chain == straight-through'
       Proves = 'PerFileScoring -> FirstPassFDR -> PerFileRescoring -> SecondPassFDR across process boundaries reproduces the straight-through blib at 1e-9, per-file sidecars match, the worker answer is folded, and the join streams.'
       Lines = @('mode3 (per-file FDR sidecars==straight)', 'mode3 (shipped fold)', 'mode3 (streamed join)', 'mode3 (verifier split)', 'mode3 (per-run hydrate)', 'mode3 (HPC chain==straight)')
       When = { param($s) -not (Skipped $s 3) }; Gate = 'not in SkipModes (-SkipHpcChain turns it off everywhere)' }
    @{ Id = '3+'; Title = 'chain report is two-pass'
       Proves = 'The HPC chain''s diagnostics report carries both passes.'
       Lines = @('mode3 (chain report is two-pass)'); When = { param($s) [bool]$s.ModelDiagnostics -and -not (Skipped $s 3) }; Gate = 'ModelDiagnostics and mode 3' }
    @{ Id = '10'; Cost = '10'; Title = 'alternate pass-2 arm (mean-best-2) runs and produces'
       Proves = 'The non-default pass-2 arm is reachable and writes output; ONE dataset carries it by design (see AltPass2 in the spec table).'
       Lines = @('mode10 (meanbest2 arm runs and produces)'); When = { param($s) [bool]$s.AltPass2 }; Gate = 'AltPass2' }
    @{ Id = '4';  Cost = '4'; Title = 'warm re-run: every task reports a cache hit'
       Proves = 'An identical second invocation runs no task and rewrites nothing - the only leg that can see a cache-invalidation regression. Seconds.'
       Lines = @('mode4 (warm re-run all cached)'); When = { param($s) $true }; Gate = 'every dataset (-SkipWarmRerun)' }
    @{ Id = '2';  Cost = '2'; Title = 'resume == straight-through'
       Proves = 'Invalidate the Stage 5 join + blib, re-run the same command: the tasks that must recompute do, the rest cache-hit, and the resume blib equals the straight-through one at 1e-9.'
       Lines = @('mode2 (resume cache hits)', 'mode2 (resume==straight)'); When = { param($s) -not (Skipped $s 2) }; Gate = 'not in SkipModes' }
    @{ Id = '12'; Title = '--fdrbench-pass both writes both FDRBench files'
       Proves = 'bench.pass1.tsv and bench.pass2.tsv both exist beside their pairing manifests, and pass 1 (pre-compaction) has strictly more rows than pass 2 (reported set). Issue #4507. Rides the straight leg.'
       Lines = @('mode12 (fdrbench both files)'); When = { param($s) [bool]$s.FdrBench }; Gate = 'FdrBench' }
    @{ Id = '12+'; Title = 'resume rewrites both FDRBench files identically'
       Proves = 'The second half of mode 12; rides the mode-2 resume.'
       Lines = @('mode12 (resume fdrbench==straight)'); When = { param($s) [bool]$s.FdrBench -and -not (Skipped $s 2) }; Gate = 'FdrBench and mode 2' }
    @{ Id = '5';  Cost = '5'; Title = 'Stage-5 rehydrate (own-sidecar loader) == straight-through'
       Proves = 'Invalidate only SecondPassFDR: the rehydrate arm builds its bundle from this run''s OWN sidecars (a marker from inside the loader proves it) and the blib still equals the straight-through one.'
       Lines = @('mode5 (rehydrate entered + cache hits)', 'mode5 (rehydrate==straight)'); When = { param($s) -not (Skipped $s 5) }; Gate = 'not in SkipModes (-SkipRehydrate)' }
    @{ Id = '5+'; Title = 'rehydrated diagnostics vs golden + FDR sanity bounds'
       Proves = 'The report re-emitted from the rehydrated sidecars matches the golden and the bounds.'
       Lines = @('mode5 (rehydrate diagnostics vs golden)', 'mode5 (rehydrate FDR sanity bounds)'); When = { param($s) [bool]$s.ModelDiagnostics -and -not (Skipped $s 5) }; Gate = 'ModelDiagnostics and mode 5' }
    @{ Id = 'S7'; Title = 'streamed Stage-7 join on every leg (modes 1, 2, 5)'
       Proves = 'Each leg''s log shows the per-run fold and no all-runs survivor pool - the O(files) resident join must not come back silently. Free: log checks on legs that ran.'
       Lines = @('mode1 (streamed join)'); When = { param($s) $true }; Gate = 'every dataset; the mode-2 and mode-5 lines follow those modes'
       Skip = @{ 'mode2 (streamed join)' = { param($s) -not (Skipped $s 2) } }
       Extra = @{ 'mode5 (streamed join)' = { param($s) -not (Skipped $s 5) } } }
    @{ Id = '6';  Title = 'library-fragment release engaged'
       Proves = 'The release RAN on every leg that holds the library and did NOT run on --task FirstPassFDR; output-neutral by design, so only the logs can see it. Free.'
       Lines = @('mode6 (library-fragment release engaged)'); When = { param($s) $true }; Gate = 'every dataset' }
    @{ Id = '7';  Cost = '7'; Title = '--task ModelDiagnostics regeneration'
       Proves = 'Re-entering a completed run changes exactly one artifact (the report) and it still matches the golden.'
       Lines = @('mode7 (diagnostics regeneration: report only, vs golden)'); When = { param($s) [bool]$s.ModelDiagnostics -and -not (Skipped $s 7) }; Gate = 'ModelDiagnostics, not in SkipModes' }
    @{ Id = '11'; Cost = '11'; Title = 'pay-later diagnostics: folded, no analysis, same report'
       Proves = 'With both diagnostics products deleted, asking for the report folds it from the sidecars, runs no analysis, and produces the byte-identical page.'
       Lines = @('mode11 (pay-later diagnostics: folded, no analysis, same report)'); When = { param($s) [bool]$s.ModelDiagnostics -and -not (Skipped $s 11) }; Gate = 'ModelDiagnostics, not in SkipModes' }
    @{ Id = '8';  Cost = '8'; Title = 'partial rescore resume'
       Proves = 'A rescore killed part-way resumes and finishes, re-scoring only the outstanding runs; on a ModelDiagnostics dataset the --model-diagnostics arm also reports its capability gap.'
       Lines = @('mode8 (partial rescore resume)'); When = { param($s) -not (Skipped $s 8) }; Gate = 'not in SkipModes' }
    @{ Id = '9';  Cost = '9'; Title = 'crash-shaped half-done resume'
       Proves = 'A file with one of its two rescore products missing is re-scored, not treated as done.'
       Lines = @('mode9 (crash-shaped half-done resume)'); When = { param($s) -not (Skipped $s 9) }; Gate = 'not in SkipModes' }
)

function Expected-Lines($m, $spec) {
    # label -> RUN | SKIP | ABSENT
    $e = @{}
    $on = & $m.When $spec
    foreach ($l in $m.Lines) { $e[$l] = if ($on) { 'RUN' } else { 'ABSENT' } }
    if ($m.Skip) { foreach ($k in $m.Skip.Keys) { $e[$k] = if (& $m.Skip[$k] $spec) { 'RUN' } else { 'SKIP' } } }
    if ($m.Extra) { foreach ($k in $m.Extra.Keys) { $e[$k] = if (& $m.Extra[$k] $spec) { 'RUN' } else { 'ABSENT' } } }
    return $e
}

# ---- Costs from the "Phase cost" tables ------------------------------------------------
$costs = @{}   # "$ds|$mode" -> seconds
if ($CostsFrom) {
    foreach ($pattern in $CostsFrom) {
        foreach ($file in Get-ChildItem $pattern -File) {
            foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
                if ($line -match '^\s*([0-9.,]+)s\s+[0-9.]+%\s+(\S+): (.*)$') {
                    $secs = [double]($Matches[1] -replace ',', ''); $ds = $Matches[2]; $label = $Matches[3]
                    $mode = if ($label -match '\(mode (\d+[a-c]?)\)') { $Matches[1] }
                            elseif ($label -match 'straight-through run') { '1' }
                            else { $null }
                    if ($mode) { $costs["$ds|$mode"] = [double]($costs["$ds|$mode"]) + $secs }
                }
            }
        }
    }
}
function Cell-Cost([string]$ds, $m) {
    if (-not $m.Cost) { return $null }
    $k = "$ds|$($m.Cost)"
    if ($costs.ContainsKey($k)) { return [double]$costs[$k] } else { return $null }
}

# ---- Optional verification against real run logs -------------------------------------
$verifyReport = @()
$verifyFailed = $false
if ($VerifyAgainst) {
    $seen = @{}
    foreach ($pattern in $VerifyAgainst) {
        foreach ($file in Get-ChildItem $pattern -File) {
            foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
                if ($line -match '^\s*(\S+) (mode\S+ \([^)]*\)): (PASS|FAIL|SKIP)') {
                    $ds = $Matches[1]; $label = $Matches[2]; $verdict = $Matches[3]
                    if (-not $seen.ContainsKey($ds)) { $seen[$ds] = @{} }
                    $seen[$ds][$label] = $verdict
                }
            }
        }
    }
    foreach ($ds in $seen.Keys) {
        if (-not $datasets.Contains($ds)) { $verifyReport += "$ds : in the log but not in the spec table"; $verifyFailed = $true; continue }
        $expected = @{}
        foreach ($m in $modes) { $e = Expected-Lines $m $datasets[$ds]; foreach ($k in $e.Keys) { $expected[$k] = $e[$k] } }
        foreach ($label in $expected.Keys) {
            $want = $expected[$label]; $got = $seen[$ds][$label]
            $ok = switch ($want) {
                'RUN'    { $got -eq 'PASS' -or $got -eq 'FAIL' }
                'SKIP'   { $got -eq 'SKIP' }
                'ABSENT' { $null -eq $got }
            }
            if (-not $ok) { $verifyReport += ("{0} : {1} - matrix says {2}, log has {3}" -f $ds, $label, $want, ($(if ($got) { $got } else { 'nothing' }))); $verifyFailed = $true }
        }
        foreach ($label in $seen[$ds].Keys) {
            if (-not $expected.ContainsKey($label)) { $verifyReport += "$ds : $label - in the log but no mode in the matrix emits it"; $verifyFailed = $true }
        }
        $verifyReport += ("{0} : {1} summary line(s) checked" -f $ds, $seen[$ds].Count)
    }
}

# ---- Render ------------------------------------------------------------------------------
function Esc([string]$s) { [System.Net.WebUtility]::HtmlEncode($s) }
function Show($v) {
    if ($null -eq $v) { return '' }
    if ($v -is [bool]) { return $(if ($v) { 'yes' } else { 'no' }) }
    if ($v -is [array]) { return ($v -join ', ') }
    return [string]$v
}
function Secs([double]$s) { if ($s -ge 100) { return ('{0:N0} s' -f $s) } else { return ('{0:N1} s' -f $s) } }
$names = @($datasets.Keys)
$commit = try { (& git -C (Split-Path -Parent $Script) rev-parse --short HEAD 2>$null) } catch { '' }
$isProposal = [bool]$SkipModesOverride
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<!DOCTYPE html>')
[void]$sb.AppendLine("<html lang=`"en`"><head><meta charset=`"utf-8`"><title>$(Esc $Title)</title>")
[void]$sb.AppendLine(@'
<style>
body { font-family: Segoe UI, system-ui, sans-serif; font-size: 14px; margin: 24px auto; max-width: 1240px; color: #222; background: #fff; }
h1 { font-size: 22px; margin: 0 0 4px; } h2 { font-size: 17px; margin: 28px 0 8px; }
p, li { line-height: 1.45; } .muted { color: #666; } small { color: #666; }
table { border-collapse: collapse; margin: 8px 0 16px; } th, td { border: 1px solid #d0d0d0; padding: 5px 9px; vertical-align: top; text-align: left; }
th { background: #f3f3f3; font-weight: 600; } td.c { text-align: center; }
td.run { background: #e8f5e9; } td.off { background: #f7f7f7; color: #999; } td.cut { background: #fdecea; color: #b71c1c; text-decoration: line-through; }
td.tot { font-weight: 600; text-align: center; }
.rule { border-left: 4px solid #c62828; background: #fff5f5; padding: 10px 14px; margin: 12px 0; }
.prop { border-left: 4px solid #1565c0; background: #eef4fb; padding: 10px 14px; margin: 12px 0; }
.ok { color: #2e7d32; } .bad { color: #c62828; } code { background: #f2f2f2; padding: 1px 4px; }
tr.mode td:first-child { white-space: nowrap; font-weight: 600; }
</style></head><body>
'@)
[void]$sb.AppendLine("<h1>$(Esc $Title)</h1>")
[void]$sb.AppendLine(('<p class="muted">Generated {0} from <code>regression.ps1</code>{1} by <code>Regression\Write-RegressionMatrix.ps1</code>. The dataset specs are read from the script; the mode table is maintained in the generator and verified against run logs with <code>-VerifyAgainst</code>.{2}</p>' -f (Get-Date -Format 'yyyy-MM-dd HH:mm'), $(if ($commit) { " @ <code>$commit</code>" } else { '' }), $(if ($CostsFrom) { ' Cell seconds are from the run log(s) named on the command line.' } else { '' })))
if ($isProposal) {
    $ov = ($SkipModesOverride.Keys | Sort-Object | ForEach-Object { "<code>$(Esc $_)</code>: skip modes $(@($SkipModesOverride[$_]) -join ', ')" }) -join '; '
    [void]$sb.AppendLine("<div class=`"prop`"><b>PROPOSAL, not what the gate runs today.</b> Rendered with <code>-SkipModesOverride</code> on top of the script's specs - $ov. Struck-through cells run today and would stop; their seconds are what each cut saves. Nothing in <code>regression.ps1</code> has changed.</div>")
}
[void]$sb.AppendLine(@'
<div class="rule"><b>Before adding an assertion, pick its ONE dataset.</b> The four datasets are two acquisitions searched four ways, not four acquisitions. A new leg or check applied to every column inherits a 4x wall-time multiplier for no extra coverage unless the property genuinely differs by dataset. Gate it on a spec key (<code>ModelDiagnostics</code>, <code>AltPass2</code>, <code>FdrBench</code>, <code>SkipModes</code>, or a new one), give that key to the dataset that exercises every branch of the property, and emit no line on the others - a designed omission is not a SKIP. Then regenerate this page and run <code>-VerifyAgainst</code> on a green run.</div>
'@)

# Dataset properties
[void]$sb.AppendLine('<h2>The datasets</h2><table><tr><th>spec key</th>')
foreach ($n in $names) { [void]$sb.Append("<th>$(Esc $n)</th>") }
[void]$sb.AppendLine('</tr>')
$keys = [System.Collections.Generic.List[string]]::new()
foreach ($n in $names) { foreach ($k in $datasets[$n].Keys) { if (-not $keys.Contains($k)) { $keys.Add($k) } } }
foreach ($k in $keys) {
    [void]$sb.Append("<tr><td><code>$(Esc $k)</code></td>")
    foreach ($n in $names) {
        $v = Show $datasets[$n][$k]
        $changed = $isProposal -and $k -eq 'SkipModes' -and ((Show $baseline[$n][$k]) -ne $v)
        [void]$sb.Append(("<td{0}>{1}</td>" -f $(if ($changed) { ' style="background:#eef4fb;font-weight:600"' } else { '' }), (Esc $v)))
    }
    [void]$sb.AppendLine('</tr>')
}
[void]$sb.AppendLine('</table>')
[void]$sb.AppendLine('<p class="muted">Blank = key not set, i.e. the default: generated decoys, no entrapment, no diagnostics report, no alternate pass-2 arm, no FDRBench files, every mode. <code>StripDecoys</code> takes the library-decoy file and removes its decoy rows, so Osprey generates decoys while the entrapment peptides stay - the only dataset that can measure a decoy-construction regression against a true-FDP oracle. What each dataset is FOR: Stellar = the default product path (generated decoys, unit resolution); StellarLibDecoy = library-supplied decoys and their pairing manifest (a different Stage-6 pairing path), plus the alternate pass-2 arm; StellarGenDecoyEntrap = the decoy-construction oracle; Astral = hram scoring and the gap-fill rows only hram produces.</p>')

# Matrix
[void]$sb.AppendLine('<h2>The matrix</h2><table><tr><th>mode</th><th>what it proves</th><th>gate</th>')
foreach ($n in $names) { [void]$sb.Append("<th>$(Esc $n)</th>") }
[void]$sb.AppendLine('</tr>')
$dsTotal = @{}; $dsSaved = @{}
foreach ($n in $names) { $dsTotal[$n] = 0.0; $dsSaved[$n] = 0.0 }
foreach ($m in $modes) {
    [void]$sb.Append(("<tr class=`"mode`"><td>{0}<br><span class=`"muted`" style=`"font-weight:normal`">{1}</span></td><td>{2}</td><td><code>{3}</code></td>" -f (Esc $m.Id), (Esc $m.Title), (Esc $m.Proves), (Esc $m.Gate)))
    foreach ($n in $names) {
        $on = & $m.When $datasets[$n]
        $was = & $m.When $baseline[$n]
        $cost = Cell-Cost $n $m
        $lines = ($m.Lines -join "&#10;")
        if ($on) {
            if ($null -ne $cost) { $dsTotal[$n] += $cost }
            $text = if ($null -ne $cost) { "&#10003; <small>$(Secs $cost)</small>" } else { '&#10003;' }
            [void]$sb.Append(("<td class=`"c run`" title=`"{0}`">{1}</td>" -f (Esc $lines), $text))
        } elseif ($was) {
            if ($null -ne $cost) { $dsSaved[$n] += $cost }
            $text = if ($null -ne $cost) { "cut <small>&minus;$(Secs $cost)</small>" } else { 'cut' }
            [void]$sb.Append(("<td class=`"c cut`" title=`"runs today: {0}`">{1}</td>" -f (Esc $lines), $text))
        } else {
            [void]$sb.Append(("<td class=`"c off`" title=`"{0}`">&mdash;</td>" -f (Esc $lines)))
        }
    }
    [void]$sb.AppendLine('</tr>')
}
if ($CostsFrom) {
    [void]$sb.Append('<tr><td colspan="3" class="tot" style="text-align:right">seconds per dataset (legs with a cost)</td>')
    foreach ($n in $names) {
        [void]$sb.Append(("<td class=`"tot`">{0}{1}</td>" -f (Secs $dsTotal[$n]), $(if ($isProposal -and $dsSaved[$n] -gt 0) { "<br><small class=`"bad`">&minus;$(Secs $dsSaved[$n])</small>" } else { '' })))
    }
    [void]$sb.AppendLine('</tr>')
}
[void]$sb.AppendLine('</table>')
[void]$sb.AppendLine('<p class="muted">&#10003; = the mode''s summary line(s) appear for that dataset; &mdash; = a designed omission, no line at all. Hover a cell for the exact summary labels. Mode 2''s streamed-join assertion is the one that prints an explicit SKIP where mode 2 does not run (Astral), because it names a leg that exists elsewhere. Seconds are the leg''s phase cost from the named run; free checks ride a leg that is already counted.</p>')

# Lanes
if ($CostsFrom) {
    [void]$sb.AppendLine('<h2>Lanes and the wall</h2><p><code>regression-parallel.ps1</code> runs the lanes concurrently; the wall is the slower lane. Per-leg seconds add up to slightly less than a lane''s wall (the build, data staging and golden compares are outside the legs).</p><table><tr><th>lane</th><th>datasets</th><th>seconds</th>')
    if ($isProposal) { [void]$sb.Append('<th>saved</th>') }
    [void]$sb.AppendLine('</tr>')
    $laneMax = 0.0
    $i = 0
    foreach ($lane in $Lanes) {
        $i++
        $members = @($lane -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        $sum = 0.0; $saved = 0.0
        foreach ($mbr in $members) { if ($dsTotal.ContainsKey($mbr)) { $sum += $dsTotal[$mbr]; $saved += $dsSaved[$mbr] } }
        if ($sum -gt $laneMax) { $laneMax = $sum }
        [void]$sb.Append(("<tr><td>lane {0}</td><td>{1}</td><td class=`"tot`">{2} ({3:N0} min)</td>" -f $i, (Esc ($members -join ', ')), (Secs $sum), ($sum / 60)))
        if ($isProposal) { [void]$sb.Append(("<td class=`"tot bad`">&minus;{0}</td>" -f (Secs $saved))) }
        [void]$sb.AppendLine('</tr>')
    }
    [void]$sb.AppendLine(("<tr><td colspan=`"2`" class=`"tot`" style=`"text-align:right`">wall (slower lane)</td><td class=`"tot`">{0} ({1:N0} min)</td>{2}</tr></table>" -f (Secs $laneMax), ($laneMax / 60), $(if ($isProposal) { '<td></td>' } else { '' })))
}

# Counts per dataset
[void]$sb.AppendLine('<h2>Summary lines per dataset</h2><p>A green <code>-Dataset All</code> prints exactly these many <code>&lt;Dataset&gt; modeN (...): PASS</code> lines (SKIP counted). A short count is what distinguishes an aborted run.</p><table><tr>')
foreach ($n in $names) { [void]$sb.Append("<th>$(Esc $n)</th>") }
[void]$sb.AppendLine('</tr><tr>')
foreach ($n in $names) {
    $count = 0
    foreach ($m in $modes) { $e = Expected-Lines $m $datasets[$n]; foreach ($k in $e.Keys) { if ($e[$k] -ne 'ABSENT') { $count++ } } }
    [void]$sb.Append("<td class=`"c`">$count</td>")
}
[void]$sb.AppendLine('</tr></table>')

# Verification
if ($VerifyAgainst) {
    [void]$sb.AppendLine('<h2>Verification</h2>')
    [void]$sb.AppendLine(('<p class="{0}">{1}</p>' -f $(if ($verifyFailed) { 'bad' } else { 'ok' }), $(if ($verifyFailed) { 'MISMATCH between this matrix and the run log(s) - fix the mode table in Write-RegressionMatrix.ps1 or the spec, then regenerate.' } else { 'Every summary line the matrix predicts appeared in the run log(s), and nothing unpredicted did.' })))
    [void]$sb.AppendLine('<ul>')
    foreach ($r in $verifyReport) { [void]$sb.AppendLine("<li>$(Esc $r)</li>") }
    [void]$sb.AppendLine('</ul>')
}
[void]$sb.AppendLine('</body></html>')
[System.IO.File]::WriteAllText($OutPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
"wrote $OutPath"
if ($VerifyAgainst) {
    $verifyReport | ForEach-Object { $_ }
    if ($verifyFailed) { exit 1 }
}
