//
// $Id$
//
//
// Original author: Austin Keller <atkeller .@. uw.edu>
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

#include "DemuxHelpers.hpp"
#include <boost/algorithm/string.hpp>

namespace pwiz
{
namespace analysis
{

    bool TryGetScanIDToken(const msdata::SpectrumIdentity& spectrumIdentity, const std::string& tokenName, std::string& value)
    {
        auto const& id = spectrumIdentity.id;
        boost::char_separator<char> sep(" ");
        ScanIdTokenizer tokenizer(id, sep);
        for (ScanIdTokenizer::const_iterator token = tokenizer.begin(); token != tokenizer.end(); ++token)
        {
            std::vector<std::string> attrs;
            boost::split(attrs, *token, boost::is_any_of("="));
            if (attrs.size() != 2)
            {
                continue;
            }
            if (attrs[0] == tokenName)
            {
                value = attrs[1];
                return true;
            }
        }
        return false;
    }

    bool TryGetDemuxIndex(const msdata::SpectrumIdentity& spectrumIdentity, size_t& index)
    {
        std::string indexValue;
        if (TryGetScanIDToken(spectrumIdentity, "demux", indexValue))
        {
            index = std::stoi(indexValue);
            return true;
        }
        return false;
    }

    bool TryGetScanIndex(const msdata::SpectrumIdentity& spectrumIdentity, size_t& index)
    {
        std::string indexValue;
        if (TryGetScanIDToken(spectrumIdentity, "scan", indexValue))
        {
            index = std::stoi(indexValue);
            return true;
        }
        return false;
    }

    bool TryGetOriginalIndex(const msdata::SpectrumIdentity& spectrumIdentity, size_t& index)
    {
        std::string indexValue;
        if (TryGetScanIDToken(spectrumIdentity, "originalScan", indexValue))
        {
            index = std::stoi(indexValue);
            return true;
        }
        return false;
    }

    bool TryGetMSLevel(const msdata::Spectrum& spectrum, int& msLevel)
    {
        data::CVParam param = spectrum.cvParamChild(cv::MS_spectrum_type);
        if (param.cvid == cv::CVID_Unknown) return false;
        // treat non MS-spectra as MS1 spectra, which just get written out to the file with no modification
        if (!cvIsA(param.cvid, cv::MS_mass_spectrum)) return 1;
        param = spectrum.cvParam(cv::MS_ms_level);
        if (param.cvid == cv::CVID_Unknown) return false;
        msLevel = param.valueAs<int>();
        return true;
    }

    bool TryGetNumPrecursors(const msdata::Spectrum& spectrum, int& numPrecursors)
    {
        int msLevel;
        if (!TryGetMSLevel(spectrum, msLevel)) return false;
        numPrecursors = 0;
        if (msLevel == 2)
        {
            numPrecursors = static_cast<int>(spectrum.precursors.size());
            if (numPrecursors == 0) return false;
        }
        return true;
    }

    bool TryGetStartTime(const msdata::Spectrum& spectrum, double& startTime)
    {
        if (spectrum.scanList.scans.size() == 0) return false;
        const auto& scan = spectrum.scanList.scans.at(0);
        data::CVParam param = scan.cvParam(cv::MS_scan_start_time);
        if (param.cvid == cv::CVID_Unknown) return false;
        startTime = param.valueAs<double>();
        return true;
    }

    bool FindNearbySpectra(std::vector<size_t>& spectraIndices, boost::shared_ptr<const msdata::SpectrumList> slPtr, size_t centerIndex, size_t numSpectraToFind, size_t stride)
    {
        if (centerIndex >= slPtr->size())
            throw std::out_of_range("Spectrum index not in range of the given spectrum list");
        boost::shared_ptr<const msdata::Spectrum> spec = slPtr->spectrum(centerIndex, true);
        if (!spec)
            throw std::runtime_error("[DemuxHelpers::FindNearbySpectra] Failed to get spectrum from spectrumlists");
        if (spec->cvParam(cv::MS_ms_level).valueAs<int>() != 2)
            throw std::runtime_error("Center index must be an MS2 spectrum");
        spectraIndices.clear();
        spectraIndices.push_back(centerIndex);
        size_t backwardsNeeded = size_t(round((numSpectraToFind - 1) / 2.0));
        size_t afterNeeded = numSpectraToFind - 1 - backwardsNeeded;
        size_t indexLoc = centerIndex;

        size_t stepCount = 0;
        while (backwardsNeeded > 0 && indexLoc != 0 /* hit the beginning of the file */)
        {
            --indexLoc;
            // note -- the cache handles calls that request binary+meta data, so the binary data is requested
            // even though it is not needed.  Otherwise, the cache would not be used to pull the spectrum
            spec = slPtr->spectrum(indexLoc, true);
            if (!spec)
                throw std::runtime_error("[DemuxHelpers::FindNearbySpectra] Failed to get spectrum from spectrumlists");
            if (spec->cvParam(cv::MS_ms_level).valueAs<int>() == 2)
            {
                ++stepCount;
                if (stepCount == stride)
                {
                    // We've completed enough steps, record the spectrum and reset the count of steps
                    spectraIndices.push_back(indexLoc);
                    --backwardsNeeded;
                    stepCount = 0;
                }
            }
        }

        // If there were not enough spectra earlier in the file (hit the beginning of the file),
        // pull extra spectra from the end
        afterNeeded += backwardsNeeded;
        indexLoc = centerIndex + 1;
        stepCount = 0;
        while (indexLoc < slPtr->size() && afterNeeded > 0)
        {
            spec = slPtr->spectrum(indexLoc, true);
            if (!spec)
                throw std::runtime_error("[DemuxHelpers::FindNearbySpectra] Failed to get spectrum from spectrumlists");
            if (spec->cvParam(cv::MS_ms_level).valueAs<int>() == 2)
            {
                ++stepCount;
                if (stepCount == stride)
                {
                    spectraIndices.push_back(indexLoc);
                    --afterNeeded;
                    stepCount = 0;
                }
            }
            ++indexLoc;
        }

        if (afterNeeded > 0)
        {
            indexLoc = *min_element(spectraIndices.begin(), spectraIndices.end());
        }
        while (afterNeeded > 0 && indexLoc != 0 /* hit the beginning of the file */)
        {
            --indexLoc;
            spec = slPtr->spectrum(indexLoc, true);
            if (!spec)
                throw std::runtime_error("[DemuxHelpers::FindNearbySpectra] Failed to get spectrum from spectrumlists");
            if (spec->cvParam(cv::MS_ms_level).valueAs<int>() == 2)
            {
                ++stepCount;
                if (stepCount == stride)
                {
                    spectraIndices.push_back(indexLoc);
                    --afterNeeded;
                    stepCount = 0;
                }
            }
        }

        if (spectraIndices.size() != numSpectraToFind)
            return false;

        sort(spectraIndices.begin(), spectraIndices.end());
        return true;
    }
}
}
