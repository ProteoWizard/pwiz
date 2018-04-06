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

#include "MatrixIO.hpp"

namespace pwiz
{
namespace analysis
{
    using namespace std;
    using namespace DemuxTypes;
    using namespace Eigen;

    void MatrixIO::GetWriteStream(ofstream& out, const string& filename)
    {
        out.open(filename, ios::out | ios::binary | ios::trunc);
    }

    void MatrixIO::WriteBinary(ofstream& out, boost::shared_ptr<MatrixType> matrix)
    {
        MatrixType::Index rows = matrix->rows(), cols = matrix->cols();
        out.write(reinterpret_cast<char*>(&rows), sizeof(MatrixType::Index));
        out.write(reinterpret_cast<char*>(&cols), sizeof(MatrixType::Index));
        // Store as row-major
        if (matrix->IsRowMajor)
            out.write(reinterpret_cast<char*>(matrix->data()), rows * cols * sizeof(MatrixType::Scalar));
        else
        {
            for (int row = 0; row < rows; ++row)
            {
                for (int col = 0; col < cols; ++col)
                {
                    out.write(reinterpret_cast<char*>(&(*matrix)(row, col)), sizeof(MatrixType::Scalar));
                }
            }
        }
    }

    void MatrixIO::WriteBinary(const string& filename, boost::shared_ptr<MatrixType> matrix)
    {
        ofstream out;
        GetWriteStream(out, filename);
        if (!out.is_open())
        {
            throw runtime_error("Could not open file");
        }
        WriteBinary(out, matrix);
        out.flush();
        out.close();
    }

    void MatrixIO::GetReadStream(ifstream& in, const string& filename)
    {
        in.open(filename, ios::in | ios::binary);
    }

    void MatrixIO::ReadBinary(ifstream& in, boost::shared_ptr<MatrixType> matrix)
    {
        MatrixType::Index rows = 0, cols = 0;
        in.read(reinterpret_cast<char*>(&rows), sizeof(MatrixType::Index));
        in.read(reinterpret_cast<char*>(&cols), sizeof(MatrixType::Index));
        matrix->resize(rows, cols);
        // File should be stored as row major
        if (matrix->IsRowMajor)
            in.read(reinterpret_cast<char *>(matrix->data()), rows * cols * sizeof(MatrixType::Scalar));
        else
        {
            vector<double> flattened(rows * cols);
            in.read(reinterpret_cast<char *>(&flattened[0]), rows * cols * sizeof(MatrixType::Scalar));
            Map<Matrix<MatrixType::Scalar, Dynamic, Dynamic, RowMajor>> readMatrix(&flattened[0], rows, cols);
            *matrix = readMatrix;
        }
    }

    void MatrixIO::ReadBinary(const string& filename, boost::shared_ptr<MatrixType> matrix)
    {
        ifstream in;
        GetReadStream(in, filename);
        if (!in.is_open())
        {
            throw runtime_error("Could not open file");
        }
        ReadBinary(in, matrix);
        in.close();
    }
} // namespace analysis
} // namespace pwiz