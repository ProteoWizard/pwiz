using System.Collections;
using System.Collections.Generic;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline
{
    public class PeakAreaPointData
    {
        public PeakAreaPointData(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, IdentityPath identityPath)
        {
            NodePep = nodePep;
            NodeGroup = nodeGroup;
            IdentityPath = identityPath;

            CalcStats(nodePep, nodeGroup);
        }

        public PeakAreaPointData()
        {
        }
        public PeptideDocNode NodePep { get; set; }
        public TransitionGroupDocNode NodeGroup { get; set; }
        public IdentityPath IdentityPath { get; set; }
        public double AreaGroup { get; set; }
        //            public double AreaPepCharge { get; private set; }
        public double TimeGroup { get; private set; }
        public double MassErrorGroup { get; private set; }
        public double TimePepCharge { get; private set; }
        public double AreaCv { get; set; }

        // ReSharper disable SuggestBaseTypeForParameter
        public void CalcStats(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        // ReSharper restore SuggestBaseTypeForParameter
        {
            var times = new List<double>();
            foreach (TransitionGroupDocNode nodePepChild in nodePep.Children)
            {
                double? meanArea, meanTime, meanMassError, areaCv;
                CalcStats(nodePepChild, out meanArea, out meanTime, out meanMassError, out areaCv);
                if (!Equals(nodeGroup.TransitionGroup.PrecursorAdduct, nodePepChild.TransitionGroup.PrecursorAdduct))
                    continue;
                if (meanTime.HasValue)
                    times.Add(meanTime.Value);
                if (ReferenceEquals(nodeGroup, nodePepChild))
                {
                    AreaGroup = meanArea ?? 0;
                    TimeGroup = meanTime ?? 0;
                    MassErrorGroup = meanMassError ?? 0;
                    AreaCv = areaCv ?? 0;
                }
            }
            //                AreaPepCharge = (areas.Count > 0 ? new Statistics(areas).Mean() : 0);
            TimePepCharge = (times.Count > 0 ? new Statistics(times).Mean() : 0);
        }

        private static void CalcStats(TransitionGroupDocNode nodeGroup, out double? meanArea, out double? meanTime, out double? meanMassError, out double? areaCv)
        {
            var areas = new List<double>();
            var times = new List<double>();
            var massErrors = new List<double>();
            foreach (var chromInfo in nodeGroup.ChromInfos)
            {
                if (chromInfo.Area.HasValue)
                    areas.Add(chromInfo.Area.Value);
                if (chromInfo.RetentionTime.HasValue)
                    times.Add(chromInfo.RetentionTime.Value);
                if (chromInfo.MassError.HasValue)
                    massErrors.Add(chromInfo.MassError.Value);
            }
            meanArea = null;
            areaCv = null;
            if (areas.Count > 0)
            {
                var statAreas = new Statistics(areas);
                meanArea = statAreas.Mean();
                areaCv = statAreas.StdDev() / meanArea;
            }
            meanTime = null;
            if (times.Count > 0)
                meanTime = new Statistics(times).Mean();
            meanMassError = null;
            if (massErrors.Count > 0)
                meanMassError = new Statistics(massErrors).Mean();
        }

        public static int CompareGroupAreas(PeakAreaPointData p1, PeakAreaPointData p2)
        {
            return Comparer.Default.Compare(p2.AreaGroup, p1.AreaGroup);
        }
    }
}