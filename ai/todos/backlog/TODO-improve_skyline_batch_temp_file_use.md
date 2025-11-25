# TODO-improve_skyline_batch_temp_file_use.md

## Objective
Improve SkylineBatch production code to avoid OS temp folder usage, following Skyline patterns of using FileSaver or placing files in user-visible locations (analysis folders).

## Background

During test infrastructure cleanup (`TODO-skylinebatch_test_cleanup.md`), an audit revealed production code using `Path.GetTempPath()` and `Path.GetTempFileName()` in ways that could be improved:

### Why Avoid OS Temp Folder

**Skyline philosophy:**
- Prefer placing temporary files near where they're used (user-visible locations)
- Use `FileSaver` for transactional file operations (auto-cleanup on exception)
- OS temp accumulates garbage, harder for users to discover/debug
- Users can't easily find or inspect intermediate files when troubleshooting

**FileSaver pattern:**
```csharp
using (var saver = new FileSaver(targetPath))
{
    // Write to saver.SafeName (temp location)
    File.WriteAllText(saver.SafeName, content);
    
    saver.Commit();  // Atomically moves to targetPath
}
// If exception occurs, temp file auto-deleted
```

## Issues Found

### Issue 1: CommandWriter Creates Batch File in OS Temp

**File:** `SkylineBatch/CommandWriter.cs`

**Current behavior:**
```csharp
public CommandWriter(Logger logger, bool multiLine, bool invariantReport)
{
    _commandFile = Path.GetTempFileName();  // OS temp!
    _writer = new StreamWriter(_commandFile);
    // ...
}

public string GetCommandFile()
{
    _writer.Close();
    return _commandFile;  // Returns temp path
}
```

**Usage:** `ConfigRunner.Run()` calls `GetCommandFile()` and passes to SkylineCmd with `--batch-commands`

**Problems:**
- Command file left in OS temp (may accumulate over time)
- Not visible to users debugging SkylineBatch runs
- Comment says "Consider: deleting tmp command file" but doesn't
- No transactional safety if write fails

**Recommendation:**

**Option A: Use Analysis Folder (Preferred)**
```csharp
public CommandWriter(string analysisFolderPath, Logger logger, bool multiLine, bool invariantReport)
{
    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    _commandFile = Path.Combine(analysisFolderPath, $".skybatch-commands-{timestamp}.txt");
    _writer = new StreamWriter(_commandFile);
    // ...
}
```

Then in `ConfigRunner.Run()`:
```csharp
var commandWriter = new CommandWriter(Config.MainSettings.AnalysisFolderPath, _logger, multiLine, invariantReport);
// ... after run completes ...
if (File.Exists(_batchFile))
    File.Delete(_batchFile);  // Clean up
```

**Benefits:**
- User can inspect batch commands in analysis folder
- Easier debugging ("what commands did SkylineBatch send to Skyline?")
- No OS temp accumulation
- Predictable location

**Option B: Use FileSaver (Most Robust)**
```csharp
public class CommandWriter : IDisposable
{
    private readonly FileSaver _fileSaver;
    private readonly StreamWriter _writer;
    private readonly string _targetPath;
    
    public CommandWriter(string analysisFolderPath, Logger logger, bool multiLine, bool invariantReport)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _targetPath = Path.Combine(analysisFolderPath, $".skybatch-commands-{timestamp}.txt");
        _fileSaver = new FileSaver(_targetPath);
        _writer = new StreamWriter(_fileSaver.SafeName);
        // ...
    }
    
    public string GetCommandFile()
    {
        _writer.Close();
        _fileSaver.Commit();
        return _targetPath;
    }
    
    public void Dispose()
    {
        _writer?.Dispose();
        _fileSaver?.Dispose();  // Auto-cleanup on exception
    }
}
```

**Benefits:**
- Transactional semantics (auto-cleanup on exception)
- User-visible location
- Follows Skyline FileSaver pattern
- Safe against partial writes

**Estimated effort:** 1-2 hours

### Issue 2: PanoramaServerConnector Downloads .skyp to Temp File

**File:** `SkylineBatch/PanoramaServerConnector.cs` (lines ~111-147)

**Current behavior:**
```csharp
var downloadSkypUri = PanoramaUtil.CallNewInterface(..., "downloadDocument", ...);
var tmpFile = Path.GetTempFileName();

using (var httpClient = new HttpClientWithProgress())
{
    // ... auth ...
    httpClient.DownloadFile(downloadSkypUri, tmpFile);
}

using (var fileStream = new FileStream(tmpFile, FileMode.Open, FileAccess.Read))
using (var streamReader = new StreamReader(fileStream))
{
    while (!streamReader.EndOfStream)
    {
        var line = streamReader.ReadLine();
        if (!string.IsNullOrEmpty(line))
        {
            webdavUri = new Uri(line);
            break;  // Parse first non-empty line
        }
    }
}
File.Delete(tmpFile);
```

**Problems:**
- Downloads entire .skyp file just to read first line
- Creates temp file unnecessarily
- File I/O overhead for simple string parsing
- Temp file may be left behind if exception occurs before `File.Delete()`

**Recommendation:**

**Option A: Use DownloadString (Simplest)**
```csharp
var downloadSkypUri = PanoramaUtil.CallNewInterface(..., "downloadDocument", ...);

using (var httpClient = new HttpClientWithProgress())
{
    if (!string.IsNullOrEmpty(server.FileSource.Username) && !string.IsNullOrEmpty(server.FileSource.Password))
    {
        httpClient.AddAuthorizationHeader(Server.GetBasicAuthHeader(server.FileSource.Username, server.FileSource.Password));
    }
    
    var skypContent = httpClient.DownloadString(downloadSkypUri);
    
    // Parse first non-empty line
    using (var reader = new StringReader(skypContent))
    {
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrEmpty(line))
            {
                webdavUri = new Uri(line);
                break;
            }
        }
    }
}
```

**Benefits:**
- No temp file needed
- Simpler code (no file I/O)
- Faster (in-memory string parsing)
- No cleanup required

**Option B: Use FileSaver if File is Needed**
If you must save the .skyp file (e.g., for progress reporting on large files):
```csharp
var skypPath = Path.Combine(Config.MainSettings.AnalysisFolderPath, ".skyp-temp");
using (var saver = new FileSaver(skypPath))
{
    using (var httpClient = new HttpClientWithProgress())
    {
        // ... auth ...
        httpClient.DownloadFile(downloadSkypUri, saver.SafeName);
    }
    
    // Parse from saver.SafeName
    // ... parsing logic ...
    
    // Don't commit - let it auto-delete
}
```

**Estimated effort:** 30 minutes - 1 hour

### Issue 3: TemporaryDirectory Helper (Low Priority)

**File:** `SharedBatch/FileUtil.cs`

**Current behavior:**
```csharp
public class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory(string dirPath = null, string tempPrefix = TEMP_PREFIX)
    {
        if (string.IsNullOrEmpty(dirPath))
            DirPath = Path.Combine(Path.GetTempPath(), tempPrefix + Path.GetRandomFileName());
        else
            DirPath = dirPath;
        Directory.CreateDirectory(DirPath);
    }
    
    public void Dispose()
    {
        Directory.Delete(DirPath);  // Non-recursive!
    }
}
```

**Notes:**
- Not currently used in SkylineBatch code (only defined)
- Generic helper for scratch directories
- Dispose is non-recursive (will fail if directory has contents)

**Recommendation:**

**If used in future:**
1. Add analysis-folder-rooted overload for user operations
2. Make disposal recursive if content is expected:
   ```csharp
   public void Dispose()
   {
       if (Directory.Exists(DirPath))
           Directory.Delete(DirPath, recursive: true);
   }
   ```
3. Document expected usage and cleanup guarantees

**Priority:** Low (not actively used)

## Implementation Plan

### Phase 1: CommandWriter (High Priority)

1. **Update CommandWriter constructor** to accept `analysisFolderPath` parameter
2. **Change temp file location** to analysis folder with timestamp
3. **Update ConfigRunner.Run()** to pass analysis folder path
4. **Add cleanup** after Skyline run completes
5. **Test** that command files appear in analysis folder and are cleaned up

**Files to modify:**
- `SkylineBatch/CommandWriter.cs`
- `SkylineBatch/ConfigRunner.cs`

**Testing:**
- Run a batch configuration
- Verify `.skybatch-commands-*.txt` appears in analysis folder during run
- Verify file is deleted after run completes
- Verify no files accumulate in OS temp

### Phase 2: PanoramaServerConnector (Medium Priority)

1. **Replace DownloadFile + file I/O** with `DownloadString()`
2. **Update parsing logic** to work with in-memory string
3. **Remove temp file creation/deletion**
4. **Test** Panorama .skyp download and parsing

**Files to modify:**
- `SkylineBatch/PanoramaServerConnector.cs`

**Testing:**
- Test with real Panorama server (or mock)
- Verify .skyp content is parsed correctly
- Verify no temp files created

### Phase 3: Document Pattern (Optional)

Add to `ai/STYLEGUIDE.md` or `ai/docs/testing-patterns.md`:

**Best practices for temporary files:**
1. **Prefer user-visible locations** (analysis folders, document folders)
2. **Use FileSaver** for transactional file operations
3. **Avoid OS temp** unless truly temporary scratch space
4. **Always clean up** temporary files after use
5. **In tests**, use `TestContext.GetTestResultsPath()` (never OS temp)

## Success Criteria

### Must Have
- ✅ CommandWriter creates batch file in analysis folder (not OS temp)
- ✅ Batch command files cleaned up after run
- ✅ PanoramaServerConnector uses DownloadString (no temp file)
- ✅ All tests pass
- ✅ No new temp files accumulate in OS temp during normal operations

### Should Have
- ✅ Command files visible to users in analysis folder (for debugging)
- ✅ FileSaver pattern used for transactional safety
- ✅ Pattern documented in style guide

### Nice to Have
- ✅ TemporaryDirectory helper made more robust (recursive delete)
- ✅ Analysis-folder-rooted overload for TemporaryDirectory

## Related Work

**Completes:** `TODO-skylinebatch_test_cleanup.md` focused on test infrastructure
**Follows:** Skyline's FileSaver pattern (see `pwiz_tools/Skyline/Util/UtilIO.cs`)
**References:** SharedBatch FileSaver implementation (`SharedBatch/FileSaver.cs`)

## Priority

**Medium** - Not urgent, but improves code quality and user experience.

**Rationale:**
- Not blocking other work
- Improves debuggability for users
- Reduces OS temp pollution
- Follows established Skyline patterns
- Small, focused changes (2-3 hours total)

**Timeline:**
- Can be done in any sprint after test cleanup TODO is complete
- Good candidate for "polish" or "tech debt" sprint
- Could be combined with other file handling improvements
