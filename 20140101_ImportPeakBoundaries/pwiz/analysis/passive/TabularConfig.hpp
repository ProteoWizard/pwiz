//
// $Id$
//
//
// Original author: John Chilton <jmchilton .@. gmail.com>
//
// Copyright 2012 University of Minnesota - Minneapolis, MN 55455
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


#ifndef _TABULARCONFIG_HPP_ 
#define _TABULARCONFIG_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include <string>

// for ease of extending this list and getting the usage statements right everywhere
#define TABULARCONFIG_DELIMITER_OPTIONS_STR "delimiter=<fixed|space|comma|tab>"
#define TABULARCONFIG_DELIMITER_USAGE_STR "delimiter: sets column separation; default is fixed width"


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
    /// delimiter between columns (unless set to Delimiter_FixedWidth) 

    char getDelimiterChar() const;
    std::string getFileExtension() const;
    bool checkDelimiter(const std::string& arg); // set delimiter_ and return true iff arg is a valid delimiter setting 
    void copyDelimiterConfig(const TabularConfig &rhs) {delimiter_ = rhs.delimiter_;};
    bool delim_equal(const TabularConfig &rhs) const { return delimiter_==rhs.delimiter_;};
    bool operator==(const TabularConfig &rhs) const {return delim_equal(rhs);};
 protected:
    TabularConfig();
    Delimiter delimiter_; // gets set by checkDelimiter()
};

} // namespace analysis 
} // namespace pwiz


#endif // _TABULARCONFIG_HPP_ 
