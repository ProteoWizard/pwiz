# Spec: Quasar, Spectrum-Centric Search of Unassigned MS2 Signal

Nov 26, 2026 · @Michael MacCoss

## Summary and goals

We will build a spectrum-centric companion to Osprey, **Quasar**, that turns the peptide-like MS2 signal no document peptide explains into deconvoluted, background-subtracted pseudo-spectra, searches them with semi-tryptic, non-specific and modified peptides allowed, and returns an FDR-controlled blib that Skyline and Osprey can use directly.

Glimpse showed the size of the gap: on a 174,000-spectrum Astral Zoom DIA run, 38% of MS2 signal is peptide-like by Hardklor and Kronik, and much of it is not assigned to any peptide in the document. Osprey is peptide-centric, so it can only find what is in its library. Semitryptic and nonspecific peptides are the first thing we expect to find in that gap.

Goals:

1. **Find** the elution profiles of the MS2 Hardklor features in each window as shared templates, then **cluster** every MS2 trace a template explains, splitting fragments that co-eluting analytes share.
2. **Model** elution with exponentially modified Gaussians (EMGs) fitted to all of a cluster's anchors at once, and set each cluster's peak boundaries with Osprey's median CWT consensus.
3. **Write** one deconvoluted, background-subtracted pseudo-spectrum per cluster to mzML, with peak areas as intensities (datum's integration model).
4. **Search** the pseudo-spectra with a .NET port of Sage, with semi-enzymatic and nonspecific digestion.
5. **Control FDR** with Osprey's native Percolator and target-decoy competition, then write a blib whose spectra are the observed cluster areas and whose boundaries are the median CWT boundaries. The same peptides become an add-on library that Osprey searches beside its unmodified library (Stage 7).

Non-goals for version 1: open modification search, DDA data, and replacing Osprey's peptide-centric quantification. The blib is meant to feed Osprey as a library, so quantification across runs stays in Osprey.

## Existing components and what we reuse

Most of the hard parts already exist in C#. The new code is the trace builder, the templates and the window fit, the mzML writer and the Sage port.

| Component | Where | What we take | Change needed |
| --- | --- | --- | --- |
| Hardklor + Kronik (C# port) | [skyline-glimpse](https://github.com/maccoss/skyline-glimpse) `src/Glimpse.Features` | MS1 and MS2 isotope envelopes, linked per DIA window into persistent features; the `run.hk` Parquet cache (scan, rt, series, mono\_mass, charge, intensity, feature id) | Port `Glimpse.Features` into `pwiz_tools/Quasar/Quasar.Features` so Quasar does not depend on the WPF app; Glimpse references it there later |
| Signal assignment | Glimpse `Glimpse.Core/Assignment` | Which MS2 peaks document peptides claim inside their integrated boundaries, each peak once | Used for the Gain metric's each-peak-once accounting. `--features unassigned` does not mask: Osprey's precursors become known templates (Stage 3a) |
| Median CWT consensus | [Osprey](https://github.com/ProteoWizard/pwiz/tree/master/pwiz_tools/Osprey) `Osprey.Chromatography/CwtPeakDetector.cs` | `DetectConsensusPeaks`: Mexican hat at the median-FWHM scale, pointwise median across traces, zero-crossing ±2σ boundaries with the valley guard | None for the call; we pass cluster members in place of library fragments |
| Peak and background model | [datum](https://github.com/maccoss/datum) `Datum.Core` | `EmgFit` (fit, unit density, height crossings), `ConsensusEmgIntegrator` (shared shape, median amplitude), per-trace linear baseline subtraction | Ported into `Quasar.Core/Elution` (fit, consensus integrator, baseline, held to datum's values); add the pooled template fit (Stage 2d) and the fixed-shape window fit, the `FixedShapeEmgIntegrator` the datum design note describes (Stage 3a) |
| Percolator | Osprey `Osprey.FDR` | Native linear SVM, Granholm calibration, PEP, q-values at precursor and peptide level, protein parsimony | A new entry adapter: Sage PSM features in place of Osprey's 41-feature coelution vector |
| blib writer | `pwiz_tools/Shared/BiblioSpec/BiblioSpec.IO` | `BlibWriter`, the schema Osprey and CarafeSharp share; `RetentionTimes` rows carry per-run boundaries | A Quasar adapter like `OspreyBlibWriter`, writing observed peaks rather than library fragments |
| RT and spectrum prediction | [CarafeSharp](https://github.com/ProteoWizard/pwiz/tree/master/pwiz_tools/CarafeSharp) | Predicted RT and fragment intensities for candidate peptides, used as rescoring features | Batch prediction for search candidates (not a FASTA-wide library) |
| Sage | [lazear/sage](https://github.com/lazear/sage) (Rust, MIT) | Fragment index, semi-enzymatic digestion, wide-window search, hyperscore and PSM features | Port to .NET. No public port exists; Osprey's PEP code is already Sage-derived |

**Where the code lives.** Quasar is a new tool in `pwiz_tools/Quasar`, laid out like Osprey: a CLI, `Quasar.Core`, the trace, EMG and clustering code, `Quasar.Tasks` for the HPC tasks, and tests. SageSharp is a project inside Quasar (`Quasar.SageSharp`), not a separate tool. Quasar references `Osprey.Chromatography`, `Osprey.FDR`, `Osprey.IO` and `Shared/BiblioSpec` directly. Glimpse's Hardklor and Kronik code moves into `pwiz_tools/Quasar/Quasar.Features` so Quasar and Glimpse share one copy; Glimpse's Kronik was itself ported from `pwiz_tools/BullseyeSharp`.

## Pipeline overview

```
 raw / mzML ─┐
 run.hk ─────┼─> 1 Traces ─> 2 Templates ─> 3 Window fit ─> 4 Pseudo-spectrum mzML
 Osprey blib ┘                (anchors)     (clusters, boundaries, areas)         │
                                                                                  v
 Osprey <── 7 Add-on library <── 6 Percolator FDR + blib <── 5 SageSharp search <─┘
```

Stages 1 to 4 turn raw MS2 signal into one pseudo-spectrum per co-eluting cluster; Stages 5 and 6 identify them and write a blib that Osprey searches in every run. Each stage writes its own file, so each can run as a separate HPC task.

## Running with Osprey

Quasar is meant to run after Osprey on the same runs. It reuses what Osprey has already measured about each run rather than estimating it again, and it can search either what Osprey missed or everything.

**Two scopes.** `--features unassigned` (the default) searches the MS2 signal Osprey's passing precursors do not explain: they enter the window fit as known templates (Stage 3a), so the signal they account for is taken out before new clusters form. `--features all` searches all of it: this is the positive control, and the mode for a study searched with Quasar alone.

**What Quasar reads from an Osprey search:**

| From Osprey | File | Used for |
| --- | --- | --- |
| MS1 and MS2 mass calibration, including the MS2 m/z offset curve | `<stem>.calibration.json` | Corrects every centroid before traces are linked and before pseudo-spectra are written; SageSharp's precursor and fragment tolerances are set from the calibrated error spread, not a fixed 10 ppm |
| Average peak width | Osprey's run-level CWT scale (median FWHM) | Sets the σ range for the templates and the EMG dictionary (Stage 2a), the apex tolerance for grouping anchors (Stage 2d) and the median CWT scale; Stage 2a then only refines it |
| RT calibration (library RT to run RT, LOESS) | `<stem>.calibration.json` | Puts CarafeSharp's predicted RT on the run's time axis for the RT feature (Stage 5c) |
| Cross-run RT alignment (consensus library RT space) | Osprey's consensus RTs | The shared RT space for Quasar's own reconciliation (Stage 6d) |
| Passing precursors, their matched fragments and boundaries | Osprey's output blib | Known templates for `--features unassigned` (Stage 3a) |

**Without Osprey.** Quasar can also run alone. It then measures the mass error and peak width from its Hardklor anchors, and fits its own RT calibration from its first-pass tryptic PSMs at 1% FDR. The rest of the pipeline is the same.

## Stage 1: Feature input and traces

Stage 1 turns one DIA run into a set of MS2 traces per isolation window, each tagged as a Hardklor anchor or a plain centroid.

**Inputs.** The raw file or mzML; its `run.hk` from Glimpse (made here if absent); and optionally an Osprey search, whose passing precursors become known templates in Stage 3.

**Staggered and overlapping windows.** These runs are demultiplexed first with Osprey's demux, and Quasar reads the demultiplexed data. Each window series then holds non-overlapping windows, so every trace belongs to one window.

**Two modes.** The mode is a command-line switch. Stage 1 builds the same traces in both; the modes differ only in Stage 3.

- `--features unassigned` (default): Osprey's passing precursors are loaded as known templates (Stage 3a). Nothing is removed from the traces. The window fit explains the precursors' signal, including their share of fragments they share with other peptides, and new clusters form from what is left. Deleting the peaks they claim would instead lose a shared fragment for the unknown peptide, and the deleted points would look like real gaps to the CWT and to the fits.
- `--features all`: no known templates. This is the positive control: Quasar should re-find the peptides Osprey already found, with matching boundaries and areas.

**Traces.** Within each window series (Glimpse's `series`, one per DIA isolation window):

1. **Anchor traces** come from MS2 Kronik features: one trace per feature, its points the feature's envelope intensities scan by scan. Each anchor carries a monoisotopic mass and charge, so its m/z in the pseudo-spectrum is the singly charged monoisotopic m/z. This is the deconvolution.
2. **Centroid traces** come from every remaining MS2 centroid (singly charged fragments with no resolvable envelope, y1 and y2 ions, immonium ions). Peaks are linked across scans at a ppm tolerance (default 10 ppm, Kronik's), allowing one missed scan, as Kronik links envelopes. Isotope peaks already inside an anchor's envelope are removed so a fragment is not counted twice.
3. Every trace is put on the window's full scan axis with zeros where it is absent, which is what `CwtPeakDetector` expects (equal-length XICs).
4. Every point also carries its ion count, for the noise model (Stage 2b).

**Size.** An Astral run has thousands of envelopes per scan. To keep this tractable, traces shorter than 3 scans or below a per-window intensity floor (a quantile of the window's centroid intensities, default the 20th) are dropped before Stage 2. The floor is a setting, because the low-abundance tail is where the unassigned peptides are likely to be.

## Stage 2: Elution templates

Stage 2 finds the elution profiles in each window, using only the anchors. Each profile becomes a **template**: one EMG shape that all the members of a cluster share. Stage 3 then fits every trace in the window to the templates, and that fit decides which traces belong to which cluster. A cluster is a template and its members.

Templates come first, instead of modeling each trace on its own and then comparing the models, because the data cannot support per-trace models. On the Astral HeLa runs each 3 Th window is sampled about every 1.1 s (167 windows; measured on file 55), so a peak a few seconds wide has only about 3 to 5 points above half height in any one window. A four-parameter EMG fitted to one trace on so few points is poorly determined, τ especially, and two noisy fits cannot be compared reliably. A template is fitted to the points of all its anchors at once, so it is well determined even when no single trace is. This is also what DIA offers that DDA does not: every fragment of a peptide is sampled on the same time axis, so the fragments can be modeled together.

### 2a. Run-level shape priors

When Osprey has searched the run, start from its peak width (see Running with Osprey). Otherwise, or to refine it, use the anchor traces (they are the cleanest signal) to measure FWHM and asymmetry over the run with Osprey's `EstimateScale` logic. This gives the run's σ range (in scans, clamped to Osprey's 2 to 20) and a τ/σ range. These bound the template fits (2d) and the dictionary (2c), so both fit the column and gradient of each run.

### 2b. Noise model

Every point gets a variance, so the fits in Stages 2 and 3 can weight their points and decide membership by how well a model explains the data, instead of by fixed correlation thresholds.

On HRAM data, intensities are converted to ion counts with Glimpse's ion conversion (intensity × injection time / 1000, divided by the analyzer's α). A point's variance is its expected count (counting noise) plus a baseline term measured per window from the trace values outside peaks. The model is checked on the data: after weighting, the residuals of good fits should have about unit variance, and if they do not, the variance is scaled per run. Where the ion conversion is not available, as on unit-resolution data, the variance comes from the intensity-histogram noise model (see Unit resolution data).

The test this supports degrades the way it should. A weak fragment with few ions needs a cleaner match to join a cluster than a strong one does, but it is not rejected just because a Pearson correlation over five noisy points came out at 0.7. The low-abundance range is where the unassigned peptides are likely to be, so this matters more here than in a peptide-centric search.

### 2c. Anchor components

Each anchor trace is split into its peaks first, because one fragment mass can elute more than once (two peptides that share a fragment, at different times). Peaks are found with Osprey's CWT at the run's scale (`CwtPeakDetector.FindPeaks`). Where two peaks overlap, the overlapping segment is decomposed with a dictionary of EMG shapes:

- **Atoms:** unit-area EMGs (`EmgShape.Density`) on a grid of apex positions (every scan), σ (about 4 steps across the run's range) and τ/σ (for example 0, 0.25, 0.5, 1, 2).
- **Fit:** sparse non-negative regression (non-negative orthogonal matching pursuit) weighted by the noise model, on the overlapping segment only, with at most K atoms per segment (default 3).

Each peak becomes an anchor component: trace id, apex, approximate width, amplitude and the scans it covers. Its own shape is only a starting point; the shape that matters is the template's.

### 2d. Grouping anchors into templates

Anchor components in the same window are grouped greedily, most intense first, as Kronik groups envelopes:

1. The most intense unused anchor component seeds a group. Its own EMG fit is the provisional template.
2. Another anchor component joins when its apex is within 0.5 × the run's median FWHM of the template's mode and the template explains its trace: amplitude × template plus a linear baseline fits significantly better than a baseline alone (the likelihood test of Stage 3b).
3. After each join the template is refit to all members (below) and the members are rechecked against it. A component that only fit an earlier, provisional template is released.
4. Repeat until no anchor components are left. A seed with no partner still makes a template; Stage 3d decides whether its cluster is kept.

**The template fit.** One EMG shape (μ, σ, τ) is fitted to all members' points at once, each member with its own amplitude and linear baseline, weighted by the noise model. This is the Consensus EMG idea with the points pooled instead of reduced to a pointwise median: with six members of six points each, the shape is fitted to thirty-six points. The fit uses a robust loss (Huber, by iteratively reweighted least squares), so a member carrying an interference bump on a few points does not move the shape. The result is the template: μ, σ, τ, its anchor members and the scans it covers.

Only anchors build templates on HRAM data, as before: a centroid trace has no isotope evidence that it is a peptide fragment. Two peptides whose apexes are closer than about a third of a peak width cannot be told apart by elution at this sampling, so they share a template. That is expected. A cluster is a group of co-eluting signal, not necessarily one peptide, and the search scores chimeric spectra (Stage 5).

A graph method (Leiden on the anchor co-elution graph) is the alternative if greedy grouping splits clusters in tests.

## Stage 3: Window fit and clusters

Stage 3 fits every trace in the window, anchor or centroid, to the templates that overlap it. The fit decides membership, splits shared fragments, and gives each member its area.

### 3a. The window fit

Each trace is fitted locally. Over the scans where at least one template is above 5% of its peak, the trace is modeled as a non-negative combination of the templates that overlap there, plus a linear baseline:

y(t) = Σₖ aₖ · Tₖ(t) + b₀ + b₁ t,  with every aₖ ≥ 0

The template shapes are fixed, so this is linear: non-negative least squares on the amplitudes, weighted by the noise model. It is the `FixedShapeEmgIntegrator` the datum design note describes, applied across all the templates in a window. Each Tₖ has unit area, so aₖ is the trace's area in cluster k. The problem is small: a trace overlaps only the few templates near it in time, so each fit has a handful of unknowns, and windows fit in parallel.

Two cases that a link-based clustering handles badly come out of this fit with no special step:

- **A fragment shared by two co-eluting peptides** is split between their clusters by the fit itself.
- **A fragment in almost every tryptic peptide** (y1 of K or R at 147.11 and 175.12, immonium ions) gets a small amplitude in many clusters. It cannot pull two clusters together, because membership is decided trace by trace against fixed templates, not by links between traces.

**Known templates.** In `--features unassigned` mode, each of Osprey's passing precursors becomes a known template before Stage 2d runs: one EMG fitted to its matched fragments' traces inside Osprey's boundaries, by the same pooled fit. Known templates take part in the window fit like any other, so the signal they explain, including their share of fragments they share with other peptides, is accounted for. Anchors that a known template explains do not seed new templates. New clusters form from what is left. In `--features all` mode there are no known templates.

### 3b. Membership

A trace is a member of cluster k when both hold:

1. **The template explains it.** Dropping Tₖ from the trace's fit raises the weighted residual by more than a threshold. This is a likelihood-ratio test under the noise model of 2b; its threshold is a setting, tuned on simulations and on the cluster benchmark (see Validation).
2. **Enough points.** At least 4 of the trace's points fall where Tₖ is above 5% of its peak.

A member that is also significant in another cluster is marked **shared**, with its shared fraction (aₖ over the trace's total fitted area). Anchor traces go through the same test, so an anchor that did not join a template in 2d can still be a member here.

### 3c. Residual pass

After the fit each trace has a residual: its data minus everything the templates explain. Where an anchor trace keeps significant unexplained signal (a peak in the residual that passes the 3b test against a baseline alone), Stages 2c and 2d run again on the residuals, and the new templates join the window fit. This finds peptides whose anchors were hidden under a stronger co-eluting peptide. It repeats until no new template appears, at most twice by default.

Two rules keep the residual pass from inventing templates. First, a residual is judged against the noise of the whole signal: the signal already explained still counts toward each point's variance, so the ordinary counting scatter left after fitting a strong peak is not mistaken for a new peak. Second, a new template whose apex is within the apex tolerance of an existing template is dropped: signal that close is co-eluting with that template and shares it (2d), and a near-copy would split the existing template's members between the two. On the synthetic test window, adding the two rules took the residual pass from 11 spurious templates to none.

Centroid residuals do not seed templates, for the same reason centroids do not seed in 2d. The unexplained centroid signal is summed per window and logged: it shows how much signal no template accounts for, which feeds the Gain diagnostics.

### 3d. Acceptance

A cluster goes on to the pseudo-spectrum when it has at least 1 anchor member and at least 6 members. Known templates from Osprey do not produce clusters in `unassigned` mode; they only explain signal. There is no rule that most members be unshared: shared members are split by the fit, so their areas are already corrected, and chimeric clusters are expected. The shared fraction goes to Percolator as a feature instead (5c). Rejected clusters are logged with the reason, for the diagnostics in Stage 7.

### 3e. Boundaries and areas

**Boundaries.** Each accepted cluster's member traces go to `CwtPeakDetector.DetectConsensusPeaks` unchanged: scale from the members' median FWHM, Mexican hat convolution, pointwise median across members, zero-crossing ±2σ boundaries with the 5% valley guard. For a shared member, the trace passed is the member's data minus the other clusters' fitted parts, so a co-eluting peptide's share does not move the boundaries. Only the consensus peak whose apex is within 0.5 FWHM of the template's mode is kept. These are the boundaries written to the blib and used by Skyline.

**Areas.** A member's area is its amplitude aₖ from the window fit, in intensity × minutes (the template has unit area on the minutes axis). For a member explained by one template only, the area is checked against the Consensus EMG integrator's robust amplitude: the median of y/g over the points where the template is at least 5% of its peak, which held to about 0% deviation with 2 of 6 transitions interfered in datum's tests. When the two disagree by more than a tolerance, the robust amplitude is used and the member is flagged, since that usually means interference that no template models. These areas are the pseudo-spectrum's intensities.

### 3f. Precursor candidates

Each accepted cluster gets precursor candidates from three tiers, tried in order. The tier is kept with the cluster, written to the mzML, and passed to Percolator as a feature.

| Tier | Evidence | What it gives | SageSharp precursor tolerance |
| --- | --- | --- | --- |
| 1. MS1 feature | An MS1 Kronik feature whose envelope falls in the isolation window, apex within 0.5 FWHM of the cluster's, and the cluster's template explains its MS1 profile (the Stage 3b test) | Monoisotopic m/z and charge | Calibrated ppm, isotope errors −1 to +2 |
| 2. Unfragmented precursor in MS2 | An MS2 Hardklor anchor inside the isolation window, charge 2 or more, that the cluster's template explains (the Stage 3b test) and that is heavier than every fragment-like member | Monoisotopic m/z and charge; isotope errors allowed, since the window edge can clip the envelope | Calibrated ppm, isotope errors −1 to +3 |
| 3. None | No precursor found | The isolation window only | The isolation window (wide-window mode) |

Tier 2 is checked for consistency before it is used. Complementary b/y pairs should sum to its mass, and expected neutral losses should sit at the right offset (for example water, or phosphoric acid for phosphopeptides). A tier 2 precursor is a cluster member, so it is taken out of the pseudo-spectrum's fragment list.

For tier 3, the complementary pair sums can still propose a mass. When they do, SageSharp tries it first with a narrow tolerance, then searches the full window. The precursor charge is tried from 2 to 4, and never below the highest fragment charge seen in the cluster plus one.

One cluster can have more than one tier 1 or 2 candidate (co-isolated peptides). All are written to the mzML and searched, and the best PSM wins within the cluster.

## Stage 4: Pseudo-spectrum mzML

Each accepted cluster becomes one centroided MS2 spectrum whose peaks are its members' areas from Stage 3e (background modeled in the window fit), at deconvoluted (singly charged, monoisotopic) m/z.

**Peaks.**

- Anchor member: m/z = (mono\_mass + 1.007276) for its Hardklor mass, whatever its observed charge. Its area is the whole envelope's area, so isotopes are already folded in.
- Centroid member: its observed m/z (intensity-weighted mean over the boundaries); charge unknown, written as observed.
- Intensity: the member's area from Stage 3e, in intensity × minutes. An `--units ions` option uses Glimpse's ion conversion (intensity × injection time / 1000, divided by the analyzer's α) so areas compare across instruments.

**Spectrum metadata** (mzML, PSI-MS terms where they exist, userParams otherwise):

| Field | Value |
| --- | --- |
| `id` | `run=<stem> window=<n> cluster=<k>` (stable across reruns with the same settings) |
| scan start time | Cluster apex from the median CWT |
| isolation window | The DIA window's target, lower and upper offsets |
| selected ions | Every Stage 3f precursor candidate (m/z, charge, tier), tier 1 first; none for a tier 3 cluster |
| userParams | `start_rt`, `end_rt` (median CWT), `emg_mu`, `emg_sigma`, `emg_tau`, `n_members`, `n_anchors`, `n_shared` |
| dataProcessing | Deisotoping (MS:1000033), charge deconvolution (MS:1000034), and a Quasar processing method with its version and command line |

**Sidecar.** A `<stem>.clusters.parquet` file holds what mzML cannot carry neatly: one row per member (cluster id, trace m/z, charge, anchor or centroid, amplitude, shared fraction, area, the likelihood-ratio statistic of its membership, and whether the robust area replaced the fitted one). Stage 6 reads it for rescoring features and the blib. It carries the same validity stamp convention as Osprey's sidecars (tool, version, settings hash), so reruns can skip finished work.

**Writer.** Use ProteoWizard's mzML writer through `ProteoWizardWrapper` if it builds on .NET 8 and Linux in this setting; otherwise a small streaming writer, since the spectra are few and simple. The mzML must also open in SeeMS and Glimpse, so a cluster can be checked by eye against the raw data.

## Stage 5: Search with SageSharp

The search is a .NET port of Sage's core (**SageSharp**, a project inside Quasar, named like CarafeSharp and BullseyeSharp), checked against Rust Sage with unit tests on its scoring functions rather than a full parity harness. Sage's LDA is not ported: its PSM features go to Osprey's Percolator (SVM or GBDT) instead.

### 5a. What to port

- **Port:** FASTA reading and digestion (enzymes, semi-enzymatic, non-specific, missed cleavages), modifications, the fragment index, the preliminary and full scoring (hyperscore and the PSM feature set), wide-window and chimeric search, and reversed decoys paired one-to-one with their targets.
- **Do not port:** the cloud I/O, LFQ and TMT quantification, and Sage's LDA rescoring (Osprey's Percolator replaces it). Sage's PEP code is already in `Osprey.ML/PepEstimator.cs`.
- **mzML input:** SageSharp reads Quasar's mzML, including the multiple selected ions, and the clusters sidecar for the extra features below.

**Checking against Sage.** Sage's own Rust tests are ported first; they are strong on digestion, peptides, modifications, ion series and protein grouping but check few score values. On top of them, golden-value tests hold SageSharp's building blocks to values Rust Sage's CLI writes for small fixed inputs (its test spectrum plus a few Quasar pseudo-spectra): digestion (tryptic, semi-tryptic, non-specific), modification placement, fragment ion masses, hyperscore, Poisson score, matched peaks, longest b and y runs, matched intensity, and ppm and isotope errors. End to end, SageSharp and Rust Sage are both run on the Phase 1 mzML, and the overlap of identifications and the correlation of scores are reported, not used as a gate. SageSharp is free to differ where Quasar needs it to, such as several selected ions per spectrum or wide-window search for tier 3 clusters only. Sage's LDA, q-values and PEP are not ported.

**Streaming.** Inside the Quasar binary, SageSharp starts scoring while Quasar is still deconvoluting peak groups. Windows are clustered in parallel, and each accepted cluster's pseudo-spectrum goes onto a bounded queue (`System.Threading.Channels`) that SageSharp workers read from. The fragment index is built from the FASTA at startup, alongside Stages 1 and 2, so it is ready before the first pseudo-spectrum arrives. The mzML is still written from the same queue for inspection and the Rust Sage comparison, but it is off the critical path.

This works because Sage scores each spectrum independently; the steps that need every PSM (Sage's LDA and RT model) are the ones not ported. The output must not depend on arrival order, so PSMs are sorted by stable spectrum id before Percolator, and a streamed run must give the same PSMs as a run from the written mzML. Percolator, reconciliation and the blib stay join steps after the stream ends.

### 5b. Search settings (version 1 defaults)

| Setting | Value | Why |
| --- | --- | --- |
| Enzyme | Trypsin; tryptic, semi-tryptic or non-specific | All three are supported; the digest is a setting of the search |
| Missed cleavages | 2 |  |
| Peptide length | 7 to 50 |  |
| Modifications | Fixed: carbamidomethyl C. Standard variable: oxidized M, pyro-Glu (N-terminal Q and E), protein N-terminal acetyl. Optional sets: phosphorylation (S, T, Y), acetyl-K, GG-K (ubiquitin remnant), and others by Unimod ID | The standard set is always on; PTM sets are added per study. PTMs multiply the semi-tryptic and non-specific space, so the most variable mods per peptide is a setting (default 3) |
| Fragment tolerance | ±10 ppm (Astral), set per instrument | Anchor m/z are already monoisotopic |
| Precursor tolerance | Calibrated ppm for tier 1 and 2 precursors, isotope errors as in Stage 3f | Every candidate in the spectrum is tried; the best PSM wins |
| Wide-window mode | Precursor tolerance = the isolation window, for tier 3 clusters | Sage's DIA/WWA mode |
| Deisotoping | Off | Done in Stage 1 |
| Reported PSMs | Top 2 per spectrum, plus chimeric PSMs when a second candidate precursor matches | Delta score to the next hit is a rescoring feature |

### 5c. Features passed to Percolator

1. **Sage's own:** hyperscore, delta to the next and best score, matched peaks, longest b and y runs, matched intensity fraction, fragment and precursor ppm error, isotope error, Poisson score, charge, peptide length, missed cleavages.
2. **Cluster:** members, anchors, shared fraction, the fraction of the cluster's total area the PSM explains, the precursor tier (MS1, unfragmented in MS2, or none), how well the cluster's template explains the precursor's profile (its Stage 3b likelihood-ratio statistic; 0 for tier 3).
3. **Prediction (CarafeSharp):** predicted fragment intensities against the observed areas (spectral angle), and predicted RT against the cluster apex. The RT is mapped onto the run with the run's Osprey calibration (`<stem>.calibration.json`), so no separate fit is needed when Osprey has searched the run.
4. **Digest class:** tryptic, semi at the N-terminus, semi at the C-terminus, non-specific. With the modification state, these are features of the PSM, not separate FDR groups (Stage 6b).

The prediction features matter most here. A semi-tryptic search has many more candidates per spectrum than a tryptic one, and an RT and fragment-intensity check is the best way to separate a correct semi-tryptic match from a chance one.

**Modified peptides.** Each modification is a Percolator feature (6b). For phosphorylation and other modifications with more than one possible site, SageSharp adds a localization step that Sage does not have: it scores every site isoform of the matched peptide on its site-determining fragments, and reports the best site, its localization probability and the score gap to the next isoform, all as features. A false localization rate comes from decoy sites on residues that cannot carry the modification. The blib stores the best-localized form, and positional isomers that elute apart stay separate clusters and separate library entries. CarafeSharp prediction features are used where it has a model for the modification (its diGly and acetyl-K models, for example) and are marked missing otherwise.

## Stage 6: FDR with Osprey's Percolator, then the blib

Osprey's native Percolator scores the SageSharp PSMs in one model, with peptide class (tryptic, semi-tryptic, modified) among its features, and the passing PSMs are written as a blib of observed cluster areas with median CWT boundaries.

### 6a. Percolator adapter

- Build one `PercolatorEntry` per PSM: file, modified peptide, charge, decoy flag, an entry id (run, window, cluster, rank) and the Stage 5c feature vector.
- **Paired target-decoy competition, as Osprey does it now.** SageSharp makes one decoy per target peptide (reversed, terminal residue kept) and records the pair with a pairing ID, the same design as a blib's `DecoyPairs`. Each pseudo-spectrum keeps its best PSM. Then each target competes with its paired decoy, and only the better of the pair goes on to precursor and peptide q-values. `Osprey.FDR`'s competition needs no change; the new code is only the adapter that turns SageSharp PSMs into entries with pairing IDs.
- Training, cross-validation folds, Granholm calibration, PEP and the run and experiment q-value levels are used as they are.

### 6b. Peptide class as features, one FDR

Peptide class goes into the classifier, not into separate FDR calculations. Each PSM carries flags for tryptic, semi-tryptic at the N-terminus, semi-tryptic at the C-terminus and non-specific, plus modification features (a flag for each variable modification and the count of modified residues). The SVM, or the gradient-boosted trees that are now Osprey's default (`OSPREY_FDR_MODEL`), learns how much less often a semi-tryptic or modified match is right and how that trades against the spectral and RT evidence. The trees can also learn interactions, such as a semi-tryptic match being credible only with a close predicted RT.

q-values come from one target-decoy competition over all PSMs, reported at 1% for precursors and peptides. This only works if decoys have the same class mix as targets, so SageSharp gives each decoy the class and modifications of the target it was reversed from. Class alone then cannot tell a target from a decoy.

### 6c. Checking the FDR

Run every benchmark with an entrapment database (CarafeSharp's entrapment FASTA stage, scored with Osprey's fractional entrapment method). The FDR must hold overall. The entrapment rate is also reported by peptide class, to check that the classifier is not letting a weak class through.

### 6d. Cross-run reconciliation

Quasar reconciles across runs the way Osprey does: the confident runs establish where each peptide elutes, and every run is then made to report the same feature at that place, or a gap-filled one. Because Quasar is spectrum-centric, a peptide can be found by a different cluster in each run, or by no cluster at all where its signal was weak. Without this step the runs would not agree.

The steps follow Osprey's Stages 5 to 7, reusing `Osprey.FDR/Reconciliation` where it fits:

1. **First-pass FDR.** Percolator over every run's PSMs (6a), giving run-level precursor and peptide q-values.
2. **Consensus.** For each precursor passing at 1% in at least one run, map each passing apex into the consensus library RT space (Osprey's alignment, or Quasar's own without Osprey) and take the score-weighted median, as `ConsensusRts` does, with the same hard precursor q-value gate. Also build the precursor's **consensus fragment set**: the matched fragments seen in most passing runs, with their median relative areas and the median EMG shape. The set is fixed for the precursor: every run, reconciled or gap-filled, is quantified and rescored on exactly these fragments, so runs can be compared.
3. **Per-run plan** (Osprey's Keep / UseCwtPeak / ForcedIntegration):
   - *Keep*: the run's cluster apex is within the local RT tolerance of the consensus RT.
   - *Use another cluster*: the run's best match is elsewhere, but another accepted cluster in that window sits at the consensus RT; score that cluster against this peptide.
   - *Forced integration (gap fill)*: no cluster there. Extract the consensus fragments' traces at the predicted RT, set boundaries from the consensus width, and quantify each with the consensus EMG shape held fixed (the Stage 3a fixed-template fit: amplitudes only). A peptide that is absent then gets an area at the noise floor, not a missing value.
4. **Rescore.** In every run, Keep runs included, extract the consensus fragments at the planned boundaries and quantify each with the consensus EMG shape. A cluster member outside the set is dropped, and a set fragment the run's cluster missed is extracted from that run's traces. The run's pseudo-spectrum is built from those fragments only, scored against its peptide with SageSharp's scoring, and the frozen first-pass model is applied.
5. **Second-pass FDR.** Experiment-level q-values over the reconciled pool, with the same paired competition.

Paired decoys go through the same steps as their targets: a gap-filled target is matched by a decoy gap-filled the same way, at the same RT, with its own fixed fragment set built the same way and used in every run. Otherwise reconciliation would favor targets. Osprey handles this through its pairing (base ID) links, and Quasar uses its own pairing IDs the same way.

The work splits into the same HPC tasks as Osprey's: per-file clustering and search, `FirstPassFDR` (consensus), `PerFileRescoring` (plan, gap fill, rescore), and `SecondPassFDR` (q-values and the blib). The blib's `RetentionTimes` rows carry the reconciled boundaries for every run.

### 6e. blib contents

The blib is written through `BiblioSpec.IO.BlibWriter` with a small `QuasarBlibWriter` adapter, like `OspreyBlibWriter`:

| Table and column | Value |
| --- | --- |
| `RefSpectra` peaks | The **observed** cluster members the PSM matched, each at the m/z of its monoisotopic peak at its **observed charge**, with its Stage 3e area as intensity |
| `RefSpectra.retentionTime`, `startTime`, `endTime` | Cluster apex and median CWT boundaries in the best run |
| `RefSpectra.score`, `scoreType` | Experiment q-value; type 19 (generic q-value), as Osprey writes |
| `RefSpectraPeakAnnotations` | The fragment ion for each peak (for example y7, b5 2+). Osprey leaves this empty; here it costs nothing and helps review |
| `RetentionTimes` | One row per run the precursor passed in, with that run's apex, boundaries and q-value |
| `SpectrumSourceFiles.fileName` | The original run's file name, not the pseudo-spectrum mzML, so Skyline matches ID lines to replicates |
| `LibMetadata` | `quasar.command`, `quasar.version`, the FDR threshold, the features mode, the resolution mode |

The observed-charge rule matters for Skyline. A fragment seen at 2+ but stored at its deconvoluted 1+ m/z would make Skyline pull an empty chromatogram at the wrong m/z.

## Stage 7: Add-on library for Osprey

Quasar's second output is an add-on library: the modified, semi-tryptic and non-specific peptides it found in the signal Osprey did not annotate, written so Osprey can search them beside its unmodified library and quantify everything in one search.

### Workflow

1. **Osprey, first search.** Osprey searches the runs with its unmodified tryptic library (CarafeSharp predictions).
2. **Quasar on the leftovers.** Quasar searches only the MS2 signal Osprey did not annotate (`--features unassigned`), using Osprey's calibration, peak width and RT alignment, with the modifications and digest chosen for the study.
3. **Add-on library.** Quasar writes `<output>.quasar-addon.blib`: every precursor that passed 1% after reconciliation (6d) and is not already in Osprey's library.
4. **Osprey, combined search.** The add-on is merged with the unmodified library and Osprey searches again. The new peptides are then quantified in every run, reconciled across runs with everything else, and included in protein inference.
5. **Another round (optional).** Run Quasar again on what is still unannotated, and stop when a round adds little.

### What each add-on entry holds

| Field | Source |
| --- | --- |
| Spectrum | CarafeSharp's prediction when it has a model for every modification on the peptide; otherwise Quasar's consensus fragment set from 6d (observed m/z at observed charge, median relative areas). Each entry records which source it came from |
| Library RT | The consensus library RT from 6d. Quasar builds it in Osprey's library RT space, so it needs no conversion |
| Modifications | Unimod masses in the blib `Modifications` table, at the best-localized site |
| Proteins | Every FASTA protein containing the sequence, so Osprey's parsimony sees shared peptides |
| Decoys | None in the add-on. Osprey makes decoys for the merged library, so add-on and unmodified entries get decoys the same way |

Predicted spectra keep the add-on consistent with Osprey's predicted unmodified library. Observed spectra cover the modifications CarafeSharp cannot predict yet. Either way the merged library is one blib, written through the shared `BlibWriter` with a `library_source` per entry.

### Keeping the FDR honest

The add-on peptides were chosen on the same runs Osprey then searches, but their decoys were not, and that favors the targets in Osprey's combined search. The guards:

- **Entry gate:** only precursors passing Quasar's experiment-level 1% FDR go into the add-on.
- **Origin as a feature:** Osprey's classifier gets the entry's origin (unmodified library or Quasar add-on) and its modification and digest flags as features. Each decoy carries its target's values, consistent with 6b.
- **Entrapment carried through:** entrapment sequences go into Quasar's FASTA. Those that reach the add-on show how often Osprey then passes a wrong library entry.
- **Held-out check:** build the add-on from half the runs, search the other half, and compare with the full result.

### Changes needed in Osprey

- A library merge step (unmodified plus add-on into one blib, entries deduplicated by modified sequence and charge), so Osprey keeps reading a single library.
- Checking that Osprey scores and writes every modification in the add-on (phosphorylation, GG-K, acetyl-K, pyro-Glu) through its library loader and blib output.
- The origin feature in Osprey's PIN feature set.

## Unit resolution data (Stellar)

Unit-resolution MS1 and MS2 have no resolved isotope envelopes, so Hardklor and Kronik give no anchors. The plan replaces isotope evidence with persistence above a measured noise level, then checks on link density and complementary b/y pairs and, where the samples also exist as HRAM runs, transfers what was found there instead.

### What changes by stage

| Stage | HRAM | Unit resolution |
| --- | --- | --- |
| 1 Traces | Hardklor anchors plus centroid traces, linked at 10 ppm | Centrix centroids (Centrix also demultiplexes staggered windows); noise separated from signal per window; persistent signal peaks become anchors; centroids linked within 0.5 Th after Osprey's MS2 calibration; no `run.hk` needed |
| 2 Templates | As written | Same; wider bins mix more ions, so more templates overlap each trace, and the dictionary allows K = 4 per segment. Variances come from the intensity-histogram noise model |
| 2d Seeds | Most intense anchor | Persistent above-noise peaks, in intensity order (below) |
| 3f Precursor | MS1 Kronik features | Mass estimated from complementary pairs when they agree; else wide-window search |
| 4 mzML | Deconvoluted, monoisotopic 1+ m/z | Observed bin m/z; no deconvolution |
| 5 SageSharp | Hyperscore, ppm tolerances | Fragment tolerance 0.5 Th; XCorr at unit bins (Osprey's `SpectralScorer`) added as a feature |
| 6 FDR and blib | As written | Same; a resolution flag goes into `LibMetadata` |

### Anchors without isotopes

1. **Centroid with Centrix.** Stellar profile spectra are centroided by Centrix, which separates fragments closer than 1 Th that the vendor centroider merges (fragment m/z error 0.17 against 0.39 m/z SD in its HeLa comparison), and demultiplexes staggered windows in the same pass.
2. **Noise and signal.** Within each window series, in m/z segments (20 Th, as Centrix refines its noise) and RT segments, histogram the log centroid intensities. The most frequent intensity, the mode, is the noise level. The noise width comes from the part of the histogram below the mode, mirrored. Centroids above the mode plus 3 widths are signal candidates. The rest stay available as cluster members but cannot seed. The same noise level and width give each point its variance for the window fit (Stage 2b).
3. **Persistence.** A signal candidate becomes an anchor when a centroid within 0.5 Th, also above the noise threshold, appears in at least 2 of 3 consecutive spectra of the same window series. This is Kronik's linking rule with a short window. Anchors are linked into traces and play the part Hardklor features play on HRAM data.
4. **Clusters.** Stages 2 and 3 run as on HRAM data. Anchors build templates in intensity order, and any centroid trace the window fit assigns to a template joins its cluster, sub-threshold traces included.
5. **Acceptance.** At least 8 members (6 for HRAM). Two checks score whether a cluster is a peptide, and both go to Percolator as features. One is link density, the number of members that share the seed's elution profile. The other is complementary pairs: a b ion and its complementary y ion, both 1+, sum to the precursor's neutral mass plus 2 × 1.00728, so in a one-peptide cluster the pairwise sums pile up at one value. Compared with shuffled clusters, the pile-up gives a score, and its position gives a precursor mass for tier 3 (Stage 3f).

### Transfer from HRAM runs

When the same samples, or the same sample type, were also run on an Astral, the preferred route is to run Quasar there and give Osprey that blib (or CarafeSharp predictions for its peptides) as the library for the Stellar runs. Osprey's unit-resolution search is already validated on Stellar, so this needs no new unit-resolution discovery code. The anchor-free mode is for studies that only have Stellar data.

### Validation

- **Paired runs.** Osprey's reference datasets include Stellar and Astral runs of the same material. Compare anchor-free Quasar IDs on Stellar with Quasar and Osprey IDs on Astral, matched through RT calibration.
- **Anchors switched off.** Run the anchor-free mode on Astral data, ignoring its anchors. The difference from the anchored run measures what the anchors are worth, separately from the loss of resolution.

Expect fewer and less pure clusters than on HRAM data. The search will lean harder on XCorr, predicted RT and predicted fragment intensities.

## Data model and file formats

Every stage writes a file next to the input with a validity stamp, as Osprey's tasks do, so each stage can run as its own HPC task and a rerun skips finished work.

| File | Written by | One row or record per | Key contents |
| --- | --- | --- | --- |
| `<stem>.hk` | Glimpse (or Quasar, the same code) | Isotope envelope | scan, rt, series, mono\_mass, charge, intensity, feature (Format 2, as now) |
| `<stem>.traces.parquet` | Stage 1 | Trace point | window, trace id, kind (anchor or centroid), m/z, charge, scan, intensity, ion count |
| `<stem>.templates.parquet` | Stage 2 (and the 3c residual pass) | Template | window, template id, μ, σ, τ, scan range, anchor members, source (new, residual pass, or known from Osprey) |
| `<stem>.clusters.parquet` | Stages 3 and 4 | Cluster member | cluster id, template id, trace id, anchor flag, amplitude, shared fraction, area, likelihood-ratio statistic, robust-area flag; cluster apex and median CWT bounds |
| `<stem>.quasar.mzML` | Stage 4 | Cluster | The pseudo-spectrum (Stage 4 table) |
| `<stem>.sage.parquet` | Stage 5 | PSM | peptide, proteins, charge, decoy, digest class, Stage 5c features |
| `<blib-stem>.1st-pass.consensus.json` | Stage 6d (FirstPassFDR) | Experiment | Consensus library RT, consensus fragment set and EMG shape per precursor |
| `<stem>.reconciliation.json` | Stage 6d (PerFileRescoring) | Run | Keep, use another cluster, or forced integration per precursor; gap-fill targets |
| `<stem>.sage-reconciled.parquet` | Stage 6d (PerFileRescoring) | PSM | Rescored and gap-filled PSMs, read by SecondPassFDR |
| `<output>.blib` | Stage 6 | Passing precursor | Stage 6e table |
| `<output>.quasar-addon.blib` | Stage 7 | New precursor | Predicted or consensus spectrum, consensus library RT, modifications, proteins, library\_source (Stage 7 table) |

The core in-memory types are small records: `Trace`, `ElutionTemplate` (an `EmgShape` with its anchor members), `Cluster` (template, members with amplitudes, `XICPeakBounds` from Osprey.Core), `PseudoSpectrum` and `Psm`. The first three need no pwiz dependency, so they can also live in datum for simulation.

## Validation and benchmarks

Success means the new identifications hold their FDR under entrapment and close a measurable part of the gap between Glimpse's Peptide-like and Assigned MS2 traces.

| Test | Data | Pass condition |
| --- | --- | --- |
| Simulation | datum: many co-eluting clusters with shared fragments, background, Poisson noise, known truth | Cluster purity and recall measured against truth; areas within datum's Consensus EMG error |
| Cluster benchmark | Astral file 55 with Osprey's 1% precursors as truth (their matched fragments and boundaries); no search | Completeness (the share of a precursor's fragment intensity that lands in one cluster) and purity (the share of a cluster's intensity from one Osprey precursor), both higher than the baseline design: each trace decomposed on its own, then linked by shape cosine and Pearson correlation |
| Positive control | `--features all` on runs Osprey has searched | Re-finds most Osprey tryptic IDs; apex, boundaries and areas agree with Osprey's |
| SageSharp vs Sage | Sage's test data and Quasar mzML, searched by Rust Sage and SageSharp | Unit tests: scoring functions match Rust Sage's values. End to end: identification overlap and score correlation reported, not a gate |
| FDR | Entrapment search | Entrapment FDR at or under the reported FDR; also reported by peptide class |
| Gain | `--features unassigned` on the Astral Zoom run | New precursors at 1%, and the share of the Peptide-like minus Assigned MS2 signal they explain |
| Closed loop | Quasar blib (or CarafeSharp predictions) added to Osprey's library | New peptides quantified across runs; Osprey's existing IDs unchanged |
| Throughput | The 174,000-spectrum Astral run | Measured per stage; target set after M2 |

The cluster benchmark runs in minutes without any search, so it is the loop for tuning Stages 2 and 3 (the likelihood threshold, the apex tolerance, the residual pass). It runs in `--features all` mode, so Osprey's precursors are truth rather than known templates. Rust Sage identifications then confirm what it finds.

The "Gain" metric fits in Glimpse: an *Explained by Quasar* trace beside Peptide-like and Assigned, using the same each-peak-once rule.

## Milestones, risks and decisions

Phase 1 puts the clustering in front of a real search engine as early as possible: Quasar writes mzML and Rust Sage searches it. Phase 2 brings the search into the Quasar binary, with Phase 1's Rust Sage results as the comparison baseline.

### Milestones

1. **Phase 1: Quasar mzML, searched by Rust Sage.**
   - Create `pwiz_tools/Quasar`; port Glimpse's Hardklor and Kronik code into `Quasar.Features`; port datum's EMG code into `Quasar.Core`.
   - Stages 1 to 4 from a raw file: traces with ion counts, anchor templates, the window fit and the residual pass, precursor tiers, median CWT boundaries, areas, the pseudo-spectrum mzML and its clusters sidecar. Osprey's calibration and peak width are used when an Osprey search exists.
   - The cluster benchmark against Osprey on Astral file 55 (completeness and purity), with the baseline design for comparison, before the Sage search.
   - Search the mzML with Rust Sage. Check first how Sage reads several selected ions and the isolation window; if it uses only the first, write one spectrum per precursor candidate for this phase. Tier 3 clusters go in their own mzML, searched in Sage's wide-window mode.
   - Sage's own LDA q-values are used here only to judge the clustering: the positive control (`--features all`) against Osprey, then the gain on `--features unassigned`.
   - Keep the Rust Sage results with their exact settings and versions. They are Phase 2's comparison baseline.
   - Done when the pseudo-spectra give Sage identifications and the positive control re-finds Osprey's peptides with matching boundaries.
2. **Phase 2: SageSharp, Percolator and blib in the Quasar binary.**
   - Port Sage's core into `Quasar.SageSharp` and run it on the Phase 1 mzML. Unit tests hold its scoring functions to Rust Sage's values, and the end-to-end comparison with the Phase 1 results is reported.
   - Percolator through `Osprey.FDR`: the PSM adapter with pairing IDs, paired target-decoy competition, peptide-class, modification and precursor-tier features, SVM or GBDT.
   - blib output through the shared `BlibWriter` (Stage 6e).
   - Done when one `quasar` command goes from a raw file to a blib, with SageSharp scoring pseudo-spectra while Quasar is still clustering (5a, Streaming).
3. **Phase 3: Features beyond Sage.** CarafeSharp prediction features and PTM site localization.
4. **Phase 4: Across runs.** Cross-run reconciliation with fixed fragment sets, the second FDR pass and the HPC task split.
5. **Phase 5: Into Osprey.** The add-on library, the merge step, the origin feature, Osprey's combined search, and the validation table.
6. **Phase 6: Unit resolution.** Centrix input, the intensity-histogram noise model, persistent anchors, complementary-pair scoring and XCorr in SageSharp. The HRAM-to-Stellar transfer can run as soon as Phase 5 gives an add-on library.

### Risks

| Risk | Effect | Mitigation |
| --- | --- | --- |
| Few points per peak (about 1.1 s per window on Astral) | Shapes of single traces poorly determined | Templates fitted to all anchors' points at once; membership by a fit test, not by comparing shapes |
| Chimeric clusters in wide windows | Mixed pseudo-spectra, fewer IDs | The window fit splits shared fragments; clusters need not be one peptide; chimeric search; narrow-window (Astral) data first |
| Peptides with no visible anchor | No template, so never clustered | The residual pass finds anchors hidden under stronger peptides; the cluster benchmark measures what is still missed |
| Weak fragments with no envelope | Clusters too small to pass | Centroid traces join clusters through the window fit; the noise model accepts weak members on evidence, not a fixed threshold; the intensity floor is a setting |
| Semi-tryptic and non-specific search space with PTMs | Higher chance matches, slower search, more memory | Peptide-class and prediction features, entrapment checks; non-specific as a separate run |
| Shared signal counted twice | Inflated areas in Quasar and Osprey | Osprey's precursors as known templates in `unassigned` mode, so shared signal is split, not counted again; each-peak-once accounting in the Gain metric |
| Compute on Astral runs | Long runs | Local window fits with a handful of unknowns each, the dictionary only on overlapping anchor peaks, per-window parallelism, per-stage HPC tasks |
| Unit-resolution data | No envelopes to anchor clusters (Stellar) | Transfer from HRAM runs; anchor-free mode (see Unit resolution data) |

### Decisions made

- Quasar lives in `pwiz_tools`; SageSharp is part of Quasar.
- One FDR: peptide class, modifications and precursor tier are classifier features, with paired target-decoy competition.
- On HRAM data only Hardklor anchors start clusters; on unit-resolution data, persistent above-noise peaks do.
- Clusters are found template first (2026-11-26): anchors define shared EMG templates, every trace is fitted to them, and membership is a likelihood test under a counting-noise model. A template is the shared shape; a cluster is a template and its members. Osprey's precursors enter `--features unassigned` as known templates, not as a mask.
- Staggered and overlapping windows are demultiplexed first.
- Reconciliation uses one fixed fragment set per precursor in every run.
- Osprey gets an add-on library of modified, semi-tryptic and non-specific peptides beside its unmodified library.
