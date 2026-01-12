# Release Cycle Guide

Quick reference for understanding where we are in the Skyline release cycle and what that means for daily work.

For detailed release procedures, see `ai/docs/release-guide.md`.

## Current State

**Phase**: FEATURE COMPLETE (Release Candidate)
**Release Branch**: `Skyline/skyline_26_1`
**Release Version**: 26.0.9.xxx
**Master Version**: 26.1.1.xxx
**Branch Created**: 2026-01-04

## Release Cycle Phases

### 1. Open Development (Normal)

**When**: After a major release, before next FEATURE COMPLETE

- Master is the primary development branch
- No active release branch (or release branch only for emergency patches)
- PRs merge freely to master
- Nightly tests run on master only
- No cherry-pick considerations

**Cherry-pick policy**: N/A - no active release branch

### 2. FEATURE COMPLETE / Release Candidate

**When**: Release branch created, preparing for major release

- **Release branch** receives bug fixes only (no new features)
- **Master** continues development for next release
- Both branches run nightly tests
- PRs may need cherry-picking to release branch

**Cherry-pick policy**:
- Bug fixes: Add "Cherry pick to release" label
- New features: Master only (no label)
- Refactoring: Usually master only unless it fixes a bug

**Test interpretation**:
- Same failure on both branches early in this phase = likely same code (branches just diverged)
- Same failure later = potentially systemic issue or cherry-picked bug

**Current release branch**: `Skyline/skyline_26_1`

### 3. Post-Release Patch Mode

**When**: Major release shipped, critical fixes may be needed

- Release branch used only for critical bug fixes
- Master continues normal development
- Cherry-picks are rare (critical fixes only)

**Cherry-pick policy**: Only critical bug fixes affecting released users

### 4. Release Branch Dormant

**When**: Release is stable, no patches expected

- Release branch exists but rarely touched
- All development on master
- Effectively same as Open Development

## Quick Decision Tree: Should I Cherry-Pick?

```
Is there an active release branch in RC/patch mode?
├── No → Just merge to master
└── Yes → Is this a bug fix?
    ├── No (feature/refactor) → Master only
    └── Yes → Add "Cherry pick to release" label
        └── Is it critical/blocking?
            ├── Yes → Consider direct commit to release branch
            └── No → Label is sufficient, auto-cherry-pick on merge
```

## Cherry-Pick Label Gotchas

The "Cherry pick to release" label triggers an automatic cherry-pick when a PR is merged. Two common issues can cause this to fail:

### 1. Deleting the PR branch too early

**Problem**: If you delete the source branch immediately after merging, the cherry-pick bot may not have time to create the cherry-pick PR.

**Solution**: Wait for the cherry-pick PR to be created before deleting the branch, or be prepared to manually cherry-pick if needed:
```bash
git checkout -b Skyline/work/YYYYMMDD_feature_release origin/Skyline/skyline_26_1
git cherry-pick <squash-merge-commit-hash>
git push -u origin Skyline/work/YYYYMMDD_feature_release
gh pr create --base Skyline/skyline_26_1
```

### 2. Merge commits in the PR history

**Problem**: If you update your branch with `git merge master` instead of rebasing, the merge commits interfere with the squash-and-merge process, causing the cherry-pick to fail or produce unexpected results.

**Solution**: Always update your branch with rebase:
```bash
git pull --rebase origin master
```

Or use the `/rebase` comment on the PR before squash-and-merge to have GitHub rebase your commits automatically.

## Nightly Test Interpretation

### When branches just diverged (early FEATURE COMPLETE)

- Master and release branch have nearly identical code
- Same failure on both = single issue, not "systemic across branches"
- Focus on fixing once, cherry-pick will sync both

### When branches have diverged significantly

- Same failure on both = may indicate:
  - Long-standing issue
  - External dependency problem (Koina, Panorama, etc.)
  - Test infrastructure issue
- Different failures = branch-specific changes

### Missing computers

- Check if computer is expected on that branch
- Some computers only run master, others run release branch
- BOSS-PC, SKYLINE-DEV1 may have configuration issues

## Version Number Reference

| Phase | Version Pattern | Example |
|-------|-----------------|---------|
| Daily (master) | YY.N.1.DDD | 26.1.1.007 |
| FEATURE COMPLETE | YY.0.9.DDD | 26.0.9.007 |
| Release | YY.N.0.DDD | 26.1.0.045 |

## Updating This Document

Update the "Current State" section when:
- Creating a new release branch (entering FEATURE COMPLETE)
- Shipping a major release (entering Post-Release Patch)
- Deciding release branch is dormant (entering Open Development)

Keep this document short - detailed procedures belong in `release-guide.md`.
