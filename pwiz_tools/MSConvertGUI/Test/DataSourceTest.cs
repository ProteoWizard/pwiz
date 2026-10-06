/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

namespace MSConvertGUI.Tests;

[TestClass]
public class DataSourceTest
{
    /// <summary>A URL given with username:password converts with them and is shown without them;
    /// anything else is not a <see cref="CredentialUrl"/>.</summary>
    [TestMethod]
    public void CredentialUrl_ShownWithoutCredentials()
    {
        foreach (var (input, shown) in new[]
                 {
                     ("https://user:secret@unifi.example.com:50034/unifi/v1/sampleresults(1)", "https://unifi.example.com:50034/unifi/v1/sampleresults(1)"),
                     ("HTTP://user:@host/path", "HTTP://host/path"),
                     ("https://host/unifi/v1/sampleresults(1)", null),
                     ("https://host/path?filter=a:b@c", null),
                     (@"C:\data\run1.raw", null),
                 })
        {
            var url = CredentialUrl.TryCreate(input);
            Assert.AreEqual(shown, url?.ToString(), input);
            if (url != null)
                Assert.AreEqual(input, url.Url, input);
        }
    }

    /// <summary>Only an SDK build's output folder, bin\Debug|Release\&lt;framework&gt;, counts as a
    /// development build; an installed or copied app does not.</summary>
    [TestMethod]
    public void IsDevBuildDirectory_OnlyBuildOutput()
    {
        foreach (var (exePath, expected) in new[]
                 {
                     (@"C:\dev\pwiz\pwiz_tools\MSConvertGUI\bin\Release\net10.0-windows\MSConvertGUI-sharp.exe", true),
                     (@"C:\dev\pwiz\pwiz_tools\MSConvertGUI\bin\Debug\net10.0-windows\MSConvertGUI-sharp.exe", true),
                     (@"C:\Program Files\ProteoWizard\MSConvertGUI-sharp.exe", false),
                     (@"C:\tools\bin\MSConvertGUI-sharp.exe", false),
                     (@"C:\dev\pwiz\scripts\installer\build\stage\MSConvertGUI-sharp.exe", false),
                 })
            Assert.AreEqual(expected, RemoteAccountDetailForm.IsDevBuildDirectory(exePath), exePath);
    }
}
