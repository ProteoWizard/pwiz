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
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AuditLogListTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestAuditLogSerialization()
        {
            const int entryCount = 20000;
            var document = new SrmDocument(SrmSettingsList.GetDefault());
            var simpleEntry = AuditLogEntry.CreateSimpleEntry(document, MessageType.test_only, string.Empty);
            var entries = Enumerable.Range(0, entryCount).Select(index => simpleEntry).ToArray();
            Array.Reverse(entries);
            AuditLogEntry headEntry = null;
            foreach (var entry in entries)
            {
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
            var roundTrip = (AuditLogList) serializer.Deserialize(new StringReader(serializedAuditLog.ToString()));
            Assert.IsNotNull(roundTrip);
            Assert.AreEqual(auditLogList.AuditLogEntries.Count, roundTrip.AuditLogEntries.Count);
        }
    }
}
