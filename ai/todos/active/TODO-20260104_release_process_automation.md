# TODO-20260104_release_process_automation.md

## Branch Information
- **Branch**: `Skyline/work/20260104_release_process_automation`
- **Base**: `master`
- **Created**: 2026-01-04
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Improve and automate Skyline release process with reproducible versioning

## Background

The Skyline release process involves multiple manual steps and has some historical quirks:
- Version numbers use the pattern `YY.N.M.DDD` where DDD is day-of-year
- The day-of-year was previously computed from build date (JAMDATE), making rebuilds non-reproducible
- Release branches, tagging, and version bumping require careful coordination
- No centralized documentation of the complete release workflow

## Goals

1. **Reproducible Versioning**: Version numbers tied to git commit date, not build date
2. **Consistent Formatting**: Zero-padded day-of-year for chronological sorting (`004` vs `4`)
3. **Documented Workflow**: Comprehensive release guide capturing all steps
4. **Reduced Manual Errors**: Clear, sequential process for releases

## Implementation

### Phase 1: Jamfile.jam Versioning Enhancements

- [x] Analyze existing version computation in Jamfile.jam
- [x] Implement git-based date extraction (`git log -1 --format=%ci HEAD`)
- [x] Add zero-padding to day-of-year (DDD format: `001`-`365`)
- [x] Test version generation with different dates
- [x] Verify reproducibility (same version from rebuild)

### Phase 2: Release Workflow Execution

- [x] Commit git-based versioning to master (both Jamroot.jam and Jamfile.jam)
- [x] Commit zero-padding enhancement to master
- [x] Create release branch `Skyline/skyline_26_1` from master
- [x] Set release branch to 26.0.9 versioning, commit and push
- [x] Build official release with `clean.bat` and `bso64.bat`
- [x] Tag release commit `Skyline-daily-26.0.9.004`
- [x] Publish release (ClickOnce, ZIP, MSI)
- [x] Update wiki download pages
- [x] Send MailChimp release email
- [x] Notify dev team via Zoom meeting
- [x] Update cherry-pick workflow to target new branch (pending commit)
- [ ] Update master to 26.1.1 versioning, commit and push

### Phase 3: Documentation

- [x] Write release notes for FEATURE COMPLETE announcement (draft below)
- [x] Update comprehensive release guide in `ai/docs/release-guide.md`
- [x] Document version numbering scheme
- [x] Document branching and tagging conventions
- [x] Add release checklist (integrated into release-guide.md)

## Technical Details

### Version Number Format
```
YY.N.M.DDD
│  │ │ └── Day of year (001-365), zero-padded
│  │ └──── Build type: 0=daily, 1=official
│  └────── Release number within year
└───────── Year - base year (25 for 2025)
```

### Git Date Extraction
```jam
# Old: Used JAMDATE (build machine date)
rule GetDayOfYear { return $(JAMDATE[5]) ; }

# New: Uses git commit date
actions GetGitCommitDate {
    git log -1 --format=%ci HEAD > "$(<)"
}
```

### Zero-Padding Implementation
```jam
rule ZeroPad3 val {
    local str = $(val) ;
    if $(val:E=0) < 10 { str = "00$(val)" ; }
    else if $(val:E=0) < 100 { str = "0$(val)" ; }
    return $(str) ;
}
```

## Files Modified

- `pwiz_tools/Skyline/Jamfile.jam` - Version computation rules (git-based date, zero-padding)
- `Jamroot.jam` - ProteoWizard version (git-based MAKE_BUILD_TIMESTAMP)
- `pwiz_tools/Skyline/Skyline.csproj` - Version 26.0.9.004 on release branch
- `.github/workflows/cherrypick-pr-to-release.yml` - Updated target branch (pending commit)
- `ai/docs/release-guide.md` - Comprehensive release workflow documentation
- `ai/docs/mcp/wiki.md` - Wiki container documentation

## Progress Log

### 2026-01-04
- Analyzed existing Jamfile.jam version logic
- Identified JAMDATE dependency causing non-reproducible builds
- Implemented git commit date extraction via `git log -1 --format=%cs HEAD`
- Added ZeroPad3 rule for consistent DDD formatting
- Tested with date manipulation: git date (Jan 4) produces same version regardless of system date
- Verified: `26.0.9.004` generated correctly with zero-padding
- Committed git-based versioning to master:
  - `2b169349f3` - Skyline Jamfile.jam
  - `cee2c514d0` - ProteoWizard Jamroot.jam (cherry-pick both for reproducible builds)
  - `d36891d89d` - Zero-padding enhancement
- Created release branch `Skyline/skyline_26_1`
- Set version to 26.0.9 on release branch (`7993aae832`)
- Updated `ai/docs/release-guide.md` with:
  - Four release types and workflows
  - Version numbering scheme
  - Release notes generation process
  - GitHub ID to name mapping
- Drafted FEATURE COMPLETE release notes (see below)
- Successful test build with correct versioning:
  - Skyline-daily (64-bit) 26.0.9.004 (7993aae832)
  - ProteoWizard MSData 3.0.26004
  - Code signing successful
- Running tests (30-45 min)
- Tests passed
- Added .csproj version update as new commit (`7b2495b686`)
- Updated TeamCity VCS root to point to `Skyline/skyline_26_1`
- Updated release-guide.md with TeamCity step (do immediately after branch creation)
- Ready for morning rebuild and publish

### 2026-01-05
- Discovered: Must verify ALL TeamCity build configurations transitioned to new branch:
  - Core Windows x86_64 (Skyline release branch) - **Docker dependency**
  - Skyline Release Branch x86_64 - **Docker dependency**
  - Skyline Release Branch Code Inspection
  - Skyline Release Perf and Tutorial tests (has different VCS config!)
  - Skyline release TestConnected tests
- Updated release-guide.md with this critical verification step
- Build verified: Skyline-daily (64-bit) 26.0.9.004 (7b2495b686)
- Documented detailed publish workflow in release-guide.md:
  - ClickOnce via VS Publish
  - ZIP to nexus server + FileContent
  - MSI rename and upload
  - Wiki download page updates
  - MailChimp email to Skyline-daily list
- MailChimp email prepared and ready
- Tests running (~2/3 complete, passing)
- Test email reviewed via Gmail MCP:
  - Caught leftover "Peptide Search with Tide" from previous release (25.1.1.271)
  - Documented MailChimp workflow and test email review process in release-guide.md
- Tagged release: Skyline-daily-26.0.9.004
- Posted release notes to skyline.ms/daily
- Discovered Docker build config also needs branch update
- **Improvement**: Matt parameterized release branch in TeamCity project params
  - Single location: https://teamcity.labkey.org/admin/editProject.html?projectId=ProteoWizard&tab=projectParams
  - Controls all release branch configs including Docker builds
- **Completed publish workflow**:
  - ClickOnce publish to T: drive (skyline.ms)
  - ZIP uploaded to nexus server (M: drive) and FileContent
  - MSI renamed and uploaded to FileContent
  - Wiki download pages updated (download-64.html, downloadInstaller-64.html)
  - Downloads tested successfully
- Sent MailChimp release email to Skyline-daily list (~5,000 users)
- Notified dev team via Zoom meeting about:
  - New release branch `Skyline/skyline_26_1`
  - Cherry-pick policy using "Cherry pick to release" label
- Updated `ai/docs/mcp/wiki.md` with wiki container documentation:
  - Public (`/home/software/Skyline`) vs Authenticated (`/home/development`)
  - Event page hierarchy
- Updated `ai/docs/release-guide.md` with:
  - VS Publish settings (screenshots documentation)
  - Disk publish settings for disconnected install
  - Docker deployment with correct dependency chain
  - MailChimp workflow with test email review
  - Email list scope (~23,500 major, ~5,000 daily)
  - Dev team notification moved to step 2 (immediate after branch creation)
  - Cherry-pick workflow update added as step 2
  - Troubleshooting for Docker file locks and Release/Debug builds
- Modified `.github/workflows/cherrypick-pr-to-release.yml`:
  - Changed `pr_branch` from `skyline_25_1` to `skyline_26_1`
  - **Pending commit to master**
- Identified UpgradeDlg version display fix (documented in Follow-up Tasks)

### Remaining Tasks - Completion Workflow

**Step 0: Revert local cherry-pick workflow change** (noted below, will re-apply in PR)

**Step 1: Commit documentation to ai-context**
```bash
git stash
git checkout ai-context
git pull origin ai-context
git stash pop
# Commit a) ai/docs/release-guide.md + ai/docs/mcp/wiki.md
# Commit b) ai/todos/active/TODO-20260104_release_process_automation.md
git push origin ai-context
```

**Step 2: Create work branch from master**
```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/20260104_release_process_automation
git cherry-pick <TODO-commit-from-ai-context>
```

**Step 3: Create 3 code commits on work branch**

See "Code Changes for PR" section below for exact changes.

1. **Version update** - Update master to 26.1.1 versioning
2. **Cherry-pick workflow** - Target new release branch
3. **UpgradeDlg fix** - Fix version display for FEATURE COMPLETE

**Step 4: Push and create PR**
```bash
git push -u origin Skyline/work/20260104_release_process_automation
gh pr create --base master --title "..." --body "..."
```

**Step 5: Verify Docker deployment**
- [ ] Check DockerHub for new proteowizard/pwiz-skyline-i-agree images

---

## Code Changes for PR

### Commit 1: Update master to 26.1.1 versioning

**File:** `pwiz_tools/Skyline/Jamfile.jam`

Change line 56:
```jam
# Before:
constant SKYLINE_YEAR : 25 ;

# After:
constant SKYLINE_YEAR : 26 ;
```

This changes daily builds from `25.1.1.DDD` to `26.1.1.DDD`.

### Commit 2: Update cherry-pick workflow for new release branch

**File:** `.github/workflows/cherrypick-pr-to-release.yml`

Change line 19:
```yaml
# Before:
pr_branch: 'Skyline/skyline_25_1'

# After:
pr_branch: 'Skyline/skyline_26_1'
```

This ensures PRs labeled "Cherry pick to release" are automatically cherry-picked to the new release branch.

### Commit 3: Fix UpgradeDlg version display for FEATURE COMPLETE releases

**File:** `pwiz_tools/Skyline/UpgradeManager.cs`

**Problem:** When upgrading from 25.1.1.xxx to 26.0.9.004, the upgrade dialog shows "Skyline-daily 26.0 has been released!" instead of "26.0.9.004". This makes a FEATURE COMPLETE update look like a major release.

**Root cause:** `GetVersionDiff()` (lines 221-226) abbreviates version to "Major.Minor" whenever Major or Minor changes, but Branch=9 (FEATURE COMPLETE) isn't a major release.

**Fix:** Replace `GetVersionDiff()` method:
```csharp
private static string GetVersionDiff(Version versionCurrent, Version versionAvailable)
{
    // For Skyline versioning: Major.Minor.Build.Revision = YY.N.B.DDD
    // B=0 is release, B=1 is daily, B=9 is feature complete
    // Only show abbreviated version (YY.N) for actual releases (Build=0)
    // when the release number (YY.N) has changed
    bool majorUpgrade = versionCurrent.Major != versionAvailable.Major ||
                        versionCurrent.Minor != versionAvailable.Minor;
    bool isRelease = versionAvailable.Build == 0;

    if (majorUpgrade && isRelease)
        return string.Format(@"{0}.{1}", versionAvailable.Major, versionAvailable.Minor);
    return versionAvailable.ToString();
}
```

---

## Related

- `pwiz_tools/Skyline/Jamfile.jam` - Build configuration
- `pwiz_tools/Skyline/UpgradeManager.cs` - Upgrade dialog version display
- `Jamroot.jam` - ProteoWizard version (MAKE_BUILD_TIMESTAMP)
- `ai/docs/release-guide.md` - Comprehensive release documentation
- `/pw-configure` - Developer environment setup
- Release branch naming: `Skyline/skyline_YY_N`

## Draft Release Notes: Skyline-daily 26.0.9.DDD (FEATURE COMPLETE)

```
Dear Skyline-daily Users,

I have just released Skyline-daily 26.0.9.DDD, our FEATURE COMPLETE release for
our next major release Skyline 26.1. This release also contains the following
improvements over the last release:

- **New!** Peak boundary imputation for DIA (https://skyline.ms/webinar27.url). (thanks to Nick)
- **New!** Files view to display and manage document-related files. (View > Files, Alt+9)
- Added extended keyboard shortcut help with grid shortcuts.
- Added library spectrum match graph peak tooltips. (thanks to Rita)
- Added Waters connect method export. (thanks to Rita)
- Added "Replicate" column to Peptide Normalized Areas and Protein Abundances reports. (thanks to Nick)
- Added "Protein Abundances" and "Peptide Normalized Areas" default reports. (thanks to Nick)
- Added "TransitionIonMetrics" columns to Document Grid. (thanks to Nick)
- Added sorting and grouping replicates on list lookup columns. (thanks to Nick)
- Added support for Constant Neutral Loss/Gain scans in Spectrum Filter. (reported by Cristina, thanks to Brian)
- Updated Shimadzu API to IoModule 5.0. (thanks to Matt)
- Updated Thermo Stellar SRM spectra handling. (thanks to Matt)
- Fixed retention time alignment performance problem. (thanks to Nick)
- Fixed "Apply Peak to All" ArgumentOutOfRangeException. (thanks to Nick)
- Fixed Candidate Peaks grid when replicate has multiple injections. (thanks to Nick)
- Fixed synchronized zoom with multiple panes and/or inactive tabs. (thanks to Nick)
- Fixed iRT alignment in older documents. (thanks to Nick)
- Fixed crashes in Wine/Docker when importing Bruker TIMS data. (thanks to Matt)
- Fixed handling of libraries containing nonsensical entries. (thanks to Brian)
- Improved Relative Abundance graph performance with background computation.
- Improved peak imputation performance. (thanks to Nick)

Skyline-daily should ask to update automatically when you next restart or use
Help > Check for Updates.

Thanks for using Skyline-daily and reporting the issues you find as we make
Skyline even better.

--Brendan
```
