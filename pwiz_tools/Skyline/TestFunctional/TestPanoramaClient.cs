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
        private const string VALID_SERVER = "https://panoramaweb.org/";

        private const string TARGETED_LIBRARY = "TargetedMS folder/Library module property";
        private const string TARGETED = "TargetedMS folder";
        private const string NO_TARGETED = "Not a TargetedMS folder";
        private const string TARGETED_COLLABORATION = "Collaboration folder/TargetedMS module property";

        [TestMethod]
        public void TestPanorama()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            //Test navigation buttons
            TestNavigationButtons();

            //Test versions: selected option is correct for all versions vs current version
            TestVersions();

            //Test checkbox switching
            TestShowSkyCheckBox();

            //Test TreeView icons
            TestTreeViewIcons();

            //Test enter key - ensure node is expanded
            TestKeyStrokeResponse();

            //Verify JSON is as expected 
            TestVerifyJson();

            //Test which columns are being displayed(versions shouldn't be displayed if there are no versions etc.)
            TestColumns();


        }



        //Identify edge cases for each test
        //Up is disabled at the top?
        //Are buttons enabled-disabled when they should be ?
        public void TestNavigationButtons()
        {
            var testClient = new TestClientJson();
            var fileJson = testClient.CreateFiles();
            var sizeJson = testClient.CreateSizesJson();
            var folderJson = testClient.GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() => 
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, folderJson, fileJson, sizeJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(NO_TARGETED);
                remoteDlg.FolderBrowser.SelectNode(TARGETED);
                remoteDlg.FolderBrowser.SelectNode(TARGETED_LIBRARY);
                Assert.IsTrue(remoteDlg.BackEnabled());
                remoteDlg.ClickBack();
                Assert.AreEqual(TARGETED, remoteDlg.FolderBrowser.Clicked.Text);
                remoteDlg.ClickBack();
                Assert.IsTrue(remoteDlg.ForwardEnabled());
                Assert.AreEqual(NO_TARGETED, remoteDlg.FolderBrowser.Clicked.Text);
                Assert.IsFalse(remoteDlg.BackEnabled());
                remoteDlg.ClickForward();
                Assert.AreEqual(TARGETED, remoteDlg.FolderBrowser.Clicked.Text);
                remoteDlg.ClickUp();
                remoteDlg.FolderBrowser.SelectNode(VALID_SERVER);
                Assert.IsFalse(remoteDlg.UpEnabled());
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }

        private void TestVersions()
        {
            var testClient = new TestClientJson();
            var fileJson = testClient.CreateFiles();
            var sizeJson = testClient.CreateSizesJson();
            var folderJson = testClient.GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, folderJson, fileJson, sizeJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(TARGETED);
                Assert.IsTrue(remoteDlg.VersionsVisible());
                Assert.AreEqual("Most recent", remoteDlg.VersionsOption());
                Assert.AreEqual(1, remoteDlg.FileNumber());
                remoteDlg.ClickVersions();
                Assert.AreEqual(4, remoteDlg.FileNumber());
                Assert.AreEqual("All", remoteDlg.VersionsOption());
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);

        }


        private void TestShowSkyCheckBox()
        {
            var testClient = new TestClientJson();
            var fileJson = testClient.CreateFiles();
            var sizeJson = testClient.CreateSizesJson();
            var json = testClient.GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, json, fileJson, sizeJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                Assert.IsTrue(remoteDlg.CheckBoxVisible());
                Assert.IsTrue(remoteDlg.ShowingSky);
                remoteDlg.FolderBrowser.SelectNode(TARGETED);
                remoteDlg.ClickCheckBox();
                Assert.IsFalse(remoteDlg.ShowingSky);
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }


        /// <summary>
        /// Test TreeView icons
        /// If a folder is not a TargetedMS folder and does not have a TargetedMS module, the folder should have a folder icon which is image index 3
        /// If a folder is a TargetedMS folder and does not have a Library module, the folder should have a flask icon which is image index 1
        /// If a folder is a top level node it should have a Panorama icon which is image index -1
        /// If a folder is a TargetedMS folder and has a Library module, the folder should have a chromatogram icon which is image index 2
        /// If a folder is a Collaboration folder and has a TargetedMS module, the folder should have a folder icon which is image index 3
        /// </summary>
        private void TestTreeViewIcons()
        {
            var testClient = new TestClientJson();
            var fileJson = testClient.CreateFiles();
            var sizeJson = testClient.CreateSizesJson();
            var folderJson = testClient.GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, folderJson, fileJson, sizeJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(NO_TARGETED);
                Assert.AreEqual(3, remoteDlg.FolderBrowser.GetIcon());
                remoteDlg.FolderBrowser.SelectNode(TARGETED);
                Assert.AreEqual(1, remoteDlg.FolderBrowser.GetIcon());
                remoteDlg.FolderBrowser.SelectNode(VALID_SERVER);
                Assert.AreEqual(-1, remoteDlg.FolderBrowser.GetIcon());
                remoteDlg.FolderBrowser.SelectNode(TARGETED_LIBRARY);
                Assert.AreEqual(2, remoteDlg.FolderBrowser.GetIcon());
                remoteDlg.FolderBrowser.SelectNode(TARGETED_COLLABORATION);
                Assert.AreEqual(3, remoteDlg.FolderBrowser.GetIcon());
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }

        /// <summary>
        /// Test enter key - ensure node is expanded
        /// </summary>
        private void TestKeyStrokeResponse()
        {
            var testClient = new TestClientJson();
            var filesJson = testClient.CreateFiles();
            var fileJson = testClient.CreateFile();
            var sizeJson = testClient.CreateSizeJson();
            var sizesJson = testClient.CreateSizesJson();
            var folderJson = testClient.GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, folderJson, filesJson, sizesJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(TARGETED);
                remoteDlg.FolderBrowser.ClickEnter();
                Assert.IsTrue(remoteDlg.FolderBrowser.IsExpanded(TARGETED));
                Assert.IsTrue(remoteDlg.VersionsVisible());
                Assert.AreEqual("Most recent", remoteDlg.VersionsOption());
                Assert.AreEqual(1, remoteDlg.FileNumber());

                remoteDlg.FileJson = fileJson;
                remoteDlg.SizeJson = sizeJson;

                remoteDlg.FolderBrowser.SelectNode(TARGETED_LIBRARY);
                remoteDlg.FolderBrowser.ClickEnter();
                Assert.IsTrue(remoteDlg.FolderBrowser.IsExpanded(TARGETED_LIBRARY));
                Assert.IsFalse(remoteDlg.VersionsVisible());
                Assert.AreEqual("Most recent", remoteDlg.VersionsOption());
                Assert.AreEqual(1, remoteDlg.FileNumber());

                remoteDlg.FileJson = filesJson;
                remoteDlg.SizeJson = sizesJson;

                remoteDlg.FolderBrowser.SelectNode(TARGETED_COLLABORATION);
                Assert.IsTrue(remoteDlg.VersionsVisible());
                Assert.AreEqual("Most recent", remoteDlg.VersionsOption());
                Assert.AreEqual(1, remoteDlg.FileNumber());
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }

        /// <summary>
        /// Test that you are parsing JSON correctly, verify the size that is shown is what the JSON has
        /// </summary>
        private void TestVerifyJson()
        {
            var testClient = new TestClientJson();
            var filesJson = testClient.CreateFiles();
            var sizesJson = testClient.CreateSizesJson();
            var folderJson = testClient.GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, folderJson, filesJson, sizesJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(TARGETED);
                remoteDlg.ClickFile("File1");
                Assert.AreEqual("File1", remoteDlg.GetItemValue(0));
                Assert.AreEqual("3.0 B", remoteDlg.GetItemValue(1));
                Assert.AreEqual(4.ToString(), remoteDlg.GetItemValue(2));
                Assert.AreEqual(string.Empty, remoteDlg.GetItemValue(3));
                Assert.AreEqual("5/11/2023 12:00:00 AM", remoteDlg.GetItemValue(4));

                remoteDlg.ClickVersions();

                remoteDlg.ClickFile("File2");
                Assert.AreEqual("File2", remoteDlg.GetItemValue(0));
                Assert.AreEqual("200.0 B", remoteDlg.GetItemValue(1));
                Assert.AreEqual(4.ToString(), remoteDlg.GetItemValue(2));
                Assert.AreEqual("File1", remoteDlg.GetItemValue(3));
                Assert.AreEqual("5/11/2023 12:00:00 AM", remoteDlg.GetItemValue(4));

                remoteDlg.ClickFile("File3");
                Assert.AreEqual("File3", remoteDlg.GetItemValue(0));
                Assert.AreEqual("5.9 KB", remoteDlg.GetItemValue(1));
                Assert.AreEqual(4.ToString(), remoteDlg.GetItemValue(2));
                Assert.AreEqual("File2", remoteDlg.GetItemValue(3));
                Assert.AreEqual("5/11/2023 12:00:00 AM", remoteDlg.GetItemValue(4));

                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }

        /// <summary>
        /// Tests which columns are being displayed 
        /// Column 2 is the Versions column that lists the number of versions there are for each file
        /// Column 3 is the Replaced By column that shows which run replaced each file
        /// Column 2 should only be shown if there are multiple versions of any file in a given folder
        /// Column 3 should only be shown if there are multiple versions of any file in a given folder AND the user is viewing 'All versions'
        /// </summary>
        private void TestColumns()
        {
            var testClient = new TestClientJson();
            var filesJson = testClient.CreateFiles();
            var fileJson = testClient.CreateFile();
            var sizeJson = testClient.CreateSizeJson();
            var sizesJson = testClient.CreateSizesJson();
            var folderJson = testClient.GetInfoForFolders(new PanoramaServer(new Uri(VALID_SERVER), VALID_USER_NAME, VALID_PASSWORD),
                TARGETED);
            var remoteDlg = ShowDialog<PanoramaFilePicker>(() =>
                SkylineWindow.OpenFromPanorama(VALID_SERVER, string.Empty, string.Empty, folderJson, fileJson, sizeJson));

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode(TARGETED_LIBRARY);
                Assert.AreEqual(1, remoteDlg.FileNumber());
                Assert.IsFalse(remoteDlg.ColumnVisible(2));
                Assert.IsFalse(remoteDlg.ColumnVisible(3));

                remoteDlg.FileJson = filesJson;
                remoteDlg.SizeJson = sizesJson;

                remoteDlg.FolderBrowser.SelectNode(TARGETED);
                Assert.IsTrue(remoteDlg.ColumnVisible(2));
                Assert.IsFalse(remoteDlg.ColumnVisible(3));
                remoteDlg.ClickVersions();
                Assert.IsTrue(remoteDlg.ColumnVisible(2));
                Assert.IsTrue(remoteDlg.ColumnVisible(3));

                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);

        }


        /// <summary>
        /// This class contains methods used to generate JSON data in order to test PanoramaClient
        /// </summary>
        private class TestClientJson
        {

            public JToken CreateFiles()
            {
                var root = new JObject();
                root["rowCount"] = 4;
                root["rows"] = new JArray(CreateObj("File1", ("/" + TARGETED + "/"), false, null, 2, 1, 1, 4), CreateObj("File2", ("/" + TARGETED + "/"), true, 1, 3, 2, 2, 4), CreateObj("File3", ("/" + TARGETED + "/"), true, 2, 4, 3, 3, 4), CreateObj("File4", ("/" + TARGETED + "/"), true, 3, null, 4, 4, 4));
                return root;
            }

            public JToken CreateFile()
            {
                var root = new JObject();
                root["rowCount"] = 1;
                root["rows"] = new JArray(CreateObj("File1", ("/" + TARGETED_LIBRARY + "/"), false, null, null, 1, 1, 1));
                return root;
            }

            public JToken CreateSizeJson()
            {
                var root = new JObject();
                root["rowCount"] = 4;
                root["rows"] = new JArray(CreateSizeObj("1", "3"));
                return root;
            }

            public JToken CreateSizesJson()
            {
                var root = new JObject();
                root["rowCount"] = 4;
                root["rows"] = new JArray(CreateSizeObj("1", "3"), CreateSizeObj("2", "200"), CreateSizeObj("3", "6000"), CreateSizeObj("4", "50"));
                return root;
            }

            public JToken CreateSizeObj(string id, string size)
            {
                var obj = new JObject();
                obj["Id"] = id;
                obj["DocumentSize"] = size;
                return obj;
            }

            public JToken CreateObj(string name, string path, bool replaced, int? replacedBy, int? replaces, int id, int rowId, int versions)
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
                obj["Created"] = "5/11/23";
                obj["File/Versions"] = versions;
                return obj;
            }

            public JObject CreatePrivateFolder(string name)
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

            public JObject CreateFolder(string name, bool subfolder, bool targeted, bool collaboration = false, bool library = false)
            {
                JObject obj = new JObject();
                obj["name"] = name;
                obj["path"] = "/" + name + "/";
                obj["userPermissions"] = subfolder ? 3 : 1;
                if (subfolder || !targeted)
                {
                    // Create a writable subfolder if this folder is not writable, i.e. it is
                    // not a targetedMS folder or the user does not have write permissions in this folder.
                    // Otherwise, it will not get added to the folder tree (PublishDocumentDlg.AddChildContainers()).
                    obj["children"] = new JArray(CreateFolder("Subfolder", false, true));
                }
                else
                {
                    obj["children"] = new JArray();
                }

                if (library)
                {
                    JObject objChild = new JObject();
                    objChild["effectiveValue"] = "Library";
                    obj["moduleProperties"] = new JArray(objChild);
                }
                obj["folderType"] = collaboration ? "Collaboration" : "Targeted MS";

                obj["activeModules"] = targeted
                    ? new JArray("MS0", "MS1", "TargetedMS", "MS3")
                    : new JArray("MS0", "MS1", "MS3");

                return obj;
            }

            //Only generating 3 nodes in the tree
            public JToken GetInfoForFolders(PanoramaServer server, string folder)
            {
                JObject testFolders = new JObject();
                testFolders = CreateFolder(TARGETED, true, true);
                testFolders["children"] = new JArray(
                    CreateFolder(TARGETED_LIBRARY, true, true, false, true),
                    CreateFolder(TARGETED, true, true),
                    CreateFolder(NO_TARGETED, true, false, true),
                    CreateFolder(TARGETED_COLLABORATION, true, true, true));
                return testFolders;
            }
        }
    }
}
