///
/// WarpFunction.hpp
///

#ifndef _WARP_FUNCTION_HPP_
#define _WARP_FUNCTION_HPP_

#include <iostream>
#include <vector>
#include "pwiz/utility/misc/Export.hpp"


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
    //  std::vector<double> coefficients_;

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


} // namespace eharmony
} // namespace pwiz

#endif //_WARP_FUNCTION_HPP_
