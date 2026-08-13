/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;
using System.Diagnostics;
using System.IO;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AiFolderTest : AbstractUnitTest
    {
        /// <summary>
        /// Verifies that if the "/ai" folder exists that it is a git submodule.
        /// </summary>
        [TestMethod]
        public void TestAiFolderIsSubmodule()
        {
            var codeBaseRoot = GetProjectRoot();
            if (codeBaseRoot == null)
            {
                return;
            }

            var aiFolder = Path.Combine(codeBaseRoot, "ai");
            if (Directory.Exists(aiFolder))
            {
                var aiDotGitFolder = Path.Combine(aiFolder, ".git");
                if (!Directory.Exists(aiDotGitFolder))
                {
                    Assert.Fail(".git folder not found inside folder {0}", aiFolder);
                }
            }
        }

        private string GetProjectRoot()
        {
            var thisFile = new StackTrace(true).GetFrame(0).GetFileName();
            if (string.IsNullOrEmpty(thisFile))
            {
                return null;
            }
            // ReSharper disable once PossibleNullReferenceException
            return thisFile.Replace("pwiz_tools\\Skyline\\Test\\AiFolderTest.cs", string.Empty);
        }

    }
}
