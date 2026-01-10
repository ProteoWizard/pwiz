# Version Control Guide

Detailed conventions for Git commits, PRs, and branch management in Skyline/ProteoWizard.

## Commit Message Format

All commits MUST follow this exact format:

```
<Title line in past tense>

* <bullet point 1>
* <bullet point 2>
* <bullet point 3>

See ai/todos/active/TODO-YYYYMMDD_feature_name.md

Co-Authored-By: Claude <noreply@anthropic.com>
```

### Format Rules

| Element | Rule |
|---------|------|
| Title | Single line, **past tense** ("Added", "Fixed", "Moved" - NOT "Add", "Fix") |
| Bullets | 1-5 points, each starting with `* ` (asterisk + space) |
| TODO reference | `See ai/todos/active/TODO-YYYYMMDD_feature_name.md` |
| Co-authorship | Exactly `Co-Authored-By: Claude <noreply@anthropic.com>` |
| Total lines | Maximum 10 lines including blank lines |
| Prohibited | Emojis, markdown links |

### Example

```
Fixed alert dialog timeout in functional tests

* Added ShowWithTimeout method to catch unexpected dialogs
* Timer closes dialog after 10 seconds in test mode
* Throws TimeoutException with dialog message for debugging

See ai/todos/active/TODO-20251217_alert_timeout.md

Co-Authored-By: Claude <noreply@anthropic.com>
```

### Creating Commits with HEREDOC

Use HEREDOC for proper formatting:

```bash
git commit -m "$(cat <<'EOF'
Fixed alert dialog timeout in functional tests

* Added ShowWithTimeout method to catch unexpected dialogs
* Timer closes dialog after 10 seconds in test mode
* Throws TimeoutException with dialog message for debugging

See ai/todos/active/TODO-20251217_alert_timeout.md

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

## Pull Request Format

```markdown
## Summary
- Bullet point summarizing change 1
- Bullet point summarizing change 2
- Bullet point summarizing change 3

## Test plan
- [x] Test that was run
- [x] Another test that was run

See ai/todos/active/TODO-YYYYMMDD_feature_name.md

Co-Authored-By: Claude <noreply@anthropic.com>
```

## Branch Naming Convention

**Format**: `Skyline/work/YYYYMMDD_feature_name`

- Use today's date (YYYYMMDD)
- Use snake_case for feature name
- Examples:
  - `Skyline/work/20251217_alert_timeout`
  - `Skyline/work/20251218_files_view_fix`

## Finding Current TODO

```bash
# Get branch name
git branch --show-current
# Output: Skyline/work/20251217_feature_name

# TODO location: ai/todos/active/TODO-20251217_feature_name.md
```

## Amending Commits

For small updates (TODO PR link, typo fix):

```bash
git add <files>
git commit --amend --no-edit
git push --force-with-lease
```

Only amend if:
- Commit was created by you in this session
- Change is small
- You will force-push immediately

## Slash Commands

| Command | Purpose |
|---------|---------|
| `/pw-pcommit` | Propose commit message from staged changes |
| `/pw-pcommitfull` | Full pre-commit with TODO update and message proposal |
| `/pw-uptodo` | Update current branch TODO with progress |

## Checklist Before Commit

- [ ] Title in past tense
- [ ] 1-5 bullet points with `* ` prefix
- [ ] TODO reference included
- [ ] Co-Authored-By line at end
- [ ] No emojis or markdown links
- [ ] ≤10 total lines

## Cherry-Picking to Release Branch

During FEATURE COMPLETE phase, bug fixes often need to go to both master and the release branch. See `ai/docs/release-cycle-guide.md` for current release state.

### Automatic Cherry-Pick (Preferred)

Add the **"Cherry pick to release"** label to your PR before merging. The bot will create a cherry-pick PR automatically.

### Manual Cherry-Pick

Use `/pw-cptorelease <PR#>` when:
- Automatic cherry-pick failed (branch deleted too early, merge commits in history)
- You forgot to add the label before merging
- You want a more informative PR description

**Manual cherry-pick steps:**
```bash
# 1. Find the merge commit
git fetch origin master
git log --oneline origin/master | grep "#<PR#>"

# 2. Create branch from release branch
git checkout -b Skyline/work/YYYYMMDD_feature_release origin/Skyline/skyline_XX_X

# 3. Cherry-pick
git cherry-pick <merge-commit-hash>

# 4. Push and create PR
git push -u origin Skyline/work/YYYYMMDD_feature_release
gh pr create --base Skyline/skyline_XX_X --title "Cherry-pick: <title>" --body "..."
```

### Cherry-Pick PR Format

```markdown
## Summary

Cherry-pick of #<original-PR> to release branch `Skyline/skyline_XX_X`.

<Optional: reason for manual cherry-pick>

**Original changes:**
<Brief summary of what the PR did>
```

### Common Gotchas

1. **Deleting PR branch too early** - Wait for the cherry-pick PR to be created before deleting your branch
2. **Merge commits in history** - Use `git pull --rebase` or `/rebase` comment before squash-and-merge
