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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using static pwiz.Skyline.Model.AuditLog.Databinding.AuditLogColumn;

namespace pwiz.SkylineTestConnected.Results.RemoteApi
{
    [TestClass]
    public class RemoteApiTest : AbstractUnitTest
    {
        private static RemoteAccount GetUnifiAccount(bool assumeEnabled = false)
        {
            if (!assumeEnabled && !UnifiTestUtil.EnableUnifiTests)
                return null;
            return UnifiTestUtil.GetTestAccount();
        }

        private static RemoteAccount GetWatersConnectAccount(bool assumeEnabled = false)
        {
            if (!assumeEnabled && !WatersConnectTestUtil.EnableWatersConnectTests)
                return null;
            return WatersConnectTestUtil.GetTestAccount();
        }

        private void TestRemoteSessionListContents(RemoteAccount remoteAccount)
        {

            using var session = RemoteSession.CreateSession(remoteAccount);
            var contents = new List<RemoteItem>();
            var rootUrl = remoteAccount.GetRootUrl();

            session.ContentsAvailable += () =>
            {
                contents.AddRange(session.ListContents(rootUrl));
                lock (session) Monitor.Pulse(session);
            };

            RemoteServerException exception = null;
            Helpers.Try<Exception>(() =>
            {
                while (!session.AsyncFetchContents(rootUrl, out exception) && exception == null)
                {
                }
            }, 5, 1000);

            if (exception != null)
                throw exception;

            lock (session) Monitor.Wait(session);
            Assert.AreNotEqual(0, contents.Count);
        }

        [TestMethod]
        public void TestUnifiListContents()
        {
            TestRemoteSessionListContents(GetUnifiAccount());
        }

        [TestMethod]
        public void TestWatersConnectListContents()
        {
            TestRemoteSessionListContents(GetWatersConnectAccount());
        }

        private void TestRemoteAccountSerialize(RemoteAccount remoteAccount)
        {
            var remoteAccountList = new RemoteAccountList();
            remoteAccountList.Add(remoteAccount);
            StringWriter stringWriter = new StringWriter();
            var xmlSerializer = new XmlSerializer(typeof(RemoteAccountList));
            xmlSerializer.Serialize(stringWriter, remoteAccountList);
            var serializedAccountList = stringWriter.ToString();
            Assert.AreEqual(-1, serializedAccountList.IndexOf(remoteAccount.Password, StringComparison.Ordinal));
            var roundTrip = (RemoteAccountList) xmlSerializer.Deserialize(new StringReader(serializedAccountList));
            Assert.AreEqual(remoteAccountList.Count, roundTrip.Count);
            Assert.AreEqual(remoteAccount, roundTrip[0]);
        }

        [TestMethod]
        public void TestUnifiAccountSerialize()
        {
            TestRemoteAccountSerialize(GetUnifiAccount(true));
        }

        [TestMethod]
        public void TestWatersConnectAccountSerialize()
        {
            TestRemoteAccountSerialize(GetWatersConnectAccount(true));
        }
    }
}
