/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Tools;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class InstallNvidiaTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestWriteUninstallNvidiaBat()
        {
            var uninstallBatFile = PythonInstaller.UninstallNvidiaLibrariesBat;
            var directory = Path.GetDirectoryName(uninstallBatFile) ?? string.Empty;
            if (!directory.IsNullOrEmpty())
                Directory.CreateDirectory(directory);
            
            PythonInstaller.WriteUninstallNvidiaBatScript();
            Assert.IsTrue(File.Exists(uninstallBatFile));

            Console.WriteLine($@"Written Nvidia uninstall script: '{uninstallBatFile}'");
        }

        [TestMethod]
        public void TestWriteInstallNvidiaBat()
        {
            var installBatFile = PythonInstaller.InstallNvidiaLibrariesBat;
            var directory = Path.GetDirectoryName(installBatFile) ?? string.Empty;
            if (!directory.IsNullOrEmpty())
                Directory.CreateDirectory(directory);
            
            PythonInstaller.WriteInstallNvidiaBatScript();
            Assert.IsTrue(File.Exists(installBatFile));

            Console.WriteLine($@"Written Nvidia install script: '{installBatFile}'");
        }
    }
}
