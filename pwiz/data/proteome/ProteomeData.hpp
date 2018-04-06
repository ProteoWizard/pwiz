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


#ifndef _PROTEOMEDATA_HPP_
#define _PROTEOMEDATA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "Peptide.hpp"
#include <boost/shared_ptr.hpp>


namespace pwiz {
namespace proteome {


struct PWIZ_API_DECL Protein : public Peptide
{
    Protein(const std::string& id, size_t index, const std::string& description, const std::string& sequence);

    bool empty() const;

    std::string id;
    size_t index;
    std::string description;
};


typedef boost::shared_ptr<Protein> ProteinPtr;


// note: derived container to support dynamic linking on Windows
class IndexList : public std::vector<size_t> {};


class PWIZ_API_DECL ProteinList
{
    public:

    virtual size_t size() const = 0;

    virtual ProteinPtr protein(size_t index, bool getSequence = true) const = 0;

    virtual bool empty() const;

    virtual size_t find(const std::string& id) const;

    virtual IndexList findKeyword(const std::string& keyword, bool caseSensitive = true) const;

    virtual ~ProteinList() {}
};


typedef boost::shared_ptr<ProteinList> ProteinListPtr;


struct PWIZ_API_DECL ProteinListSimple : public ProteinList
{
    std::vector<ProteinPtr> proteins;

    // ProteinList implementation

    virtual size_t size() const {return proteins.size();}
    virtual bool empty() const {return proteins.empty();}
    virtual ProteinPtr protein(size_t index, bool getSequence = true) const;
};


struct PWIZ_API_DECL ProteomeData
{
    std::string id;

    ProteinListPtr proteinListPtr;

    ProteomeData() {}
    virtual ~ProteomeData() {}
    bool empty() const;

    private:
    // no copying
    ProteomeData(const ProteomeData&);
    ProteomeData& operator=(const ProteomeData&);
};


} // namespace proteome
} // namespace pwiz


#endif // _PROTEOMEDATA_HPP_
