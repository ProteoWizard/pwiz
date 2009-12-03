
#include "ProteomeData.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Exception.hpp"


namespace pwiz {
namespace proteome {


//
// Protein
//

PWIZ_API_DECL
Protein::Protein(const std::string& id, size_t index, const std::string& description, const std::string& sequence)
: Peptide(sequence), id(id), index(index), description(description)
{}


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
