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

#include "Compare.h"
#include "pwiz/utility/misc/unit.hpp"
#include <cstring>
#include <stdlib.h>
#include "sqlite3.h"
#include "smart_stmt.h"

using namespace BiblioSpec;

// Information for replacing part of a string.
// Replace text between start and end with replace_str.
// To define the text to replace, use str if not null, else use pos.
// e.g.  find_str_start_ == NULL and find_str_end == "here"
// then replace text starting at find_pos_start_ and ending at the
// character before first occurance of "here".
// Delete the text if replace_str_ is NULL or empty.
struct FindReplace {

    const char* find_str_start_;
    const char* find_str_end_;
    size_t find_pos_start_;
    size_t find_pos_end_;
    const char* replace_str_;

    FindReplace(){
        find_str_start_ = NULL;
        find_str_end_ = NULL;
        find_pos_start_ = string::npos;
        find_pos_end_ = string::npos;
        replace_str_ = NULL;
    }

    FindReplace(const char* find_str_start,
            const char* find_str_end,
            size_t find_pos_start,
            size_t find_pos_end,
            const char* replace_str){
        find_str_start_ = find_str_start;
        find_str_end_ = find_str_end;
        find_pos_start_ = find_pos_start;
        find_pos_end_ = find_pos_end;
        replace_str_ = replace_str;
    }

    void clear(){
        find_str_start_ = NULL;
        find_str_end_ = NULL;
        find_pos_start_ = string::npos;
        find_pos_end_ = string::npos;
        replace_str_ = NULL;
    }

    // true if no replacement to make
    bool empty(){
        return ( find_str_start_ == NULL && find_str_end_ == NULL
                 && find_pos_start_ == string::npos );
    }
};

// Use info to modify the given string.
void Replace(FindReplace& info, string& workingString){

    if( info.empty() ){ return; }

    // use either the given start and end positions
    // or set them based on the text
    size_t start = info.find_pos_start_;
    size_t end = info.find_pos_end_;

    if( info.find_str_start_ != NULL && strlen(info.find_str_start_) != 0 ){
        start = workingString.find(info.find_str_start_);
        // if not found, do no replacement
        if( start == string::npos ){
            return;
        }
    } 
    if( info.find_str_end_ != NULL && strlen(info.find_str_end_) != 0 ){
        end = workingString.find(info.find_str_end_, start);
        // if we didn't find it, don't replace anything
        if( end == string::npos ){ return; }
    }

    // get the length of the section to replace
    size_t length = end - start;

    // replace the selected substring
    const char* replace = (info.replace_str_ == NULL) ? "" : info.replace_str_;
    workingString.replace(start, length, replace);
}

// Execute the given select statement on the given database
// and store the results in the vector of strings (one row per string)
void statementToLines(sqlite3* dbConnection, 
                      const char* selectString,
                      FindReplace& findReplace,
                      vector<string>& outputLines,
                      bool swapSlash){

    smart_stmt sqlStatement;
    int returnCode = sqlite3_prepare(dbConnection, selectString,
                                     -1, &sqlStatement, 0);

    if( returnCode != SQLITE_OK ){ // no input to add, decide later if error
        return;
    }

    int returnedColumns = sqlite3_column_count(sqlStatement);

    // first get the column names
    string columns = sqlite3_column_name(sqlStatement, 0);
    for(int i = 1; i < returnedColumns; i++){
        columns += "	";
        columns += sqlite3_column_name(sqlStatement, i);
    }
    outputLines.push_back(columns);

    returnCode = sqlite3_step(sqlStatement);
    while( returnCode == SQLITE_ROW ){
        string result;
        result += reinterpret_cast<const char*>(
                       sqlite3_column_text(sqlStatement,0));
        for(int i = 1; i < returnedColumns; i++){
            result += "	";
            const char* val = reinterpret_cast<const char*>(
                sqlite3_column_text(sqlStatement, i));
            result += ((val == NULL || strlen(val) == 0) ? "N/A" : val);
        }

        if( swapSlash ){
            size_t position = result.find( "\\" ); 

            while ( position != string::npos ) {
              result.replace( position, 1, "/" );
              position = result.find( "\\", position + 1 );
           } 
        }
        Replace(findReplace, result);
        outputLines.push_back(result);

        returnCode = sqlite3_step(sqlStatement);
    }
}

// List the select statements for the library along with any text
// replacements to make and when to fix paths
void getSelectInfo(vector<const char*>& selectStrings, 
                   vector<FindReplace>& deleteText,
                   vector<bool>& swapSlash){
    FindReplace fp;
    selectStrings.push_back("SELECT libLSID, numSpecs, majorVersion, minorVersion FROM LibInfo");
    deleteText.push_back(fp);
    swapSlash.push_back(false);
    selectStrings.push_back("select * from Modifications"); 
    deleteText.push_back(fp);
    swapSlash.push_back(false);
    selectStrings.push_back("select * from RefSpectra");
    deleteText.push_back(fp);
    swapSlash.push_back(false);
    selectStrings.push_back("select * from SpectrumSourceFiles");
    fp.find_str_start_ = "	";
    fp.find_str_end_ = "/BiblioSpec/tests";
    fp.replace_str_ = "	";
    deleteText.push_back(fp);
    fp.clear();
    swapSlash.push_back(true);
    selectStrings.push_back("select * from ScoreTypes");
    deleteText.push_back(fp);
    swapSlash.push_back(false);
    selectStrings.push_back("select * from RetentionTimes");
    deleteText.push_back(fp);
    swapSlash.push_back(false);
}


// Select the contents of the library and store it, line by line, in the 
// given vector
void getObserved(const char* libName, vector<string>& outputLines){

    // create a sqlite connection
    sqlite3* db = NULL;
    sqlite3_open(libName, &db);

    // list the select statements to execute and changes for the output
    vector<const char*> selectStrings;
    vector<FindReplace> deleteText;
    vector<bool> swapSlash;
    getSelectInfo(selectStrings, deleteText, swapSlash);

    // for each statement, execute on the db and put the results in outputLines
    for(size_t i = 0; i < selectStrings.size(); i++){
        statementToLines(db, selectStrings[i], deleteText[i], 
                         outputLines, swapSlash[i]);
    }
    sqlite3_close(db);
}

// remove the output of printObserved from any prior runs
void removeObservedFiles(const string& libName){
    string outName = libName;
    outName += ".observed";
    remove(outName.c_str());
}

// When the output does not match the expected, print the observed output
// to a file named <libName>.observed
void printObserved(vector<string>& outputLines, string& libName){
    string outName = libName;
    outName += ".observed";
    ofstream outfile(outName.c_str());
    if( !outfile.good() ){
        cerr << "Could not open file '" << outName << "'" << endl;
        return;
    }

    for(size_t i = 0; i < outputLines.size(); i++){
        outfile << outputLines[i] << endl;
    }
    outfile.close();
}

// Extract a pre-defined set of queries from the given library and
// compare it to the given text file.  Optionally, give a list of
// lines to skip and/or fields that do not require an exact text match
int test (const vector<string>& args)
{
    string usage ="CompareLibraryContents <library> <expected output> [<skip>]";
    
    if( args.size() < 3 )
        throw runtime_error(usage);
    
    // since we can't rely on the order , sort them alphabetically
    vector<string> tokens;
    tokens.push_back(args[1]);
    tokens.push_back(args[2]);
    if( args.size() > 3 )
        tokens.push_back(args[3]);

    sort(tokens.begin(), tokens.end());

    string libName = tokens[0];
    string expectedOutput = tokens[1];

    CompareDetails compareDetails;
    if( args.size() > 3 )
        getSkipLines(tokens[2].c_str(), compareDetails);

    // clean up from previous tests
    removeObservedFiles(libName);

    // extract what is in the library
    cerr << "Extracting the contents of " << libName << endl;
    vector<string> outputLines;
    getObserved(libName.c_str(), outputLines);

    // compare it to the file of expected results
    cerr << "Comparing library to " << expectedOutput << endl;
    ifstream compareFile(expectedOutput.c_str(), ifstream::in);

    // for now do the stupidest thing, compare line i with line i and
    // report as soon as any do not match
    size_t lineNum = 0;
    string expected;
    getline(compareFile, expected);

    while( !compareFile.eof() )
    {
        if( lineNum >= outputLines.size() )
        {
            printObserved(outputLines, libName);
            throw runtime_error("The expected input has more lines than what was observed (" +
                                lexical_cast<string>(outputLines.size()) + ")");
        }

        string& observed = outputLines[lineNum];

        if( ! linesMatch(expected, observed, compareDetails) )
        {
            cerr << "Line " << lineNum + 1 << " differs." << endl;
            cerr << "expected: " << expected << endl;
            cerr << "observed: " << observed << endl;
            printObserved(outputLines, libName);
            throw runtime_error("Line " + lexical_cast<string>(lineNum + 1) + " differs.");
        }

        getline(compareFile, expected);
        lineNum++;
    }

    if (lineNum < outputLines.size())
    {
        printObserved(outputLines, libName);
        throw runtime_error("Observed output has more lines (" + lexical_cast<string>(outputLines.size()) +
                            ") than expected (" + lexical_cast<string>(lineNum) + ")");
        }
    cerr << "All output matches" << endl;

    return 0;
}


int main(int argc, char* argv[])
{
    try
    {
        return test(vector<string>(argv, argv+argc));
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (const char* msg)
    {
        cerr << msg << endl;
        return 1;
    }
    catch (string msg)
    {
        cerr << msg << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception." << endl;
        return 1;
    }
}




/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
