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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
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

            var line = sb.ToString();
            var fieldsOut = line.ParseDsvFields(separator);
            Assert.IsTrue(ArrayUtil.EqualsDeep(fields, fieldsOut),
                TextUtil.LineSeparate("while parsing:", line, string.Empty, 
                    "expected:", TextUtil.LineSeparate(fields), string.Empty, 
                    "got:",TextUtil.LineSeparate(fieldsOut)));
            var detectedSeparator = AssertEx.DetermineDsvDelimiter(new[] { line }, out var detectedColumnCount);
            Assert.AreEqual(separator, detectedSeparator);
            Assert.AreEqual(fields.Length, detectedColumnCount);
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

        // Test the code to compare strings with same files on different paths
        [TestMethod]
        public static void TestNoDiffIgnoringPathDifferences()
        {
            // No quotes, tab separated
            var line1 = "C:\\Dev\\FeatureFinding\\pwiz_tools\\Skyline\\SkylineTester Results\\HardklorFeatureDetectionTest\\MS1FilteringMzml_2\\Ms1FilteringMzml\\100803_0001_MCF7_TiB_L.mzML\t4056\t4065\t10\t2\t1223.5398\t612.7772\t6858\tc:\\tmp\\greeble\t21379\t36.4559\t36.9732\t36.6144\t0.9993\t_\t4057\tH104C54N15O16[+4.761216]\tc:\\tmp\\blorf";
            var line2 = "D:\\Nightly\\SkylineTesterForNightly_integration_perf\\SkylineTester Files\\SkylineTester Results\\HardklorFeatureDetectionTest\\MS1FilteringMzml_2\\Ms1FilteringMzml\\100803_0001_MCF7_TiB_L.mzML\t4056\t4065\t10\t2\t1223.5398\t612.7772\t6858\td:\\tmp\\greeble\t21379\t36.4559\t36.9732\t36.6144\t0.9993\t_\t4057\tH104C54N15O16[+4.761216]\tj:\\dogs\\cats\\blorf";
            AssertEx.NoDiff(line1, line2, null, null, true);
            // With quotes, tab separated
            line1 = "\"C:\\Dev\\FeatureFinding\\pwiz_tools\\Skyline\\SkylineTester Results\\HardklorFeatureDetectionTest\\MS1FilteringMzml_2\\Ms1FilteringMzml\\100803_0001_MCF7_TiB_L.mzML\"\t4056\t4065\t10\t2\t1223.5398\t612.7772\t6858\tc:\\tmp\\greeble\t21379\t36.4559\t36.9732\t36.6144\t0.9993\t_\t4057\tH104C54N15O16[+4.761216]\tc:\\tmp\\blorf";
            line2 = "\"D:\\Nightly\\SkylineTesterForNightly_integration_perf\\SkylineTester Files\\SkylineTester Results\\HardklorFeatureDetectionTest\\MS1FilteringMzml_2\\Ms1FilteringMzml\\100803_0001_MCF7_TiB_L.mzML\"\t4056\t4065\t10\t2\t1223.5398\t612.7772\t6858\td:\\tmp\\greeble\t21379\t36.4559\t36.9732\t36.6144\t0.9993\t_\t4057\tH104C54N15O16[+4.761216]\tj:\\dogs\\birds\\blorf";
            AssertEx.NoDiff(line1, line2, null, null, true);
            // CSV
            line1 = line1.Replace("\t", ",");
            line2 = line2.Replace("\t", ",");
            AssertEx.NoDiff(line1, line2, null, null, true);
            // European ; separated
            line1 = line1.Replace(",", ";");
            line2 = line2.Replace(",", ";");
            AssertEx.NoDiff(line1, line2, null, null, true);
            // Make sure it actually does catch differences
            line1 = line1.Replace("MCF", "zzz");
            AssertEx.ThrowsException<AssertFailedException>(() => AssertEx.NoDiff(line1, line2));
        }

        // Test the code used to compare DSV files with tiny numerical differences (e.g. BullseyeSharp MS2 output files)
        [TestMethod]
        public void TestFieldsEqual()
        {
            var txtA =
                "H\tCreationDate Thu May 18 10:22:17 2023\n" +
                "H\tExtractor\tProteoWizard\n" +
                "H\tExtractor version\tXcalibur\n" +
                "H\tSource file\t2021_0810_Eclipse_LiPExp_05_SS3.raw\n" +
                "S\t3\t3\t613.3168\n" +
                "I\tNativeID\tcontrollerType=0 controllerNumber=1 scan=3\n" +
                "I\tRTime\t0.01054074\n" +
                "I\tBPI\t34995.21\n" +
                "I\tBPM\t358.0672\n" +
                "I\tTIC\t247701.8\n" +
                "Z\t1\t612.1838\n" +
                "181.6542 21635.59\n" +
                "203.9089 9885.277\n" +
                "221.082 10638.83\n" +
                "251.8089 9747.531\n" +
                "268.5652 8213.748\n" +
                "300.0618 11877.35\n" +
                "355.0699 12373.97\n" +
                "356.0695 23859.23\n" +
                "357.0657 33525.14\n" +
                "358.0672 34995.21\n" +
                "373.2889 8678.741\n" +
                "510.3278 11798.26\n" +
                "588.6589 10310.75\n" +
                "594.7731 10238.01\n" +
                "940.1085 9085.032\n" +
                "1124.124 10834.5\n" +
                "1208.332 10004.55\n" +
                "S\t6\t6\t422.7364\n" +
                "I\tNativeID\tcontrollerType=0 controllerNumber=1 scan=6\n" +
                "I\tRTime\t0.01241985\n" +
                "I\tBPI\t36354.39\n" +
                "I\tBPM\t157.0823\n" +
                "I\tTIC\t150800.6\n" +
                "Z\t2\t1682.29\n" +
                "157.0823 36354.39\n" +
                "181.659 32937.68\n" +
                "230.6742 8594.602\n" +
                "384.1447 10770.04\n" +
                "422.3313 22468.13\n" +
                "487.0845 9533.217\n" +
                "780.8187 9651.796\n" +
                "1014.462 9763.788\n" +
                "1582.427 10726.89\n";

            var txtB = txtA.Replace("247701.8", "247701.7"). // Could be serializations of 247701.751 and 247701.749
                Replace("36354.39", "36354.40").
                Replace("34995.21", "34995.22").
                Replace("10726.89", "10726.9");
            AssertEx.FieldsEqual(new StringReader(txtA),
                new StringReader(txtB),
                null, // Variable field count
                null, // Ignore no columns
                true, // Allow for rounding errors - the TIC line in particular is an issue here
                0, // Allow no extra lines
                null, // No overall tolerance
                1); // Skip first line with its timestamp

            // And make sure it catches actual errors
            var txtC = txtA.Replace("247701.8", "247701.6");
            AssertEx.ThrowsException<AssertFailedException>(() => AssertEx.FieldsEqual(new StringReader(txtA),
                new StringReader(txtC),
                null, // Variable field count
                null, // Ignore no columns
                true, // Allow for rounding errors - the TIC line in particular is an issue here
                0, // Allow no extra lines
                null, // No overall tolerance
                1)); // Skip first line with its timestamp);

            var txtD = txtA.Replace("10726.89", "10726.896");
            AssertEx.ThrowsException<AssertFailedException>(() => AssertEx.FieldsEqual(new StringReader(txtA),
                new StringReader(txtD),
                null, // Variable field count
                null, // Ignore no columns
                true, // Allow for rounding errors - the TIC line in particular is an issue here
                0, // Allow no extra lines
                null, // No overall tolerance
                1)); // Skip first line with its timestamp);
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
            TestContext.EnsureTestResultsDir();
            string busyFile = TestContext.GetTestResultsPath("TestBusyDelete.txt"); // Not L10N
            using (File.CreateText(busyFile))
            {
                AssertEx.ThrowsException<IOException>(() => FileEx.SafeDelete(busyFile));
                AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(busyFile, true));
            }
            AssertEx.NoExceptionThrown<IOException>(() => FileEx.SafeDelete(busyFile));

            // Test UnauthorizedAccessException.
            string readOnlyFile = TestContext.GetTestResultsPath("TestReadOnlyFile.txt"); // Not L10N
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

        /// <summary>
        /// Makes sure nobody accidentally checked in a change to <see cref="ParallelEx.SINGLE_THREADED"/>.
        /// </summary>
        [TestMethod]
        public void ParallelExNotSingleThreadedTest()
        {
            Assert.IsFalse(ParallelEx.SINGLE_THREADED);
        }

        [TestMethod]
        public void OptimalThreadCountTest()
        {
            // Just get out if ParallelEx.SINGLE_THREADED let the ParallelExNotSingleThreadedTest do the work
            // of telling the developer. Too confusing to decypher why this test would be failing
            if (ParallelEx.SINGLE_THREADED)
                return;

            // On some systems we find that parallel performance suffers when not using ServerGC, as during SkylineTester runs, so we lower the max thread count
            var hasReducedLoadThreadCount = !System.Runtime.GCSettings.IsServerGC;
            Assert.AreEqual(hasReducedLoadThreadCount ? MultiFileLoader.MAX_PARALLEL_LOAD_FILES_USER_GC : MultiFileLoader.MAX_PARALLEL_LOAD_FILES,
                MultiFileLoader.GetMaxLoadThreadCount());

            // Make sure an explicit number of threads just returns that number of threads
            Assert.AreEqual(4, MultiFileLoader.GetOptimalThreadCount(4, null, MultiFileLoader.ImportResultsSimultaneousFileOptions.one_at_a_time));
            Assert.AreEqual(4, MultiFileLoader.GetOptimalThreadCount(4, null, MultiFileLoader.ImportResultsSimultaneousFileOptions.several));
            Assert.AreEqual(4, MultiFileLoader.GetOptimalThreadCount(4, null, MultiFileLoader.ImportResultsSimultaneousFileOptions.many));

            // i7 4-core
            int processors = 8;
            Assert.AreEqual(1, MultiFileLoader.GetOptimalThreadCount(null, 6, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.one_at_a_time));
            Assert.AreEqual(processors/4, MultiFileLoader.GetOptimalThreadCount(null, 6, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.several));
            Assert.AreEqual(Math.Min(processors/2, MultiFileLoader.GetMaxLoadThreadCount()), MultiFileLoader.GetOptimalThreadCount(null, null, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.many));
            //   Load balancing 6 files into 2 cycles of 3
            Assert.AreEqual(3, MultiFileLoader.GetOptimalThreadCount(null, 6, 8, MultiFileLoader.ImportResultsSimultaneousFileOptions.many));
            // i7 6-core
            processors = 12;
            Assert.AreEqual(processors/4, MultiFileLoader.GetOptimalThreadCount(null, null, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.several));
            Assert.AreEqual(Math.Min(processors / 2, MultiFileLoader.GetMaxLoadThreadCount()), MultiFileLoader.GetOptimalThreadCount(null, null, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.many));
            // Load balancing 8 files into 2 cycles of 4, or if not using server GC then 3 cycles (of 3,3,2) 
            Assert.AreEqual(Math.Min(4, MultiFileLoader.GetMaxLoadThreadCount()), MultiFileLoader.GetOptimalThreadCount(null, 8, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.many));
            // Xeon 24-core
            processors = 48;
            Assert.AreEqual(Math.Min(processors/4, MultiFileLoader.GetMaxLoadThreadCount()),
                MultiFileLoader.GetOptimalThreadCount(null, null, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.several));
            Assert.AreEqual(Math.Min(processors/2, MultiFileLoader.GetMaxLoadThreadCount()),
                MultiFileLoader.GetOptimalThreadCount(null, null, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.many));
            // Load balancing 14 files into 2 cycles of 7, , or if not using server GC then 5 cycles (of 3,3,3,3,2) 
            Assert.AreEqual(hasReducedLoadThreadCount ? MultiFileLoader.GetMaxLoadThreadCount() : MultiFileLoader.MAX_PARALLEL_LOAD_FILES/2 + 1,
                MultiFileLoader.GetOptimalThreadCount(null, MultiFileLoader.MAX_PARALLEL_LOAD_FILES+2, processors, MultiFileLoader.ImportResultsSimultaneousFileOptions.many));
        }

        [TestMethod]
        public void ArrayUtilSortTest()
        {
            var arrayBase = new[] {4, 2, 1, 3, 5};
            int[] array2 = new int[arrayBase.Length];
            int[] array3 = new int[arrayBase.Length];
            int[] array4 = new int[arrayBase.Length];
            for (int i = 0; i < arrayBase.Length; i++)
            {
                array2[i] = arrayBase[i];
                array3[i] = arrayBase[i];
                array4[i] = arrayBase[i];
            }
            int[] sortIndexes;
            ArrayUtil.Sort(arrayBase, out sortIndexes);
            AssertEx.AreEqualDeep(new[] {2, 1, 3, 0, 4}, sortIndexes);
            ArrayUtil.Sort(array2, array3, array4);
            AssertEx.AreEqualDeep(arrayBase, array2);
            AssertEx.AreEqualDeep(arrayBase, array3);
            AssertEx.AreEqualDeep(arrayBase, array4);
        }

        [TestMethod]
        public void TestIsTempZipFolder()
        {
            string zipFileName;
            Assert.IsTrue(DirectoryEx.IsTempZipFolder(@"C:\Users\skylinedev\AppData\Local\Temp\Temp1_TargetedMSMS_2.zip\TargetedMSMS\Low Res\BSA_Protea_label_free_meth3.sky", out zipFileName));
            Assert.AreEqual("TargetedMSMS_2.zip", zipFileName);
            Assert.IsFalse(DirectoryEx.IsTempZipFolder(@"C:\Users\skylinedev\Temp1_TargetedMSMS_2.zip\TargetedMSMS\Low Res\BSA_Protea_label_free_meth3.sky", out zipFileName));
            Assert.IsTrue(DirectoryEx.IsTempZipFolder(@"C:\Users\skylinedev\AppData\Local\Temp\ZipFile.zip\BSA_Protea_label_free_meth3.sky", out zipFileName));
            Assert.AreEqual("ZipFile.zip", zipFileName);
        }

        [TestMethod]
        public void TestEscapePathForXML()
        {
            Assert.AreEqual("&amp;oops", PathEx.EscapePathForXML("&oops")); // Unescaped ampersand in filename
            Assert.AreEqual("&amp;oops &amp;oops", PathEx.EscapePathForXML("&oops &oops")); // More than one
            Assert.AreEqual("&amp;oops", PathEx.EscapePathForXML("&amp;oops")); // Already escaped
            Assert.AreEqual("&amp;oops &amp;oops &amp;oops", PathEx.EscapePathForXML("&oops &oops &amp;oops")); // Mix of escaped and unescaped
            Assert.AreEqual("&amp;oops &amp;oops &apos;oops", PathEx.EscapePathForXML("&oops &oops &apos;oops")); // Use of & to escape apostrophe
        }

        [TestMethod]
        public void FilesDirTest()
        {
            var cleanupLevel = DesiredCleanupLevel;
            try
            {
                var testFilesDir = new TestFilesDir(TestContext, @"Test\DocLoadLibraryTest.zip");
                // Lock a file and make sure that CleanUp fails
                // NOTE: This takes seconds because the cleanup code uses TryTwice()
                const string fileName = "DocWithLibrary.sky";
                string filePath = testFilesDir.GetTestPath(fileName);
                using (new StreamReader(filePath))
                {
                    // Test the code that names the process locking a file
                    try
                    {
                        var names = string.Join(@", ", FileLockingProcessFinder.GetProcessesUsingFile(filePath).Select(p => p.ProcessName));
                        AssertEx.Contains(names, Process.GetCurrentProcess().ProcessName);
                    }
                    catch
                    {
                        // ignored - the mechanism behind FileLockingProcessFinder may not be supported, probably due to insufficient permissions
                    }

                    // Test our post-test cleanup code's handling of a locked file
                    if (!Install.IsRunningOnWine)
                    {
                        DesiredCleanupLevel = DesiredCleanupLevel.none; // Folders renamed
                        AssertEx.ThrowsException<IOException>(testFilesDir.Cleanup, x => AssertEx.Contains(x.Message, fileName));
                        DesiredCleanupLevel = DesiredCleanupLevel.all;  // Folders deleted
                        AssertEx.ThrowsException<IOException>(testFilesDir.Cleanup, x => AssertEx.Contains(x.Message, fileName));
                    }
                }
                // Now test successful cleanup
                DesiredCleanupLevel = DesiredCleanupLevel.downloads; // Folders renamed
                testFilesDir.Cleanup();
                AssertEx.FileExists(filePath);
                DesiredCleanupLevel = DesiredCleanupLevel.all;  // Folders deleted
                testFilesDir.Cleanup();
                AssertEx.FileNotExists(filePath);
                AssertEx.IsFalse(Directory.Exists(testFilesDir.FullPath));
                // CONSIDER: This could be extended to test persistent files and
                //           ZIP files in the Downloads folder.
            }
            finally
            {
                DesiredCleanupLevel = cleanupLevel;
            }
        }
    }
}
