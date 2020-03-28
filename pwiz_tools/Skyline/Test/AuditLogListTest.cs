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
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AuditLogListTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void TestAuditLogSerialization()
        {
            var datetime = AuditLogEntry.ParseSerializedTimeStamp("2019-01-01T05:02:03+04", out var tzoffset); // Accept ISO format, which may omit minutes in offset
            Assume.AreEqual(DateTimeKind.Utc, datetime.Kind);
            Assume.AreEqual(1, datetime.Hour); // Local time was 5AM, 4 hour offset to GMT.
            Assume.AreEqual(new TimeSpan(4, 0, 0), tzoffset);
            var xsd = AuditLogEntry.FormatSerializationString(datetime, tzoffset);
            Assume.AreEqual("2019-01-01T05:02:03+04:00", xsd);
            
            var zulu = AuditLogEntry.ParseSerializedTimeStamp("2019-01-01T01:02:03Z", out tzoffset);
            Assume.AreEqual(tzoffset, TimeSpan.Zero);
            Assume.AreEqual(datetime, zulu);

            AuditLogEntry.ParseSerializedTimeStamp("2019-01-01T01:02:03-04:30", out tzoffset);
            Assume.AreEqual(new TimeSpan(-4, -30, 0), tzoffset);

            datetime = AuditLogEntry.ParseSerializedTimeStamp("2018-12-31T23:02:03-04", out tzoffset);
            Assume.AreEqual(datetime, AuditLogEntry.ParseSerializedTimeStamp("2019-01-01T03:02:03Z", out tzoffset));

            // Test backward compatibility - this file with 4.2 log should load without any problems
            Assume.IsTrue(AuditLogList.ReadFromXmlTextReader(new XmlTextReader(new StringReader(Test42FormatSkyl)), out var loggedSkylineDocumentHash, out var old));
            Assume.AreEqual("tgnQ8fDiKLMIS236kpdJIXNR+fw=", old.RootHash.ActualHash.HashString);
            Assume.AreEqual("AjigWTmQeAO94/jAlwubVMp4FRg=", loggedSkylineDocumentHash); // Note that this is a base64 representation of the 4.2 hex representation "<document_hash>0238A05939907803BDE3F8C0970B9B54CA781518</document_hash>

            var then = DateTime.Parse("2019-03-08 00:02:03Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime(); // Just before DST
            const int entryCount = 20000; // Enough to ensure stack overflow in case of some design error per Nick
            var timestep = new TimeSpan(1,0,1); // 20000 hours should be sufficient to take us into and out of daylight savings twice
            AuditLogEntry headEntry = null;
            for (var index = 0; index++ < entryCount;)
            {
                var documentType = (SrmDocument.DOCUMENT_TYPE)(index % ((int)SrmDocument.DOCUMENT_TYPE.none + 1));
                var entry = AuditLogEntry.CreateTestOnlyEntry(then, documentType, string.Empty);
                then += timestep;
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
            var entries = auditLogList.AuditLogEntries.Enumerate().ToArray();
            var roundtripEntries = roundTrip.AuditLogEntries.Enumerate().ToArray();
            for (var i = 0; i < auditLogList.AuditLogEntries.Count; i++)
            {
                Assert.AreEqual(entries[i].TimeStampUTC, roundtripEntries[i].TimeStampUTC);
                Assert.AreEqual(entries[i].TimeZoneOffset, roundtripEntries[i].TimeZoneOffset);
                Assert.AreEqual(entries[i].SkylineVersion, roundtripEntries[i].SkylineVersion);
                Assert.AreEqual(entries[i].User, roundtripEntries[i].User);
                Assert.AreEqual(entries[i].DocumentType == SrmDocument.DOCUMENT_TYPE.proteomic ? SrmDocument.DOCUMENT_TYPE.none : entries[i].DocumentType, roundtripEntries[i].DocumentType);
                Assert.AreEqual(entries[i].Hash.ActualHash, roundtripEntries[i].Hash.ActualHash);
                // No Skyl hash until sserialized, so can't compare here
            }
            Thread.CurrentThread.CurrentCulture = currentCulture;

            // Make sure current system timezone isn't messing with us
            Assert.AreEqual(14, roundtripEntries[100].TimeStampUTC.Day);
            Assert.AreEqual(8,  roundtripEntries[100].TimeStampUTC.Hour);
            Assert.AreEqual(27, roundtripEntries[10000].TimeStampUTC.Day);
            Assert.AreEqual(17, roundtripEntries[10000].TimeStampUTC.Hour);
        }

        private static string Test42FormatSkyl =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<audit_log_root>\n" +
         "<document_hash>0238A05939907803BDE3F8C0970B9B54CA781518</document_hash>\n" + // Note that this is not how we serialize hashes any more, we use base64 instead of byte.ToString("@"X2")
         "<audit_log>\n" +
          "<audit_log_entry format_version=\"4.21\" time_stamp=\"03/07/2019 19:09:05\" user=\"bspratt-UW2\\bspratt\">\n" +
           "<extra_info>foo</extra_info>\n" +
           "<message>\n" +
            "<type>added_new_peptide_group</type>\n" +
            "<name>foo</name>\n" +
           "</message>\n" +
           "<message>\n" +
            "<type>added_new_peptide_group</type>\n" +
            "<name>foo</name>\n" +
           "</message>\n" +
           "<message>\n" +
            "<type>added_to</type>\n" +
            "<name>{0:Targets}</name>\n" +
            "<name>foo</name>\n" +
           "</message>\n" +
           "<message>\n" +
            "<type>is_</type>\n" +
            "<name>{0:Targets}{2:PropertySeparator}foo{2:PropertySeparator}{0:ProteinMetadata_Name}</name>\n" +
            "<name>\"foo\"</name>\n" +
           "</message>\n" +
          "</audit_log_entry>\n" +
          "<audit_log_entry format_version=\"4.21\" time_stamp=\"03/07/2019 19:08:27\" user=\"bspratt-UW2\\bspratt\">\n" +
           "<message>\n" +
            "<type>start_log_existing_doc</type>\n" +
           "</message>\n" +
           "<message>\n" +
            "<type>start_log_existing_doc</type>\n" +
           "</message>\n" +
          "</audit_log_entry>\n" +
         "</audit_log>\n" +
        "</audit_log_root>";

        [TestMethod]
        public void TestAuditLogRef()
        {
            const string TEST_PREFIX = "test";
            const int TEST_ENTRY_COUNT = 3;
            var document = new SrmDocument(SrmSettingsList.GetDefault());
            Assert.IsTrue(document.AuditLog.AuditLogEntries.IsRoot);
            Assert.AreEqual(0, AuditLogEntryRef.PROTOTYPE.ListChildrenOfParent(document).Count());
            AuditLogEntry headEntry = document.AuditLog.AuditLogEntries;
            for (int i = 0; i < TEST_ENTRY_COUNT; i++)
            {
                var auditLogEntry = AuditLogEntry.CreateTestOnlyEntry(DateTime.UtcNow, SrmDocument.DOCUMENT_TYPE.mixed, TEST_PREFIX + i);
                headEntry = auditLogEntry.ChangeParent(headEntry);
            }
            Assert.IsNotNull(headEntry);
            Assert.AreEqual(TEST_ENTRY_COUNT, headEntry.Count);
            document = document.ChangeAuditLog(headEntry);
            var auditLogRefs = AuditLogEntryRef.PROTOTYPE.ListChildrenOfParent(document).Cast<AuditLogEntryRef>().ToList();
            Assert.AreEqual(TEST_ENTRY_COUNT, auditLogRefs.Count);
            var extraLogEntry = AuditLogEntry
                .CreateTestOnlyEntry(DateTime.UtcNow, SrmDocument.DOCUMENT_TYPE.mixed, "extra entry")
                .ChangeParent(document.AuditLog.AuditLogEntries);
            var document2 = document.ChangeAuditLog(extraLogEntry);
            Assert.AreEqual(TEST_ENTRY_COUNT + 1, AuditLogEntryRef.PROTOTYPE.ListChildrenOfParent(document2).Count());
            foreach (var auditLogRef in auditLogRefs)
            {
                var entry1 = auditLogRef.FindAuditLogEntry(document);
                var entry2 = auditLogRef.FindAuditLogEntry(document2);
                Assert.AreEqual(entry1, entry2);
            }
        }
    }
}
