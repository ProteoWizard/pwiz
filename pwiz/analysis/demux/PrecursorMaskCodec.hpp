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

#ifndef _PRECURSORMASKCODEC_HPP
#define _PRECURSORMASKCODEC_HPP

#include "IPrecursorMaskCodec.hpp"

namespace pwiz{
namespace analysis{
    
    /// Implementation of the IPrecursorMaskCodec interface that is able to handle both overlapping MSX experiments. Some features that are only
    /// applicable when overlapping is used without MSX or MSX used without overlapping are not implemented in this class. Such features and optimizations
    /// are currently left to other more targeted implementations of IPrecursorMaskCodec. One missing feature is interpolation of overlap-only data to
    /// optimize weighting of nearby spectra before demultiplexing.
    class PrecursorMaskCodec : public IPrecursorMaskCodec
    {
    public:
        
        struct Params
        {
            Params() :
            variableFill(false),
            minimumWindowSize(0.2) {}

            /// Whether this data acquired with variable fill times or not.
            bool variableFill;

            /// This tolerance is used to decide whether window boundaries are aligned on the same point
            double minimumWindowSize;
        };

        /// Construct a PrecursorMaskCodec for interpreting overlapping and MSX experiments for demultiplexing.
        /// @param[in] slPtr SpectrumList to demux
        /// @param[in] p Parameter set
        explicit PrecursorMaskCodec(msdata::SpectrumList_const_ptr slPtr, Params p = Params());
        virtual ~PrecursorMaskCodec(){}

        /// \name IPrecursorMaskCodec interface
        ///@{

        Eigen::VectorXd GetMask(msdata::Spectrum_const_ptr sPtr, double weight) const override;
        void GetMask(msdata::Spectrum_const_ptr sPtr, DemuxTypes::MatrixType& m, size_t rowNum, double weight) const override;
        void SpectrumToIndices(msdata::Spectrum_const_ptr spectrumPtr, std::vector<size_t>& indices) const override;
        IsolationWindow GetIsolationWindow(size_t i) const override;
        size_t GetNumDemuxWindows() const override;
        int GetSpectraPerCycle() const override;
        int GetPrecursorsPerSpectrum() const override;
        int GetOverlapsPerCycle() const override;
        size_t GetDemuxBlockSize() const override;
        ///@}

    protected:
        
        
        /// Simple container that is useful for breaking up DemuxWindows into their edges and resolving overlap
        struct DemuxBoundary
        {
            /// Constructs a DemuxBoundary from an m/z floating point value
            explicit DemuxBoundary(double mz) : mz(mz), mzHash(IsoWindowHasher::Hash(mz)) {}

            double mz; ///< Full precision m/z value

            MZHash mzHash; ///< Hashed m/z value for fast and simple comparison operations

            /// DemuxBoundaries are sorted to the precision of their hash
            bool operator<(const DemuxBoundary& rhs) const { return this->mzHash < rhs.mzHash; }

            /// DemuxBoundaries are equated only by their hashes
            bool operator==(const DemuxBoundary& rhs) const { return this->mzHash == rhs.mzHash; }
        };

        /// Interpret the experimental design of the multiplexed experiment and cache values for building the design matrix when later given spectra.
        void ReadDemuxScheme(msdata::SpectrumList_const_ptr spectrumList);
        
        /// Identifies the repeating scan pattern in the experiment and extracts features of the experimental design in order to interpret the intended demux
        /// scheme. Note that an alternative to this would be to have the experimental design specified by the user or written in some form in metadata.
        /// @param[in] spectrumList The SpectrumListPtr containing the multiplexed experiment
        /// @param[out] demuxWindows The largest set of windows that the experiment can be demultiplexed into when not accounting for overlap.
        /// \post demuxWindows is sorted and contains no duplicate elements
        void IdentifyCycle(msdata::SpectrumList_const_ptr spectrumList, std::vector<IsolationWindow>& demuxWindows);
        
        /// Identifies any overlap in a DemuxWindow set and splits any overlapping regions such that a non-overlapping DemuxWindow set is produced.
        /// @param[in] demuxWindows Set of possibly overlapping DemuxWindows.
        /// @param[out] demuxWindows Set of non-overlapping DemuxWindows produced from the input demuxWindows. 
        /// \pre demuxWindows is sorted and has no duplicate elements.
        /// \post demuxWindows is output as a vector of size equal to or greater than the input vector.
        void IdentifyOverlap(std::vector<IsolationWindow>& demuxWindows);
    
    private:

        /// Template for retrieving masks from array-like objects with [] accessors. Rvalue references are used for compatibility with Eigen types.
        /// See IPrecursorMaskCodec::GetMask
        /// @param[out] arrayType Array-like object with [] accessor
        /// @param[in] sPtr Multiplexed spectrum from which to extract precursor windows.
        /// @param[in] weight Scalar value by which to weight the resulting design matrix vector.
        ///                   This weighting is a simple scalar multiplication of the vector.
        template <class T>
        void GetMask(T&& arrayType, msdata::Spectrum_const_ptr sPtr, double weight) const;

        /// This is effectively the index to isolation window map for translating isolation windows to 
        std::vector<IsolationWindow> isolationWindows_;
        
        /// Number of spectra required to cover all precursor windows. This is the number of spectra required to make a fully determined
        /// system of equations.
        size_t spectraPerCycle_;

        /// Number of precursors (or isolation windows) per multiplexed spectrum. This is calculated in ReadDemuxScheme() and is assumed to be constant
        /// for all spectra. An error is thrown if this is ever observed to change.
        size_t precursorsPerSpectrum_;
        
        /// Number of overlap windows per multiplexed spectrum. E.g. having one additional round of acquisition with an overlap offset would result in
        /// two overlap regions per spectrum. Alternatively, in the case of no overlap the number of "overlap windows" is one. This is calculated in
        /// ReadDemuxScheme() and assumed to be constant for all spectra. An error is thrown if this is ever observed to change.
        size_t overlapsPerSpectrum_;
        
        Params params_;
    };
} // namespace analysis
} // namespace pwiz
#endif // _PRECURSORMASKCODEC_HPP