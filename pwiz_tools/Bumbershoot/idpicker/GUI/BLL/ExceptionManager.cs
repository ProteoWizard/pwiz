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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Text;

using IdPickerGui.MODEL;

namespace IdPickerGui.BLL
{
    public static class ExceptionManager
    {
        private static int debugLevel = 0;
        private static string logFilePath;
        public static string LogFilePath
        {
            get { return logFilePath; }
            set { logFilePath = value; }
        }

        public static int DebugLevel
        {
            get { return debugLevel; }
            set { debugLevel = value; }
        }

        public static void logRequestByFormToFile(Form ownerForm, IDPickerInfo pInfo, DateTime dt)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(File.Open(LogFilePath, FileMode.Append)))
                {
                    sw.WriteLine(dt.ToString() + "----------------------------------------------");
                    sw.WriteLine("Entry Type : IDPicker Request State");
                    sw.WriteLine("Issued By  : " + ownerForm.Name);

                    sw.WriteLine(pInfo.ToString());

                    sw.WriteLine();
                }

            }
            catch (Exception e)
            {
                throw new Exception("Error logging exceptions to file " + LogFilePath + ".", e);
            }


        }

        public static void logExceptionsByFormToFile(Form ownerForm, Exception exc, DateTime dt)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(File.Open(LogFilePath, FileMode.Append)))
                {
                    sw.WriteLine(dt.ToString() + "----------------------------------------------");
                    sw.WriteLine("Entry Type : Exception");
                    sw.WriteLine("Issued By  : " + ownerForm.Name);
                    

                    sw.WriteLine(exc.Message);
                    if (DebugLevel == 1)
                    {
                        sw.WriteLine(exc.StackTrace);
                    }

                    Exception subExc = exc.InnerException;

                    while (subExc != null)
                    {
                        sw.WriteLine(subExc.Message);

                        if (DebugLevel == 1)
                        {
                            sw.WriteLine(subExc.StackTrace);
                        }
                        
                        subExc = subExc.InnerException;
                    }
                    sw.WriteLine();

                }

            }
            catch (Exception e)
            {
                throw new Exception("Error logging exceptions to file " + LogFilePath + ".", e);
            }

        }

        public static void logExceptionMessageByFormToFile(Form ownerForm, string msg, DateTime dt)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(File.Open(LogFilePath, FileMode.Append)))
                {
                    sw.WriteLine(dt.ToString() + "----------------------------------------------");
                    sw.WriteLine("Entry Type : IDPicker Request State");
                    sw.WriteLine("Issued By  : " + ownerForm.Name);

                    sw.WriteLine(msg);

                    sw.WriteLine();
                }

            }
            catch (Exception e)
            {
                throw new Exception("Error logging exceptions to file " + LogFilePath + ".", e);
            }
        }

        public static void logExceptionToFile(string msg, DateTime dt)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(File.Open(LogFilePath, FileMode.Append)))
                {
                    sw.WriteLine(dt.ToString() + "----------------------------------------------");
                    sw.WriteLine("Entry Type : IDPicker Request State");
                    sw.WriteLine("Issued By  : " + "unknown");

                    sw.WriteLine(msg);

                    sw.WriteLine();
                }

            }
            catch (Exception e)
            {
                throw new Exception("Error logging exceptions to file " + LogFilePath + ".", e);
            }
        }

    }
}
