using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NormalizeResxWhitespace
{
    /// <summary>
    /// Normalizes the whitespace in a .resx file.
    /// Performs the following changes: <ul>
    /// <li>Converts to UTF-8 no byte order mark</li>
    /// <li>Converts all tabs in top-level comments with two spaces</li>
    /// <li>Removes all text nodes and comment children from top-level elements</li>
    /// </ul>
    /// </summary>
    internal class Program
    {
        public static readonly XmlWriterSettings XmlWriterSettings = new XmlWriterSettings()
        {
            Indent = true,
            Encoding = new UTF8Encoding(false, true),

        };

        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                NormalizeWhitespace(arg);
            }
        }

        public static void NormalizeWhitespace(string path)
        {
            var document = XDocument.Load(path);
            var newNodes = new List<XNode>();
            foreach (var node in document.Root!.Nodes())
            {
                if (node is XComment comment)
                {
                    newNodes.Add(new XComment(comment.Value.Replace("\t", "  ")));
                    continue;
                }

                if (node is XElement element)
                {
                    element.ReplaceNodes(element.Elements());
                    newNodes.Add(element);
                }
            }
            document.Root.ReplaceNodes(newNodes.Cast<object>().ToArray());
            using var writer = XmlWriter.Create(path, XmlWriterSettings);
            document.Save(writer);
        }
    }
}
