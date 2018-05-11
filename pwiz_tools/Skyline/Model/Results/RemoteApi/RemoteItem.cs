using System;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public class RemoteItem
    {
        public RemoteItem(MsDataFileUri msDataFileUri, string label, string type, DateTime? lastModified, long fileSizeBytes)
        {
            MsDataFileUri = msDataFileUri;
            Label = label;
            Type = type;
            LastModified = lastModified;
            FileSize = (ulong) fileSizeBytes;
        }

        public MsDataFileUri MsDataFileUri { get; private set; }
        public string Label { get; private set; }
        public string Type { get; private set; }
        public DateTime? LastModified { get; private set; }
        public ulong FileSize { get; private set; }
    }
}
