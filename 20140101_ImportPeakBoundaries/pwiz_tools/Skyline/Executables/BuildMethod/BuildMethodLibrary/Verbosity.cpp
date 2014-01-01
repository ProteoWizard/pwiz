/*
 * Original author: Barbara Frewen <frewen .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
/*
 * FILE: verbosity.cpp
 * PROJECT: srm-finder
 * AUTHOR: Barbara Frewen
 * DATE: January 17, 2008
 * FUNCTION: Static functions and variables for sterr output
 * REVISION: $Revision 1.0$
 */

#include "Verbosity.h"

V_LEVEL Verbosity::Global_Verbosity = V_STATUS;

void Verbosity::set_verbosity(V_LEVEL level, bool use_log) {
    Verbosity::Global_Verbosity = level;

    /*
      if( use_log ) {
      Verbosity::log_file = new ofstream("blib-build.log");
      } else {
      Verbosity::log_file = NULL;
      }
    */
}

V_LEVEL Verbosity::string_to_level(const char* level_str) {
    if( strcmp(level_str, "silent")==0) {
        return V_SILENT;
    }
    if( strcmp(level_str, "error")==0) {
        return V_ERROR;
    }
    if( strcmp(level_str, "warn")==0) {
        return V_WARN;
    }
    if( strcmp(level_str, "status")==0) {
        return V_STATUS;
    }
    if( strcmp(level_str, "debug")==0) {
        return V_DEBUG;
    }
    if( strcmp(level_str, "detail")==0) {
        return V_DETAIL;
    }
    if( strcmp(level_str, "all")==0) {
        return V_ALL;
    }

    string error_msg = "The verbosity level ";
    error_msg += level_str;
    error_msg += " is not valid";
    throw error_msg;
}

/**
 * Print a message to stderr and exit.
 * Equivalent to comment(V_ERROR,,)
 */
void Verbosity::error(const char* format, ...){
    va_list args;
    va_start(args, format);

    char buffer[1024];
    vsprintf(buffer, format, args);

    Verbosity::comment(V_ERROR, buffer);
}

/**
 * Print a message to stderr if the verbosity level is at or above "warn". 
 * Equivalent to comment(V_WARN,,)
 */
void Verbosity::warn(const char* format, ...){
    va_list args;
    va_start(args, format);

    char buffer[1024];
    vsprintf(buffer, format, args);

    Verbosity::comment(V_WARN, buffer);
}

/**
 * Print a message to stderr if the verbosity level is at or above "status". 
 * Equivalent to comment(V_STATUS,,)
 */
void Verbosity::status(const char* format, ...){
    va_list args;
    va_start(args, format);

    char buffer[1024];
    vsprintf(buffer, format, args);

    Verbosity::comment(V_STATUS, buffer);
}

/**
 * Print a message to stderr if the verbosity level is at or above "debug". 
 * Equivalent to comment(V_DEBUG,,)
 */
void Verbosity::debug(const char* format, ...){
    va_list args;
    va_start(args, format);

    char buffer[1024];
    vsprintf(buffer, format, args);

    Verbosity::comment(V_DEBUG, buffer);
}

/**
 * Print message to stderr if requested verbosity level is at or above
 * the global verbosity level.  Prepend errors, warnings, and debug
 * statements with ERROR, WARNING, DEBUG.  Exit on V_ERROR.
 * 
 */
// TODO: to add log file printing, create a Verbosity::print private
// function that prints to stderr and to log file and replace all cerr
// with Verbosity::print();
void Verbosity::comment(V_LEVEL print_level, 
                        const char* format, ...) {

    if(Verbosity::Global_Verbosity < print_level ) {
        return;
    }
  
    switch(print_level) {
    case V_ERROR:
        cerr << "ERROR: ";
        break;
    case V_WARN:
        cerr << "WARNING: ";
        break;
    case V_STATUS:
        // No extra prefix for status.  Too ugly for command-line use.
        // cerr << "STATUS: ";
        break;
    case V_DEBUG:
    case V_DETAIL:
    case V_ALL:
        cerr << "DEBUG: ";
        break;
    case V_SILENT:
        break; // print nothing, shouldn't have gotten to here anyway
    }

    va_list args;
    va_start(args, format);

    vfprintf(stderr, format, args);
    va_end(args);
    cerr << endl;

    // print also to log file 
    /*    
    if( Verbosity::log_file != NULL ) {
        va_start(args, format);

        vfprintf(Verbosity::logfile, format, args);
        cerr << endl;
    */

    if( print_level == V_ERROR ){
        exit(1);
        // if log file, close file
    }
    return;
}


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
