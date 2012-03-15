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
/**
 * \file CommandLine.h
 *
 * A class for parsing the command line, reading optional parameter
 * files, and storing values.  The caller must provide an
 * options_description object to define what is permissable on the
 * command line, what is permissable in a config file (if different),
 * and what the required arguments are (if any).  Caller can
 * optionally give an options_description with hidden options which
 * are not included in help messages.  Caller also provides
 * a variables_map object for storing values.  That object can be
 * shared between classes for accessing the options/arguments values.
 *
 */

#pragma once

#include <iostream> 
#include <fstream>
#include "boost/program_options.hpp"

using namespace std;
using namespace boost::program_options;

namespace BiblioSpec {

class CommandLine{

 private:
    // collections of options for cmdline, file, help
    options_description cmdlineOptions_;    // args + generic + hidden
    options_description fileOptions_;       // generic + hidden
    options_description helpOptions_;       // generic + file

    positional_options_description argsPosition_; // position of req args
    vector<const char*> requiredArgNames_;        // names given by caller
    string usage_;     // exe name [options] <arg1> <arg2> ...

    /**
     * Set the _usage string to
     * Usage: exeName [options] <arg1> <arg2>...[+]
     */
    void createUsage(const char* exeName, 
                     bool repeatLast);
    /**
     * Create the descriptions and positional information for the
     * required arguments.
     */
    void initRequiredArgs(options_description& requiredArguments,
                          bool repeatLast);
    /**
     * Print usage statement and options info.
     */
    void printHelp(options_description& options){
        cerr << usage_ << endl;
        cerr << options<< endl;
    };
    
    void setVerbosity(variables_map& options_table);

 public:
    CommandLine(const char* exeName,
                const options_description& options,
                const vector<const char*>& requiredArgNames,
                bool repeatLast);
    
    ~CommandLine(){};
    
    void addParamFileOptions(const options_description& optons);
    void addHiddenOptions(const options_description& optons);

    void parse(int argc,
               char** const argv,
               variables_map& options_table);
    
};

/*
// TESTING
    // can we iterate over all values in the table
    for(ops::variables_map::iterator i = options_table.begin();
        i != options_table.end(); ++i){
        cerr << "Hey, anther option!" << flush;
        cerr << " key is " << i->first << flush;
            //<< " second is " << i->second << endl;
        const ops::variable_value& v = i->second;
        if( ! v.empty() ){
            const type_info& type = v.value().type();
            if( type == typeid(string)) {
                cerr << " value is " << v.as<string>() << endl;
            } else {
                cerr << endl;
            }

        }
        //        cerr << " value is " << v.value() << endl;
    }
    // END TESTING
    */

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */








