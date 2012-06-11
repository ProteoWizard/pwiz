//
// $Id: SpectrumList_PrecursorRefine.cpp 2051 2010-06-15 18:39:13Z chambm $
//
//
// Original author: Chris Paulse <cpaulse@systemsbiology.org>
//
// Copyright 2010 Institute for Systems Biology
//                Seattle, WA 98103
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

#include <iomanip>
#include <numeric>

#include "SpectrumList_PrecursorRefine.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/data/vendor_readers/Agilent/SpectrumList_Agilent.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <fstream>


namespace pwiz {
namespace analysis {


using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::data;

//
// SpectrumList_PrecursorRefine::Impl
//


struct SpectrumList_PrecursorRefine::Impl
{
    MSDataCache cache;
    CVID targetMassAnalyzerType;

    Impl(const MSData& msd);
};


SpectrumList_PrecursorRefine::Impl::Impl(const MSData& msd)
:   targetMassAnalyzerType(CVID_Unknown), cache(MSDataCache::Config(20))
{
    cache.open(msd);

    // choose highest-accuracy mass analyzer for targetMassAnalyzerType
    for (vector<InstrumentConfigurationPtr>::const_iterator it=msd.instrumentConfigurationPtrs.begin(),
         end=msd.instrumentConfigurationPtrs.end(); it!=end; ++it)
    {
        if (!it->get()) continue;
        const InstrumentConfiguration& ic = **it;

        BOOST_FOREACH(const Component& component, ic.componentList)
        {
            if (component.type == ComponentType_Analyzer)
                if (targetMassAnalyzerType!=MS_FT_ICR &&
                    targetMassAnalyzerType!=MS_orbitrap &&
                    targetMassAnalyzerType!=MS_time_of_flight)
                    targetMassAnalyzerType = component.cvParamChild(MS_mass_analyzer_type).cvid;
        }
    }

#if 1 // silently proceed with pass-through in case data can't be processed (inconsistent with other filters)
    if (targetMassAnalyzerType!=MS_FT_ICR && targetMassAnalyzerType!=MS_orbitrap && targetMassAnalyzerType!=MS_time_of_flight)
        throw runtime_error(("[SpectrumList_PrecursorRefine] Mass analyzer not supported: " +
                            cvTermInfo(targetMassAnalyzerType).name).c_str());
#endif

}


//
// SpectrumList_PrecursorRefine
//


PWIZ_API_DECL SpectrumList_PrecursorRefine::SpectrumList_PrecursorRefine(
    const MSData& msd)
:   SpectrumListWrapper(msd.run.spectrumListPtr), impl_(new Impl(msd))
{
    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    method.userParams.push_back(UserParam("precursor refinement", "msPrefix defaults"));
    
    if (!dp_->processingMethods.empty())
        method.softwarePtr = dp_->processingMethods[0].softwarePtr;

    dp_->processingMethods.push_back(method);

    numRefined = 0;
    numTotal = 0;
}

SpectrumList_PrecursorRefine::~SpectrumList_PrecursorRefine()
{
}


struct HasLowerMZ
{
    bool operator()(const MZIntensityPair& a, const MZIntensityPair& b){return a.mz < b.mz;}
};

PWIZ_API_DECL SpectrumPtr SpectrumList_PrecursorRefine::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr originalSpectrum;

    // silently return for incompatible data types
    if (impl_->targetMassAnalyzerType != MS_FT_ICR && impl_->targetMassAnalyzerType != MS_orbitrap && impl_->targetMassAnalyzerType != MS_time_of_flight)
        return inner_->spectrum(index, getBinaryData);

    // HACK: this is to circumvent problems with spectrum list wrapper nesting: peakPicking and precursorRefine
    // can't be combined in msconvert for this reason.
    if (dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_))
        originalSpectrum = dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->spectrum(index, true, util::IntegerSet(1,2));
    else
        originalSpectrum = inner_->spectrum(index, getBinaryData);  

    // return non-MS/MS as-is
    CVParam spectrumType = originalSpectrum->cvParamChild(MS_spectrum_type);
    if (spectrumType != MS_MSn_spectrum)
        return originalSpectrum;

    // return MS1 as-is
    CVParam msLevel = originalSpectrum->cvParam(MS_ms_level);
    if (msLevel.valueAs<int>() < 2)
        return originalSpectrum;

    if (originalSpectrum->precursors.size() == 0 || 
        originalSpectrum->precursors[0].selectedIons.size() == 0 ||
        originalSpectrum->precursors[0].selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>() == 0.0)
        return originalSpectrum;

    BOOST_FOREACH(SelectedIon& selectedIon, originalSpectrum->precursors[0].selectedIons)
    {
        double refined = RefineMassVal(selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>(), index);
        selectedIon.set(MS_selected_ion_m_z, refined);
    }

    SpectrumPtr  newSpectrum = SpectrumPtr(new Spectrum(*originalSpectrum));
 
    return newSpectrum;
}

PWIZ_API_DECL double SpectrumList_PrecursorRefine::RefineMassVal(double initialEstimate, size_t index) const
{
    size_t parentIndex[3];
    bool foundParent[3] = {false, false, false};
    parentIndex[0] = index;
    parentIndex[1] = index;
    parentIndex[2] = index;

    if (initialEstimate == 0) 
        return initialEstimate;

    SpectrumInfo xx1;
    SpectrumInfo xx2;
    SpectrumInfo xx3;
    pwiz::msdata::SpectrumInfo& info1 = xx1;
    pwiz::msdata::SpectrumInfo& info2 = xx2;
    pwiz::msdata::SpectrumInfo& info3 = xx3;

    while (1) 
    {
        if (foundParent[0] == false && parentIndex[0]-- == 0)
            return initialEstimate;
        if (foundParent[1] == false && parentIndex[1]++ >= inner_->size()-2)
            return initialEstimate;
        if (foundParent[2] == false && parentIndex[2]++ >= inner_->size()-2)
            return initialEstimate;

        if (foundParent[0] == false)
            info1 = impl_->cache.spectrumInfo(parentIndex[0]);

        if (foundParent[1] == false)
            info2 = impl_->cache.spectrumInfo(parentIndex[1]);

        if (foundParent[1] && foundParent[2] == false)
            info3 = impl_->cache.spectrumInfo(parentIndex[2]);

        if (foundParent[0] == false && info1.msLevel == 1)
        {
            foundParent[0] = true;
            info1 = impl_->cache.spectrumInfo(parentIndex[0], true);
        }
        if (foundParent[1] == false && info2.msLevel == 1)
        {
            info2 = impl_->cache.spectrumInfo(parentIndex[1], true);
            foundParent[1] = true;
            parentIndex[2] = parentIndex[1] + 1;
            info3 = impl_->cache.spectrumInfo(parentIndex[2]);
        }
        if (foundParent[1] && parentIndex[2] != index && info3.msLevel==1)
        {
            info3 = impl_->cache.spectrumInfo(parentIndex[2], true);
            foundParent[2] = true;
        }

        if (foundParent[0] && foundParent[1] && foundParent[2])
            break;
    }

    // return early if no data found in adjacent ms1 scans
    if (info1.data.empty() || info2.data.empty() || info3.data.empty())
        return initialEstimate;

    // find the maximum intensity in a window
    // the initial estimate of the precursor +/- a selected window width.  If these three points
    // form a maximum, they are used as input to an intensity weighted centriod
    //
    //  refined m/z = sum over three spectra j ( sum over three data points in profile i) mz_ij * I_ij^exponent / I_ij^exponent
    //

    bool isOrbitrap = (info1.massAnalyzerType == MS_orbitrap);

    double windowFactor = isOrbitrap ? 30e-6 : 90e-6;
    double mzLow = initialEstimate - windowFactor * initialEstimate;
    double mzHigh = initialEstimate + windowFactor * initialEstimate;
    double newCentroid = 0;
    double denom = 0.0;
    int intensityWeightingExponent =  isOrbitrap ? 8 : 4; // best result: exp 4 width 2 (Halo data file)
    int width = isOrbitrap ? 1 : 2;
    double intensMax = 0.;

    for (int i = 0; i < 3; i++)
    {
        pwiz::msdata::SpectrumInfo& info = (i == 0) ? info1 : (i == 1) ? info2 : info3;
        const std::vector<MZIntensityPair>& output = info.data;

        const MZIntensityPair* low = lower_bound(&output[0], &output[0]+output.size(), MZIntensityPair(mzLow, 0), HasLowerMZ());
        const MZIntensityPair* high = lower_bound(&output[0], &output[0]+output.size(), MZIntensityPair(mzHigh, 0), HasLowerMZ());

        int maxIndex = 0;
        intensMax = 0.;
        int ix = 0;

        for (const MZIntensityPair* p = low; p <= high; p++, ix++)
        {
            if (p->intensity > intensMax)
            {
                intensMax = p->intensity;
                maxIndex = ix;
            }
        }
        if (maxIndex >= width && maxIndex <= high-low-width)
        {
            for (int ii = -width; ii <= width; ii++)
            {
                newCentroid += (low + maxIndex + ii)->mz * pow((low + maxIndex + ii)->intensity, intensityWeightingExponent)/*/(double)sqrt(abs(ii)+1.0)*/;
                denom += pow((low + maxIndex + ii)->intensity, intensityWeightingExponent)/*/(double)sqrt(abs(ii)+1.0)*/;
            }
        }
    }
    if (denom > 0)
    {
        numRefined++;
        newCentroid /= denom;
    }
    else
    {
        newCentroid = initialEstimate;
    }

    numTotal++;
    return newCentroid;
}

} // namespace analysis
} // namespace pwiz

