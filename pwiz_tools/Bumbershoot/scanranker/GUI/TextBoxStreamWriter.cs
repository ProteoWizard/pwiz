//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Initial Developer of the DirecTag peptide sequence tagger is Matt Chambers.
// Contributor(s): Surendra Dasaris
//
// The Initial Developer of the ScanRanker GUI is Zeqiang Ma.
// Contributor(s): 
//
// Copyright 2009 Vanderbilt University
//

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
