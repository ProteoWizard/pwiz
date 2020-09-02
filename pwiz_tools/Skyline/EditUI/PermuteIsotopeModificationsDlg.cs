using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class PermuteIsotopeModificationsDlg : FormEx
    {
        private ImmutableList<StaticMod> _heavyModifications;
        public PermuteIsotopeModificationsDlg(IEnumerable<StaticMod> heavyModifications)
        {
            InitializeComponent();
            _heavyModifications = ImmutableList.ValueOf(heavyModifications);
            comboIsotopeModification.Items.AddRange(_heavyModifications.Select(mod=>mod.Name).ToArray());
            if (comboIsotopeModification.Items.Count > 0)
            {
                comboIsotopeModification.SelectedIndex = 0;
            }
        }

        public IDocumentUIContainer DocumentContainer { get; private set; }

        public bool SimplePermutation
        {
            get
            {
                return radioButtonSimplePermutation.Checked;
            }
            set
            {
                if (value)
                {
                    radioButtonSimplePermutation.Checked = true;
                }
                else
                {
                    radioButtonComplexPermutation.Checked = true;
                }
            }
        }

        public StaticMod IsotopeModification
        {
            get
            {
                if (comboIsotopeModification.SelectedIndex < 0)
                {
                    return null;
                }

                return _heavyModifications[comboIsotopeModification.SelectedIndex];
            }
        }

        public SrmDocument PermuteDocument(SrmDocument document)
        {
            var permuter = new IsotopeModificationPermutationGenerator(IsotopeModification, SimplePermutation, document);
            return permuter.GetNewDocument();
        }

        public AuditLogEntry GetAuditLogEntry(SrmDocumentPair docPair)
        {
            return AuditLogEntry.CreateSimpleEntry(MessageType.permuted_isotope_label, docPair.NewDocumentType,
                IsotopeModification.Name, SimplePermutation);
        }
    }
}
