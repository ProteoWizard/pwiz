using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.DataBinding
{
    public class TopographDataSchema : DataSchema
    {
        public TopographDataSchema(Workspace workspace)
        {
            Workspace = workspace;
        }

        public Workspace Workspace { get; private set; }
    }
}
