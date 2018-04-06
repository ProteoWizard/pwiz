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

#ifndef _DEMUXDEBUGREADER_HPP
#define _DEMUXDEBUGREADER_HPP

#include "DemuxTypes.hpp"
#include <cstdint>
#include <fstream>
#include <vector>

namespace pwiz {
namespace analysis {
    using std::uint64_t;
    using std::int64_t;

    /// A class for reading demux matrices from file. The primary purpose of writing demux matrices to file is for
    /// analysis externally, so the intent of this class is to provide a method to test the output of the
    /// DemuxDebugWriter class. This class follows the RAII of ifstream and so the file is kept open until the
    /// destructor is called.
    class DemuxDebugReader
    {
    public:

        /// Constructs a DemuxDebugReader to read the debug file with the given filename. During construction the
        /// file header is read to inform the NumBlocks() function of the filesize and to build a map of pointers
        /// to the matrix blocks for random access.
        /// @param fileName Filename of debug matrices file
        explicit DemuxDebugReader(const std::string& fileName);

        /// Destructor closes the file
        ~DemuxDebugReader();

        /// Number of blocks (sets of matrices) that are contained in the file. This is the number of times that ReadDeconvBlock
        /// can be called sequentially. Throws error if IsOpen() returns false.
        /// @return Number of blocks in the file
        size_t NumBlocks() const;

        /// Can be used to read through the blocks sequntially. Each time this is called the next set of blocks is returned. This
        /// can be called NumBlocks() times before an out_of_range error will be thrown. Throws error if IsOpen() returns false.
        /// @param[out] spectrumIndex The index of the spectrum corresponding to this block
        /// @param[out] masks The masks matrix
        /// @param[out] solution The solution matrix
        /// @param[out] signal The signal matrix
        void ReadDeconvBlock(uint64_t& spectrumIndex,
            DemuxTypes::MatrixPtr masks,
            DemuxTypes::MatrixPtr solution,
            DemuxTypes::MatrixPtr signal);

        /// Used for random-access reading of the blocks. The block indices range from 0 to NumBlocks() - 1. Throws error if IsOpen() returns false.
        /// @param[in] blockIndex index of the block to read
        /// @param[out] spectrumIndex The index of the spectrum corresponding to this block
        /// @param[out] masks The masks matrix
        /// @param[out] solution The solution matrix
        /// @param[out] signal The signal matrix
        void ReadDeconvBlock(size_t blockIndex,
            uint64_t& spectrumIndex,
            DemuxTypes::MatrixPtr masks,
            DemuxTypes::MatrixPtr solution,
            DemuxTypes::MatrixPtr signal);

        /// Should be called after construction to verify that the file was opened successfully
        /// @return true if file was successfully opened and its header read and verified, false otherwise
        bool IsOpen() const;

    private:

        /// Reads the header/footer which contains information about the number of blocks and their locations in the file for random access.
        void ReadHeader();

        /// Input file stream of the debug file
        std::ifstream _reader;

        /// Set of pointers to blocks in the file extracted from the header/footer information
        std::vector<std::pair<uint64_t, int64_t>> _fileIndex;

        /// Current block index used for tracking progress through file when sequential iteration of ReadDeconvBlock() is used
        size_t _currentBlockIndex;
    };

} //namespace analysis
} //namespace pwiz
#endif //_DEMUXDEBUGREADER_HPP