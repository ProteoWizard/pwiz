using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AssortResources
{
    public class ResourceIdentifiers
    {
        private static Regex _regexWhitespaceDot =
            new Regex("\\s*\\.\\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public ResourceIdentifiers(string filePath, string resourceFileName, XDocument document)
        {
            FilePath = filePath;
            ResourceFileName = resourceFileName;
            var resources = new Dictionary<string, XElement>();
            foreach (var element in document.Root!.Elements("data"))
            {
                string? type = (string?) element.Attribute("type");
                if (type != null && type.StartsWith("System.Resources.ResXFileRef"))
                {
                    continue;
                }
                string? name = (string?)element.Attribute("name");
                if (name != null)
                {
                    name = name.Replace(' ', '_');
                    if (!resources.ContainsKey(name))
                    {
                        resources.Add(name, element);
                    }
                }
            }

            Resources = resources;
        }

        public static ResourceIdentifiers FromPath(string path)
        {
            return new ResourceIdentifiers(path, Path.GetFileNameWithoutExtension(path), XDocument.Load(path));
        }

        public string FilePath { get; }
        public string ResourceFileName { get; }
        public Dictionary<string, XElement> Resources { get; }

        public IEnumerable<ResourceReference> GetResourceReferences(string code)
        {
            int ichPrev = 0;
            while (true)
            {
                int ichNext = code.IndexOf(ResourceFileName, ichPrev, StringComparison.Ordinal);
                if (ichNext < 0)
                {
                    break;
                }
                ichPrev = ichNext + ResourceFileName.Length;
                if (ichNext > 0 && IsIdentifierChar(code[ichNext - 1]))
                {
                    continue;
                }
                var matchDot = _regexWhitespaceDot.Match(code, ichPrev);
                if (!matchDot.Success)
                {
                    continue;
                }

                int ichIdentifierStart = ichPrev + matchDot.Length;
                int ichIdentifierEnd = ichIdentifierStart;
                while (ichIdentifierEnd < code.Length && IsIdentifierChar(code[ichIdentifierEnd]))
                {
                    ichIdentifierEnd++;
                }

                if (ichIdentifierEnd > ichIdentifierStart)
                {
                    string resourceIdentifier =
                        code.Substring(ichIdentifierStart, ichIdentifierEnd - ichIdentifierStart);
                    yield return new ResourceReference(ResourceFileName, ichNext, resourceIdentifier,
                        ichIdentifierStart);
                }
            }
        }

        public static bool IsIdentifierChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }

        public string ReplaceReferences(string code, string newResourcesName, HashSet<string> resourceIdentifiers)
        {
            StringBuilder newCode = new StringBuilder();
            int ichLast = 0;
            foreach (var resourceReference in GetResourceReferences(code))
            {
                if (!resourceIdentifiers.Contains(resourceReference.ResourceIdentifier))
                {
                    continue;
                }

                newCode.Append(code.Substring(ichLast, resourceReference.ResourceFileNameOffset - ichLast));
                newCode.Append(newResourcesName);
                ichLast = resourceReference.ResourceFileNameOffset + resourceReference.ResourceFileName.Length;
            }

            newCode.Append(code.Substring(ichLast));
            return newCode.ToString();
        }
    }
}
