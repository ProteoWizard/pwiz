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

#include "pwiz/utility/misc/Std.hpp"
#include <cstdlib>
#include <boost/core/cmath.hpp>
#include <algorithm>


// Allowed tolerance on one or all columns of a tab-delimited file
struct CompareDetails
{
    int fieldIdx_;   // compare the ith field (-1 means compare all doubles at default tolerance)
    double delta_;   // allow this much difference
    vector<string> skipLines_; // list of keyphrases which if found cause a line to be skipped

    CompareDetails() : fieldIdx_(-999), delta_(0) {}; // user can configure to allow a very small tolerance to doubles for OS/compiler effects
    bool allow_tolerance_all_doubles(){ return (fieldIdx_ == -1); }
};

// Read from file and save each line in vector.
// Save optional info about allowed tolerance in compareDetails.
void getSkipLines(const char* fileName, 
          CompareDetails& compareDetails){

    ifstream infile(fileName);

    if( !infile.good() ){
        cerr << "Could not open '" << fileName << "'.  No lines being skipped" 
             << endl;
        return;
    }

    string line;
    getline(infile, line);

    // first line may contain info on what field should be compared as a number
    if( line.find("CompareDetails: ") != string::npos ){
        stringstream ss (stringstream::in | stringstream::out);
        ss << line;
        // get the fields
        string head;
        ss >> head >> compareDetails.fieldIdx_ >> compareDetails.delta_ ;

        getline(infile, line);
    }

    // now get remaining lines of text to skip
    while( !infile.eof() ){
        int length = line.size();
        if (length > 0 && line[length - 1] == '\r')
            line.erase(length - 1);
        if (line.size() > 0)
            compareDetails.skipLines_.push_back(line);
        getline(infile, line);
    }

    infile.close();
}

// Split the given string on tab, extract the nth field, cast to double,
// save the beginning and ending indexes of the field in the string.
double getField(int fieldIdx, const string& line, 
        size_t& finalStart, size_t& finalEnd, string &field)
{

    size_t start = line.find('\t');
    int counter = 1;

    // find the beginning of the field
    while( (counter < fieldIdx) && (start != string::npos )){
        start = line.find('\t', start + 1); // look for the next tab
        counter++;
    }

    size_t end = line.find('\t', start + 1);
    field = line.substr(start + 1, end - start - 1);
    finalStart = start + 1;
    finalEnd = end;
    const char *startp = field.c_str();
    char *endp;
    double result = strtod(startp, &endp);
    if (endp == startp)
        result = NAN; // Not a number
    return result;
}

// account for Windows line endings
string trim(const string &raw)
{
    string result = raw;
    int len = result.size();
    if (len > 0 && result[len - 1] == '\r')
        result.erase(len - 1);
    return result;
}

// Return true if the lines match, false if not.
// Use the information in CompareDetails to decide if an exact
// text match is required.
bool linesMatch(const string& expectedRaw,
                const string& observedRaw,
                CompareDetails& details)
{
    // account for Windows line endings
    string expected = trim(expectedRaw);
    string observed = trim(observedRaw);
    size_t length = observed.size();

    // they match if both empty
    if(expected.empty() && length == 0 ){
        return true;
    } else if ( expected.empty() || observed.empty() ){// not if only one empty
        return false;
    }

    if( observed.compare(0, length, expected) == 0){
        return true;
    }

    // Does this line contain a keyphrase indicating that it can be ignored for comparison purposes?
    for (int i = 0; i < (int)details.skipLines_.size(); i++)
    {
        if (expected.find(details.skipLines_[i]) != string::npos)
        {
            return true;
        }
    }

    // now compare for a particular small numerical difference, if given, or a very small difference across all doubles if not
    int startRange = details.allow_tolerance_all_doubles() ? 0 : details.fieldIdx_; 
    int endRange = details.allow_tolerance_all_doubles() ? std::count(expected.begin(), expected.end(), '\t') : details.fieldIdx_;
    string expectedRemaining = expected;
    string observedRemaining = observed;
    for (int fieldIdx = endRange; fieldIdx >= startRange; fieldIdx--) // work from end, trimming as we resolve
    {
        // get a substr from each, cast to double and compare with delta
        size_t expStart, expEnd, obsStart, obsEnd;
        string parsedExpected, parsedObserved;
        double expectedField = getField(fieldIdx, expected,
            expStart, expEnd, parsedExpected);
        double observedField = getField(fieldIdx, observed,
            obsStart, obsEnd, parsedObserved);

        if (boost::core::isnan(observedField) || boost::core::isnan(expectedField))
        {
            if (parsedExpected.compare(parsedObserved) != 0)
                return false;
            continue; // Not a double, leave it alone
        }

        // get absolute value of difference between the two
        double diff = expectedField - observedField;
        if (diff < 0)
        {
            diff = diff * -1;
        }

        if (diff > details.delta_)
        {
            return false;
        }

        // also compare remaining string, after the field is removed
        expectedRemaining.erase(expStart, expEnd - expStart);
        observedRemaining.erase(obsStart, obsEnd - obsStart);
    }
    length = observedRemaining.size();
    return length==0 || observedRemaining.compare(0, length, expectedRemaining) == 0;
}




/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
