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

#ifndef _DEMUXDEBUGWRITER_HPP
#define _DEMUXDEBUGWRITER_HPP

#include "DemuxTypes.hpp"
#include <cstdint>
#include <fstream>
#include <vector>

namespace pwiz {
namespace analysis {
    using std::uint64_t;
    using std::int64_t;

    /// A class for writing demux matrices to file. The primary purpose of writing demux matrices to file is for
    /// analysis externally. Exporting matrices is useful for comparing output with Skyline, which has a similar
    /// functionality for writing demux matrices to file. Python code exists for reading and interpreting these matrices.
    /// This class follows the RAII of ifstream and so the file is kept open until the destructor is called.
    class DemuxDebugWriter
    {
    public:

        /// Constructs a DemuxDebugWriter to write the debug file with the given filename
        explicit DemuxDebugWriter(const std::string& fileName);

        /// Destructor writes header and closes the file
        ~DemuxDebugWriter();

        /// Should be called after construction to verify that the file was opened successfully
        bool IsOpen() const;

        /// Writes a set of matrices with the given spectrum index to file
        void WriteDeconvBlock(uint64_t spectrumIndex,
            DemuxTypes::MatrixPtr masks,
            DemuxTypes::MatrixPtr solution,
            DemuxTypes::MatrixPtr signal);

    private:

        /// Writes the the header. The header is simply a pointer to the footer (fileIndex). 
        void WriteHeader();

        /// Writes the file index at the end of the file. This is the footer pointed to by the header. The footer contains information about
        /// the locations of the beginning of each block. Each matrix has it's own header for information about its size. This means
        /// that individual matrices must be accessed sequentially.
        void WriteIndex();
        
        /// Output file stream
        std::ofstream _writer;
        
        /// Set of spectrum indices and filepointers to their respective blocks
        std::vector<std::pair<uint64_t, int64_t>> _fileIndex;
    };
} //namespace analysis
} //namespace pwiz
#endif //_DEMUXDEBUGWRITER_HPP