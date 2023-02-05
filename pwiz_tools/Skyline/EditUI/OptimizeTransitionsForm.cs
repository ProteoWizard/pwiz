using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;

namespace pwiz.Skyline.EditUI
{
    public partial class OptimizeTransitionsForm : DataboundGridForm
    {
        public OptimizeTransitionsForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
        }

        public SkylineWindow SkylineWindow { get; }

        public class Row
        {
            public Model.Databinding.Entities.Transition Transition { get; private set; }
            [ChildDisplayName("Single{0}")] public QuantLimit SingleQuantLimit { get; private set; }
            [ChildDisplayName("Accepted{0}")] public QuantLimit AcceptedQuantLimit { get; private set; }
            [ChildDisplayName("Rejected{0}")] public QuantLimit RejectedQuantLimit { get; private set; }

            public static Row CreateRow(Model.Databinding.Entities.Transition transition,
                OptimizeTransitionDetails details)
            {
                return new Row
                {
                    Transition = transition,
                    SingleQuantLimit = details.SingleQuantLimits.FirstOrDefault(tql =>
                        Equals(transition.IdentityPath, tql.TransitionIdentityPaths.Single()))?.QuantLimit,
                    AcceptedQuantLimit = details.AcceptedQuantLimits
                        .FirstOrDefault(tql => Equals(transition.IdentityPath, tql.TransitionIdentityPaths.Last()))
                        ?.QuantLimit,
                    RejectedQuantLimit = details.RejectedQuantLimits
                        .FirstOrDefault(tql => Equals(transition.IdentityPath, tql.TransitionIdentityPaths.Last()))
                        ?.QuantLimit
                };
            }
        }

        public OptimizeTransitionSettings Settings
        {
            get
            {
                return optimizeTransitionsSettingsControl1.CurrentSettings;
            }
            set
            {
                optimizeTransitionsSettingsControl1.CurrentSettings = value;
            }
        }
    }
}
    
