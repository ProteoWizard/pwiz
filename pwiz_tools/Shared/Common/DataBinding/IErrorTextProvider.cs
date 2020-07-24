using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Common.DataBinding
{
    public interface IErrorTextProvider
    {
        string GetErrorText(string columnName);
    }
}
