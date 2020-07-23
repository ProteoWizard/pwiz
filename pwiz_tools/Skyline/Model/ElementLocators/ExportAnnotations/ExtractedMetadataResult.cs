using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    public class ExtractedMetadataResultRow
    {
        public object SourceObject { get; private set; }
        public IDictionary<string, ExtractedMetadataResultColumn> Values { get; private set; }
        public IList<ExtractedMetadataRuleResult> RuleResults { get; private set; }
    }

    public class ExtractedMetadataResultColumn
    {
        public string Name { get; private set; }
        public object Value { get; private set; }
    }

    public class ExtractedMetadataRuleResult
    {
        public string Source { get; private set; }
        public bool Match { get; private set; }
        public string ExtractedText { get; private set; }
        public object Target { get; private set; }
    }
}
