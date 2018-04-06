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
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BumberDash.Model
{
    //Utility Classes
    public static class JobType
    {
        public const string Myrimatch = "MyriMatch: Database Searching";
        public const string Tag = "DirecTag / TagRecon: Sequence Tagging";
        public const string Library = "Pepitome: Spectral Library";
        public const string Comet = "Comet: Database Searching";
        public const string MSGF = "MS-GF+: Database Searching";
    }

    //Mapped Classes
    public class ConfigFile
    {
        public virtual int ConfigId { get; set; }
        public virtual string Name { get; set; }
        public virtual string DestinationProgram { get; set; }
        public virtual string FilePath { get; set; }
        public virtual DateTime FirstUsedDate { get; set; }
        public virtual IList<HistoryItem> UsedByList { get; set; } //List of HistoryItems in which ConfigFile is main config
        public virtual IList<HistoryItem> UsedByList2 { get; set; } //List of HistoryItems in which ConfigFile is second (TagRecon) config
        public virtual IList<ConfigProperty> PropertyList { get; set; }

        public override string ToString()
        {
            return DestinationProgram + " - " + Name;
        }

        public virtual string GetDescription()
        {
            try
            {
                if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                {
                    var fileIn = new StreamReader(FilePath);
                    return fileIn.ReadToEnd();
                }
                var sb = new StringBuilder();
                foreach (var property in PropertyList)
                    sb.AppendLine(property.Name + " = " + property.Value);
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public class ConfigProperty
    {
        public virtual int PropertyId { get; set; }
        public virtual string Name { get; set; }
        public virtual string Value { get; set; }
        public virtual string Type { get; set; }
        public virtual ConfigFile ConfigAssociation { get; set; }
    }

    public class HistoryItem
    {
        public virtual int HistoryItemId { get; set; }
        public virtual int RowNumber { get; set; }
        public virtual int Cpus { get; set; }
        public virtual string JobName { get; set; }
        public virtual string JobType { get; set; }
        public virtual ConfigFile InitialConfigFile { get; set; }
        public virtual ConfigFile TagConfigFile { get; set; }
        public virtual IList<InputFile> FileList { get; set; }
        public virtual string ProteinDatabase { get; set; }
        public virtual string SpectralLibrary { get; set; }
        public virtual string OutputDirectory { get; set; }
        public virtual DateTime? StartTime { get; set; }
        public virtual DateTime? EndTime { get; set; }
        public virtual string CurrentStatus { get; set; }
    }

    public class InputFile
    {
        public virtual int InputFileId { get; set; }
        public virtual HistoryItem HistoryItem { get; set; }
        public virtual string FilePath { get; set; }
    }



}
