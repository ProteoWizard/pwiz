using System.Xml.Linq;

namespace AssortResources
{
    public class CsProjFile
    {
        public CsProjFile(XDocument document)
        {
            Document = document;
            foreach (var el in document.Root.Elements(ElementName("ItemGroup")))
            {
                if (el.Elements(ElementName("Compile")).Any())
                {
                    MainItemGroup = el;
                    break;
                }
            }
        }

        public XDocument Document { get; }

        public XElement MainItemGroup { get; }

        public IEnumerable<string> ListAllSourceFiles()
        {
            foreach (var itemGroup in Document.Root.Elements(ElementName("ItemGroup")))
            {
                foreach (var compile in itemGroup.Elements(ElementName("Compile")))
                {
                    string? include = (string?)compile.Attribute("Include");
                    if (include != null)
                    {
                        yield return include;
                    }
                }
            }
        }

        public void AddResourceFile(string relativeResourcePath)
        {
            string resourceName = Path.GetFileNameWithoutExtension(relativeResourcePath);
            string folder = Path.GetDirectoryName(relativeResourcePath) ?? string.Empty;
            var designerName = resourceName + ".Designer.cs";
            MainItemGroup.Add(new XElement(ElementName("EmbeddedResource"),
                new XAttribute("Include", relativeResourcePath),
                new XElement(ElementName("Generator"), "PublicResXFileCodeGenerator"),
                new XElement(ElementName("SubType"), "Designer"),
                new XElement(ElementName("LastGenOutput"), designerName)));
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
    }
}
