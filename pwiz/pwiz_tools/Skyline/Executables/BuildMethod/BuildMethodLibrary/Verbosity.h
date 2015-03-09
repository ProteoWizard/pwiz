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
 * FILE: verbosity.h
 * PROJECT: srm-finder
 * AUTHOR: Barbara Frewen
 * DATE: January 17, 2008
 * FUNCTION: Static functions and variables for sterr output
 * REVISION: $Revision 1.0$
 */

#ifndef VERBOSITY_T
#define VERBOSITY_T

#include <iostream>
#include <fstream>
#include <sstream>
#include <cstdarg>

using namespace std;

enum V_LEVEL { V_SILENT, ///< no output to stderr
               V_ERROR,  ///< only fatal error messages
               V_STATUS, ///< current step of execution
               V_WARN,   ///< non-fatal errors
               V_DEBUG,  ///< extra info for debugging
               V_DETAIL, ///< more info
               V_ALL };  ///< way more output than you should ever need

class Verbosity{
 private:
  static V_LEVEL Global_Verbosity;
  static ofstream* log_file;

 public:
  static V_LEVEL string_to_level(const char*);
  static V_LEVEL get_verbosity()
  { return Global_Verbosity; }
  static void set_verbosity(V_LEVEL level, bool write_log_file);
  static void set_verbosity(const char* level, bool write_log_file)
  { set_verbosity(string_to_level(level), write_log_file); }
  static void error(const char*, ...);
  static void warn(const char*, ...);
  static void status(const char*, ...);
  static void debug(const char*, ...);
  static void comment(V_LEVEL, const char*, ...);
 
};


#endif //VERBOSITY_T



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
