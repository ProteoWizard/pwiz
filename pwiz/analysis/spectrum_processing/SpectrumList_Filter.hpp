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


#ifndef _SPECTRUMLIST_FILTER_HPP_
#define _SPECTRUMLIST_FILTER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "boost/logic/tribool.hpp"

#include <set>

namespace pwiz {
namespace analysis {


/// SpectrumList filter, for creating Spectrum sub-lists
class PWIZ_API_DECL SpectrumList_Filter : public msdata::SpectrumListWrapper
{
    public:

    /// client-implemented filter predicate -- called during construction of
    /// SpectrumList_Filter to create the filtered list of spectra
    struct PWIZ_API_DECL Predicate
    {
        /// return values:
        ///  true: accept the Spectrum
        ///  false: reject the Spectrum
        ///  indeterminate: need to see the full Spectrum object to decide
        virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const = 0;

        /// return true iff Spectrum is accepted
        virtual bool accept(const msdata::Spectrum& spectrum) const {return false;}

        /// return true iff done accepting spectra; 
        /// this allows early termination of the iteration through the original
        /// SpectrumList, possibly using assumptions about the order of the
        /// iteration (e.g. index is increasing, nativeID interpreted as scan number is
        /// increasing, ...)
        virtual bool done() const {return false;} 

        virtual ~Predicate() {}
    };

    SpectrumList_Filter(const msdata::SpectrumListPtr original, const Predicate& predicate);

    /// \name SpectrumList interface
    //@{
    virtual size_t size() const;
    virtual const msdata::SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    //@}

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    SpectrumList_Filter(SpectrumList_Filter&);
    SpectrumList_Filter& operator=(SpectrumList_Filter&);
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_IndexSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_IndexSet(const util::IntegerSet& indexSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const;
    virtual bool done() const;

    private:
    util::IntegerSet indexSet_;
    mutable bool eos_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_ScanNumberSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_ScanNumberSet(const util::IntegerSet& scanNumberSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const;
    virtual bool done() const;

    private:
    util::IntegerSet scanNumberSet_;
    mutable bool eos_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_ScanEventSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_ScanEventSet(const util::IntegerSet& scanEventSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual bool accept(const msdata::Spectrum& spectrum) const;

    private:
    util::IntegerSet scanEventSet_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_ScanTimeRange : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_ScanTimeRange(double scanTimeLow, double scanTimeHigh);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const;
    virtual bool accept(const msdata::Spectrum& spectrum) const;
    virtual bool done() const;

    private:
    double scanTimeLow_;
    double scanTimeHigh_;
    mutable bool done_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_MSLevelSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_MSLevelSet(const util::IntegerSet& msLevelSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual bool accept(const msdata::Spectrum& spectrum) const;

    private:
    util::IntegerSet msLevelSet_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_DefaultArrayLengthSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_DefaultArrayLengthSet(const util::IntegerSet& defaultArrayLengthSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual bool accept(const msdata::Spectrum& spectrum) const;

    private:
    util::IntegerSet defaultArrayLengthSet_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_MS2ActivationType : public SpectrumList_Filter::Predicate
{
public:

    SpectrumList_FilterPredicate_MS2ActivationType(const std::set<pwiz::cv::CVID> filterItem, bool hasNoneOf_ = false);

    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual bool accept(const msdata::Spectrum& spectrum) const;

    private:
    std::set<pwiz::cv::CVID> cvFilterItems;
    bool                     hasNoneOf;

};

} // namespace analysis
} // namespace pwiz


#endif // _SPECTRUMLIST_FILTER_HPP_

