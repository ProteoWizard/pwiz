using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Filtering
{
    public interface IFilterAutoComplete
    {
        AutoCompleteStringCollection GetAutoCompleteValues(PropertyPath propertyPath);
    }
}
