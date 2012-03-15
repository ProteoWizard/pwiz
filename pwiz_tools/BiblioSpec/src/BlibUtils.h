/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
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
#include "boost/filesystem/operations.hpp"
#include "boost/filesystem/fstream.hpp"

#ifdef _MSC_VER
#include <direct.h>
#define getcwd _getcwd

template<typename T>
inline bool isinf(T value)
{
    return value == std::numeric_limits<T>::infinity();
} 
#endif


using namespace std;

namespace BiblioSpec {

/**
 * The three possible ways a spectrum may be identified in both result
 * files and spectrum files.  
 */
enum SPEC_ID_TYPE { SCAN_NUM_ID, INDEX_ID, NAME_ID };

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
    return left.second < right.second;
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
