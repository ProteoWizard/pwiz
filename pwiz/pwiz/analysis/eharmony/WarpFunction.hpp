//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// WarpFunction.hpp
///

#ifndef _WARP_FUNCTION_HPP_
#define _WARP_FUNCTION_HPP_

#include <iostream>
#include <vector>
#include "pwiz/utility/misc/Export.hpp"
#include "boost/tuple/tuple_comparison.hpp"

namespace pwiz {
namespace eharmony {

enum WarpFunctionEnum{Default, Linear, PiecewiseLinear};

class PWIZ_API_DECL WarpFunction
{

public:

  WarpFunction(const std::vector<std::pair<double,double> >& anchors);
  virtual void operator()(std::vector<double>& rt_vals, std::vector<double>& warped_rt_vals);
  virtual ~WarpFunction() {}

protected:

  std::vector<std::pair<double,double> > anchors_;

};

class PWIZ_API_DECL LinearWarpFunction : public WarpFunction
{

public:

  LinearWarpFunction(std::vector<std::pair<double,double> >& anchors);
  virtual void operator()(std::vector<double>& rt_vals, std::vector<double>& warped_rt_vals);

private:

    std::vector<double> coefficients_;

};

class PWIZ_API_DECL PiecewiseLinearWarpFunction : public WarpFunction
{

public:

  PiecewiseLinearWarpFunction(std::vector<std::pair<double,double> >& anchors);
  virtual void operator()(std::vector<double>& rt_vals, std::vector<double>& warped_rt_vals);
  
private:

    std::vector<std::pair<double,double> > coefficients_;

};

class PWIZ_API_DECL SplineWarpFunction : public WarpFunction
{

public:

  SplineWarpFunction(std::vector<std::pair<double,double> >& anchors);
  virtual void operator()(std::vector<double>& rt_vals, std::vector<double>& warped_rt_vals);
  

private:

  std::vector<boost::tuple<double,double,double,double> > coefficients_;

};

} // namespace eharmony
} // namespace pwiz

#endif //_WARP_FUNCTION_HPP_
