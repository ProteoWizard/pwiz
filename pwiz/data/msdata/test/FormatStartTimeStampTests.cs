using Pwiz.Data.MsData.Readers;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Guards the host time zone adjustment in <see cref="ReaderConfig.FormatStartTimeStamp(DateTime, bool)"/>.
/// The offset must be the one on the acquisition date, so the same file read in the same time zone gives the
/// same <c>startTimeStamp</c> whether it is read in summer or in winter.
/// </summary>
[TestClass]
public class FormatStartTimeStampTests
{
    [TestMethod]
    public void FormatStartTimeStamp_UsesOffsetOnAcquisitionDate()
    {
        // UTC-8, with DST from 2 AM on the second Sunday of March to 2 AM on the first Sunday of November.
        // Built here so the result does not depend on the time zone data of the computer running the test.
        var pacific = TimeZoneInfo.CreateCustomTimeZone("Test Pacific", TimeSpan.FromHours(-8),
            "Test Pacific", "Test Pacific Standard", "Test Pacific Daylight",
            new[]
            {
                TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date,
                    TimeSpan.FromHours(1),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2,
                        DayOfWeek.Sunday),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1,
                        DayOfWeek.Sunday))
            });

        var importedInSummer = new DateTime(2025, 5, 21, 12, 0, 0);
        var importedInWinter = new DateTime(2026, 3, 7, 12, 0, 0);

        // The Thermo SDK returns the instrument's local clock reading labeled Kind=Utc.
        var acquiredInSummer = new DateTime(2025, 5, 19, 15, 47, 11, DateTimeKind.Utc);
        AssertStartTimeStamp("2025-05-19T22:47:11Z", acquiredInSummer, pacific, importedInSummer);
        AssertStartTimeStamp("2025-05-19T22:47:11Z", acquiredInSummer, pacific, importedInWinter);

        var acquiredInWinter = new DateTime(2025, 1, 15, 15, 47, 11, DateTimeKind.Utc);
        AssertStartTimeStamp("2025-01-15T23:47:11Z", acquiredInWinter, pacific, importedInSummer);
        AssertStartTimeStamp("2025-01-15T23:47:11Z", acquiredInWinter, pacific, importedInWinter);

        // With the adjustment off the stored value is written unchanged.
        Assert.AreEqual("2025-05-19T15:47:11Z",
            ReaderConfig.FormatStartTimeStamp(acquiredInSummer, false, pacific, importedInWinter));
    }

    private static void AssertStartTimeStamp(string expected, DateTime acquired, TimeZoneInfo hostZone,
        DateTime hostNow)
    {
        Assert.AreEqual(expected, ReaderConfig.FormatStartTimeStamp(acquired, true, hostZone, hostNow),
            $"startTimeStamp for a file acquired {acquired:yyyy-MM-dd HH:mm:ss} and read on {hostNow:yyyy-MM-dd} " +
            "must use the UTC offset on the acquisition date");
    }
}
