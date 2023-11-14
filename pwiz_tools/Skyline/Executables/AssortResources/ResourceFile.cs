using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.Design.WebControls;

namespace AssortResources
{
    public class ResourceFile
    {
        public static ResourceFile FromPath(string path)
        {
            var resourceIdentifiers = ResourceIdentifiers.FromPath(path);
            var languages = new Dictionary<string, ResourceIdentifiers>();
            var baseName = Path.GetFileNameWithoutExtension(path) + ".";
            foreach (var filePath in Directory.EnumerateFiles(Path.GetDirectoryName(path), "*.resx"))
            {
                var fileName = Path.GetFileName(filePath);
                if (fileName.StartsWith(baseName) && fileName.Length > baseName.Length + 5)
                {
                    string language = fileName.Substring(baseName.Length, fileName.Length - baseName.Length - 5);
                    languages.Add(language, ResourceIdentifiers.FromPath(filePath));
                }
            }

            return new ResourceFile(resourceIdentifiers, languages);
        }

        public ResourceFile(ResourceIdentifiers resourceIdentifiers,
            Dictionary<string, ResourceIdentifiers> languages)
        {
            ResourceIdentifiers = resourceIdentifiers;
            Languages = languages;
        }

        public string FilePath
        {
            get { return ResourceIdentifiers.FilePath; }
        }
        public ResourceIdentifiers ResourceIdentifiers { get; }
        public Dictionary<string, ResourceIdentifiers> Languages { get; }
    }
}
