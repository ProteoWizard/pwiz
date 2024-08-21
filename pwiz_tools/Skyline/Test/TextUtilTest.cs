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
using pwiz.Common.Collections;
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

        /// <summary>
        /// Test for natural sort. Randomly shuffles sorted lists of stings and then sorts them again in order to ensure
        /// consistency and accuracy in establishing the original natural sort order. This test is run on a set of strings
        /// spanning nearly every number and letter in order to demonstrate consistency across a diverse range of possible
        /// file names. Should this test be working correctly it will demonstrate that on each shuffle sort cycle that
        /// the sorting algorithm renders the same sort outcome every time.

        /// </summary>
        [TestMethod]
        public void TestNaturalSort()
        {   
            //Sample test strings
            var orderedSample = new List<string>() 
            {
                "0a123.30561n","0a123.456n","0a123.4567n", // Note this isn't the order supported by the filename natural sort - it doesn't understand 123.30561 as a decimal, sees it as two distinct parts
                "1NvWw","1U2t6","1uBYH","2E1fK","2V6La","3pxza","3sVDq","4tuzE",
                "4VK2-","4ztTB","5QRmn","5Zcd7","6hkS_","6qvCh","6sAzR","7Z25C","8fMsb","8HRzI",
                "8wqJy","9jz1y","a5E6p","a736Q","aL4lL","alpXu","B-2ag","BRrbj","bzUgj","c8qdT","CdaAF",
                "CDk8I","Cdm0k","CFYgZ","D9Xdy","dP8a5","e9teS","eibUe","EMNkm","EsE4z","evAra","eZUyJ",
                "f1A-B","fB6mW","FUjdN","GCtkn","gLHOp","gNbrM","Go6jE","gRm0t","Gw_5C","gxuE_",
                "GYmXe","GyquW","GZw_D","H5a9A","H6KDQ","H6oOL","h7k2O","h7L2H","hcFa4","hhtYn",
                "HNKWG","HOmz9","hp_4Y","hTa-9","igDjW","IqHG0","IXRRk","j0OXu","J6fuV","jnLi3","K6rl0",
                "kCFaa","kIcKi","KS0Ua","KxR_m","Ky9Ef","LHiBw","lMdGG","ls8Vb","lSOc_","lWk5c","mE9Bc",
                "mKppU","mpSNi","mWsW_","naLxD","ne-LG","NhsZW","NPk3-","nsoxE","Nxsqw","o6S3j","Ol8lY",
                "oRQN_","otCNq","OXNJo","p5-zT","pc8rL","PEpSv","pj_FZ","PQWQ0","pseOl","pxiUY","qe7ib",
                "QGkFc","QgYG9","QOi50","QwrRK","QWwUL","qZwkK","Rhsrt","rlw8H",
                "Skyline-64_1_4_0_4421.zip","Skyline-64_1_4_0_4422.zip","Skyline-64_2_5_0_5675.zip","Skyline-64_20_1_0_155.zip",
                "sna_M","SwvOY",
                "TachS","thxs7","U4sCZ","UIQF2","UNGYS","UPLhZ","USZgP","uz-Oq","ve7Ml","vlpJ-",
                "vscw3","W2ffg","waINu","wAYVn","wcnac","wgnCt","Whe2M","WHs9b","wj-Sy","woie2","WOrKF",
                "XfWhr","XfY9w","xlt5k","XPCHC","XxgDy","Zdrnb","zXdQ1"
            };
            var orderedSampleFilename = orderedSample.Select(s => s.Replace("30561n", "356n")).ToList(); // Make it amenable to windows filename sort tradtiion

            // Run test 12 time to ensure consistency 
            for (int i = 0; i < 12; i++)
            {
                SortAndTest(orderedSample, false); // Test sort on a smaller sample set, using the decimal-aware sort
                SortAndTest(orderedSampleFilename, true); // Test sort on a smaller sample set, using the filename sort
            }

            // Shuffle an input list and sort. Compares against original in order version to ensure consistency
            void SortAndTest(List<string> ordered, bool useFilenameSort)
            {
                List<string> misOrdered = Shuffle(ordered); // Shuffle

                misOrdered.Sort((x, y) => useFilenameSort ? NaturalFilenameComparer.Compare(x, y) : NaturalStringComparer.Compare(x, y)); // Naturally sort

                // Compare with original. Prints out discrepancies between sort and original if they should occur
                for (int i = 0; i < ordered.Count; i++)
                {
                    AssertEx.AreEqual(ordered[i], misOrdered[i], string.Format("Test Natural is not sorting in the correct order: at position {0}: {1} vs {2}", i, ordered[i], misOrdered[i]) + 
                                                                 "\nExpected sort:\n" + ordered.Aggregate((a,b) => a + "\n" + b) +
                                                                 "\n\nActual sort:\n" + misOrdered.Aggregate((a, b) => a + "\n" + b));
                }
            }

            // Returns a shuffled copy the input list
            List<string> Shuffle(List<string> inOrder)
            {
                List<string> misOrdered = new List<string>(inOrder); // Copy list
                Random rand = new Random();
                
                // Shuffle by swapping elements positions randomly
                for (int a = 0; a < misOrdered.Count; a++)
                {
                    int loc = rand.Next(misOrdered.Count);
                    (misOrdered[loc], misOrdered[a]) = (misOrdered[a], misOrdered[loc]);
                }
                return misOrdered;
            }
        }

        [TestMethod]
        public void TestEnforceMaxWordLength()
        {
            Assert.AreEqual(6, GetMaxWordLength("Hello, world"));
            Assert.AreEqual(6, GetMaxWordLength("Hello world!"));
            Assert.AreEqual(5, GetMaxWordLength("Hello-world"));
            foreach (var input in new[]
                     {
                         "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                         "The quick brown fox jumps over a lazy dog",
                     })
            {
                int inputMaxWordLength = GetMaxWordLength(input);
                var inputWithoutSpaces = input.Replace(" ", "");
                for (int enforcedMaxWordLength = 1; enforcedMaxWordLength <= input.Length; enforcedMaxWordLength++)
                {
                    var result = TextUtil.EnforceMaxWordLength(input, enforcedMaxWordLength);
                    if (inputMaxWordLength <= enforcedMaxWordLength)
                    {
                        Assert.AreEqual(input, result);
                    }
                    else
                    {
                        Assert.AreNotEqual(input, result);
                        Assert.AreEqual(enforcedMaxWordLength, GetMaxWordLength(result));
                        var resultWithoutSpaces = result.Replace(" ", "");
                        Assert.AreEqual(inputWithoutSpaces, resultWithoutSpaces);
                    }
                }
            }
        }

        private int GetMaxWordLength(string str)
        {
            int maxWordLength = 0;
            int currentWordLength = 0;
            foreach (var ch in str)
            {
                if (char.IsWhiteSpace(ch) || ch == '-')
                {
                    maxWordLength = Math.Max(maxWordLength, currentWordLength);
                    currentWordLength = 0;
                }
                else
                {
                    currentWordLength++;
                }
            }
            return Math.Max(maxWordLength, currentWordLength);
        }
    }
}
