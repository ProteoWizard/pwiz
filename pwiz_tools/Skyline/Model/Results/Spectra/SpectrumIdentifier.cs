using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumIdentifier
    {
        public SpectrumIdentifier(int index, string id)
        {

        }

        public int Index { get; private set; }
        public string Id { get; private set; }
    }
}
