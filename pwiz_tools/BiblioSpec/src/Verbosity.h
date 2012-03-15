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
 * FILE: verbosity.h
 * PROJECT: srm-finder
 * AUTHOR: Barbara Frewen
 * DATE: January 17, 2008
 * FUNCTION: Static functions and variables for sterr output
 * REVISION: $Revision 1.0$
 */

#ifndef VERBOSITY_T
#define VERBOSITY_T

#include <cstdlib>
#include <string.h>
#include <iostream>
#include <stdio.h>
#include <sstream>
#include <cstdarg>

using namespace std;

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
