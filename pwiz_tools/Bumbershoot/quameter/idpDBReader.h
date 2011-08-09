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
// The Original Code is the Quameter software.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#ifndef _IDPDBREADER_H
#define _IDPDBREADER_H

#include "sqlite/sqlite3pp.h"
#include "quameterSharedTypes.h"
#include "quameterSharedFuncs.h"

namespace sqlite = sqlite3pp;
using namespace std;

namespace freicore
{
namespace quameter
{

struct IDPDBReader
{
    string idpDBFile;

    IDPDBReader(const string& file)
    {
        idpDBFile = file;
    }

    // For metric MS1-5A: Find the median real value of precursor errors
    double MedianRealPrecursorError(const string& spectrumSourceId);
    // For metric MS1-5B: Find the mean of the absolute precursor errors
    double GetMeanAbsolutePrecursorErrors(const string& spectrumSourceId);
    // For metrics MS1-5C and MS1-5D: Find the median real value and interquartile distance of precursor errors (both in ppm)
    PPMMassError GetRealPrecursorErrorPPM(const string& spectrumSourceId);
    // For metric P-1: Find the median peptide identification score for all peptides
    double GetMedianIDScore(const string& spectrumSourceId);
    // For metric P-2A: Find the number of MS2 spectra that identify tryptic peptide ions
    int GetNumTrypticMS2Spectra(const string& spectrumSourceId);
    // For metric P-2B: Find the number of tryptic peptide ions identified.
    int GetNumTrypticPeptides(const string& spectrumSourceId);
    // For metric P-2C: Find the number of unique tryptic peptide sequences identified
    int GetNumUniqueTrypticPeptides(const string& spectrumSourceId);
    // For metric P-3: Find the ratio of semi- over fully-tryptic peptide IDs.
    int GetNumUniqueSemiTrypticPeptides(const string& spectrumSourceId);
    // For metric DS-1A: Finds the number of peptides identified by one spectrum
    int PeptidesIdentifiedOnce(const string& spectrumSourceId);
    // For metrics DS-1A and DS-1B: Finds the number of peptides identified by two spectra
    int PeptidesIdentifiedTwice(const string& spectrumSourceId);
    // For metric DS-1B: Finds the number of peptides identified by three spectra
    int PeptidesIdentifiedThrice(const string& spectrumSourceId);
    // For metric IS-2: Find the median precursor m/z of unique ions of id'd peptides
    double MedianPrecursorMZ(const string& spectrumSourceId);
    // Query the idpDB and return with a list of MS2 native IDs for all identified peptides
    vector<string> GetNativeId(const string& spectrumSourceId);
    // Finds duplicate peptide IDs. Used in metrics C-1A and C-1B.
    multimap<int, string> GetDuplicateID(const string& spectrumSourceId);
    // For metrics IS-3A, IS-3B and IS-3C: Return the number of peptides with a charge of +1, +2, +3 and +4 
    fourInts PeptideCharge(const string& spectrumSourceId);
    // Used for peak finding of identified peptides
    vector<XICWindows> MZRTWindows(const string& spectrumSourceId, map<string, int> nativeToArrayMap, vector<MS2ScanInfo> scanInfo);
};

}
}

#endif

