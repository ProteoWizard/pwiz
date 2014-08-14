//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SAVITZKYGOLAYSMOOTHER_HPP_ 
#define _SAVITZKYGOLAYSMOOTHER_HPP_


#include "Smoother.hpp"
#include <vector>
#include <boost/shared_ptr.hpp>


namespace pwiz {
namespace analysis {


struct PWIZ_API_DECL SavitzkyGolaySmoother : public Smoother
{
    SavitzkyGolaySmoother(int polynomialOrder, int windowSize);
    ~SavitzkyGolaySmoother();

    /// smooth y values to existing vectors using Savitzky-Golay algorithm;
    /// preconditions:
    /// - samples within the window must be (approximately) equally spaced
    virtual void smooth(const std::vector<double>& x, const std::vector<double>& y,
                        std::vector<double>& xSmoothed, std::vector<double>& ySmoothed);

    /// smooth y values and copy back to the input vectors using Savitzky-Golay algorithm;
    /// preconditions:
    /// - samples within the window must be (approximately) equally spaced
    virtual void smooth_copy(std::vector<double>& x, std::vector<double>& y);

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
};


} // namespace analysis
} // namespace pwiz

#endif // _SAVITZKYGOLAYSMOOTHER_HPP_
