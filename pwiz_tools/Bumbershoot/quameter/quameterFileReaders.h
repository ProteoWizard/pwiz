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
// The Original Code is the Quameter software.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#ifndef _QUAMETERFILEREADERS_H
#define _QUAMETERFILEREADERS_H

#include "pwiz/utility/misc/Std.hpp"
#include "sqlite/sqlite3pp.h"
#include "quameterSharedTypes.h"
#include "quameterSharedFuncs.h"
#include "quameterConfig.h"
#include <boost/tokenizer.hpp>
#include <boost/assign.hpp>


namespace sqlite = sqlite3pp;


namespace freicore
{
namespace quameter
{
    using namespace boost::assign;
    
    static const boost::char_separator<char> delim(" =\r\n");
    static const boost::char_separator<char> tabDelim("\t");

    typedef boost::tokenizer<boost::char_separator<char> > tokenizer;

    struct ScanRankerReader
    {
        string srTextFile;
        multimap<string,ScanRankerMS2PrecInfo> precursorInfos;
        map<ScanRankerMS2PrecInfo,double> bestTagScores;
        map<ScanRankerMS2PrecInfo,double> bestTagTics;
        map<ScanRankerMS2PrecInfo,double> tagMzRanges;
        map<ScanRankerMS2PrecInfo,double> scanRankerScores;

        ScanRankerReader(const string& file)
        {
            srTextFile = file;
        }

        void extractData();
    };

    struct IDPDBReader
    {
        const string idpDBFile;
        const string spectrumSourceId;

        IDPDBReader(const string& file, const string& spectrumSourceId);

        // For metric P-1: Find the median peptide identification score for all peptides
        double getMedianIDScore() const;

        // For metric IS-2: Find the median precursor m/z of distinct matches
        double getMedianPrecursorMZ() const;

        // For metric DS-1A: Finds the number of peptides identified by one spectrum
        // For metrics DS-1A and DS-1B: Finds the number of peptides identified by two spectra
        // For metric DS-1B: Finds the number of peptides identified by three spectra
        const vector<size_t>& peptideSamplingRates() const {return _peptideSamplingRates;}

        // Returns a map of MS2 native IDs to distinct peptide id
        const map<string, size_t>& distinctModifiedPeptideByNativeID() const {return _distinctModifiedPeptideByNativeID;}

        // Returns a map of MS2 native IDs to charge states
        const map<string, vector<int> >& chargeStatesByNativeID() const {return _chargeStatesByNativeID;}

        // For metrics IS-3A, IS-3B and IS-3C: Return the number of peptides with a charge of +1, +2, +3 and +4 
        const vector<size_t>& distinctMatchCountByCharge() const {return _distinctMatchCountByCharge;}

        // For metric MS1-5A: Find the median real value of precursor errors
        // For metric MS1-5B: Find the mean of the absolute precursor errors
        // For metrics MS1-5C and MS1-5D: Find the median real value and interquartile distance of precursor errors (both in ppm)
        const MassErrorStats& precursorMassErrorStats() const {return _precursorMassErrorStats;}

        // For metric P-2A: Find the number of MS2 spectra that identify tryptic peptide ions
        const vector<size_t>& spectrumCountBySpecificity() const {return _spectrumCountBySpecificity;}

        // For metric P-2B: Find the number of tryptic peptide ions identified.
        const vector<size_t>& distinctMatchCountBySpecificity() const {return _distinctMatchCountBySpecificity;}

        // For metric P-2C: Find the number of unique tryptic peptide sequences identified
        // For metric P-3: Find the ratio of semi- over fully-tryptic peptide IDs.
        const vector<size_t>& distinctPeptideCountBySpecificity() const {return _distinctPeptideCountBySpecificity;}

        // Used for peak finding of identified peptides;
        // precursorMZs for identified scans are reset based on the one from the idpDB (i.e. monoisotope corrected)
        XICWindowList MZRTWindows(MS2ScanMap& ms2ScanMap);

        private:
        vector<size_t> _peptideSamplingRates;
        map<string, size_t> _distinctModifiedPeptideByNativeID;
        map<string, vector<int> > _chargeStatesByNativeID;
        vector<size_t> _distinctMatchCountByCharge;
        MassErrorStats _precursorMassErrorStats;
        vector<size_t> _spectrumCountBySpecificity;
        vector<size_t> _distinctMatchCountBySpecificity;
        vector<size_t> _distinctPeptideCountBySpecificity;
    };

}
}

#endif

