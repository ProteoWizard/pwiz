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


#ifndef _PROTEINLIST_FILTER_HPP_
#define _PROTEINLIST_FILTER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/proteome/ProteinListWrapper.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "boost/logic/tribool_io.hpp"

#include <set>

namespace pwiz {
namespace analysis {


/// ProteinList filter, for creating Protein sub-lists
class PWIZ_API_DECL ProteinList_Filter : public proteome::ProteinListWrapper
{
    public:

    /// client-implemented filter predicate -- called during construction of
    /// ProteinList_Filter to create the filtered list of proteins
    struct PWIZ_API_DECL Predicate
    {
        /// return true iff Protein is accepted
        virtual boost::logic::tribool accept(const proteome::Protein& protein) const {return false;}

        /// return true iff done accepting proteins; 
        /// this allows early termination of the iteration through the original
        /// ProteinList, possibly using assumptions about the order of the
        /// iteration (e.g. index is increasing)
        virtual bool done() const {return false;} 

        virtual ~Predicate() {}
    };

    ProteinList_Filter(const proteome::ProteinListPtr original, const Predicate& predicate);

    /// \name ProteinList interface
    //@{
    virtual size_t size() const;
    virtual proteome::ProteinPtr protein(size_t index, bool getSequence = true) const;
    //@}

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    ProteinList_Filter(ProteinList_Filter&);
    ProteinList_Filter& operator=(ProteinList_Filter&);
};


class PWIZ_API_DECL ProteinList_FilterPredicate_IndexSet : public ProteinList_Filter::Predicate
{
    public:
    ProteinList_FilterPredicate_IndexSet(const util::IntegerSet& indexSet);
    virtual boost::logic::tribool accept(const proteome::Protein& protein) const;
    virtual bool done() const;

    private:
    util::IntegerSet indexSet_;
    mutable bool eos_;
};


class PWIZ_API_DECL ProteinList_FilterPredicate_IdSet : public ProteinList_Filter::Predicate
{
    public:
    ProteinList_FilterPredicate_IdSet(const std::set<std::string>& idSet);
    template <typename InputIterator> ProteinList_FilterPredicate_IdSet(const InputIterator& begin, const InputIterator& end) : idSet_(begin, end) {}
    virtual boost::logic::tribool accept(const proteome::Protein& protein) const;
    virtual bool done() const;

    private:
    mutable std::set<std::string> idSet_;
};


} // namespace analysis
} // namespace pwiz


#endif // _PROTEINLIST_FILTER_HPP_
