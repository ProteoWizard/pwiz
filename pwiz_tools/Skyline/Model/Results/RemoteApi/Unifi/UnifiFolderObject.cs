using Newtonsoft.Json.Linq;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiFolderObject : UnifiObject
    {
        public string Path { get; private set; }
        public string FolderType { get; private set; }
        public string ParentId { get; private set; }

        public UnifiFolderObject(JObject jobject)
        {
            Id = GetProperty(jobject, "id");
            Name = GetProperty(jobject, "name");
            Path = GetProperty(jobject, "path");
            FolderType = GetProperty(jobject, "folderType");
            ParentId = GetProperty(jobject, "parentId");
            if (string.IsNullOrEmpty(ParentId))
            {
                ParentId = null;
            }
        }
    }
}
