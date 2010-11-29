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
// The Initial Developer of the DirecTag peptide sequence tagger is Matt Chambers.
// Contributor(s): Surendra Dasaris
//
// The Initial Developer of the ScanRanker GUI is Zeqiang Ma.
// Contributor(s): 
//
// Copyright 2009 Vanderbilt University
//

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
