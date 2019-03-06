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

#ifndef _CUBICHERMITESPLINE_HPP
#define _CUBICHERMITESPLINE_HPP

#include "IInterpolation.hpp"
#include <CSpline.h>
#include <Eigen>
#include <vector>

namespace pwiz
{
namespace analysis
{
    /// An implementation of the IInterpolation interface that acts as a wrapper for a cSpline.
    class CubicHermiteSpline : public IInterpolation
    {
    public:

        /// Constructs a CubicHermiteSpline using standard vectors
        /// @param points The independent values as a monotonically increasing series
        /// @param values The dependent values
        /// \pre points must be sorted in order of increasing value with no duplicates
        /// \pre points and values must be of the same size
        CubicHermiteSpline(const std::vector<double>& points, const std::vector<double>& values);

        /// Constructs a CubicHermiteSpline using Eigen vectors
        /// @param points The independent values as a monotonically increasing series
        /// @param values The dependent values
        /// \pre points must be sorted in order of increasing value with no duplicates
        /// \pre points and values must be of the same size
        CubicHermiteSpline(const Eigen::VectorXd& points, const Eigen::VectorXd& values);

        virtual ~CubicHermiteSpline();

        /// \name IInterpolation interface
        ///@{

        bool IsDifferentiable() override;
        bool IsIntegrable() override;
        double Differentiate(double x) override;
        double Integrate(double a, double b) override;
        double Interpolate(double x) override;
        ///@}
    private:

        /// Checks for errors and throws exception if cSpline initialization resulted in an error
        void CheckForError() const;

        /// The class containing the algorithm for constructing splines and retrieving interpolated values
        cSpline* cSpline_;
    };
} // namespace analysis
} // namespace pwiz
#endif // _CUBICHERMITESPLINE_HPP