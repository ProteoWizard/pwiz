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

using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class TextUtilTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestEncryptString()
        {
            string testString1 = "TestString1";
            string testString2 = "TestString2";
            string encrypted1 = TextUtil.EncryptString(testString1);

            byte[] bytes1 = Convert.FromBase64String(encrypted1);
            CollectionAssert.AreNotEqual(bytes1, Encoding.UTF8.GetBytes(testString1));

            string decrypted1 = TextUtil.DecryptString(encrypted1);
            Assert.AreEqual(testString1, decrypted1);

            string encrypted2 = TextUtil.EncryptString(testString2);
            Assert.AreNotEqual(encrypted1, encrypted2);
        }

        [TestMethod]
        public void TestDecryptGarbage()
        {
            string garbageString = "garbage";
            AssertEx.ThrowsException<FormatException>(delegate 
            {
                TextUtil.DecryptString(garbageString);
            });
            AssertEx.ThrowsException<CryptographicException>(delegate 
            {
                TextUtil.DecryptString(Convert.ToBase64String(Encoding.UTF8.GetBytes(garbageString)));
            });
            Assert.AreEqual(garbageString, TextUtil.DecryptString(TextUtil.EncryptString(garbageString)));
        }
    }
}
