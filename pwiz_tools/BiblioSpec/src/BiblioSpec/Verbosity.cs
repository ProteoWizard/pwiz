// Port of pwiz_tools/BiblioSpec/src/Verbosity.{h,cpp}

using System.Diagnostics;
using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Verbosity / log levels, ordered from least-verbose to most-verbose. A message at level X
/// is emitted when <see cref="Verbosity.GlobalLevel"/> &gt;= X.
/// </summary>
/// <remarks>Port of <c>BiblioSpec::V_LEVEL</c>. Numeric values match the cpp enum.</remarks>
public enum VerbosityLevel
{
    /// <summary>No output (cpp: V_SILENT).</summary>
    Silent = 0,
    /// <summary>Only fatal errors (cpp: V_ERROR).</summary>
    Error = 1,
    /// <summary>Current step of execution (cpp: V_STATUS).</summary>
    Status = 2,
    /// <summary>Non-fatal warnings (cpp: V_WARN).</summary>
    Warn = 3,
    /// <summary>Extra info for debugging (cpp: V_DEBUG).</summary>
    Debug = 4,
    /// <summary>More info (cpp: V_DETAIL).</summary>
    Detail = 5,
    /// <summary>Way more output than you should ever need (cpp: V_ALL).</summary>
    All = 6,
}

/// <summary>
/// Static logging utility ported from <c>BiblioSpec::Verbosity</c>. Messages are emitted
/// to <see cref="Console.Error"/> when their level is &lt;= the current <see cref="GlobalLevel"/>.
/// <see cref="Error(string)"/> additionally throws <see cref="BlibException"/> after writing
/// (the cpp version calls <c>exit(1)</c>; throwing is the BCL-friendly equivalent and lets
/// callers in tests catch the failure).
/// </summary>
/// <remarks>
/// cpp Verbosity.cpp uses varargs printf style; in C# callers pass pre-formatted strings
/// (use string interpolation or <see cref="string.Format(System.IFormatProvider, string, object?[])"/>).
/// The cpp default is <c>V_WARN</c>; we keep the same default here.
/// </remarks>
public static class Verbosity
{
    private static readonly object _logLock = new();
    private static StreamWriter? _logFile;
    private static readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>Current global verbosity level. Defaults to <see cref="VerbosityLevel.Warn"/>
    /// (matching cpp <c>V_WARN</c>).</summary>
    public static VerbosityLevel GlobalLevel { get; set; } = VerbosityLevel.Warn;

    /// <summary>When true, every emitted line is prefixed with an HH:MM:SS.fff elapsed timestamp.</summary>
    public static bool TimestampEnabled { get; set; }

    /// <summary>
    /// Translate the cpp-style verbosity strings ("silent","error","warn","status","debug",
    /// "detail","all") to a <see cref="VerbosityLevel"/>. Throws <see cref="BlibException"/>
    /// on unknown input (cpp throws a bare <c>std::string</c>; we use the typed exception).
    /// </summary>
    public static VerbosityLevel StringToLevel(string levelStr)
    {
        ArgumentNullException.ThrowIfNull(levelStr);
        return levelStr switch
        {
            "silent" => VerbosityLevel.Silent,
            "error" => VerbosityLevel.Error,
            "warn" => VerbosityLevel.Warn,
            "status" => VerbosityLevel.Status,
            "debug" => VerbosityLevel.Debug,
            "detail" => VerbosityLevel.Detail,
            "all" => VerbosityLevel.All,
            _ => throw new BlibException(false, $"The verbosity level {levelStr} is not valid"),
        };
    }

    /// <summary>Open <c>blib-build.log</c> in the current directory and tee all output to it.</summary>
    public static void OpenLogfile()
    {
        lock (_logLock)
        {
            try
            {
                _logFile = new StreamWriter("blib-build.log", append: false) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                throw new BlibException(false, $"Cannot open log file 'blib-build.log': {ex.Message}");
            }
        }
    }

    /// <summary>Close the log file opened by <see cref="OpenLogfile"/>, if any.</summary>
    public static void CloseLogfile()
    {
        lock (_logLock)
        {
            _logFile?.Dispose();
            _logFile = null;
        }
    }

    /// <summary>
    /// Emit a message at <see cref="VerbosityLevel.Error"/> and throw <see cref="BlibException"/>.
    /// </summary>
    /// <remarks>cpp Verbosity.cpp:175 calls <c>exit(1)</c>; throwing keeps the BCL stack
    /// intact so tests / callers can recover.</remarks>
    public static void Error(string message) => Comment(VerbosityLevel.Error, message);

    /// <summary>Emit a message at <see cref="VerbosityLevel.Warn"/>.</summary>
    public static void Warn(string message) => Comment(VerbosityLevel.Warn, message);

    /// <summary>Emit a message at <see cref="VerbosityLevel.Status"/>.</summary>
    public static void Status(string message) => Comment(VerbosityLevel.Status, message);

    /// <summary>Emit a message at <see cref="VerbosityLevel.Debug"/>.</summary>
    public static void Debug(string message) => Comment(VerbosityLevel.Debug, message);

    /// <summary>
    /// Split a (possibly multi-line) message and write each line to stderr prefixed with
    /// <c>"ERROR: "</c>. Does NOT route through <see cref="Comment"/> / <see cref="Log"/> —
    /// the per-line-prefix shape only makes sense for the per-file build-errors collected
    /// in <see cref="BlibBuilder.BuildLibrary"/>, and routing through Verbosity would
    /// either throw (Error level) or prefix only the first physical line (Warn level —
    /// the original bug that broke Skyline's regex parser in
    /// BiblioSpecLiteBuilder.IsLibraryMissingExternalSpectraError because the captured
    /// stderr lost the "Run with the -E flag" anchor on the unprefixed continuation lines).
    /// </summary>
    /// <remarks>cpp parity: BlibBuild.cpp:78 <c>WriteErrorLines</c>.</remarks>
    public static void WriteErrorLines(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        using var reader = new System.IO.StringReader(message);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            Console.Error.WriteLine("ERROR: " + line);
        }
    }

    /// <summary>
    /// Emit a message at the given level. Suppressed when <see cref="GlobalLevel"/> &lt; level.
    /// </summary>
    public static void Comment(VerbosityLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (GlobalLevel < level)
        {
            // Fatal level still throws even when suppressed, matching cpp:
            // V_ERROR always calls exit(1) regardless of Global_Verbosity.
            if (level == VerbosityLevel.Error)
            {
                // AlreadyLogged=false here because we did NOT write to stderr (suppressed).
                throw new BlibException(false, message);
            }
            return;
        }

        Log(level, message);
    }

    private static void Log(VerbosityLevel level, string message)
    {
        var prefix = string.Empty;
        if (TimestampEnabled)
        {
            var elapsed = _clock.Elapsed;
            prefix = string.Format(
                CultureInfo.InvariantCulture,
                "[{0:D2}:{1:D2}:{2:D2}.{3:D3}] ",
                (int)elapsed.TotalHours,
                elapsed.Minutes,
                elapsed.Seconds,
                elapsed.Milliseconds);
        }

        var levelPrefix = level switch
        {
            VerbosityLevel.Error => "ERROR: ",
            VerbosityLevel.Warn => "WARNING: ",
            VerbosityLevel.Debug or VerbosityLevel.Detail or VerbosityLevel.All => "DEBUG: ",
            _ => string.Empty,
        };

        var line = prefix + levelPrefix + message;

        lock (_logLock)
        {
            if (_logFile is null)
            {
                Console.Error.WriteLine(line);
            }
            else
            {
                _logFile.WriteLine(line);
                if (level <= VerbosityLevel.Status)
                {
                    Console.Error.WriteLine(line);
                }
            }
        }

        if (level == VerbosityLevel.Error)
        {
            lock (_logLock)
            {
                _logFile?.Dispose();
                _logFile = null;
            }
            // AlreadyLogged=true because Log() just wrote the formatted line to stderr.
            // CLI top-level handler can use this to avoid double-printing.
            throw new BlibException(false, message) { AlreadyLogged = true };
        }
    }
}
