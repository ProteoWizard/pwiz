using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AssortResources
{
    public class ResourceAssorter
    {
        private Dictionary<string, HashSet<string>> _referencedResourcesByFolder = new();

        public ResourceAssorter(string rootFolder, string csProjPath, string resourceFilePath)
        {
            RootFolder = rootFolder;
            CsProjPath = csProjPath;
            ResourceFilePath = resourceFilePath;
            ResourceIdentifiers = ResourceIdentifiers.FromPath(ResourceFilePath);
            CsProjFile = new CsProjFile(XDocument.Load(CsProjPath));
        }

        public string RootFolder { get; }
        public string CsProjPath { get; }
        public string ResourceFilePath { get; }

        public CsProjFile CsProjFile { get; }

        public ResourceIdentifiers ResourceIdentifiers { get; }

        public IEnumerable<string> GetReferencedResourcesInFolder(IEnumerable<string> files)
        {
            var result = new HashSet<string>();
            foreach (var file in files)
            {
                foreach (var reference in ResourceIdentifiers.GetResourceReferences(File.ReadAllText(Path.Combine(RootFolder, file))))
                {
                    result.Add(reference.ResourceIdentifier);
                }
                
            }

            return result;
        }

        public IEnumerable<string> GetSourceFilesInPath(string path)
        {
            return Directory.GetFiles(path, "*.cs");
        }

        public void DoWork()
        {
            var folders = CsProjFile.ListAllSourceFiles()
                .ToLookup(path => Path.GetDirectoryName(path) ?? string.Empty);
            foreach (var folder in folders)
            {
                ProcessFolder(folder);
            }
            var uniqueReferences = new Dictionary<string, List<string>>();
            foreach (var resourceName in ResourceIdentifiers.Resources.Keys)
            {
                int count = 0;
                string foundFolderPath = null;
                foreach (var entry in _referencedResourcesByFolder)
                {
                    if (entry.Value.Contains(resourceName))
                    {
                        count++;
                        if (count == 1)
                        {
                            foundFolderPath = entry.Key;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (count == 1)
                {
                    if (!uniqueReferences.TryGetValue(foundFolderPath!, out var list))
                    {
                        list = new List<string>();
                        uniqueReferences.Add(foundFolderPath!, list);
                    }
                    list.Add(resourceName);
                }
            }

            foreach (var entry in uniqueReferences)
            {
                MakeResourceFile(entry.Key, folders[entry.Key], entry.Value.ToHashSet());
            }
            WriteDocument(CsProjFile.Document, CsProjPath);
        }

        public void ProcessFolder(IGrouping<string, string> folder)
        {
            var referencedResources = GetReferencedResourcesInFolder(folder).ToHashSet();
            if (referencedResources.Count > 0)
            {
                _referencedResourcesByFolder.Add(folder.Key, referencedResources);
            }
        }

        public void MakeResourceFile(string folderPath, IEnumerable<string> files, HashSet<string> resourceIdentifiers)
        {
            string folderName;
            if (string.IsNullOrEmpty(folderPath))
            {
                folderName = Path.GetFileNameWithoutExtension(RootFolder);
            }
            else
            {
                folderName = Path.GetFileNameWithoutExtension(folderPath);
            }
            var newResourcesName = folderName + ResourceIdentifiers.ResourceFileName;
            var resourceFilePath = Path.Combine(folderPath, newResourcesName + ".resx");
            XDocument document;
            var absoluteResourceFilePath = Path.Combine(RootFolder, resourceFilePath);
            if (File.Exists(absoluteResourceFilePath))
            {
                document = XDocument.Load(absoluteResourceFilePath);
            }
            else
            {
                document = GetBlankResourceDocument();
                CsProjFile.AddResourceFile(resourceFilePath);
            }

            var names = document.Root!.Elements("data").Select(e => (string?)e.Attribute("name")).OfType<string>()
                .ToHashSet();
            foreach (var resourceIdentifier in resourceIdentifiers.OrderBy(x=>x))
            {
                if (names.Add(resourceIdentifier))
                {
                    document.Root.Add(ResourceIdentifiers.Resources[resourceIdentifier]);
                }
            }
            WriteDocument(document, absoluteResourceFilePath);
            foreach (var sourceFile in files)
            {
                var absoluteSourceFile = Path.Combine(RootFolder, sourceFile);
                var code = File.ReadAllText(absoluteSourceFile);
                var newCode = ResourceIdentifiers.ReplaceReferences(code, newResourcesName, resourceIdentifiers);
                if (newCode != code)
                {
                    File.WriteAllText(absoluteSourceFile, newCode, Encoding.UTF8);
                }
            }
        }

        public static XDocument GetBlankResourceDocument()
        {
            var type = typeof(ResourceAssorter);
            using (var stream = type.Assembly.GetManifestResourceStream(type, "BlankResourcesFile.txt"))
            {
                return XDocument.Load(stream!);
            }
        }

        public void WriteDocument(XDocument document, string path)
        {
            using (var xmlWriter = XmlWriter.Create(path, new XmlWriterSettings()
                   {
                       Indent = true,
                       Encoding = new UTF8Encoding(false, true)
                   }))
            {
                document.Save(xmlWriter);
            }
        }
    }
}
