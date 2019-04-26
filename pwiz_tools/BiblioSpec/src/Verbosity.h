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

#ifndef VERBOSITY_T
#define VERBOSITY_T

#include <cstdlib>
#include <string.h>
#include <iostream>
#include <stdio.h>
#include <sstream>
#include <cstdarg>


namespace BiblioSpec {

enum V_LEVEL { V_SILENT, ///< 0 no output to stderr
               V_ERROR,  ///< 1 only fatal error messages
               V_STATUS, ///< 2 current step of execution
               V_WARN,   ///< 3 non-fatal errors
               V_DEBUG,  ///< 4 extra info for debugging
               V_DETAIL, ///< 5 more info
               V_ALL };  ///< 6 way more output than you should ever need

class Verbosity{
 private:
  static V_LEVEL Global_Verbosity;
  static FILE* log_file;
  static char msg_buffer[1024];

 public:
  static V_LEVEL string_to_level(const char*);
  static void set_verbosity(V_LEVEL level);
  static void open_logfile();
  static void close_logfile();
  static void error(const char*, ...);
  static void warn(const char*, ...);
  static void status(const char*, ...);
  static void debug(const char*, ...);
  static void comment(V_LEVEL, const char*, ...);
 
};

} // namespace
#endif //VERBOSITY_T



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
