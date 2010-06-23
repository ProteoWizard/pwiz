//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.IO;

namespace IdPickerGui.MODEL
{
	public enum RunStatus
	{
		New = 0,
		InProgress = 1,
		Complete = 2,
		Error = 3,
		Cancelled = 4
	}

	public class QonvertErrorInfo
	{
		public enum ExitCodes
		{
			QONVERT_SUCCESS,
			QONVERT_ERROR_UNHANDLED_EXCEPTION,
			QONVERT_ERROR_FASTA_FILE_FAILURE,
			QONVERT_ERROR_RUNTIME_CONFIG_FILE_FAILURE,
			QONVERT_ERROR_RESIDUE_CONFIG_FILE_FAILURE,
			QONVERT_ERROR_NOT_ENOUGH_ARGUMENTS,
			QONVERT_ERROR_RUNTIME_CONFIG_OVERRIDE_FAILURE,
			QONVERT_ERROR_NO_INPUT_FILES_FOUND,
		    QONVERT_ERROR_NO_TARGET_PROTEINS,
			QONVERT_ERROR_NO_DECOY_PROTEINS
		}

		private static string[] errorStrings =
		{
			"success",
			"unhandled exception",
			"protein FASTA database file not found",
			"runtime configuration file specified but not found",
			"residue configuration file specified but not found",
			"not enough arguments to command-line",
			"error parsing command-line overrides of runtime parameters",
			"no input files",
            "no target proteins in the protein FASTA database (check decoy prefix)",
			"no decoy proteins in the protein FASTA database (check decoy prefix)"
		};

		public static string GetErrorStringForCode( int exitCode )
		{
			return errorStrings[exitCode];
		}
	}

    public class KeyTagCollection : KeyedCollection<string, InputFileTag>
    {

        public KeyTagCollection() : base() { }

       
        protected override string GetKeyForItem(InputFileTag tag)
        {
            return tag.FullPath;
        }

        

    }


    public class IDPickerInfo : ICloneable
    {
                /*  From Matt 3/5/08
                 *  DecoyPrefix: "rev_" 
                    MaxFDR: "0.25" 
                    MaxResultRank: "1" always 1 / ignore for now
                    NormalizeSearchScores: "0" 
                    NumChargeStates: "3" ignore for now
                    OptimizeScorePermutations: "200" 
                    OptimizeScoreWeights: "0" 
                    OutputSuffix: "" 
                    ProteinDatabase: "" 
                    SearchScoreWeights: "mvh 1 xcorr 1 hyperscore 1" these are defaults
                    MostParsimoniousAnalysis=True 
                    GenerateBipartiteGraphs=False ignore for now
                    ModsAreDistinctByDefault=True 
                    DistinctModsOverride="" 
                    IndistinctModsOverride="" 
                    MinPeptideLength=5 
                    MinDistinctPeptides=2 
                    MaxAmbiguousIds=2 
                    AllowSharedSourceNames=True
                 * 
                 * */

        private int id;
        public int Id
        {
            get { return id; }
            set { id = value; }
        }

        private string reportName;
        public string ReportName
        {
            get { return reportName; }
            set { reportName = value; }
        }

        private string department;
        public string Department
        {
            get { return department; }
            set { department = value; }
        }

        // changed to allow nongrouped files
        // emtpy group name in ScrcPathToTagCol
        // but need this count for prog bars
        private int numGroupedFiles;
        public int NumGroupedFiles
        {
            get { return numGroupedFiles; }
            set { numGroupedFiles = value; }
        }

        private KeyTagCollection srcPathToTagCollection;
        public KeyTagCollection SrcPathToTagCollection
        {
            get { return srcPathToTagCollection; }
            set { srcPathToTagCollection = value; }

        }

        private string srcFilesDir;
        public string SrcFilesDir
        {
            get { return srcFilesDir; }
            set { srcFilesDir = value; }
        }

        private bool includeSubdirectories;
        public bool IncludeSubdirectories
        {
            get { return includeSubdirectories; }
            set { includeSubdirectories = value; }
        }

        private string resultsDir;
        public string ResultsDir
        {
            get { return resultsDir; }
            set { resultsDir = value; }
        }

        private string databasePath;
        public string DatabasePath
        {
            get { return databasePath; }
            set { databasePath = value; }
        }

        private DateTime dateRunStart;
        public DateTime DateRunStart
        {
            get { return dateRunStart; }
            set { dateRunStart = value; }
        }

        private DateTime dateRunComplete;
        public DateTime DateRunComplete
        {
            get { return dateRunComplete; }
            set { dateRunComplete = value; }
        }

        private string decoyPrefix;
        public string DecoyPrefix
        {
            get { return decoyPrefix; }
            set { decoyPrefix = value; }

        }

        private Single decoyRatio;
        public Single DecoyRatio
        {
            get { return decoyRatio; }
            set { decoyRatio = value; }
        }

        private Single maxFdr;
        public Single MaxFDR
        {
            get { return maxFdr; }
            set { maxFdr = value; }
        }

        private int maxResultRank;
        public int MaxResultRank
        {
            get { return maxResultRank; }
            set { maxResultRank = value; }
        }

        private bool normalizeSearchScores;
        public bool NormalizeSearchScores
        {
            get { return normalizeSearchScores; }
            set { normalizeSearchScores = value; }

        }

        private int numChargeStates;
        public int NumChargeStates
        {
            get { return numChargeStates; }
            set { numChargeStates = value; }
        }

        private int optimizeScorePermutations;
        public int OptimizeScorePermutations
        {
            get { return optimizeScorePermutations; }
            set { optimizeScorePermutations = value; }

        }
        
        private bool optimizeScoreWeights;
        public bool OptimizeScoreWeights
        {
            get { return optimizeScoreWeights; }
            set { optimizeScoreWeights = value; }
        }

		private int parsimonyPeptideVariable;
		public int MinAdditionalPeptides
		{
			get { return parsimonyPeptideVariable; }
			set { parsimonyPeptideVariable = value; }
		}

        private bool generateBipartiteGraphs;
        public bool GenerateBipartiteGraphs
        {
            get { return generateBipartiteGraphs; }
            set { generateBipartiteGraphs = value; }
        }

        private bool modsAreDistinctByDefault;
        public bool ModsAreDistinctByDefault
        {
            get { return modsAreDistinctByDefault; }
            set { modsAreDistinctByDefault = value; }
        }

        private ModOverrideInfo[] modOverrides;
        public ModOverrideInfo[] ModOverrides
        {
            get { return modOverrides; }
            set { modOverrides = value; }
        }

        private string searchScoreWeights
        {
            get
            {
                string s = string.Empty;

                foreach (ScoreInfo si in ScoreWeights)
                {
                    s += si.ToString() + " ";
                }

                return s.Trim();
            }
        }
        
        private int minPeptideLength;
        public int MinPeptideLength
        {
            get { return minPeptideLength; }
            set { minPeptideLength = value; }
        }

        private int minDistictPeptides;
        public int MinDistinctPeptides
        {
            get { return minDistictPeptides; }
            set { minDistictPeptides = value; }
        }

        // Variable that controls the minimum spectra required for a protein
        private int minSpectraPerProtein;
        public int MinSpectraPerProetin
        {
            get { return minSpectraPerProtein; }
            set { minSpectraPerProtein = value; }
        }

        private int maxAmbiguousIds;
        public int MaxAmbiguousIds
        {
            get { return maxAmbiguousIds; }
            set { maxAmbiguousIds = value; }
        }

        private bool allowSharedSourceNames;
        public bool AllowSharedSourceNames
        {
            get { return allowSharedSourceNames; }
            set { allowSharedSourceNames = value; }
        }

        private ScoreInfo[] scoreWeights;
        public ScoreInfo[] ScoreWeights
        {
            get { return scoreWeights; }
            set { scoreWeights = value; }
        }
        
        private int active;
        public int Active
        {
            get { return active; }
            set { active = value; }
        }

        private DateTime dateRequested;
        public DateTime DateRequested
        {
            get { return dateRequested; }
            set { dateRequested = value; }
        }

        private RunStatus runStatus;
		public RunStatus RunStatus
        {
            get { return runStatus; }
            set { runStatus = value; }
        }
        private string RunStatusDesc
        {
            get
            {
                switch (RunStatus)
                {
                    default:
                        return "Error";
                    case RunStatus.New:
                        return "New";
                    case RunStatus.InProgress:
                        return "InProgress";
                    case RunStatus.Complete:
                        return "Complete";
                    case RunStatus.Error:
                        return "Error";
                  }
            }
		
            set { RunStatusDesc = value; }
        }

        private string stdInput;
        public string QonvertCommandLine
        {
            get { return stdInput; }
            set { stdInput = value; }
        }

        private StringBuilder stdError;
        public StringBuilder StdError
        {
            get { return stdError; }
            set { stdError = value; }
        }

        private StringBuilder stdOutput;
        public StringBuilder StdOutput
        {
            get { return stdOutput; }
            set { stdOutput = value; }
        }

        public IDPickerInfo()
        {
            Id = -1;
            ScoreWeights = new ScoreInfo[0];
            ModOverrides = new ModOverrideInfo[0];
            SrcPathToTagCollection = new KeyTagCollection();
            Active = 1;
            stdError = new StringBuilder();
            stdOutput = new StringBuilder();
        }

        public object Clone()
        {
            try
            {
                IDPickerInfo pInfo = new IDPickerInfo();

                pInfo.DatabasePath = DatabasePath;
                pInfo.DateRequested = DateTime.Parse(DateRequested.ToString());
                pInfo.DateRunComplete = DateTime.Parse(DateRunComplete.ToString());
                pInfo.DateRunStart = DateTime.Parse(DateRunStart.ToString());
                pInfo.DecoyPrefix = DecoyPrefix;
                pInfo.DecoyRatio = DecoyRatio;
                pInfo.Department = Department;
                pInfo.GenerateBipartiteGraphs = GenerateBipartiteGraphs;
                pInfo.MaxAmbiguousIds = MaxAmbiguousIds;
                pInfo.MaxFDR = MaxFDR;
                pInfo.MaxResultRank = MaxResultRank;
                pInfo.MinDistinctPeptides = MinDistinctPeptides;
                pInfo.MinSpectraPerProetin = MinSpectraPerProetin;
                pInfo.MinPeptideLength = MinPeptideLength;
                pInfo.ModOverrides = ModOverrides;
                pInfo.ModsAreDistinctByDefault = ModsAreDistinctByDefault;
                pInfo.NormalizeSearchScores = NormalizeSearchScores;
                pInfo.NumChargeStates = NumChargeStates;
                pInfo.OptimizeScorePermutations = OptimizeScorePermutations;
                pInfo.OptimizeScoreWeights = OptimizeScoreWeights;
                pInfo.MinAdditionalPeptides = MinAdditionalPeptides;
                pInfo.QonvertCommandLine = QonvertCommandLine;
                pInfo.ReportName = ReportName;
                pInfo.ResultsDir = ResultsDir;
                pInfo.RunStatus = RunStatus;
                pInfo.ScoreWeights = ScoreWeights.Clone() as ScoreInfo[];
                pInfo.ModOverrides = ModOverrides.Clone() as ModOverrideInfo[];
                pInfo.SrcFilesDir = SrcFilesDir;
                pInfo.IncludeSubdirectories = IncludeSubdirectories;

                foreach(InputFileTag tag in SrcPathToTagCollection)
                {
                    pInfo.SrcPathToTagCollection.Add(tag.Clone() as InputFileTag);
                }

                pInfo.StdError = new StringBuilder( StdError.ToString() );
                pInfo.StdOutput = new StringBuilder( StdOutput.ToString() );
                pInfo.stdInput = stdInput;

                return pInfo;

            }
            catch (Exception exc)
            {
                throw exc;
            }


        }

        public override string ToString()
        {
            string s = string.Empty;

            //s += "Id: " + Id.ToString() + "\r\n";
            //s += "DateRequested: " + DateRequested.ToShortDateString() + "\r\n";
            //s += "ReportName: " + ReportName + "\r\n";
            //s += "SrcFilesDir: " + SrcFilesDir + "\r\n";
            //s += "DateRunStart: " + DateRunStart.ToShortDateString() + "\r\n";
            //s += "DateRunComplete: " + DateRunComplete.ToShortDateString() + "\r\n";

            //s += "Results Dir: " + ResultsDir + "\r\n";
            //s += "Database Path: " + DatabasePath + "\r\n";
            //s += "Decoy Ratio: " + DecoyRatio.ToString() + "\r\n";
            s += "Decoy Prefix: " + DecoyPrefix + "\r\n";
            s += "Max FDR: " + (MaxFDR * 100).ToString() + "%" + "\r\n";
            s += "Max Result Rank: " + MaxResultRank.ToString() + "\r\n";
            s += "Normalize Search Scores: " + Convert.ChangeType(NormalizeSearchScores, typeof(bool)) + "\r\n";
            s += "Num Charge States: " + NumChargeStates.ToString() + "\r\n";
            s += "Optimize Score Permutations: " + OptimizeScorePermutations.ToString() + "\r\n";
            s += "Optimize Score Weights: " + Convert.ChangeType(OptimizeScoreWeights, typeof(bool)) + "\r\n";
            s += "Scores And Weights: " + "\r\n";

            if (ScoreWeights.Length > 0)
            {
                foreach (ScoreInfo si in ScoreWeights)
                {
                    s += "\t" + si.ToString() + "\r\n";
                }
            }
            else
            {
                s += "\tNone\r\n"; 
                    
            }

			s += "Parsimony Peptide Variable: " + Convert.ChangeType( MinAdditionalPeptides, typeof( bool ) ) + "\r\n";
            s += "Generate Bipartite Graphs: " + Convert.ChangeType(GenerateBipartiteGraphs, typeof(bool)) + "\r\n";
            s += "Mods Are Distinct By Default: " + Convert.ChangeType(ModsAreDistinctByDefault, typeof(bool)) + "\r\n";
            s += "Distinct/Indistinct Mods Override: " + "\r\n";

            if (ModOverrides.Length > 0)
            {
                foreach (ModOverrideInfo mo in ModOverrides)
                {
                    s += "\t" + mo.ToString() + "\r\n";
                }
            }
            else
            {
                s += "\tNone\r\n"; 
            }

            s += "Min Peptide Length: " + MinPeptideLength.ToString() + "\r\n";
            s += "Min Distinct Peptides: " + MinDistinctPeptides.ToString() + "\r\n";
            s += "Min Spectra Per Protein: " + MinSpectraPerProetin.ToString() + "\r\n";
            s += "Max Ambiguous Ids: " + MaxAmbiguousIds.ToString() + "\r\n";
            s += "Allow Shared Source Names: " + Convert.ChangeType(AllowSharedSourceNames, typeof(bool)) + "\r\n"; ;
            s += "Run Status: " + RunStatus.ToString();
            //s += "Active: " + Active.ToString() + "\r\n";
            //s += "RunStatus: " + RunStatus.ToString();

            return s;

        }

        // these are used for backwards compatibility
        // if you mark a field as [NonSerialized()] you can
        // set its value in these methods

       

    }
}
