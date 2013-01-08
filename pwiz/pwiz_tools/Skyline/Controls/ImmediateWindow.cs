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

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class ImmediateWindow : DockableFormEx     
    {
        private readonly SkylineWindow _parent;
        private readonly TextBoxStreamWriter _textBoxStreamWriter;

        public ImmediateWindow(SkylineWindow parent)
        {
            InitializeComponent();
            // Initializes a new TextWriter to write to the textBox instead.
            _textBoxStreamWriter = new TextBoxStreamWriter(textImWindow, this);
            _parent = parent;
        }

        public TextWriter Writer { get { return _textBoxStreamWriter; } }

        private void textImWindow_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                string textBoxText = TextContent;
                int line = GetCurrentLine(textImWindow);                
                RunLine(line);             
                if (textBoxText != TextContent)
                {
                    // this supresses the newline character if RunLine modified the textBoxText
                    e.Handled = true;
                }
            }            
        }

        /// <summary>
        /// Execute the line of the text box. 
        /// Run any tools who's title is on the line. 
        /// Also attempting to run the line like a SkylineRunner input.
        /// </summary>
        /// <param name="line">number line to run. Textbox.Lines is an string[] so zero indexed.</param>
        public void RunLine (int line)
        {
            // Case when the textBox is completely empty, line returns 1 but Lines is an array length 0. 
            if (line < textImWindow.Lines.Length)
            {
                // This is to take care of the annoying case when the user trys to add a tool with a title they already used and the tool runs.@
                if (!textImWindow.Lines[line].Contains("--tool-add"))
                {
                    //Check if there is a tool to run on the line
                    foreach (var tool in
                            Settings.Default.ToolList.Where(tool => textImWindow.Lines[line].Contains(tool.Title)))
                    {                        
                        //CONSIDER: multiple tools running. eg. two tools titled "Tool" and "ToolTest" if you enter ToolTest then both tools will run.
                        try
                        {
                            tool.RunTool(_parent.Document, _parent, _textBoxStreamWriter, _parent);                                                                                                                                         
                        }
                        catch (WebToolException er  )
                        {
                            AlertLinkDlg.Show(_parent, Resources.Could_not_open_web_Browser_to_show_link_, er.Link, er.Link, false);                            
                        }
                        catch(Exception e)
                        {
                            MessageDlg.Show(_parent, e.Message);
                        }
                        
                    }
                }
                // Try to parse like SkylineRunner parameters 
                string[] args = CommandLine.ParseInput(textImWindow.Lines[line]);
                CommandLine commandLine = new CommandLine(new CommandStatusWriter(_textBoxStreamWriter));
                commandLine.Run(args);
            }
        }
     
        /// <summary>
        /// Returns the line the cursor is on. 
        /// </summary>
        private int GetCurrentLine(TextBox box)
        {
            return box.GetLineFromCharIndex((box.SelectionStart) + 1);
        }

        /// <summary>
        /// Clear the Immediate Window.
        /// </summary>
        public void Clear()
        {
            textImWindow.Clear();
        }

        public string TextContent
        {
            get { return textImWindow.Text; }
        }

        public void WriteLine(string line)
        {
            _textBoxStreamWriter.WriteLine(line);            
        }

        public void Write(string text)
        {
            _textBoxStreamWriter.Write(text);
        }

        /// <summary>
        /// Write to the Immediate Window starting with a newline if the curent line isnt blank.
        /// </summary>        
        public void WriteFresh(string text)
        {
            _textBoxStreamWriter.WriteFresh(text);
        }

        private void textImWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                e.Effect = DragDropEffects.All;
            }
        }

        /// <summary>
        /// Enables users to drag and drop files into the textBox and have their full path show up. 
        /// </summary>        
        private void textImWindow_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);

            foreach (string file in files)
            {
                _textBoxStreamWriter.WriteStringToCursor(file);
            }
            textImWindow.Focus();
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);

            var skylineWindow = _parent;
            if (skylineWindow != null)
            {
                skylineWindow.ClipboardControlGotFocus(this);
            }
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);

            var skylineWindow = _parent;
            if (skylineWindow != null)
            {
                skylineWindow.ClipboardControlLostFocus(skylineWindow);
            }
        }
    }

    public class TextBoxStreamWriter : TextWriter
    {
        private readonly TextBox _box;
        private readonly Control _immediateWindow;

        public delegate void Del(string text);

        public TextBoxStreamWriter(TextBox box, Control immediateWindow)
        {
            _box = box;
            _immediateWindow = immediateWindow;
        }

        /// <summary>
        ///  Behaves slightly differenty. If there is text on the current line, it first writes a newline character.
        /// </summary>
        public override void WriteLine(string s)
        {
            RunUIAction(WriteLineHelper,s);
        }
        private void WriteLineHelper(string s)
        {
            int currentline = _box.GetLineFromCharIndex((_box.SelectionStart) + 1);
            // if there is text on the current line, write to the next one
            if (currentline < _box.Lines.Count() && _box.Lines[currentline] != "")
            {
                WriteLine();
            }
            _box.AppendText(s + Environment.NewLine);
        }

        /// <summary>
        /// Writes an NewLine character to the end of the text in the text box.
        /// </summary>
        public override void WriteLine()
        {
            RunUIAction(_box.AppendText, Environment.NewLine);            
        }
 
        /// <summary>
        /// Writes to the current line of the textbox, if there is text on the current line, it first writes a newline character.
        /// </summary>  
        public void WriteFresh(string text)
        {
            RunUIAction(WriteFreshHelper, text);
        }      
        private void WriteFreshHelper(string text)
        {
            int currentline = _box.GetLineFromCharIndex((_box.SelectionStart) + 1);
            // if there is text on the current line, write to the next one
            if (currentline < _box.Lines.Count() && _box.Lines[currentline] != "")
            {
                WriteLine();    
            }
            _box.AppendText(text);            
        }

        public override void Write(string s)
        {
            RunUIAction(str => _box.AppendText(str), s);              
        }

        public void WriteStringToCursor (string s)
        {
            RunUIAction(str=> _box.Text = _box.Text.Insert(_box.SelectionStart, str), s);
        }

        private void RunUIAction<TArg>(Action<TArg> act, TArg arg)
        {
            if (_immediateWindow.InvokeRequired)
                _immediateWindow.BeginInvoke(act, arg);
            else
                act(arg);
        }

        #region Overrides of TextWriter

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        #endregion
    }
}