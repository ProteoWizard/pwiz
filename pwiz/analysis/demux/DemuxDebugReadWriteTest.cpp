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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/analysis/demux/DemuxDebugReader.hpp"
#include "pwiz/analysis/demux/DemuxDebugWriter.hpp"
#include <boost/make_shared.hpp>

using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace DemuxTypes;

class DemuxDebugRWTest {

public:

    void Run()
    {
        SetUp();
        ReadWriteTest();
        TearDown();
    }

protected:

    void ReadWriteTest()
    {
        { // Use scope to ensure deletion of debug writer to close output file
            DemuxDebugWriter debugWriter("DemuxDebugOutput_TestOut.log");
            unit_assert(debugWriter.IsOpen());

            for (size_t i = 0; i < spectrumList_.size(); i += 3)
            {
                debugWriter.WriteDeconvBlock(i, spectrumList_.at(i), spectrumList_.at(i + 1), spectrumList_.at(i + 2));
            }
        }

        vector<MatrixPtr> readSpectrumList;

        DemuxDebugReader debugReader("DemuxDebugOutput_TestOut.log");
        unit_assert(debugReader.IsOpen());
        unit_assert_operator_equal(3 * debugReader.NumBlocks(), spectrumList_.size());

        uint64_t spectrumIndex = 0;
        for (size_t i = 0; i < debugReader.NumBlocks(); ++i)
        {
            auto index = static_cast<uint64_t>(spectrumIndex); // needed for 32-bit compatibility
            debugReader.ReadDeconvBlock(index, A_, B_, C_);
            readSpectrumList.push_back(A_);
            readSpectrumList.push_back(B_);
            readSpectrumList.push_back(C_);
        }

        unit_assert_operator_equal(readSpectrumList.size(), spectrumList_.size());

        for (size_t i = 0; i < readSpectrumList.size(); ++i)
        {
            unit_assert(spectrumList_.at(i)->isApprox(*readSpectrumList.at(i)));
        }
    }

    void SetUp() {
        // Generate a list of spectra
        A_ = boost::make_shared<MatrixType>(3, 4);
        B_ = boost::make_shared<MatrixType>(4, 3);
        C_ = boost::make_shared<MatrixType>(3, 3);
        *A_ << -14.834628974133, -15.729764770592, 56.292839002858, 30.766363712773,
            79.595747995303, -8.356622426449, 20.840197237638, 83.801095382748,
            87.889866880787, 13.75327399942, 86.730656404499, -0.46420627108677;

        *B_ << 23.588885367543, 49.667231605868, -86.700220187964,
            51.392601274063, -77.511392742378, 23.389497301117,
            -78.475202879706, -62.60684915327, -42.39206607192,
            59.595164405161, 2.1025961854091, 65.787705013259;

        *C_ = *A_ * *B_;

        spectrumList_.push_back(A_);
        spectrumList_.push_back(B_);
        spectrumList_.push_back(C_);

        MatrixPtr D = boost::make_shared<MatrixType>(3, 4);
        MatrixPtr E = boost::make_shared<MatrixType>(4, 3);
        MatrixPtr F = boost::make_shared<MatrixType>(3, 3);

        *D = 5 * A_->eval();
        *E = 3 * B_->eval();
        *F = *A_ * *B_;

        spectrumList_.push_back(A_);
        spectrumList_.push_back(B_);
        spectrumList_.push_back(C_);
    }

    void TearDown()
    {
        remove("DemuxDebugOutput_TestOut.log");
    }

    vector<MatrixPtr> spectrumList_;
    MatrixPtr A_;
    MatrixPtr B_;
    MatrixPtr C_;
};


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        DemuxDebugRWTest tester;
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