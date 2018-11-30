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
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class TextUtilTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestEncryptString()
        {
            const string testString1 = "TestString1";
            const string testString2 = "TestString2";
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
            const string garbageString = "garbage";
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

        [TestMethod]
        public void TestCommonPrefixAndSuffix()
        {
            string[] baseStrings = {"mediummer", "much, much longer", string.Empty, "short"};
            const string fixText = "1234567890";
            const int len1 = 3;
            Assert.AreEqual(fixText.Substring(0, len1), AddPrefix(baseStrings, fixText, len1).GetCommonPrefix());
            Assert.AreEqual(fixText.Substring(fixText.Length - len1), AddSuffix(baseStrings, fixText, len1).GetCommonSuffix());
            Assert.AreEqual(string.Empty, baseStrings.GetCommonPrefix());
            Assert.AreEqual(string.Empty, baseStrings.GetCommonSuffix());
            var base2 = baseStrings.Take(2).ToArray();
            Assert.AreEqual("m", base2.GetCommonPrefix());
            Assert.AreEqual("er", base2.GetCommonSuffix());
            Assert.AreEqual(string.Empty, base2.GetCommonPrefix(2));
            Assert.AreEqual(string.Empty, base2.GetCommonSuffix(3));
            const int len2 = 6;
            Assert.AreEqual(fixText.Substring(0, len2), AddPrefix(base2, fixText, len2).GetCommonPrefix(len2));
            Assert.AreEqual(fixText.Substring(fixText.Length - len2), AddSuffix(base2, fixText, len2).GetCommonSuffix(len2));
            Assert.AreEqual(string.Empty, AddPrefix(base2, fixText, len2).GetCommonPrefix(len2+1));
            Assert.AreEqual(string.Empty, AddSuffix(base2, fixText, len2).GetCommonSuffix(len2+1));
        }

        private IEnumerable<string> AddPrefix(string[] baseStrings, string fixText, int i)
        {
            foreach (string baseString in baseStrings)
                yield return fixText.Substring(0, i++) + baseString;
        }

        private IEnumerable<string> AddSuffix(string[] baseStrings, string fixText, int i)
        {
            foreach (string baseString in baseStrings)
                yield return baseString + fixText.Substring(fixText.Length - i, i++);
        }
    }
}
