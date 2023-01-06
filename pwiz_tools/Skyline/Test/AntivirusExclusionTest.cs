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

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Test to check whether antivirus software is not monitoring the test directory by 
    /// creating a fake threat file containing a standard test string that's used to 
    /// test antivirus systems (See http://www.eicar.org/86-0-Intended-use.html for details).
    /// If the file is not there after we create it, we know we're being watched.
    ///
    /// It is not considered an error if there is antivirus software watching the folder,
    /// but a warning will be written to the test log file.
    /// This is necessary because antivirus systems may slow down or even interfere with testing.
    /// </summary>
    [TestClass]
    public class AntivirusExclusionTest : AbstractUnitTest
    {
        [TestMethod]
        public void AaantivirusTestExclusion() // Intentional misspelling to encourage this as first test in nightlies
        {
            CheckDirectory(".");
        }

        private static void CheckDirectory(string directory)
        {
            var eicarTestString = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"; // See http://www.eicar.org/86-0-Intended-use.html
            if (!Directory.Exists(directory))
            {
                return; // First run of tutorials, presumably
            }
            var eicarTestFile = Path.Combine(directory,"eicar_fake_threat.com");
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
                // If anti virus software is enabled, do not treat it as an error but write the fact out to the test log
                Console.Out.WriteLine("Virus scanning appears to be enabled in the folder '{0}'. Tests may be flaky or slow as a result.", Path.GetFullPath(directory));
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
