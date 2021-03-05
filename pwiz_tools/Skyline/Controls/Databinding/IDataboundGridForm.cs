using pwiz.Skyline.Model.Databinding;

namespace pwiz.Skyline.Controls.Databinding
{
    public interface IDataboundGridForm
    {
        DataGridId DataGridId { get; }
        DataboundGridControl GetDataboundGridControl();
    }
}
