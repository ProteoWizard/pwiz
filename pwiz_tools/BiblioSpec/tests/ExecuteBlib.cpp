#include <iostream>
#include <string>
#include <cstring>
#include <stdlib.h>
#include <vector>
#include <algorithm>

using namespace std;

int main(int argc, char** argv){

    string usage = "ExecuteBlib <blib tool> [<inputs>+] ";

    // require a Blib executable and then any number of inputs for it
    if( argc < 2 ){
        cerr << usage << endl;
        exit(1);
    }

    // this absurd bjam run rule requires that input files be
    // listed alphabetically!!
    // hunt down the components separately: executable, options, inputs, lib
    string command; // must contain BlibBuild, BlibFilter, BlibSearch, BlibToMs2
    string options; // must start with '-'
    vector<string> libNames; // collect so we can sort them
    string libName; // must end with '.blib'
    const char* libExt = ".blib";
    string inputs;  // all else
    for(int i = 1; i < argc; i++){
        string token = argv[i];
        if( token[0] == '-' ){
            // replace any _ with ' ' so options can have args
            size_t position = token.find('_');
            if( position != string::npos ){
                token.replace(position, 1, " ");
            }
            options += token;
            options += " ";
        } else if( token.find("BlibBuild") != string::npos ||
                   token.find("BlibFilter") != string::npos ||
                   token.find("BlibToMs2") != string::npos ||
                   token.find("BlibSearch") != string::npos ){
            command = token; // check that only one token matches?
            command += " ";
        } else if ( token.compare(token.length() - strlen(libExt),
                                  strlen(libExt), libExt) == 0 ){
            cerr << "adding lib '" << token << endl;
            libNames.push_back(token);
            //            libName += token; 
            //            libName += " ";
        } else{
            inputs += token;
            inputs += " ";
        }
    }

    // for multiple libs, put them in alphabetical order since we have no
    // guarantee how they will be passed to this
    sort(libNames.begin(), libNames.end());
    for(size_t i = 0; i < libNames.size(); i++){
        libName += libNames[i];
        libName += " ";
    }

    string fullCommand = command + options + inputs + libName;
    cerr << "Running " << fullCommand << endl;

    int returnValue = system(fullCommand.c_str());
    cerr << "System returned " << returnValue << endl;

    // why does 'return returnValue' exit with 0 when returnValue != 0

    if( returnValue != 0 ){
        return 1;
    }
}





/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
