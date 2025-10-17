using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public class DocumentRowSources
    {
        public DocumentRowSources(SkylineDataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }
        
        public SkylineDataSchema DataSchema { get; }
    }
}
