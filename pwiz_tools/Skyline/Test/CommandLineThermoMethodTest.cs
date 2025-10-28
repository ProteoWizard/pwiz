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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CommandLineThermoMethodTest : AbstractUnitTestEx
    {
        private const string COMMAND_FILE = @"TestData\CommandLineTest.zip";

        [TestMethod]
        public void TestCommandLineExportThermoMethod()
        {
            TestFilesDir = new TestFilesDir(TestContext, COMMAND_FILE);

            string docPath = TestFilesDir.GetTestPath("Study7.sky");
            string thermoTemplate = TestFilesDir.GetTestPath("20100329_Protea_Peptide_targeted.meth");
            string thermoOut = TestFilesDir.GetTestPath("Thermo_test.meth");
            var clazz = typeof(CommandLineThermoMethodTest);
            var testsDllFinder = ThermoDllFinderTestCase.LoadAll(
                clazz.Assembly.GetManifestResourceStream(clazz, "CommandLineThermoMethodTest.json"));

            // Create a .sky file that can be read quickly
            using (var stream = clazz.Assembly.GetManifestResourceStream(clazz, "Study7.sky"))
            {
                Assert.IsNotNull(stream);
                using (var reader = new StreamReader(stream))
                using (var writer = new StreamWriter(docPath))
                {
                    var skyXml = reader.ReadToEnd();
                    writer.Write(skyXml);
                }
            }

            var dllFinderServices = ThermoDllFinder.DEFAULT_SERVICES;
            try
            {
                // No valid installation
                ThermoDllFinder.DEFAULT_SERVICES = testsDllFinder.Last().DllFinderServices;
                string output = RunCommand("--in=" + docPath,
                    "--exp-method-instrument=Thermo",
                    "--exp-template=" + thermoTemplate,
                    "--exp-file=" + thermoOut);
                AssertEx.Contains(output, TextUtil.SpaceSeparate(Resources.CommandStatusWriter_WriteLine_Error_,
                    ModelResources.ThermoMassListExporter_EnsureLibraries_Failed_to_find_a_valid_Thermo_instrument_installation_));

                // Unknown installation type
                var testUnknownInstrument = testsDllFinder[1];
                ThermoDllFinder.DEFAULT_SERVICES = testUnknownInstrument.DllFinderServices;
                output = RunCommand("--in=" + docPath,
                    "--exp-method-instrument=Thermo",
                    "--exp-template=" + thermoTemplate,
                    "--exp-file=" + thermoOut);
                AssertEx.Contains(output, TextUtil.SpaceSeparate(Resources.CommandStatusWriter_WriteLine_Error_, string.Format(ModelResources.ThermoMassListExporter_EnsureLibraries_Unknown_Thermo_instrument_type___0___installed_,
                    testUnknownInstrument.ExpectedInstrumentType)));

                // Wrong type specified
                ThermoDllFinder.DEFAULT_SERVICES = testsDllFinder.First().DllFinderServices;

                output = RunCommand("--in=" + docPath,
                    "--exp-method-instrument=" + ExportInstrumentType.THERMO_ASCEND,
                    "--exp-template=" + thermoTemplate,
                    "--exp-file=" + thermoOut);
                AssertEx.Contains(output, 
                    string.Format(ModelResources.CommandLine_ExportInstrumentFile_Error__The_specified_instrument_type___0___does_not_match_the_installed_software___1___,
                        ExportInstrumentType.THERMO_ASCEND, testsDllFinder.First().ExpectedInstrumentType),
                    ModelResources.CommandLine_ExportInstrumentFile_Use_the_instrument_type__Thermo__to_export_a_method_with_the_installed_software_);

                // Valid installation but not to BuildThermoMethod.exe
                output = RunCommand("--in=" + docPath,
                    "--exp-method-instrument=Thermo",
                    "--exp-template=" + thermoTemplate,
                    "--exp-file=" + thermoOut);
                // This means that BuildThermoMethod was actually run and failed when the Thermo API
                // tried to find the OrbitrapAstral that the test case said exists
                AssertEx.Contains(output, "Registry key (Software\\Thermo Instruments\\TNG\\OrbitrapAstral) not found. OrbitrapAstral is not installed on this machine.",
                    "Command-line: Method\\Thermo\\BuildThermoMethod -t OrbitrapAstral");
            }
            finally
            {
                ThermoDllFinder.DEFAULT_SERVICES = dllFinderServices;
            }
        }
    }
}