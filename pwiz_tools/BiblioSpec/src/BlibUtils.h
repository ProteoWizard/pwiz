//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

#pragma once

#include <string.h>
#include <string>
#include <iostream>
#include <utility>
#include <vector>
#include <deque>
#include <iterator>
#include <cstdlib>
#include "Verbosity.h"
#include "BlibException.h"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"

using std::numeric_limits;
using std::min;
using std::max;

#if defined(_MSC_VER)
#include <direct.h>
#define getcwd _getcwd

#if _MSC_VER < 1700
template<typename T>
inline bool isinf(T value)
{
    return value == std::numeric_limits<T>::infinity();
}
#endif
#else
#define isinf(x) std::isinf((x))
#endif


namespace BiblioSpec {

/**
 * The three possible ways a spectrum may be identified in both result
 * files and spectrum files.  
 */
enum SPEC_ID_TYPE { SCAN_NUM_ID, INDEX_ID, NAME_ID };
const char* specIdTypeToString(SPEC_ID_TYPE specIdType);

/**
 * Different kinds of ion mobility are supported.
 * N.B. this should agree with the enum eIonMobilityUnits in pwiz.CLI.analysis.SpectrumList_IonMobility
 */
enum IONMOBILITY_TYPE { IONMOBILITY_NONE, IONMOBILITY_DRIFTTIME_MSEC, IONMOBILITY_INVERSEREDUCED_VSECPERCM2, IONMOBILITY_COMPENSATION_V, NUM_IONMOBILITY_TYPES };
const char* ionMobilityTypeToString(IONMOBILITY_TYPE ionMobilityType);

/**
 * All possible scores from different search algorithms.
 */
enum PSM_SCORE_TYPE {
    UNKNOWN_SCORE_TYPE,       // default for ssl files
    PERCOLATOR_QVALUE,        // sequest/percolator .sqt files
    PEPTIDE_PROPHET_SOMETHING,// pepxml files
    SPECTRUM_MILL,            // pepxml files (currently scoreless)
    IDPICKER_FDR,             // idpxml files
    MASCOT_IONS_SCORE,        // mascot .dat files (.pep.xml?, .mzid?)
    TANDEM_EXPECTATION_VALUE, // tandem .xtan.xml files
    PROTEIN_PILOT_CONFIDENCE, // protein pilot .group.xml files
    SCAFFOLD_SOMETHING,       // scaffold .mzid files
    WATERS_MSE_PEPTIDE_SCORE, // Waters MSE .csv files
    OMSSA_EXPECTATION_SCORE,  // pepxml files
    PROTEIN_PROSPECTOR_EXPECT,// pepxml with expectation score
    SEQUEST_XCORR,            // sequest (no percolator) .sqt files
    MAXQUANT_SCORE,           // maxquant msms.txt files
    MORPHEUS_SCORE,           // pepxml files with morpehus scores
    MSGF_SCORE,               // pepxml files with ms-gfdb scores
    PEAKS_CONFIDENCE_SCORE,   // pepxml files with peaks confidence scores
    BYONIC_PEP,               // byonic .mzid files
    PEPTIDE_SHAKER_CONFIDENCE,// peptideshaker .mzid files
    GENERIC_QVALUE,

    NUM_PSM_SCORE_TYPES
};

/**
 * \brief Translate a string value into its corresponding score type.  
 * \returns The score type for the given string, UNKNOWN if string is
 * invalid.
 */
PSM_SCORE_TYPE stringToScoreType(const string& scoreName);

/**
 * Returns the string representation of the score type.
 */
const char* scoreTypeToString(PSM_SCORE_TYPE scoreType);

/**
* Returns the string representation of the score's cutoff type.
*/
const char* scoreTypeToProbabilityTypeString(PSM_SCORE_TYPE scoreType);
/**
 * \brief Return a string from the root to the given filename.
 * Converts relative paths to absolute.  For filenames with no path,
 * prepends current working directory.  Does not check that the file
 * or relative path exists.  Does not resolve symbolic links.
 */
string getAbsoluteFilePath(string filename);

/**
 * \brief Return all of the string before the last / or \\. Returns an
 * empty string if neither found.
 */
string getPath(string fullFileName);

/**
 * \brief Return all of string after the last '.'.  Returns an empty
 * string if none found.
 */
string getExtension(string fullFileName);

/**
 * \brief Return the string between the last \ or // and the last
 * '.'.  Returns the whole string if neither found.
 */
string getFileRoot(string fullFileName);

/**
 * \brief Returns true if the end of the filename matches exactly the
 * ext string.  Assumes the ext string includes a dot.
 */
bool hasExtension(string filename, string ext);
bool hasExtension(string filename, const char* ext);
bool hasExtension(const char* filename, const char* ext);

/**
 *  Replace all characters after the last '.' with ext.  If no '.'
 *  found concatinate .ext onto the filename. 
 */
void replaceExtension(string& filename, const char* ext);

/**
 * Compare the first element of a pair of doubles for sorting in
 * descending order. 
 */
bool compare_first_pair_doubles_descending(const pair<double, double>& left,
                                           const pair<double, double>& right);
/**
 * Compare two doubles for sorting in descending order
 */
bool doublesDescending(const double left, const double right);

/**
 * Delete any spaces or tabs at the end of the given string
 */
void deleteTrailingWhitespace(string& str);

/**
 * Sum the masses of amino acids and modifications from the given
 * array of masses (as initialized by AminoAcidMasses).
 */
double getPeptideMass(string& modifiedSeq, double* masses);

/**
 * Clear a deque of object pointers and free the memory for each object.
 */
// Why do I get an undefined reference when the body of this is in .cpp?
template<class T> void clearDeque(deque<T*>& clearMe){
    while( !clearMe.empty() ){
        delete clearMe.back();
        clearMe.pop_back();
    }
 }

/**
 * Clear a vector of object pointers and free the memory for each object.
 */
// Why do I get an undefined reference when the body of this is in .cpp?
template<class T> void clearVector(vector<T*>& clearMe){
    while( !clearMe.empty() ){
        delete clearMe.back();
        clearMe.pop_back();
    }
 }

/**
 * Replace all occurances of findChar and replace them with
 * replaceChar, returning the number of substitutions performed.
 */
size_t replaceAllChar(string& s, const char findChar, const char replaceChar);

/**
 * Delete any spaces or tabs at the end of the given string
 */
void deleteTrailingWhitespace(string& str);

/**
 * Take a vector of elements and return the index of the element with
 * the highest value.  Elements must have comparison operators defined
 * for them.  In the case of a tie, returns the first element with the
 * maximum value.  Returns 0 for empty vector.
 * \returns The index of the element with largest value.
 */
// again, the undefined reference when in cpp
template <class T> size_t getMaxElementIndex(const std::vector<T>& elements)
{
    if( elements.empty() ){
        return 0;
    }

    // initialize max to first element
    T max = elements[0];
    size_t max_idx = 0;

    // look for bigger elements
    for(size_t i = 0; i < elements.size(); i++){
        if( elements[i] > max ){
            max = elements[i];
            max_idx = i;
        }
    }

    return max_idx;
}


class TempFileDeleter
{
    bfs::path filepath_;

    public:
    TempFileDeleter(const bfs::path& filepath) : filepath_(filepath)
    {
    }

    ~TempFileDeleter()
    {
        boost::system::error_code ec;
        bfs::remove(filepath_, ec);
    }

    const bfs::path& filepath() const { return filepath_; }
};


/**
 * Return the full path to the location of the executable.
 */
string getExeDirectory();




} // namespace


/**
 *  Turn a vector of elements into an ostream of elements separated by a space.
 */
template<class T> ostream& operator<<(ostream& os, const vector<T>& v)
{
    copy(v.begin(), v.end(), ostream_iterator<T>(os, " ")); 
    return os;
}

/**
 * Comparison function for comparing second element of pairs.  Sorts
 * in increasing order.
 */
template <class T1, class T2> bool compareSecond(const pair<T1,T2>& left,
                                                 const pair<T1,T2>& right){
    if( left.second < right.second ){
        return true;
    } else if( left.second == right.second ){
        return (left.first < right.first );
    }
    // else
    return false;
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
