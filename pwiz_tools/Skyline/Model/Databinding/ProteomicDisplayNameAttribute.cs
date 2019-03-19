using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding
{
    public class ProteomicDisplayNameAttribute : InvariantDisplayNameAttribute
    {
        public ProteomicDisplayNameAttribute(string invariantDisplayName) : base(invariantDisplayName)
        {
            InUiMode = UiModes.PROTEOMIC;
        }
    }
}
