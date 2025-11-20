/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using System;
using System.IO;
using System.Linq;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class DllFinderUnitTest : AbstractUnitTest
    {
        /// <summary>
        /// Uses registry and file configurations described in JSON to test finding
        /// and copying Thermo method export DLLs.
        /// </summary>
        [TestMethod]
        public void ThermoDllFinderTest()
        {
            var clazz = typeof(DllFinderUnitTest);
            foreach (var testCase in ThermoDllFinderTestCase.LoadAll(clazz.Assembly.GetManifestResourceStream(clazz, "DllFinderUnitTest.json")))
            {
                try
                {
                    if (testCase.ExpectException.HasValue)
                    {
                        AssertEx.ThrowsException<IOException>(() => RunTest(testCase),
                            GetExceptionMessage(testCase.ExpectException.Value));
                    }
                    else
                    {
                        AssertEx.NoExceptionThrown<Exception>(() => RunTest(testCase));
                    }
                }
                catch (Exception e)
                {
                    throw new AssertFailedException(TextUtil.LineSeparate($"Test case failed '{testCase.TestName}'",
                        e.ToString()));
                }
            }
        }

        private void RunTest(ThermoDllFinderTestCase testCase)
        {
            var testServices = new TestDllFinderServices(testCase);

            var finder = new ThermoDllFinder(testServices); // May throw

            var softwareInfo = finder.GetSoftwareInfo();
            Assert.AreEqual(testCase.ExpectedInstrumentType, softwareInfo.InstrumentType);
            Assert.AreEqual(testCase.ExpectedVersion, softwareInfo.Version);
            if (testCase.ExpectFailureReason.HasValue)
            {
                Assert.AreEqual(GetFailureMessage(testCase.ExpectFailureReason.Value), softwareInfo.FailureReason);
            }
            if (testCase.ExpectedCopyCount > 0)
            {
                Assert.IsNotNull(softwareInfo.Path);
                Assert.IsTrue(testServices.DirectoryExists(softwareInfo.Path), $"The directory {softwareInfo.Path} does not exist");
            }

            finder.EnsureDlls();    // May throw

            VerifyCopiedDlls(testCase, finder, testServices);
        }

        private string GetExceptionMessage(int testCaseExpectException)
        {
            switch (testCaseExpectException)
            {
                case 0:
                    return ModelResources.ThermoMassListExporter_EnsureLibraries_Thermo_method_creation_software_may_not_be_installed_correctly_;
                case 1:
                    return ModelResources.ThermoMassListExporter_EnsureLibraries_Failed_to_find_a_valid_Thermo_instrument_installation_;
            }

            return null;
        }

        private string GetFailureMessage(int testCaseExpectException)
        {
            switch (testCaseExpectException)
            {
                case 0:
                    return string.Format(ModelResources.ThermoDllFinder_GetSoftwareInfo_The_key__0__was_not_found_in_the_Windows_registry_, ThermoDllFinder.ROOT_KEY_PATH);
                case 1:
                    return TextUtil.LineSeparate(string.Format(ModelResources.ThermoDllFinder_GetSoftwareInfo_Failed_to_find_a_machine_name_key_with_a_valid_ProgramPath, ThermoDllFinder.ROOT_KEY_PATH), string.Empty);
                case 2:
                    return TextUtil.LineSeparate(string.Format(ModelResources.ThermoDllFinder_GetSoftwareInfo_Failed_to_find_a_machine_name_key_with_a_valid_ProgramPath, ThermoDllFinder.ROOT_KEY_PATH), 
                        TextUtil.LineSeparate("OrbitrapExploris480", "OrbitrapExploris120", "OrbitrapExplorisGC"));
                case 3:
                    return TextUtil.LineSeparate(string.Format(ModelResources.ThermoDllFinder_GetSoftwareInfo_The_ProgramPath__0__for_the_instrument__1__is_missing_required_files_,"C:\\Thermo\\instrumentSoftwarePath", "OrbitrapAstral"),
                        "Thermo.TNG.MethodXMLInterface.dll");
            }

            return null;
        }
        private void VerifyCopiedDlls(ThermoDllFinderTestCase testCase, ThermoDllFinder finder,
            TestDllFinderServices testServices)
        {
            string destDir = finder.DestinationDir;
            if (testCase.RegistrySubKeys == null)
            {
                // If no dependency keys then it should only be possible to succeed if
                // the test started with all the required files in place
                Assert.IsTrue(finder.ContainsDependencyLibraries(destDir));
                return;
            }

            // Should end up with dependent libraries in the destination folder
            foreach (var dependencyLibrary in ThermoDllFinder.AllDependencyLibraries)
            {
                string fileName = dependencyLibrary.DllFileName;
                var sourceFile = GetTestFile(testCase, fileName, destDir, false);

                // If not a required library and the file was not present, continue
                if (!dependencyLibrary.IsRequired && sourceFile == null)
                    continue;

                Assert.IsNotNull(sourceFile, $"Missing required source file {fileName}");

                bool copied = testServices.CopiedFiles.Any(f => Equals(f.Src, sourceFile.Path));
                if (!copied)
                {
                    var destFile = GetTestFile(testCase, fileName, destDir, true);
                    Assert.IsNotNull(destFile, $"Dependency file {fileName} not copied unexpectedly.");
                    Assert.AreEqual(destFile.LastWriteTime, sourceFile.LastWriteTime, $"Dependency file {fileName} not overwritten unexpectedly.");
                }
            }

            // All files in the test that are not starting in the destination folder are expected to be copied
            Assert.AreEqual(testCase.ExpectedCopyCount ?? testCase.Files.Count(f => !Equals(destDir, Path.GetDirectoryName(f.Path))),
                testServices.CopiedFiles.Count);
        }

        private static FileData GetTestFile(ThermoDllFinderTestCase testCase, string fileName, string destDir, bool inDest)
        {
            var sourceFile = testCase.Files.FirstOrDefault(f => 
                Equals(fileName, Path.GetFileName(f.Path)) &&
                Equals(destDir, Path.GetDirectoryName(f.Path)) == inDest);
            return sourceFile;
        }
    }
}
