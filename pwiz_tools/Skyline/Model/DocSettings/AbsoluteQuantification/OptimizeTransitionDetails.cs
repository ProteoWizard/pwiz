using System;
using pwiz.Common.SystemUtil;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class OptimizeTransitionDetails
    {
        public OptimizeTransitionDetails()
        {
            SingleQuantLimits = new List<TransitionsQuantLimit>();
            AcceptedQuantLimits = new List<TransitionsQuantLimit>();
            RejectedQuantLimits = new List<TransitionsQuantLimit>();
        }
        public TransitionsQuantLimit Original { get; set; }
        public List<TransitionsQuantLimit> SingleQuantLimits { get; }
        public List<TransitionsQuantLimit> AcceptedQuantLimits { get; }
        public List<TransitionsQuantLimit> RejectedQuantLimits { get; }

        public TransitionsQuantLimit Optimized
        {
            get
            {
                return AcceptedQuantLimits.OrderByDescending(q => q.TransitionIdentityPaths.Count).FirstOrDefault();
            }
        }
    }

    public class OptimizeTransitionSettings : Immutable
    {
        public static readonly OptimizeTransitionSettings DEFAULT = new OptimizeTransitionSettings
        {
            OptimizeType = OptimizeType.LOQ,
            MinimumNumberOfTransitions = 5,
        };

        private static OptimizeTransitionSettings _globalSettings = DEFAULT;
        public static OptimizeTransitionSettings GlobalSettings
        {
            get
            {
                return _globalSettings;
            }
            set
            {
                if (!Equals(value, _globalSettings))
                {
                    _globalSettings = value;
                    GlobalSettingsChange?.Invoke();
                }
            }
        }
        public static event Action GlobalSettingsChange;


        private OptimizeTransitionSettings()
        {
            
        }
        public int RandomSeed { get; private set; }

        public OptimizeTransitionSettings ChangeRandomSeed(int value)
        {
            return ChangeProp(ImClone(this), im => im.RandomSeed = value);
        }

        public int MinimumNumberOfTransitions { get; private set; }

        public OptimizeTransitionSettings ChangeMinimumNumberOfTransitions(int value)
        {
            return ChangeProp(ImClone(this), im => im.MinimumNumberOfTransitions = value);
        }

        public OptimizeType OptimizeType { get; private set; }

        public OptimizeTransitionSettings ChangeOptimizeType(OptimizeType value)
        {
            return ChangeProp(ImClone(this), im => im.OptimizeType = value);
        }

        public bool PreserveNonQuantitative { get; private set; }

        public OptimizeTransitionSettings ChangePreserveNonQuantitative(bool value)
        {
            return ChangeProp(ImClone(this), im => im.PreserveNonQuantitative = value);
        }

        public bool CombinePointsWithSameConcentration { get; private set; }

        public OptimizeTransitionSettings ChangeCombinePointsWithSameConcentration(bool value)
        {
            return ChangeProp(ImClone(this), im => im.CombinePointsWithSameConcentration = value);
        }
        protected bool Equals(OptimizeTransitionSettings other)
        {
            return RandomSeed == other.RandomSeed && 
                   MinimumNumberOfTransitions == other.MinimumNumberOfTransitions &&
                   OptimizeType.Equals(other.OptimizeType) &&
                   PreserveNonQuantitative == other.PreserveNonQuantitative && 
                   CombinePointsWithSameConcentration == other.CombinePointsWithSameConcentration;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((OptimizeTransitionSettings)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RandomSeed;
                hashCode = (hashCode * 397) ^ MinimumNumberOfTransitions;
                hashCode = (hashCode * 397) ^ OptimizeType.GetHashCode();
                hashCode = (hashCode * 397) ^ PreserveNonQuantitative.GetHashCode();
                hashCode = (hashCode * 397) ^ CombinePointsWithSameConcentration.GetHashCode();
                return hashCode;
            }
        }

        public CalibrationCurveFitter GetCalibrationCurveFitter(PeptideQuantifier peptideQuantifier, SrmSettings srmSettings)
        {
            return new CalibrationCurveFitter(peptideQuantifier, srmSettings)
            {
                CombinePointsWithSameConcentration = CombinePointsWithSameConcentration
            };
        }

        public QuantificationSettings GetQuantificationSettings(SrmSettings settings)
        {
            var quantificationSettings = settings.PeptideSettings.Quantification;
            quantificationSettings = quantificationSettings.ChangeLodCalculation(LodCalculation.TURNING_POINT_STDERR)
                .ChangeMaxLoqBias(null).ChangeRegressionFit(RegressionFit.BILINEAR);
            if (!quantificationSettings.MaxLoqCv.HasValue)
            {
                quantificationSettings = quantificationSettings.ChangeMaxLoqCv(20);
            }

            return quantificationSettings;
        }
    }
}
