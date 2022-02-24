/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Outputs the size of fields and classes to the console.
    /// These tests use TestContext.WriteLine, which is not implemented by the TestContext
    /// supplied by TestRunner. Because of this, these tests need to be run inside of VisualStudio
    /// in order to see the output.
    /// </summary>
    [TestClass]
    public class ObjectSizeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestChromKeySize()
        {
            DumpTypeAndFields(typeof(ChromKey));
        }

        [TestMethod]
        public void TestChromDataSize()
        {
            // have to use reflection since class "ChromKey" is not public
            var chromDataType = typeof(ChromKey).Assembly.GetType("pwiz.Skyline.Model.Results.ChromData");

            DumpTypeAndFields(chromDataType);
        }

        [TestMethod]
        public void TestTransitionChromInfoSize()
        {
            DumpTypeAndFields(typeof(TransitionChromInfo));
        }
        [TestMethod]
        public void TestTransitionGroupChromInfoSize()
        {
            DumpTypeAndFields(typeof(TransitionGroupChromInfo));
        }

        private void DumpTypeAndFields(Type type)
        {
            StringWriter stringWriter = new StringWriter();
            TypeInspector.DumpTypeAndFields(type, stringWriter);
            try
            {
                TestContext.WriteLine("{0}", stringWriter);
            }
            catch (NotImplementedException)
            {
                // TestRunner TestContext does not implement WriteLine.
                // Do not output anything in that case
            }
        }
    }
}
