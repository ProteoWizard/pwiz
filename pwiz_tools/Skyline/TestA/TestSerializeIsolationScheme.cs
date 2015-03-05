/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class TestSerializeIsolationScheme : AbstractUnitTest
    {
        /// <summary>
        /// Test error handling in XML deserialization of <see cref="IsolationScheme"/>.
        /// </summary>
        [TestMethod]
        public void SerializeIsolationSchemeTest()
        {
            var isolationScheme = new IsolationScheme("object test", 1.0, 2.0);

            // Test IsolationScheme.Validate method through ChangeProp
            isolationScheme = (IsolationScheme) isolationScheme.ChangeName("test2");

            // Test IsolationScheme object methods
            isolationScheme.Equals(null);
            isolationScheme.Equals(isolationScheme);
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
            isolationScheme.Equals((object)null);
            isolationScheme.Equals((object)isolationScheme);
            isolationScheme.GetHashCode();
// ReSharper restore ReturnValueOfPureMethodIsNotUsed

            // Test IsolationWindow object methods
            var isolationWindow = new IsolationWindow(100.0, 150.0, 125.0, 1.0, 1.0);
            isolationWindow.Equals(null);
            isolationWindow.Equals(isolationWindow);
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
            isolationWindow.Equals((object)null);
            isolationWindow.Equals((object)isolationWindow);
// ReSharper disable SuspiciousTypeConversion.Global
            isolationWindow.Equals(2);
// ReSharper restore SuspiciousTypeConversion.Global
            isolationWindow.GetHashCode();
            isolationWindow.GetSchema();
// ReSharper restore ReturnValueOfPureMethodIsNotUsed

            // Test round trip serialization
            AssertEx.Serialization<IsolationSchemeList>(ISOLATION_SCHEME_LIST, CheckSettingsList, false); // Not part of a Skyline document, don't check against schema

            // Valid first
            AssertEx.DeserializeNoError<IsolationScheme>(
                @"<isolation_scheme name=""Validate (1)"" precursor_filter=""1""/>");
            AssertEx.DeserializeNoError<IsolationScheme>(
                @"<isolation_scheme name=""Validate (2)"" precursor_left_filter=""0.5"" precursor_right_filter=""0.5""/>");
            AssertEx.DeserializeNoError<IsolationScheme>(
                @"<isolation_scheme name=""Validate (3)"" special_handling=""Multiplexed"" windows_per_scan=""2"">
                    <isolation_window start=""100"" end=""110""/><isolation_window start=""110"" end=""130""/></isolation_scheme>");
            AssertEx.DeserializeNoError<IsolationScheme>(
                @"<isolation_scheme name=""Validate (4)"" special_handling=""MSe""/>");

            // Missing parameters
            AssertEx.DeserializeError<IsolationScheme>(@"<isolation_scheme/>");
            // No name
            AssertEx.DeserializeError<IsolationScheme>(@"<isolation_scheme precursor_filter=""1""/>");
            // Filter and prespecified window
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (1)"" precursor_filter=""1""><isolation_window start=""1"" end=""10""/></isolation_scheme>");
            // Filter and special handling
            //AssertEx.DeserializeError<IsolationScheme>(
            //    @"<isolation_scheme name=""Invalid (2)"" precursor_filter=""1"" special_handling=""MSe""/>");
            // Filter and windows per scan
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (3)"" precursor_filter=""1"" windows_per_scan=""3""/>");
            // Special handling but no windows
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (4)"" special_handling=""Multiplexed""/>");
            // Right filter only
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (5)"" precursor_right_filter=""1""/>");
            // Windows per scan with no special handling
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (6)"" windows_per_scan=""2""><isolation_window start=""1"" end=""10""/></isolation_scheme>");
            // Windows per scan with MSe
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (7)"" windows_per_scan=""2"" special_handling=""MSe"" />");
            // Multiplexed and no windows per scan
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (8)"" special_handling=""Multiplexed""><isolation_window start=""1"" end=""10""/></isolation_scheme>");
            // Multiplexed and invalid windows per scan
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (9)"" windows_per_scan=""0"" special_handling=""Multiplexed""><isolation_window start=""1"" end=""10""/></isolation_scheme>");
            // Invalid special handling
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (10)"" special_handling=""invalid option""/>");

            // Bad window: start > end
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (10)""><isolation_window start=""10"" end=""1""/></isolation_scheme>");
            // Bad window: target not between start and end
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (11)""><isolation_window start=""1"" end=""10"" target=""20""/></isolation_scheme>");
            // Bad window: start margin < 0
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (12)""><isolation_window start=""1"" end=""10"" margin_left=""-1"" margin_right=""2""/></isolation_scheme>");
            // MSe with window
            AssertEx.DeserializeError<IsolationScheme>(
                @"<isolation_scheme name=""Invalid (14)"" special_handling=""MSe""><isolation_window start=""1"" end=""10""/></isolation_scheme>");

        }

        private const string ISOLATION_SCHEME_LIST =
            @"
            <IsolationSchemeList>
                <isolation_scheme name=""SYMMETRIC_FILTER"" precursor_filter=""1""/>
                <isolation_scheme name=""ASYMMETRIC_FILTER"" precursor_left_filter=""1"" precursor_right_filter=""2""/>
                <isolation_scheme name=""SPECIAL_HANDLING_NONE"">
                    <isolation_window start=""100"" end=""150"" target=""125"" margin_left=""1"" margin_right=""2""/>
                </isolation_scheme>
                <isolation_scheme name=""SPECIAL_HANDLING_MULTIPLEXED"" special_handling=""Multiplexed"" windows_per_scan=""2"">
                    <isolation_window start=""100"" end=""150"" target=""125"" margin=""1""/>
                    <isolation_window start=""150"" end=""200"" target=""175"" margin=""1""/>
                </isolation_scheme>
                <isolation_scheme name=""SPECIAL_HANDLING_MS_E"" special_handling=""MSe"" />
            </IsolationSchemeList>";

        private static void CheckSettingsList<TItem>(SettingsList<TItem> target, SettingsList<TItem> copy)
            where TItem : IKeyContainer<string>, IXmlSerializable
        {
            Assert.AreEqual(target.Count, copy.Count);
            for (int i = 0; i < target.Count; i++)
                AssertEx.Cloned(target[i], copy[i]);
        }
    }
}
