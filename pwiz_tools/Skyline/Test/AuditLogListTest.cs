/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AuditLogListTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestAuditLogSerialization()
        {
            var datetime = AuditLogEntry.ParseSerializedTimeStamp("2019-01-01T05:02:03+04", out var tzoffset);
            Assume.AreEqual(new TimeSpan(4, 0, 0), tzoffset);
            var zulu = AuditLogEntry.ParseSerializedTimeStamp("2019-01-01T01:02:03Z", out tzoffset);
            Assume.AreEqual(tzoffset, TimeSpan.Zero);
            Assume.AreEqual(datetime, zulu);

            AuditLogEntry.ParseSerializedTimeStamp("2019-01-01T01:02:03-04:30", out tzoffset);
            Assume.AreEqual(new TimeSpan(-4, -30, 0), tzoffset);

            datetime = AuditLogEntry.ParseSerializedTimeStamp("2018-12-31T23:02:03-04", out tzoffset);
            Assume.AreEqual(datetime, AuditLogEntry.ParseSerializedTimeStamp("2019-01-01T03:02:03Z", out tzoffset));

            var now = DateTime.SpecifyKind(DateTime.Parse("2019-03-09 00:02:03", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal), DateTimeKind.Utc); // Day before DST
            const int entryCount = 20000;
            var timestep = new TimeSpan(1,0,1); // 20000 hours should be sufficient to take us into and out of daylight savings twice
            AuditLogEntry headEntry = null;
            for (var index = 0; index++ < entryCount;)
            {
                var entry = AuditLogEntry.CreateTestOnlyEntry(now, string.Empty);
                now += timestep;
                if (headEntry == null)
                {
                    headEntry = entry;
                }
                else
                {
                    headEntry = entry.ChangeParent(headEntry);
                }
            }
            Assert.IsNotNull(headEntry);
            Assert.AreEqual(entryCount, headEntry.Count);
            Assert.AreEqual(headEntry.Count, headEntry.Enumerate().Count());
            var auditLogList = new AuditLogList(headEntry);
            var serializedAuditLog = new StringWriter();
            var serializer = new XmlSerializer(typeof(AuditLogList));
            serializer.Serialize(serializedAuditLog, auditLogList);
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; // Logs are meant to be culture invariant
            var roundTrip = (AuditLogList) serializer.Deserialize(new StringReader(serializedAuditLog.ToString()));
            Assert.IsNotNull(roundTrip);
            Assert.AreEqual(auditLogList.AuditLogEntries.Count, roundTrip.AuditLogEntries.Count);
            Thread.CurrentThread.CurrentCulture = currentCulture;
        }
    }
}
