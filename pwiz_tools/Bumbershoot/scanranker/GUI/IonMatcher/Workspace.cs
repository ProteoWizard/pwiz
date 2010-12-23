//
// $Id: Workspace.cs 29 2010-12-17 16:55:36Z zeqiangma $
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
//using System.Threading;

namespace IonMatcher
{
    class Workspace
    {
        //public static TextBoxForm statusForm;

        /// <summary>
        /// send text to status form
        /// </summary>
        /// <param name="text"></param>
 //       delegate void SetTextCallback(string text);
 //       public static void SetText(string text)
 //       {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            //if (statusForm.tbStatus.InvokeRequired)
            //{
            //    SetTextCallback d = new SetTextCallback(SetText);
            //    statusForm.Invoke(d, new object[] { text });
            //}
            //else
            //{
            //    statusForm.tbStatus.AppendText(text);
            //}
//        }

        private MainForm mainForm;
        public Workspace(MainForm mainForm)
        {
            this.mainForm = mainForm;
        }

        /// <summary>
        /// run a process and send standard output and standard error to status form
        /// </summary>
        /// <param name="pathAndExeFile"></param>
        /// <param name="args"></param>
        public void RunProcess(string pathAndExeFile, string args, string workingDir)
        {
            Process RunProc = new Process();
            RunProc.EnableRaisingEvents = true;
            RunProc.StartInfo.CreateNoWindow = true;
            RunProc.StartInfo.WorkingDirectory = workingDir;
            RunProc.StartInfo.UseShellExecute = false;
            RunProc.StartInfo.RedirectStandardError = true;
            RunProc.StartInfo.RedirectStandardOutput = true;

            RunProc.ErrorDataReceived += new DataReceivedEventHandler(StdErrorHandler);
            //RunProc.OutputDataReceived += new DataReceivedEventHandler( StdOutputHandler );
            //RunProc.ErrorDataReceived += new DataReceivedEventHandler(StdErrorHandler);

            RunProc.StartInfo.FileName = pathAndExeFile;
            RunProc.StartInfo.Arguments = args;
            
            RunProc.Start();

            //RunProc.BeginOutputReadLine();
            RunProc.BeginErrorReadLine();

            //string stdOutput = RunProc.StandardOutput.ReadToEnd();
            //statusForm.tbStatus.AppendText(stdOutput);
            //RunProc.WaitForExit();

            StreamReader srOut = RunProc.StandardOutput;
            //StreamReader srErr = RunProc.StandardError;
            //while (!RunProc.HasExited)

            string outLine = string.Empty;
            //string errLine = string.Empty;
            //while (((outLine = srOut.ReadLine()) != null) || ((errLine = srErr.ReadLine()) != null))
            //while (((outLine = srOut.ReadLine()) != null) && (!RunProc.HasExited))

            while ((outLine = srOut.ReadLine()) != null)
            {
                //SetText(outLine+"\r\n");
                MainForm.SetText(mainForm, outLine + "\r\n");
                //lock(statusForm.tbStatus)
                //statusForm.tbStatus.AppendText(outLine + "\n");
                // RunProc.WaitForExit(500);
            }
            //SetText("\r\nError/warning message from " + pathAndExeFile + " :\r\n");          
            //string srErr = RunProc.StandardError.ReadToEnd();
            //SetText(srErr.ReadToEnd().ToString()+"\r\n");
            //SetText(srErr + "\r\n");
            RunProc.WaitForExit();
            RunProc.Close();

        }

        private void StdErrorHandler(object sendingProcess,
            DataReceivedEventArgs errLine)
        {
            
            //SetText(errLine.Data + "\r\n");
            MainForm.SetText(mainForm, errLine.Data + "\r\n");

            // Write the error text to the file if there is something
            // to write and an error file has been specified.

            //if (!String.IsNullOrEmpty(errLine.Data))
            //{
            //    if (!errorsWritten)
            //    {
            //        if (streamError == null)
            //        {
            //            // Open the file.
            //            try
            //            {
            //                streamError = new StreamWriter(netErrorFile, true);
            //            }
            //            catch (Exception e)
            //            {
            //                Console.WriteLine("Could not open error file!");
            //                Console.WriteLine(e.Message.ToString());
            //            }
            //        }

            //        if (streamError != null)
            //        {
            //            // Write a header to the file if this is the first
            //            // call to the error output handler.
            //            streamError.WriteLine();
            //            streamError.WriteLine(DateTime.Now.ToString());
            //            streamError.WriteLine("Net View error output:");
            //        }
            //        errorsWritten = true;
            //    }

            //    if (streamError != null)
            //    {
            //        // Write redirected errors to the file.
            //        streamError.WriteLine(errLine.Data);
            //        streamError.Flush();
            //    }
            //}
        }


    }//workspace
}
