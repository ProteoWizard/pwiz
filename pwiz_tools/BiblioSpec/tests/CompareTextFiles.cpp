#include <iostream>
#include <algorithm>
#include <fstream>
#include <string>
#include <stdlib.h>
#include <vector>

using namespace std;

// Read from file and save each line in vector
void getSkipLines(const char* fileName, vector<string>& skipLines){
    ifstream infile(fileName);
    if( !infile.good() ){
        cerr << "Could not open '" << fileName << "'.  No lines being skipped" 
             << endl;
        return;
    }

    string line;
    getline(infile, line);
    while( !infile.eof() ){
        skipLines.push_back(line);
        getline(infile, line);
    }

    infile.close();
}

// Take as input two text files and report if the differ
int main (int argc, char** argv){

    cerr << "Hit it" << endl;
    string usage = "CompareTextFiles <expected> <observed> [<skip lines>]";

    if( argc < 3 ){
        cerr << usage << endl;
        exit(1);
    }

    cerr << "argc is " << argc << endl;
    cerr << "input was " << endl;
    for(int i = 0; i < argc; i++){
        cerr << i << ": " << argv[i] << endl;
    }
    // since we can't rely on the order , sort them alphabetically
    vector<string> tokens;
    tokens.push_back(argv[1]);
    tokens.push_back(argv[2]);
    if( argc > 3 ){
        tokens.push_back(argv[3]);
    }
    sort(tokens.begin(), tokens.end());
    cerr << "input now is " << endl;
    for(int i = 0; i < tokens.size(); i++){
        cerr << i << ": " << tokens[i] << endl;
    }
    string observedName = tokens[0];
    string expectedName = tokens[1];

    cerr << "Comparing expected, " << expectedName 
         << ", to observed, " << observedName << endl;

    // get the text from any lines that should not be compared
    vector<string> skipLines;
    if( argc > 3 ){
        getSkipLines(tokens[2].c_str(), skipLines);
    }
    cerr << "We have " << skipLines.size() << " lines to skip" << endl;


    ifstream expectedFile(expectedName.c_str());
    if( !expectedFile.good() ){
        cerr << "Could not open first file, '" << expectedName << "'" << endl;
        exit(1);
    }

    ifstream observedFile(observedName.c_str());
    if( !observedFile.good() ){
        cerr << "Could not open second file, '" << observedName << "'" << endl;
        exit(1);
    }

    int lineNum = 1;
    vector<string>::iterator skipLinesIter = skipLines.begin();
    vector<string>::iterator lastLine = skipLines.end();
    string expected;
    string observed;
    getline(expectedFile, expected);
    while( !expectedFile.eof() ){
        if( observedFile.eof() ){
            cerr << "The expected file has more lines than observed ("
                 << lineNum << ")" << endl;
            exit(1);
        }
        getline(observedFile, observed);

        // should we compare this line or skip it?
        if( skipLinesIter < lastLine 
            && expected.find(*skipLinesIter) != string::npos ){
            cerr << "skipping line " << lineNum << endl;
            lineNum++;
            getline(expectedFile, expected);
            skipLinesIter++;
            continue;
        }

        if( expected != observed ){
            cerr << "Line " << lineNum << " differs." << endl;
            cerr << "expected: " << expected << endl;
            cerr << "observed: " << observed << endl;
            exit(1);
        }
        lineNum++;

        getline(expectedFile, expected);
    }

    // check the observed file for extra lines
    getline(observedFile, observed);
    if( !observedFile.eof() ){
        cerr << "The observed file has more lines than the expected ("
             << lineNum << ")" << endl;
        exit(1);
    }

    cerr << "All " << lineNum << " lines match" << endl;

    expectedFile.close();
    observedFile.close();

}




/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
