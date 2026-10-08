using Pwiz.Data.MsData.Readers;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Pins the <see cref="ReaderConfig"/> defaults that library consumers inherit.
///
/// These matter because only msconvert-sharp overrides them; Skyline and the hosted C API take
/// whatever the constructor sets. cpp draws the same line - <c>Reader::Config</c> is permissive
/// (<c>Reader.cpp:47</c>) and msconvert tightens it by negating its own
/// <c>--ignoreUnknownInstrumentError</c> flag (<c>msconvert.cpp:507-508</c>).
/// </summary>
[TestClass]
public class ReaderConfigDefaultsTests
{
    [TestMethod]
    public void Defaults_MatchTheCppLibraryDefaults()
    {
        var config = new ReaderConfig();

        // Defaulting this to true would hard-fail every non-msconvert caller on a vendor file
        // whose instrument is not in the lookup tables - files cpp and net472 open with a warning.
        Assert.IsFalse(config.UnknownInstrumentIsError);
        Assert.IsTrue(config.UnknownFormatIsError);

        // Permissive: the message goes to stderr and the read continues.
        config.InstrumentMetadataError("unresolved instrument");

        // Strict, as msconvert-sharp sets it: the same call throws, and says how to turn it off.
        config.UnknownInstrumentIsError = true;
        var ex = Assert.ThrowsException<IOException>(
            () => config.InstrumentMetadataError("unresolved instrument"));
        StringAssert.Contains(ex.Message, "ignoreUnknownInstrumentError");
    }
}
