using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.Databinding
{
    public class TargetDataGridViewColumn : BoundComboBoxColumn
    {
        protected override object[] GetDropdownItems()
        {
            var document = SkylineDataSchema.Document;
            return document.Molecules.Select(molecule => molecule.Target.InvariantName)
                .OrderBy(value=>value, StringComparer.InvariantCultureIgnoreCase).ToArray();
        }
    }
}
