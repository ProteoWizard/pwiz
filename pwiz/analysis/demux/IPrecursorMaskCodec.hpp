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

#ifndef _IPRECURSORMASKCODEC_HPP
#define _IPRECURSORMASKCODEC_HPP

#include "DemuxTypes.hpp"
#include "DemuxHelpers.hpp"

namespace pwiz{
namespace analysis{

    // TODO Rework the MZHash system to be strongly typed. Implicit conversion of uint to float and vice-versa is possible.
    typedef uint64_t MZHash;

    /// A method of hashing an isolation window to a unique long value
    /// mz is and m/z of a unique point in the isolation window, such as
    /// the lower bound, upper bound, or center. This value is multiplied
    /// by 100000000 and rounded to convert the isolation m/z to an integer that
    /// is used as the hash. This creates an effective fuzzy window of
    /// +/- 5e-8 m/z. For example: a window with m/z 500.49 would
    /// be hashed to 50049000000.
    class IsoWindowHasher
    {
    public:

        /// Hash a floating-point m/z value to an integer
        static MZHash Hash(double mz)
        {
            auto mult = mz * 100000000.0;
            auto rounded = llround(mult);
            return static_cast<MZHash>(rounded);
        }

        /// Unhash an integer to a floating-point m/z value
        static double UnHash(MZHash hashed)
        {
            return hashed / 100000000.0;
        }
    };

    /// A container for describing the isolation windows that are dedicated to columns of the design matrix for demultiplexing.
    ///
    /// Ideally, a ProteoWizard Precursor container could be used instead with manipulation of its internal data since a demultiplexed spectrum
    /// is in many ways able to be thought of as an isolated spectrum with narrower isolation boundaries. However, this is used as a slimmer
    /// container in favor of the existing ProteoWizard Precursor because in the case of overlapping spectra the Precursor object would have
    /// to be copied and manipulated to split the isolation ranges.
    struct DemuxWindow
    {
        MZHash mzLow; ///< Start m/z of the window range

        MZHash mzHigh; ///< End m/z of the window range

        /// Constructs a DemuxWindow from a Precursor by using its isolation window.
        explicit DemuxWindow(const msdata::Precursor& p)
        {
            double target = precursor_target(p);
            mzLow = IsoWindowHasher::Hash(target - precursor_lower_offset(p));
            mzHigh = IsoWindowHasher::Hash(target + precursor_upper_offset(p));
        }

        /// Constructs a DemuxWindow for a given mass range.
        explicit DemuxWindow(MZHash mzLow, MZHash mzHigh) :    mzLow(mzLow), mzHigh(mzHigh)
        {
        }        

        /// Isolation windows are sorted by their start value
        bool operator<(const DemuxWindow& rhs) const { return this->mzLow < rhs.mzLow; }

        /// Can be used to find whether the mass range of another DemuxWindow is a subset of this one.
        bool Contains(const DemuxWindow& inner) const
        {
            return inner.mzLow >= this->mzLow && inner.mzHigh <= this->mzHigh;
        }

        /// Used to find whether a window's center is contained within this window
        bool ContainsCenter(const DemuxWindow& inner) const
        {
            auto center = static_cast<MZHash>(llround(inner.mzLow + (inner.mzHigh - inner.mzLow) / 2.0));
            return center >= this->mzLow && center <= this->mzHigh;
        }

        /// Can be used to find whether two windows are identical within the error of the hash
        bool operator==(const DemuxWindow& rhs) const { return rhs.Contains(*this) && this->Contains(rhs); }

        /// Can be used to find whether two windows are identical within the error of the hash
        bool operator!=(const DemuxWindow& rhs) const { return !(*this == rhs); }
    };

    /// A container that wraps DemuxWindow to preserve the full precision window boundaries
    struct IsolationWindow
    {

        /// Constructs an IsolationWindow from a Precursor
        explicit IsolationWindow(const msdata::Precursor& p) :
            lowMz(precursor_mz_low(p)), highMz(precursor_mz_high(p)), demuxWindow(p) {}

        /// Constructs an IsolationWindow from a given mass range
        IsolationWindow(double mzLow, double mzHigh) :
            lowMz(mzLow), highMz(mzHigh), demuxWindow(IsoWindowHasher::Hash(mzLow), IsoWindowHasher::Hash(mzHigh)) {}

        double lowMz; ///< Full precision lower m/z bound

        double highMz; ///< Full precision upper m/z bound

        /// Set of isolation window boundaries that provides useful operations for sorting and comparing
        /// different isolation windows.
        DemuxWindow demuxWindow;

        /// Isolation windows are sorted by their start value
        bool operator<(const IsolationWindow& rhs) const { return this->demuxWindow < rhs.demuxWindow; }
    };

    /// Interface for generating and accessing precursor masks for a demultiplexing scheme.
    class IPrecursorMaskCodec
    {
    public:

        /// Shared pointer definition
        typedef boost::shared_ptr<IPrecursorMaskCodec> ptr;

        /// Constant shared pointer definition
        typedef boost::shared_ptr<const IPrecursorMaskCodec> const_ptr;

        /// Generates a design matrix row describing which precursor isolation windows are present in the given spectrum. This row can be weighted by
        /// a given scalar.
        /// @param[in] sPtr Multiplexed spectrum from which to extract precursor windows.
        /// @param[in] weight Scalar value by which to weight the resulting design matrix vector.
        ///                   This weighting is a simple scalar multiplication of the vector.
        /// @return Design matrix row describing which precursor isolation windows are present in the given spectrum.
        virtual Eigen::VectorXd GetMask(msdata::Spectrum_const_ptr sPtr, double weight = 1.0) const = 0;

        /// Generates a design matrix row describing which precursor isolation windows are present in the given spectrum and places it into the specified
        /// row of the user-provided matrix.
        /// @param[in] sPtr Multiplexed spectrum from which to extract precursor windows.
        /// @param[out] m Matrix in which to place the design vector.
        /// @param[in] rowNum Row of the matrix in which to place the design vector corresponding to the given spectrum.
        /// @param[in] weight Scalar value by which to weight the resulting design matrix vector.
        ///                   This weighting is a simple scalar multiplication of the vector.
        /// \pre Out array must be same size as GetDemuxBlockSize()
        virtual void GetMask(msdata::Spectrum_const_ptr sPtr, DemuxTypes::MatrixType& m, size_t rowNum, double weight = 1.0) const = 0;

        /// Identifies the precursor windows within a spectrum and returns the indices to the design matrix columns corresponding to those windows
        /// @param[in] sPtr Multiplexed spectrum from which to extract precursor windows.
        /// @param[out] indices Indices of the design matrix columns that correspond to the precursor windows in the given spectrum.
        virtual void SpectrumToIndices(msdata::Spectrum_const_ptr sPtr, std::vector<size_t>& indices) const = 0;

        /// Returns the precursor window for a given index.
        /// @param[in] i Index of the column of the design matrix corresponding to the precursor window.
        /// @return A DemuxWindow describing the m/z range of the precursor window.
        virtual IsolationWindow GetIsolationWindow(size_t i) const = 0;

        /// Returns the total number of demux'd precursor windows. This is the number of possible indices returned by SpectrumToIndices().
        virtual size_t GetNumDemuxWindows() const = 0;

        /// Returns the number of spectra required to cover all precursor isolation windows
        virtual int GetSpectraPerCycle() const = 0;

        /// Returns the number of precursor isolations per spectrum. This is verified to be constant for all spectra.
        virtual int GetPrecursorsPerSpectrum() const = 0;

        /// Returns the number of overlap repeats per cycle. So for no overlap, this returns 1. For an overlap that splits each precursor in two, this returns 2. Etc.
        virtual int GetOverlapsPerCycle() const = 0;

        /// Returns the number of windows required to demultiplex
        virtual size_t GetDemuxBlockSize() const = 0;

        virtual ~IPrecursorMaskCodec() {}
    };
} // namespace analysis
} // namespace pwiz

#endif // _IPRECURSORMASKCODEC_HPP
