using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedAutoQcBatch
{
    // Validates a string variable, throws ArgumentException if invalid
    public delegate void Validator(string variable);

    // UserControl interface to validate value of an input
    public interface IValidatorControl
    {
        object GetVariable();

        // Uses Validator to determine if variable is valid
        bool IsValid(out string errorMessage);
    }
}
