//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "ChromatogramListFactory.hpp"
#include "pwiz/analysis/chromatogram_processing/ChromatogramList_Filter.hpp"
#include "pwiz/analysis/chromatogram_processing/ChromatogramList_LockmassRefiner.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {


using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;


namespace {


inline const char* cppTypeToNaturalLanguage(short T) { return "a small integer"; }
inline const char* cppTypeToNaturalLanguage(int T) { return "an integer"; }
inline const char* cppTypeToNaturalLanguage(long T) { return "an integer"; }
inline const char* cppTypeToNaturalLanguage(unsigned short T) { return "a small positive integer"; }
inline const char* cppTypeToNaturalLanguage(unsigned int T) { return "a positive integer"; }
inline const char* cppTypeToNaturalLanguage(unsigned long T) { return "a positive integer"; }
inline const char* cppTypeToNaturalLanguage(float T) { return "a real number"; }
inline const char* cppTypeToNaturalLanguage(double T) { return "a real number"; }
inline const char* cppTypeToNaturalLanguage(bool T) { return "true or false"; }


/// parses a lexical-castable key=value pair from a string of arguments which may also include non-key-value strings;
/// if the key is not in the argument string, defaultValue is returned;
/// if bad_lexical_cast is thrown, it is converted into a sensible error message
template <typename ArgT>
ArgT parseKeyValuePair(string& args, const string& tokenName, const ArgT& defaultValue)
{
    try
    {
        size_t keyIndex = args.rfind(tokenName);
        if (keyIndex != string::npos)
        {
            size_t valueIndex = keyIndex + tokenName.length();
            if (valueIndex < args.length())
            {
                string valueStr;
                try
                {
                    valueStr = args.substr(valueIndex, args.find(" ", valueIndex) - valueIndex);
                    ArgT value = lexical_cast<ArgT>(valueStr);
                    args.erase(keyIndex, args.find(" ", valueIndex) - keyIndex);
                    return value;
                }
                catch (bad_lexical_cast&)
                {
                    throw runtime_error("error parsing \"" + valueStr + "\" as value for \"" + tokenName + "\"; expected " + cppTypeToNaturalLanguage(defaultValue));
                }
            }
        }
        return defaultValue;
    }
    catch (exception& e)
    {
        throw runtime_error(string("[parseKeyValuePair] ") + e.what());
    }
}

/// parses a lexical-castable key=value pair from a string of arguments which may also include non-key-value strings;
/// if the key is not in the argument string or if bad_lexical_cast is thrown, a default-constructed ArgT is returned
template <typename ArgT>
ArgT parseKeyValuePair(string& args, const string& tokenName)
{
    return parseKeyValuePair<ArgT>(args, tokenName, ArgT());
}


//
// each ChromatogramListWrapper has a filterCreator_* function, 
// and an entry in the jump table below
//


typedef ChromatogramListPtr (*FilterCreator)(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr);
typedef const char *UsageInfo[2];  // usage like <int_set>, and details

ChromatogramListPtr filterCreator_index(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    IntegerSet indexSet;
    indexSet.parse(arg);

    return ChromatogramListPtr(new 
        ChromatogramList_Filter(msd.run.chromatogramListPtr, 
            ChromatogramList_FilterPredicate_IndexSet(indexSet)));
}
UsageInfo usage_index = {"<index_value_set>",
    "Selects chromatograms by index - an index value 0-based numerical order in which the chromatogram appears in the input.\n"
    "  <index_value_set> is an int_set of indexes."
};

ChromatogramListPtr filterCreator_lockmassRefiner(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    const string mzToken("mz=");
    const string mzNegIonsToken("mzNegIons=");
    const string toleranceToken("tol=");

    string arg = carg;
    double lockmassMz = parseKeyValuePair<double>(arg, mzToken, 0);
    double lockmassMzNegIons = parseKeyValuePair<double>(arg, mzNegIonsToken, lockmassMz); // Optional
    double lockmassTolerance = parseKeyValuePair<double>(arg, toleranceToken, 1.0);
    bal::trim(arg);
    if (!arg.empty())
        throw runtime_error("[lockmassRefiner] unhandled text remaining in argument string: \"" + arg + "\"");

    if (lockmassMz <= 0 || lockmassTolerance <= 0 || lockmassMzNegIons <= 0)
    {
        cerr << "lockmassMz and lockmassTolerance must be postive real numbers" << endl;
        return ChromatogramListPtr();
    }

    return ChromatogramListPtr(new ChromatogramList_LockmassRefiner(msd.run.chromatogramListPtr, lockmassMz, lockmassMzNegIons, lockmassTolerance));
}
UsageInfo usage_lockmassRefiner = { "mz=<real> mzNegIons=<real (mz)> tol=<real (1.0 Daltons)>", "For Waters data, adjusts m/z values according to the specified lockmass m/z and tolerance. Distinct m/z value for negative ions is optional and defaults to the given mz value. For other data, currently does nothing." };


/*ChromatogramListPtr filterCreator_polarityFilter(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    istringstream parser(arg);
    string polarityArg;

    parser >> polarityArg;

    CVID polarity = CVID_Unknown;

    if (parser)
    {
        if (polarityArg == "positive" || polarityArg == "+")
            polarity = MS_positive_scan;
        else if (polarityArg == "negative" || polarityArg == "-")
            polarity = MS_negative_scan;
    }

    if (polarity == CVID_Unknown)
        throw user_error("[ChromatogramListFactory::filterCreator_polarityFilter()] invalid polarity (expected \"positive\" or \"negative\")");

    return ChromatogramListPtr(new ChromatogramList_Filter(msd.run.chromatogramListPtr, ChromatogramList_FilterPredicate_Polarity(polarity)));
}
UsageInfo usage_polarity = { "<polarity>",
    "Keeps only chromatograms with scan of the selected <polarity>.\n"
    "   <polarity> is any one of \"positive\" \"negative\" \"+\" or \"-\"."
};*/

struct JumpTableEntry
{
    const char* command;
    const UsageInfo& usage; // {const char *usage,const char &details}
    FilterCreator creator;
};


JumpTableEntry jumpTable_[] =
{
    {"index", usage_index, filterCreator_index},
    {"lockmassRefiner", usage_lockmassRefiner, filterCreator_lockmassRefiner}//,
    //{"polarity", usage_polarity, filterCreator_polarityFilter}
};


size_t jumpTableSize_ = sizeof(jumpTable_)/sizeof(JumpTableEntry);


JumpTableEntry* jumpTableEnd_ = jumpTable_ + jumpTableSize_;


struct HasCommand
{
    HasCommand(const string& command) : command_(command) {}
    bool operator()(const JumpTableEntry& entry) {return command_ == entry.command;}
    string command_;
};


} // namespace


PWIZ_API_DECL
void ChromatogramListFactory::wrap(MSData& msd, const string& wrapper, pwiz::util::IterationListenerRegistry* ilr)
{
    if (!msd.run.chromatogramListPtr)
        return;

    // split wrapper string into command + arg

    istringstream iss(wrapper);
    string command;
    iss >> command;
    string arg = wrapper.substr(command.size());

    // switch on command, instantiate the filter

    JumpTableEntry* entry = find_if(jumpTable_, jumpTableEnd_, HasCommand(command));

    if (entry == jumpTableEnd_)
    {
        // possibly a quoted commandline copied to a config file, 
        // eg filter=\"index [3,7]\" or filter=\"precursorRecalculation\"
        string quot;
        if (bal::starts_with(command,"\""))
            quot="\"";
        else if (bal::starts_with(command,"'"))
            quot="\'";
        if (quot.size())
        {
            command = command.substr(1);
            if (arg.size())
            {
                if  (bal::ends_with(arg,quot))
                {
                    arg    = arg.substr(0,arg.size()-1);
                }
            }
            else if (bal::ends_with(command,quot)) 
            {
                command    = command.substr(0,command.size()-1);
            }
            entry = find_if(jumpTable_, jumpTableEnd_, HasCommand(command));
        }
    }
    if (entry == jumpTableEnd_)
    {
        cerr << "[ChromatogramListFactory] Ignoring wrapper: " << wrapper << endl;
        return;
    }

    ChromatogramListPtr filter = entry->creator(msd, arg, ilr);
    msd.filterApplied(); // increase the filter count

    if (!filter.get())
    {
        cerr << "command: " << command << endl;
        cerr << "arg: " << arg << endl;
        throw runtime_error("[ChromatogramListFactory::wrap()] Error creating filter.");
    }

    // replace existing ChromatogramList with the new one

    msd.run.chromatogramListPtr = filter;
}


PWIZ_API_DECL
void ChromatogramListFactory::wrap(msdata::MSData& msd, const vector<string>& wrappers, pwiz::util::IterationListenerRegistry* ilr)
{
    for (vector<string>::const_iterator it=wrappers.begin(); it!=wrappers.end(); ++it)
        wrap(msd, *it, ilr);
}


PWIZ_API_DECL
string ChromatogramListFactory::usage(bool detailedHelp, const char *morehelp_prompt, int maxLineLength)
{
    ostringstream oss;
    MSData fakemsd;

    oss << endl;

    oss << "Chromatogram List Filters\n"
           "=========================\n";
    if (!detailedHelp)
    {
        if (morehelp_prompt)
            oss << morehelp_prompt << endl;
    }
    else
    {
        oss << endl;
        oss << "Note: Filters are applied sequentially in the order that you list them, and the sequence order\n";
        oss << "can make a large difference in your output.\n\n";
        oss << "Many filters take 'int_set' arguments.  An \'int_set\' is a list of intervals of the form [a,b] or a[-][b].\n";
        oss << "For example \'[0,3]\' and \'0-3\' both mean \'the set of integers from 0 to 3 inclusive\'.\n";
        oss << "\'1-\' means \'the set of integers from 1 to the largest allowable number\'.  \n";
        oss << "\'9\' is also an integer set, equivalent to \'[9,9]\'.\n";
        oss << "\'[0,2] 5-7\' is the set \'0 1 2 5 6 7\'. \n";
    }

    for (JumpTableEntry* it=jumpTable_; it!=jumpTableEnd_; ++it)
    {
        if (detailedHelp)
            oss << it->command << " " << it->usage[0] << endl << it->usage[1] << endl << endl ;
        else
            oss << it->command << " " << it->usage[0] << endl ;
    }

    oss << endl;

    // tidy up the word wrap
    std::string str = oss.str();
    size_t lastPos = 0;
    for (size_t curPos = maxLineLength; curPos < str.length();)
    {
        std::string::size_type newlinePos = str.rfind( '\n', curPos );
        if( newlinePos == std::string::npos || (newlinePos <= lastPos))
        {   // no newline within next wrap chars, add one
            std::string::size_type spacePos = str.rfind( ' ', curPos );
            lastPos = curPos;
            if( spacePos == std::string::npos )
            {
                curPos++; // no spaces, go long
            }
            else 
            {
                str[ spacePos ] = '\n';
                curPos = spacePos + maxLineLength + 1;
            }
        } 
        else 
        {
            lastPos = curPos;
            curPos = newlinePos + maxLineLength + 1;
        }
    }

    return str;
}


} // namespace analysis 
} // namespace pwiz


