//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s): Ze-Qiang Ma
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace IDPicker.DataModel
{
    /// <summary>
    /// Store linked spectral IDs of each spectrum
    /// </summary>
    public class LinkMap : Map<long, Set<long>>
    {
        public LinkMap()
        {
            MergedLinkList = new List<Set<long>>();            
        }
        
        public List<Set<long>> MergedLinkList {get; private set;}
        private Set<long> mergedIDs = new Set<long>();
        
        /// <summary>
        /// Go through each key(spectrumId) of a LinkMap instance
        /// Find linked spectra recursively and populate MergedLinkList, single linkage clustering
        /// </summary>
        public void GetMergedLinkList()
        {            
            Set<long> singleSet = new Set<long>();
            foreach (long k in this.Keys)
            {
                if (!mergedIDs.Contains(k))  //not precessed yet
                {
                    getAllLinks_R(singleSet, k);
                    MergedLinkList.Add(new Set<long>(singleSet));
                    singleSet.Clear();
                }
            }            

        }

        private void getAllLinks_R(Set<long> singleSet, long spectrumID)
        {
            if (!mergedIDs.Contains(spectrumID))  //not precessed yet
            {            
                Set<long> linkedSpectraIDs = new Set<long>();
                linkedSpectraIDs = this[spectrumID];
            
                mergedIDs.Insert(spectrumID);                
                singleSet.Union(linkedSpectraIDs);
                foreach (long id in linkedSpectraIDs)
                {
                    if (!mergedIDs.Contains(id))  //not precessed yet
                        getAllLinks_R(singleSet,id);                 
                }
            }            
        }

    }
    
    /// <summary>
    /// Peak m/z and intensity lists
    /// </summary>
    public class Peaks
    {
        public Peaks(IList<double> MZs, IList<double> intensities)
        {
            originalMZs = MZs;
            OriginalIntensities = intensities;
        }
        private IList<double> originalMZs;
        public IList<double> OriginalMZs
        {
            get { return (originalMZs); }
            set { originalMZs = value; }
        }

        private IList<double> originalIntensities;
        public IList<double> OriginalIntensities
        {
            get { return (originalIntensities); }
            set { originalIntensities = value; }

        }
                
    }

    public struct UpdateValues
    {
        public double ReassignedQvalue;
        public int ReassignedRank;

        public UpdateValues(double qvalue, int rank)
        {
            ReassignedQvalue = qvalue;
            ReassignedRank = rank;
        }

    }


    ///// <summary>
    ///// Store identified spectra and clustering scores for each peptide sequence
    ///// </summary>
    //public class PepInfo
    //{
    //    public PepInfo()
    //    {
    //        SpectrumIDs = new Set<string>();
    //        Score = 0;
    //    }

    //    public Set<string> SpectrumIDs { get; set; }
    //    public double Score { get; set; }
    //}




    ///// <summary>
    ///// Store identified spectra and clustering scores for each peptide sequence
    ///// </summary>
    //public class PepDictionary : Dictionary<string, PepInfo>
    //{

    //    private double sumAllSearchScores;  // sum of search scores for all peptide identifications
    //    private int numAllPSMs; // number of PSMs in this cluster, count all, not only unique

    //    /// <summary>
    //    /// Add a spectrum id to peptide id, compute summed search scores.
    //    /// </summary>
    //    /// <param name="pepId"></param>
    //    /// <param name="spectrumID"></param>
    //    /// <param name="searchScore"></param>
    //    public void Add(string pepId, long spectrumID, int charge, double searchScore)
    //    {
    //        if (this.ContainsKey(pepId))
    //        {
    //            this[pepId].SpectrumIDs.Add(spectrumID.ToString() + '.' + charge.ToString());
    //            this[pepId].Score += searchScore;
    //            sumAllSearchScores += searchScore;
    //            numAllPSMs++;
    //        }
    //        else
    //        {
    //            PepInfo pep = new PepInfo();
    //            pep.SpectrumIDs.Add(spectrumID.ToString() + '.' + charge.ToString());
    //            pep.Score += searchScore;
    //            this.Add(pepId, pep);
    //            sumAllSearchScores += searchScore;
    //            numAllPSMs++;
    //        }
    //    }

    //    /// <summary>
    //    /// Compute Bayesian Average score for each peptide, use 10 * maxVotes for a single peptide in this cluster as the constant
    //    /// </summary>
    //    public void ComputeBayesianAverage()
    //    {
    //        int maxVotes = this.Max(kvp => kvp.Value.SpectrumIDs.Count);
    //        double aveAllSearchScores = sumAllSearchScores / numAllPSMs;
    //        int C = maxVotes * 10;
    //        foreach (string pepId in this.Keys)
    //        {
    //            double sumSearchScores = this[pepId].Score;
    //            int numVotes = this[pepId].SpectrumIDs.Count;
    //            //this[pepId].Score = (sumAllSearchScores + sumSearchScores) / (numAllPSMs + numVotes);
    //            this[pepId].Score = (C * aveAllSearchScores + sumSearchScores) / (C + numVotes);
    //        }
    //    }

    //}

    ///// <summary>
    ///// Store psm ids and scores for each peptide id
    ///// </summary>
    //public class PepInfo
    //{
    //    public PepInfo()
    //    {
    //        PsmIds = new Set<long>();
    //        Score = 0;
    //    }

    //    public Set<long> PsmIds { get; set; }
    //    public double Score { get; set; }
    //}

    ///// <summary>
    ///// Store peptide Id and correspoinding psm Ids and scores
    ///// </summary>
    //public class PepDictionary : Dictionary<long, PepInfo>
    //{

    //    private double sumAllSearchScores;  // sum of search scores for all peptide identifications
    //    private int numAllPSMs; // number of PSMs in this cluster, count all, not only unique

    //    /// <summary>
    //    /// Add a psm id to peptide id, compute summed search scores.
    //    /// </summary>
    //    /// <param name="pepId"></param>
    //    /// <param name="psmId"></param>
    //    /// <param name="searchScore"></param>
    //    public void Add(long pepId, long psmId, double searchScore)
    //    {
    //        if (this.ContainsKey(pepId))
    //        {
    //            this[pepId].PsmIds.Add(psmId);
    //            this[pepId].Score += searchScore;
    //            sumAllSearchScores += searchScore;
    //            numAllPSMs++;
    //        }
    //        else
    //        {
    //            PepInfo pep = new PepInfo();
    //            pep.PsmIds.Add(psmId);
    //            pep.Score += searchScore;
    //            this.Add(pepId, pep);
    //            sumAllSearchScores += searchScore;
    //            numAllPSMs++;
    //        }
    //    }

    //    /// <summary>
    //    /// Compute Bayesian Average score for each peptide, use 10 * maxVotes for a single peptide in this cluster as the constant
    //    /// </summary>
    //    public void ComputeBayesianAverage()
    //    {
    //        int maxVotes = this.Max(kvp => kvp.Value.PsmIds.Count);
    //        double aveAllSearchScores = sumAllSearchScores / numAllPSMs;
    //        int C = maxVotes * 10;
    //        foreach (long pepId in this.Keys)
    //        {
    //            double sumSearchScores = this[pepId].Score;
    //            int numVotes = this[pepId].PsmIds.Count;
    //            //this[pepId].Score = (sumAllSearchScores + sumSearchScores) / (numAllPSMs + numVotes);
    //            this[pepId].Score = (C * aveAllSearchScores + sumSearchScores) / (C + numVotes);
    //        }
    //    }

    //}

    /// <summary>
    /// Store identified spectra and clustering scores for each peptide sequence
    /// </summary>
    public class PepInfo
    {
        public PepInfo()
        {
            PsmIdSpecDict = new Dictionary<long, string>();
            //PsmIds = new Set<long>();
            //SpecChargeAnalysisSetSet = new Set<string>();
            FinalScore = 0;
            SearchScores = new Dictionary<long, List<double>>();
            BAScores = new Dictionary<long, double>();
            
        }

        //public Set<long> PsmIds { get; set; }
        //public Set<string> SpecChargeAnalysisSet { get; set; }
        public Dictionary<long, string> PsmIdSpecDict { get; set; }
        public double FinalScore { get; set; }
        public Dictionary<long, List<double>> SearchScores { get; set; } //key: analysis Id, value: search scores
        public Dictionary<long, double> BAScores { get; set; } //key: analysis Id, value: Baysian average scores
    }
    /// <summary>
    /// Store identified spectra and clustering scores for each peptide sequence
    /// </summary>
    public class PepDictionary : Dictionary<string, PepInfo>
    {

        //private double sumAllSearchScores;  // sum of search scores for all peptide identifications
        //private int numAllPSMs; // number of PSMs in this cluster, count all, not only unique
        private Dictionary<long, double> sumAllSearchScoresDict = new Dictionary<long,double>();
        private Dictionary<long, int> numAllPsmsDict = new Dictionary<long,int>();
        private Dictionary<long, int> maxVotes = new Dictionary<long, int>();

        /// <summary>
        /// Add a spectrum id to peptide id, compute summed search scores.
        /// </summary>
        /// <param name="pepSeq"></param>
        /// <param name="spectrumID"></param>
        /// <param name="charge"></param>
        /// <param name="analysisId"></param>
        /// <param name="searchScore"></param>
        public void Add(string pepSeq, long spectrumID, int charge, long analysisId, double searchScore, long psmId)
        {
            if (sumAllSearchScoresDict.ContainsKey(analysisId))
            {
                sumAllSearchScoresDict[analysisId] += searchScore;
            }
            else
            {
                sumAllSearchScoresDict.Add(analysisId, searchScore);
            }

            if (numAllPsmsDict.ContainsKey(analysisId))
            {
                numAllPsmsDict[analysisId]++;
            }
            else
            {
                numAllPsmsDict.Add(analysisId, 1);
            }

            if (!maxVotes.ContainsKey(analysisId))
                maxVotes.Add(analysisId, 1);

            if (this.ContainsKey(pepSeq))
            {
                this[pepSeq].PsmIdSpecDict.Add(psmId, spectrumID.ToString() + '.' + charge.ToString() + '.' + analysisId.ToString());
                //this[pepSeq].PsmIds.Add(psmId);
                //this[pepSeq].SpecChargeAnalysisSet.Add(spectrumID.ToString() + '.' + charge.ToString() + '.' + analysisId.ToString());
                //this[pepSeq].Score += searchScore;
                //sumAllSearchScores += searchScore;
                //numAllPSMs++;
                if (this[pepSeq].SearchScores.ContainsKey(analysisId))
                {
                    this[pepSeq].SearchScores[analysisId].Add(searchScore);
                }
                else
                {
                    this[pepSeq].SearchScores[analysisId] = new List<double>();
                    this[pepSeq].SearchScores[analysisId].Add(searchScore);
                }

                if (this[pepSeq].SearchScores[analysisId].Count > maxVotes[analysisId])
                    maxVotes[analysisId] = this[pepSeq].SearchScores[analysisId].Count;
            }
            else
            {
                PepInfo pep = new PepInfo();
                pep.PsmIdSpecDict.Add(psmId, spectrumID.ToString() + '.' + charge.ToString() + '.' + analysisId.ToString());
                //pep.PsmIds.Add(psmId);
                //pep.SpecChargeAnalysisSet.Add(spectrumID.ToString() + '.' + charge.ToString() + '.' + analysisId.ToString());
                //pep.Score += searchScore;
                pep.SearchScores[analysisId] = new List<double>();
                pep.SearchScores[analysisId].Add(searchScore);
                this.Add(pepSeq, pep);
                //sumAllSearchScores += searchScore;
                //numAllPSMs++;
            }
        }

        
        public static double PercentileRank<T>(IEnumerable<T> values, T i) where T : IComparable<T>
        {
            ////http://social.msdn.microsoft.com/Forums/en-us/csharpgeneral/thread/e4984186-a376-4e1d-97ed-d3d68a3e3ff6
            ////Console.WriteLine(PercentileRank(new int[] { 10, 11, 12, 12, 12, 12, 15, 18, 19, 20 }, 12)); // Prints 40.0
            int cfl = 0, fi = 0, n = 0;
            foreach (T value in values)
            {
                if (value.CompareTo(i) < 0)
                    cfl++;

                if (value.CompareTo(i) == 0)
                    fi++;

                n++;
            }
            //return (cfl + 0.5 * fi) * 100 / n;  // prints 40.0
            return (cfl + 0.5 * fi) / n;  // prints 0.40
        }

        /// <summary>
        /// Compute Bayesian Average score for each peptide, use 10 * maxVotes for a single peptide in this cluster as the constant
        /// </summary>
        public void ComputeBayesianAverage( Dictionary<long,string> analysisScoreOrder )
        {
            foreach (var analysisId in numAllPsmsDict.Keys)
            {
                
                //int maxVotes = this.Max(kvp => kvp.Value.SearchScores[analysisId].Count; 
                double aveAllSearchScores = sumAllSearchScoresDict[analysisId] / numAllPsmsDict[analysisId];
                int C = maxVotes[analysisId] * 10;
                //int C = maxVotes[analysisId] * 100;
                List<double> BAScores = new List<double>();
                foreach (string pepSeq in this.Keys)
                {
                    if (this[pepSeq].SearchScores.ContainsKey(analysisId))
                    {
                        double sumSearchScores = (this[pepSeq].SearchScores[analysisId]).Sum(); ;
                        int numVotes = this[pepSeq].SearchScores[analysisId].Count;
                        //this[pepId].Score = (sumAllSearchScores + sumSearchScores) / (numAllPSMs + numVotes);
                        //this[pepSeq].Score = (C * aveAllSearchScores + sumSearchScores) / (C + numVotes);
                        this[pepSeq].BAScores[analysisId] = (C * aveAllSearchScores + sumSearchScores) / (C + numVotes);
                        BAScores.Add(this[pepSeq].BAScores[analysisId]);
                    }
                }

                if (analysisScoreOrder[analysisId] == "Ascending")
                {
                    BAScores.Sort();
                    foreach (string pepSeq in this.Keys)
                    {
                        if (this[pepSeq].BAScores.ContainsKey(analysisId))
                            this[pepSeq].BAScores[analysisId] = PercentileRank(BAScores, this[pepSeq].BAScores[analysisId]);
                    }
                }
                else
                {
                    List<double> negativeBAScores = BAScores.Select(o => o * -1).ToList();
                    negativeBAScores.Sort();
                    foreach (string pepSeq in this.Keys)
                    {
                        if (this[pepSeq].BAScores.ContainsKey(analysisId))
                            this[pepSeq].BAScores[analysisId] = PercentileRank(negativeBAScores, -this[pepSeq].BAScores[analysisId]);
                    }
                }
            }

            foreach (string pepSeq in this.Keys)
            {
                this[pepSeq].FinalScore = this[pepSeq].BAScores.Values.Sum();  ////final score is the sum of percentile normalized BAscores from multiple analysis, not using average coz some peptide only ident in one search
            }
            
            //int maxVotes = this.Max(kvp => kvp.Value.PsmIdSpecDict.Count);
            //double aveAllSearchScores = sumAllSearchScores / numAllPSMs;
            //int C = maxVotes * 10;
            //foreach (string pepSeq in this.Keys)
            //{
            //    double sumSearchScores = this[pepSeq].Score;
            //    int numVotes = this[pepSeq].PsmIdSpecDict.Count;
            //    //this[pepId].Score = (sumAllSearchScores + sumSearchScores) / (numAllPSMs + numVotes);
            //    this[pepSeq].Score = (C * aveAllSearchScores + sumSearchScores) / (C + numVotes);
            //}
        }

    }


    public class ClusteringAnalysis
    {
        public static double DotProductCompareTo(Peaks oMe, Peaks Other, double fragmentMzTolerance)
        {
            // modified from ScanSifter project
            //square root transformed intensity for dot product calculation may provide higher discrimination
            int ThisIndex = 0;
            int OtherIndex = 0;
            int PeaksMatched = 0;
            double MZDiff;
            double ThisProduct = 0;
            double OtherProduct = 0;
            double ThisMZ;
            double OtherMZ;
            double ThisOtherDotProduct = 0;
            double fPwr = .5;
            //if (oMe.MSLevel != Other.MSLevel) { return 0; }
            while ((ThisIndex < oMe.OriginalMZs.Count) && (OtherIndex < Other.OriginalMZs.Count))
            {
                ThisMZ = oMe.OriginalMZs[ThisIndex];
                OtherMZ = Other.OriginalMZs[OtherIndex];
                MZDiff = ThisMZ - OtherMZ;
                if (MZDiff > fragmentMzTolerance)
                {
                    OtherIndex++;
                }
                else if (-MZDiff > fragmentMzTolerance)
                {
                    ThisIndex++;
                }
                else
                {
                    //ThisOtherDotProduct += oMe.OriginalIntensities[ThisIndex] * Other.OriginalIntensities[OtherIndex];
                    ThisOtherDotProduct += Math.Sqrt(oMe.OriginalIntensities[ThisIndex]) * Math.Sqrt(Other.OriginalIntensities[OtherIndex]);
                    PeaksMatched++;
                    ThisIndex++;
                    OtherIndex++;
                }
            }
            for (int i = 0; i < oMe.OriginalMZs.Count; i++)
            {
                //ThisProduct += (double)Math.Pow(oMe.OriginalIntensities[i], 2);
                ThisProduct += (double)oMe.OriginalIntensities[i];
            }
            for (int j = 0; j < Other.OriginalMZs.Count; j++)
            {
                //OtherProduct += (double)Math.Pow(Other.OriginalIntensities[j], 2);
                OtherProduct += (double)Other.OriginalIntensities[j];
            }
            return (ThisOtherDotProduct) / (double)(Math.Pow(((ThisProduct) * (OtherProduct)), (fPwr)));
            //return fPwr;
        }              
       
    }


}