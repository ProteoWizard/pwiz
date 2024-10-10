#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
#nullable enable

namespace AssortResources
{
    public class CsProjFile
    {
        public static CsProjFile FromProjFilePath(string projFile)
        {
            return new CsProjFile(projFile, XDocument.Load(projFile));
        }
        public CsProjFile(string filePath, XDocument document)
        {
            ProjFilePath = filePath;
            string projectFolder = Path.GetDirectoryName(filePath)!;
            if (!projectFolder.EndsWith("\\"))
            {
                projectFolder += "\\";
            }

            ProjectFolder = projectFolder;
            Document = document;
            foreach (var el in document.Root!.Elements(ElementName("ItemGroup")))
            {
                if (el.Elements(ElementName("Compile")).Any())
                {
                    MainItemGroup = el;
                    break;
                }
            }

            RootNamespace = GetRootNameSpace();
        }

        public string ProjFilePath { get; }
        public string ProjectFolder { get; } 

        private string GetRootNameSpace()
        {
            foreach (var elPropertyGroup in Document.Root!.Elements(ElementName("PropertyGroup")))
            {
                foreach (var el in elPropertyGroup.Elements(ElementName("RootNamespace")))
                {
                    return el.Value;
                }
            }

            return string.Empty;
        }

        public XDocument Document { get; }
        public string RootNamespace { get; }

        public XElement? MainItemGroup { get; }

        public IEnumerable<string> ListAllSourceFiles()
        {
            foreach (var itemGroup in Document.Root!.Elements(ElementName("ItemGroup")))
            {
                foreach (var compile in itemGroup.Elements(ElementName("Compile")))
                {
                    string? include = (string?)compile.Attribute("Include");
                    if (include != null)
                    {
                        yield return GetAbsolutePath(include);
                    }
                }
            }
        }

        public void AddResourceFile(string absoluteResourcePath, IEnumerable<string> languages)
        {
            var relativeResourcePath = GetRelativePath(absoluteResourcePath);
            string resourceName = Path.GetFileNameWithoutExtension(relativeResourcePath);
            string folder = Path.GetDirectoryName(relativeResourcePath) ?? string.Empty;
            var designerName = resourceName + ".Designer.cs";
            MainItemGroup!.Add(new XElement(ElementName("EmbeddedResource"),
                new XAttribute("Include", relativeResourcePath),
                new XElement(ElementName("Generator"), "PublicResXFileCodeGenerator"),
                new XElement(ElementName("SubType"), "Designer"),
                new XElement(ElementName("LastGenOutput"), designerName)));
            foreach (var language in languages.OrderBy(l=>l))
            {
                string languageFileName = Path.Combine(Path.GetDirectoryName(relativeResourcePath),
                    resourceName + "." + language + ".resx");
                MainItemGroup.Add(new XElement(ElementName("EmbeddedResource"),
                    new XAttribute("Include", languageFileName),
                    new XElement(ElementName("DependentUpon"), Path.GetFileName(relativeResourcePath)),
                    new XElement(ElementName("SubType"), "Designer")));
            }
            MainItemGroup.Add(new XElement(ElementName("Compile"),
                new XAttribute("Include", Path.Combine(folder, designerName)),
                new XElement(
                    ElementName("AutoGen"), "True"),
                new XElement(ElementName("DesignTime"), "True"),
                new XElement(ElementName("DependentUpon"), Path.GetFileName(relativeResourcePath))));
        }

        public static XName ElementName(string name)
        {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }

        public string GetAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(ProjectFolder, path);
        }

        public string GetRelativePath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                return path;
            }
            var baseUri = new Uri(ProjectFolder);
            var absoluteUri = new Uri(path);
            string relativePath = Uri.UnescapeDataString(baseUri.MakeRelativeUri(absoluteUri).ToString());
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        public string GetNamespace(string path)
        {
            var namespaceName = RootNamespace;
            string? folderName = Path.GetDirectoryName(GetRelativePath(path));
            if (!string.IsNullOrEmpty(folderName))
            {
                if (namespaceName.Length > 0)
                {
                    namespaceName += '.';
                }

                namespaceName += folderName.Replace('\\', '.');
            }

            return namespaceName;
        }
    }
}
