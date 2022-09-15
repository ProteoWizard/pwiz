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
using System.Net.Mail;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class DetailedReportErrorDlg : FormEx
    {
        public byte[] SkylineFileBytes;
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
            // If user closing form without clicking a button, call OkDialog as anonymous
            if (e.CloseReason != CloseReason.None)
            {
                if (!OkDialog(true))
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
                catch (Exception)
                {
                    textBoxEmail.Focus();
                    MessageDlg.Show(this, Resources.SkippedReportErrorDlg_btnOK_Click_No_Email);
                    return false;
                }

                Email = textBoxEmail.Text;
            }
            
            // Writes .sky file to byte array to be later posted to the skyline exceptions website.
            if (checkBoxSkyFile.Checked)
            {
                SkylineFileBytes = GetDocumentBytesForErrorReport(Program.ActiveDocument, ReportErrorDlg.MAX_ATTACHMENT_SIZE);
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
                    Debug.WriteLine(@"Exception while generating screenshot {0}", exception);
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
                return FormUtil.OpenForms;
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
        public void SetFormProperties(bool sendScreenShots, bool sendSkyFile, string email, string text)
        {
            checkBoxScreenShot.Checked = sendScreenShots;
            checkBoxSkyFile.Checked = sendSkyFile;
            textBoxEmail.Text = email;
            textBoxMsg.Text = text;
        }

        /// <summary>
        /// Returns the bytes which would go into the .sky file if the document were saved.
        /// </summary>
        public static byte[] GetDocumentBytesForErrorReport(SrmDocument document, int maxBytes)
        {
            var xmlSerializer = new XmlSerializer(typeof(SrmDocument));
            MemoryStream memoryStream = new MemoryStream();
            try
            {
                xmlSerializer.Serialize(
                    new StreamWriter(new MaxLengthStream(memoryStream, maxBytes), Encoding.UTF8),
                    document);
            }
            catch (Exception)
            {
                // ignore
            }

            if (memoryStream.Length > maxBytes)
            {
                memoryStream.SetLength(maxBytes);
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Write-only wrapper around a Stream which throws an exception after MaxLength bytes have been written.
        /// </summary>
        private class MaxLengthStream : Stream
        {
            public MaxLengthStream(MemoryStream stream, int maxLength)
            {
                MemoryStream = stream;
                MaxLength = maxLength;
            }

            public MemoryStream MemoryStream { get; }
            public int MaxLength { get; }

            public override void Flush()
            {
                MemoryStream.Flush();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                MemoryStream.Write(buffer, offset, count);
                if (MemoryStream.Length >= MaxLength)
                {
                    throw new ApplicationException(@"Maximum number of bytes written");
                }
            }

            public override bool CanRead
            {
                get
                {
                    return false;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }

            public override long Length
            {
                get
                {
                    return MemoryStream.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return MemoryStream.Position;
                }
                set
                {
                    MemoryStream.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
