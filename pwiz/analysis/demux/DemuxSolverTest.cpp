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

#include "pwiz/analysis/demux/DemuxSolver.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::msdata;

class DemuxSolverTest {
public:
    void Run()
    {
        SetUp();
        NNLSSolverTest();
        TearDown();
    }

protected:

    virtual void SetUp()
    {
    }

    void TearDown()
    {
    }

    void NNLSSolverTest()
    {
        // Assume an expected solution
        vector<double> expectedSolution = {
            0.0,
            0.0,
            0.0,
            11.0,
            13.0,
            0.0,
            0.0
        };

        // Assume that the trailing precursor window that is only half represented has no spectral contribution from the unseen portion
        double trailingWindowIntensity = 0.0;

        TestNNLSGivenSolution(expectedSolution, trailingWindowIntensity);

        // Try a more dense solution
        expectedSolution = {
            5.0,
            3.0,
            2.0,
            11.0,
            13.0,
            9.0,
            3.0
        };

        TestNNLSGivenSolution(expectedSolution, trailingWindowIntensity);
    }

    void TestNNLSGivenSolution(const vector<double>& expectedSolution, double trailingWindowIntensity)
    {
        NNLSSolver solver;
        MatrixPtr signal;
        MatrixPtr masks;
        MatrixPtr solution;
        int numSpectra = 7;
        int numDemuxWindows = 7;
        int numTransitions = 1;
        signal.reset(new MatrixType(numSpectra, numTransitions));
        masks.reset(new MatrixType(numSpectra, numDemuxWindows));
        solution.reset(new MatrixType(numDemuxWindows, numTransitions));

        /*
        * Create mask matrix of the form
        * 1 1 0 0 0 0 0     \ \00000
        * 0 1 1 0 0 0 0     0\ \0000
        * 0 0 1 1 0 0 0     00\ \000
        * 0 0 0 1 1 0 0     000\ \00
        * 0 0 0 0 1 1 0     0000\ \0
        * 0 0 0 0 0 1 1     00000\ j = i + 1
        * 0 0 0 0 0 0 1     000000j = i
        *
        * This mask matrix is used in overlap demultiplexing
        */
        for (int i = 0; i < numSpectra; ++i)
        {
            for (int j = 0; j < numDemuxWindows; ++j)
            {
                if (j == i || j == i + 1)
                {
                    masks->row(i)[j] = 1.0;
                }
                else
                {
                    masks->row(i)[j] = 0.0;
                }
            }
        }

        // Create a multiplexed signal from the expected solution
        vector<double> signalVec;
        for (int i = 0; i < numSpectra; ++i)
        {
            double signalSum = expectedSolution[i];
            if (i + 1 < numSpectra)
                signalSum += expectedSolution[i + 1];
            else
                signalSum += trailingWindowIntensity;
            signalVec.push_back(signalSum);
        }
        for (size_t i = 0; i < signalVec.size(); ++i)
        {
            signal->row(i)[0] = signalVec[i];
        }

        solver.Solve(masks, signal, solution);

        // Verify result
        for (size_t i = 0; i < expectedSolution.size(); ++i)
        {
            unit_assert_equal(expectedSolution[i], solution->row(i)[0], 0.0001);
        }
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        DemuxSolverTest tester;
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