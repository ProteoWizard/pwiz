using System;
using System.Windows.Forms;

namespace SkylineTester
{
    /// <summary>
    /// Subclass to reduce flicker when coloring text in the text box.
    /// See http://www.c-sharpcorner.com/UploadFile/mgold/ColorSyntaxEditor12012005235814PM/ColorSyntaxEditor.aspx
    /// </summary>
    public class MyTextBox : RichTextBox
    {
        const short WM_PAINT = 0x00f;

// ReSharper disable once ConvertToConstant.Global
        public bool DoPaint = true;

        protected override void WndProc(ref Message m)
        {
            // Code courtesy of Mark Mihevc
            // sometimes we want to eat the paint message so we don't have to see all the
            // flicker from when we select the text to change the color.
            if (m.Msg == WM_PAINT && !DoPaint)
                m.Result = IntPtr.Zero; // not painting, must set this to IntPtr.Zero, otherwise serious problems.
            else
                base.WndProc(ref m); // message other than WM_PAINT, jsut do what you normally do.
        }
    }
}
