//
// $Id$
//
//
// Original author: William French <william.french .@. vanderbilt.edu>
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

#define PWIZ_SOURCE

#include "SpectrumList_ScanSummer.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/sort_together.hpp"
#include "pwiz/data/vendor_readers/Waters/SpectrumList_Waters.hpp"
#include <boost/range/adaptor/map.hpp>


namespace
{
    bool sortFunc(parentIon i, parentIon j) { return (i.mz < j.mz); } // comparator for sorting of parentIon by m/z 
    bool sortRtime(precursorGroupPtr i, precursorGroupPtr j) { return (i->scanTimes.front() < j->scanTimes.front()); } // comparator for sorting of precursor List by rtime 
    bool pIonCompare(parentIon i, double mz) { return (i.mz < mz); } // comparator for searching parentIon by m/z

    double median(vector<double>& input)
    {
        if (input.size() == 0)
            return 0;

        sort(input.begin(), input.end());
        size_t midpoint = input.size() / 2;
        if (input.size() % 2 == 0) // even number
        {
            return (input[midpoint] + input[midpoint - 1]) / 2;
        }
        else
        {
            return input[midpoint];
        }
    }
}


namespace pwiz {
namespace analysis {

using namespace util;
using namespace msdata;

void SpectrumList_ScanSummer::pushSpectrum(const SpectrumIdentity& spectrumIdentity)
{
    indexMap.push_back(spectrumIdentity.index);
    spectrumIdentities.push_back(spectrumIdentity);
    spectrumIdentities.back().index = spectrumIdentities.size()-1;
}

double SpectrumList_ScanSummer::getPrecursorMz(const msdata::Spectrum& spectrum) const
{
    for (size_t i=0; i<spectrum.precursors.size(); i++)
    {
        for (size_t j=0; j<spectrum.precursors[i].selectedIons.size(); j++)
        {
            CVParam param = spectrum.precursors[i].selectedIons[j].cvParam(MS_selected_ion_m_z);
            if (param.cvid != CVID_Unknown)
                return lexical_cast<double>(param.value);
        }




    }
    return 0;
}

//void SpectrumList_ScanSummer::sumSubScansResample( vector<double> & x, vector<double> & y, size_t refIndex, DetailLevel detailLevel ) const
//{
//
//    if (x.size() != y.size())
//        throw runtime_error("[SpectrumList_ScanSummer::sumSubScans()] x and y arrays must be the same size");
//
//    int nPointsPerDalton = 800;
//
//    // get the index for the precursorGroupList
//    SpectrumPtr s = inner_->spectrum(refIndex, detailLevel ); // get binary data
//    double precursorMZ = getPrecursorMz(*s); // get the precursor value
//
//    vector<precursorGroup>::const_iterator pGroupIt = lower_bound(precursorList.begin(),precursorList.end(),precursorMZ,pGroupCompare); // returns iterator to first element in precursorList where mz >= precursorMZ
//    while ( pGroupIt->indexList[0] != (int)refIndex )
//    {
//        pGroupIt++; // lower_bound returns the first element in a repeat sequence
//        if ( pGroupIt == precursorList.end() )
//            throw runtime_error("[SpectrumList_ScanSummer::sumSubScans()] Cannot find the correct precursorList element...");
//    }
//
//    // setup the intensity bins for summing over multiple sub-scans
//    double MZspacing = 1.0 / double(nPointsPerDalton);
//    vector<double> summedIntensity(int(TotalDaltons/MZspacing)+2,0.0);
//
//    for( vector<int>::const_iterator listIt = pGroupIt->indexList.begin(); listIt != pGroupIt->indexList.end(); ++listIt)
//    {
//
//        SpectrumPtr s2 = inner_->spectrum( *listIt, detailLevel );
//        vector<double>& subMZ = s2->getMZArray()->data;
//        if (subMZ.size() < 2) continue;
//        vector<double>& subIntensity = s2->getIntensityArray()->data;
//
//        int binA, binB = (subMZ[0] - lowerMZlimit) / MZspacing; // initialize 
//        for( size_t j=0, jend=subMZ.size()-1; j < jend ; ++j)
//        {
//
//            binA = binB+1; // get bucket to the right of the first data point
//            binB = (subMZ[j+1] - lowerMZlimit) / MZspacing; // get the bucket to the left of the second data point
//
//            if ( subIntensity[j] == 0 && subIntensity[j+1] == 0 ) continue; // no interpolation needed
//
//            if ( binB < binA )
//            {
//                this->warn_once("[SpectrumList_ScanSummer]: Warning, grid spacing is coarser than raw m/z spacing in at least one case" );
//            }
//
//            int k = binA;
//            while ( k <= binB ) // while loop ensures no interpolation when binB < binA
//            {
//                // get the m/z position of the current bin
//                double mzBin = double(k) * MZspacing + lowerMZlimit;
//
//                // linear interpolation
//                summedIntensity[k] += subIntensity[j] + ( subIntensity[j+1] - subIntensity[j] ) * ( ( mzBin - subMZ[j] ) / ( subMZ[j+1] - subMZ[j] ) ); 
//
//                ++k;
//            }
//
//        }
//
//    }
//
//    x.resize(summedIntensity.size());
//    y.resize(summedIntensity.size());
//    int cnt=0;
//    for (size_t i=1, iend=summedIntensity.size()-1; i < iend ; ++i)
//    {
//        // don't print zero-intensity points flanked by other zero-intensity points
//        if ( summedIntensity[i-1] != 0.0 || summedIntensity[i] != 0.0 || summedIntensity[i+1] != 0.0 )
//        {
//            x[cnt] = lowerMZlimit + double(i) * MZspacing;
//            y[cnt] = summedIntensity[i];
//            cnt++;
//        }
//    }
//    x.resize(cnt);
//    y.resize(cnt);
//
//}


//
// SpectrumList_ScanSummer
//

void SpectrumList_ScanSummer::sumSubScansNaive( BinaryData<double> & x, BinaryData<double> & y, const precursorGroupPtr& precursorGroupPtr, DetailLevel detailLevel ) const
{
    if (x.size() != y.size())
        throw runtime_error("[SpectrumList_ScanSummer::sumSubScansNaive()] x and y arrays must be the same size");

    BinaryData<double>& summedMZ = x;
    BinaryData<double>& summedIntensity = y;

    sort_together(summedMZ, vector<boost::iterator_range<BinaryData<double>::iterator>> { summedIntensity });

    vector<double> binnedMZ; binnedMZ.reserve(summedMZ.size());
    vector<double> binnedIntensity; binnedIntensity.reserve(summedMZ.size());

    if (summedMZ.size() > 1)
    {
        binnedMZ.push_back(summedMZ[0]);
        binnedIntensity.push_back(summedIntensity[0]);
        for (size_t i = 1; i < summedMZ.size(); ++i)
        {
            if (fabs(binnedMZ.back() - summedMZ[i]) < 1e-6)
            {
                for (; i < summedMZ.size() && fabs(binnedMZ.back() - summedMZ[i]) < 1e-6; ++i)
                    binnedIntensity.back() += summedIntensity[i];
                --i;
            }
            else
            {
                binnedMZ.push_back(summedMZ[i]);
                binnedIntensity.push_back(summedIntensity[i]);
            }
        }

        swap(summedMZ, binnedMZ);
        swap(summedIntensity, binnedIntensity);
    }

    if (!precursorGroupPtr)
        return;

    vector<int>::const_iterator InitialIt = precursorGroupPtr->indexList.begin()+1;
    for( vector<int>::const_iterator listIt = InitialIt; listIt != precursorGroupPtr->indexList.end(); ++listIt)
    {
        SpectrumPtr s2 = inner_->spectrum( *listIt, detailLevel );
        BinaryData<double>& subMz = s2->getMZArray()->data;
        BinaryData<double>& subIntensity = s2->getIntensityArray()->data;

        for( size_t j=0, jend=subMz.size(); j < jend ; ++j)
        {
            // check if this m/z point was recorded from a previous sub-scan
            BinaryData<double>::iterator pIonIt;
            pIonIt = lower_bound(summedMZ.begin(), summedMZ.end(), subMz[j] - 1e-2); // first element that is greater than or equal to subMz[j]
            int indexMZ = pIonIt - summedMZ.begin();
            if (pIonIt == summedMZ.end()) // first check if mzs[j] is outside search range
            {
                summedMZ.push_back(subMz[j]);
                summedIntensity.push_back(subIntensity[j]);
            }
            else if (fabs(*pIonIt - subMz[j]) > 1e-2) // if the closest value is not equal to mzs[i], start a new m/z point
            {
                summedMZ.insert(pIonIt,subMz[j]);
                summedIntensity.insert(summedIntensity.begin()+indexMZ,subIntensity[j]);
            }
            else // m/z value recorded from previous sub-scan for this precursor; calculate the sum
            {
                summedIntensity[indexMZ] += subIntensity[j];
            }
        }
    }
}





PWIZ_API_DECL SpectrumList_ScanSummer::SpectrumList_ScanSummer(const SpectrumListPtr& original, double precursorTol, double rTimeTol, double mobilityTol, bool sumMs1, IterationListenerRegistry* ilr)
:   SpectrumListWrapper(original), precursorTol_(precursorTol), rTimeTol_(rTimeTol), mobilityTol_(mobilityTol), sumMs1_(sumMs1)
{
    if (!inner_.get()) throw runtime_error("[SpectrumList_ScanSummer] Null pointer");

    try
    {
        for (size_t i = 0, end = inner_->size(); i < end; ++i)
        {
            if (ilr) ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(i, inner_->size(), "grouping spectra with similar precursor m/z, scan time, and ion mobility"));
            const SpectrumIdentity& spectrumIdentity = inner_->spectrumIdentity(i);
            SpectrumPtr s = inner_->spectrum(i, false);
            double precursorMZ = getPrecursorMz(*s);

            if (precursorMZ == 0.0) // ms1 scans do not need grouping
            {
                pushSpectrum(spectrumIdentity);
                precursorMap.push_back(precursorGroupPtr());
                continue;
            }
            double rTime = s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds();
            double ionMobility = s->scanList.scans[0].cvParamValueOrDefault(MS_inverse_reduced_ion_mobility, 0.0);

            if (precursorList.empty()) // set some parameters
            {
                if (s->scanList.scans[0].scanWindows.empty())
                {
                    lowerMZlimit = 0;
                    upperMZlimit = 3000;
                }
                else
                {
                    lowerMZlimit = s->scanList.scans[0].scanWindows[0].cvParamValueOrDefault(MS_scan_window_lower_limit, 0.0);
                    upperMZlimit = s->scanList.scans[0].scanWindows[0].cvParamValueOrDefault(MS_scan_window_upper_limit, 3000.0);
                }
                TotalDaltons = upperMZlimit - lowerMZlimit;
            }

            auto pGroupIt = precursorList.lower_bound(precursorMZ - precursorTol_); // returns iterator to first element in precursorList where mz >= precursorMZ-tolerance
            auto pGroupEnd = precursorList.lower_bound(precursorMZ + precursorTol_);
            bool foundGroup = false;

            for (; pGroupIt != pGroupEnd && pGroupIt != precursorList.end(); ++pGroupIt)
            {
                double timeDiff = abs(rTime - pGroupIt->second->scanTimes.front());
                double imDiff = abs(ionMobility - pGroupIt->second->ionMobilities.front());

                if ((rTimeTol_ == 0 || timeDiff < rTimeTol_) && (mobilityTol_ == 0 || imDiff < mobilityTol_))
                {
                    pGroupIt->second->precursorMZs.push_back(precursorMZ);
                    pGroupIt->second->scanTimes.push_back(rTime);
                    pGroupIt->second->ionMobilities.push_back(ionMobility);
                    pGroupIt->second->indexList.push_back(i);
                    foundGroup = true;
                    break;
                }
            }

            if (!foundGroup)
            {
                pushSpectrum(spectrumIdentity);
                precursorGroupPtr newGroup(new precursorGroup);
                newGroup->precursorMZs.push_back(precursorMZ);
                newGroup->scanTimes.push_back(rTime);
                newGroup->ionMobilities.push_back(ionMobility);
                newGroup->indexList.push_back(i);
                precursorMap.push_back(newGroup);
                precursorList[precursorMZ] = newGroup;
            }

        } // end for loop over all spectra

        auto precursorGroups = precursorList | boost::adaptors::map_values;
        ms2RetentionTimes.insert(ms2RetentionTimes.end(), precursorGroups.begin(), precursorGroups.end());
        sort(ms2RetentionTimes.begin(), ms2RetentionTimes.end(), sortRtime);

        // add processing methods to the copy of the inner SpectrumList's data processing
        ProcessingMethod method;
        method.order = dp_->processingMethods.size();
        method.userParams.emplace_back("summing of spectra from the same precursor adjacent in time and/or mobility space");
        method.userParams.emplace_back("m/z tolerance", toString(precursorTol_), "xsd:double");
        method.userParams.emplace_back("scan time tolerance", toString(rTimeTol_), "xsd:double");
        method.userParams.emplace_back("ion mobility tolerance", toString(mobilityTol_), "xsd:double");
        method.userParams.emplace_back("sumMS1", sumMs1_ ? "true" : "false", "xsd:boolean");

        if (!dp_->processingMethods.empty())
            method.softwarePtr = dp_->processingMethods[0].softwarePtr;
        dp_->processingMethods.push_back(method);
    }
    catch (exception& e)
    {
        throw runtime_error(std::string("[SpectrumList_ScanSummer::ctor()] Error grouping spectra: ") + e.what());
    }
    catch (...)
    {
        throw runtime_error("[SpectrumList_ScanSummer::ctor()] Caught unknown exception grouping spectra");
    }
}

PWIZ_API_DECL size_t SpectrumList_ScanSummer::size() const
{
    return indexMap.size();
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_ScanSummer::spectrumIdentity(size_t index) const
{
    return spectrumIdentities.at(index);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_ScanSummer::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_ScanSummer::spectrum(size_t index, DetailLevel detailLevel) const
{

    size_t summedScanIndex = indexMap.at(index);
    SpectrumPtr summedSpectrum = inner_->spectrum(summedScanIndex, true);

    int msLevel = summedSpectrum->cvParam(MS_ms_level).valueAs<int>();
    if (msLevel > 1 || sumMs1_)
    {
        try
        {
            precursorGroupPtr precursorGroupPtr;
            if (msLevel > 1)
            {
                precursorGroupPtr = precursorMap.at(index);
                if (!precursorGroupPtr)
                    throw runtime_error("ms2 index points to null precursorGroupPtr");

                // output ms2 spectra by retention time, grab the appropriate spectrum
                int newIndex = precursorGroupPtr->indexList[0];
                summedSpectrum = inner_->spectrum(newIndex, true);

                for (auto& cvParam : summedSpectrum->scanList.scans[0].cvParams)
                    if (cvParam.cvid == MS_scan_start_time)
                        cvParam.value = lexical_cast<string>(median(precursorGroupPtr->scanTimes));
                    else if (cvParam.cvid == MS_inverse_reduced_ion_mobility)
                        cvParam.value = lexical_cast<string>(median(precursorGroupPtr->ionMobilities));
                for (auto& cvParam : summedSpectrum->precursors[0].selectedIons[0].cvParams)
                    if (cvParam.cvid == MS_selected_ion_m_z)
                        cvParam.value = lexical_cast<string>(median(precursorGroupPtr->precursorMZs));

                // keep only first scan
                summedSpectrum->scanList.scans.erase(summedSpectrum->scanList.scans.begin() + 1, summedSpectrum->scanList.scans.end());
            }

            BinaryData<double>& mzs = summedSpectrum->getMZArray()->data;
            BinaryData<double>& intensities = summedSpectrum->getIntensityArray()->data;

            // remove extra arrays that are the same length as the m/z array because the summing will not preserve the one-to-one correspondence
            for (size_t i = 2; i < summedSpectrum->binaryDataArrayPtrs.size(); ++i)
                if (summedSpectrum->binaryDataArrayPtrs[i]->data.size() == mzs.size())
                    summedSpectrum->binaryDataArrayPtrs.erase(summedSpectrum->binaryDataArrayPtrs.begin() + (i--));

            sumSubScansNaive(mzs, intensities, precursorGroupPtr, DetailLevel_FullData);
            summedSpectrum->defaultArrayLength = mzs.size();

        }
        catch( exception& e )
        {
            throw runtime_error(std::string("[SpectrumList_ScanSummer::spectrum()] Error summing precursor sub-scans: ") + e.what());
        }
        catch (...)
        {
            throw runtime_error("[SpectrumList_ScanSummer::spectrum()] Caught unknown exception summing spectra");
        }    
    }

    summedSpectrum->index = index; // redefine the index
    return summedSpectrum;

}


} // namespace analysis
} // namespace pwiz
