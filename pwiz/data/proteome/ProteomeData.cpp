//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "ProteomeData.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace proteome {


//
// Protein
//

PWIZ_API_DECL
Protein::Protein(const std::string& id, size_t index, const std::string& description, const std::string& sequence)
: Peptide(sequence), id(id), index(index), description(description)
{}


PWIZ_API_DECL
bool Protein::empty() const
{
    return id.empty() && description.empty() && sequence().empty();
}


//
// ProteinList (default implementations)
//

PWIZ_API_DECL bool ProteinList::empty() const
{
    return size() == 0;
}


PWIZ_API_DECL size_t ProteinList::find(const string& id) const
{
    for (size_t index = 0, end = size(); index < end; ++index)
        if (protein(index, false)->id == id)
            return index;
    return size();
}


PWIZ_API_DECL IndexList ProteinList::findKeyword(const string& keyword, bool caseSensitive /*= true*/) const
{
    IndexList indexList;
    if (caseSensitive)
    {
        for (size_t index = 0, end = size(); index < end; ++index)
            if (protein(index, false)->description.find(keyword) != string::npos)
                indexList.push_back(index);
    }
    else
    {
        string lcKeyword = keyword;
        for (size_t index = 0, end = size(); index < end; ++index)
        {
            string lcDescription = protein(index, false)->description;
            bal::to_lower(lcDescription);
            if (lcDescription.find(lcKeyword) != string::npos)
                indexList.push_back(index);
        }
    }
    return indexList;
}


//
// ProteinListSimple
//

PWIZ_API_DECL ProteinPtr ProteinListSimple::protein(size_t index, bool getSequence) const
{
    // validate index
    if (index >= size())
        throw runtime_error("[ProteinListSimple::protein()] Invalid index.");

    // validate Protein* 
    if (!proteins[index].get())
        throw runtime_error("[ProteinListSimple::protein()] Null ProteinPtr.");

    return proteins[index];
} 


//
// ProteomeData
//

PWIZ_API_DECL bool ProteomeData::empty() const
{
    return proteinListPtr->empty();
}


} // namespace proteome
} // namespace pwiz
