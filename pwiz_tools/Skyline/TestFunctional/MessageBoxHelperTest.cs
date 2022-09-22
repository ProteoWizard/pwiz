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

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MessageBoxHelperTest : AbstractFunctionalTest
    {
        private FormEx _testForm;
        private TextBox _testTextBox;
        /// <summary>
        /// Text of the Label which appears before _testTextBox.
        /// <see cref="MessageBoxHelper.GetControlMessage"/> substitutes this text into its error messages.
        /// </summary>
        private const string TEXT_BOX_LABEL = "TextBoxLabel";
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
                _testForm = new FormEx();
                var flowLayoutPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill
                };
                _testForm.Controls.Add(flowLayoutPanel);

                // MessageBoxHelper.GetControlMessage inserts the text of the label before the textbox into
                // the error message.
                flowLayoutPanel.Controls.Add(new Label{Text = TEXT_BOX_LABEL});
                _testTextBox = new TextBox();
                flowLayoutPanel.Controls.Add(_testTextBox);
            });
            Program.MainWindow.BeginInvoke(new Action(()=>_testForm.ShowDialog(Program.MainWindow)), null);
            VerifyDecimalError("NaN");
            VerifyDecimalError("Infinity");
            VerifyDecimalError("1e99999");
            VerifyDecimalError("-1e99999");
            VerifyDecimalError(double.PositiveInfinity.ToString(CultureInfo.CurrentCulture));
            VerifyDecimalError(double.NegativeInfinity.ToString(CultureInfo.CurrentCulture));
            VerifyDecimalNoError(double.MinValue);
            VerifyDecimalNoError(double.MaxValue);
            VerifyDecimalNoError("1e-9999999", 0);

            VerifyIntegerError("99999999999");
            VerifyIntegerError("xxx");
            VerifyIntegerError("1.0");
            VerifyIntegerError(long.MaxValue.ToString());
            VerifyIntegerError(long.MinValue.ToString());
            VerifyIntegerNoError("0000000000000000001", 1);
            VerifyIntegerNoError(int.MaxValue.ToString(), int.MaxValue);
            VerifyIntegerNoError(int.MinValue.ToString(), int.MinValue);


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
                Assert.AreEqual(string.Format(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, TEXT_BOX_LABEL), messageDlg.Message);
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

        private void VerifyIntegerError(string textValue)
        {
            var messageBoxHelper = new MessageBoxHelper(_testForm);
            RunDlg<MessageDlg>(() =>
            {
                _testTextBox.Text = textValue;
                messageBoxHelper.ValidateNumberTextBox(_testTextBox, null, null, out int _);
            }, messageDlg =>
            {
                Assert.AreEqual(string.Format(Resources.MessageBoxHelper_ValidateNumberTextBox__0__must_contain_an_integer, TEXT_BOX_LABEL), messageDlg.Message);
                messageDlg.OkDialog();
            });
        }

        private void VerifyIntegerNoError(string textValue, int expectedValue)
        {
            var messageBoxHelper = new MessageBoxHelper(_testForm);
            RunUI(() =>
            {
                _testTextBox.Text = textValue;
                messageBoxHelper.ValidateNumberTextBox(_testTextBox, null, null, out int parsedValue);
                Assert.AreEqual(expectedValue, parsedValue);
            });
        }
    }
}
