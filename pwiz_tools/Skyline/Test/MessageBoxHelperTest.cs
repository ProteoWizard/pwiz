/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MessageBoxHelperTest : AbstractFunctionalTest
    {
        const string textBoxLabel = "TextBoxLabel";
        private MessageBoxHelperTestForm _testForm;
        private TextBox _testTextBox;
        [TestMethod]
        public void TestMessageBoxHelper()
        {
            RunFunctionalTest();
        }
      
        protected override void DoTest()
        {
            _testForm = null;
            _testTextBox = null;
            RunUI(() =>
            {
                _testForm = new MessageBoxHelperTestForm();
                var flowLayoutPanel = new FlowLayoutPanel()
                {
                    Dock = DockStyle.Fill
                };
                _testForm.Controls.Add(flowLayoutPanel);

                flowLayoutPanel.Controls.Add(new Label{Text = textBoxLabel});
                _testTextBox = new TextBox();
                flowLayoutPanel.Controls.Add(_testTextBox);
            });
            Assert.AreSame(_testForm, ShowDialog<MessageBoxHelperTestForm>(() => _testForm.ShowDialog(Program.MainWindow)));
            VerifyDecimalError("NaN");
            VerifyDecimalError("Infinity");
            VerifyDecimalError("1e99999");
            VerifyDecimalError("-1e99999");
            VerifyDecimalError(double.PositiveInfinity.ToString(CultureInfo.CurrentCulture));
            VerifyDecimalError(double.NegativeInfinity.ToString(CultureInfo.CurrentCulture));
            VerifyDecimalNoError(double.MinValue);
            VerifyDecimalNoError(double.MaxValue);
            VerifyDecimalNoError("1e-9999999", 0);
            OkDialog(_testForm, _testForm.Close);
            _testForm.Dispose();
        }

        private void VerifyDecimalError(string textValue)
        {
            var messageBoxHelper = new MessageBoxHelper(_testForm);
            RunDlg<MessageDlg>(() =>
            {
                _testTextBox.Text = textValue;
                messageBoxHelper.ValidateDecimalTextBox(_testTextBox, out _);
            }, messageDlg =>
            {
                Assert.AreEqual(string.Format(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, textBoxLabel), messageDlg.Message);
                messageDlg.OkDialog();
            });
        }

        private void VerifyDecimalNoError(double value)
        {
            VerifyDecimalNoError(value.ToString(Formats.RoundTrip, CultureInfo.CurrentCulture), value);
        }

        private void VerifyDecimalNoError(string textValue, double expectedValue)
        {
            var messageBoxHelper = new MessageBoxHelper(_testForm);
            RunUI(() =>
            {
                _testTextBox.Text = textValue;
                messageBoxHelper.ValidateDecimalTextBox(_testTextBox, out double parsedValue);
                Assert.AreEqual(expectedValue, parsedValue);
            });
        }

        private void VerifyError(Action action, string expectedMessage)
        {
            RunDlg<AlertDlg>(action, alertDlg =>
            {
                if (expectedMessage != null)
                {
                    Assert.AreEqual(expectedMessage, alertDlg.Message);
                }
                alertDlg.OkDialog();
            });
        }

        private class MessageBoxHelperTestForm : FormEx
        {

        }
    }
}
