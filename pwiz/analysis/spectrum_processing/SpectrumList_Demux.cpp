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

#define PWIZ_SOURCE

#include <pwiz/utility/misc/Std.hpp>
#include "SpectrumList_Demux.hpp"
#include "pwiz/analysis/demux/DemuxDataProcessingStrings.hpp"
#include "pwiz/analysis/demux/PrecursorMaskCodec.hpp"
#include "pwiz/analysis/demux/OverlapDemultiplexer.hpp"
#include "pwiz/analysis/demux/MSXDemultiplexer.hpp"
#include "pwiz/analysis/demux/SpectrumPeakExtractor.hpp"
#include "pwiz/analysis/demux/DemuxSolver.hpp"
#include "pwiz/analysis/demux/IPrecursorMaskCodec.hpp"
#include "pwiz/analysis/demux/IDemultiplexer.hpp"
#ifdef _USE_DEMUX_DEBUG_WRITER
#include "pwiz/analysis/demux/DemuxDebugWriter.hpp"
#include "pwiz/analysis/demux/DemuxDebugReader.hpp"
#endif
#include "pwiz/analysis/demux/DemuxHelpers.hpp"
#include "pwiz/analysis/demux/DemuxTypes.hpp"
#include "pwiz/data/msdata/SpectrumListCache.hpp"
#include <boost/make_shared.hpp>


namespace pwiz {
namespace analysis {

    using namespace msdata;
    using namespace util;

    class SpectrumList_Demux::Impl
    {
        public:
        Impl(const msdata::SpectrumListPtr& inner, const Params& p, DataProcessingPtr dp);
        msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
        msdata::SpectrumPtr spectrum(size_t index, msdata::DetailLevel detailLevel) const;
        size_t size() const;
        const msdata::SpectrumIdentity& spectrumIdentity(size_t index) const;
        
        private:

        /// Simple caching object for storing the last solved demultiplexing spectrum
        struct PreviousDemuxSolution
        {
            size_t origSpecIndex; ///< index of original spectrum
            MatrixPtr solution; ///< solution matrix output of DemuxSolver
        };

        /// Container that maps from the indices of the demultiplexed output spectra to their source multiplexed input spectra. This provides
        /// the number of output demultiplexed spectra so that they can be iterated through. This also provides the multiplexed source spectra
        /// for each demultiplexed spectra and an index to the specific demultiplexed spectrum within the source spectrum.
        struct IndexMapper
        {
            /// Shared pointer definition
            typedef boost::shared_ptr<IndexMapper> ptr;

            /// Constant shared pointer definition
            typedef boost::shared_ptr<const IndexMapper> const_ptr;

            /// Structure stored by the IndexMapper that is used to uniquely query (or construct) a demultiplexed spectrum
            typedef struct
            {
                int msLevel; ///< Number of rounds of sequential MS performed

                size_t spectrumOriginalIndex; ///< Index in SpectrumList of the mux'd spectrum to demux

                size_t precursorIndex; ///< Index of the precursor isolation window used to assign a demultiplex window to the multiplexed precursor
                ///< isolation window that it was derived from.

                size_t demuxIndex; ///< Arbitrarily assigned index of the demultiplexed window used to uniquely map from the original set of
                ///< multiplexed spectrum indices to the larger set of indices created by demultiplexing. This could just as well be an unordered
                ///<  hash since the precursors are isolated at effectively the same time.
            } DemuxRequestIndex;

            /// SpectrumList of mux spectra to map to
            boost::shared_ptr<const msdata::SpectrumList> original;

            /// List of SpectrumIdentities to be assigned ono-to-one to each demux spectrum by their index
            std::vector<msdata::SpectrumIdentity> spectrumIdentities;

            /// Maps from the index of a demux spectrum to the index of the mux spectrum it was derived from and the precursor within that spectrum
            /// that it was derived from
            std::vector<DemuxRequestIndex> indexMap;

            /// The detail level needed for a non-indeterminate result
            msdata::DetailLevel detailLevel;

            /// Constructs an empty IndexMapper that can be filled with spectra from the given SpectrumListPtr
            explicit IndexMapper(msdata::SpectrumListPtr original, const IPrecursorMaskCodec& pmc);

            /// Adds a spectrum to the index map
            /// @param spectrumIdentity SpectrumIdentity to be added as corresponding to the demux'd spectrum (or mux'd spectrum if not MS2)
            /// @param msLevel Number of rounds of tandem MS
            /// @param numPrecursors Number of precursor isolation windows in the spectrum
            /// @param numOverlap Number of overlap windows per precursor
            void pushSpectrum(const msdata::SpectrumIdentity& spectrumIdentity, int msLevel, int numPrecursors, int numOverlap);

            /// Parses a list of key-value pairs coded in a string id to replace the old mux index with the new demux index.
            /// This modifes the "scan" key to use the new demux scan index while also reassigning the original scan index to the key "originalScan".
            /// @param[in] id Original scan id to be modified
            /// @param[in] scanNumber The index of the original scan
            /// @param[in] demuxIndex The index of the demux spectrum to be added
            /// @return The modified scan id
            static std::string injectScanId(std::string id, size_t scanNumber, size_t demuxIndex);
        };

        /// Retrieves demultiplexed spectrum from cache or, if it isn't cached, this delegates the demultiplexing of the spectrum.
        /// @param[in] index Index of the requested demux spectrum (not the same index as the original multiplexed spectrum)
        /// @return The requested demultiplexed spectrum
        msdata::Spectrum_const_ptr GetDemuxSpectrum(size_t index) const;

        /// PrecursorMaskCodec is used for interpreting a series of spectra and generating an MSX design matrix
        IPrecursorMaskCodec::ptr pmc_;

        /// SpectrumList that caches recently used spectra since we expect to access the same spectra multiple times while demultiplexing
        msdata::SpectrumListPtr sl_;

        /// Each input mux'd spectrum is split into multiple demux'd spectra. Therefore, we need to reinterpret spectrum
        /// requests by index and map them to a larger set of indices. The indexMap keeps track of mapping of an input
        /// query index to demux'd sub-spectrum.
        IndexMapper::ptr indexMapper_;

        /// Once a design matrix is generated from the experimental design and a response matrix is made from the signals in the multiplexed spectra, the
        /// demultiplexing problem can be framed as a non-negative least squares problem. This NNLS problem is delegated to the DemuxSolver.
        DemuxSolver::ptr demuxSolver_;

        /// This caches the last solution generated by the DemuxSolver.
        /// We expect to access the demultiplixed spectra in order but may access a multiplexed spectrum multiple times before moving to the next solve.
        /// The primary application is currently to write spectra sequentially to file so caching optimization for random access isn't necessary at this time.
        boost::shared_ptr<PreviousDemuxSolution> lastSolved_;

        /// The demultiplexer to use for generating the matrices to be solved
        IDemultiplexer::ptr demux_;

        /// A set of user-defined options
        Params params_;

#ifdef _USE_DEMUX_DEBUG_WRITER
        boost::shared_ptr<DemuxDebugWriter> debugWriter_;
#endif
    };

    inline PrecursorMaskCodec::Params GeneratePrecursorMaskCodecParams(SpectrumList_Demux::Params p)
    {
        PrecursorMaskCodec::Params newParams;
        newParams.variableFill = p.variableFill;
        newParams.minimumWindowSize = p.minimumWindowSize;
        return newParams;
    }

    inline MSXDemultiplexer::Params GenerateMSXParams(SpectrumList_Demux::Params p)
    {
        MSXDemultiplexer::Params newParams;
        newParams.applyWeighting = p.applyWeighting;
        newParams.massError = p.massError;
        newParams.variableFill = p.variableFill;
        return newParams;
    }

    inline OverlapDemultiplexer::Params GenerateOverlapParams(SpectrumList_Demux::Params p)
    {
        OverlapDemultiplexer::Params newParams;
        newParams.interpolateRetentionTime = p.interpolateRetentionTime;
        newParams.applyWeighting = p.applyWeighting;
        newParams.massError = p.massError;
        return newParams;
    }
    
    namespace {

    /// Key-value pairs for transforming Optimization enums to strings and vice-versa
    const std::map<SpectrumList_Demux::Params::Optimization, std::string> kOptimizationStrings = {
        { SpectrumList_Demux::Params::Optimization::NONE, "none" },
        { SpectrumList_Demux::Params::Optimization::OVERLAP_ONLY, "overlap_only" }
    };

    } // namespace

    std::string SpectrumList_Demux::Params::optimizationToString(SpectrumList_Demux::Params::Optimization opt)
    {
        return enumToString<SpectrumList_Demux::Params::Optimization>(opt, kOptimizationStrings);
    }

    SpectrumList_Demux::Params::Optimization SpectrumList_Demux::Params::stringToOptimization(const std::string& s)
    {
        return stringToEnum<SpectrumList_Demux::Params::Optimization>(s, kOptimizationStrings);
    }

    SpectrumList_Demux::Impl::IndexMapper::IndexMapper(SpectrumListPtr originalIn, const IPrecursorMaskCodec& pmc)
        : original(originalIn), detailLevel(DetailLevel::DetailLevel_InstantMetadata)
    {
        if (!original.get()) throw runtime_error("[SpectrumlList_Demux] Null pointer");

        // iterate through the spectra, building the expanded index
        for (size_t i = 0, end = original->size(); i < end; ++i)
        {
            const auto& spectrumIdentity = original->spectrumIdentity(i);
            do
            {
                auto spectrum = original->spectrum(i, detailLevel);
                int msLevel = 0;
                if (TryGetMSLevel(*spectrum, msLevel))
                {
                    pushSpectrum(spectrumIdentity, msLevel, pmc.GetPrecursorsPerSpectrum(), pmc.GetOverlapsPerCycle());
                    break;
                }
                detailLevel = DetailLevel(int(detailLevel) + 1);
            } while (int(detailLevel) <= int(DetailLevel::DetailLevel_FullMetadata));
        }
    }

    void SpectrumList_Demux::Impl::IndexMapper::pushSpectrum(const SpectrumIdentity& spectrumIdentity, int msLevel, int numPrecursors, int numOverlap)
    {
        DemuxRequestIndex originalIndex;
        originalIndex.msLevel = msLevel;
        originalIndex.spectrumOriginalIndex = spectrumIdentity.index;
        int numDemuxIndices = numPrecursors * numOverlap;
        if (msLevel != 2)
        {
            // spectrum will not be demux'd
            numDemuxIndices = 1;
        }

        for (int demuxIndex = 0; demuxIndex < numDemuxIndices; ++demuxIndex)
        {
            // spectrum will be demux'd
            int pIndex = demuxIndex / numOverlap; // Use floored integer division to repeat the precursor index for the number of overlap sections
            originalIndex.precursorIndex = pIndex;
            originalIndex.demuxIndex = demuxIndex;
            indexMap.push_back(originalIndex);
            spectrumIdentities.push_back(spectrumIdentity);
            spectrumIdentities.back().index = spectrumIdentities.size() - 1;
            //spectrumIdentities.back().id += " demux=" + to_string(demuxIndex);
            // update scan= and use originalScan=
            spectrumIdentities.back().id = injectScanId(spectrumIdentities.back().id, spectrumIdentities.size(), demuxIndex);
        }
    }

    string SpectrumList_Demux::Impl::IndexMapper::injectScanId(string id, size_t scanNumber, size_t demuxIndex)
    {
        boost::char_separator<char> sep(" ");
        ScanIdTokenizer tokenizer(id, sep);
        string newId = "";
        for (ScanIdTokenizer::const_iterator token = tokenizer.begin(); token != tokenizer.end(); ++token)
        {
            vector<string> attrs;
            boost::split(attrs, *token, boost::is_any_of("="));
            if (attrs.size() != 2)
            {
                newId += *token + " ";
                continue;
            }
            if (attrs[0] == "scan")
            {
                newId += "originalScan=" + attrs[1] + " ";
                newId += "demux=" + lexical_cast<string>(demuxIndex) + " ";
                newId += "scan=" + lexical_cast<string>(scanNumber) + " ";
                continue;
            }
            newId += *token + " ";
        }
        // remove trailing whitespace
        auto end = newId.find_last_not_of(" ");
        if (end != std::string::npos)
            newId.erase(end + 1);
        return newId;
    }

    SpectrumList_Demux::Impl::Impl(const SpectrumListPtr& inner, const Params& p, DataProcessingPtr dp) :
        demuxSolver_(new NNLSSolver(p.nnlsMaxIter, p.nnlsEps)),
        lastSolved_(new PreviousDemuxSolution),
        params_(p)
#ifdef _USE_DEMUX_DEBUG_WRITER		
        ,
        debugWriter_(boost::make_shared<DemuxDebugWriter>("DemuxDebugOutput.log"))
#endif
    {
        switch (params_.optimization)
        {
        case Params::Optimization::NONE:
            pmc_ = boost::make_shared<PrecursorMaskCodec>(inner, GeneratePrecursorMaskCodecParams(params_));
            demux_ = boost::make_shared<MSXDemultiplexer>(GenerateMSXParams(params_));
            break;
        case Params::Optimization::OVERLAP_ONLY:
            pmc_ = boost::make_shared<PrecursorMaskCodec>(inner, GeneratePrecursorMaskCodecParams(params_));
            demux_ = boost::make_shared<OverlapDemultiplexer>(GenerateOverlapParams(params_));
            break;
        default: break;
        }

        // Generate the IndexMapper using the chosen IPrecursorMaskCodec
        indexMapper_ = boost::make_shared<IndexMapper>(inner, *pmc_);
        // Use a SpectrumListCache since we expect to request the same spectra multiple times to extract all demux spectra before moving to the next
        sl_ = boost::make_shared<SpectrumListCache>(inner, MemoryMRUCacheMode_MetaDataAndBinaryData, 1000);
        // Add processing methods to the copy of the inner SpectrumList's data processing
        /// WARNING: It is important that this gives a string containing "Demultiplexing" in order for SpectrumWorkerThreads.cpp to handle demultiplexing properly.
        ProcessingMethod method;
        method.set(MS_data_processing);
        stringstream processingString;
        processingString << "PRISM " << DemuxDataProcessingStrings::kDEMUX_NAME;
        method.userParams.push_back(UserParam(processingString.str()));
        method.order = static_cast<int>(dp->processingMethods.size());
        if (!dp->processingMethods.empty())
            method.softwarePtr = dp->processingMethods[0].softwarePtr;
        dp->processingMethods.push_back(method);
        // TODO Sanity-check the user's choice of demultiplexer based on the PrecursorMaskCodec's initial read-through of the data set
        // Initialize the unique methods for demultiplexing
        demux_->Initialize(sl_, pmc_);
    }

    PWIZ_API_DECL SpectrumPtr SpectrumList_Demux::Impl::spectrum(size_t index, bool getBinaryData) const
    {
        // TODO -- make this work for getBinaryData is false
        const IndexMapper::DemuxRequestIndex& demuxRequest = indexMapper_->indexMap[index];
        if (demuxRequest.msLevel != 2)
        {
            // This is an MS1 spectrum, so just return it
            Spectrum_const_ptr originalSpectrum = sl_->spectrum(demuxRequest.spectrumOriginalIndex, true);
            SpectrumPtr newSpectrum = boost::make_shared<Spectrum>(*originalSpectrum);
            newSpectrum->index = index;
            newSpectrum->id = spectrumIdentity(index).id;
            return newSpectrum;
        }
        return boost::make_shared<Spectrum>(*GetDemuxSpectrum(index));
    }

    PWIZ_API_DECL SpectrumPtr SpectrumList_Demux::Impl::spectrum(size_t index, DetailLevel detailLevel) const
    {
        // TODO: add ability to deal with non-binary-data requests
        return spectrum(index, true);
    }

    PWIZ_API_DECL size_t SpectrumList_Demux::Impl::size() const
    {
        return indexMapper_->indexMap.size();
    }

    PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Demux::Impl::spectrumIdentity(size_t index) const
    {
        return indexMapper_->spectrumIdentities.at(index);
    }

    Spectrum_const_ptr SpectrumList_Demux::Impl::GetDemuxSpectrum(size_t index) const
    {
        IndexMapper::DemuxRequestIndex& request = indexMapper_->indexMap[index];
        Spectrum_const_ptr refSpectrum = sl_->spectrum(request.spectrumOriginalIndex, true); // The multiplexed spectrum to be demultiplexed
        MatrixPtr solution;
        if (lastSolved_->solution && lastSolved_->origSpecIndex == request.spectrumOriginalIndex)
        {
            // This spectrum has been already solved (there will be separate requests for each precursor of a single spectrum)
            solution = lastSolved_->solution;
        }
        else
        {
            // Figure out which spectra to include in the system of equations to demux
            vector<size_t> muxIndices;
            demux_->GetMatrixBlockIndices(request.spectrumOriginalIndex, muxIndices, params_.demuxBlockExtra);

            // Generate matrices for least squares solve
            MatrixPtr masks;
            MatrixPtr signal;
            demux_->BuildDeconvBlock(request.spectrumOriginalIndex, muxIndices, masks, signal);

            // Perform the least squares solve
            solution.reset(new MatrixType(masks->cols(), signal->cols()));
            demuxSolver_->Solve(masks, signal, solution);
            lastSolved_->solution = solution;
            lastSolved_->origSpecIndex = request.spectrumOriginalIndex;

#ifdef _USE_DEMUX_DEBUG_WRITER
            if (debugWriter_->IsOpen())
            {
                debugWriter_->WriteDeconvBlock(request.spectrumOriginalIndex, masks, solution, signal);
            }
#endif
        }
        
        // Build a new demultiplexed spectrum from a copy of the original spectrum
        SpectrumPtr demuxed = boost::make_shared<Spectrum>(*refSpectrum);
        demuxed->precursors.clear();
        vector<size_t> deconvIndices;
        pmc_->SpectrumToIndices(refSpectrum, deconvIndices);

        // Make the demux window boundaries and add to the spectrum
        auto demuxIsolationWindow = pmc_->GetIsolationWindow(deconvIndices[request.demuxIndex]);
        
        const auto& originalPrecursor = refSpectrum->precursors[request.precursorIndex];
        auto originalWindow = IsolationWindow(originalPrecursor);
        auto demuxPrecursor = originalPrecursor;
        
        {
            // Rewrite the isolation window based on the boundaries found by the IPrecursorMaskCodec.
            auto lowMz = demuxIsolationWindow.lowMz;
            auto highMz = demuxIsolationWindow.highMz;
            auto offsetMz = (highMz - lowMz) / 2.0;
            auto targetMz = lowMz + offsetMz;
            auto mzUnits = demuxPrecursor.isolationWindow.cvParam(MS_isolation_window_target_m_z).units;
            demuxPrecursor.isolationWindow.set(MS_isolation_window_target_m_z, targetMz, mzUnits);
            demuxPrecursor.isolationWindow.set(MS_isolation_window_lower_offset, offsetMz, mzUnits);
            demuxPrecursor.isolationWindow.set(MS_isolation_window_upper_offset, offsetMz, mzUnits);
            if (!demuxPrecursor.selectedIons.empty())
            {
                demuxPrecursor.selectedIons.front().set(MS_selected_ion_m_z, targetMz, mzUnits);
                // Zero the precursor intensity since it is invalidated by splitting the demux windows.
                // This could be recalculated if it ever becomes necessary.
                auto intensityUnits = demuxPrecursor.selectedIons.front().cvParam(MS_peak_intensity).units;
                demuxPrecursor.selectedIons.front().set(MS_peak_intensity, 0, intensityUnits);
            }
        }
        demuxed->precursors.push_back(demuxPrecursor);

        // Add the new spectrum index
        demuxed->index = index;

        // Add the new spectrum identity
        demuxed->id = spectrumIdentity(index).id;
        for (auto & precursor : demuxed->precursors)
        {
            precursor.spectrumID = demuxed->id;
        }
        for (auto & scan : demuxed->scanList.scans)
        {
            scan.spectrumID = demuxed->id;
        }

        const bool isProfileSpectrum = refSpectrum->hasCVParam(MS_profile_spectrum);

        // Build the new mz and intensity arrays
        demuxed->binaryDataArrayPtrs.clear();
        demuxed->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
        BinaryData<double>& newMzs = demuxed->getMZArray()->data;
        BinaryData<double>& newIntensities = demuxed->getIntensityArray()->data;
        BinaryData<double>& originalMzs = refSpectrum->getMZArray()->data;
        BinaryData<double>& originalIntensities = refSpectrum->getIntensityArray()->data;

        auto& referenceDemuxIndices = demux_->SpectrumIndices();
        auto summedIntensities = solution->row(referenceDemuxIndices[0]).eval(); // eval() performs copy instead of reference
        for (size_t i = 1; i < referenceDemuxIndices.size(); ++i)
        {
            summedIntensities += solution->row(referenceDemuxIndices[i]);
        }
        auto rawSolutionIntensities = solution->row(referenceDemuxIndices[request.demuxIndex]);
        for (int i = 0; i < rawSolutionIntensities.size(); ++i)
        {
            // Note: We don't skip zero-valued entries in profile spectra because Thermo's centroider assumes even m/z spacing
            if (rawSolutionIntensities[i] <= 0.0 && !isProfileSpectrum) continue;

            // The original intensities can be 0 even if the least squares solution are non-zero. This may invalidate the idea of rescaling the intensities...
            if (originalIntensities[i] <= 0.0 && !isProfileSpectrum) continue;

            newMzs.push_back(originalMzs[i]);
            if (!params_.variableFill)
            {
                auto newIntensity = originalIntensities[i] * rawSolutionIntensities[i] / summedIntensities[i];
                newIntensities.push_back(newIntensity);
            }
            else
            {
                newIntensities.push_back(rawSolutionIntensities[i]);
            }
        }
        demuxed->defaultArrayLength = newMzs.size();
        return demuxed;
    }


    SpectrumList_Demux::SpectrumList_Demux(const msdata::SpectrumListPtr& inner, const Params& p) : SpectrumListWrapper(inner), impl_(new Impl(inner, p, dp_)) {}
    SpectrumList_Demux::~SpectrumList_Demux() {}
    msdata::SpectrumPtr SpectrumList_Demux::spectrum(size_t index, bool getBinaryData) const { return impl_->spectrum(index, getBinaryData); }
    msdata::SpectrumPtr SpectrumList_Demux::spectrum(size_t index, msdata::DetailLevel detailLevel) const { return impl_->spectrum(index, detailLevel); }
    size_t SpectrumList_Demux::size() const { return impl_->size(); }
    const msdata::SpectrumIdentity& SpectrumList_Demux::spectrumIdentity(size_t index) const { return impl_->spectrumIdentity(index); }

} // namespace analysis
} // namespace pwiz