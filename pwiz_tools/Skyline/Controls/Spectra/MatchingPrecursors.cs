using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Controls.Spectra
{
    public class MatchingPrecursors : SkylineObject, ILinkValue
    {
        private MatchingPrecursors(IEnumerable<PrecursorClass> precursors)
        {
            Precursors = ImmutableList.ValueOf(precursors);
        }

        public static MatchingPrecursors FromPrecursors(IEnumerable<PrecursorClass> precursors)
        {
            var list = ImmutableList.ValueOf(precursors);
            if (list.Count == 0)
            {
                return null;
            }
            return new MatchingPrecursors(list);
        }

        protected override SkylineDataSchema GetDataSchema()
        {
            return Precursors[0].DataSchema;
        }

        public IList<PrecursorClass> Precursors { get; private set; }

        EventHandler ILinkValue.ClickEventHandler
        {
            get
            {
                return LinkValueOnClick;
            }
        }

        object ILinkValue.Value
        {
            get { return this; }
        }

        public void LinkValueOnClick(object sender, EventArgs args)
        {
            var skylineWindow = DataSchema.SkylineWindow;
            if (null == skylineWindow)
            {
                return;
            }

            var identityPaths = Precursors.SelectMany(precursor => precursor.GetIdentityPaths()).Distinct().ToList();
            if (identityPaths.Count > 0)
            {
                skylineWindow.SequenceTree.SelectPath(identityPaths[0]);
            }
            if (identityPaths.Count > 1) 
            {
                skylineWindow.SequenceTree.SelectedPaths = identityPaths;
            }
        }

        public override string ToString()
        {
            if (Precursors.Count == 1)
            {
                return Precursors[0].ToString();
            }
            return string.Format(SpectraResources.MatchingPrecursors_ToString__0__precursors, Precursors.Count);
        }
    }
}
