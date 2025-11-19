/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests for <see cref="Kernel32.CopyFileWithProgress"/>
    /// </summary>
    [TestClass]
    public class CopyFileWithProgressTest : AbstractUnitTest
    {
        private const string TEST_ZIP_PATH = @"Test\CopyFileWithProgressTest.zip";

        [TestMethod]
        public void TestCopyFileWithProgressSimple()
        {
            int? progressValue = null;
            Action<int> progressAction = value => progressValue = value;
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var noExistPath = TestFilesDir.GetTestPath("NoExist.txt");
            var destination = TestFilesDir.GetTestPath("Destination.txt");
            Assert.IsFalse(File.Exists(noExistPath));
            Assert.IsFalse(File.Exists(destination));
            // Should fail because source file does not exist
            AssertEx.ThrowsException<IOException>(() =>
                Kernel32.CopyFileWithProgress(noExistPath, destination, false, CancellationToken.None, progressAction));

            var source = TestFilesDir.GetTestPath("Source.txt");
            var sourceFileBytes = new UTF8Encoding(true).GetBytes("sourceFileBytes");
            using (var stream = File.OpenWrite(source))
            {
                stream.Write(sourceFileBytes, 0, sourceFileBytes.Length);

                // Should fail because source is locked
                AssertEx.ThrowsException<IOException>(() =>
                    Kernel32.CopyFileWithProgress(source, destination, true, CancellationToken.None, progressAction));
            }

            AssertEx.IsFalse(File.Exists(destination));
            Kernel32.CopyFileWithProgress(source, destination, false, CancellationToken.None, progressAction);
            AssertEx.IsTrue(File.Exists(destination));
            AssertFileContent(sourceFileBytes, destination);
            var source2 = TestFilesDir.GetTestPath("Source2.txt");
            var source2FileBytes = new UTF8Encoding(false).GetBytes("Second source file bytes");
            using (var stream = File.OpenWrite(source2))
            {
                // Should fail because destination (source2) is locked
                AssertEx.ThrowsException<IOException>(() =>
                    Kernel32.CopyFileWithProgress(source, source2, true, CancellationToken.None, progressAction));

                // Should fail because source is locked
                AssertEx.ThrowsException<IOException>(() =>
                    Kernel32.CopyFileWithProgress(source2, destination, true, CancellationToken.None, progressAction));

                stream.Write(source2FileBytes, 0, source2FileBytes.Length);
            }

            AssertFileContent(source2FileBytes, source2);
            // Should fail because destination file exists and overwrite false
            AssertEx.ThrowsException<IOException>(() =>
                Kernel32.CopyFileWithProgress(source2, destination, false, CancellationToken.None, progressAction));
            AssertFileContent(sourceFileBytes, destination);

            Kernel32.CopyFileWithProgress(source2, destination, true, CancellationToken.None, progressAction);
            AssertFileContent(source2FileBytes, destination);

            using (File.OpenRead(destination))
            {
                // Should fail because destination is locked
                AssertEx.ThrowsException<IOException>(() =>
                    Kernel32.CopyFileWithProgress(source, destination, true, CancellationToken.None, progressAction));
                AssertFileContent(source2FileBytes, destination);
            }

            Kernel32.CopyFileWithProgress(source, destination, true, CancellationToken.None, progressAction);
            AssertFileContent(sourceFileBytes, destination);
        }

        [TestMethod]
        public void TestCopyFileWithProgressProgress()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var source = TestFilesDir.GetTestPath("source.txt");
            var destination = TestFilesDir.GetTestPath("destination.txt");

            const int minProgressNotificationCount = 3;
            // Increase the size of the test file until we get notified about progress updates at least 3 times
            for (var fileSize = 1_000_000;; fileSize *= 2)
            {
                List<int> progressValues = new List<int>();
                File.Delete(source);
                File.Delete(destination);
                var fileBytes = GetTestFileBytes().Take(fileSize).ToArray();
                Assert.AreEqual(fileSize, fileBytes.Length);
                File.WriteAllBytes(source, fileBytes);
                Kernel32.CopyFileWithProgress(source, destination, true, CancellationToken.None, progressValues.Add);
                VerifyProgressValues(progressValues);
                if (progressValues.Count > minProgressNotificationCount)
                {
                    return;
                }

                AssertFileContent(fileBytes, destination);
                if (ProcessEx.IsRunningOnWine)
                {
                    // Wine does not support progress notifications so there is nothing more to test
                    return;
                }
            }
        }

        [TestMethod]
        public void TestCopyFileWithProgressCancel()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var source = TestFilesDir.GetTestPath("source.txt");
            var destination = TestFilesDir.GetTestPath("destination.txt");

            // Increase the size of the test file until we are able to cancel it before the copy is completed
            for (var fileSize = 1_000_000;; fileSize *= 2)
            {
                List<int> progressValues = new List<int>();
                File.Delete(source);
                File.Delete(destination);
                var fileBytes = GetTestFileBytes().Take(fileSize).ToArray();
                Assert.AreEqual(fileSize, fileBytes.Length);
                File.WriteAllBytes(source, fileBytes);
                IOException caughtException = null;
                using var cancellationTokenSource = new CancellationTokenSource();
                try
                {
                    const int minProgressNotificationCount = 3;
                    // Copy the file, but cancel after receiving 3 progress notifications
                    Kernel32.CopyFileWithProgress(source, destination, true, cancellationTokenSource.Token,
                        progressValue =>
                        {
                            progressValues.Add(progressValue);
                            if (progressValues.Count >= minProgressNotificationCount)
                            {
                                cancellationTokenSource.Cancel();
                            }
                        });
                }
                catch (IOException exception)
                {
                    if (!cancellationTokenSource.IsCancellationRequested)
                    {
                        throw;
                    }

                    caughtException = exception;
                }

                VerifyProgressValues(progressValues);
                if (caughtException != null)
                {
                    return;
                }

                AssertFileContent(fileBytes, destination);
                if (ProcessEx.IsRunningOnWine)
                {
                    // Wine does not support cancelling a copy so there's nothing more to test
                    return;
                }
            }
        }

        /// <summary>
        /// Verify that progress values are in ascending order and between 0 and 100.
        /// </summary>
        private void VerifyProgressValues(IList<int> progressValues)
        {
            int previous = 0;
            for (int index = 0; index < progressValues.Count; index++)
            {
                var progressValue = progressValues[index];
                Assert.IsFalse(progressValue < previous,
                    "Progress value {0} at position {1} should not be less than {2}", progressValue, index, previous);
                Assert.IsFalse(progressValue > 100, "Progress value {0} at position {1} should not be greater than 100",
                    progressValue, index);
            }
        }

        private IEnumerable<byte> GetTestFileBytes()
        {
            return Enumerable.Range(0, int.MaxValue)
                .SelectMany(i => Encoding.UTF8.GetBytes("Line " + i + "\r\n"))
                .Take(1_000_000_000);
        }

        private static void AssertFileContent(byte[] expectedBytes, string file)
        {
            var actualBytes = File.ReadAllBytes(file);
            CollectionAssert.AreEqual(expectedBytes, actualBytes, "Incorrect bytes in file {0}", file);
        }
    }
}