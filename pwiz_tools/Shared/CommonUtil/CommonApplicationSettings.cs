using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Common
{
    public static class CommonApplicationSettings
    {
        private static string _programNameAndVersion;
        public static bool Offscreen { get; set; }
        public static bool FunctionalTest { get; set; }
        public static bool ShowFormNames { get; set; }

        public static string ProgramName { get; set; }

        public static string ProgramNameAndVersion
        {
            get
            {
                return _programNameAndVersion ?? ProgramName;
            }
            set
            {
                _programNameAndVersion = value;
            }
        }
    }
}
