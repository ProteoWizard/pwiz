using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;

namespace MSStatArgsCollector
{
    public class DataSetInfo
    {
        public IList<string> GroupNames { get; private set; }
        public bool HasGlobalStandards { get; private set; }
        public bool HasQValues { get; private set; }
        public bool HasMs1 { get; private set; }
        public bool HasMs2 { get; private set; }

        public static DataSetInfo ReadDataSet(TextReader report)
        {
            const string conditionColumnName = "Condition"; // Not L10N
            var parser = new TextFieldParser(report);
            parser.SetDelimiters(",");
            string[] fields = parser.ReadFields() ?? new string[0];
            int groupIndex = Array.IndexOf(fields, conditionColumnName);
            int isMs1Index = Array.IndexOf(fields, "TransitionResultIsMs1");
            int standardTypeIndex = Array.IndexOf(fields, "StandardType");
            int qValueIndex = Array.IndexOf(fields, "DetectionQValue");
            bool hasMs1 = false;
            bool hasMs2 = false;
            bool hasGlobalStandards = false;
            bool hasQValues = false;

            if (groupIndex < 0)
            {
                throw new InvalidDataException(string.Format(
                    MSstatsResources.MSstatsGroupComparisonCollector_CollectArgs_Unable_to_find_a_column_named___0__,
                    conditionColumnName));
            }

            ICollection<string> groups = new HashSet<string>();
            string[] line;
            while ((line = parser.ReadFields()) != null)
            {
                groups.Add(line[groupIndex]);
                if (isMs1Index >= 0)
                {
                    var strIsMs1 = line[isMs1Index];
                    if (bool.TryParse(strIsMs1, out bool isMs1))
                    {
                        hasMs1 = hasMs1 || isMs1;
                        hasMs2 = hasMs2 || !isMs1;
                    }
                }

                if (standardTypeIndex >= 0)
                {
                    hasGlobalStandards = hasGlobalStandards || "Normalization".Equals(line[standardTypeIndex]);
                }

                if (qValueIndex >= 0)
                {
                    hasQValues = hasQValues || !string.IsNullOrEmpty(line[qValueIndex]);
                }
            }

            return new DataSetInfo()
            {
                GroupNames =
                    new ReadOnlyCollection<string>(groups.OrderBy(g => g, StringComparer.InvariantCultureIgnoreCase)
                        .ToList()),
                HasMs1 = hasMs1,
                HasMs2 = hasMs1,
                HasGlobalStandards = hasGlobalStandards,
                HasQValues = hasQValues
            };
        }
    }
}
