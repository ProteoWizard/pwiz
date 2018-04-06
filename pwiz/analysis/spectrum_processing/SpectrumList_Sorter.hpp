//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUMLIST_SORTER_HPP_ 
#define _SPECTRUMLIST_SORTER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "boost/logic/tribool.hpp"


namespace pwiz {
namespace analysis {


/// Provides a custom-sorted spectrum list
class PWIZ_API_DECL SpectrumList_Sorter : public msdata::SpectrumListWrapper
{
    public:

    /// client-implemented sort predicate -- called during construction of
    /// SpectrumList_Sorter to sort the underlying spectrum list
    struct PWIZ_API_DECL Predicate
    {
        /// return values:
        ///  true: lhs < rhs
        ///  false: lhs >= rhs
        ///  indeterminate: need to see the full Spectrum object to decide
        virtual boost::logic::tribool less(const msdata::SpectrumIdentity& lhs,
                                           const msdata::SpectrumIdentity& rhs) const 
        {return boost::logic::indeterminate;}

        /// return values:
        ///  true: lhs < rhs
        ///  false: lhs >= rhs
        ///  indeterminate: need a more detailed Spectrum object to decide
        virtual boost::logic::tribool less(const msdata::Spectrum& lhs,
                                           const msdata::Spectrum& rhs) const
        {return lhs.index < rhs.index;}

        virtual ~Predicate() {}
    };

    SpectrumList_Sorter(const msdata::SpectrumListPtr& inner,
                        const Predicate& predicate,
                        bool stable = false);

    /// \name SpectrumList interface
    //@{
    virtual size_t size() const;
    virtual const msdata::SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    //@}

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    SpectrumList_Sorter(SpectrumList_Sorter&);
    SpectrumList_Sorter& operator=(SpectrumList_Sorter&);
};


class PWIZ_API_DECL SpectrumList_SorterPredicate_ScanStartTime : public SpectrumList_Sorter::Predicate
{
    public:
    virtual boost::logic::tribool less(const msdata::Spectrum& lhs,
                                       const msdata::Spectrum& rhs) const;
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_SORTER_HPP_ 

