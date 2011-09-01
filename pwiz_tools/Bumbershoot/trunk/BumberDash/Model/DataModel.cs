using System;
using System.Collections.Generic;

namespace BumberDash.Model
{
    //Utility Classes
    public static class JobType
    {
        public const string Database = "Database Search";
        public const string Tag = "Tag Sequencing";
        public const string Library = "Spectral Library";
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
