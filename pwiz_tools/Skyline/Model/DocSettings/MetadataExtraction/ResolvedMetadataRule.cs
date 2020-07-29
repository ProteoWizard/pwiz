using System.Text.RegularExpressions;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;

namespace pwiz.Skyline.Model.DocSettings.MetadataExtraction
{
    public class ResolvedMetadataRule
    {
        public static readonly ResolvedMetadataRule EMPTY = new ResolvedMetadataRule(MetadataRule.EMPTY, null, null, null, null);
        public ResolvedMetadataRule(MetadataRule def, TextColumnWrapper source, Regex regex, string replacement, TextColumnWrapper target)
        {
            Def = def;
            Source = source;
            Regex = regex;
            Replacement = replacement;
            Target = target;
        }

        public MetadataRule Def { get; private set; }
        public TextColumnWrapper Source { get; private set; }

        public string Replacement { get; private set; }
        public Regex Regex { get; private set; }
        public TextColumnWrapper Target { get; private set; }
    }
}