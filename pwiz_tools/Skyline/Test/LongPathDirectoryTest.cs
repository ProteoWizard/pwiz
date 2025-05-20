/*
 * Original author: David Shteynberg <dshteyn .at. proteinms.net>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class LongPathDirectoryTest : AbstractUnitTest
    {
        [TestMethod]
        //Simple test that creates a long path directory (one with more than 256 characters) then deletes it using DirectoryEx.SafeDeleteLongPath
        public void DirectoryWithLongPathTest()
        {
            string inputPath = Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), @"LongPath");
            
            //Make the directory path longer than 256 characters
            for (int i = 0; i < 32; i++)
            {
                inputPath = Path.Combine(inputPath, @"LongPath");
            }

            Assert.IsTrue(inputPath.Length > 256);

            if (!PythonInstaller.ValidateEnableLongpaths())
                Assert.Fail($@"Error: Cannot finish LongPathDirectoryTest because {PythonInstaller.REG_FILESYSTEM_KEY}\{PythonInstaller.REG_LONGPATHS_ENABLED} is not set and have insufficient permissions to set it");

            Directory.CreateDirectory(inputPath);
            Assert.IsTrue(Directory.Exists(inputPath));

            DirectoryEx.SafeDeleteLongPath(inputPath);
            Assert.IsFalse(Directory.Exists(inputPath));
        }
    }
}