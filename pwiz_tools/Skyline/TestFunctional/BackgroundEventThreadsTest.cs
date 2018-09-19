/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class BackgroundEventThreadsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestBackgroundEventThreads()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            BackgroundEventThreads.ShowFormOnBackgroundThread(
                ()=>new BackgroundEventThreadsTestForm());
            var testForm = WaitForOpenForm<BackgroundEventThreadsTestForm>();
            testForm.BeginInvoke(new Action(testForm.Close));
            WaitForClosedForm<BackgroundEventThreadsTestForm>();

            Assert.AreEqual(0, Program.TestExceptions.Count);
            BackgroundEventThreads.ShowFormOnBackgroundThread(() =>
            {
                var form = new BackgroundEventThreadsTestForm();
                form.HandleCreated += (sender, args) => throw new TestException();
                return form;
            });
            WaitForCondition(10000, ()=>Program.TestExceptions.Any(), 
                "Waiting for exception to be thrown", true, false);
            Assert.AreEqual(1, Program.TestExceptions.Count);
            Assert.IsInstanceOfType(Program.TestExceptions.First(), typeof(TestException));
            Program.TestExceptions.Clear();
            testForm = WaitForOpenForm<BackgroundEventThreadsTestForm>();
            Assert.IsNotNull(testForm);
            testForm.BeginInvoke(new Action(testForm.Close));
        }

        class BackgroundEventThreadsTestForm : FormEx
        {
        }

        class TestException : ApplicationException
        {
        }
    }
}
