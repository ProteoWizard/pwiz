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

            // N.B. Excel's behavior around these admittedly weird uses of quotes has changed since this test was written
            // (For what it's worth, Google Spreadsheets largely agrees with Excel) - bpratt March 2018
            Assert.AreEqual("End in quote", "\"End in quote".ParseCsvFields()[0]);
            Assert.AreEqual("Internal quotes", "Intern\"al quot\"es,9.7".ParseCsvFields()[0]);
            Assert.AreEqual("Multiple \"quote\" blocks",
                "\"Mult\"iple \"\"\"quote\"\"\" bl\"ocks\",testing,#N/A".ParseCsvFields()[0]);

            // Make sure we can read in small molecule transition lists that use  our isotope nomenclature eg C12H5O"7
            // We can reasonably expect this to meet modern standards and and be escaped as C12H5O""7 (though excel/google  accept C12H5O"7)
            Assert.AreEqual("C12H5O\"7", "C12H5O\"\"7,1".ParseCsvFields()[0]);
            Assert.AreEqual("1", "C12H5O\"\"7,1".ParseCsvFields()[1]);
            Assert.AreEqual("C12H5O\"", "C12H5O\"\",1".ParseCsvFields()[0]);
            Assert.AreEqual("1", "C12H5O\"\",1".ParseCsvFields()[1]);
            // N.B. it would be nice if this unescaped quote worked too, as it does in Excel and Google Spreadsheets even though it's not "standard"
            // Assert.AreEqual("C12H5O\"7", "C12H5O\"7,1".ParseCsvFields()[0]); 

            var testStr = "c:\\tmp\\foo\tbar\r\n\\r\\n";
            var escaped = testStr.EscapeTabAndCrLf();
            var escapedTwice = escaped.EscapeTabAndCrLf();
            Assert.AreNotEqual(testStr, escaped);
            Assert.AreEqual(testStr, escaped.UnescapeTabAndCrLf());
            Assert.AreEqual(testStr, escapedTwice.UnescapeTabAndCrLf().UnescapeTabAndCrLf());
            var testStrs = new[] { testStr, "c:\\tmp2\\foo2\tbar2\r\n" };
            var escaped2 = TextUtil.ToEscapedTSV(testStrs);
            var unescaped2 = escaped2.FromEscapedTSV();
            for (int i =0; i < testStrs.Length; i++)
                Assert.AreEqual(testStrs[i], unescaped2[i]);
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
            Assert.IsTrue(ArrayUtil.EqualsDeep(fields, fieldsOut), "while parsing:\n"+sb+"\nexpected:\n" + string.Join("\n", fields) + "\n\ngot:\n" + string.Join("\n", fieldsOut));
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
            string[] lines = {@"dog,1.1,cat,2.2", @"pony,3.3,fish,4.4"};
            const char csvSeperator = ',';
            // ReSharper disable once LocalizableElement
            var withheaders = new DsvFileReader(new StringReader("0,1,2,3\n" + String.Join("\n", lines)), csvSeperator);
            // ReSharper disable once LocalizableElement
            var withoutheaders = new DsvFileReader(new StringReader(String.Join("\n", lines)), csvSeperator, hasHeaders: false);

            Assert.AreEqual(withheaders.NumberOfFields, withoutheaders.NumberOfFields);
            for (int h = 0; h < withoutheaders.NumberOfFields; h++)
            {
                var f = String.Format(@"{0}", h); // verify that a headerless CSV file will pretend to have a header of form @"0,1,2,..."
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
            // ReSharper disable once LocalizableElement
            var A = String.Join("\n", lines).Replace("cat","hamster");  // change col 2, then ignore it
            // ReSharper disable once LocalizableElement
            var B = String.Join("\n", lines).Replace("1.1","1.09");  // add error at limits of precision, then allow for it
            AssertEx.FieldsEqual(A, B, 4, 2, true);  

        }

        [TestMethod]
        public void SafeDeleteTest()
        {
            // Test ArgumentException.
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(null));
            // ReSharper disable once LocalizableElement
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(""));
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(@"   "));
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(@"<path with illegal chars>"));
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(null, true));
            // ReSharper disable once LocalizableElement
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete("", true));
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(@"   ", true));
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(@"<path with illegal chars>", true));

            // Test DirectoryNotFoundException.
            // ReSharper disable once LocalizableElement
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(@"c:\blah-blah-blah\blah.txt"));
            // ReSharper disable once LocalizableElement
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(@"c:\blah-blah-blah\blah.txt", true));

            // Test PathTooLongException.
            var pathTooLong = @"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx".Replace(@"x", @"xxxxxxxxxx");
            AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(pathTooLong));
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(pathTooLong, true));

            // Test IOException.
            const string busyFile = "TestBusyDelete.txt";
            using (File.CreateText(busyFile))
            {
                AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(busyFile));
                AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(busyFile, true));
            }
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(busyFile));

            // Test UnauthorizedAccessException.
            const string readOnlyFile = "TestReadOnlyFile.txt";
// ReSharper disable LocalizableElement
            // ReSharper disable once LocalizableElement
            File.WriteAllText(readOnlyFile, "Testing read only file delete.\n");
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

        /// <summary>
        /// Makes sure nobody accidentally checked in a change to <see cref="ParallelEx.SINGLE_THREADED"/>.
        /// </summary>
        [TestMethod]
        public void TestParallelExNotSingleThreaded()
        {
            Assert.IsFalse(ParallelEx.SINGLE_THREADED);
        }
    }
}
