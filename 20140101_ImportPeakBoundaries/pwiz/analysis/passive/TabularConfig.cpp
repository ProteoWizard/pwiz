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


#define PWIZ_SOURCE

#include "TabularConfig.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {

using namespace pwiz::util;

TabularConfig::TabularConfig()
: delimiter_(TabularConfig::Delimiter_FixedWidth)
{
}

char TabularConfig::getDelimiterChar() const
{
    char delimiterChar;
    switch (delimiter_)
    {
        default:
        case TabularConfig::Delimiter_FixedWidth:
            delimiterChar = 0;
            break;
        case TabularConfig::Delimiter_Space:
            delimiterChar = ' ';
            break;
        case TabularConfig::Delimiter_Comma:
            delimiterChar = ',';
            break;
        case TabularConfig::Delimiter_Tab:
            delimiterChar = '\t';
    }
    return delimiterChar;
}

string TabularConfig::getFileExtension() const
{
    string fileExtension;
    switch (delimiter_)
    {
     	default:
        case TabularConfig::Delimiter_FixedWidth:
        case TabularConfig::Delimiter_Space:
            fileExtension = ".txt";
            break;
        case TabularConfig::Delimiter_Comma:
            fileExtension = ".csv";
            break;
        case TabularConfig::Delimiter_Tab:
            fileExtension = ".tsv";
            break;
    }

    return fileExtension;
}


bool TabularConfig::checkDelimiter(const std::string& arg)
{
    const string delimiterArgKey = "delimiter=";

    bool found = false;
    if (bal::starts_with(arg, delimiterArgKey))
    {
        string delimiterStr = arg.substr(delimiterArgKey.length());
        found = true;
        if (delimiterStr == "space")
             delimiter_ = TabularConfig::Delimiter_Space;
        else if (delimiterStr == "tab")
             delimiter_ = TabularConfig::Delimiter_Tab;
        else if (delimiterStr == "comma")
             delimiter_ = TabularConfig::Delimiter_Comma;
        else if (delimiterStr != "fixed") 
        {
             cerr << "Invalid delimiter. Must be one of {fixed, space, tab, comma}." << endl;
             found = false;
        }
	
	      
    }
    return found;
}

} // namespace analysis 
} // namespace pwiz

