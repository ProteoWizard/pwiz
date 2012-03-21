#include <iostream>
#include <fstream>
#include <string>
#include <cstring>
#include <stdlib.h>
#include <vector>
#include "sqlite3.h"
#include "smart_stmt.h"

using namespace std;
using namespace BiblioSpec;

// Information for replacing part of a string.
// Replace text between start and end with replace_str.
// Use str if not null, else use pos for defining text to replace.
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
                      vector<string>& outputLines){

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
            result += reinterpret_cast<const char*>(
                          sqlite3_column_text(sqlStatement, i));
        }
        Replace(findReplace, result);
        outputLines.push_back(result);

        returnCode = sqlite3_step(sqlStatement);
    }

}


// Select the contents of the library and store it, line by line, in the 
// given vector
void getObserved(const char* libName, vector<string>& outputLines){

    // create a sqlite connection
    sqlite3* db = NULL;
    sqlite3_open(libName, &db);

    // list the select statements to execute and changes for the output
    vector<const char*> selectStrings;
    FindReplace fp;
    vector<FindReplace> deleteText;
    selectStrings.push_back("SELECT libLSID, numSpecs, majorVersion, minorVersion FROM LibInfo");
    deleteText.push_back(fp);
    selectStrings.push_back("select * from Modifications"); 
    deleteText.push_back(fp);
    selectStrings.push_back("select * from RefSpectra");
    deleteText.push_back(fp);
    selectStrings.push_back("select * from SpectrumSourceFiles");
    fp.find_str_start_ = "	";
    fp.find_str_end_ = "/BiblioSpec/tests";
    fp.replace_str_ = "	";
    deleteText.push_back(fp);
    fp.clear();
    selectStrings.push_back("select * from ScoreTypes");
    deleteText.push_back(fp);
    selectStrings.push_back("select * from RetentionTimes");
    deleteText.push_back(fp);

    cerr << "after listing commands and delete there are " <<
        selectStrings.size() << " and " << deleteText.size() << " of each" << endl;
    // for each statement, execute on the db and put the results in outputLines
    for(size_t i = 0; i < selectStrings.size(); i++){
        cerr << "Selecting index " << i << endl;
        statementToLines(db, selectStrings[i], deleteText[i], outputLines);
    }
    sqlite3_close(db);
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

int main(int argc, char** argv){

    cerr << "Shall we begin?" << endl;
    string usage = "CompareLibraryContents <library> <expected output>";

    if( argc < 2 ){
        cerr << usage << endl;
        exit(1);
    }
    
    string libName = argv[1];
    string expectedOutput = argv[2];
    if( libName.find("blib") == string::npos ){
        libName = argv[2];
        expectedOutput = argv[1];
    }

    // extract what is in the library
    cerr << "Extracting the contents of " << libName << endl;
    vector<string> outputLines;
    getObserved(libName.c_str(), outputLines);

    // compare it to the file of expected results
    cerr << "Comparing library to " << expectedOutput << endl;
    ifstream compareFile(expectedOutput.c_str(), ifstream::in);

    // for now do the stupidest thing, compare line i with line i and report if
    // any do not match
    size_t lineNum = 0;
    string expected;
    getline(compareFile, expected);

    while( !compareFile.eof() ){
        cerr << "read expected line '" << expected << "'" << endl;

        if( lineNum >= outputLines.size() ){
            cerr << "The expected input has more lines than what was observed ("
                 << outputLines.size() << ")" << endl;
            exit(1);
        }

        string& observed = outputLines[lineNum];
        cerr << "read observed line '" << observed << "'" << endl;
        if( expected != observed ){
            cerr << "Line " << lineNum + 1 << " of the output does not match "
                 << endl;
            printObserved(outputLines, libName);
            exit(1);
        }

        getline(compareFile, expected);
        lineNum++;
    }

    if( lineNum < outputLines.size() ){
        cerr << "Observed output has more lines (" << outputLines.size()
             << ") than expected (" << lineNum << endl;
        exit(1);
    }
    cerr << "All output matches" << endl;

    compareFile.close();
}


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
