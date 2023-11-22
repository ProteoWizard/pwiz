using System.Collections.Generic;
using System.Linq;

namespace pwiz.Skyline.Model.Databinding
{
    public class SurrogateStandardDataGridViewColumn : BoundComboBoxColumn
    {
        protected override object[] GetDropdownItems()
        {
            var document = SkylineDataSchema.Document;
            var items = new List<object> { string.Empty };
            items.AddRange(document.Settings.GetPeptideStandards(StandardType.SURROGATE_STANDARD).Select(peptide=>peptide.PeptideDocNode.ModifiedTarget.InvariantName).Distinct());
            return items.ToArray();
        }
    }
}
