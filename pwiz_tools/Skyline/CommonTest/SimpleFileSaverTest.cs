/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    /// <summary>
    /// Tests for <see cref="SimpleFileSaver"/>, the atomic-write primitive every durable
    /// artifact writer in the command-line tools routes through. Locks the contract those
    /// writers rely on: the temp is a sibling in the destination directory (so Commit is an
    /// in-volume rename that cannot truncate on a NAS), Commit promotes it and replaces any
    /// existing destination, CommitAs moves it to another name, and disposal without Commit
    /// discards the temp while leaving the destination untouched - unless the forensic
    /// keep-failed-writes switch is on.
    /// </summary>
    [TestClass]
    public class SimpleFileSaverTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSimpleFileSaver()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sfs_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            bool savedKeep = SimpleFileSaver.KeepFailedWrites;
            try
            {
                SimpleFileSaver.KeepFailedWrites = false;
                string dest = Path.Combine(dir, "artifact.bin");
                TestAtomicContract(dir, dest);
                TestCommitAs(dir, dest);
                TestKeepFailedWrites(dir, dest);
            }
            finally
            {
                SimpleFileSaver.KeepFailedWrites = savedKeep;
                Directory.Delete(dir, true);
            }
        }

        private static void TestAtomicContract(string dir, string dest)
        {
            // Commit promotes the sibling temp into the destination.
            using (var saver = new SimpleFileSaver(dest))
            {
                // The temp lives in the SAME directory as the destination - the property that
                // makes Commit a metadata-only in-volume rename rather than a cross-volume copy
                // that could silently truncate on a CIFS/NFS mount.
                AssertEx.AreEqual(Path.GetDirectoryName(Path.GetFullPath(dest)), Path.GetDirectoryName(saver.SafeName));
                AssertEx.AreNotEqual(Path.GetFullPath(dest), saver.SafeName, "The temp must not be the destination");
                AssertEx.IsTrue(Path.GetFileName(saver.SafeName).StartsWith(SimpleFileSaver.TEMP_PREFIX));
                AssertEx.IsTrue(File.Exists(saver.SafeName)); // The constructor claims a 0-byte temp

                File.WriteAllText(saver.SafeName, "v1");
                AssertEx.IsFalse(File.Exists(dest)); // Not promoted until Commit
                saver.Commit();
            }
            AssertEx.AreEqual("v1", File.ReadAllText(dest));
            AssertNoTempFiles(dir);

            // Commit over an existing destination replaces it.
            using (var saver = new SimpleFileSaver(dest))
            {
                File.WriteAllText(saver.SafeName, "v2");
                saver.Commit();
            }
            AssertEx.AreEqual("v2", File.ReadAllText(dest));

            // Disposal without Commit discards the temp and leaves the destination intact.
            string abandonedTemp;
            using (var saver = new SimpleFileSaver(dest))
            {
                abandonedTemp = saver.SafeName;
                File.WriteAllText(saver.SafeName, "discarded on dispose");
            }
            AssertEx.IsFalse(File.Exists(abandonedTemp));
            AssertEx.AreEqual("v2", File.ReadAllText(dest));
            AssertNoTempFiles(dir);

            // A missing destination directory is created (a bare File.WriteAllText throws).
            string nestedDir = Path.Combine(dir, "created", "on", "demand");
            string nestedDest = Path.Combine(nestedDir, "artifact.bin");
            AssertEx.IsFalse(Directory.Exists(nestedDir));
            using (var saver = new SimpleFileSaver(nestedDest))
            {
                AssertEx.IsTrue(Directory.Exists(nestedDir));
                File.WriteAllText(saver.SafeName, "nested");
                saver.Commit();
            }
            AssertEx.AreEqual("nested", File.ReadAllText(nestedDest));

            // A bare file name resolves against the working directory, so its temp is a sibling
            // of the destination rather than a failure inside Path.GetDirectoryName.
            string cwd = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(dir);
            try
            {
                using (var saver = new SimpleFileSaver("bare.bin"))
                {
                    AssertEx.AreEqual(Path.Combine(dir, "bare.bin"), saver.RealName);
                    AssertEx.AreEqual(dir, Path.GetDirectoryName(saver.SafeName));
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(cwd);
            }
        }

        private static void TestCommitAs(string dir, string dest)
        {
            // CommitAs keeps a finished file under another name: the fallback a writer uses
            // when Commit could not replace the final name.
            string alternate = Path.Combine(dir, "artifact (1).bin");
            using (var saver = new SimpleFileSaver(dest))
            {
                File.WriteAllText(saver.SafeName, "kept elsewhere");
                saver.CommitAs(alternate);
                AssertEx.IsNull(saver.SafeName);
            }
            AssertEx.AreEqual("kept elsewhere", File.ReadAllText(alternate));
            AssertEx.AreEqual("v2", File.ReadAllText(dest)); // The final name is untouched
            AssertNoTempFiles(dir);

            // A failed Commit leaves the temp for CommitAs. A read-only destination fails with
            // UnauthorizedAccessException (a file open elsewhere would give IOException).
            File.SetAttributes(dest, FileAttributes.ReadOnly);
            try
            {
                using (var saver = new SimpleFileSaver(dest))
                {
                    File.WriteAllText(saver.SafeName, "v3");
                    AssertEx.ThrowsException<UnauthorizedAccessException>(() => saver.Commit());
                    AssertEx.IsTrue(File.Exists(saver.SafeName));
                    string alternate2 = Path.Combine(dir, "artifact (2).bin");
                    saver.CommitAs(alternate2);
                    AssertEx.AreEqual("v3", File.ReadAllText(alternate2));
                }
            }
            finally
            {
                File.SetAttributes(dest, FileAttributes.Normal);
            }
            AssertEx.AreEqual("v2", File.ReadAllText(dest));
            AssertNoTempFiles(dir);
        }

        private static void TestKeepFailedWrites(string dir, string dest)
        {
            // The forensic opt-in: an abandoned temp survives disposal instead of being
            // deleted, without ever touching the real destination.
            SimpleFileSaver.KeepFailedWrites = true;
            string abandonedTemp;
            using (var saver = new SimpleFileSaver(dest))
            {
                abandonedTemp = saver.SafeName;
                File.WriteAllText(saver.SafeName, "left for forensics");
            }
            AssertEx.IsTrue(File.Exists(abandonedTemp));
            AssertEx.AreEqual("left for forensics", File.ReadAllText(abandonedTemp));
            AssertEx.AreEqual("v2", File.ReadAllText(dest));

            // A later successful write still commits and replaces normally - the flag only
            // changes what happens to an ABANDONED temp.
            using (var saver = new SimpleFileSaver(dest))
            {
                File.WriteAllText(saver.SafeName, "v4");
                saver.Commit();
            }
            AssertEx.AreEqual("v4", File.ReadAllText(dest));
            SimpleFileSaver.KeepFailedWrites = false;
        }

        private static void AssertNoTempFiles(string dir)
        {
            AssertEx.AreEqual(0, Directory.GetFiles(dir, SimpleFileSaver.TEMP_PREFIX + "*").Length);
        }
    }
}
