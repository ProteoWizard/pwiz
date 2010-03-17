//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#define PWIZ_SOURCE

#include "TruncatedLorentzian.hpp"
#include <fstream>


#define i_ (complex<double>(0,1))
#define pi_ M_PI


namespace pwiz {
namespace frequency {


using namespace std;


struct TruncatedLorentzian::Impl
{
    public:

    Impl(double T)
    :   T_(T),
        cacheLevel_(-1),
        f_(0), alpha_(0,0), tau_(0), f0_(0), x_(0), L_(0),
        dLdx_(0), dxdt_(0), dxdf_(0), dLdt_(0), dLdf_(0),
        d2Ldx2_(0), d2xdt2_(0), d2Ldt2_(0), d2Ldf2_(0), d2Ldtdf_(0)
    {}

    complex<double> value(double f, const ublas::vector<double>& p);

    void d1(double f,
            const ublas::vector<double>& p,
            ublas::vector< complex<double> >& result);

    void d2(double f,
            const ublas::vector<double>& p,
            ublas::matrix< complex<double> >& result);

    double fwhm(const ublas::vector<double>& p) const;


    private:

    double T_;
    int cacheLevel_; // (-1 == invalid)

    // function values (valid if cacheLevel >= 0)
    double f_;
    complex<double> alpha_;
    double tau_;
    double f0_;
    complex<double> x_;
    complex<double> L_;

    // first derivatives (valid if cacheLevel >= 1)
    complex<double> dLdx_;
    complex<double> dxdt_;
    complex<double> dxdf_;
    complex<double> dLdt_;
    complex<double> dLdf_;

    // second derivatives (valid if cacheLevel >= 2)
    complex<double> d2Ldx2_;
    complex<double> d2xdt2_;
    complex<double> d2Ldt2_;
    complex<double> d2Ldf2_;
    complex<double> d2Ldtdf_;

    void calculate(double f, const ublas::vector<double>& p, int cacheLevel);
};


complex<double> TruncatedLorentzian::Impl::value(double f, const ublas::vector<double>& p)
{
    calculate(f, p, 0);
    return alpha_ * L_;
}


void TruncatedLorentzian::Impl::d1(double f,
                                   const ublas::vector<double>& p,
                                   ublas::vector< complex<double> >& result)
{
    calculate(f, p, 1);
    result.resize(4);
    result.clear();

    result(0) = L_;
    result(1) = i_ * L_;
    result(2) = alpha_ * dLdt_;
    result(3) = alpha_ * dLdf_;
}


void TruncatedLorentzian::Impl::d2(double f,
                                   const ublas::vector<double>& p,
                                   ublas::matrix< complex<double> >& result)
{
    calculate(f, p, 2);
    result.resize(4,4);
    result.clear();

    result(0,0) = result(0,1) = result(1,0) = result(1,1) = 0;
    result(0,2) = result(2,0) = dLdt_;
    result(0,3) = result(3,0) = dLdf_;
    result(1,2) = result(2,1) = i_ * dLdt_;
    result(1,3) = result(3,1) = i_ * dLdf_;
    result(2,2) = alpha_ * d2Ldt2_;
    result(2,3) = result(3,2) = alpha_ * d2Ldtdf_;
    result(3,3) = alpha_ * d2Ldf2_;
}


double TruncatedLorentzian::Impl::fwhm(const ublas::vector<double>& p) const
{
    return sqrt(T_*T_+p(Tau)*p(Tau))/(T_*p(Tau));
}


void TruncatedLorentzian::Impl::calculate(double f, const ublas::vector<double>& p, int cacheLevel)
{
    // cache with key <f,p>
    if (f != f_ ||
        p(AlphaR) != alpha_.real() ||
        p(AlphaI) != alpha_.imag() ||
        p(Tau) != tau_ ||
        p(F0) != f0_)
    {
        // recache
        *this = Impl(T_); // zero out everything except T_
        f_ = f;
        alpha_ = complex<double>(p(AlphaR), p(AlphaI));
        tau_ = p(Tau);
        f0_ = p(F0);
    }
    else
    {
        // cache hit
        //cout << "cache hit!\n";
    }

    if (cacheLevel>=0 && cacheLevel_<0)
    {
        x_ = 1/tau_ + 2*pi_*i_*(f_-f0_);
        L_ = (1.-exp(-x_*T_))/x_;
        cacheLevel_ = 0;
    }

    if (cacheLevel>=1 && cacheLevel_<1)
    {
        dLdx_ = ((T_*x_+1.)*exp(-x_*T_) - 1.) / (x_*x_);
        dxdt_ = -1/(tau_*tau_);
        dxdf_ = -2*pi_*i_;
        dLdt_ = dLdx_ * dxdt_;
        dLdf_ = dLdx_ * dxdf_;
        cacheLevel_ = 1;
    }

    if (cacheLevel>=2 && cacheLevel_<2)
    {
        d2Ldx2_ = (2. - (pow(T_*x_+1.,2)+1.)*exp(-x_*T_)) / pow(x_,3);
        d2xdt2_ = 2/pow(tau_,3);
        d2Ldt2_ = d2Ldx2_*pow(dxdt_,2) + dLdx_*d2xdt2_;
        d2Ldf2_ = d2Ldx2_*pow(dxdf_,2);
        d2Ldtdf_ = d2Ldx2_ * dxdt_ * dxdf_;
        cacheLevel_ = 2;
    }
}


PWIZ_API_DECL TruncatedLorentzian::TruncatedLorentzian(double T)
:   impl_(new Impl(T))
{}


PWIZ_API_DECL TruncatedLorentzian::~TruncatedLorentzian()
{} // this must be here to delete Impl properly


PWIZ_API_DECL complex<double> TruncatedLorentzian::operator()(double f, const ublas::vector<double>& p) const
{
    return impl_->value(f, p);
}


PWIZ_API_DECL ublas::vector< complex<double> > TruncatedLorentzian::dp(double f, const ublas::vector<double>& p) const
{
    ublas::vector< complex<double> > result;
    impl_->d1(f, p, result);
    return result;
}


PWIZ_API_DECL ublas::matrix< complex<double> > TruncatedLorentzian::dp2(double f, const ublas::vector<double>& p) const
{
    ublas::matrix< complex<double> > result;
    impl_->d2(f, p, result);
    return result;
}


PWIZ_API_DECL void TruncatedLorentzian::outputSamples(const string& filename, const ublas::vector<double>& p, double shift, double scale) const
{
    cout << "[TruncatedLorentzian] Writing file " << filename << endl;
    ofstream os(filename.c_str());
	if (!os)
	{
		cout << "[TruncatedLorentzian::outputSamples()] Unable to write to file " << filename << endl;
		return;
	}

    os.precision(8);

    double fwhm = impl_->fwhm(p);

    for (double f=p(F0)-5*fwhm; f<p(F0)+5*fwhm; f+=.01*fwhm)
    {
        complex<double> value = impl_->value(f, p);
        os << f+shift << " 0 " << value.real()*scale << ' ' << value.imag()*scale << ' ' << sqrt(norm(value))*scale << endl;
    }
}


} // namespace frequency
} // namespace pwiz

