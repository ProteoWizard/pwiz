/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Test
{
    /// <summary>
    /// Summary description for SerializationTest
    /// </summary>
    [TestClass]
    public class SerializationTest
    {
        [TestMethod]
        public void TestDbMsDataFile()
        {
            var dbMsDataFile = new DbMsDataFile
                                   {
                                       Name = "Foo",
                                       MsLevels = new byte[] {0x1, 0x2, 0x1, 0x2},
                                       Times = new [] {0.1, 0.2, 0.3, 0.4}
                                   };
            var serializer = new DataContractJsonSerializer(typeof (DbMsDataFile));
            var stream = new MemoryStream();
            serializer.WriteObject(stream, dbMsDataFile);
            var streamAsString = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int) stream.Length);
            Trace.TraceInformation("Serialed as {0}", streamAsString);
            stream.Seek(0, SeekOrigin.Begin);
            var dbMsDataFile2 = (DbMsDataFile) serializer.ReadObject(stream);
            Assert.AreEqual(dbMsDataFile.Name, dbMsDataFile2.Name);
            CollectionAssert.AreEqual(dbMsDataFile.MsLevels, dbMsDataFile2.MsLevels);
            CollectionAssert.AreEqual(dbMsDataFile.Times, dbMsDataFile2.Times);
        }
    }
}
