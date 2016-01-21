/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Windows.Forms;
using System.Xml.Serialization;
using DigitalRune.Windows.Docking;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class DetailedReportErrorDlg : FormEx
    {
        public string SkylineFile = string.Empty;
        public string Email;
        public string Message;
        public List<Image> ScreenShots;
        public bool IsTest = false;

        private const int MAX_SCREENSHOTS = 3;
        private readonly Rectangle _picBoxRect;
        
        public DetailedReportErrorDlg()
        {
            InitializeComponent();
            
            _picBoxRect = pictureBox1.Bounds;

            var openForms = FindOpenForm<Form>();
            var screenShotForms = new List<Form>();
                
            foreach (var form in openForms)
            {
                if (!(form is ReportErrorDlg) && !(form is DetailedReportErrorDlg))
                {
                    screenShotForms.Add(form);
                }
            }
            ScreenShots = TakeScreenShots(screenShotForms);
            var picBoxList = new List<PictureBox>();
            foreach (Image img in ScreenShots)
                picBoxList.Add(ResizeAndPlaceImage(img));

            int offset = pictureBox1.Right + 10; // Margin 10 right of pictureBox1
            for (var i = 1; i < picBoxList.Count; i++)  // Skip pictureBox1
            {
                picBoxList[i].Location = new Point(offset, _picBoxRect.Y);
                Controls.Add(picBoxList[i]);
                offset += picBoxList[i].Width + 10; // Margin of 10 between images.
            }
            
        }

        // Scales images to fit on form.
        private PictureBox ResizeAndPlaceImage(Image img)
        {
            int divider = 4;
            for (int i = 1; i < img.Width; i++)
            {
                if (img.Width / i < _picBoxRect.Width && img.Height / i < pictureBox1.Height)
                {
                    divider = i;
                    break;
                }
            }
            var sizeImage = new Size(img.Width/divider, img.Height/divider);
            if (pictureBox1.Image == null)
            {
                pictureBox1.Size = sizeImage;
                pictureBox1.Image = new Bitmap(img, sizeImage);
                return pictureBox1;
            }
            else
            {
                return new PictureBox
                {
                    Visible = true,
                    Width = sizeImage.Width,
                    Height = sizeImage.Height,
                    Image = new Bitmap(img, sizeImage)
                };
            }
        }

        public static Image ResizeImage(Image imgToResize, Size size)
        {
            return new Bitmap(imgToResize, size);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog(false);
        }

        private void btnOkAnon_Click(object sender, EventArgs e)
        {
            OkDialog(true);
        }

        private void SkippedReportErrorDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If user closing form without clicking a button, call OkDialog non anonymous
            if (e.CloseReason != CloseReason.None)
            {
                if (!OkDialog(false))
                {
                    e.Cancel = true;
                }                
            }
        }

        public bool OkDialog(bool anonymousPost)
        {
            if (anonymousPost)
                Email = string.Empty;
            else
            {
                // try catch block checks that given email is valid
                try
                {
                    // ReSharper disable once UnusedVariable
                    var addr = new MailAddress(textBoxEmail.Text);
                }
                catch (Exception x)
                {
                    textBoxEmail.Focus();
                    MessageDlg.ShowWithException(this, Resources.SkippedReportErrorDlg_btnOK_Click_No_Email, x);
                    return false;
                }

                Email = textBoxEmail.Text;
            }
            
            // Writes .sky file to string to be later posted to the skyline exceptions website.
            if (checkBoxSkyFile.Checked)
            {
                var xmlSerializer = new XmlSerializer(typeof(SrmDocument));
                StringWriter fileWriter = new StringWriter();
                xmlSerializer.Serialize(fileWriter, Program.ActiveDocument);
                SkylineFile = fileWriter.ToString();
            }           
          
            Message = textBoxMsg.Text;
            // if don't send screenshots set to empty list
            if (!checkBoxScreenShot.Checked)
                ScreenShots = new List<Image>();

            DialogResult = DialogResult.OK;
            return true;
        }
        
        private static void DrawForms(Form topLevelForm, Bitmap bitmap, Control control)
        {
            if (!control.Visible || control.Width <= 0 || control.Height <= 0)
            {
                return;
            }
            if (control is Form || control is UserControl)
            {
                try
                {
                    var childForm = new Bitmap(control.Width, control.Height);
                    control.DrawToBitmap(childForm, new Rectangle(0, 0, control.Width, control.Height));

                    Point offset;
                    if (control.Parent != null)
                    {
                        var myPosition = control.Parent.PointToScreen(control.Location);
                        var topLevelPosition = topLevelForm.Location;
                        offset = new Point(myPosition.X - topLevelPosition.X, myPosition.Y - topLevelPosition.Y);
                    }
                    else
                    {
                        offset = new Point(0, 0);
                    }
                    var g = Graphics.FromImage(bitmap);

                    g.DrawImage(childForm, offset);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine("Exception while generating screenshot {0}", exception); // Not L10N
                }
            }
            foreach (Control child in control.Controls)
            {
                DrawForms(topLevelForm, bitmap, child);
            }            
        }

        // Iterates through forms and takes screenshot of each indavidually.
        private static List<Image> TakeScreenShots(IEnumerable<Form> forms)
        {  
            var screenShots = new List<Image>();
            foreach (var form in forms)
            {
                if (form.Parent == null || form is FloatingWindow)
                {
                    var bitmap = new Bitmap(form.Width, form.Height);
                    DrawForms(form, bitmap, form);
                    screenShots.Add(bitmap);
                    if (screenShots.Count >= MAX_SCREENSHOTS)
                        break;
                }                
            }
            return screenShots;
        }

        private static IEnumerable<Form> OpenForms
        {
            get
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        return Application.OpenForms.Cast<Form>().ToArray();
                    }
                    catch (InvalidOperationException)
                    {
                        // Application.OpenForms might be modified during the iteration.
                        // If that happens, go through the list again.
                    }
                }
                return new Form[0];
            }
        }
        
        // Need to find all open forms to create the screenshots.
        public static List<TDlg>  FindOpenForm<TDlg>() where TDlg : Form
        {
            var forms = new List<TDlg>();
            foreach (var form in OpenForms)
            {
                var tForm = form as TDlg;
                if (tForm != null && tForm.Created)
                {
                    forms.Add(tForm);
                }
            }
            return forms;
        }

        // Test Method
        public void SetFormProperties(bool sendScreenSchots, bool sendSkyFile, string email, string text)
        {
            checkBoxScreenShot.Checked = sendScreenSchots;
            checkBoxSkyFile.Checked = sendSkyFile;
            textBoxEmail.Text = email;
            textBoxMsg.Text = text;
        }
    }
}
