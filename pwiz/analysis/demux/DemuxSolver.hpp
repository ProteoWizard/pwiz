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

#ifndef _DEMUXSOLVER_HPP
#define _DEMUXSOLVER_HPP

#include "DemuxTypes.hpp"

namespace pwiz {
namespace analysis {
    using namespace DemuxTypes;

    /// Interface for solver that can be used for demultiplexing.
    /// This is done by solving least squares problems of the form \f[ \min \left\Vert Ax-b\right\Vert_2^2\quad \f] where
    /// A are the masks (or design matrix), b is the signal (or response), and x is the solution discovered by the solver.
    class DemuxSolver
    {
    public:

        /// Shared pointer definition
        typedef boost::shared_ptr<DemuxSolver> ptr;

        /// Constant shared pointer definition
        typedef boost::shared_ptr<const DemuxSolver> const_ptr;

        /// Perform the least squares solve
        /// @param[in] masks Design matrix describing which isolation windows are selected for each spectrum.
        /// @param[in] signal Response matrix describing the signal of each transition in each multiplexed spectrum.
        /// @param[out] solution Matrix describing the independent spectrum of each isolation window. These are the demultiplexed spectra.
        ///
        virtual void Solve(const MatrixPtr& masks, const MatrixPtr& signal, MatrixPtr& solution) const = 0;

        virtual ~DemuxSolver(){}
    };

    /// Implementation of the DemuxSolver interface as a non-negative least squares (NNLS) problem.
    /// That is, the least squares is problem is constrained such that the solution is not negative, or
    /// \f[ \min \left\Vert Ax-b\right\Vert_2^2\quad s.t.\, x\ge 0 \f]
    class NNLSSolver : public DemuxSolver
    {
    public:

        /// Constructor for non-negative least squares solver
        /// @param[in] numIters The maximum number of iterations allowed for convergence
        /// @param[in] eps Epsilon value for convergence criterion of NNLS solver
         NNLSSolver(int numIters = 50, double eps = 1e-10) : numIters_(numIters), eps_(eps)
         {}
        
        /// Implementation of DemuxSolver interface
        void Solve(const MatrixPtr& masks, const MatrixPtr& signal, MatrixPtr& solution) const override;

    private:
     
        int numIters_; ///< maximum number of iterations allowed for convergence
         
        double eps_; ///< tolerance for convergence
    };
} // namespace analysis 
} // namespace pwiz
#endif // _DEMUXSOLVER_HPP