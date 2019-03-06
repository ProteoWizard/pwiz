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

#include "DemuxDebugReader.hpp"
#include "MatrixIO.hpp"
#include <cassert>

namespace pwiz {
namespace analysis {
    using namespace std;
    using namespace DemuxTypes;
    
    DemuxDebugReader::DemuxDebugReader(const std::string& fileName) :
        _currentBlockIndex(0)
    {
        MatrixIO::GetReadStream(_reader, fileName);
        if (_reader.is_open())
        {
            ReadHeader();
        }
    }

    
    DemuxDebugReader::~DemuxDebugReader()
    {

    }

    
    size_t DemuxDebugReader::NumBlocks() const
    {
        return _fileIndex.size();
    }

    
    void DemuxDebugReader::ReadDeconvBlock(uint64_t& spectrumIndex,
        MatrixPtr masks,
        MatrixPtr solution,
        MatrixPtr signal)
    {
        assert(_reader.is_open());
        assert(_currentBlockIndex < _fileIndex.size());
        if (_currentBlockIndex >= _fileIndex.size())
            return;

        const auto& indices = _fileIndex.at(_currentBlockIndex);

        spectrumIndex = indices.first;

        _reader.seekg(indices.second);

        MatrixIO::ReadBinary(_reader, masks);
        MatrixIO::ReadBinary(_reader, signal);
        MatrixIO::ReadBinary(_reader, solution);
        ++_currentBlockIndex;
    }

    
    void DemuxDebugReader::ReadDeconvBlock(size_t blockIndex,
        uint64_t& spectrumIndex,
        MatrixPtr masks,
        MatrixPtr solution,
        MatrixPtr signal)
    {
        assert(blockIndex < _fileIndex.size());
        _currentBlockIndex = blockIndex;
        ReadDeconvBlock(spectrumIndex, masks, solution, signal);
    }

    
    bool DemuxDebugReader::IsOpen() const
    {
        return _reader.is_open();
    }

    
    void DemuxDebugReader::ReadHeader()
    {
        assert(_reader.is_open());

        // read the file index position
        int64_t fileIndexPos = -1;
        _reader.read(reinterpret_cast<char*>(&fileIndexPos), sizeof(int64_t));

        assert(fileIndexPos > 0);

        auto matricesBeginPos = _reader.tellg();

        // go to the file index and read the file index list
        _reader.seekg(fileIndexPos);
        uint64_t numIndices;
        _reader.read(reinterpret_cast<char*>(&numIndices), sizeof(uint64_t));

        for (auto i = 0; i < numIndices; ++i)
        {
            pair<uint64_t, int64_t> index(0,0);
            _reader.read(reinterpret_cast<char*>(&index.first), sizeof(uint64_t));
            _reader.read(reinterpret_cast<char*>(&index.second), sizeof(int64_t));
            _fileIndex.push_back(index);
        }

        // Go to beginning of matrices
        _reader.seekg(matricesBeginPos);
    }
} //namespace analysis
} //namespace pwiz