using System;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace ScanRanker
{
    public class TextBoxStreamWriter : TextWriter
    {
        TextBox output = null;

        public TextBoxStreamWriter(TextBox Output)
        {
            output = Output;
        }

        public override void Write(char value)
        {
            base.Write(value);
            output.AppendText(value.ToString()); // when char data is written, append it to the text box
        }

        public override Encoding Encoding
        {
            get { return System.Text.Encoding.UTF8; }
                
        }

    }
}
