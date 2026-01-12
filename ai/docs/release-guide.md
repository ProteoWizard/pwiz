# Release Guide

Guide to Skyline release management, including version numbering, release types, workflows, and automation.

## Wiki Page Locations

Release documentation wiki pages are in two locations on skyline.ms:

| Container | Access | Pages |
|-----------|--------|-------|
| `/home/software/Skyline` | Public | Install pages, tutorials |
| `/home/software/Skyline/daily` | Semi-public (signup required) | Skyline-daily release announcements |
| `/home/development` | Authenticated | `release-prep`, `DeployToDockerHub`, dev tools |

See `ai/docs/mcp/wiki.md` for full wiki container documentation.

## Release Types

Skyline has four distinct release types, each with a different workflow:

| Release Type | Version Format | Branch | Purpose |
|--------------|----------------|--------|---------|
| **Skyline-daily (beta)** | `YY.N.1.DDD` | `master` | Ongoing development builds |
| **Skyline-daily (FEATURE COMPLETE)** | `YY.N.9.DDD` | `Skyline/skyline_YY_N` | Pre-release stabilization |
| **Skyline (release)** | `YY.N.0.DDD` | `Skyline/skyline_YY_N` | Official stable release |
| **Skyline (patch)** | `YY.N.0.DDD` | `Skyline/skyline_YY_N` | Bug fixes to stable release |

## Version Numbering

### Format: `YY.N.B.DDD`

| Component | Name | Values | Description |
|-----------|------|--------|-------------|
| `YY` | Year | 24, 25, 26... | Year of release (also base year for day calculation) |
| `N` | Ordinal | 0, 1, 2... | Release number within year (0 = first/unreleased, 1 = first official) |
| `B` | Branch | 0, 1, 9 | Build type: 0=release, 1=daily, 9=feature complete |
| `DDD` | Day | 001-365 | Zero-padded day of year from git commit date |

### Jamfile.jam Constants

```jam
constant SKYLINE_YEAR : 26 ;      # YY component, also base year for day calculation
constant SKYLINE_ORDINAL : 1 ;    # N component
constant SKYLINE_BRANCH : 1 ;     # B component: 0=release, 1=daily, 9=feature complete
```

### Day-of-Year Calculation (Reproducible Builds)

**Key change (2026-01-04)**: Day-of-year is now calculated from the **git commit date**, not the build machine date. This enables reproducible builds from any release tag.

```jam
# Uses git commit date (not JAMDATE) for reproducible versioning
local git_date = [ SHELL "git log -1 --format=%cs HEAD" ] ;
```

**Day calculation formula**:
```
DDD = (year_2digit - SKYLINE_YEAR) * 365 + day_of_year(commit_date)
```

This means:
- Rebuilding from a release tag produces the same version number
- Cherry-pick commits `2b169349f3` + `cee2c514d0` to any old release tag for reproducible builds
- Zero-padding (`004` not `4`) ensures chronological sorting in file listings

### Version Examples

| Version | Meaning |
|---------|---------|
| `26.1.1.004` | 2026, release 1, daily build, day 4 (Jan 4) |
| `26.0.9.004` | 2026, release 0, feature complete, day 4 |
| `26.1.0.045` | 2026, release 1, official release, day 45 (Feb 14) |
| `25.1.1.369` | 2025, release 1, daily, day 369 (crosses into 2026 = 365 + day 4) |

## Version-Format-Schema Dependency

**CRITICAL**: Version numbers, document format, and schema files must stay synchronized.

### The Constraint

When Skyline version changes to a new `YY.N` (e.g., 25.1 → 26.1), three things must be updated together:

1. **`DocumentFormat.CURRENT`** in `Model/DocSettings/DocumentFormat.cs`
2. **XSD schema file** `TestUtil/Schemas/Skyline_YY.N.xsd`
3. **`SkylineVersion.SupportedForSharing()`** must include the new version

### Why This Matters

Two automated tests enforce this constraint on daily builds (Build=1) and release builds (Build=0):

```csharp
// TestDocumentFormatCurrent - enforces version/format match
if (Install.Build > 1) return; // Skip for FEATURE COMPLETE (Build=9)
Assert.AreEqual(expectedVersion, DocumentFormat.CURRENT.AsDouble(), 0.099);

// TestMostRecentReleaseFormatIsSupportedForSharing
if (Install.Build > 1) return; // Skip for FEATURE COMPLETE (Build=9)
Assert.Fail("SupportedForSharing needs to include {0}.{1}", MajorVersion, MinorVersion);
```

These tests **skip** for FEATURE COMPLETE builds (Build=9) but **run** for daily builds (Build=1).

### Implication for Release Workflow

- **FEATURE COMPLETE (26.0.9)**: Tests skip, so no format/schema changes needed yet
- **Master during FEATURE COMPLETE**: Cannot update to 26.x because tests would run and fail
- **MAJOR release (26.1.0)**: Must update format, schema, and SupportedForSharing together
- **After MAJOR**: Master can update to 26.1.1, tests will pass

### Files to Update for MAJOR Release

| File | Change |
|------|--------|
| `Model/DocSettings/DocumentFormat.cs` | Add `FORMAT_26_1` constant, update `CURRENT` |
| `TestUtil/Schemas/Skyline_26.1.xsd` | Create new schema file |
| `SkylineVersion.cs` | Add 26.1 to `SupportedForSharing()` |
| `Jamfile.jam` (release branch) | Set `SKYLINE_BRANCH : 0` for release |
| `Jamfile.jam` (master) | Set `SKYLINE_YEAR : 26` after release branch format is updated |

## Release Folder Setup

Each major release uses a dedicated folder (e.g., `C:\proj\skyline_26_1`). This keeps release work separate from ongoing master development and maintains isolated build configurations.

**Folder naming convention**: `C:\proj\skyline_YY_N` (e.g., `skyline_26_1`, `skyline_25_1`)

### Why Separate Folders?

- **Master stays development-ready**: `C:\proj\pwiz` remains on master for ongoing work
- **Isolated build artifacts**: Each release has its own intermediate files
- **Preserved configuration**: Signing files, publish settings persist per-release
- **Historical reference**: Keep 1-2 previous release folders for debugging old versions

### Setup Steps for New Release Folder

**1. Clone the release branch:**
```bash
cd C:\proj
git clone --branch Skyline/skyline_YY_N https://github.com/ProteoWizard/pwiz.git skyline_YY_N
```

**2. Create build batch files** (required - contains vendor license agreement):
```bash
# b.bat - base build command
echo 'pwiz_tools\build-apps.bat 64 --i-agree-to-the-vendor-licenses toolset=msvc-14.3 %*' > b.bat

# bs.bat - build Skyline only
echo 'b.bat pwiz_tools\Skyline//Skyline.exe' > bs.bat

# bso.bat - official release build (Skyline + Installer + version check)
cat > bso.bat << 'EOF'
call b.bat pwiz_tools\Skyline//Skyline.exe --official
call b.bat pwiz_tools/Skyline/Executables/Installer//setup.exe --official
pwiz_tools\Skyline\bin\x64\Release\SkylineCmd --version
EOF
```

Note: These files cannot be in the repo because `--i-agree-to-the-vendor-licenses` is a legal acknowledgment each developer must make.

**3. Copy signing files** from previous release folder:
```bash
cp ../skyline_25_1/pwiz_tools/Skyline/SignAfterPublishKey.bat \
   ../skyline_25_1/pwiz_tools/Skyline/SignSimple.bat \
   "../skyline_25_1/pwiz_tools/Skyline/University of Washington (MacCoss Lab).crt" \
   pwiz_tools/Skyline/
```

**4. Copy and edit publish settings** (`.csproj.user`):
```bash
cp ../skyline_25_1/pwiz_tools/Skyline/Skyline.csproj.user pwiz_tools/Skyline/
```

Then edit `PublishUrlHistory` to update the ZIP path version:
- Change: `Skyline-daily-64_25_1_1_xxx` → `Skyline-daily-64_26_0_9_004`
- The ClickOnce path (T: drive) stays the same

Note: The third position matches the version branch type (1=daily, 9=feature complete, 0=release).

This is faster than navigating the VS Publish UI to create the folder.

**5. First build** to populate intermediate files:
```bash
clean.bat && bso.bat
```

### Transitioning from Previous Release

When starting FEATURE COMPLETE, set up the new release folder **before** making version changes:

1. Create `C:\proj\skyline_YY_N` as described above
2. Do all release work from the new folder
3. Keep `C:\proj\pwiz` on master for development

This way, `C:\proj\pwiz` is always ready for the next daily release after the major release.

## Release Workflows

### Skyline-daily (beta)

Regular daily builds from master branch. No special workflow - automated nightly builds.

**Version settings on master**:
```jam
constant SKYLINE_YEAR : 25 ;
constant SKYLINE_ORDINAL : 1 ;
constant SKYLINE_BRANCH : 1 ;  # daily
```

### Skyline-daily (FEATURE COMPLETE)

Pre-release stabilization period before official release.

**Key concepts:**
- **Release branch** (`Skyline/skyline_YY_N`): All FEATURE COMPLETE releases come from here
- **Master is release-frozen**: No Skyline-daily releases from master during this period
- **Master is development-open**: PRs merge freely, new features accumulate for next cycle
- **Version stays at 25.x on master**: Cannot update to 26.x until MAJOR release (see "Version-Format-Schema Dependency" below)

**Strategic goal**: By major release day, master has exciting new work. Release 26.1.0 (major) and 26.1.1 (daily) on the same day so Skyline-daily users see 26.1.1 as an upgrade with new features and stay on the daily track, rather than switching to nearly-identical 26.1.0.

**Pre-branch preparation**: FEATURE COMPLETE also means **UI Freeze**. In the days before creating the release branch:

- **Finalize tutorial screenshots**: Run automated screenshot capture and verify all screenshots look presentable with the final UI. This is a critical reality check before branching.
- **Finalize localized .resx files**: Run `FinalizeResxFiles` target to update .ja and .zh-CHS .resx files, adding comments for strings added since last release:
  ```cmd
  quickbuild pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer//FinalizeResxFiles
  ```
  This uses `Translation/LastReleaseResources.db` as the baseline. Commit the updated .resx files to master before branching.
- **Hold back non-release PRs**: Developers should hold back PRs not triaged for this release (prevents exclusion work after branch creation)
- **Set up release folder**: See "Release Folder Setup" section above

**Workflow**:

1. **Create release branch** from master: `Skyline/skyline_YY_N`

2. **Copy tutorials for the new version** (can do immediately after branch creation):
   ```cmd
   xcopy /E /I C:\proj\skyline_26_1\pwiz_tools\Skyline\Documentation\Tutorials T:\www\site\skyline.ms\html\tutorials\26-0-9
   ```

   This copies the tutorial HTML to a versioned directory on the web server (same T: drive used for ClickOnce publishing).
   The `tutorial.js` update to show the `[html 26.0.9]` link happens later (step 15).

3. **Generate translation CSV files** and send to translators (time-sensitive):
   ```cmd
   cd C:\proj\skyline_26_1
   b.bat pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer//GenerateLocalizationCsvFiles
   ```

   This creates `localization.ja.csv` and `localization.zh-CHS.csv` in `pwiz_tools\Skyline\Translation\Scratch\` containing strings needing translation. The CSV files include:
   - **Name**: Resource key (empty for consolidated entries shared across files)
   - **English**: The English text to translate
   - **Translation**: Empty column for translators to fill in
   - **Issue**: Any localization issues (e.g., "English text changed", "Inconsistent translation")
   - **FileCount/File**: Source .resx file(s) for context

   Send these to Japanese and Chinese translators immediately - translation time often determines release schedule.

   **When translations come back**, import them on the release branch:
   ```cmd
   cd C:\proj\skyline_26_1
   # Place translated CSVs in pwiz_tools\Skyline\Translation\Scratch\
   # (keeping same filenames: localization.ja.csv, localization.zh-CHS.csv)
   b.bat pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer//ImportLocalizationCsvFiles
   ```

   This imports translations into the .resx files and extracts the updated files. Verify the import succeeded by checking:
   - Build log shows "changed X/Y matching records in resx files"
   - Build ends with "SUCCESS"

   Commit the updated .resx files to the release branch, then merge to master (translations are one of the things that flow from release branch back to master, unlike release-only changes like renaming Skyline-daily to Skyline).

   See `pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer/README.md` for detailed ResourcesOrganizer documentation.

4. **Update cherry-pick workflow** in `.github/workflows/cherrypick-pr-to-release.yml`:
   - Change `pr_branch: 'Skyline/skyline_YY_N'` to the new branch name
   - Commit to master so the "Cherry pick to release" label works for the new branch

5. **Immediately notify dev team** (don't wait until end of release):

   > I have made and pushed the Skyline/skyline_YY_N release branch. Everyone needs to
   > consider whether a merge to master needs to be cherry-picked to the release branch
   > starting now. Use the "Cherry pick to release" label on PRs to automate this.
   > Also, master is now open for YY.N+1 development.
   >
   > Congratulations on reaching feature complete!

   This is time-sensitive: developers merging to master need to know immediately so they
   can use the cherry-pick label. Delayed notification causes manual cherry-pick work.

6. **Update TeamCity** to point to new release branch (do this immediately so commits trigger builds):
   - Go to: [ProteoWizard Project Parameters](https://teamcity.labkey.org/admin/editProject.html?projectId=ProteoWizard&tab=projectParams)
   - Update the release branch parameter to the new branch name (e.g., `skyline_26_1`)
   - This single parameter controls all release branch build configurations including Docker

   **Verify** all build configurations have transitioned to the new branch:
   - Core Windows x86_64 (Skyline release branch)
   - Skyline Release Branch x86_64
   - Skyline Release Branch Code Inspection
   - Skyline Release Perf and Tutorial tests
   - Skyline release TestConnected tests
   - ProteoWizard and Skyline (release branch) Docker container (Wine x86_64) - **Docker image build**

   Note: These configs appear in 3 separate alphabetical sections (Core..., ProteoWizard..., Skyline...)
   so they don't cluster together when scrolling through TeamCity.

   Check that each configuration shows a queued or running build for the new branch.

7. **Calculate the version** for today's commit date:
   ```
   DDD = (year - SKYLINE_YEAR) * 365 + day_of_year
   Example: Jan 4, 2026 with SKYLINE_YEAR=26 → DDD = (26-26)*365 + 4 = 004
   ```

8. **Set version on release branch** in both files (single commit):

   **Jamfile.jam**:
   ```jam
   constant SKYLINE_YEAR : 26 ;
   constant SKYLINE_ORDINAL : 0 ;   # Not yet released
   constant SKYLINE_BRANCH : 9 ;    # Feature complete
   ```

   **Skyline.csproj**:
   ```xml
   <ApplicationRevision>4</ApplicationRevision>
   <ApplicationVersion>26.0.9.004</ApplicationVersion>
   ```

   **WARNING**: Never commit `<SignManifests>true</SignManifests>`. This requires
   special certificate configuration and would break builds on other machines.
   Always revert this line before committing.

9. **DO NOT update master version yet**:

   Master stays at its current version (e.g., 25.1.1) during FEATURE COMPLETE.
   The version cannot be updated to 26.x until the MAJOR release because:
   - `DocumentFormat.CURRENT` must match the version (see "Version-Format-Schema Dependency")
   - Format updates require creating a new XSD schema file
   - Tests enforce this constraint and will fail if version/format mismatch

   Master version is updated during the MAJOR release workflow, not here.

10. **Build and test** from release branch (`clean.bat` + `bso.bat`)

   **IMPORTANT**: Verify you are building a **Release** build, not Debug. Developers often
   work with Debug builds. Double-check this before running tests - you want tests running
   against the Release build that will be published.

   **Troubleshooting**: If build fails with file lock errors on DLLs, check for leftover
   Docker Desktop containers from testing. Stop any remaining containers manually before
   rebuilding. (Most containers exit automatically, but occasionally some hold file locks.)

11. **Tag release commit**: `Skyline-daily-26.0.9.004`

12. **Publish installers** (5 steps):

   a. **ClickOnce to website** (Visual Studio Project Properties):

      **Publish tab**:
      - Publishing Folder Location: `T:\www\site\skyline.ms\html\software\Skyline-daily13-64\`
        (T: is mapped to the skyline.ms server through an internal Samba share)
      - Installation Folder URL: `https://skyline.gs.washington.edu/software/Skyline-daily13-64/`
      - Install Mode: "The application is available offline as well (launchable from Start menu)"
      - Publish Version: Set to match build (e.g., 26.0.9.004)
      - "Automatically increment revision with each publish" - **unchecked**

      **Updates button** (Application Updates dialog):
      - "The application should check for updates" - **unchecked**
      - Update location: `https://skyline.gs.washington.edu/software/Skyline-daily13-64/`

      **Signing tab**:
      - "Sign the ClickOnce manifests" - **checked**
      - Certificate: University of Washington (DigiCert, expires 2/28/2027)
      - "Sign the assembly" - **unchecked**

      Click **Publish Now**

   b. **ZIP to nexus server** (disk publish for disconnected install):

      Change VS Publish settings for disk (no URLs):
      - Publishing Folder Location: `M:\home\brendanx\tools\Skyline-daily\Skyline-daily-64_26_0_9_004\`
        (M: is mapped to the nexus server through an internal Samba share)
      - Installation Folder URL: **empty**
      - Updates > Update location: **empty**

      Click **Publish Now**, then:
      - ZIP the published folder to `Skyline-daily-64_26_0_9_004.zip`
      - Upload ZIP to skyline.ms FileContent module at `/home/software/Skyline/daily`

   c. **MSI to FileContent**:
      - Rename `bin\x64\Skyline-daily-26.0.9.004-x86_64.msi` to `Skyline-Daily-Installer-64_26_0_9_004.msi` (historical naming convention)
      - Upload to same FileContent location

   d. **Update wiki download pages**:
      - `install-disconnected-64` → point to new ZIP file
      - `install-administrator-64` → point to new MSI file

   e. **Test all downloads** to verify files are correctly linked

13. **Publish Docker image** to DockerHub (see `/home/development/DeployToDockerHub` wiki):
   - For FEATURE COMPLETE (release candidates): [Skyline-release-daily](https://teamcity.labkey.org/buildConfiguration/ProteoWizard_ProteoWizardPublishDockerImageSkylineReleaseDaily)
   - For final release: [Skyline Release Branch](https://teamcity.labkey.org/viewType.html?buildTypeId=ProteoWizard_ProteoWizardPublishDockerAndSingularityImagesSkylineReleaseBranch)

   **Before clicking Deploy**: Check Settings > Dependencies to verify dependency builds:
   - ProteoWizard and Skyline (release branch) Docker container (Wine x86_64) - **the actual Docker image**

   This config depends on:
   - Core Windows x86_64 (Skyline release branch)
   - Skyline Release Branch x86_64

   This is a double-check of work already verified in step 6. The Deploy button publishes
   from the Docker container build, not from a new build.

   - Verify at [DockerHub Tags](https://hub.docker.com/r/proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses/tags) after deployment

   **Optional: Test Docker image locally** (requires Docker Desktop in Linux container mode):
   ```bash
   # Pull the new image
   docker pull proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses:skyline_daily_26.0.9.004-7b2495b

   # Verify SkylineCmd works (use MSYS_NO_PATHCONV on Windows Git Bash)
   MSYS_NO_PATHCONV=1 docker run --rm --entrypoint /bin/bash \
     proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses:skyline_daily_26.0.9.004-7b2495b \
     -c "WINEPREFIX=/wineprefix64 wine /wineprefix64/drive_c/pwiz/skyline/SkylineCmd.exe --version"

   # Expected output:
   # Skyline-daily (64-bit : automated build) 26.0.9.004 (7b2495b)
   #     ProteoWizard MSData 3.0.26004
   ```

   Notes:
   - Docker Desktop must be in **Linux container mode** (right-click tray icon to switch)
   - The `MSYS_NO_PATHCONV=1` prefix prevents Git Bash from mangling Linux paths
   - Replace the tag with your actual release version

14. **Post release notes**:
   - Post to `/home/software/Skyline/daily` announcements (LabKey Wiki format with dashes)
   - Email to Skyline-daily list via **MailChimp** (formatted with bullet points)

   **Email list scope** (both are open signup, opt-in):
   - Major releases → entire active Skyline list (~23,500 users)
   - Skyline-daily releases → beta signup list only (~5,000 users)

   See `/home/development/User Signups` for signup analytics dashboard.

   **MailChimp workflow**:
   - Copy previous release email as template (preserves formatting and font sizes)
   - Replace version numbers in subject and body
   - Replace bullet list with new release notes
   - **Gotcha**: When copying from previous email, carefully remove ALL old content.
     Keeping one bullet to preserve font size can leave stale features at the end.
   - **Send test email to Claude** for review before sending to list. Claude can
     access the test email via Gmail MCP and verify: correct version numbers,
     no leftover content from previous releases, proper attributions, etc.
   - Query previous releases with `query_support_threads(container_path="/home/software/Skyline/daily")`
     to check if features were already announced

15. **Update tutorial.js to show version link** (after first release is published):

   Edit `pwiz_tools/Skyline/Documentation/tutorial.js` on the release branch:
   ```javascript
   var altVersion = '26-0-9';  // Update to match the release version
   ```

   This makes tutorial wiki pages show an `[html 26.0.9]` link to the versioned tutorials
   copied in step 2. Commit and push to the release branch.

16. Continue stabilization on release branch, daily development on master

**Why version calculation works**: Since versioning is now based on git commit date (not build time), we can calculate the exact version before committing. The tagged commit accurately represents the build.

### Skyline (release)

Official stable release.

**Workflow**:
1. Finalize stabilization on release branch
2. Set version for release:
   ```jam
   constant SKYLINE_YEAR : 26 ;
   constant SKYLINE_ORDINAL : 1 ;   # First official release
   constant SKYLINE_BRANCH : 0 ;    # Release
   ```
3. Build and deploy
4. Tag release commit: `Skyline-26.1.0.DDD`

### Skyline (patch)

Bug fixes to an existing stable release.

**Workflow**:
1. Work on existing release branch
2. Cherry-pick or commit fixes
3. Keep same version settings (BRANCH = 0)
4. Build and deploy
5. Tag: `Skyline-26.1.0.DDD` (new day number)

## Build Commands

### Full Release Build

```bash
# Clean build environment
pwiz_tools\Skyline\clean.bat

# Build 64-bit Skyline with all installers
pwiz_tools\Skyline\bso64.bat
```

### Quick Test Build

```bash
# Quick build to verify version
quickbuild.bat -j12 --abbreviate-paths pwiz_tools\Skyline//Skyline.exe --official
```

## Git Tags

### Tag Format

| Release Type | Tag Format | Example |
|--------------|------------|---------|
| Daily (beta) | `Skyline-daily-YY.N.1.DDD` | `Skyline-daily-25.1.1.147` |
| Feature Complete | `Skyline-daily-YY.N.9.DDD` | `Skyline-daily-26.0.9.004` |
| Official Release | `Skyline-YY.N.0.DDD` | `Skyline-26.1.0.045` |

### Creating Tags

```bash
# Create tag
git tag Skyline-daily-26.0.9.004

# Push tag
git push origin Skyline-daily-26.0.9.004
```

### Finding Tags

```bash
git fetch --tags origin
git tag -l "Skyline-daily-26*"
git show Skyline-daily-26.0.9.004 --no-patch
```

## Release Notes

### Locations

| Type | Location |
|------|----------|
| Major release | skyline.ms wiki: `/home/software/Skyline` → `Release%20Notes` |
| Skyline-daily | skyline.ms announcements: `/home/software/Skyline/daily` |

### Generating Skyline-daily Release Notes

**Step 1: Find commits since last release**

```bash
# Find last Skyline-daily tag
git fetch --tags origin
git tag -l "Skyline-daily-*" --sort=-version:refname | head -1

# Get commits since that tag
git log Skyline-daily-25.1.1.271..HEAD --oneline
```

**Step 2: Convert to user-facing summaries**

Transform developer commit messages into brief (single line) past tense summaries from a **user perspective**.

**Include:**
- Added features ("Added support for X")
- Updated functionality ("Updated method export for Thermo instruments")
- Fixed bugs ("Fixed crash when importing large files")
- Performance improvements (visible to users)

**Exclude:**
- Refactoring (internal code changes)
- Test changes
- Infrastructure/build improvements
- Anything invisible to users

**Step 3: Format each item**

- **Past tense, subjectless sentences** ending with period
- **Categories**: "Added...", "Updated...", "Fixed..."
- **New features**: Prefix with `**New!**` - use sparingly for major user-facing features only
- **Developer attribution**: `(thanks to Nick)` - not needed for Brendan (he sends the email)
- **Requester/reporter attribution**: `(requested by Philip)`, `(reported by Lillian)`
- **First names only** for all attributions
- **Look in commit body** for requester/reporter info (not in title)
- **Link to tutorials/webinars** when a feature has associated documentation (e.g., `https://skyline.ms/webinar27.url`)

### GitHub ID to Name Mapping

| GitHub ID | First Name |
|-----------|------------|
| brendanx67, Brendan MacLean | Brendan (omit - sends email) |
| nickshulman | Nick |
| Brian Pratt | Brian |
| Matt Chambers | Matt |
| Rita Chupalov | Rita |
| vagisha | Vagisha |
| Eddie O'Neil | Eddie |
| eduardo-proteinms | Eduardo |
| danjasuw | Dan |

**Examples:**
```
- **New!** Peak boundary imputation for DIA (https://skyline.ms/webinar27.url). (thanks to Nick)
- Added support for NCE optimization for Thermo instruments. (requested by Philip)
- Fixed MS Fragger download. (thanks to Matt)
- Fixed case where library m/z tolerance got multiplied improperly.
```

### Release Notes Templates

**FEATURE COMPLETE:**
```
Dear Skyline-daily Users,

I have just released Skyline-daily 26.0.9.DDD, our FEATURE COMPLETE release for
our next major release Skyline 26.1. This release also contains the following
improvements over the last release:

- [bullet points]

Skyline-daily should ask to update automatically when you next restart or use
Help > Check for Updates.

Thanks for using Skyline-daily and reporting the issues you find as we make
Skyline even better.

--Brendan
```

**Regular Skyline-daily:**
```
Dear Skyline-daily Users,

I have just released Skyline-daily 26.1.1.DDD. This release contains the
following improvements over the last release:

- [bullet points]

Skyline-daily should ask to update automatically when you next restart or use
Help > Check for Updates.

Thanks for using Skyline-daily and reporting the issues you find as we make
Skyline even better.

--Brendan
```

**First post-release daily:**
```
Dear Skyline-daily Users,

I have just released Skyline-daily 26.1.1.DDD, our first since the Skyline 26.1
release. This release contains the following improvements over Skyline 26.1:

- [bullet points]

...
```

### Querying Past Release Notes

```python
# List recent releases
query_support_threads(container_path="/home/software/Skyline/daily", days=365)

# Get specific release notes
get_support_thread(thread_id=69437, container_path="/home/software/Skyline/daily")
```

## Wiki Documentation

**Container**: skyline.ms → `/home/development`

| Page | Purpose |
|------|---------|
| `installers` | General installer overview |
| `ClickOnce-installers` | ClickOnce deployment |
| `WIX-installers` | WiX-based MSI installers |
| `release-prep` | Pre-release checklist |
| `test-upgrade` | Upgrade testing procedures |
| `renew-code-sign` | Certificate renewal |

## Tutorial Versioning System

Tutorial cover images on wiki pages are versioned to match Skyline releases. A centralized JavaScript system manages version numbers so they only need to be updated in one place.

### How It Works

Wiki pages use **placeholder version numbers** in image URLs that get rewritten by JavaScript:

| Placeholder | Rewrites To | Purpose |
|-------------|-------------|---------|
| `/tutorials/0-0/` | `/tutorials/25-1/` | Current stable release |
| `/tutorials/0-9/` | `/tutorials/26-0-9/` | Pre-release only (new tutorials) |

The `0-0` and `0-9` placeholders are obviously invalid versions, making it clear to anyone reading the HTML source that they will be rewritten.

### Configuration File

**Location**: `pwiz_tools/Skyline/Documentation/tutorial.js` (served as `/tutorials/tutorial.js`)

```javascript
var tutorialVersion = '25-1';       // Current stable release
var tutorialAltVersion = '26-0-9';  // Pre-release version (empty string if none)
```

The `rewriteTutorialUrls()` function transforms placeholder URLs to actual version paths when pages load.

### Wiki Pages Using This System

| Page | Language | Placeholders |
|------|----------|--------------|
| `tutorials` | English | 0-0, 0-9 |
| `tutorials_ja` | Japanese | 0-0 |
| `tutorials_zh` | Chinese | 0-0 |
| `default` | English (homepage slideshow) | 0-0, 0-9 |

Each page includes the script and calls `rewriteTutorialUrls()` at the end:
```html
<script src="/tutorials/tutorial.js"></script>
<script>rewriteTutorialUrls();</script>
```

### Release Updates

**FEATURE COMPLETE** (step 15 in workflow):
1. Copy tutorials to versioned directory (e.g., `/tutorials/26-0-9/`)
2. Update `tutorialAltVersion` in tutorial.js to show `[html 26.0.9]` link

**MAJOR release**:
1. Copy tutorials to release directory (e.g., `/tutorials/26-1/`)
2. Update `tutorialVersion` to the new release (e.g., `'26-1'`)
3. Clear `tutorialAltVersion` to empty string (no pre-release)

### Adding New Tutorials

For tutorials that only exist in pre-release (not yet in stable):
1. Use `0-9` placeholder in the image URL
2. Call `renderPagePreRelease()` instead of `renderPageRelease()` in the tutorial wiki page

When the tutorial is included in a stable release, change the placeholder to `0-0` and the render function to `renderPageRelease()`.

### Version Override Parameter

Tutorial wiki pages support `ver=` and `show=html` URL parameters:
```
https://skyline.ms/home/software/Skyline/wiki-page.view?name=tutorial_method_edit&show=html&ver=24-1
```

- `show=html` - Jump directly to tutorial content (with TOC and screenshots) instead of summary page
- `ver=24-1` - Override the default version to show tutorials from a specific release folder

Note: The short `.url` redirects (e.g., `/tutorial_method_edit.url`) don't preserve query parameters, so the full wiki URL is required for version-specific links.

### Future: Version-Locked Tutorial Links in Skyline

**Current state**: Tutorial links in `Skyline\Controls\Startup\TutorialLinkResources.resx` point to generic short URLs (e.g., `/tutorial_method_edit.url`), which always show the latest tutorial version.

**Potential enhancement**: Major releases could use full wiki URLs with version parameters:
```
/home/software/Skyline/wiki-page.view?name=tutorial_method_edit&show=html&ver=25-1
```

This would ensure users on Skyline 25.1 always see tutorials matching their UI, even after newer tutorials are published. The `ver=` parameter already works - only the .resx URLs would need updating per release.

## Future Automation: `/pw-release` Command

**Vision**: A guided slash command that walks through release workflows step-by-step.

| Type | Purpose | Documentation Status |
|------|---------|---------------------|
| `complete` | FEATURE COMPLETE release - create branch, publish, announce | **Fully documented** (see workflow above) |
| `major` | Official stable release (e.g., 26.1.0) | Placeholder - expand when performed |
| `patch` | Bug fix to existing release | Placeholder - expand when performed |
| `rc` | Release candidate (repeat of complete workflow on existing branch) | Placeholder - expand when performed |

**Note**: `daily` builds are automated nightly from master and don't need a command.

### What Each Type Involves

**`complete`** (most complex - fully documented):
- Create release branch from master
- Update TeamCity project parameters
- Notify dev team immediately
- Update cherry-pick workflow
- Set version to `YY.0.9.DDD`
- Build, test, publish (ClickOnce, ZIP, MSI)
- Update wiki download pages
- Tag release, deploy Docker, send MailChimp email

**`major`** (similar to complete, differences):
- No new branch (already on release branch)
- Set version to `YY.N.0.DDD` (BRANCH=0)
- Email to full Skyline list (~23,500 users)
- More extensive testing/validation

**`patch`** (subset of complete):
- Work on existing release branch
- Cherry-pick or commit fixes
- Build, test, publish
- Tag with new day number
- Smaller announcement

**`rc`** (release candidate):
- Similar to `complete` but on existing branch
- Incremental testing cycle
- May have multiple RCs before major release

## Related Documentation

- **ai/docs/version-control-guide.md** - Git conventions
- **ai/docs/build-and-test-guide.md** - Building and testing
- **Jamroot.jam** - ProteoWizard version (MAKE_BUILD_TIMESTAMP)
- **pwiz_tools/Skyline/Jamfile.jam** - Skyline version constants
