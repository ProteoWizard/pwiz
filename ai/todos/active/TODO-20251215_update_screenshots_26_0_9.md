# TODO-20251215_update_screenshots_26_0_9.md

## Branch Information
- **Branch**: `Skyline/work/20251215_update_screenshots_26_0_9`
- **Created**: 2025-12-15
- **Status**: In Progress
- **Objective**: Review and finalize tutorial screenshot updates for the 26.0.9 "FEATURE COMPLETE" release

## Background

Tutorial screenshots need to be reviewed before the 26.0.9 release. The current screenshots are from the 25.1 release, with updates applied during the 26.0 development cycle (including Files view feature work from TODO-20251126_files_view.md).

## Review Workflow

Using **ImageComparer** tool (`pwiz_tools/Skyline/Executables/DevTools/ImageComparer`) to compare current screenshots against the Git versions. For each difference:

1. **Accept** - Valid change from completed feature work
2. **Revert** - Unintended change, restore original
3. **Fix** - Bug discovered, needs code fix and re-capture

## Screenshots to Review

### AuditLog Tutorial (en)
- [x] s-20.png - **Accepted** - Added "Detailed Info" menu item to AuditLogForm reports menu
- [x] s-23.png - **Reverted** - Only Skyline version change, not relevant to tutorial

### CustomReports Tutorial
#### English (en)
- [x] s-04.png - **Accepted** - Scrollbar thumb size change due to added report fields
- [x] s-06.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-08.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-09.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-12.png - **Accepted** - Edit Report scrollbar thumb size change due to added fields
- [x] s-15.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-17.png - **Accepted** - Slight change in ellipse drawing on top of screenshot
- [x] s-18.png - **Accepted** - Scrollbar thumb change
- [x] s-19.png - **Accepted** - Scrollbar thumb change
- [x] s-27.png - **Accepted** - New field "Imputed Peak" added, shifting names and scrollbar thumb

#### Japanese (ja)
- [x] s-04.png - **Accepted** - Scrollbar thumb size change due to added report fields
- [x] s-06.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-08.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-09.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-12.png - **Accepted** - Edit Report scrollbar thumb size change due to added fields
- [x] s-15.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-17.png - **Accepted** - Slight change in ellipse drawing on top of screenshot
- [x] s-19.png - **Accepted** - Scrollbar thumb change
- [x] s-27.png - **Accepted** - New field "Imputed Peak" added, shifting names and scrollbar thumb

#### Chinese (zh-CHS)
- [x] s-04.png - **Accepted** - Scrollbar thumb size change due to added report fields
- [x] s-06.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-08.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-09.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-12.png - **Accepted** - Edit Report scrollbar thumb size change due to added fields
- [x] s-15.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-17.png - **Accepted** - Slight change in ellipse drawing on top of screenshot
- [x] s-18.png - **Accepted** - Scrollbar thumb change
- [x] s-19.png - **Accepted** - Scrollbar thumb change
- [x] s-27.png - **Accepted** - New field "Imputed Peak" added, shifting names and scrollbar thumb

### DIA Tutorial
#### English (en)
- (no changes after BUG-002 and BUG-003 fixes)

#### Japanese (ja)
- [x] s-12.png - **Accepted** - Translation completed after 25.1 release

#### Chinese (zh-CHS)
- [x] s-12.png - **Accepted** - Translation completed after 25.1 release

### ExistingQuant Tutorial
#### Japanese (ja)
- [x] s-07.png - **Reverted** - 1px rendering artifact

#### Chinese (zh-CHS)
- [x] s-07.png - **Reverted** - 1px rendering artifact

### GroupedStudies Tutorial
#### English (en)
- [x] s-03.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-17.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-18.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-81.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-98.png - **BUG-004 FIXED** - AlertDlg text shifted left (LABEL_PADDING fix)

#### Japanese (ja)
- [x] s-03.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-17.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-18.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-81.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-98.png - **BUG-004 FIXED** - AlertDlg text shifted left (LABEL_PADDING fix)

#### Chinese (zh-CHS)
- [x] s-03.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-17.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-18.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-81.png - **Accepted** - Customize Reports scrollbar thumb size changed
- [x] s-98.png - **BUG-004 FIXED** - AlertDlg text shifted left (LABEL_PADDING fix)

### LibraryExplorer Tutorial (en)
- [x] s-14.png - **BUG-004 FIXED** - Alert text shift resolved
- [x] s-22.png - **BUG-004 FIXED** - Alert text shift resolved

### LiveReports Tutorial (en)
- [x] s-02.png - **Accepted** - Date-time values now use fixed test date (2025-01-01)
- [x] s-68.png - **Accepted** - Date-time values now use fixed test date
- [x] s-69.png - **Accepted** - Date-time values now use fixed test date

### MS1Filtering Tutorial
#### English (en)
- [x] s-09.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-15.png - **Accepted** - Slight RT alignment shift
- [x] s-19.png - **Accepted** - Slight RT alignment shift
- [x] s-20.png - **Accepted** - Slight RT alignment shift
- [x] s-28.png - **Accepted** - Peak selection changed due to RT alignment shift (see Future Work: Peak Scoring)
- [x] s-31.png - **Accepted** - Slight RT alignment shift
- [x] s-33.png - **Accepted** - Slight RT alignment shift
- [x] s-34.png - **Accepted** - Slight RT alignment shift
- [x] s-38.png - **Accepted** - Now correctly shows aligned view (old image was wrong due to commit 60e01d89 accidentally removing alignment)
- [x] s-42.png - **Accepted** - Slight RT alignment shift
- [x] s-43.png - **Accepted** - Slight RT alignment shift
- [x] s-44.png - **Accepted** - File compression changes due to format updates

#### Japanese (ja)
- [x] s-09.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-15.png - **Accepted** - Slight RT alignment shift
- [x] s-19.png - **Accepted** - Slight RT alignment shift
- [x] s-20.png - **Accepted** - Slight RT alignment shift
- [x] s-21.png - **Reverted** - X-axis labels flipped orientation (see Future Work)
- [x] s-28.png - **Accepted** - Peak selection changed due to RT alignment shift (see Future Work: Peak Scoring)
- [x] s-31.png - **Accepted** - Slight RT alignment shift
- [x] s-33.png - **Accepted** - Slight RT alignment shift
- [x] s-34.png - **Accepted** - Slight RT alignment shift
- [x] s-38.png - **Accepted** - Now correctly shows aligned view (old image was wrong)
- [x] s-42.png - **Accepted** - Slight RT alignment shift
- [x] s-43.png - **Accepted** - Slight RT alignment shift
- [x] s-44.png - **Accepted** - File compression changes due to format updates

#### Chinese (zh-CHS)
- [x] s-09.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-15.png - **Accepted** - Slight RT alignment shift
- [x] s-19.png - **Accepted** - Slight RT alignment shift
- [x] s-20.png - **Accepted** - Slight RT alignment shift
- [x] s-21.png - **Reverted** - X-axis labels flipped orientation (see Future Work)
- [x] s-28.png - **Accepted** - Peak selection changed due to RT alignment shift (see Future Work: Peak Scoring)
- [x] s-31.png - **Accepted** - Slight RT alignment shift
- [x] s-33.png - **Accepted** - Slight RT alignment shift
- [x] s-34.png - **Accepted** - Slight RT alignment shift
- [x] s-38.png - **Accepted** - Now correctly shows aligned view (old image was wrong)
- [x] s-42.png - **Accepted** - Slight RT alignment shift
- [x] s-43.png - **Accepted** - Slight RT alignment shift
- [x] s-44.png - **Accepted** - File compression changes due to format updates

### MethodEdit Tutorial (en)
- (no changes after BUG-002 fix - ExportMethodDlg radio buttons)

### MethodRefine Tutorial
#### English (en)
- [x] s-03.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-05.png - **Accepted** - RT regression graph changes
- [x] s-06.png - **Accepted** - RT regression graph changes
- [x] s-19.png - **Accepted** - Peptide Settings Prediction tab changes

#### Japanese (ja)
- [x] s-03.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-05.png - **Accepted** - RT regression graph changes
- [x] s-06.png - **Accepted** - RT regression graph changes
- [x] s-19.png - **Accepted** - Peptide Settings Prediction tab changes

#### Chinese (zh-CHS)
- [x] s-03.png - **Reverted** - Import progress monitor timing inconsistency (see Future Work)
- [x] s-05.png - **Accepted** - RT regression graph changes
- [x] s-06.png - **Accepted** - RT regression graph changes
- [x] s-19.png - **Accepted** - Peptide Settings Prediction tab changes

### OptimizeCE Tutorial (en)
- (no changes after BUG-002 fix - ExportMethodDlg radio buttons)

### PRM Tutorial
#### English (en)
- [x] s-09.png - **Accepted** - Edit Report form scrollbar thumb size change
- [x] s-13.png - **Accepted** - ImportPeptideSearchDlg height 524→547 (related to BUG-003 MinimumSize; may revisit)
- [x] s-15.png - **Reverted** - AllChromatogramsGraph timing inconsistency (see Future Work)
- [x] s-28.png - **Accepted** - Files view tab + peak shift from PR #3581 (terminal point fix affects chromatogram imputation/boundary detection)
- [x] s-30.png - **Accepted** - Peak shift from PR #3581
- [x] s-32.png - **Accepted** - Peak shift from PR #3581
- [x] s-33.png - **Accepted** - Files view tab + peak shift from PR #3581

#### Japanese (ja)
- [x] s-09.png - **Accepted** - Edit Report form scrollbar thumb size change
- [x] s-13.png - **Accepted** - ImportPeptideSearchDlg height 524→547 (related to BUG-003 MinimumSize; may revisit)
- [x] s-15.png - **Reverted** - AllChromatogramsGraph timing inconsistency (see Future Work)
- [x] s-28.png - **Accepted** - Files view tab + peak shift from PR #3581
- [x] s-30.png - **Accepted** - Peak shift from PR #3581
- [x] s-32.png - **Accepted** - Peak shift from PR #3581
- [x] s-33.png - **Accepted** - Files view tab + peak shift from PR #3581

#### Chinese (zh-CHS)
- [x] s-09.png - **Accepted** - Edit Report form scrollbar thumb size change
- [x] s-13.png - **Accepted** - ImportPeptideSearchDlg height 524→547 (related to BUG-003 MinimumSize; may revisit)
- [x] s-15.png - **Reverted** - AllChromatogramsGraph timing inconsistency (see Future Work)
- [x] s-28.png - **Accepted** - Files view tab + peak shift from PR #3581
- [x] s-30.png - **Accepted** - Peak shift from PR #3581
- [x] s-32.png - **Accepted** - Peak shift from PR #3581
- [x] s-33.png - **Accepted** - Files view tab + peak shift from PR #3581

### PeakPicking Tutorial (en)
- [x] s-01.png - **Accepted** - New "Preserve precursor mass" checkbox added to form
- [x] s-06.png - **Accepted** - More score types enabled (not checked) in Edit Peak Scoring Model form
- [x] s-09.png - **Accepted** - Same as s-06 + binoculars button icon added
- [x] s-18.png - **Accepted** - Files view tab added
- [x] s-20.png - **BUG-001 FIXED** - "Detailed Log" report now has AuditLog icon
- [x] s-25.png - **Accepted** - Different score enabling in Edit Peak Scoring Model form
- [x] s-26.png - **Accepted** - Same as s-25

### SmallMoleculeMethodDevCEOpt Tutorial
#### English (en)
- [x] s-05.png - **Accepted** - Waters instrument naming change (requested by Waters)
- (s-06, s-15, s-26, s-34 - no changes after re-recording)
- [x] s-14.png - **Accepted** - Molecule Settings Prediction tab changes (alignment, imputation)

#### Japanese (ja)
- (s-01 - no changes after re-recording)
- [x] s-04.png - **Accepted** - Same as en/s-05 (Waters naming)
- [x] s-13.png - **Accepted** - Same as en/s-14 (Prediction tab)
- **NOTE**: Numbering offset (ja s-04 = en s-05) suggests translations missing first screenshot - investigate

#### Chinese (zh-CHS)
- (s-01 - no changes after re-recording)
- [x] s-04.png - **Accepted** - Same as en/s-05 (Waters naming)
- [x] s-13.png - **Accepted** - Same as en/s-14 (Prediction tab)
- **NOTE**: Same numbering offset as Japanese - translations appear to be behind/missing screenshots

### SmallMoleculeQuantification Tutorial
#### Japanese (ja)
- [x] s-07.png - **Accepted** - Translation of "Advanced filtering" in Transition Settings Full-Scan tab

#### Chinese (zh-CHS)
- [x] s-07.png - **Accepted** - Translation of "Advanced filtering" in Transition Settings Full-Scan tab

## TestPerf Tutorial Screenshots

These tutorials are run from TestPerf project (longer-running tests).

### AcquisitionComparison Tutorial (en)
- [x] s-11.png - **Accepted** - Build Library form now shows AlphaPeptDeep option (PR #3123)
- [x] s-22.png - **Accepted** - Extract Chromatograms auto-detects files (PR #3477) - tutorial updated
- [x] s-28.png - **Accepted** - Context menu changes: "Exemplary Peak" added, "Align Times To..." and "Aligned" removed (RT alignment/imputation work)
- [x] s-36.png - **Accepted** - Same context menu changes as s-28

### DDASearch Tutorial
#### English (en)
- [x] s-11.png - **Accepted** - New "Edit Search Tools" button + MS1 tolerance control spacing adjustment
- [x] s-13.png - **Reverted** - Alert wrapping should be fixed by LABEL_RIGHT_PADDING = 18 (not verified due to test length)
- [x] s-16.png - **Reverted** - 1px rendering artifact (see Future Work: ImageComparer amplification)
- [x] s-17.png - **Accepted** - Files view tab added + chromatogram annotation shifts (RT alignment/imputation)

#### Japanese (ja)
- [x] s-11.png - **Accepted** - Same as en (Edit Search Tools button)
- [x] s-16.png - **Accepted** - Slightly higher bar for 1...100fmol replicate (integration area change from alignment)
- [x] s-17.png - **Accepted** - Same as en (Files tab + chromatogram shifts)

#### Chinese (zh-CHS)
- [x] s-11.png - **Accepted** - Same as en (Edit Search Tools button)
- [x] s-16.png - **Accepted** - Slightly higher bar for 1...100fmol replicate (integration area change from alignment)
- [x] s-17.png - **Accepted** - Same as en (Files tab + chromatogram shifts)

### DIA-PASEF Tutorial (en)
- [x] s-14.png - **Reverted** - AllChromatogramsGraph timing inconsistency
- [x] s-18.png - **Accepted** - Tab bold text change due to Files view insertion during layout load (SkylineFile.cs .sky.view handling)
- [x] s-19.png - **Accepted** - Same as s-18 (tab bold text)
- [x] s-23.png - **Accepted** - Full Scan spectrum intensity increased (~2300→3700) due to PR #3583 returning complete ion mobility frame instead of limited range - validated by s-24 showing additional ions below 1.1 1/K0
- [x] s-24.png - **Accepted** - Ion mobility plot now shows data below 1.1 1/K0 (PR #3583 returns full frame) - explains s-23 intensity increase from summing more ions
- [x] s-25.png - **Accepted** - Full frame view clearly shows complete 1/K0 range (~0.85-1.4) vs old slice (~1.1-1.4) - PR #3583 returns entire timsTOF frame
- [x] s-27.png - **Accepted** - BUG-010 fixed: Regression Refined line now drawn correctly
- [x] s-28.png - **Accepted** - BUG-010 fixed: "Peptides Refined" label now shown correctly

### DIA-QE Tutorial
#### English (en)
- [x] s-13.png - **Reverted** - AllChromatogramsGraph timing inconsistency
- [x] s-14.png - **Accepted** - mProphet model changes
- [x] s-17.png - **Accepted** - Tab bold text change due to Files view insertion during layout load (SkylineFile.cs .sky.view handling)
- [x] s-18.png - **Accepted** - Same as s-17 (tab bold text)
- [x] s-24.png - **Accepted** - Slight mass error histogram graph changes
- [x] s-25.png - **Accepted** - BUG-010 fixed: Regression Refined line now drawn correctly
- [x] s-26.png - **Accepted** - BUG-010 fixed: "Peptides Refined" label now shown correctly
- [x] s-28.png - **Accepted** - Slight group comparison change
- [x] s-29.png - **Accepted** - Slight group comparison change and volcano plot change
- [x] s-32.png - **Accepted** - Slight group comparison volcano plot change
- [x] s-33.png - **Accepted** - Slight group comparison bar plot change
- [x] s-34.png - **Accepted** - Slight group comparison bar plot change

#### Japanese (ja)
- [x] s-07.png - **Accepted** - Translation of "Advanced filtering" in Transition Settings Full-Scan tab
- [x] s-13.png - **Reverted** - AllChromatogramsGraph timing inconsistency
- [x] s-14.png - **Accepted** - mProphet model changes
- [x] s-17.png - **Accepted** - Tab bold text change due to Files view insertion during layout load (SkylineFile.cs .sky.view handling)
- [x] s-18.png - **Accepted** - Same as s-17 (tab bold text)
- [x] s-24.png - **Accepted** - Slight mass error histogram graph changes
- [x] s-25.png - **Accepted** - BUG-010 fixed: Regression Refined line now drawn correctly
- [x] s-26.png - **Accepted** - BUG-010 fixed: "Peptides Refined" label now shown correctly
- [x] s-28.png - **Accepted** - Slight group comparison change
- [x] s-29.png - **Accepted** - Slight group comparison change and volcano plot change
- [x] s-32.png - **Accepted** - Slight group comparison volcano plot change
- [x] s-33.png - **Accepted** - Slight group comparison bar plot change
- [x] s-34.png - **Accepted** - Slight group comparison bar plot change

#### Chinese (zh-CHS)
- [x] s-07.png - **Accepted** - Translation of "Advanced filtering" in Transition Settings Full-Scan tab
- [x] s-13.png - **Reverted** - AllChromatogramsGraph timing inconsistency
- [x] s-14.png - **Accepted** - mProphet model changes
- [x] s-17.png - **Accepted** - Tab bold text change due to Files view insertion during layout load (SkylineFile.cs .sky.view handling)
- [x] s-18.png - **Accepted** - Same as s-17 (tab bold text)
- [x] s-24.png - **Accepted** - Slight mass error histogram graph changes
- [x] s-25.png - **Accepted** - BUG-010 fixed: Regression Refined line now drawn correctly
- [x] s-26.png - **Accepted** - BUG-010 fixed: "Peptides Refined" label now shown correctly
- [x] s-28.png - **Accepted** - Slight group comparison change
- [x] s-29.png - **Accepted** - Slight group comparison change and volcano plot change
- [x] s-32.png - **Accepted** - Slight group comparison volcano plot change
- [x] s-33.png - **Accepted** - Slight group comparison bar plot change
- [x] s-34.png - **Accepted** - Slight group comparison bar plot change

### DIA-TTOF Tutorial
#### English (en)
- [x] s-14.png - **Accepted** - mProphet model changes
- [x] s-17.png - **Accepted** - Tab bold text change due to Files view insertion during layout load (SkylineFile.cs .sky.view handling)
- [x] s-18.png - **Accepted** - Same as s-17 (tab bold text)
- [x] s-25.png - **Accepted** - BUG-010 fixed: Regression Refined line now drawn correctly
- [x] s-26.png - **Accepted** - BUG-010 fixed: "Peptides Refined" label now shown correctly

#### Japanese (ja)
- [x] s-07.png - **Accepted** - Translation of "Advanced filtering" in Transition Settings Full-Scan tab
- [x] s-14.png - **Accepted** - mProphet model changes
- [x] s-17.png - **Accepted** - Tab bold text change due to Files view insertion during layout load (SkylineFile.cs .sky.view handling)
- [x] s-18.png - **Accepted** - Same as s-17 (tab bold text)
- [x] s-25.png - **Accepted** - BUG-010 fixed: Regression Refined line now drawn correctly
- [x] s-26.png - **Accepted** - BUG-010 fixed: "Peptides Refined" label now shown correctly

#### Chinese (zh-CHS)
- [x] s-07.png - **Accepted** - Translation of "Advanced filtering" in Transition Settings Full-Scan tab
- [x] s-14.png - **Accepted** - mProphet model changes
- [x] s-17.png - **Accepted** - Tab bold text change due to Files view insertion during layout load (SkylineFile.cs .sky.view handling)
- [x] s-18.png - **Accepted** - Same as s-17 (tab bold text)
- [x] s-25.png - **Accepted** - BUG-010 fixed: Regression Refined line now drawn correctly
- [x] s-26.png - **Accepted** - BUG-010 fixed: "Peptides Refined" label now shown correctly

### DIA-Umpire-TTOF Tutorial (en)
- [x] s-12.png - **Accepted** - New "Edit Search Tools" button + MS1 tolerance control spacing adjustment
- [x] s-13.png - **Accepted** - Slight change in search output text
- [x] s-19.png - **Accepted** - Tab bold text change due to Files view insertion during layout load (SkylineFile.cs .sky.view handling)
- [x] s-20.png - **Accepted** - Same as s-17 (tab bold text)
- [x] s-27.png - **Accepted** - BUG-010 fixed: Regression Refined line now drawn correctly
- [x] s-28.png - **Accepted** - BUG-010 fixed: "Peptides Refined" label now shown correctly

### HiResMetabolomics Tutorial (en)
- [x] s-07.png - Alert wrapping fixed - no change

### IMSFiltering Tutorial (en)
- [x] s-14.png - **Reverted** - 18 px change not visible in any way
- [x] s-17.png - **Reverted** - 11 px change not visible in any way
- [x] s-22.png - **Accepted** - Peptide Settings Prediction tab changes

### PRMOrbitrap Tutorial (en)
- [x] s-04.png - **Accepted** - Peptide Settings Prediction tab changes
- [x] s-16.png - **Accepted** - Orbitrap Fusion now has "Optimizing" dropdown enabled
- [x] s-20.png - **Accepted** - Orbitrap Fusion now has "Optimizing" dropdown enabled

### SmallMoleculeIMSLibraries Tutorial (en)
- [x] s-08.png - **Reverted** - AllChromatogramsGraph inconsistent timing

---

## TestTutorial Screenshots (Completed)

### iRT Tutorial
#### English (en)
- [x] s-06.png - **BUG-005 + BUG-010 FIXED** - Legend fixed + Regression Refined line now drawn
- [x] s-09.png - **Accepted**
- [x] s-10.png - **BUG-006 FIXED** - UnknownScore positioning restored
- [x] s-17.png - **BUG-005 + BUG-010 FIXED** - Legend fixed + Regression Refined line now drawn
- [x] s-18.png - **BUG-007 FIXED** - PeptideSettingsUI cramped control layout
- [x] s-19.png - **BUG-005 FIXED** - Unrefined regression text now shows when outliers exist
- [x] s-21.png - **Accepted**
- [x] s-22.png - **BUG-005 + BUG-010 FIXED** - Legend fixed + Regression Refined line now drawn (distinct from Predictor)
- [x] s-23.png - **Accepted**
- [x] s-24.png - **Accepted**
- [x] s-28.png - **BUG-005 + BUG-010 FIXED** - Legend fixed + Regression Refined line now drawn

#### Japanese (ja)
- [x] s-06.png - **BUG-005 + BUG-010 FIXED**
- [x] s-10.png - **BUG-006 FIXED**
- [x] s-17.png - **BUG-005 + BUG-010 FIXED**
- [x] s-18.png - **BUG-007 FIXED**
- [x] s-19.png - **BUG-005 FIXED**
- [x] s-22.png - **BUG-005 + BUG-010 FIXED**
- [x] s-23.png - **Accepted**
- [x] s-24.png - **Accepted**
- [x] s-28.png - **BUG-005 + BUG-010 FIXED**

#### Chinese (zh-CHS)
- [x] s-06.png - **BUG-005 + BUG-010 FIXED**
- [x] s-10.png - **BUG-006 FIXED**
- [x] s-17.png - **BUG-005 + BUG-010 FIXED**
- [x] s-18.png - **BUG-007 FIXED**
- [x] s-19.png - **BUG-005 FIXED**
- [x] s-22.png - **BUG-005 + BUG-010 FIXED**
- [x] s-23.png - **Accepted**
- [x] s-24.png - **Accepted**
- [x] s-28.png - **BUG-005 + BUG-010 FIXED**

## Summary Statistics

- **Total Screenshots**: ~195 files
- **Tutorials Affected**: 16
- **Languages**: English (en), Japanese (ja), Chinese Simplified (zh-CHS)
- **Baseline**: 25.1 release screenshots + Files view updates

## Bugs Found

### BUG-001: "Detailed Info" report missing icon in Manage Reports form
**Found in**: CustomReports s-06.png, s-08.png, s-09.png, s-15.png; PeakPicking s-20.png
**Description**: The new "Detailed Info" audit log report in Group: Main is missing an icon, while all other reports have icons.
**Root cause**: `SkylineViewContext._imageIndexes` dictionary didn't include `AuditLogRow` type, so `GetImageIndex()` returned -1 (no image).
**Fix**:
1. Renamed report from "Detailed Info" to "Detailed Log" to better indicate audit log context
2. Added `AuditLogRow` to `_imageIndexes` dictionary with index 8
3. Added `Resources.AuditLog` (16x16 bitmap) to `GetImageList()` array
**Files modified**:
- `pwiz_tools/Skyline/Controls/Databinding/SkylineViewContext.cs` - Added AuditLog to image list and index mapping
- `pwiz_tools/Skyline/Model/ModelResources.resx` - Renamed resource to `PersistedViews_GetDefaults_Detailed_Log`
- `pwiz_tools/Skyline/Model/ModelResources.designer.cs` - Updated auto-generated accessor
- `pwiz_tools/Skyline/Model/PersistedViews.cs` - Updated name map and XML view definition
**Status**: FIXED

### BUG-002: Radio buttons shifted in Export Isolation List form
**Found in**: DIA/en/s-06.png
**Description**: Group of 3 radio buttons in Export Isolation List form (ExportMethodDlg) shifted up and became unevenly spaced.
**Root cause**: PR #3386 (Waters Connect implementation) added a 4th radio button `wcDecideBuckets` and squeezed the existing 3 buttons to fit.
**Fix**: Dynamic positioning in `ExportMethodDlg.cs`:
- Added constant `RADIO_BUTTON_SHIFT_WC_HIDDEN = 11`
- When `wcDecideBuckets` is hidden (99% of cases), shift the 3 visible radio buttons down by 11px to restore original positions
- When `wcDecideBuckets` is visible (Waters Connect only), use designer positions with 24px even spacing
- Layout state detected by comparing button spacing (avoids redundant shifts)
**Files modified**:
- `ExportMethodDlg.cs` - Added shift logic and constant
- `ExportMethodDlg.resx` - Updated to 24px even spacing for 4-button layout
**Impact**: All ExportMethodDlg screenshots now match original positions; no screenshot changes needed
**Status**: FIXED

### BUG-003: ImportPeptideSearchDlg height increased 547px to 553px
**Found in**: DIA s-10.png (en, ja, zh-CHS)
**Description**: ImportPeptideSearchDlg form height increased by 6 pixels without otherwise changing. This affects many screenshots.
**Root cause**: `FullScanSettingsControl` has `AutoSize=True`. PR #3587 added `MinimumSize: 385, 425` which prevented the control from auto-sizing to its natural smaller height during `AdjustHeightForFullScanSettings()`.
**Fix**: Reduced MinimumSize from `385, 425` to `385, 419` in `FullScanSettingsControl.resx`
**Status**: FIXED

### BUG-004: AlertDlg message text shifted left by 6 pixels
**Found in**: GroupedStudies s-98.png (en, ja, zh-CHS) and any screenshot showing CommonAlertDlg
**Description**: Message text in alert dialogs shifted 6 pixels to the left compared to 25.1 baseline.
**Root cause**: PR #3386 (Waters Connect) added icon support to CommonAlertDlg with new `iconAndMessageSplitContainer`. The `labelMessage.Location` was changed from `24, 21` to `0, 21` in the designer, with `LABEL_PADDING = 18` used when no icon is shown. This differs from the original X position of 24.
**Fix**: Split `LABEL_PADDING` into two constants:
- `LABEL_LEFT_PADDING = 24` - matches original designer position
- `LABEL_RIGHT_PADDING = 12` - reduced from 18 to maintain same text width (24 + 12 = 36, same as original 18 + 18)
**Files modified**:
- `pwiz_tools/Shared/CommonUtil/GUI/CommonAlertDlg.cs` - Split padding constants to fix position while maintaining text width
**Impact**: All AlertDlg screenshots will match original positioning AND text wrapping; affects multiple tutorials showing alert dialogs
**Status**: FIXED

### BUG-005: iRT regression graph legend shows "Regression" and "Outliers" when no outliers exist
**Found in**: iRT s-06.png (en, ja, zh-CHS)
**Description**: The iRT regression graph legend shows "Regression" (purple, unrefined) and "Outliers" entries even when there are no outlier points. This causes the legend to wrap to two lines unnecessarily.
**Root cause**: In `RTLinearRegressionGraphPane.GraphCorrelation()`, the unrefined "Regression" line was always added when refinement is enabled, regardless of whether outliers exist. The "Outliers" curve was added when `Data.Outliers != null` instead of checking `Data.HasOutliers`.
**Fix**:
- Only add unrefined "Regression" line when `Data.HasOutliers` is true
- Changed `if (Data.Outliers != null)` to `if (Data.HasOutliers)` in both `GraphCorrelation()` and `GraphResiduals()`
**Files modified**:
- `pwiz_tools/Skyline/Controls/Graphs/RTLinearRegressionGraphPane.cs`
**Impact**: iRT regression graphs with no outliers will have cleaner single-line legends
**Status**: FIXED

### BUG-006: Peptides without library scores positioned at X=0 instead of UnknownScore
**Found in**: iRT s-10.png (en, ja, zh-CHS)
**Description**: Peptides without a score in the iRT library are shown at X=0 on the regression graph, instead of being positioned at approximately 20% below the lowest score (the `UnknownScore` value).
**Root cause**: The `_regressionIncludesMissingValues` feature in `RetentionTimeRegressionGraphData.Refine()` was setting missing X values to 0 instead of using the calculator's `UnknownScore`.
**Fix**: Changed `pt.X ?? 0` to `pt.X ?? _calculator?.UnknownScore ?? 0` in the Refine method.
**Files modified**:
- `pwiz_tools/Skyline/Model/RetentionTimes/RetentionTimeRegressionGraphData.cs`
**Impact**: Peptides without library scores will be correctly positioned below the score range, making them visually distinct from scored peptides.
**Status**: FIXED

### BUG-007: PeptideSettingsUI Prediction tab cramped control layout
**Found in**: iRT s-18.png (en)
**Description**: Control layout in PeptideSettingsUI Prediction tab was too cramped.
**Root cause**: Addition of alignment and imputation controls to the Prediction tab caused existing controls to be cramped.
**Fix**: Adjusted control layout spacing in PeptideSettingsUI.
**Files modified**:
- `pwiz_tools/Skyline/SettingsUI/PeptideSettingsUI.Designer.cs` and/or `.resx`
**Status**: FIXED

### BUG-008: Aligned ID time line shown for current file's own ID
**Found in**: MS1Filtering s-15.png (en, ja, zh-CHS)
**Description**: A light blue "aligned ID" vertical line appears in the chromatogram graph for a run's own MS/MS ID time. The aligned ID lines should only show times from OTHER runs aligned to the current run's time scale. The current run's own ID should only appear as the red "ID" line, not also as a blue aligned line.
**Root cause**: In `SrmSettings.GetAlignedRetentionTimes()`, when collecting aligned times from library source files, the current file was not excluded. This caused the current file's ID time to be:
1. Retrieved from the library
2. Normalized to iRT scale
3. Re-aligned back to the current file's scale
4. Displayed as a spurious "aligned" time (light blue line)
**Fix**: After building `spectrumSourceFiles`, exclude the current file by finding its library path and removing it from the set. If no batch filter exists (spectrumSourceFiles is null), create a set of all library files except the current one.
**Files modified**:
- `pwiz_tools/Skyline/Model/DocSettings/SrmSettings.cs` - Added exclusion logic in `GetAlignedRetentionTimes()`
**Status**: FIXED

### BUG-010: Regression line not drawn when refinement enabled but no outliers
**Found in**: DIA-PASEF s-27.png
**Description**: The "Score To Run Regression" graph shows "Regression Refined" in legend but no regression line is visible. Only the predictor line shows.
**Root cause**: BUG-005 fix changed condition from `if (!_refine)` to `if (!HasOutliers)` in label-drawing code, but line-drawing code at line 789-799 has inconsistent logic:
- When `Refine == true` but `HasOutliers == false`, it calls `GraphRegression(_statisticsRefined, ...)`
- But `_statisticsRefined` is null when there are no outliers (nothing to refine)
- `GraphRegression` adds legend entry but draws empty line (no visible curve)
**Fix**:
1. Changed regression line condition to `!Data.RegressionSettings.Refine || !Data.HasOutliers` - uses `_statisticsAll` when no outliers
2. Added `GetPointsLabel()` helper method that returns "Peptides Refined" when refinement is requested, regardless of outliers
3. Both `GraphCorrelation` and `GraphResiduals` now use this helper for consistent labeling
**Files modified**:
- `pwiz_tools/Skyline/Controls/Graphs/RTLinearRegressionGraphPane.cs`
**Impact**: Affects any RT regression graph where refinement is enabled but no outliers were found. Need to re-run iRT and DIA-PASEF tutorials after fix.
**Status**: FIXED

### BUG-009: MinimizeResultsDlg screenshot taken before statistics computation complete
**Found in**: MS1Filtering s-44.png (en, ja, zh-CHS)
**Description**: The Minimize Results form screenshot showed varying compression percentages (49% vs 70%) because the screenshot was taken before the background statistics computation completed.
**Root cause**: `MinimizeResultsDlg` uses a background thread to compute compression statistics. The test took the screenshot immediately after setting options, before `_minStatistics.PercentComplete` reached 100.
**Fix**:
1. Added `IsComplete` property to `MinimizeResultsDlg` that returns true when `PercentComplete == 100`
2. Added `WaitForConditionUI(() => minimizeResultsDlg.IsComplete)` before screenshot in test
**Files modified**:
- `pwiz_tools/Skyline/FileUI/MinimizeResultsDlg.cs` - Added `IsComplete` property
- `pwiz_tools/Skyline/TestTutorial/Ms1FullScanFilteringTutorial.cs` - Added wait before screenshot
**Status**: FIXED

## Reverted Screenshots

(List screenshots reverted to original)

## Accepted Changes

(List screenshots accepted as valid feature updates)

## Progress Tracking

### Current Status
- [x] ImageComparer review of all screenshots (complete - all 16 tutorials reviewed)
- [x] BUG-002 fixed (ExportMethodDlg dynamic radio button positioning)
- [x] BUG-003 fixed (FullScanSettingsControl MinimumSize)
- [x] BUG-004 fixed (CommonAlertDlg LABEL_PADDING)
- [x] BUG-005 fixed (RTLinearRegressionGraphPane legend/text visibility)
- [x] BUG-006 fixed (RetentionTimeRegressionGraphData UnknownScore positioning)
- [x] BUG-007 fixed (PeptideSettingsUI Prediction tab layout)
- [x] BUG-008 fixed (SrmSettings aligned ID time exclusion)
- [x] BUG-001 fixed (Detailed Log report icon and rename)
- [x] BUG-009 fixed (MinimizeResultsDlg wait for statistics completion)
- [x] Re-capture CustomReports and PeakPicking screenshots after BUG-001 fix
- [x] Final verification of BUG-001 affected screenshots

### ImageComparer Enhancements (2025-12-16)
- Added `Ctrl+Alt+C` to copy diff image to clipboard
- Added `Ctrl+S` to save diff image to `ai\.tmp\{Name}-{Locale}-s-{Number}-diff-{PixelCount}px.png`
- Moved all keyboard shortcuts to `ProcessCmdKey` override for proper handling
- F5 refresh now restores selection hierarchically (exact match → closest in tutorial/locale → tutorial start → beginning)

## Notes

- ImageComparer tool: `pwiz_tools/Skyline/Executables/DevTools/ImageComparer`
- Previous screenshot work: Files view feature (TODO-20251126_files_view.md)

## Future Work (Extract to backlog before merge)

### Screenshot Consistency Improvements

These items represent classes of screenshots that are frequently reverted because they cannot be captured consistently. Future work should make these deterministic so they don't appear as false-positive changes.

#### Import Progress Monitor (AllChromatogramsGraph)
**Affected**: GroupedStudies s-03, MethodRefine s-03, MS1Filtering s-09 (en, ja, zh-CHS) and potentially other tutorials
**Issue**: Import progress monitor form shows timing-dependent state that varies between runs
**Prior work**: Some work done to improve consistency, but still not 100% accurate
**Proposed**: Further investigation into making progress state deterministic during screenshot capture

#### Audit Log Version Display
**Affected**: AuditLog s-23 (reverted - only version change)
**Issue**: Audit Log form displays Skyline version number, causing false-positive diffs on version bumps
**Prior work**: Consistent dates/times already implemented for audit log screenshots (see ITimeProvider below)
**Proposed**: Create an `IVersionProvider` interface similar to `ITimeProvider` to allow tests to set a fixed version string

### Existing Pattern: ITimeProvider for Consistent Date/Times

The `ITimeProvider` mechanism is already implemented and working for audit log date/time consistency:

**Interface** (`pwiz_tools/Skyline/Model/AuditLog/AuditLogEntry.cs:560-573`):
```csharp
public interface ITimeProvider
{
    DateTime Now { get; }
}

/// <summary>
/// For consistent screenshots involving AuditLogEntries
/// </summary>
public static ITimeProvider TimeProvider { get; set; }

public static DateTime Now
{
    get { return TimeProvider?.Now ?? DateTime.UtcNow; }
}
```

**Test Implementation** (`pwiz_tools/Skyline/TestTutorial/AuditLogTutorialTest.cs:658-680`):
```csharp
public class TestTimeProvider : AuditLogEntry.ITimeProvider
{
    private readonly DateTime _startTime;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    private Random _random = new Random(1); // A consistent random series

    public TestTimeProvider()
    {
        // Start with a consistent local time of 2025-1-1 at 9:35 AM
        var localTime = new DateTime(2025, 1, 1, 9, 35, 0, DateTimeKind.Local);
        // The audit logging system expects a UTC time.
        _startTime = localTime.ToUniversalTime();
    }

    public DateTime Now
    {
        get
        {
            _elapsedTime += TimeSpan.FromSeconds(_random.Next(2, 10));
            return _startTime.Add(_elapsedTime);
        }
    }
}
```

**Usage** (`AuditLogTutorialTest.cs:98`):
```csharp
AuditLogEntry.TimeProvider = new TestTimeProvider();
```

**Tutorials using this**:
- LiveReports (s-02, s-68, s-69) - Now shows consistent 2025-01-01 dates

**Proposed IVersionProvider**:
Follow the same pattern to create an injectable version provider:
```csharp
public interface IVersionProvider
{
    string Version { get; }
}

public static IVersionProvider VersionProvider { get; set; }

public static string _skylineVersion =
    VersionProvider?.Version ??
    (string.IsNullOrEmpty(Install.Version)
        ? string.Format(@"Developer build, document format {0}", DocumentFormat.CURRENT)
        : Install.Version)
    + (Install.Is64Bit ? @" (64-Bit)" : string.Empty);
```

This would allow tests to set a fixed version string (e.g., "26.0.9.999 (64-Bit)") for screenshot consistency across development versions.

### Peak Scoring: ID Proximity to Peak Boundaries

#### Binary ID-Inside-Peak Scoring Issue
**Affected**: MS1Filtering s-28.png (all locales) - now shows incorrect peak selection
**Issue**: The current peak scoring model gives a binary score for whether an ID (or aligned ID) falls inside peak boundaries:
- Inside = full score contribution
- Outside = zero score contribution

This binary approach causes problems when an aligned ID shifts slightly due to RT alignment changes. In s-28, the aligned ID moved from just inside the correct peak's boundaries to inside a nearby interference peak's boundaries, causing Skyline to select the wrong peak.

**Observed behavior**:
- Original: Correct peak selected at ~36.9 RT with signal on all 3 isotope channels (precursor, M+1, M+2)
- After RT alignment change: Wrong peak selected at ~37.5-38.0 with signal only on precursor and M+2 (missing M+1, suggesting 2 Da heavier interference)
- The taller but isotopically-incorrect peak was chosen because it now has the aligned ID inside its boundaries

**Proposed improvement**: Implement graduated ID proximity scoring that gives partial credit for IDs slightly outside peak boundaries. This would allow other peak quality factors (isotope pattern, shape, intensity) to compete when the ID is near but not inside the peak.

**Tutorial impact**: The MS1Filtering tutorial now shows an anomalous peak selection result.
**Action taken**: Updated `pwiz_tools/Skyline/Documentation/Tutorials/MS1Filtering/en/index.html` to explain the interference scenario and instruct users how to manually select the correct peak by clicking and dragging beneath the x-axis.

**Priority**: Medium - affects real-world data analysis accuracy, not just screenshots

#### X-Axis Label Orientation Inconsistency
**Affected**: MS1Filtering s-21.png (ja, zh-CHS only)
**Issue**: X-axis labels on graphs can flip between horizontal and vertical orientation depending on available space. This is inconsistent between runs and causes screenshot diffs even when no functional change occurred.
**Proposed**: Consider making graphs slightly wider to ensure labels consistently remain horizontal, or implement deterministic label orientation during screenshot capture mode.

#### ImageComparer Diff Amplification for Minuscule Changes
**Affected**: DDASearch s-16.png (1px diff), ExistingQuant s-07.png (1px diff), and similar
**Issue**: When ImageComparer shows only 1-2 pixel differences, it's nearly impossible to visually locate the changed pixels to diagnose the root cause.
**Root cause hypothesis**: The `ScreenshotProcessingExtensions.CleanupBorder` algorithm that draws borders around screenshots (because window borders are translucent) may have an off-by-one error for certain window sizes. In Windows 10, this is just a single pixel border, but in Windows 11, the curved corners are translucent and must be reproduced consistently by CleanupBorder. The border pixel color may be determined by the background color behind the window.
**Proposed enhancements**:
1. Add a "diff-only" view mode that removes the image and paints only the diff pixels on a white background
2. Add an "Amplification" slider in the color picker dropdown that expands the visual area around each changed pixel (e.g., from 1:1 up to a calculated circle with radius 10 around each modified pixel)
3. This would help identify the exact pixel locations and potentially fix the border drawing algorithm to be 100% consistent

#### Automated Screenshot Review CLI Tool and Workflow
**Goal**: Enable autonomous screenshot review triggered by PR merges or scheduled CI runs, catching unintentional UI changes as they happen.

**Phase 1: CLI Diff Tool**
Extract ImageComparer's diff generation logic to a command-line tool that:
- Compares all modified screenshots against git HEAD
- Generates structured output to `ai/.tmp/screenshot-review/`:
  - `report.json` - machine-readable diff data (dimensions, pixel counts, file paths)
  - `report.md` - human-readable summary
  - `{Tutorial}-{Locale}-{Number}-diff.png` - diff images for each changed screenshot
  - Header metadata in diff images: pixel count, dimensions before/after, tutorial/locale info

**Phase 2: Claude Code Review Command**
Create a slash command (e.g., `/pw-screenshot-review`) that:
- Runs the CLI diff tool
- Reads the generated report and diff images
- Produces initial accept/revert/investigate recommendations with reasoning
- Groups changes by likely cause (Files view, RT alignment, control layout, etc.)
- Flags potential bugs for human review

**Phase 3: Autonomous CI Integration**
- Trigger screenshot generation on PR merge (requires standardized test environment)
- Run automated review and generate report
- Post results to PR or create tracking issue
- Human reviews recommendations and approves/overrides

**Vision**: Each code change is automatically reviewed for UI impact. Unintentional changes (like BUG-001 through BUG-009 found in this review) are caught when they happen, not months later during release preparation.
