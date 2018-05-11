using System;
using Newtonsoft.Json.Linq;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiFileObject : UnifiObject
    {
        public UnifiFileObject(JObject jobject)
        {
            Id = GetProperty(jobject, "id");
            Name = GetProperty(jobject, "name");
            Type = GetProperty(jobject, "type");
            IdInFolder = GetIntegerProperty(jobject, "idInFolder");
            CreatedAt = GetDateProperty(jobject, "createdAt");
            ModifiedAt = GetDateProperty(jobject, "modifiedAt");
        }

        public string Type { get; private set; }
        public int? IdInFolder { get; private set; }
        public DateTime? CreatedAt { get; private set; }
        public DateTime? ModifiedAt { get; private set; }
    }
}
