using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Util
{
    public class Error
    {
        public Error(String component, String message, Exception exception)
        {
            DateTime = DateTime.Now;
            Component = component;
            Message = message;
            if (exception != null)
            {
                Detail = exception.ToString();
            }
        }
        public DateTime DateTime { get; private set; }
        public String Component { get; private set; }
        public String Message { get; private set; }
        public String Detail { get; private set; }
    }
}
