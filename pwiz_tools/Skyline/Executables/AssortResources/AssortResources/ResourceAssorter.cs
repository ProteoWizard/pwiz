using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace AssortResources
{
    public class ResourceAssorter
    {
        private Dictionary<string, HashSet<string>> _referencedResourcesByFolder = new();

        public ResourceAssorter(ResourceIdentifiers resourceIdentifiers)
        {
            ResourceIdentifiers = resourceIdentifiers;
        }

        public ResourceIdentifiers ResourceIdentifiers { get; }

        public IEnumerable<string> GetReferencedResourcesInFolder(string directoryName)
        {
            return GetSourceFilesInPath(directoryName).SelectMany(sourceFile =>
                ResourceIdentifiers.GetReferencedNames(File.ReadAllText(sourceFile)));
        }

        public IEnumerable<string> GetSourceFilesInPath(string path)
        {
            return Directory.GetFiles(path, "*.cs");
        }

        public void ProcessRootFolder(string rootFolderPath)
        {
            ProcessFolder(rootFolderPath);
            var uniqueReferences = new Dictionary<string, List<string>>();
            foreach (var resourceName in ResourceIdentifiers.Identifiers)
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
                    if (!uniqueReferences.TryGetValue(foundFolderPath, out var list))
                    {
                        list = new List<string>();
                        uniqueReferences.Add(foundFolderPath, list);
                    }
                    list.Add(resourceName);
                }
            }

            foreach (var entry in uniqueReferences)
            {
                Console.Out.WriteLine("{0}:{1}", entry.Key, string.Join(",", entry.Value));
            }
        }

        public void ProcessFolder(string folderPath)
        {
            var referencedResources = GetReferencedResourcesInFolder(folderPath).ToHashSet();
            if (referencedResources.Count > 0)
            {
                _referencedResourcesByFolder.Add(folderPath, referencedResources);
            }
        }

        public void FindResourceReferences(string folderPath)
        {
            ProcessFolder(folderPath);
            foreach (var subfolder in Directory.GetDirectories(folderPath))
            {
                FindResourceReferences(subfolder);
            }
        }

        public IEnumerable<string> GetFoldersWithResourceReferences()
        {
            return _referencedResourcesByFolder.Keys;
        }
    }
}
