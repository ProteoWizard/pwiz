using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AssortResources
{
    public class ResourceIdentifiers
    {
        private static Regex _regexWhitespaceDot =
            new Regex("\\s*\\.\\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public ResourceIdentifiers(string resourceFileName, IEnumerable<string> resourceNames)
        {
            ResourceFileName = resourceFileName;
            Identifiers = resourceNames.OrderByDescending(name => name.Length).ToList();
        }

        public static ResourceIdentifiers FromPath(string path)
        {
            return FromXDocument(Path.GetFileNameWithoutExtension(path), XDocument.Load(path));
        }

        public static ResourceIdentifiers FromXDocument(string resourceFilename, XDocument xDocument)
        {
            var resourceNames = xDocument.Root!.Elements("data").Select(el => (string?) el.Attribute("name")).OfType<string>();
            return new ResourceIdentifiers(resourceFilename, resourceNames);
        }


        public string ResourceFileName { get; }
        public IList<string> Identifiers { get; }

        public IEnumerable<string> GetReferencedNames(string code)
        {
            int ichLast = 0;
            while (true)
            {
                int ichNext = code.IndexOf(ResourceFileName, ichLast, StringComparison.Ordinal);
                if (ichNext < 0)
                {
                    break;
                }

                ichLast = ichNext + ResourceFileName.Length;
                var matchDot = _regexWhitespaceDot.Match(code, ichLast);
                if (!matchDot.Success)
                {
                    continue;
                }

                int ichIdentifierStart = ichLast + matchDot.Length;
                foreach (var name in Identifiers)
                {
                    if (code.Length <= ichIdentifierStart + name.Length)
                    {
                        continue;
                    }

                    string substring = code.Substring(ichIdentifierStart, name.Length);
                    if (Equals(name, substring))
                    {
                        yield return substring;
                        break;
                    }
                }
            }
        }
    }
}
