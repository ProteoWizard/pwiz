/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ConfigureToolsDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestConfigureToolsDlg()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestEmptyOpen();

            TestButtons();

            TestPopups();

            TestEmptyCommand();

            TestSaveDialogNo();
            
            TestSavedCancel();

            TestValidNo();

            TestIndexChange();

            TestProcessStart();

            TestNewToolName();
        }

        // Opens dlg assuming no tools exist exits with no tools.
        private static void TestEmptyOpen()
        {
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
                {
                    AssertEx.NoDiff(configureToolsDlg.ToolList[0].Title, "[New Tool1]");
                    Assert.IsTrue(configureToolsDlg.ToolList.Count == 1);
                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled == false);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled == false);
                    configureToolsDlg.Delete();
                    configureToolsDlg.SaveTools();
                    Assert.IsTrue(configureToolsDlg.btnApply.Enabled == false);
                    Assert.IsTrue(configureToolsDlg.ToolList.Count == 0);
                    Assert.IsTrue(configureToolsDlg.listTools.SelectedIndex == -1);
                    Assert.IsTrue(configureToolsDlg._previouslySelectedIndex == -1);
                    configureToolsDlg.OkDialog();
                });
        }

        private static void TestButtons()
        {
            {
                ConfigureToolsDlg configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                RunUI(() =>
                {
                    Assert.AreEqual(1, configureToolsDlg.ToolList.Count);
                    configureToolsDlg.Delete();
                    Assert.AreEqual("", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual("", configureToolsDlg.textInitialDirectory.Text);
                    Assert.IsFalse(configureToolsDlg.textTitle.Enabled);
                    Assert.IsFalse(configureToolsDlg.textCommand.Enabled);
                    Assert.IsFalse(configureToolsDlg.textArguments.Enabled);
                    Assert.IsFalse(configureToolsDlg.textInitialDirectory.Enabled);
                    Assert.AreEqual(0, configureToolsDlg.ToolList.Count);
                    Assert.IsFalse(configureToolsDlg.btnDelete.Enabled);
                    // Now add an example.
                    configureToolsDlg.AddDialog("example1", "example1.exe", "", "");
                    Assert.AreEqual(1, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(0, configureToolsDlg.listTools.SelectedIndex);
                    Assert.IsTrue(configureToolsDlg.btnDelete.Enabled);
                    Assert.AreEqual("example1", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("example1.exe", configureToolsDlg.textCommand.Text);
                    Assert.IsFalse(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnMoveDown.Enabled);
                    // Now add an example2.
                    configureToolsDlg.AddDialog("example2", "example2.exe", "2Arguments",
                                                "2InitialDirectory");
                    Assert.AreEqual(2, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual("example2", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("example2.exe", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("2Arguments", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(configureToolsDlg.textInitialDirectory.Text,
                                    "2InitialDirectory");
                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnMoveDown.Enabled);
                    // Test move up/down with only 2 tools.
                    configureToolsDlg.MoveUp();
                    Assert.AreEqual(2, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(0, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual("example2", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("example2.exe", configureToolsDlg.textCommand.Text);
                    Assert.IsFalse(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled);
                    Assert.AreEqual("example2", configureToolsDlg.ToolList[0].Title);
                    Assert.AreEqual("example1", configureToolsDlg.ToolList[1].Title);
                    configureToolsDlg.MoveDown();
                    Assert.AreEqual("example1", configureToolsDlg.ToolList[0].Title);
                    Assert.AreEqual("example2", configureToolsDlg.ToolList[1].Title);
                    // Now add an example 3.
                    configureToolsDlg.AddDialog("example3", "example3.exe", "", "");
                    Assert.AreEqual(3, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(2, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual("example3", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("example3.exe", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual("", configureToolsDlg.textInitialDirectory.Text);
                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsFalse(configureToolsDlg.btnMoveDown.Enabled);
                    // Test btnMoveUp.
                    configureToolsDlg.MoveUp();
                    Assert.AreEqual(3, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual("example3", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("example3.exe", configureToolsDlg.textCommand.Text);
                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled);
                    Assert.AreEqual("example2", configureToolsDlg.ToolList[2].Title);
                    Assert.AreEqual("example3", configureToolsDlg.ToolList[1].Title);
                    Assert.AreEqual("example1", configureToolsDlg.ToolList[0].Title);
                });
                // Test response to selected index changing.
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(0), messageDlg =>
                {
                    AssertEx.Contains(
                        messageDlg.
                            Message,
                        "Warning: \n The command for example3 may not exist in that location. Would you like to edit it?");
                    messageDlg.
                        BtnCancelClick();
                });
                RunUI(() =>
                {
                    Assert.AreEqual("example1", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("example1.exe", configureToolsDlg.textCommand.Text);
                    Assert.IsFalse(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled);
                    // Test update of previously selected index.
                    configureToolsDlg.listTools.SelectedIndex = 0;
                    Assert.AreEqual(configureToolsDlg._previouslySelectedIndex, 0);
                    // Test btnMoveDown.
                    configureToolsDlg.MoveDown();

                    Assert.AreEqual(1, configureToolsDlg.listTools.SelectedIndex);
                    Assert.AreEqual("example1", configureToolsDlg.textTitle.Text);
                    Assert.AreEqual("example1.exe", configureToolsDlg.textCommand.Text);
                    Assert.AreEqual("", configureToolsDlg.textArguments.Text);
                    Assert.AreEqual("", configureToolsDlg.textInitialDirectory.Text);
                    Assert.IsTrue(configureToolsDlg.btnMoveUp.Enabled);
                    Assert.IsTrue(configureToolsDlg.btnMoveDown.Enabled);
                    Assert.AreEqual("example2", configureToolsDlg.ToolList[2].Title);
                    Assert.AreEqual("example1", configureToolsDlg.ToolList[1].Title);
                    Assert.AreEqual("example3", configureToolsDlg.ToolList[0].Title);
                });
                // Save and return to skylinewindow.
                RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
                {
                    AssertEx.Contains(messageDlg.Message,
                                    "Warning: \n The command for example1 may not exist in that location. Would you like to edit it?");
                    messageDlg.BtnCancelClick();
                });
                RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    Assert.AreEqual("example3", SkylineWindow.GetToolText(0));
                    Assert.AreEqual("example1", SkylineWindow.GetToolText(1));
                    Assert.AreEqual("example2", SkylineWindow.GetToolText(2));
                    Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                });

            }
            {
                // Reopen menu to swap, save, close, and check changes showed up.
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                RunUI(() =>
                {
                    Assert.AreEqual(3, configureToolsDlg.ToolList.Count);
                    Assert.AreEqual("example3", configureToolsDlg.ToolList[0].Title);
                    Assert.AreEqual("example1", configureToolsDlg.ToolList[1].Title);
                    Assert.AreEqual("example2", configureToolsDlg.ToolList[2].Title);
                });
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(1), messageDlg =>
                {
                    AssertEx.Contains(messageDlg.Message,
                                      "Warning: \n The command for example3 may not exist in that location. Would you like to edit it?");
                    messageDlg.BtnCancelClick();
                });
                RunUI(() =>
                {
                    configureToolsDlg.MoveDown();
                    Assert.AreEqual("example3", configureToolsDlg.ToolList[0].Title);
                    Assert.AreEqual("example2", configureToolsDlg.ToolList[1].Title);
                    Assert.AreEqual("example1", configureToolsDlg.ToolList[2].Title);
                });
                RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
                {
                    AssertEx.Contains(messageDlg.Message,
                                    "Warning: \n The command for example1 may not exist in that location. Would you like to edit it?");
                    messageDlg.BtnCancelClick();
                });
                RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    Assert.AreEqual("example3", SkylineWindow.GetToolText(0));
                    Assert.AreEqual("example2", SkylineWindow.GetToolText(1));
                    Assert.AreEqual("example1", SkylineWindow.GetToolText(2));
                    Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                });
            }
            {
                // First empty the tool list then return to skyline to check dropdown is correct.
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                // Change selected index to test deleting from the middle.
                RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(1), messageDlg =>
                {
                    AssertEx.Contains(messageDlg.Message,
                                    "Warning: \n The command for example3 may not exist in that location. Would you like to edit it?");
                    messageDlg.BtnCancelClick();
                });
                RunUI(() =>
                {
                    configureToolsDlg.Delete();
                    configureToolsDlg.Delete();
                    configureToolsDlg.Delete();
                    // Now the tool list is empty.
                    Assert.IsFalse(configureToolsDlg.btnDelete.Enabled);
                    configureToolsDlg.OkDialog();
                    SkylineWindow.PopulateToolsMenu();
                    Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
                });
            }
        }

        private static void TestPopups()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunDlg<MessageDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "The command cannot be blank, please enter a valid command for [New Tool1]");
                messageDlg.OkDialog();
            });
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.AddDialog("example1", "example1", "", "");
            });
            RunDlg<MessageDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "The command for example1 must be of a supported type \n\n Supported Types: *.exe; *.com; *.pif; *.cmd; *.bat");
                messageDlg.OkDialog();
            });
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.AddDialog("example1", "example1.exe", "", "");
            });
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "Warning: \n The command for example1 may not exist in that location. Would you like to edit it?");
                messageDlg.Btn1Click();
            });
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.AddDialog("", "example1.exe", "", "");      
            });              
            RunDlg<MessageDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "You must enter a valid title for the tool");
                messageDlg.OkDialog();
            });
            // Replace the value, save, delete it then cancel to test the "Do you wish to Save changes" dlg.
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.AddDialog("example", "example1.exe", "", "");
             });
            RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.SaveTools(), messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "Warning: \n The command for example may not exist in that location. Would you like to edit it?");
                messageDlg.BtnCancelClick();
            });
            RunUI(configureToolsDlg.Delete);
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Cancel, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "Do you wish to Save changes?");
                messageDlg.CancelDialog();
            });
            RunUI(() => Assert.IsTrue(configureToolsDlg.btnApply.Enabled));
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Cancel, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "Do you wish to Save changes?");
                messageDlg.Btn0Click();
            });
            RunUI(() => Assert.IsFalse(configureToolsDlg.btnApply.Enabled));
        }

        private static void TestEmptyCommand()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunDlg<MessageDlg>(configureToolsDlg.Add, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "The command cannot be blank, please enter a valid command for [New Tool1]");
                messageDlg.OkDialog();
            });
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.OkDialog();
            });
        }

        private static void TestSaveDialogNo()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Cancel, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "Do you wish to Save changes?");
                messageDlg.Btn1Click();
            }); 
        }

        private static void TestSavedCancel()
        {
            // Test to show Cancel when saved has no dlg.
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.SaveTools();
                configureToolsDlg.Cancel();
            });
        }

        private static void TestValidNo()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.AddDialog("example", "example.exe", "", "");
            });
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "Warning: \n The command for example may not exist in that location. Would you like to edit it?");
                messageDlg.BtnCancelClick();
            });
        }

        private static void TestIndexChange()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.AddDialog("example","example.exe","","");
                configureToolsDlg.AddDialog("example1", ".exe", "", "");
                Assert.AreEqual(configureToolsDlg._previouslySelectedIndex, 1);
                configureToolsDlg.listTools.SelectedIndex = 1;
                Assert.AreEqual(configureToolsDlg.listTools.SelectedIndex, 1);
            });
           RunDlg<MultiButtonMsgDlg>(() => configureToolsDlg.TestHelperIndexChange(0), messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message,
                    "Warning: \n The command for example1 may not exist in that location. Would you like to edit it?");
                messageDlg.Btn1Click();
            });
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.Delete();
                configureToolsDlg.SaveTools();
                configureToolsDlg.OkDialog();
            });
        }

        private static void TestProcessStart()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() =>
            {
                configureToolsDlg.Delete();
                configureToolsDlg.AddDialog("example", "test.exe", "", "");
            });
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.OkDialog, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "Warning: \n The command for example may not exist in that location. Would you like to edit it?");
                messageDlg.BtnCancelClick();
            }); 
            RunUI(() =>
            {            
                SkylineWindow.PopulateToolsMenu();
                Assert.AreEqual("example", SkylineWindow.GetToolText(0));
                Assert.IsTrue(SkylineWindow.ConfigMenuPresent());
            });
            RunDlg<MessageDlg>(() => SkylineWindow.RunTool(0), messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "File Not Found: \n\n Please check the command location is correct for this tool");
                messageDlg.OkDialog();
            });
          }

        private static void TestNewToolName()
        {
            var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunUI(() => configureToolsDlg.AddDialog("[New Tool1]", "example1.exe", "", ""));
            RunDlg<MultiButtonMsgDlg>(configureToolsDlg.Add, messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, "Warning: \n The command for [New Tool1] may not exist in that location. Would you like to edit it?");
                messageDlg.BtnCancelClick();
            });
            RunUI(() =>
            {
                do
                {
                    configureToolsDlg.Delete();
                } while (configureToolsDlg.ToolList.Count > 0);
                configureToolsDlg.OkDialog();
            });
        }
    }
}
