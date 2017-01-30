//
// $Id$
//
//
// Original author: Jarrett Egertson <jegertso .@. uw.edu>
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

#include "PrecursorMaskCodec.hpp"

namespace pwiz{
namespace analysis{
    using namespace std;
    using namespace msdata;
    using namespace Eigen;

    PrecursorMaskCodec::PrecursorMaskCodec(SpectrumList_const_ptr slPtr, bool variableFill)
        : spectraPerCycle_(0),
        precursorsPerSpectrum_(0),
        overlapsPerSpectrum_(0),
        variableFill_(variableFill),
        processingMethod_()
    {
        ReadDemuxScheme(slPtr);
        processingMethod_.set(MS_data_processing);
        processingMethod_.userParams.push_back(UserParam("PRISM " + DemuxTypes::kDEMUX_NAME));
    }

    template <typename T>
    void PrecursorMaskCodec::GetMask(T&& arrayType, Spectrum_const_ptr sPtr, double weight) const
    {
        vector<size_t> indices;
        SpectrumToIndices(sPtr, indices);
        if (variableFill_)
        {
            vector<DemuxWindow> demuxWindows;
            for (auto i : indices)
            {
                demuxWindows.push_back(isolationWindows_[i].demuxWindow); // cache the demux windows
            }
            for (const auto& p : sPtr->precursors)
            {
                DemuxWindow precursorWindow(p);
                for (auto i = 0; i < indices.size(); ++i)
                {
                    if (precursorWindow.ContainsCenter(demuxWindows[i]))
                    {
                        arrayType[indices[i]] = weight * p.userParam("MultiFillTime").valueAs<double>() / 1000.0;
                        break;
                    }
                }
            }
        }
        else
        {
            for (auto index : indices)
            {
                arrayType[index] = weight;
            }
        }
    }

    VectorXd PrecursorMaskCodec::GetMask(msdata::Spectrum_const_ptr sPtr, double weight) const
    {
        VectorXd maskVector = VectorXd::Zero(GetDemuxBlockSize());
        GetMask(maskVector.data(), sPtr, weight);
        return maskVector;
    }

    void PrecursorMaskCodec::GetMask(msdata::Spectrum_const_ptr sPtr, DemuxTypes::MatrixType& m, size_t rowNum, double weight) const
    {
        m.row(rowNum).setZero();
        GetMask(m.row(rowNum), sPtr, weight);
    }

    void PrecursorMaskCodec::ReadDemuxScheme(msdata::SpectrumList_const_ptr spectrumList)
    {
        IdentifyCycle(spectrumList, isolationWindows_);
        IdentifyOverlap(isolationWindows_);
        // TODO once unique isolation regions have been identified it is possible to recognize patterns in their layout to optimize for speed
    }

    void PrecursorMaskCodec::IdentifyCycle(msdata::SpectrumList_const_ptr spectrumList, vector<IsolationWindow>& demuxWindows)
    {
        Spectrum_const_ptr spec;
        map<string, Precursor> precursorMap;
        {
            // Find the first MS2 spectrum to use as a representative spectrum
            size_t index = 0;
            for (; index < spectrumList->size(); ++index)
            {
                spec = spectrumList->spectrum(index);
                if (spec->cvParam(MS_ms_level).valueAs<int>() == 2)
                {
                    // Found the first MS2 spectrum, record any relevant qualities
                    precursorsPerSpectrum_ = spec->precursors.size();
                    break;
                }
            }

            if (precursorsPerSpectrum_ == 0)
                throw runtime_error("IdentifyCycle() No MS2 scans found for this experiment.");

            // Continue searching and identifying precursors until all unique precursors are found
            size_t mappedAlready = 0;
            for (; index < spectrumList->size() && mappedAlready <= 2 * precursorMap.size(); index++)
            {
                spec = spectrumList->spectrum(index);
                if (spec->cvParam(MS_ms_level).valueAs<int>() != 2) continue;
                if (spec->precursors.size() != precursorsPerSpectrum_)
                    throw runtime_error("IdentifyCycle() Precursor sizes are varying between individual MS2 scans. Cannot infer demultiplexing scheme.");
                for (const auto& p : spec->precursors)
                {
                    string mzString = prec_to_string(p);
                    auto it = precursorMap.find(mzString);
                    if (it == precursorMap.end())
                    {
                        // precursor window was not already present, add it
                        mappedAlready = 0;
                        precursorMap[mzString] = p;
                    }
                    else
                    {
                        // precursor window was already seen, move on
                        mappedAlready += 1;
                    }
                }
            }
        }

        //@{ Get the sorted keys from the map
        vector< string > sortedKeys;
        for (const auto& pe : precursorMap)
        {
            sortedKeys.push_back(pe.first);
        }
        sort(sortedKeys.begin(), sortedKeys.end(), stringToFloatCompare);

        demuxWindows.clear();
        demuxWindows.reserve(sortedKeys.size());
        for (const auto& keyString : sortedKeys)
        {
            demuxWindows.push_back(IsolationWindow(precursorMap[keyString]));
        }
        // precursors is now filled and sorted
        //@}

        // We can now solve for spectraPerCycle regardless of the prescence of overlap
        spectraPerCycle_ = demuxWindows.size() / precursorsPerSpectrum_;
    }

    void PrecursorMaskCodec::IdentifyOverlap(vector<IsolationWindow>& isolationWindows)
    {
        if (isolationWindows.size() <= 1)
            return;

        const MZHash minimumWindowSize = IsoWindowHasher::Hash(0.2);

        // Reduce risk of unintentional modification
        const auto& const_isolationWindows = isolationWindows;

        // Record all possible demux window boundaries
        set<DemuxBoundary> demuxBoundaries;
        for (auto it = const_isolationWindows.begin(); it != const_isolationWindows.end(); ++it)
        {
            demuxBoundaries.insert(DemuxBoundary(it->lowMz));
            demuxBoundaries.insert(DemuxBoundary(it->highMz));
        }
        const auto& const_demuxBoundaries = demuxBoundaries;

        // Merge nearby boundaries
        vector<DemuxBoundary> exactBoundaries;
        {
            auto lowMZ = const_demuxBoundaries.begin();
            auto highMZ = const_demuxBoundaries.begin();
            ++highMZ;
            for (; highMZ != const_demuxBoundaries.end(); ++highMZ, ++lowMZ)
            {
                if (highMZ->mzHash - lowMZ->mzHash > minimumWindowSize)
                    exactBoundaries.push_back(DemuxBoundary(lowMZ->mz));
                else
                {
                    // Since this is a small window it is most likely an edge we'll want to use, though not
                    // necessarily. It could be that two windows are adjacent with no common overlaping window.
                    // Though this isn't a likely use case we want to account for it. Later we'll match these
                    // potential edges to centers of real demux windows at which point these edge cases will
                    // be thrown out.
                    double averageMz = (highMZ->mz + lowMZ->mz) / 2.0;
                    exactBoundaries.push_back(DemuxBoundary(averageMz));
                    ++highMZ;
                    ++lowMZ;
                }
            }

            // Add the final boundary
            exactBoundaries.push_back(DemuxBoundary(lowMZ->mz));
        }

        // Generate a set of possible demux windows from the set of boundaries
        vector<IsolationWindow> possibleWindows;
        possibleWindows.reserve(exactBoundaries.size() - 1);
        {
            auto lowMZ = exactBoundaries.begin();
            auto highMZ = exactBoundaries.begin();
            ++highMZ;
            for (; highMZ != exactBoundaries.end(); ++highMZ, ++lowMZ)
            {
                possibleWindows.push_back(IsolationWindow(lowMZ->mz, highMZ->mz));
            }
        }

        // Identify the demux windows contained within each precursor isolation window and the number of times they are used.
        // Record which ranges are used to later track the unused
        multiset<IsolationWindow> usedWindows;
        for (auto it = const_isolationWindows.begin(); it != const_isolationWindows.end(); ++it)
        {
            for (auto subWindowIt = possibleWindows.begin(); subWindowIt != possibleWindows.end(); ++subWindowIt)
            {
                // TODO take advantage of sorting to make this O(n) instead of O(n^2) by not repeating lower and higher ranges
                if (it->demuxWindow.ContainsCenter(subWindowIt->demuxWindow))
                    usedWindows.insert(*subWindowIt);
            }
        }

        // Find the number of overlaps by getting the count for each demux window
        size_t maxCount = 0;
        // Record the windows that were used
        vector<IsolationWindow> returnIsolationWindows;
        for (auto it = usedWindows.begin(); it != usedWindows.end(); )
        {
            auto count = usedWindows.count(*it);
            maxCount = max(maxCount, count);
            returnIsolationWindows.push_back(*it);
            std::advance(it, count);
        }
        overlapsPerSpectrum_ = maxCount;

        // Find the unused windows
        /*
        set<DemuxWindow> unusedWindows;
        set_difference(possibleWindows.begin(), possibleWindows.end(), usedWindows.begin(), usedWindows.end(), inserter(unusedWindows, unusedWindows.begin()));
        */

        isolationWindows = move(returnIsolationWindows);
    }

    void PrecursorMaskCodec::SpectrumToIndices(msdata::Spectrum_const_ptr spectrumPtr, std::vector<size_t>& indices) const
    {
        if (spectrumPtr->precursors.size() != precursorsPerSpectrum_)
        {
            throw runtime_error("SpectrumToIndices() Number of precursors in this spectrum differ from the number expected for this demultiplexing scheme.");
        }

        indices.clear();
        vector<DemuxWindow> overlappingWindows;
        for (const auto& precursor : spectrumPtr->precursors)
        {
            overlappingWindows.push_back(DemuxWindow(precursor));
        }

        sort(overlappingWindows.begin(), overlappingWindows.end());

        auto searchLowerBoundIt = isolationWindows_.begin();
        for (const auto& window : overlappingWindows)
        {
            for (auto searchIt = searchLowerBoundIt; searchIt != isolationWindows_.end(); ++searchIt)
            {
                if (window.mzHigh <= searchIt->demuxWindow.mzLow)
                {
                    // the search window has passed the multiplexed window entirely. All remaining windows will also be past the multiplexed window, so we
                    // don't need to search them.
                    break;
                }
                if (window.ContainsCenter(searchIt->demuxWindow))
                {
                    indices.push_back(distance(isolationWindows_.begin(), searchIt)); // found a window, add its index to the list
                    searchLowerBoundIt = searchIt + 1; // update our lower bound before we keep searching so that we don't repeat this region
                }
            }
        }
        assert(indices.size() > 0);
        if (indices.size() != overlapsPerSpectrum_ * precursorsPerSpectrum_)
            throw runtime_error("SpectrumToIndices() Number of demultiplexing windows changed. Window boundary tolerance may be set too low.");
    }

    struct IsolationWindow PrecursorMaskCodec::GetIsolationWindow(size_t i) const
    {
        return isolationWindows_[i];
    }

    size_t PrecursorMaskCodec::GetNumDemuxWindows() const
    {
        return isolationWindows_.size();
    }

    int PrecursorMaskCodec::GetSpectraPerCycle() const
    {
        return static_cast<int>(spectraPerCycle_);
    }

    int PrecursorMaskCodec::GetPrecursorsPerSpectrum() const
    {
        return static_cast<int>(precursorsPerSpectrum_);
    }

    int PrecursorMaskCodec::GetOverlapsPerCycle() const
    {
        return static_cast<int>(overlapsPerSpectrum_);
    }

    size_t PrecursorMaskCodec::GetDemuxBlockSize() const
    {
        return spectraPerCycle_ * precursorsPerSpectrum_ * overlapsPerSpectrum_;
    }

    msdata::ProcessingMethod PrecursorMaskCodec::GetProcessingMethod() const
    {
        return processingMethod_;
    }
} // namespace analysis
} // namespace pwiz