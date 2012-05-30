using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class PeptideAnalysisStub : IEntity
    {
        public PeptideAnalysisStub(Workspace workspace, long id)
        {
            Workspace = workspace;
            Id = id;
        }
        [Browsable(false)]
        public Workspace Workspace { get; private set; }
        public long Id { get; private set; }
        public LinkValue<Peptide> Peptide { get; private set; }
        public ValidationStatus? Status { get; set; }
        public string Note { get; set; }
        public int DataFileCount { get; set; }
    }
}
