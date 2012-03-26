#include <iostream>
#include <sstream>
#include <algorithm>
#include <fstream>
#include <string>
#include <stdlib.h>
#include <vector>

using namespace std;

struct CompareDetails
{
	int fieldIdx_;   // compare the ith field 
	double delta_;   // allow this much difference
    CompareDetails() : fieldIdx_(-1), delta_(0) {};
};

// Read from file and save each line in vector
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

        cerr << "from '" << line << "' got idx: " << compareDetails.fieldIdx_
             << "' delta " << compareDetails.delta_ << endl;
        getline(infile, line);
    }

    // now get remaining lines of text to skip
    while( !infile.eof() ){
        skipLines.push_back(line);
        getline(infile, line);
    }

    infile.close();
}

// split the given string on tab, extract the nth field, cast to double
double getField(int fieldIdx, const string& line, 
		size_t& finalStart, size_t& finalEnd){
    size_t start = line.find('\t');
    int counter = 1;

    // find the beginning of the field, if it isn't the first field
    while( (counter < fieldIdx) && (start != string::npos )){
        start = line.find('\t', start + 1); // look for the next tab
        counter++;
        cerr << "start is now " << start << " (" << line.at(start+1) << ")" << endl;
    }

    cerr << "Ended finding with counter = " << counter
	   << ", start = " << start << " and next char = " << line[start + 1] << endl;

    size_t end = line.find('\t', start + 1);
    cerr << "end is " << end << " and length is " << end - start << endl;
    string field = line.substr(start + 1, end - start - 1);
    cerr << "field is " <<  field << endl;
finalStart = start + 1;
finalEnd = end;

    return atof(field.c_str());
}


// return true if the lines match, false if not
// use the information in CompareDetails to decide if an exact
// text match is required
bool linesMatch(const string& expected,
	       	const string& observed,
	       	CompareDetails& details){

	cerr << "Linesmatch comparing '" << expected << "'" << endl;
	cerr << "with '" << observed << "'" << endl;
	cerr << "expected is empty? " << boolalpha << expected.empty() << endl;
	cerr << "observed is empty? " << boolalpha << observed.empty() << endl;

	// account for Windows line endings
    size_t length = observed.size() ;
    if( (observed.size() > 0) && (observed[observed.size() - 1] == '\r') ){
        length--;
	//cerr << "removing line ending, length now " << length << endl;
    }

	if(expected.empty() && length == 0 ){
		cerr << "empty lines" << endl;
		return true;
	} else if ( expected.empty() || observed.empty() ){
		return false;
	}

    if( observed.compare(0, length, expected) == 0){
    cerr << "lines match" << endl;
	    return true;
    }

// else, get a substr from each, cast to double and compare with delta
    size_t expStart, expEnd, obsStart, obsEnd;
    double expectedField = getField(details.fieldIdx_, expected, expStart, expEnd);
    double observedField = getField(details.fieldIdx_, observed, obsStart, obsEnd);

    double diff = expectedField - observedField;
    if( diff < 0 ) { diff = diff * -1; }
    cerr << "comparing " << expectedField << " and " << observedField << " with diff of " << diff << endl;
    if( diff > details.delta_ ){
        return false;
    }

// also compare remaining string
string expectedRemaining = expected;
expectedRemaining.erase(expStart, expEnd - expStart);
string observedRemaining = observed;
observedRemaining.erase(obsStart, obsEnd - obsStart);

cerr << "comparing remaining exp '" << expectedRemaining << "'" << endl;
cerr << "comparing remaining obs '" << observedRemaining << "'" << endl;
    if( observedRemaining.compare(0, length - (obsEnd - obsStart), expectedRemaining) == 0){
    cerr << "remaining lines match" << endl;
    return true;
    }
	return false;
}




/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
