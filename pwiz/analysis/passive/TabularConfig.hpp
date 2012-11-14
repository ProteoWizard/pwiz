//
//
//
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#ifndef _TABULAR_HPP_ 
#define _TABULAR_HPP_ 

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Export.hpp"

namespace pwiz {
namespace analysis {


struct PWIZ_API_DECL TabularConfig
{
    PWIZ_API_DECL enum Delimiter
    {
        Delimiter_FixedWidth,
        Delimiter_Space,
        Delimiter_Comma,
        Delimiter_Tab
    };
    // for ease of extending this list and getting the usage statements right everywhere
#define DELIMITER_OPTIONS_STR "delimiter=fixed|space|comma|tab"
#define DELIMTER_USAGE_STR "delimiter: sets column separation; default is fixed width"
    /// delimiter between columns (unless set to Delimiter_FixedWidth) 

    char getDelimiterChar() const;
    string getFileExtension() const;
    bool checkDelimiter(const string& arg); // set delimiter_ and return true iff arg is a valid delimiter setting 
 protected:
    TabularConfig();
    Delimiter delimiter_; // gets set by checkDelimiter()
};

} // namespace analysis 
} // namespace pwiz


#endif // _TABULAR_HPP_ 

