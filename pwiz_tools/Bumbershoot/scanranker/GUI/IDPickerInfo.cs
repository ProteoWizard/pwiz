using System;

namespace ScanRanker
{
    public class IDPickerInfo
    {
        public string PepXMLFileDir;
        public string PepXMLFile;
        public string DBFile;
        public string DecoyPrefix;
        public double MaxFDR;
        public string ScoreWeights;
        public int NormalizeSearchScores;
        public int OptimizeScoreWeights;

        //public IDPickerInfo()
        //{
        //    PepXMLFile = string.Empty;
        //    DBFile = string.Empty;
        //    DecoyPrefix = "rev_";
        //    MaxFDR = 0.02;
        //    ScoreWeights = string.Empty;
        //    NormalizeSearchScores = 1;
        //    OptimizeScoreWeights = 1;
        //}
    }
}
