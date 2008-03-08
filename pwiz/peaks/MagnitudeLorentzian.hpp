//
// MagnitudeLorentzian.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _MAGNITUDELORENTZIAN_HPP_
#define _MAGNITUDELORENTZIAN_HPP_


#include <vector>


namespace pwiz {
namespace peaks {


class MagnitudeLorentzian
{
    public:

    // m(x) == 1/sqrt(ax^2 + bx + c)
    //      == alpha/sqrt(1/tau^2 + [2pi(x-center)]^2)

    MagnitudeLorentzian(double a, double b, double c);
    MagnitudeLorentzian(std::vector<double> a);
    MagnitudeLorentzian(const std::vector< std::pair<double,double> >& samples);

    double leastSquaresError() const;

    std::vector<double>& coefficients();
    const std::vector<double>& coefficients() const;

    double operator()(double x) const;
    double center() const;
    double tau() const;
    double alpha() const;

    private:
    std::vector<double> a_;
    double leastSquaresError_;
};


} // namespace peaks
} // namespace pwiz


#endif // _MAGNITUDELORENTZIAN_HPP_
