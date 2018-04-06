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

#ifndef _MATRIXIO_HPP
#define _MATRIXIO_HPP

#include "DemuxTypes.hpp"
#include <boost/smart_ptr/shared_ptr.hpp>
#include <fstream>

namespace pwiz
{
namespace analysis
{
    /// Provides static methods for writing and reading matrices to/from files. An example file format used is as follows:
    /// byte                        | type      | description
    /// --------------------------- | --------- | -----------
    ///    0                            | int64     | num_rows
    ///    8                            | int64     | num_cols
    ///    16                            | double    | element_0
    ///    24                            | double    | element_1
    ///    32                            | double    | element_2
    ///    ...                            | double(s) | ...
    ///    (num_elements - 1) * 8 + 16 | double    | element_n
    ///
    /// Note that the type of the index (shown as int64) is subject to change and is dependent on the type of Matrix::Index.
    /// Similarly, the type of each element is dependent on the type of the Scalar type of the Matrix. However, in the current
    /// (as of 8/30/2016) implementation of DemuxTypes::MatrixType the types shown above are accurate.
    class MatrixIO
    {
    public:

        /// Factory method for generating output streams with fixed properties set for writing matrices
        static void GetWriteStream(std::ofstream& out, const std::string& filename);

        /// Writes a matrix to filestream including header information about the number of rows and columns. Matrices are written in row-major format.
        static void WriteBinary(std::ofstream& out, boost::shared_ptr<DemuxTypes::MatrixType> matrix);

        /// Convience function for writing a single matrix to file.
        static void WriteBinary(const std::string& filename, boost::shared_ptr<DemuxTypes::MatrixType> matrix);

        /// Factory method for generating input streams with fixed properties set for reading matrices
        static void GetReadStream(std::ifstream& in, const std::string& filename);

        /// Convenience function for reading a single matrix from a file
        static void ReadBinary(std::ifstream& in, boost::shared_ptr<DemuxTypes::MatrixType> matrix);

        /// Reads a matrix from filestream. Matrices are written in row-major format.
        static void ReadBinary(const std::string& filename, boost::shared_ptr<DemuxTypes::MatrixType> matrix);
    };
} // namespace analysis
} // namespace pwiz
#endif //_MATRIXIO_HPP