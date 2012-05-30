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

#include <iostream>
#include <sstream>
#include <algorithm>
#include <fstream>
#include <string>
#include <stdlib.h>
#include <vector>

using namespace std;

// Allowed tolerance on one column of a tab-delimited file
struct CompareDetails
{
    int fieldIdx_;   // compare the ith field 
    double delta_;   // allow this much difference

    CompareDetails() : fieldIdx_(-1), delta_(0) {};
    bool empty(){ return (fieldIdx_ == -1); }
};

// Read from file and save each line in vector.
// Save optional info about allowed tolerance in compareDetails.
void getSkipLines(const char* fileName, 
		  vector<string>& skipLines, 
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
        skipLines.push_back(line);
        getline(infile, line);
    }

    infile.close();
}

// Split the given string on tab, extract the nth field, cast to double,
// save the beginning and ending indexes of the field in the string.
double getField(int fieldIdx, const string& line, 
		size_t& finalStart, size_t& finalEnd){

    size_t start = line.find('\t');
    int counter = 1;

    // find the beginning of the field
    while( (counter < fieldIdx) && (start != string::npos )){
        start = line.find('\t', start + 1); // look for the next tab
        counter++;
    }

    size_t end = line.find('\t', start + 1);
    string field = line.substr(start + 1, end - start - 1);
    finalStart = start + 1;
    finalEnd = end;

    return atof(field.c_str());
}


// Return true if the lines match, false if not.
// Use the information in CompareDetails to decide if an exact
// text match is required.
bool linesMatch(const string& expected,
	       	const string& observed,
	       	CompareDetails& details){

    // account for Windows line endings
    size_t length = observed.size() ;
    if( (observed.size() > 0) && (observed[observed.size() - 1] == '\r') ){
        length--;
    }

    // they match if both empty
    if(expected.empty() && length == 0 ){
        return true;
    } else if ( expected.empty() || observed.empty() ){// not if only one empty
        return false;
    }

    if( observed.compare(0, length, expected) == 0){
        return true;
    }

    // now compare a single field for a small numerical difference, if given
    if( details.empty() ){
        return false;
    }

    // else, get a substr from each, cast to double and compare with delta
    size_t expStart, expEnd, obsStart, obsEnd;
    double expectedField = getField(details.fieldIdx_, expected, 
                                    expStart, expEnd);
    double observedField = getField(details.fieldIdx_, observed, 
                                    obsStart, obsEnd);

    // get absolute value of difference between the two
    double diff = expectedField - observedField;
    if( diff < 0 ) { diff = diff * -1; }

    if( diff > details.delta_ ){
        return false;
    }

    // also compare remaining string, after the field is removed
    string expectedRemaining = expected;
    expectedRemaining.erase(expStart, expEnd - expStart);
    string observedRemaining = observed;
    observedRemaining.erase(obsStart, obsEnd - obsStart);
    
    if( observedRemaining.compare(0, length - (obsEnd - obsStart), 
                                  expectedRemaining) == 0){
        return true;
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
