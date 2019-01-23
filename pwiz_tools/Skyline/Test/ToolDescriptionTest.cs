/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Tools;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    // ReSharper disable UnusedMember.Local
    [TestClass]
    public class ToolDescriptionTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestFindArgsCollectorMethod()
        {
            var toolDescription = new ToolDescription("ToolTitle", string.Empty, string.Empty);
            Assert.IsNotNull(toolDescription.FindArgsCollectorMethod(typeof(DifferentArguments)));
            AssertEx.ThrowsException<ToolExecutionException>(() =>
                toolDescription.FindArgsCollectorMethod(typeof(NoMethod)));
            AssertEx.ThrowsException<ToolExecutionException>(() =>
                toolDescription.FindArgsCollectorMethod(typeof(AmbiguousMethod)));
            Assert.IsNotNull(toolDescription.FindArgsCollectorMethod(typeof(ReportAsString)));
            var reportAsStringOrReader = toolDescription.FindArgsCollectorMethod(typeof(ReportAsStringOrReader));
            Assert.IsNotNull(reportAsStringOrReader);
            CollectionAssert.AreEqual(new[]{typeof(IWin32Window), typeof(TextReader), typeof(string[])}, 
                reportAsStringOrReader.GetParameters().Select(p=>p.ParameterType).ToArray());
            Assert.AreEqual("CollectArgsReader", toolDescription.FindArgsCollectorMethod(typeof(WithCollectArgsReader)).Name);
        }

        // No such method: should throw exception
        class NoMethod
        {

        }

        class DifferentArguments
        {
            // "parent" is usually an IWin32Window, but this method should be found anyway.
            public static string[] CollectArgs(Control parent, string report, IList<string> args)
            {
                return args == null ? new string[0] : args.ToArray();
            }
        }

        // This class has ambiguous 
        class AmbiguousMethod
        {
            public static string[] CollectArgs(Control parent, string report, IList<string> args)
            {
                return args == null ? new string[0] : args.ToArray();
            }
            public static string[] CollectArgs(Control parent, TextReader report, IList<string> args)
            {
                return args == null ? new string[0] : args.ToArray();
            }
        }
        
        class ReportAsString
        {
            // This is the classic set of arguments from 4.1 and before
            public static string[] CollectArgs(IWin32Window parent, string report, string[] args)
            {
                return args ?? new string[0];
            }
        }

        // This class has two different overloads of "CollectArgs". Skyline 4.1 will 
        // choke on the ambiguity, but current versions of Skyline prefer the TextReader overload
        class ReportAsStringOrReader
        {
            public static string[] CollectArgs(IWin32Window parent, string report, string[] args)
            {
                return CollectArgs(parent, new StringReader(report), args);
            }

            public static string[] CollectArgs(IWin32Window parent, TextReader report, string[] args)
            {
                return args ?? new string[0];
            }
        }

        class WithCollectArgsReader
        {
            public static string[] CollectArgs(IWin32Window parent, string report, string[] args)
            {
                return CollectArgsReader(parent, new StringReader(report), args);
            }

            // Skyline is supposed to find this method.
            public static string[] CollectArgsReader(IWin32Window parent, TextReader report, string[] args)
            {
                return args ?? new string[0];
            }
        }
    }
}
