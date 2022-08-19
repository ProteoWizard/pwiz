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


#ifndef _CHROMATOGRAMLIST_FILTER_HPP_
#define _CHROMATOGRAMLIST_FILTER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ChromatogramListWrapper.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "boost/logic/tribool_io.hpp"

#include <set>

namespace pwiz {
namespace analysis {


/// ChromatogramList filter, for creating Chromatogram sub-lists
class PWIZ_API_DECL ChromatogramList_Filter : public ChromatogramListWrapper
{
    public:

    /// client-implemented filter predicate -- called during construction of
    /// ChromatogramList_Filter to create the filtered list of chromatograms
    struct PWIZ_API_DECL Predicate
    {
        /// can be overridden in subclasses that know they will need a certain detail level;
        /// it must be overridden to return DetailLevel_FullData if binary data is needed
        virtual bool suggestedDetailLevel() const {return false;}

        /// return values:
        ///  true: accept the Chromatogram
        ///  false: reject the Chromatogram
        ///  indeterminate: need to see the full Chromatogram object to decide
        virtual boost::logic::tribool accept(const msdata::ChromatogramIdentity& chromatogramIdentity) const = 0;

        /// return true iff Chromatogram is accepted
        virtual boost::logic::tribool accept(const msdata::Chromatogram& chromatogram) const {return false;}

        /// return true iff done accepting chromatograms; 
        /// this allows early termination of the iteration through the original
        /// ChromatogramList, possibly using assumptions about the order of the
        /// iteration (e.g. index is increasing, nativeID interpreted as scan number is
        /// increasing, ...)
        virtual bool done() const {return false;} 

        virtual ~Predicate() {}
    };

    ChromatogramList_Filter(const msdata::ChromatogramListPtr original, const Predicate& predicate);

    /// \name ChromatogramList interface
    //@{
    virtual size_t size() const;
    virtual const msdata::ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual msdata::ChromatogramPtr chromatogram(size_t index, bool getBinaryData = false) const;
    //@}

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    ChromatogramList_Filter(ChromatogramList_Filter&);
    ChromatogramList_Filter& operator=(ChromatogramList_Filter&);
};


class PWIZ_API_DECL ChromatogramList_FilterPredicate_IndexSet : public ChromatogramList_Filter::Predicate
{
    public:
    ChromatogramList_FilterPredicate_IndexSet(const util::IntegerSet& indexSet);
    virtual boost::logic::tribool accept(const msdata::ChromatogramIdentity& chromatogramIdentity) const;
    virtual bool done() const;

    private:
    util::IntegerSet indexSet_;
    mutable bool eos_;
};


} // namespace analysis
} // namespace pwiz


#endif // _CHROMATOGRAMLIST_FILTER_HPP_

