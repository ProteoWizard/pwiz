/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Text;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Summary description for UtilTest
    /// </summary>
    [TestClass]
    public class UtilTest : AbstractUnitTest
    {
        [TestMethod]
        public void DsvHelperTest()
        {
            TestDsvFields(TextUtil.SEPARATOR_CSV, TextUtil.SEPARATOR_CSV);
            TestDsvFields(TextUtil.SEPARATOR_CSV, TextUtil.SEPARATOR_CSV_INTL);
            TestDsvFields(TextUtil.SEPARATOR_CSV_INTL, TextUtil.SEPARATOR_CSV);
            TestDsvFields(TextUtil.SEPARATOR_CSV_INTL, TextUtil.SEPARATOR_CSV_INTL);
            TestDsvFields(TextUtil.SEPARATOR_TSV, TextUtil.SEPARATOR_TSV);

            Assert.AreEqual("End in quote", "\"End in quote".ParseCsvFields()[0]);
            Assert.AreEqual("Internal quotes", "Intern\"al quot\"es,9.7".ParseCsvFields()[0]);
            Assert.AreEqual("Multiple \"quote\" blocks",
                "\"Mult\"iple \"\"\"quote\"\"\" bl\"ocks\",testing,#N/A".ParseCsvFields()[0]);
        }

        private static void TestDsvFields(char punctuation, char separator)
        {
            var fields = new[]
                             {
                                 "separator" + punctuation + " test", // Just separator
                                 "\"\"",    // Just quotes
                                 "separator" + punctuation + " \"quote" + punctuation + "\" test", // Quotes and separator
                             };
            var sb = new StringBuilder();
            var writer = new StringWriter(sb);
            foreach (string field in fields)
            {
                if (sb.Length > 0)
                    writer.Write(separator);
                writer.WriteDsvField(field, separator);
            }
            var fieldsOut = sb.ToString().ParseDsvFields(separator);
            Assert.IsTrue(ArrayUtil.EqualsDeep(fields, fieldsOut));
        }

        [TestMethod]
        public void DictionaryEqualsDeepTest()
        {
            var a = new Dictionary<int, int>();
            var b = new Dictionary<int, int>();
            a.Add(3, 5);
            b.Add(3, 5);
            Assert.IsTrue(ArrayUtil.EqualsDeep(a, b));
            a.Add(1, 2);
            Assert.IsFalse(ArrayUtil.EqualsDeep(a, b));
            b.Add(1, 3);
            Assert.IsFalse(ArrayUtil.EqualsDeep(a, b));
            b[1] = 2;
            Assert.IsTrue(ArrayUtil.EqualsDeep(a, b));
            a = null;
            // ReSharper disable ExpressionIsAlwaysNull
            Assert.IsFalse(ArrayUtil.EqualsDeep(a, b));
            b = null;
            Assert.IsTrue(ArrayUtil.EqualsDeep(a, b));
            // ReSharper restore ExpressionIsAlwaysNull
        }

        [TestMethod]
        public void DsvHeadersTest()
        {
            // test reading DSV files with and without headers
            string[] lines = {"dog,1.1,cat,2.2", "pony,3.3,fish,4.4"}; // Not L10N
            const char csvSeperator = ',';
            var withheaders = new DsvFileReader(new StringReader("0,1,2,3\n" + String.Join("\n", lines)), csvSeperator);  // Not L10N
            var withoutheaders = new DsvFileReader(new StringReader(String.Join("\n", lines)), csvSeperator, hasHeaders: false);   // Not L10N

            Assert.AreEqual(withheaders.NumberOfFields, withoutheaders.NumberOfFields);
            for (int h = 0; h < withoutheaders.NumberOfFields; h++)
            {
                var f = String.Format("{0}", h); // verify that a headerless CSV file will pretend to have a header of form "0,1,2,..."  // Not L10N
                Assert.AreEqual(withheaders.GetFieldIndex(f), withoutheaders.GetFieldIndex(f));
            }
            int linenumber = 0;
            while (true)
            {
                var fields = withoutheaders.ReadLine();
                var fieldsTest = withheaders.ReadLine();
                Assert.IsTrue((fields == null) == (fieldsTest == null));

                if (fields == null)
                    break;
                for (int f = 0; f < withoutheaders.NumberOfFields; f++)
                {
                    Assert.AreEqual(fields[f], fieldsTest[f]); // quick string compare
                    Assert.AreEqual(lines[linenumber].Split(csvSeperator)[f],fields[f]);
                }
                linenumber++;
            }
            Assert.AreEqual(2,linenumber);

            // let's also check the precision handling and field exception in AssertEx.FieldsEqual
            var A = String.Join("\n", lines).Replace("cat","hamster");  // change col 2, then ignore it // Not L10N
            var B = String.Join("\n", lines).Replace("1.1","1.09");  // add error at limits of precision, then allow for it // Not L10N
            AssertEx.FieldsEqual(A, B, 4, 2, true);  

        }

        [TestMethod]
        public void SafeDeleteTest()
        {
            // Test ArgumentException.
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(null));
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete("")); // Not L10N
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete("   ")); // Not L10N
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete("<path with illegal chars>")); // Not L10N
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(null, true));
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete("", true)); // Not L10N
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete("   ", true)); // Not L10N
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete("<path with illegal chars>", true)); // Not L10N

            // Test DirectoryNotFoundException.
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(@"c:\blah-blah-blah\blah.txt")); // Not L10N
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(@"c:\blah-blah-blah\blah.txt", true)); // Not L10N

            // Test PathTooLongException.
            var pathTooLong = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx".Replace("x", "xxxxxxxxxx"); // Not L10N
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(pathTooLong));
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(pathTooLong, true));

            // Test IOException.
            const string busyFile = "TestBusyDelete.txt"; // Not L10N
            using (File.CreateText(busyFile))
            {
                AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(busyFile));
                AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(busyFile, true));
            }
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(busyFile));

            // Test UnauthorizedAccessException.
            const string readOnlyFile = "TestReadOnlyFile.txt"; // Not L10N
// ReSharper disable LocalizableElement
            File.WriteAllText(readOnlyFile, "Testing read only file delete.\n"); // Not L10N
// ReSharper restore LocalizableElement
            var fileInfo = new FileInfo(readOnlyFile) {IsReadOnly = true};
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(readOnlyFile));
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(readOnlyFile, true));
            fileInfo.IsReadOnly = false;
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(readOnlyFile));

            var directory = Environment.CurrentDirectory;
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(directory));
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(directory, true));
        }
    }
}