using System.Xml.Linq;
using System.Xml.XPath;

namespace SortProteins
{
    public class ReportDefinition
    {
        private XDocument _document;

        public void ReadDefinition(Stream stream)
        {
            _document = XDocument.Load(stream);
        }

        public override string ToString()
        {
            return _document.ToString();
        }

        public void AddColumn(string name)
        {
            var colElement = new XElement("column");
            colElement.SetAttributeValue("name", name);
            _document.XPathSelectElement("/views/view")!.Add(colElement);
        }
    }
}
