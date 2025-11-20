# Project-Wide Utility Scripts

This directory contains PowerShell scripts for maintaining code quality and consistency across the ProteoWizard project, particularly useful for LLM-assisted development workflows.

## Scripts

### Line Ending Management

#### `fix-crlf.ps1`
Converts modified files in git working directory from LF to CRLF (Windows standard).

**When to use:**
- After LLM tools inadvertently change line endings from CRLF to LF
- Before committing, if you notice unwanted line ending changes in `git diff`

**Usage:**
```powershell
.\ai\scripts\fix-crlf.ps1
```

**Behavior:**
- Only processes files shown in `git status` (modified/added)
- Converts all `\n` to `\r\n` (CRLF)
- Reports which files were converted
- Validates that no bare LFs remain

**Example output:**
```
Converted: pwiz_tools/Skyline/SkylineResources.cs
Converted: pwiz_tools/Shared/CommonUtil/SystemUtil/ProcessRunner.cs
...
All converted to CRLF.
```

**Background:**
This script was created during webclient_replacement work (Oct 2025) when LLM tools (which prefer Linux-style LF) inadvertently changed line endings from Windows CRLF to LF-only, causing large Git diffs. The project standard is CRLF on Windows.

---

### UTF-8 BOM Management

The ProteoWizard project follows a strict **UTF-8 without BOM** policy for all source files, except for a small approved list of vendor-generated files.

#### `validate-bom-compliance.ps1`
Validates that no unexpected UTF-8 BOMs exist in the repository.

**When to use:**
- Before committing changes
- As part of CI/CD validation
- When investigating encoding issues

**Usage:**
```powershell
.\ai\scripts\validate-bom-compliance.ps1
```

**Behavior:**
- Scans all git-tracked files for UTF-8 BOM (bytes `EF BB BF`)
- Reports approved BOMs (vendor files, COM type libraries)
- **Fails (exit 1)** if unexpected BOMs are found
- **Succeeds (exit 0)** if all BOMs are on approved list

**Example output:**
```
=== Approved BOMs (11) ===
  [OK] pwiz_tools/Skyline/Executables/BuildMethod/BuildLTQMethod/ltmethod.tli
       Reason: Visual Studio generated COM type library
...

=== VALIDATION PASSED ===
All files with BOM are on the approved list.
```

**What to do if validation fails:**
1. If files should NOT have BOM: use `remove-bom.ps1`
2. If files MUST have BOM (vendor/generated): add to approved list in script

---

#### `analyze-bom-git.ps1`
Analyzes git-tracked files and generates a list of files with UTF-8 BOMs.

**When to use:**
- Initial BOM audit
- Generating input for `remove-bom.ps1`

**Usage:**
```powershell
.\ai\scripts\analyze-bom-git.ps1
```

**Behavior:**
- Scans all git-tracked files
- Writes list to `files-with-bom.txt`
- Reports statistics

**Example output:**
```
Analyzing 15,432 files...
Found 13 files with UTF-8 BOM
Results written to: files-with-bom.txt
```

---

#### `remove-bom.ps1`
Removes UTF-8 BOMs from specified files while preserving timestamps.

**When to use:**
- After `validate-bom-compliance.ps1` reports unexpected BOMs
- When LLM tools add BOMs to source files (Visual Studio Code, Cursor, etc.)

**Usage:**
```powershell
# Dry-run mode (shows what would be changed)
.\ai\scripts\remove-bom.ps1

# Actually remove BOMs
.\ai\scripts\remove-bom.ps1 -Execute

# Process specific files
.\ai\scripts\remove-bom.ps1 -FileList custom-list.txt -Execute
```

**Behavior:**
- Reads file list from `files-with-bom.txt` (or `-FileList` parameter)
- **Dry-run by default** - use `-Execute` to make changes
- Preserves file timestamps (creation, modification, access)
- Validates BOM removal after writing
- Excludes patterns like `*.tli`, `*.tlh` (COM type libraries)

**Example output:**
```
UTF-8 BOM Removal Tool
======================

Processing 2 files...
[1/2] REMOVED: pwiz_tools/Skyline/Skyline.sln.DotSettings
[2/2] REMOVED: pwiz_tools/Skyline/Executables/AutoQC/AutoQC.sln.DotSettings

=== Summary ===
Total files processed: 2
BOMs removed:          2
```

**Common workflow:**
```powershell
# 1. Find files with unexpected BOMs
.\ai\scripts\validate-bom-compliance.ps1
# (fails with list of unexpected BOMs)

# 2. Create file list for removal
echo "pwiz_tools/Skyline/Skyline.sln.DotSettings" > files-with-bom.txt

# 3. Remove BOMs
.\ai\scripts\remove-bom.ps1 -Execute

# 4. Verify compliance
.\ai\scripts\validate-bom-compliance.ps1
# (passes)

# 5. Commit changes
git add .
git commit -m "Remove unexpected UTF-8 BOMs"
```

---

### UTF-8 Output for PowerShell Scripts

LLM-authored PowerShell scripts often emit Unicode status icons (`✅`, `❌`, etc.). To ensure these render correctly even if a developer forgets to switch their terminal to UTF-8, add the following guard near the top of every such script:

```powershell
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
```

This keeps the script self-contained and prevents `âœ…`-style mojibake in Cursor, VS Code, or other shells that default to a legacy code page.

---

## Background & Rationale

### Why CRLF (Windows line endings)?

The ProteoWizard project is primarily developed on Windows and uses Windows-native build tools (MSBuild, Visual Studio). Using consistent CRLF line endings:

1. **Avoids spurious diffs** - When LLM tools convert CRLF→LF, it creates noise in `git diff`
2. **Matches tooling** - Visual Studio, MSBuild, and Windows scripts expect CRLF
3. **Consistency** - Nearly all project files already use CRLF

**Exception:** Shell scripts (`.sh`) and Jamfiles use LF (Unix standard) as specified in `.editorconfig`.

### Why UTF-8 without BOM?

UTF-8 BOMs are unnecessary for UTF-8 (which is byte-order independent) and can cause issues:

1. **Build failures** - Some tools don't handle BOMs correctly
2. **Parser errors** - JSON, XML, and other parsers may reject BOM
3. **Version control noise** - BOMs create invisible diffs
4. **Tooling inconsistency** - Some editors add BOMs, others don't

**Modern best practice:** UTF-8 without BOM is the standard for cross-platform development.

**Approved exceptions:**
- Visual Studio COM type library files (`.tli`, `.tlh`) - auto-generated with BOM
- Agilent vendor data files - represent real instrument output format

---

## Configuration Files

These scripts work in conjunction with:

- **`.editorconfig`** - Enforces `end_of_line = crlf` for most files
- **`.gitattributes`** - Git line ending normalization (if configured)
- **`validate-bom-compliance.ps1`** - Maintains approved BOM list

---

## For LLM-Assisted Development

When working with LLMs (Cursor, GitHub Copilot, etc.):

1. **Before committing:**
   ```powershell
   .\ai\scripts\fix-crlf.ps1              # Fix line endings
   .\ai\scripts\validate-bom-compliance.ps1  # Check BOMs
   ```

2. **If validation fails:**
   ```powershell
   .\ai\scripts\remove-bom.ps1 -Execute
   ```

3. **Verify changes:**
   ```powershell
   git diff --stat  # Should show only meaningful changes
   ```

**Why these issues occur:**
- Many LLM-powered editors (VS Code, Cursor) are built for cross-platform development
- They may default to LF line endings and UTF-8 with BOM
- When LLMs read/modify files, they may inadvertently change encoding

**Prevention:**
- Check Cursor/VS Code settings: `"files.eol": "\r\n"`
- Ensure `.editorconfig` is respected: `"editorconfig.enable": true`
- Run validation scripts before committing

---

## Related Documentation

- **[ai/STYLEGUIDE.md](../STYLEGUIDE.md)** - File headers, encoding guidelines
- **[ai/WORKFLOW.md](../WORKFLOW.md)** - Git workflow, commit practices
- **[ai/docs/build-and-test-guide.md](../docs/build-and-test-guide.md)** - Build/test automation

---

## Troubleshooting

**Q: `fix-crlf.ps1` reports files already have CRLF, but git shows them as modified?**

A: This can happen if:
- Files have mixed line endings (some CRLF, some LF)
- `.gitattributes` is normalizing line endings differently
- Git's `core.autocrlf` setting is interfering
- Cursor has a pending “modified files” list that it keeps reapplying (see note below)

Solution: Run `git diff <file>` to see exact changes.

> **Cursor tip:** Cursor keeps its own “modified files” queue per workspace. If you ignore that queue, the editor may reapply those cached versions—often converting them to LF-only—when you reopen the workspace. Regularly accept/reject the queued files so Cursor doesn’t keep refreshing them with LF endings.

**Q: `validate-bom-compliance.ps1` fails, but I didn't add any BOMs?**

A: Common causes:
- LLM tools modified files and added BOMs
- Copy-pasting code from web browsers or other editors
- `.DotSettings` files saved by ReSharper/JetBrains tools

Solution: Use `remove-bom.ps1 -Execute` to fix.

**Q: Should I add `.gitattributes` rules for line endings?**

A: Not usually. The developer setup guide already instructs everyone to set `git config --global core.autocrlf true`, which keeps Windows checkouts in CRLF automatically. Combined with these scripts, that policy has worked well without additional project-level overrides. If a new third-party file truly needs different handling, document it in `.editorconfig` or the relevant script instead of broad `.gitattributes` changes.