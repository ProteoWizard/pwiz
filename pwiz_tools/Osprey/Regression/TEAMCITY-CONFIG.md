# TeamCity config spec: "Osprey Windows .NET Regression"

Spec for the **scheduled, overnight** TeamCity build config that runs the
Osprey end-to-end regression. Separate from the per-commit
`ProteoWizard_OspreyWindowsNet` ("Osprey Windows .NET") config, which runs
`tcbuild.bat` on every smart-triggered commit. This one runs on a schedule and
exercises the full pipeline on real data.

## Build step

Single command-line step, same shape as the per-commit config:

```
pwiz_tools\Osprey\tctest.bat
```

`tctest.bat` calls `regression.ps1 -TeamCity -Dataset All`, which emits
`##teamcity[progressMessage]` and a `##teamcity[buildProblem]` on any mismatch.
A non-zero exit fails the build. It publishes **no artifacts** (see Outputs).

## VCS root

The same `ProteoWizard/pwiz` VCS root as the per-commit config. The harness is
pwiz-standalone (no `ai/` checkout required on the agent). The committed golden
lives in the repo at `pwiz_tools/Osprey/osprey-regression.data/`, so it is
checked out with the code.

## Trigger

Runs **overnight on PRs that touch Osprey** — the same model as the Skyline
Tutorial and Perf overnight tests (run long, against pending changes, not on
every push). Wired in #4283:

1. **Nightly trigger (repo).** `scripts/misc/nightly_trigger_and_paths_config.py`
   defines `targets['OspreyWindowsNetPerfRegressionTests']` →
   `ProteoWizard_OspreyWindowsNetPerfRegressionTests`, mapped from
   `pwiz_tools/Osprey/.*` in `matchPaths`. So an Osprey change enqueues
   the regression in the **nightly** pipeline (NOT the per-commit
   `vcs_trigger_and_paths_config.py`, which keeps routing to the fast per-commit
   build + test).
2. **GitHub Actions opt-out.** `.github/workflows/build_and_test.yml` adds
   `pwiz_tools/Osprey/**` to its `paths-ignore`, so the GH Actions build
   does not redundantly run on Osprey-only changes (TeamCity owns them).
3. **TeamCity config (UI).** The `ProteoWizard_OspreyWindowsNetPerfRegressionTests`
   build config runs the step below; its overnight behaviour (agent pool /
   schedule window) is a config property, like the perf/tutorial configs.

## Agent requirements

Same as the per-commit Osprey agent, plus outbound internet:

- **pwsh** (PowerShell 7+) on PATH (project standard; no `powershell.exe` fallback)
- **Visual Studio Build Tools** (MSBuild — `regression.ps1` builds Release/net8.0
  first via `build.ps1 -NoTests`; drop with `-NoBuild` if the config builds
  separately)
- **.NET 8 SDK**
- **Outbound HTTPS to `panoramaweb.org`** — first run downloads
  `osprey-testfiles-mzML.zip` (**~14 GB** — these are real DIA mzML runs, ~1.5 GB
  each) into the agent's `<Downloads>\Perftests`; subsequent runs skip the
  download if the extracted tree is present. Measured download: ~14 GB in ~130s
  on a fast pipe. A clean agent downloads every night.
  - `SKYLINE_DOWNLOAD_PATH` env var overrides the downloads folder (shared with
    Skyline perf tests). `regression.ps1 -DownloadsPath <dir>` also overrides.
- **Free disk: ~55 GB** on the downloads volume — ~14 GB zip + ~25 GB extracted
  mzML (astral ~20 GB + stellar ~4.7 GB), plus run output/caches under
  `TestResults` (the `--work-dir` per-file `.spectra.bin` + parquet + blib for 6
  files). A persistent agent that keeps the extracted tree avoids re-downloading;
  budget accordingly.
- No dotCover needed (this config does not measure coverage).

## Data acquisition

Handled entirely by the harness (no manual staging):
`osprey-testfiles-mzML.zip` from
`panoramaweb.org/.../perftests/osprey-testfiles-mzML.zip` → extracted into
`<Downloads>\Perftests\osprey-testfiles-mzML\` (`stellar\` + `astral\`
subfolders), referenced read-only. The raw-data zip (`osprey-testfiles.zip`) is
future work (reads `.raw` directly once `pwiz_data_cli` is wired in).

## Outputs / artifacts

- **No artifacts are published** (there is no Osprey install story yet, and
  the run scratch is huge). Diagnosis on a red gate comes from:
  - the **build log** — every per-file run log is Tee'd to the console TeamCity
    captures, so the full pipeline output is in the build log; and
  - the **`buildProblem`** line, which names the failing dataset + leg (mode 1 vs
    golden, or mode 2 resume self-consistency) and the first divergent columns.
- Do **not** add a config-level "Artifact paths" rule for `TestResults` — that
  would re-introduce the 4 GB-per-artifact publish failure on the multi-GB
  `.spectra.bin` scratch files. The script emits no `publishArtifacts`.
- Run scratch lands under `pwiz_tools/Osprey/TestResults/regression-<stamp>/`
  (per-run timestamped, gitignored). It holds the multi-GB spectra caches, so the
  agent should treat `TestResults` as ephemeral and clean it (e.g. a swabra /
  clean-checkout rule) to bound disk.

## Expected duration

Measured on a dev workstation (16 threads, `-NoBuild`), data already extracted:

| Dataset | cold straight-through | resume | mode-1 compare | mode-2 compare |
|---------|----------------------|--------|----------------|----------------|
| Stellar (3-file, unit) | ~4:10 | ~1:38 | ~40s | ~2:45 |
| Astral (3-file, hram)  | ~15:30 | ~2:16 | ~90s | ~6–7 min |

Stellar end-to-end ≈ **9 min**; Astral cold straight-through alone is ~18 min
(hram is much heavier). A full `-Dataset All` night is roughly **40–55 min** of
compute once the data is staged, plus the first-ever ~14 GB download/extract
(~a few min on a fast pipe; skipped thereafter on a persistent agent). The
pipeline runs dominate; the ~1–2.4 MB text-golden compares add a few minutes.

## Golden refresh (who/when)

The golden is refreshed **only on an intentional, reviewed behavior change**, by
a developer (not the nightly), via
`regression.ps1 -Dataset All -CreateGolden`, reviewing the
`osprey-regression.data/` text diff and committing it with the change. See
`README.md` in this folder.
