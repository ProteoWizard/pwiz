/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]

    // Test to make sure antivirus software is not monitoring the test directory by 
    // creating a fake threat file containing a standard test string that's used to 
    // test antivirus systems (See http://www.eicar.org/86-0-Intended-use.html for details).
    // If the file is not there after we create it, we know we're being watched.
    //
    // This is necessary because antivirus systems slow down and even interfere with testing.
    //
    // On a system that's not configured properly this may set off alarms, but that's just as well

    public class AntivirusExclusionTest : AbstractUnitTest
    {
        [TestMethod]
        public void AaantivirusTestExclusion() // Intentional misspelling to encourage this as first test in nightlies
        {
            CheckDirectory(".");
        }

        private static void CheckDirectory(string directory)
        {
            var eicarTestString = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"; // See https://www.eicar.org/download-anti-malware-testfile/
            if (!Directory.Exists(directory))
            {
                return; // First run of tutorials, presumably
            }

            var eicarTestFilename = $@"eicar_fake_threat_{LocalizationHelper.CurrentCulture.ThreeLetterISOLanguageName}.com";
            var eicarTestFile = Path.Combine(directory, eicarTestFilename);
            if (!File.Exists(eicarTestFile))
            {
                File.WriteAllText(eicarTestFile, eicarTestString); // If we are being watched, this should get removed immediately
            }
            string test = string.Empty;
            try
            {
                test = File.ReadAllText(eicarTestFile); // This should succeed - if not we are probably under antivirus scrutiny, which can mess with other tests
            }
            catch
            {
                 // Do nothing
            }
            if (!eicarTestString.Equals(test))
            {
                Assert.Fail($"Could not read contents of the (completely harmless!) antivirus test file \"{eicarTestFile}\", probably because it was quarantined by antivirus software.  If your antivirus flagged on \"{eicarTestFilename}\", don't panic - that's part of the test (see https://www.eicar.org/download-anti-malware-testfile/).  Now go exclude that directory from further antivirus scrutiny, as it causes file locking problems in the tests.");
            }
            if (File.Exists(eicarTestFile))  // Don't leave this lying around - it can cause problems with automated backups etc
            {
                Helpers.TryTwice(() =>
                {
                    File.WriteAllText(eicarTestFile, string.Empty); // So antivirus doesn't flag on recycle bin
                    File.Delete(eicarTestFile);
                }); 
            }
        }
    }
}
