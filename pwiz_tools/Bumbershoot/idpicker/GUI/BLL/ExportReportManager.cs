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
using System.Collections;
using System.Collections.Specialized;
using System.Text;
using System.IO;
using System.Configuration;
using ICSharpCode.SharpZipLib.Checksums;
using IdPickerGui.MODEL;
using ICSharpCode.SharpZipLib.Zip;

namespace IdPickerGui.BLL
{
    public static class ExportReportManager
    {
        /*
        private IDPickerInfo idPickerRequest;
        public IDPickerInfo IDPickerRequest
        {
            get { return idPickerRequest; }
            set { idPickerRequest = value; }
        }
         * */

        /*
        private static string getOrCreateExportDir(IDPickerInfo pInfo)
        {
            string zipDirPath = string.Empty;

            try
            {
                if (pInfo.Id != -1)
                {
                    zipDirPath = pInfo.ResultsDir + "/" + "Export";

                    if (!Directory.Exists(zipDirPath))
                    {
                        Directory.CreateDirectory(zipDirPath);
                    }
                   
                }

                return zipDirPath;

            }
            catch (Exception exc)
            {
                throw new Exception("Error creating export directory.", exc);
            }

        }
        
         */

        /// <summary>
        /// Creates a zip file
        /// </summary>
        /// <param name="zipFileStoragePath">where to store the zip file</param>
        /// <param name="zipFileName">the zip file filename</param>
        /// <param name="fileSystemInfoToZip">the directory/file to zip</param>
        /// <returns>indicates whether the file was created successfully</returns>
        public static bool CreateZipFile(string zipFileName, FileSystemInfo fileSystemInfoToZip)
        {
            return CreateZipFile(zipFileName, new FileSystemInfo[] { fileSystemInfoToZip });
        }

        /// <summary>
        /// A function that creates a zip file
        /// </summary>
        /// <param name="zipFileStoragePath">location where the file should be created</param>
        /// <param name="zipFileName">the filename of the zip file</param>
        /// <param name="fileSystemInfosToZip">an array of filesysteminfos that needs to be added to the file</param>
        /// <returns>a bool value that indicates whether the file was created</returns>
        private static bool CreateZipFile(string zipFilePath, FileSystemInfo[] fileSystemInfosToZip)
        {
            // a bool variable that says whether or not the file was created
            bool isCreated = false;

            try
            {
                //create our zip file
                ZipFile z = ZipFile.Create(zipFilePath);
                //initialize the file so that it can accept updates
                z.BeginUpdate();
                //get all the files and directory to zip
                GetFilesToZip(fileSystemInfosToZip, z);
                //commit the update once we are done
                z.CommitUpdate();
                //close the file
                z.Close();
                //success!
                isCreated = true;
            }
            catch (Exception ex)
            {
                //failed
                isCreated = false;
                //lets throw our error
                throw ex;
            }

            //return the creation status
            return isCreated;
        }

        /// <summary>
        /// Iterate thru all the filesysteminfo objects and add it to our zip file
        /// </summary>
        /// <param name="fileSystemInfosToZip">a collection of files/directores</param>
        /// <param name="z">our existing ZipFile object</param>
        private static void GetFilesToZip(FileSystemInfo[] fileSystemInfosToZip, ZipFile z)
        {
            //check whether the objects are null
            if (fileSystemInfosToZip != null && z != null)
            {
                

                //iterate thru all the filesystem info objects
                foreach (FileSystemInfo fi in fileSystemInfosToZip)
                {
                    // exporting to same dir we're zipping
                    if (!fi.Name.Equals("export"))
                    {
                        //check if it is a directory
                        if (fi is DirectoryInfo)
                        {
                            DirectoryInfo di = (DirectoryInfo)fi;
                            //add the directory
                            
                            //drill thru the directory to get all
                            //the files and folders inside it.
                            GetFilesToZip(di.GetFileSystemInfos(), z);
                        }
                        else
                        {
                            //add it
                            z.Add(fi.FullName);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create and write zip file entries to zip file
        /// </summary>
        /// <param name="filesToZip"></param>
        /// <param name="zipFilePath"></param>
        public static void zipReportFiles(string[] filesToZip, string zipFilePath)
        {
            int sourceBytes = 0;
            ZipOutputStream s = new ZipOutputStream(File.Open(zipFilePath, FileMode.Create));

            try
            {
                s.SetLevel(9); // 0 - store only to 9 - means best compression

                foreach (string file in filesToZip)
                {
                    FileStream fs = File.OpenRead(file);

                    byte[] buffer = new byte[fs.Length];

                    sourceBytes = fs.Read(buffer, 0, buffer.Length);

                    ZipEntry entry = new ZipEntry(Path.GetFileName(file));

                    entry.DateTime = DateTime.Now;
                    entry.Size = fs.Length;
                    fs.Close();

                    s.PutNextEntry(entry);

                    s.Write(buffer, 0, sourceBytes);

                }

                s.Finish();
                s.Close();

            }
            catch (Exception exc)
            {

                throw new Exception("Error zipping report\r\n", exc);

            }
            finally
            {
                s.Close();
            }
        }
        

        


    }
}
