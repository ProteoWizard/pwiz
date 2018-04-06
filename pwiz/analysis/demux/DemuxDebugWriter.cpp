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

#include "DemuxDebugWriter.hpp"
#include "MatrixIO.hpp"

namespace pwiz {
namespace analysis {
    using namespace std;
    using namespace DemuxTypes;
    
    DemuxDebugWriter::DemuxDebugWriter(const std::string& fileName)
    {
        MatrixIO::GetWriteStream(_writer, fileName);
        if (_writer.is_open())
        {
            WriteHeader();
        }
    }

    
    DemuxDebugWriter::~DemuxDebugWriter()
    {
        if (_writer.is_open())
        {
            WriteIndex();
            _writer.flush();
            _writer.close();
        }
    }

    
    bool DemuxDebugWriter::IsOpen() const
    {
        return _writer.is_open();
    }

    
    void DemuxDebugWriter::WriteDeconvBlock(std::uint64_t spectrumIndex,
        MatrixPtr masks,
        MatrixPtr solution,
        MatrixPtr signal)
    {
        if (!_writer.is_open())
        {
            throw runtime_error("WriteDeconvBlock() Attempted to write deconv matrices after file failed to open");
        }
        _fileIndex.push_back(std::pair<std::uint64_t, int64_t>(spectrumIndex, _writer.tellp()));
        MatrixIO::WriteBinary(_writer, masks);
        MatrixIO::WriteBinary(_writer, signal);
        MatrixIO::WriteBinary(_writer, solution);
    }


    void DemuxDebugWriter::WriteHeader()
    {
        if (!_writer.is_open())
        {
            throw runtime_error("WriteHeader() Attempted to write deconv matrices header after write file failed to open");
        }

        // placeholder for the pointer to the file index
        int64_t indexPos = _writer.tellp();
        _writer.write(reinterpret_cast<const char*>(&indexPos), sizeof(int64_t));
    }

    
    void DemuxDebugWriter::WriteIndex()
    {
        // write the index pointer
        int64_t indexPos = _writer.tellp();
        _writer.seekp(0, ios_base::beg);
        _writer.write(reinterpret_cast<const char*>(&indexPos), sizeof(int64_t));

        // Go to index pointer location and write the file index list
        _writer.seekp(indexPos);
        uint64_t numIndices = _fileIndex.size();
        _writer.write(reinterpret_cast<const char*>(&numIndices), sizeof(uint64_t));
        for (auto index : _fileIndex)
        {
            _writer.write(reinterpret_cast<const char*>(&index.first), sizeof(uint64_t));
            _writer.write(reinterpret_cast<const char*>(&index.second), sizeof(int64_t));
        }
    }
} //namespace analysis
} //namespace pwiz