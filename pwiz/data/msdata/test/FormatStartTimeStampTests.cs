using Pwiz.Data.MsData.Readers;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Guards the host time zone adjustment in <see cref="ReaderConfig.FormatStartTimeStamp(DateTime, bool)"/>.
/// The offset must be the one on the acquisition date, not on the date the file is read, so a summer and a winter
/// acquisition in the same time zone get different offsets.
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

        // The Thermo SDK returns the instrument's local clock reading labeled Kind=Utc.
        var acquiredInSummer = new DateTime(2025, 5, 19, 15, 47, 11, DateTimeKind.Utc);
        AssertStartTimeStamp("2025-05-19T22:47:11Z", acquiredInSummer, pacific);

        var acquiredInWinter = new DateTime(2025, 1, 15, 15, 47, 11, DateTimeKind.Utc);
        AssertStartTimeStamp("2025-01-15T23:47:11Z", acquiredInWinter, pacific);

        // 3 hours after DST started on 2025-03-09. As a Pacific clock time this is PDT (UTC-7). Read as a UTC time,
        // as its Kind=Utc label says, it would be 21:00 PST the night before (UTC-8) and come out an hour late.
        var acquiredAfterSpringChange = new DateTime(2025, 3, 9, 5, 0, 0, DateTimeKind.Utc);
        AssertStartTimeStamp("2025-03-09T12:00:00Z", acquiredAfterSpringChange, pacific);

        Assert.AreEqual("2025-05-19T15:47:11Z",
            ReaderConfig.FormatStartTimeStamp(acquiredInSummer, false, pacific),
            "startTimeStamp with the adjustment off must be the stored value");
    }

    private static void AssertStartTimeStamp(string expected, DateTime acquired, TimeZoneInfo hostZone)
    {
        Assert.AreEqual(expected, ReaderConfig.FormatStartTimeStamp(acquired, true, hostZone),
            $"startTimeStamp for a file acquired {acquired:yyyy-MM-dd HH:mm:ss} " +
            "must use the UTC offset on the acquisition date");
    }
}
