/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
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

using System.Drawing;
using System.Windows.Forms;
using System;

namespace pwiz.Common.Controls
{
    public enum ProgressBarDisplayText
    {
        Percentage,
        CustomText
    }

    // based on https://stackoverflow.com/a/29175656/638445
    public class CustomTextProgressBar : ProgressBar
    {
        /// <summary>
        /// Set whether to print a % or Text
        /// </summary>
        public ProgressBarDisplayText DisplayStyle { get; set; }

        private string m_CustomText;

        /// <summary>
        /// Custom text to display over the progress bar
        /// </summary>
        public string CustomText
        {
            get { return m_CustomText; }
            set
            {
                m_CustomText = value;
                Invalidate();
            }
        }

        private const int WM_PAINT = 0x000F;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            switch (m.Msg)
            {
                case WM_PAINT:
                    int m_Percent = Convert.ToInt32((Convert.ToDouble(Value) / Convert.ToDouble(Maximum)) * 100);
                    dynamic flags = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter |
                                    TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis;

                    using (Graphics g = Graphics.FromHwnd(Handle))
                    {
                        using (Brush textBrush = new SolidBrush(ForeColor))
                        {

                            switch (DisplayStyle)
                            {
                                case ProgressBarDisplayText.CustomText:
                                    TextRenderer.DrawText(g, CustomText, Font,
                                        new Rectangle(0, 0, Width, Height), Color.Black, flags);
                                    break;
                                case ProgressBarDisplayText.Percentage:
                                    TextRenderer.DrawText(g, string.Format(@"{0}%", m_Percent), Font,
                                        new Rectangle(0, 0, Width, Height), Color.Black, flags);
                                    break;
                            }

                        }
                    }

                    break;
            }

        }

    }
}