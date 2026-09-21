/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="FileSaver"/>, the atomic-write primitive every durable
    /// Osprey artifact writer routes through. Locks the contract those writers rely
    /// on: the temp is a sibling in the destination directory (so Commit is an
    /// in-volume rename that cannot truncate on a NAS), Commit promotes it and
    /// replaces any existing destination, and disposal without Commit discards the
    /// temp while leaving the destination untouched.
    /// </summary>
    [TestClass]
    public class FileSaverTest
    {
        [TestMethod]
        public void TestFileSaverAtomicContract()
        {
            string dir = Path.Combine(Path.GetTempPath(), "osprey_fs_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                string dest = Path.Combine(dir, "artifact.bin");

                // Commit promotes the sibling temp into the destination.
                using (var saver = new FileSaver(dest))
                {
                    // The temp lives in the SAME directory as the destination -- the property
                    // that makes Commit a metadata-only in-volume rename rather than a
                    // cross-volume copy that could silently truncate on a CIFS/NFS mount.
                    Assert.AreEqual(Path.GetDirectoryName(Path.GetFullPath(dest)),
                        Path.GetDirectoryName(saver.SafeName));
                    Assert.AreNotEqual(Path.GetFullPath(dest), saver.SafeName);
                    Assert.IsTrue(File.Exists(saver.SafeName)); // ctor claims a 0-byte temp

                    File.WriteAllText(saver.SafeName, "v1");
                    Assert.IsFalse(File.Exists(dest));          // not promoted until Commit
                    saver.Commit();
                }
                Assert.AreEqual("v1", File.ReadAllText(dest));

                // Commit over an existing destination replaces it.
                using (var saver = new FileSaver(dest))
                {
                    File.WriteAllText(saver.SafeName, "v2");
                    saver.Commit();
                }
                Assert.AreEqual("v2", File.ReadAllText(dest));

                // Disposal without Commit discards the temp and leaves the destination intact.
                string abandonedTemp;
                using (var saver = new FileSaver(dest))
                {
                    abandonedTemp = saver.SafeName;
                    File.WriteAllText(saver.SafeName, "discarded on dispose");
                }
                Assert.IsFalse(File.Exists(abandonedTemp));     // temp cleaned up
                Assert.AreEqual("v2", File.ReadAllText(dest));  // destination unchanged

                // FileSaver creates a missing destination directory (a behavior change vs a
                // bare File.WriteAllText, which throws on a missing parent). Locks it in.
                string nestedDir = Path.Combine(dir, "created", "on", "demand");
                string nestedDest = Path.Combine(nestedDir, "artifact.bin");
                Assert.IsFalse(Directory.Exists(nestedDir));
                using (var saver = new FileSaver(nestedDest))
                {
                    Assert.IsTrue(Directory.Exists(nestedDir));
                    File.WriteAllText(saver.SafeName, "nested");
                    saver.Commit();
                }
                Assert.AreEqual("nested", File.ReadAllText(nestedDest));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
