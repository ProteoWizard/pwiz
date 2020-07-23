using System;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    public class MetadataExtractor
    {
        public MetadataExtractor(SkylineDataSchema dataSchema, Type sourceObjectType, ExtractedMetadataRuleSet ruleSet)
        {
            DataSchema = dataSchema;
            RuleSet = ruleSet;
        }

        public SkylineDataSchema DataSchema { get; private set; }
        public ExtractedMetadataRuleSet RuleSet { get; private set; }
    }
}
