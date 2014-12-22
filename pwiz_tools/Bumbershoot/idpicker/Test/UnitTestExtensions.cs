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
// Copyright 2014 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestStack.White.UIItems;
using TestStack.White.UIItems.Custom;
using TestStack.White.UIItems.WindowItems;
using TestStack.White.UIItems.Actions;

namespace Test
{
    using AppRunner = Action<TestStack.White.Application, Stack<Window>>;
    using System.Diagnostics;
    using TestStack.White;

    public static class UnitTestExtensions
    {
        public static void AssertSequenceEquals<T>(this IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var expectedList = expected.ToList();
            var actualList = actual.ToList();
            Assert.AreEqual(expectedList.Count, actualList.Count, "Sequences have different lengths.");
            for (int i = 0; i < expectedList.Count; ++i)
                Assert.AreEqual(expectedList[i], actualList[i], "Sequence elements at index " + i.ToString() + " are not equal.");
        }

        public static IEnumerable<CustomUIItem> GetDockableForms(this Window window)
        {
            var dockPanes = window.AutomationElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "dockPanel"))
                                                    .FindFirst(TreeScope.Children, Condition.TrueCondition)
                                                    .FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane)).OfType<AutomationElement>();
            var floatingWindows = window.AutomationElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window)).OfType<AutomationElement>();
            dockPanes = dockPanes.Union(floatingWindows.Select(o => o.FindFirst(TreeScope.Children, Condition.TrueCondition)));
            return dockPanes.SelectMany(o => o.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane))
                            .OfType<AutomationElement>())
                            .Where(o => !Char.IsDigit(o.Current.AutomationId[0]))
                            .Select(o => new CustomUIItem(o, new NullActionListener()));
        }

        public static string TestDataPath(this TestContext context)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..\\..\\TestData"));
        }

        public static string TestDataFilePath(this TestContext context, string filename)
        {
            return Path.Combine(context.TestDataPath(), filename);
        }

        public static string TestOutputSubdirectory(this TestContext context)
        {
            return context.Properties.Contains("TestOutputSubdirectory") ? (string) context.Properties["TestOutputSubdirectory"] : context.TestName;
        }

        public static void SetTestOutputSubdirectory(this TestContext context, string path)
        {
            context.Properties["TestOutputSubdirectory"] = path;
        }

        public static string TestOutputPath(this TestContext context)
        {
            return Path.Combine(context.ResultsDirectory, context.TestOutputSubdirectory());
        }

        public static string TestOutputPath(this TestContext context, string filemask)
        {
            return Path.Combine(context.ResultsDirectory, context.TestOutputSubdirectory(), filemask);
        }

        public static string QuotePathWithSpaces(this string path)
        {
            if (path.Contains(' '))
                return String.Format("\"{0}\"", path);
            return path;
        }

        public static void CopyTestInputFiles(this TestContext context, params string[] filemasks)
        {
            Directory.CreateDirectory(context.TestOutputPath());
            foreach (var filemask in filemasks)
                foreach (var file in Directory.GetFiles(context.TestDataPath(), filemask, SearchOption.TopDirectoryOnly))
                    File.Copy(file, context.TestOutputPath(Path.GetFileName(file)));
        }

        public static void LaunchAppTest(this TestContext context, string exePath, string args, AppRunner testAction, bool closeAppOnError = true)
        {
            if (testAction == null)
                return;

            var startInfo = new ProcessStartInfo(exePath, args);
            var app = Application.Launch(startInfo);
            context.Properties["Application"] = app;
            var windowStack = new Stack<Window>();

            try
            {
                testAction(app, windowStack);

                if (!closeAppOnError)
                    app.Kill();
            }
            catch (Exception e)
            {
                if (closeAppOnError)
                    while (windowStack.Count > 0)
                    {
                        var window = windowStack.Pop();
                        if (!window.IsClosed)
                            window.Close();
                    }

                throw new AssertFailedException("UI test failed:\r\n" + e.ToString(), e);
            }
            finally
            {
                if (closeAppOnError)
                    app.Kill();
            }
        }
    }
}
