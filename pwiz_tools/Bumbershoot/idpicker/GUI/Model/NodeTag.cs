using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IdPickerGui.MODEL
{
    public class NodeTag
    {
        private string typeDesc;            

        private bool allowSelection;        
        private string databasePath;
        private string groupName;
        private string fullPath;


        public string TypeDesc
        {
            get { return typeDesc; }
            set { typeDesc = value; }

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
        public string DatabasePath
        {
            get { return databasePath; }
            set { databasePath = value; }
        }
        public bool AllowSelection
        {
            get { return allowSelection; }
            set { allowSelection = value; }

        }
        public string GroupName
        {
            get { return groupName; }
            set { groupName = value; }
        }
        public string FullPath
        {
            get { return fullPath; }
            set { fullPath = value; }
        }

        // file node
        public NodeTag(string typeDescription, string path, string dbFileName, bool allowSel)
        {
            FullPath = path;
            TypeDesc = typeDescription;
            AllowSelection = allowSel;
            DatabasePath = dbFileName;
        }

        // root or group node
        public NodeTag(string typeDescription, string path, bool allowSel)
        {
            TypeDesc = typeDescription;
            FullPath = path;
            AllowSelection = allowSel;
        }

        public NodeTag()
        {

        }
       
    }
}
