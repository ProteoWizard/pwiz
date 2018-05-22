using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class UnifiTest : AbstractUnitTest
    {
        private static UnifiAccount GetAccount()
        {
            return UnifiTestUtil.GetTestAccount();
        }
        [TestMethod]
        public void TestUnifiAuthenticate()
        {
            if (!UnifiTestUtil.EnableUnifiTests)
            {
                return;
            }
            var account = GetAccount();
            var tokenResponse = account.Authenticate();
            Assert.IsFalse(tokenResponse.IsError);
            Assert.IsNotNull(tokenResponse.AccessToken);
        }

        [TestMethod]
        public void TestUnifiGetFolders()
        {
            if (!UnifiTestUtil.EnableUnifiTests)
            {
                return;
            }

            var account = GetAccount();
            var httpClient = AuthenticateHttpClient(account);
            var response = httpClient.GetAsync(account.GetFoldersUrl()).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            Assert.IsNotNull(responseBody);
            var jsonObject = JObject.Parse(responseBody);
            
            var foldersValue = jsonObject["value"] as JArray;
            Assert.IsNotNull(foldersValue);
            var folders = foldersValue.OfType<JObject>().Select(f => new UnifiFolderObject(f)).ToArray();
            Assert.AreNotEqual(0, folders.Length);
        }

        [TestMethod]
        public void TestUnifiGetFiles()
        {
            if (!UnifiTestUtil.EnableUnifiTests)
            {
                return;
            }

            var account = GetAccount();
            var folders = account.GetFolders().ToArray();
            Assert.AreNotEqual(0, folders.Length);
            var files = account.GetFiles(folders[0]).ToArray();
            Assert.AreNotEqual(0, files.Length);
        }

        private HttpClient AuthenticateHttpClient(UnifiAccount account)
        {
            var tokenResponse = account.Authenticate();
            Assert.IsFalse(tokenResponse.IsError);
            Assert.IsNotNull(tokenResponse.AccessToken);
            var httpClient = new HttpClient();
            httpClient.SetBearerToken(tokenResponse.AccessToken);
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json;odata.metadata=minimal");
            return httpClient;
        }

        [TestMethod]
        public void TestRemoteAccountList()
        {
            var unifiAccount = new UnifiAccount("https://unifiserver.xxx", "unifi_username", "unifi_password");
            var chorusAccount = new ChorusAccount("https://chorusserver.xxx", "chorus_username", "chorus_password");
            var remoteAccountList = new RemoteAccountList();
            remoteAccountList.Add(unifiAccount);
            remoteAccountList.Add(chorusAccount);
            StringWriter stringWriter = new StringWriter();
            var xmlSerializer = new XmlSerializer(typeof(RemoteAccountList));
            xmlSerializer.Serialize(stringWriter, remoteAccountList);
            var serializedAccountList = stringWriter.ToString();
            Assert.AreEqual(-1, serializedAccountList.IndexOf(unifiAccount.Password, StringComparison.Ordinal));
            Assert.AreEqual(-1, serializedAccountList.IndexOf(chorusAccount.Password, StringComparison.Ordinal));
            var roundTrip = (RemoteAccountList) xmlSerializer.Deserialize(new StringReader(serializedAccountList));
            Assert.AreEqual(remoteAccountList.Count, roundTrip.Count);
            Assert.AreEqual(unifiAccount, roundTrip[0]);
            Assert.AreEqual(chorusAccount, roundTrip[1]);
        }
    }
}
