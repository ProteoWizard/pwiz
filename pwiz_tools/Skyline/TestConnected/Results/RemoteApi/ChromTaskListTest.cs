/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected.Results.RemoteApi
{
    /// <summary>
    /// Summary description for ChromTaskListTest
    /// </summary>
    [TestClass]
    public class ChromTaskListTest : AbstractUnitTest
    {
        //[TestMethod]
        public void TestDdaSmall()
        {
            ChorusAccount TEST_ACCOUNT = new ChorusAccount("https://chorusproject.org", "pavel.kaplin@gmail.com", "pwd");
            var stream = typeof (ChromTaskListTest).Assembly.GetManifestResourceStream(typeof (ChromTaskListTest),
                "DdaSmall.ChorusRequest.xml");
            Assert.IsNotNull(stream);
            var chromatogramRequest = (ChromatogramRequestDocument) new XmlSerializer(typeof (ChromatogramRequestDocument)).Deserialize(stream);
            var chromTaskList = new ChromTaskList(() => { }, new SrmDocument(SrmSettingsList.GetDefault()), TEST_ACCOUNT,
                TEST_ACCOUNT.GetChorusUrl().SetFileId(28836), ChromTaskList.ChunkChromatogramRequest(chromatogramRequest, 100));
            chromTaskList.SetMinimumSimultaneousTasks(10);
            var failedTasks = new HashSet<ChromatogramGeneratorTask>();
            foreach (var chromKey in chromTaskList.ChromKeys)
            {
                TimeIntensities timeIntensities;
                chromTaskList.GetChromatogram(chromKey, out timeIntensities);
                if (null == timeIntensities)
                {
                    var task = chromTaskList.GetGeneratorTask(chromKey);
                    if (failedTasks.Add(task))
                    {
                        var memoryStream = new MemoryStream();
                        var xmlWriter = XmlWriter.Create(memoryStream, new XmlWriterSettings {Encoding = Encoding.UTF8});
                        new XmlSerializer(typeof(ChromatogramRequestDocument)).Serialize(xmlWriter, task.ChromatogramRequestDocument);
                        Console.Out.WriteLine("Failed to get data for {0}", Encoding.UTF8.GetString(memoryStream.ToArray()));
                        
                    }
                }
            }
            Assert.AreEqual(0, failedTasks.Count);
        }
    }

}
