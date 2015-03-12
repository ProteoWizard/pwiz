//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2015 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestStack.White;

namespace Test
{
    public class BaseInteractionTest
    {
        public TestContext TestContext { get; set; }
        public bool CloseAppOnError { get { return false; } }

        public Application Application { get { return TestContext.Properties["Application"] as Application; } set { TestContext.Properties["Application"] = value; } }
        public string TestOutputSubdirectory { get { return (string)TestContext.Properties["TestOutputSubdirectory"]; } set { TestContext.Properties["TestOutputSubdirectory"] = value; } }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            testContext.Properties["Application"] = null;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            try
            {
                //Application.Attach("IDPicker").Close();
            }
            catch (WhiteException e)
            {
                if (!e.Message.Contains("Could not find process"))
                    throw e;
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            TestOutputSubdirectory = TestContext.TestName;
            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
        }
    }
}