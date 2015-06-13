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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class ImmediateWindow : DockableFormEx     
    {
        private readonly SkylineWindow _parent;
        private readonly TextBoxStreamWriter _textBoxStreamWriter;

        public ImmediateWindow(SkylineWindow parent, TextBoxStreamWriterHelper writerHelper)
        {
            InitializeComponent();
            // Initializes a new TextWriter to write to the textBox instead.
            _textBoxStreamWriter = new TextBoxStreamWriter(textImWindow, this, writerHelper);
            _parent = parent;
        }

        public TextWriter Writer { get { return _textBoxStreamWriter; } }

        public int LineCount
        {
            get
            {
                int len = textImWindow.Lines.Length;
                if (len == 0)
                    return 0;
                if (string.IsNullOrWhiteSpace(textImWindow.Lines[len - 1]))
                    len--;
                return len;
            }
        }

        private void textImWindow_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                string textBoxText = TextContent;
                int line = GetCurrentLine(textImWindow);                
                RunLine(line);             
                if (textBoxText != TextContent)
                {
                    // This supresses the newline character if RunLine modified the textBoxText
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Call before closing to stop the TextboxStreamWriter from writing to a disposed textbox.
        /// </summary>
        public void Cleanup()
        {
            _textBoxStreamWriter.Cleanup();
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
            if (line < LineCount)
            {
                string lineText = textImWindow.Lines[line];
                // This is to take care of the annoying case when the user trys to add a tool with a title they already used and the tool runs.@
                if (!lineText.Contains("--tool-add")) // Not L10N
                {
                    //Check if there is a tool to run on the line
                    foreach (var tool in Settings.Default.ToolList.Where(tool => lineText.Contains(tool.Title)))
                    {                        
                        //CONSIDER: multiple tools running. eg. two tools titled "Tool" and "ToolTest" if you enter ToolTest then both tools will run.
                        try
                        {                            
                            tool.RunTool(_parent.Document, _parent, _textBoxStreamWriter.WriterHelper, _parent, _parent);
                        }
                        catch (WebToolException e)
                        {
                            WebHelpers.ShowLinkFailure(_parent, e.Link);
                        }
                        catch(Exception e)
                        {
                            MessageDlg.ShowException(_parent, e);
                        }
                    }
                }
                // Try to parse like SkylineRunner parameters 
                string[] args = CommandLine.ParseArgs(lineText);
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
            _textBoxStreamWriter.Clear();
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

    /// <summary>
    /// Class that persists when ImmediateWindow is Closed so external tools can continue to write to it.
    /// </summary>
    public class TextBoxStreamWriterHelper : TextWriter
    {
        // Assume they wont run more than 32 processes at the same time that output to the immediate window.          
        public const int NUM_PROC = 32;

        private readonly Dictionary<int, int> _pidToSmallInt;
        private readonly BitArrayEx _indexesInUse;

        public TextBoxStreamWriterHelper()
        {
            Text = string.Empty;
            _pidToSmallInt = new Dictionary<int, int>();
            _indexesInUse = new BitArrayEx(NUM_PROC);
        }

        public string Text { get; set; }

        public void WriteLineWithIdentifier(int process, string s)
        {
            int mapping = GetMapping(process);
            if (mapping == -1)
            {
                WriteLine(s);
            }
            else
            {
                WriteLine(mapping + ">" + s); // Not L10N
            }
        }

        private int GetMapping(int pid)
        {
            lock (this)
            {
                int index;
                if (_pidToSmallInt.TryGetValue(pid, out index))
                {
                    return index;
                }
                // If this is the only process, assign -1, which means not to print an identifier
                if (_pidToSmallInt.Count == 0)
                {
                    _pidToSmallInt.Add(pid, -1);
                    return -1;
                }
                // If there is only one, then its identifier may be -1, and need to be changed
                // to start identifying its output
                if (_pidToSmallInt.Count == 1)
                {
                    var pidIndex = _pidToSmallInt.First();
                    if (pidIndex.Value == -1)
                    {
                        _pidToSmallInt.Remove(pidIndex.Key);
                        _pidToSmallInt.Add(pidIndex.Key, _indexesInUse.GetLowest());
                    }
                }
                index = _indexesInUse.GetLowest();
                _pidToSmallInt.Add(pid, index);
                return index;
            }
        }

        public void HandleProcessExit(int pid)
        {
            lock (this)
            {
                int index;
                if (_pidToSmallInt.TryGetValue(pid, out index))
                {
                    _pidToSmallInt.Remove(pid);
                    if (_indexesInUse.IndexInRange(index) && _indexesInUse.Get(index))
                        _indexesInUse.Set(index, false);
                }
            }
        }
        
        protected virtual void OnWroteLine(string args)
        {
            WriteLineEvent handler = Wrote;
            if (handler != null) handler(this, args);
        }
        
        public event WriteLineEvent Wrote;
        
        public override void WriteLine()
        {
            try
            {
                base.WriteLine();
                Text += Environment.NewLine;
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
            OnWroteLine(string.Empty);
        }
        
        public override void WriteLine(string value)
        {
            try
            {
                base.WriteLine(value);
                Text += value + Environment.NewLine;
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
            OnWroteLine(value);
        }
        
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public void Clear()
        {
            Text = string.Empty;
        }
    }
    
    public delegate void WriteLineEvent(object sender, string args);

    public class TextBoxStreamWriter : TextWriter
    {
        private readonly TextBox _box;
        private readonly Control _immediateWindow;

        public delegate void Del(string text);

        public TextBoxStreamWriter(TextBox box, Control immediateWindow, TextBoxStreamWriterHelper writerHelper)
        {
            _box = box;
            _box.Text = writerHelper.Text;
            writerHelper.Wrote += HandleOnWroteLine;
            _immediateWindow = immediateWindow;
            WriterHelper = writerHelper;
        }

        private void HandleOnWroteLine(object sender, string s)
        {
            WriteLine(s);
        }

        public TextBoxStreamWriterHelper WriterHelper { get; set; }

        public void Cleanup()
        {
            // Stop writing to the text box we are about to dispose of. 
            WriterHelper.Wrote -= HandleOnWroteLine;
        }

        /// <summary>
        ///  Behaves slightly differenty. If there is text on the current line, it first writes a newline character.
        /// </summary>
        public override void WriteLine(string s)
        {
            RunUIAction(WriteLineHelper, s);
        }

        /// <summary>
        /// Writes an NewLine character to the end of the text in the text box.
        /// </summary>
        public override void WriteLine()
        {
            RunUIAction(WriteLineHelper, string.Empty);
        }

        private void WriteLineHelper(string s)
        {
            BoxAppendText(s + Environment.NewLine);
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
            if (currentline < _box.Lines.Count() && _box.Lines[currentline] != string.Empty)
            {
                WriteLine();    
            }
            BoxAppendText(text);            
        }

        public override void Write(string s)
        {
            RunUIAction(BoxAppendText, s);              
        }

        private void BoxAppendText(string s)
        {
            try
            {
                _box.AppendText(s);
            }
            catch (Exception)
            {
                // ignored : may be disposed
            }
        }

        public void WriteStringToCursor(string s)
        {
            RunUIAction(BoxInsertText, s);
        }

        private void BoxInsertText(string s)
        {
            try
            {
                _box.Text = _box.Text.Insert(_box.SelectionStart, s);
            }
            catch (Exception)
            {
                // ignored : may be disposed
            }
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

        public void Clear()
        {
            WriterHelper.Clear();
        }
    }

    public class BitArrayEx
    {
        public BitArrayEx(int count)
        {
            _bitArray = new BitArray(count, false);
            _length = count;
        }
        private readonly int _length;
        private readonly BitArray _bitArray;

        public int GetLowest()
        {
            for (int i = 0; i < _length; i ++)
            {
                if (!_bitArray.Get(i))
                {
                    _bitArray.Set(i,true);
                    return i;
                }                
            }
            return -1;
        }

        public void Set(int i, bool value)
        {
            _bitArray.Set(i,value);
        }
        
        public bool Get(int i)
        {
            return _bitArray.Get(i);
        }

        public bool IndexInRange(int index)
        {
            return index >= 0 && index < _length;
        }
    }
}