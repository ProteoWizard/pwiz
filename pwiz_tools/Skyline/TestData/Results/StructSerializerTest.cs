/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    [TestClass]
    public class StructSerializerTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestReadStructArray()
        {
            var item1 =
                new CachedFileHeaderStruct
                {
                    lenPath = 1
                };
            var serializer = new StructSerializer<CachedFileHeaderStruct>();
            byte[] bytes = serializer.ToByteArray(item1);
            Assert.AreEqual(Marshal.SizeOf<CachedFileHeaderStruct>(), bytes.Length);
            var roundTrip = serializer.FromByteArray(bytes);
            Assert.AreEqual(item1, roundTrip);
        }

        [TestMethod]
        public void TestShorterSizeOnDisk()
        {
            var item1 = new CachedFileHeaderStruct
            {
                modified = 1,
                lenInstrumentInfo = 100,
            };
            var serializer = new StructSerializer<CachedFileHeaderStruct>() {ItemSizeOnDisk = 8};
            byte[] bytes = serializer.ResizeByteArray(serializer.ToByteArray(item1), 8);
            Assert.AreEqual(8, bytes.Length);
            var roundTrip = serializer.FromByteArray(bytes);
            Assert.AreEqual(1, roundTrip.modified);
            Assert.AreEqual(0, roundTrip.lenInstrumentInfo);
        }

        /// <summary>
        /// Tests using the <see cref="StructSerializer{TItem}.DirectSerializer"/> to read and write
        /// arrays using the <see cref="Skyline.Util.FastWrite"/> functions.
        /// </summary>
        [TestMethod]
        public void TestDirectSerializer()
        {
            var filePath = TestContext.GetTestPath("TestDirectSerializerToDisk.bin");
            byte[] firstBytes = Encoding.UTF8.GetBytes("Beginning");
            var chromTransitions = new[] {new ChromTransition4(522.6f)};
            var chromTransitionSerializer = (StructSerializer<ChromTransition4>) ChromTransition4.StructSerializer();
            byte[] endBytes = Encoding.UTF8.GetBytes("End");
            using (var writeStream = File.OpenWrite(filePath))
            {
                PrimitiveArrays.Write(writeStream, firstBytes);
                bool writeDirectArrayResult =
                    chromTransitionSerializer.DirectSerializer.WriteArray(writeStream, chromTransitions);
                Assert.IsTrue(writeDirectArrayResult);
                PrimitiveArrays.Write(writeStream, endBytes);
            }

            using (var readStream = File.OpenRead(filePath))
            {
                var firstBytesCompare = PrimitiveArrays.Read<byte>(readStream, firstBytes.Length);
                CollectionAssert.AreEqual(firstBytes, firstBytesCompare);
                var chromTransitionsCompare =
                    chromTransitionSerializer.DirectSerializer.ReadArray(readStream, chromTransitions.Length);
                Assert.IsNotNull(chromTransitionsCompare);
                CollectionAssert.AreEqual(chromTransitions, chromTransitionsCompare);
                var endBytesCompare = PrimitiveArrays.Read<byte>(readStream, endBytes.Length);
                CollectionAssert.AreEqual(endBytes, endBytesCompare);
            }
        }
    }
}
