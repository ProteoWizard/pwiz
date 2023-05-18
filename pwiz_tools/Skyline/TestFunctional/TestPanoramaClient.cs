/*
 * Original author: Sophie Pallanck <srpall .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TestPanoramaClient : AbstractFunctionalTest
    {
        private const string VALID_USER_NAME = "skyline_tester@proteinms.net";
        private const string VALID_PASSWORD = "lclcmsms";
        private const string VALID_SERVER = "https://panoramaweb.org";

        private const string NO_WRITE_NO_TARGETED = "No write permissions/TargetedMS is not active";
        private const string NO_WRITE_TARGETED = "Does not have write permission/TargetedMS active";
        private const string WRITE_TARGETED = "Write permissions/TargetedMS active";
        private const string WRITE_NO_TARGETED = "Write permissions/TargetedMS is not active";

        [TestMethod]
        public void TestPanorama()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            //Test navigation buttons
            TestNavigationButtons();

            //Test with no user permissions
            TestPermissions();

            //Test TargetedMS folder with no Skyline documents
            //Test with no targetedMS folders

            //Test empty JSON
            
            //Test versions: selected option is correct for all versions vs current version
            TestVersions();

            //Test checkbox switching

            //Test cancel and close
            TestCancel();

            
        }

        //Comment testing methods
        //Do not assume we're connected to server, initialize remote dialog with JSON 
        public void TestNavigationButtons()
        {
            var testJson = CreateFile();
            var json = GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                NO_WRITE_NO_TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => 
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, json, testJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(WRITE_NO_TARGETED);
                remoteDlg.FolderBrowser.SelectNode(WRITE_TARGETED);
                remoteDlg.FolderBrowser.SelectNode(NO_WRITE_NO_TARGETED);
                Assert.IsTrue(remoteDlg.BackEnabled());
                remoteDlg.ClickBack();
                Assert.AreEqual(WRITE_TARGETED, remoteDlg.FolderBrowser.Clicked.Text);
                remoteDlg.ClickBack();
                Assert.IsTrue(remoteDlg.ForwardEnabled());
                Assert.AreEqual(WRITE_NO_TARGETED, remoteDlg.FolderBrowser.Clicked.Text);
                Assert.IsFalse(remoteDlg.BackEnabled());
                remoteDlg.ClickForward();
                Assert.AreEqual(WRITE_TARGETED, remoteDlg.FolderBrowser.Clicked.Text);
                remoteDlg.ClickUp();
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }

        private void TestVersions()
        {
            var testJson = CreateFile();
            var rows = testJson[@"rows"];

            var json = GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                NO_WRITE_NO_TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, json, testJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(WRITE_TARGETED);
                Assert.IsTrue(remoteDlg.VersionsVisible());
                Assert.AreEqual(4, remoteDlg.FileNumber());
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);

        }

        /// <summary>
        /// Tests loading a treeView with no read permissions
        /// </summary>
        private void TestPermissions()
        {
            var testFolders = new JObject();
            testFolders = CreateFolder(WRITE_TARGETED, true, true);
            testFolders["children"] = new JArray(CreatePrivateFolder("Private"));
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, testFolders));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(string.Concat(VALID_SERVER, "/"));
                Assert.AreEqual(1, remoteDlg.FolderBrowser.NodeCount);
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }

        private void TestCancel()
        {
            var testJson = CreateFile();
            var json = GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                NO_WRITE_NO_TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, json, testJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(WRITE_NO_TARGETED);
                remoteDlg.TestCancel();
                Assert.IsTrue(remoteDlg.FormHasClosed);
            });
            WaitForClosedForm(remoteDlg);
        }

        //"Name", @"Deleted", @"Container/Path", @"File/Proteins", @"File/Peptides", @"File/Precursors", @"File/Transitions", @"File/Replicates", @"Created", @"File/Versions", @"Replaced", @"ReplacedByRun" , @"ReplacesRun", @"File/Id", @"RowId"
        private JToken CreateFile()
        {
            var root = new JObject();
            root["Name"] = "Name";
            root["rowCount"] = 2;
            root["rows"] = new JArray(CreateObj("1", ("/" + WRITE_TARGETED + "/"), false, null, 2, 1, 1, 4 ), CreateObj("2", ("/" + WRITE_TARGETED + "/"), true, 1, 3, 2, 2, 4), CreateObj("3", ("/" + WRITE_TARGETED + "/"), true, 2, 4, 3, 3, 4), CreateObj("4", ("/" + WRITE_TARGETED + "/"), true, 3, null, 4, 4, 4));
            return root;
        }

        private JToken CreateObj(string name, string path, bool replaced, int? replacedBy, int? replaces, int id, int rowId, int versions)
        {
            var obj = new JObject();
            obj["Container/Path"] = path;
            obj["Name"] = name;
            obj["File/Proteins"] = 13;
            obj["File/Peptides"] = 13;
            obj["File/Replicates"] = 13;
            obj["File/Precursors"] = 13;
            obj["File/Transitions"] = 13;
            obj["Replaced"] = replaced;
            obj["ReplacedByRun"] = replacedBy;
            obj["ReplacesRun"] = replaces;
            obj["File/Id"] = id;
            obj["RowId"] = rowId;
            obj["Created"] = "Test";
            obj["File/Verions"] = versions;
            return obj;
        }

        private JObject CreatePrivateFolder(string name)
        {
            JObject obj = new JObject();
            obj["name"] = name;
            obj["path"] = "/" + name + "/";
            obj["userPermissions"] = 0;
            obj["children"] = new JArray();
            obj["folderType"] = "Targeted MS";
            obj["activeModules"] = new JArray("MS0", "MS1", "MS3");

            return obj;
        }

        private JObject CreateFolder(string name, bool write, bool targeted)
        {
            JObject obj = new JObject();
            obj["name"] = name;
            obj["path"] = "/" + name + "/";
            obj["userPermissions"] = write ? 3 : 1;
            if (!write || !targeted)
            {
                // Create a writable subfolder if this folder is not writable, i.e. it is
                // not a targetedMS folder or the user does not have write permissions in this folder.
                // Otherwise, it will not get added to the folder tree (PublishDocumentDlg.AddChildContainers()).
                obj["children"] = new JArray(CreateFolder("Subfolder", true, true));
            }
            else
            {
                obj["children"] = new JArray();
            }
            obj["folderType"] = targeted ? "Targeted MS" : "Collaboration";
            obj["activeModules"] = targeted
                ? new JArray("MS0", "MS1", "TargetedMS", "MS3")
                : new JArray("MS0", "MS1", "MS3");

            return obj;
        }

        //Only generating 3 nodes in the tree
        public JToken GetInfoForFolders(PanoramaServer server, string folder)
        {
            JObject testFolders = new JObject();
            testFolders = CreateFolder(WRITE_TARGETED, true, true);
            testFolders["children"] = new JArray(
                CreateFolder(NO_WRITE_NO_TARGETED, false, false),
                CreateFolder(NO_WRITE_TARGETED, false, true),
                CreateFolder(WRITE_TARGETED, true, true),
                //CreateFolder("Test", false, false),
                CreateFolder(WRITE_NO_TARGETED, true, false));
            return testFolders;
        }

    }
}
