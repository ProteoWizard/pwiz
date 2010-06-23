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
using System.Text;

namespace IdPickerGui.MODEL
{
    public enum InputFileType
    {
        Unknown,
        PepXML,
        IdpXML
    }

    public class InputFileTag : ICloneable
    {
        private string typeDesc;            

        private bool allowSelection;        
        private string databasePath;
        private string groupName;
        private string fullPath;
        private bool passedChecks;
        private string errorMsg;
        private InputFileType fileType;

        /// <summary>
        /// root, dir, group, file
        /// </summary>
        public string TypeDesc
        {
            get { return typeDesc; }
            set { typeDesc = value; }

        }
        /// <summary>
        /// The database path read from the input file (pepxml)
        /// </summary>
        public string DatabasePath
        {
            get { return databasePath; }
            set { databasePath = value; }
        }
        /// <summary>
        /// Used to filter checkability of nodes based on
        /// some criteria..currently by selection of the database
        /// </summary>
        public bool AllowSelection
        {
            get { return allowSelection; }
            set { allowSelection = value; }

        }
        /// <summary>
        /// (Complete Path - Base Path).Replace("\\", "/")
        /// Start with /, file name is currently not a group level
        /// so last level of the group name is the bottom most dir
        /// the file is in
        /// </summary>
        public string GroupName
        {
            get { return groupName; }
            set { groupName = value; }
        }
        /// <summary>
        /// The complete path (dir or file) associated with the node
        /// </summary>
        public string FullPath
        {
            get { return fullPath; }
            set { fullPath = value; }
        }
        /// <summary>
        /// Passed checks in (checkAndSetFileNode in RunReportForm)
        /// </summary>
        public bool PassedChecks
        {
            get { return passedChecks; }
            set { passedChecks = value; }
        }
        /// <summary>
        /// Error msg built for file nodes (checkAndSetFileNode in RunReportForm)
        /// </summary>
        public string ErrorMsg
        {
            get { return errorMsg; }
            set { errorMsg = value; }
        }

        public InputFileType FileType
        {
            get { return fileType; }
            set { fileType = value; }
        }

        public bool IsGroup
        {
            get
            {
                switch (TypeDesc.ToLower())
                {
                    default:
                        return false;
                    case "group":
                        return true;
                }
            }


        }
        public bool IsRoot
        {
            get
            {
                switch (TypeDesc.ToLower())
                {
                    default:
                        return false;
                    case "root":
                        return true;
                }
            }


        }
        public bool IsFile
        {
            get
            {
                switch (TypeDesc.ToLower())
                {
                    default:
                        return false;
                    case "file":
                        return true;
                }
            }
        }
        public bool IsDir
        {
            get
            {
                switch (TypeDesc.ToLower())
                {
                    default:
                        return false;
                    case "dir":
                        return true;
                }
            }


        }


        public InputFileTag(string typeDescription, string filePath, string groupPath, string dbFileName, bool allowSel)
        {
            FullPath = filePath;
            GroupName = groupPath;
            TypeDesc = typeDescription;
            AllowSelection = allowSel;
            DatabasePath = dbFileName;
            PassedChecks = false;
        }

        public InputFileTag(string typeDescription, string path, bool allowSel)
        {
            TypeDesc = typeDescription;
            FullPath = path;
            AllowSelection = allowSel;
            PassedChecks = false;
        }

        public InputFileTag()
        {
            PassedChecks = false;
        }

        public object Clone()
        {
            try
            {
                InputFileTag tag = new InputFileTag();

                tag.TypeDesc = TypeDesc;
                tag.AllowSelection = AllowSelection;
                tag.DatabasePath = DatabasePath;
                tag.GroupName = GroupName;
                tag.FullPath = FullPath;
                tag.PassedChecks = PassedChecks;
                tag.ErrorMsg = ErrorMsg;

                return tag;

            }
            catch (Exception exc)
            {
                throw exc;
            }
        }
    }
}
