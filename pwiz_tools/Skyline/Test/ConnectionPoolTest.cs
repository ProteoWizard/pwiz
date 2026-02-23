/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ConnectionPoolTest : AbstractUnitTestEx
    {
        /// <summary>
        /// For visual validation of report output. Not really recorded to the test itself.
        /// </summary>
        protected override bool IsRecordMode => false;  // Set to true to enable console output of reports for visual inspection

        private void Log(string s)
        {
            if (IsRecordMode)
                Console.WriteLine(s);
        }

        [TestMethod]
        public void TestConnectionPoolReportAndTracking()
        {
            // Save and restore TrackHistory to avoid order-dependent test behavior
            using var restoreTracking = new ScopedAction(
                () => ConnectionPool.TrackHistory = false,
                () => ConnectionPool.TrackHistory = false);

            var pool = new ConnectionPool();

            // Verify empty pool reports nothing
            Assert.IsFalse(pool.HasPooledConnections);
            Assert.AreEqual(string.Empty, pool.ReportPooledConnections());

            // Create fake connections
            var id1 = new TestConnectionId(@"C:\TestResults\data.skyd");
            var id2 = new TestConnectionId(@"C:\TestResults\lib.blib");
            pool.GetConnection(id1, () => new MemoryStream());
            pool.GetConnection(id2, () => new MemoryStream());
            Assert.IsTrue(pool.HasPooledConnections);

            // Report should show connection lines with file paths but no event history
            var report = pool.ReportPooledConnections();
            Log(@"--- Report without tracking ---");
            Log(report);
            AssertEx.Contains(report, ConnectionPool.FormatConnectionLine(id1));
            AssertEx.Contains(report, ConnectionPool.FormatConnectionLine(id2));
            var connectEvent = new PoolEvent(PoolEventType.Connect, DateTime.Now, string.Empty);
            Assert.IsFalse(report.Contains(ConnectionPool.FormatEventLine(connectEvent)),
                "Should not contain tracking events when TrackHistory is false");

            // Disconnect both and start fresh
            pool.DisposeAll();
            Assert.IsFalse(pool.HasPooledConnections);

            // Now enable tracking and repeat
            ConnectionPool.TrackHistory = true;
            pool.ClearHistory();

            pool.GetConnection(id1, () => new MemoryStream());
            pool.GetConnection(id2, () => new MemoryStream());

            // Disconnect one connection
            pool.Disconnect(id2);

            // Only id1 should remain
            Assert.IsTrue(pool.IsInPool(id1));
            Assert.IsFalse(pool.IsInPool(id2));

            // Report should show tracking history for the still-open connection
            report = pool.ReportPooledConnections();
            Log(@"--- Report with tracking (id1 open, id2 disconnected) ---");
            Log(report);

            // id1 should have its connection entry and a Connect event
            AssertEx.Contains(report, ConnectionPool.FormatConnectionLine(id1));
            AssertEx.Contains(report, connectEvent.EventType.ToString());

            // id2 should NOT appear (it was disconnected)
            Assert.IsFalse(report.Contains(ConnectionPool.FormatConnectionLine(id2)),
                "Disconnected connection should not appear in report");

            // Reconnect id2 to test multiple events on one connection
            pool.GetConnection(id2, () => new MemoryStream());
            report = pool.ReportPooledConnections();
            Log(@"--- Report after reconnecting id2 ---");
            Log(report);

            // id2's history should show Connect, Disconnect, Connect
            AssertEx.Contains(report, ConnectionPool.FormatConnectionLine(id2));
            var id2Section = GetConnectionSection(report, ConnectionPool.FormatConnectionLine(id2));
            Assert.IsNotNull(id2Section, "Should find section for id2");
            Log(@"--- id2 section ---");
            Log(id2Section);
            // Count event lines by matching the timestamp-prefixed format from PoolEvent.ToString()
            string connectMarker = PoolEventType.Connect.ToString();
            string disconnectMarker = PoolEventType.Disconnect.ToString();
            int connectCount = CountEventLines(id2Section, connectMarker);
            int disconnectCount = CountEventLines(id2Section, disconnectMarker);
            Assert.AreEqual(2, connectCount, "id2 should have 2 Connect events");
            Assert.AreEqual(1, disconnectCount, "id2 should have 1 Disconnect event");

            // Verify stack traces are included and contain this test method name
            AssertEx.Contains(report, nameof(TestConnectionPoolReportAndTracking));

            pool.DisposeAll();
        }

        /// <summary>
        /// Extract the report lines for a specific connection, starting from
        /// the connection header line and including all indented event lines.
        /// </summary>
        private static string GetConnectionSection(string report, string connectionLine)
        {
            var lines = report.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            string section = null;
            bool inSection = false;
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;
                bool isConnectionLine = !line.StartsWith(" ");
                if (isConnectionLine)
                {
                    if (inSection)
                        break;
                    if (line.TrimEnd() == connectionLine)
                        inSection = true;
                }
                if (inSection)
                    section = section == null ? line : section + Environment.NewLine + line;
            }
            return section;
        }

        /// <summary>
        /// Count event header lines matching a specific event type.
        /// Only counts lines that match the PoolEvent.ToString() format: "    [timestamp] EventType"
        /// to avoid false matches from stack trace text.
        /// </summary>
        private static int CountEventLines(string section, string eventType)
        {
            int count = 0;
            foreach (var line in section.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None))
            {
                // Event header lines start with "    [" (indent + timestamp bracket)
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("[") && trimmed.Contains("] " + eventType))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Minimal Identity subclass for testing ConnectionPool directly.
        /// </summary>
        private class TestConnectionId : Identity
        {
            public TestConnectionId(string filePath)
            {
                FilePath = filePath;
            }

            public string FilePath { get; }

            public override string ToString()
            {
                return $@"TestConnection({FilePath})";
            }
        }
    }
}
