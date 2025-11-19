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
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "boost/logic/tribool.hpp"

#include <set>
#include <string>

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
        /// controls whether spectra that pass the predicate are included or excluded from the result
        enum FilterMode
        {
            FilterMode_Include,
            FilterMode_Exclude
        };

        /// can be overridden in subclasses that know they will need a certain detail level;
        /// it must be overridden to return DetailLevel_FullData if binary data is needed
        virtual msdata::DetailLevel suggestedDetailLevel() const {return msdata::DetailLevel_InstantMetadata;}

        /// return values:
        ///  true: accept the Spectrum
        ///  false: reject the Spectrum
        ///  indeterminate: need to see the full Spectrum object to decide
        virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const = 0;

        /// return true iff Spectrum is accepted
        virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const {return false;}

        /// return true iff done accepting spectra; 
        /// this allows early termination of the iteration through the original
        /// SpectrumList, possibly using assumptions about the order of the
        /// iteration (e.g. index is increasing, nativeID interpreted as scan number is
        /// increasing, ...)
        virtual bool done() const {return false;} 

        /// return a string describing how the predicate filters
        virtual std::string describe() const = 0;

        virtual ~Predicate() {}
    };

    SpectrumList_Filter(const msdata::SpectrumListPtr original, const Predicate& predicate, pwiz::util::IterationListenerRegistry* ilr = 0);

    /// \name SpectrumList interface
    //@{
    virtual size_t size() const;
    virtual const msdata::SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    virtual msdata::SpectrumPtr spectrum(size_t index, msdata::DetailLevel detailLevel) const;
    //@}

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    SpectrumList_Filter(SpectrumList_Filter&);
    SpectrumList_Filter& operator=(SpectrumList_Filter&);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const SpectrumList_Filter::Predicate::FilterMode& mode);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, SpectrumList_Filter::Predicate::FilterMode& mode);


class PWIZ_API_DECL SpectrumList_FilterPredicate_IndexSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_IndexSet(const util::IntegerSet& indexSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const;
    virtual bool done() const;
    virtual std::string describe() const { return "set of spectrum indices"; }

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
    virtual std::string describe() const { return "set of scan numbers"; }

    private:
    util::IntegerSet scanNumberSet_;
    mutable bool eos_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_IdSet : public SpectrumList_Filter::Predicate
{
public:
    SpectrumList_FilterPredicate_IdSet(const std::set<std::string>& idSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const;
    virtual bool done() const;
    virtual std::string describe() const { return "set of spectrum ids"; }

private:
    std::set<std::string> idSet_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_ScanEventSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_ScanEventSet(const util::IntegerSet& scanEventSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of scan events"; }

    private:
    util::IntegerSet scanEventSet_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_ScanTimeRange : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_ScanTimeRange(double scanTimeLow, double scanTimeHigh, bool assumeSorted = true);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const;
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual bool done() const;
    virtual std::string describe() const { return "scan time range"; }

    private:
    double scanTimeLow_;
    double scanTimeHigh_;
    mutable bool eos_;
    bool assumeSorted_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_MSLevelSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_MSLevelSet(const util::IntegerSet& msLevelSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of MS levels"; }

    private:
    util::IntegerSet msLevelSet_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_ChargeStateSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_ChargeStateSet(const util::IntegerSet& chargeStateSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of charge states"; }

    private:
    util::IntegerSet chargeStateSet_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_PrecursorMzSet : public SpectrumList_Filter::Predicate
{
    public:

    enum TargetMode
    {
        TargetMode_Selected,
        TargetMode_Isolated
    };

    SpectrumList_FilterPredicate_PrecursorMzSet(const std::set<double>& precursorMzSet, chemistry::MZTolerance tolerance, FilterMode mode, TargetMode target = TargetMode_Selected);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of precursor M/Zs"; }

    private:
    std::set<double> precursorMzSet_;
    chemistry::MZTolerance tolerance_;
    FilterMode mode_;
    TargetMode target_;

    double getPrecursorMz(const msdata::Spectrum& spectrum) const;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_IsolationWindowSet : public SpectrumList_Filter::Predicate
{
public:

    SpectrumList_FilterPredicate_IsolationWindowSet(const std::set<std::pair<double, double>>& isolationWindowSet, chemistry::MZTolerance tolerance, FilterMode mode);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const { return boost::logic::indeterminate; }
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of precursor isolation windows"; }

private:
    std::set<std::pair<double, double>> isolationWindowSet_;
    chemistry::MZTolerance tolerance_;
    FilterMode mode_;

    static std::pair<double, double> getIsolationWindow(const msdata::Spectrum& spectrum);
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_IsolationWidthSet : public SpectrumList_Filter::Predicate
{
public:
    SpectrumList_FilterPredicate_IsolationWidthSet(const std::set<double>& isolationWidthSet, chemistry::MZTolerance tolerance, FilterMode mode);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const { return boost::logic::indeterminate; }
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of precursor isolation window widths"; }

private:
    std::set<double> isolationWidthSet_;
    chemistry::MZTolerance tolerance_;
    FilterMode mode_;

    static double getIsolationWidth(const msdata::Spectrum& spectrum);
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_DefaultArrayLengthSet : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_DefaultArrayLengthSet(const util::IntegerSet& defaultArrayLengthSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "number of spectrum data points"; }

    private:
    util::IntegerSet defaultArrayLengthSet_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_ActivationType : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_ActivationType(const std::set<pwiz::cv::CVID>& filterItem, bool hasNoneOf_ = false);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of activation types"; }

    private:
    std::set<pwiz::cv::CVID> cvFilterItems;
    bool                     hasNoneOf;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_AnalyzerType : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_AnalyzerType(const std::set<pwiz::cv::CVID>& filterItem, const util::IntegerSet& msLevelSet);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of analyzer types"; }

    private:
    std::set<pwiz::cv::CVID> cvFilterItems;
    util::IntegerSet msLevelSet;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_Polarity : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_Polarity(pwiz::cv::CVID polarity);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "polarity"; }

    private:
    pwiz::cv::CVID polarity;

};


class PWIZ_API_DECL SpectrumList_FilterPredicate_MzPresent : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_MzPresent(const chemistry::MZTolerance& mzt, const std::set<double>& mzSet, ThresholdFilter tf, FilterMode mode);
    virtual msdata::DetailLevel suggestedDetailLevel() const {return msdata::DetailLevel_FullData;}
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "set of M/Zs in spectrum"; }

    private:
    chemistry::MZTolerance mzt_;
    std::set<double> mzSet_;
    ThresholdFilter tf_;
    FilterMode mode_;
};

class PWIZ_API_DECL SpectrumList_FilterPredicate_ThermoScanFilter : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_ThermoScanFilter(const std::string& matchString, bool matchExact, bool inverse);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const {return boost::logic::indeterminate;}
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "Thermo scan filter pattern"; }

    private:
    std::string matchString_;
    bool matchExact_;
    bool inverse_;
};


class PWIZ_API_DECL SpectrumList_FilterPredicate_CollisionEnergy : public SpectrumList_Filter::Predicate
{
    public:
    SpectrumList_FilterPredicate_CollisionEnergy(double collisionEnergyLow, double collisionEnergyHigh, bool acceptNonCID, bool acceptMissingCE, FilterMode mode);
    virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const { return boost::logic::indeterminate; }
    virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;
    virtual std::string describe() const { return "collision energy"; }

    private:
    double ceLow_, ceHigh_;
    bool acceptNonCID_, acceptMissingCE_;
    FilterMode mode_;
};

} // namespace analysis
} // namespace pwiz


#endif // _SPECTRUMLIST_FILTER_HPP_

