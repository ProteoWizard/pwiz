using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Controls.Databinding
{
    public interface IDataboundGridForm
    {
        string GetPersistentString();
        DataboundGridControl GetDataboundGridControl();
    }
}
