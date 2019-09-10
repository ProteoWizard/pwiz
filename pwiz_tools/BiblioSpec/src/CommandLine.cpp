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
 * \file CommandLine.cpp
 *
 * A class for parsing the command line, reading optional parameter
 * files, and storing values.  The caller must provide an
 * options_description object to define what is permissable on the
 * command line, what is permissable in a config file (if different),
 * and what the required arguments are (if any).  Caller also provides
 * a variables_map object for storing values.  That object can then be
 * shared between classes.
 *
 */

#include "CommandLine.h"
#include "Verbosity.h"
#include "pwiz/utility/misc/Std.hpp"

namespace BiblioSpec {

CommandLine::CommandLine(const char* exeName,
                         const options_description& options,
                         const vector<const char*>& requiredArgNames,
                         bool repeatLast)
  : requiredArgNames_( requiredArgNames )
{
    // create the usage line
    createUsage(exeName, repeatLast);
    
    // create descriptions and positional info for required args
    options_description requiredArguments("Required arguments");
    initRequiredArgs(requiredArguments, repeatLast);
    
    // create common options
    options_description commonOptions = options;
    commonOptions.add_options()
        ("parameter-file,p", 
         //ops::value<string>()->default_value("blib.params"),
         value<string>(),
         "File containing search parameters.  Command line values override "
         "file values.")
        
        ("verbosity,v", 
         value<string>()->default_value("status"), 
         "Control the level of output to stderr. (silent, error, status"
         ", warn, debug, detail, all)  Default status.")
        
        ("help,h", "Print help message.")
        ;
    
    // combine all into the description used for parsing
    cmdlineOptions_.add(commonOptions).add(requiredArguments);
    fileOptions_.add(commonOptions);
    helpOptions_.add(commonOptions);
    
}

/**
 * Define visible options that are allowed in the parameter file only,
 * not on the command line.
 */
void CommandLine::addParamFileOptions(const options_description& options){
    helpOptions_.add(options);
    fileOptions_.add(options);
}

/**
 * Define options allowed on the command line or in the parameter file
 * but that will not be displayed in the help message.
 */
void CommandLine::addHiddenOptions(const options_description& options){
    cmdlineOptions_.add(options);  
    fileOptions_.add(options);
}

/**
 * Read the command line and store the values in the options table.
 */
void CommandLine::parse(int argc,
                        char** const argv,
                        variables_map& options_table) {
    try{
        
        // parse command line and store values
        store(command_line_parser(argc, argv).
              options(cmdlineOptions_).positional(argsPosition_).run(),
              options_table);
        
        // special case for help
        if( options_table.count("help")) {
            printHelp(helpOptions_);
            exit(0);
        }
        
        // parse parameter file, if it's there
        if( options_table.count("parameter-file")) {
            string paramFileName = options_table["parameter-file"].as<string>();
            ifstream paramFile(paramFileName.c_str());
            if( !paramFile ) {
                // verbosity  not set before cmdline parsed
                cerr << "Could not open parameter file " 
                     << paramFileName.c_str() << endl;
                exit(1);
            }
            store(parse_config_file(paramFile, fileOptions_), options_table);
        }
        notify(options_table);
        
        // check for all required arguments
        for(size_t i = 0; i < requiredArgNames_.size(); i++){
            if( !options_table.count(requiredArgNames_.at(i))) {
                cerr << "ERROR: Missing required argument '"
                     << requiredArgNames_.at(i) << "'." << endl;
                printHelp(helpOptions_);
                exit(1);
            }
        }
        
        // set the verbosity
        setVerbosity(options_table);

    } catch(exception& e) {
        // instead of error() so we can print help after
        cerr << "ERROR: " << e.what() << "." << endl << endl;  
        printHelp(helpOptions_);
        exit(1);
    } catch(...) {
        cerr << "Encountered exception of unknown type while " 
             << "parsing command line." << endl;
        exit(1);
    }
    
    
}


/**
 * Set the usage_ string to
 * Usage: exeName [options] <arg1> <arg2>...[+]
 */
void CommandLine::createUsage(const char* exeName, 
                              bool repeatLast){
    usage_ = "Usage: ";
    usage_ += exeName;
    usage_ += " [options]";
    
    for(size_t i = 0; i < requiredArgNames_.size(); i++){
        usage_ += " <";
        usage_ += requiredArgNames_.at(i);
        usage_ += ">";
    }
    
    if( repeatLast ){
        usage_ += "[+]";
    }
}

/**
 * Create the descriptions and positional information for the required
 * arguments.  Special handling for the last argument if it can be
 * repeated.
 */
void CommandLine::initRequiredArgs(options_description& requiredArguments,
                                   bool repeatLast)
{
    // create the descriptons for required args (handle last separately)
    int numReqArgs = requiredArgNames_.size();
    for(int i = 0; i < numReqArgs - 1; i++){
        requiredArguments.add_options()
            (requiredArgNames_.at(i),
             value<string>(),
             "req arg")
            ;
    }
    // if last arg repeated, use vector of strings
    if( repeatLast ){
        requiredArguments.add_options()
            (requiredArgNames_.at(numReqArgs - 1),
             value< vector<string> >()->composing(),
             "req arg")
            ;
    } else { // just add the last
        requiredArguments.add_options()
            (requiredArgNames_.at(numReqArgs - 1),
             value<string>(),
             "req arg")
            ;
    }
    
    // create positional info for required args (handle last separately)
    for(int i = 0; i < numReqArgs - 1; i++){
        argsPosition_.add(requiredArgNames_.at(i), i + 1);
    }
    // if last arg repeated, set position to -1
    int position = (repeatLast) ? -1 : requiredArgNames_.size() ;
    argsPosition_.add(requiredArgNames_.at(numReqArgs - 1), position);
    
}

/**
 * Set the global verbosity level based on the values in the given table.
 */
void CommandLine::setVerbosity(variables_map& options_table){
    try{
        string v_name = options_table["verbosity"].as<string>();
        V_LEVEL level = Verbosity::string_to_level(v_name.c_str());
        Verbosity::set_verbosity(level);
    } catch(const string& e){
        cerr << e << endl;
        printHelp(helpOptions_);
        exit(1);
    }

}

} // namespace




/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
