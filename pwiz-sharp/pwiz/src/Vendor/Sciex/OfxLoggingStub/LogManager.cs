using System.Globalization;
using OFX.Core.Contracts;

namespace OFX.Logging;

/// <summary>
/// No-op <see cref="ILogManager"/> implementation. OFX.Core.OFXApp's bootstrap does
/// <c>Type.GetType("OFX.Logging.LogManager,OFX.Logging")</c>; if that returns null
/// it falls back to <c>DefaultLogManager</c> (which spams stdout via a private
/// Stream that bypasses <see cref="Console.SetOut(TextWriter)"/>). We provide this
/// stub so the SDK uses a silent log manager instead.
/// </summary>
/// <remarks>
/// Public + parameterless ctor + concrete (non-abstract) so OFX can
/// <c>Activator.CreateInstance(type)</c> us without ceremony. The class name and
/// containing-assembly name must both match what OFX is searching for —
/// <c>OFX.Logging.LogManager</c> in <c>OFX.Logging.dll</c>.
/// </remarks>
public sealed class LogManager : ILogManager
{
    /// <summary>Creates the no-op log manager.</summary>
    public LogManager() { }

    /// <inheritdoc/>
    public ILog GetLogger(string name) => NoopLog.Instance;

    /// <inheritdoc/>
    public ILog GetLogger(Type type) => NoopLog.Instance;

    /// <inheritdoc/>
    public void Configure() { }

    /// <inheritdoc/>
    public void Configure(FileInfo fileInfo) { }

    /// <inheritdoc cref="IConfigurable{T}.Configure(T)" />
    public void Configure(IConfiguration configuration) { }

    /// <inheritdoc/>
    public void Shutdown() { }
}

/// <summary>
/// No-op <see cref="ILog"/>. Every level returns false from its <c>Is*Enabled</c>
/// property so SDK code that pre-checks <c>if (log.IsDebugEnabled)</c> short-circuits
/// and skips the format-string allocation that would have produced output.
/// </summary>
internal sealed class NoopLog : ILog
{
    public static readonly NoopLog Instance = new();

    public bool IsDebugEnabled => false;
    public bool IsInfoEnabled => false;
    public bool IsWarnEnabled => false;
    public bool IsErrorEnabled => false;
    public bool IsFatalEnabled => false;

    public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;

    public void Debug(object message) { }
    public void Debug(object message, Exception exception) { }
    public void DebugFormat(string format, params object[] args) { }
    public void Info(object message) { }
    public void Info(object message, Exception exception) { }
    public void InfoFormat(string format, params object[] args) { }
    public void Warn(object message) { }
    public void Warn(object message, Exception exception) { }
    public void WarnFormat(string format, params object[] args) { }
    public void Error(object message) { }
    public void Error(object message, Exception exception) { }
    public void ErrorFormat(string format, params object[] args) { }
    public void Fatal(object message) { }
    public void Fatal(object message, Exception exception) { }
    public void FatalFormat(string format, params object[] args) { }
    public object GetInnerLogger() => this;
}
