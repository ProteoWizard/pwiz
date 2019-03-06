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

#include "pwiz/analysis/demux/MatrixIO.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include <boost/make_shared.hpp>

using namespace pwiz::util;
using namespace pwiz::analysis;

class MatrixIOTest {
public:
    void Run()
    {
        SetUp();
        SingleReadWrite();
        MultiReadWrite();
        TearDown();
    }

protected:

    virtual void SetUp()
    {
    }

    void TearDown()
    {
        remove("TestMatrixIOTemp.log");
    }

    void SingleReadWrite()
    {
        std::string testFilename = "TestMatrixIOTemp.log";
        DemuxTypes::MatrixPtr matrix = boost::make_shared<DemuxTypes::MatrixType>(3, 3);
        *matrix << 1, 2, 3,
            4, 5, 6,
            7, 8, 9;
        MatrixIO::WriteBinary(testFilename, matrix);

        DemuxTypes::MatrixPtr matrixIn = boost::make_shared<DemuxTypes::MatrixType>(1, 1);
        MatrixIO::ReadBinary(testFilename, matrixIn);

        unit_assert(matrixIn->isApprox(*matrix));
    }

    void MultiReadWrite()
    {
        std::string testFilename = "TestMatrixIOTemp.log";

        boost::filesystem::path full_path(boost::filesystem::current_path());

        DemuxTypes::MatrixPtr A = boost::make_shared<DemuxTypes::MatrixType>(3, 4);
        DemuxTypes::MatrixPtr B = boost::make_shared<DemuxTypes::MatrixType>(4, 3);
        DemuxTypes::MatrixPtr C = boost::make_shared<DemuxTypes::MatrixType>(3, 3);
        *A << -14.834628974133, -15.729764770592, 56.292839002858, 30.766363712773,
            79.595747995303, -8.356622426449, 20.840197237638, 83.801095382748,
            87.889866880787, 13.75327399942, 86.730656404499, -0.46420627108677;

        *B << 23.588885367543, 49.667231605868, -86.700220187964,
            51.392601274063, -77.511392742378, 23.389497301117,
            -78.475202879706, -62.60684915327, -42.39206607192,
            59.595164405161, 2.1025961854091, 65.787705013259;

        *C = *A * *B;

        std::ofstream out;
        MatrixIO::GetWriteStream(out, testFilename);
        unit_assert(out.is_open());
        MatrixIO::WriteBinary(out, A);
        MatrixIO::WriteBinary(out, B);
        MatrixIO::WriteBinary(out, C);
        out.flush();
        out.close();

        DemuxTypes::MatrixPtr AIn = boost::make_shared<DemuxTypes::MatrixType>(1, 1);
        DemuxTypes::MatrixPtr BIn = boost::make_shared<DemuxTypes::MatrixType>(1, 1);
        DemuxTypes::MatrixPtr CIn = boost::make_shared<DemuxTypes::MatrixType>(1, 1);

        std::ifstream in;
        MatrixIO::GetReadStream(in, testFilename);
        unit_assert(in.is_open());
        MatrixIO::ReadBinary(in, AIn);
        MatrixIO::ReadBinary(in, BIn);
        MatrixIO::ReadBinary(in, CIn);
        in.close();

        unit_assert(AIn->isApprox(*A));
        unit_assert(BIn->isApprox(*B));
        unit_assert(CIn->isApprox(*C));
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        MatrixIOTest tester;
        tester.Run();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}