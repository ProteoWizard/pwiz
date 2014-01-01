//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
// Modifying author: Robert Burke <Robert.Burke@cshs.org>
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


#ifndef _PEPTIDEID_HPP_
#define _PEPTIDEID_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/iterator/iterator_facade.hpp>
#include <string>
#include <vector>
#include <iterator>

namespace pwiz {
namespace peptideid {

/// This is an interface for classes that allow access to data sources
/// of identified peptides.

class PWIZ_API_DECL PeptideID
{
public:

    struct PWIZ_API_DECL Location
    {
        std::string nativeID;
        double mz;
        double retentionTimeSec;

        Location(){}
        Location(std::string nativeID, double retentionTimeSec, double mz)
            : nativeID(nativeID), mz(mz), retentionTimeSec(retentionTimeSec)
        {}
    };
    
    struct PWIZ_API_DECL Record
    {
        std::string nativeID;
        std::string sequence;
        std::string protein_descr;
        double mz;
        double retentionTimeSec;
        double normalizedScore; // in [0,1] 

        Record() : normalizedScore(0) {}
        Record(const Record& record)
            : nativeID(record.nativeID),
              sequence(record.sequence),
              protein_descr(record.protein_descr),
              mz(record.mz),
              retentionTimeSec(record.retentionTimeSec),
              normalizedScore(record.normalizedScore)
        {
        }
    };

    /**
     * Interface for 
     */
    struct IteratorInternal {
        virtual void increment() = 0;
        virtual bool equal(const boost::shared_ptr<IteratorInternal>& li) const = 0;
        virtual const PeptideID::Record& dereference() const = 0;
    };

    /**
     * Iterator for 
     */
    class Iterator : public boost::iterator_facade<Iterator,
        const PeptideID::Record,
        boost::forward_traversal_tag>
    {
    public:
        Iterator() {}
        Iterator(const Iterator& it) : pimpl(it.pimpl) {}
        Iterator(boost::shared_ptr<PeptideID::IteratorInternal> pimpl)
            : pimpl(pimpl)
        {}

    protected:
        friend class boost::iterator_core_access;
        
        void increment() { pimpl->increment(); }
        
        bool equal(const PeptideID::Iterator& li) const
        {
            return pimpl->equal(li.pimpl);
        }
        
        const PeptideID::Record& dereference() const
        {
            return pimpl->dereference();
        }
        
        boost::shared_ptr<PeptideID::IteratorInternal> pimpl;
    };
    
    virtual Record record(const Location& location) const = 0;
    
    virtual ~PeptideID() {}

    virtual Iterator begin() const = 0;
    
    virtual Iterator end() const = 0;
};

struct nativeID_less
{
    bool operator()(const PeptideID::Record& a, const PeptideID::Record& b) const
    {
        return atof(a.nativeID.c_str()) < atof(b.nativeID.c_str());
    }
};

class location_less
{
public:
    bool operator()(const PeptideID::Location& a, const PeptideID::Location& b) const
    {
        return a.nativeID.compare(b.nativeID) < 0 && a.mz < b.mz && a.retentionTimeSec < b.retentionTimeSec;
    }
};

} // namespace peptideid
} // namespace pwiz

#endif // _PEPTIDEID_HPP_

