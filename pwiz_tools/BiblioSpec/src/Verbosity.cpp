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
/*
 * FILE: verbosity.cpp
 * PROJECT: srm-finder
 * AUTHOR: Barbara Frewen
 * DATE: January 17, 2008
 * FUNCTION: Static functions and variables for sterr output
 * REVISION: $Revision 1.0$
 */

#include "Verbosity.h"

namespace BiblioSpec {

V_LEVEL Verbosity::Global_Verbosity = V_STATUS;
FILE* Verbosity::log_file = NULL;
char Verbosity::msg_buffer[1024];

void Verbosity::set_verbosity(V_LEVEL level) {
    Verbosity::Global_Verbosity = level;
    Verbosity::msg_buffer[0] = '\0';
}

void Verbosity::open_logfile(){
    Verbosity::log_file = fopen("blib-build.log", "w");
    if( Verbosity::log_file == NULL ){
        Verbosity::error("Cannot open log file 'blib-build.log'.");
    }
}

void Verbosity::close_logfile(){
    if( Verbosity::log_file != NULL ){
        fclose(log_file);
    }
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
void Verbosity::comment(V_LEVEL print_level, 
                        const char* format, ...) {

    if(Verbosity::Global_Verbosity < print_level ) {
        return;
    }
  
    // for appending to the message buffer
    char* cur_buffer_position = Verbosity::msg_buffer;
    int added = 0;

    switch(print_level) {
    case V_ERROR:
        added = sprintf(cur_buffer_position, "ERROR: ");
        cur_buffer_position += added;
        break;
    case V_WARN:
        added = sprintf(cur_buffer_position, "WARNING: ");
        cur_buffer_position += added;
        break;
    case V_STATUS:
        // No extra prefix for status.  Too ugly for command-line use.
        break;
    case V_DEBUG:
    case V_DETAIL:
    case V_ALL:
        added = sprintf(cur_buffer_position, "DEBUG: ");
        cur_buffer_position += added;
        break;
    case V_SILENT:
        break; // print nothing, shouldn't have gotten to here anyway
    }

    va_list args;
    va_start(args, format);

    added = vsprintf(cur_buffer_position, format, args);
    cur_buffer_position += added;
    sprintf(cur_buffer_position, "\n");
    va_end(args);

    // if no log file, print all levels to stderr
    if( Verbosity::log_file == NULL ) {
        cerr << Verbosity::msg_buffer << flush;
    } else {  // print all levels to file
        fprintf(log_file, "%s", Verbosity::msg_buffer);
        if( print_level <= V_STATUS ) {
            cerr << Verbosity::msg_buffer << flush;
        }
    }
    
    if( print_level == V_ERROR ){
	
	if( Verbosity::log_file != NULL ) {
	    fclose(log_file);
	}
	exit(1);
    }

    Verbosity::msg_buffer[0] = '\0';
    return;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
