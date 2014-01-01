//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2012 Vanderbilt University - Nashville, TN 37232
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


#include "ProteinListFactory.hpp"
#include "ProteinList_Filter.hpp"
#include "ProteinList_DecoyGenerator.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"


namespace pwiz {
namespace analysis {


using namespace pwiz::proteome;
using namespace pwiz::util;


namespace {


//
// each ProteinListWrapper has a filterCreator_* function, 
// and an entry in the jump table below
//


typedef ProteinListPtr (*FilterCreator)(const ProteomeData& pd, const string& arg);


ProteinListPtr filterCreator_index(const ProteomeData& pd, const string& arg)
{
    IntegerSet indexSet;
    indexSet.parse(arg);

    return ProteinListPtr(new ProteinList_Filter(pd.proteinListPtr, ProteinList_FilterPredicate_IndexSet(indexSet)));
}


ProteinListPtr filterCreator_id(const ProteomeData& pd, const string& arg)
{
    vector<string> tokens;
    bal::split(tokens, arg, bal::is_any_of(";"));
    if (tokens.size() == 1 && bfs::exists(tokens[0]))
    {
        ifstream idListFile(tokens[0].c_str());
        string token;
        tokens.clear();
        while(idListFile >> token)
            tokens.push_back(token);
    }

    return ProteinListPtr(new ProteinList_Filter(pd.proteinListPtr, ProteinList_FilterPredicate_IdSet(tokens.begin(), tokens.end())));
}


ProteinListPtr filterCreator_decoyGenerator(const ProteomeData& pd, const string& arg)
{
    istringstream argStream(arg);
    string mode, prefix;
    argStream >> mode >> prefix;

    if (prefix.empty())
        throw user_error("[ProteinListFactory::filterCreator_decoyGenerator] no decoy prefix provided");

    ProteinList_DecoyGenerator::PredicatePtr predicate;
    if (bal::iequals(mode, "reverse"))
        predicate.reset(new ProteinList_DecoyGeneratorPredicate_Reversed(prefix));
    else if (bal::istarts_with(mode, "shuffle"))
    {
        boost::uint32_t randomSeed;
        vector<string> tokens;
        bal::split(tokens, mode, bal::is_any_of("="));
        if (tokens.size() == 2)
            try {randomSeed = lexical_cast<boost::uint32_t>(tokens[1]);} catch(bad_lexical_cast&) {randomSeed = 0;}
        predicate.reset(new ProteinList_DecoyGeneratorPredicate_Shuffled(prefix, randomSeed));
    }
    else
        throw user_error("[ProteinListFactory::filterCreator_decoyGenerator] invalid decoy mode '" + mode + "' (expected 'reverse' or 'shuffle')");

    return ProteinListPtr(new ProteinList_DecoyGenerator(pd.proteinListPtr, predicate));
}


struct JumpTableEntry
{
    const char* command;
    const char* usage;
    FilterCreator creator;
};


JumpTableEntry jumpTable_[] =
{
    {"index", "int_set", filterCreator_index},
    {"id", "filepath to a line-by-line list OR semicolon-delimited list of protein ids (unique accession strings)", filterCreator_id},
    {"decoyGenerator", "<reverse|shuffle[=random seed]> <decoy prefix>", filterCreator_decoyGenerator},
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
void ProteinListFactory::wrap(ProteomeData& pd, const string& wrapper)
{
    // split wrapper string into command + arg

    istringstream iss(wrapper);
    string command;
    iss >> command;
    string arg = wrapper.substr(command.size() + 1);

    // switch on command, instantiate the filter

    JumpTableEntry* entry = find_if(jumpTable_, jumpTableEnd_, HasCommand(command));

    if (entry == jumpTableEnd_)
    {
        cerr << "[ProteinListFactory] Ignoring wrapper: " << wrapper << endl;
        return;
    }

    ProteinListPtr filter = entry->creator(pd, arg);

    if (!filter.get())
    {
        cerr << "command: " << command << endl;
        cerr << "arg: " << arg << endl;
        throw runtime_error("[ProteinListFactory::wrap()] Error creating filter.");
    }

    // replace existing ProteinList with the new one

    pd.proteinListPtr = filter;
}


PWIZ_API_DECL
void ProteinListFactory::wrap(proteome::ProteomeData& pd, const vector<string>& wrappers)
{
    for (vector<string>::const_iterator it=wrappers.begin(); it!=wrappers.end(); ++it)
        wrap(pd, *it);
}


PWIZ_API_DECL
string ProteinListFactory::usage()
{
    ostringstream oss;

    oss << "\nFilter options:\n\n";

    for (JumpTableEntry* it=jumpTable_; it!=jumpTableEnd_; ++it)
        oss << it->command << " " << it->usage << endl;

    oss << endl;

    oss << "\'int_set\' means that a set of integers must be specified, as a list of intervals of the form [a,b] or a[-][b]\n";

    oss << endl;

    return oss.str();
}


} // namespace analysis 
} // namespace pwiz


