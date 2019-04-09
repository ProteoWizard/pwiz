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
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Container.hpp"

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








