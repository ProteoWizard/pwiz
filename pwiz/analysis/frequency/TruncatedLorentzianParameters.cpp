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

#include "TruncatedLorentzianParameters.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


namespace pwiz {
namespace frequency {
    

PWIZ_API_DECL TruncatedLorentzianParameters::TruncatedLorentzianParameters()
:   T(1), tau(1), alpha(1), f0(0)
{}


PWIZ_API_DECL TruncatedLorentzianParameters::TruncatedLorentzianParameters(const TruncatedLorentzianParameters& that)
:   T(that.T), tau(that.tau), alpha(that.alpha), f0(that.f0)
{}


namespace {

#pragma pack(1)
struct BinaryFormat
{
    char magic[4]; // "PCC\0"
    char type[4];  // "TLP\0" (Truncated Lorentzian Parameters) 
    int version;
    int reserved;    

    double T;
    double tau;
    double alphaReal;
    double alphaImag;
    double f0;

    BinaryFormat()
    :   version(2), // increment here if format changes
        reserved(0),
        T(0),
        tau(0),
        alphaReal(0),
        alphaImag(0),
        f0(0)
    {
        strcpy(magic, "PCC");
        strcpy(type, "TLP");
    }
};
#pragma pack()

} // namespace


PWIZ_API_DECL TruncatedLorentzianParameters::TruncatedLorentzianParameters(const string& filename)
{
    BinaryFormat good;
    BinaryFormat bf;

    ifstream is(filename.c_str(), ios::binary);
    if (!is)
        throw runtime_error("[TruncatedLorentzianParameters] Unable to open file " + filename);
    is.read((char*)&bf, sizeof(bf));

    if (strncmp(bf.magic, good.magic, sizeof(good.magic)) ||
        strncmp(bf.type, good.type, sizeof(good.type)) ||
        bf.version != good.version)
        throw runtime_error("[TruncatedLorentzianParameters] Bad header in file " + filename);
        
    T = bf.T;
    tau = bf.tau;
    alpha = complex<double>(bf.alphaReal, bf.alphaImag);
    f0 = bf.f0;
}


PWIZ_API_DECL void TruncatedLorentzianParameters::write(const string& filename) const
{
    ofstream os(filename.c_str(), ios::binary);
    if (!os)
        throw runtime_error("[TruncatedLorentzianParameters] Unable to open file " + filename);
 
    BinaryFormat bf;
    bf.T = T;
    bf.tau = tau;
    bf.alphaReal = alpha.real();
    bf.alphaImag = alpha.imag();
    bf.f0 = f0;

    os.write((const char*)&bf, sizeof(bf));
}


PWIZ_API_DECL void TruncatedLorentzianParameters::writeSamples(std::ostream& os) const
{
    os.precision(10);
    double fwhm = sqrt(T*T+tau*tau)/(T*tau);
    TruncatedLorentzian L(T);
    ublas::vector<double> p = parameters();
    for (double f=f0-5*fwhm; f<f0+5*fwhm; f+=.01*fwhm)
    {
        complex<double> value = L(f, p);
        os << f << " 0 " << value.real() << ' ' << value.imag() << ' ' << abs(value) << endl;
    }
}


PWIZ_API_DECL
void TruncatedLorentzianParameters::writeSamples(std::ostream& os, 
                                                 double frequencyStart, 
                                                 double frequencyStep, 
                                                 int sampleCount) const
{
    TruncatedLorentzian L(T);
    ublas::vector<double> p = parameters();
    for (int i=0; i<sampleCount; i++)
    {
        double f = frequencyStart + i*frequencyStep;
        complex<double> value = L(f, p);
        os << f << " 0 " << value.real() << ' ' << value.imag() << ' ' << abs(value) << endl;
    }
}


ublas::vector<double> TruncatedLorentzianParameters::parameters(double shift, complex<double> scale) const
{
    ublas::vector<double> p(4);
    complex<double> alpha_scaled = alpha*scale;
    p(TruncatedLorentzian::AlphaR) = alpha_scaled.real();
    p(TruncatedLorentzian::AlphaI) = alpha_scaled.imag();
    p(TruncatedLorentzian::Tau) = tau;
    p(TruncatedLorentzian::F0) = f0 + shift;
    return p;
}


PWIZ_API_DECL
void TruncatedLorentzianParameters::parameters(const ublas::vector<double>& value, 
                                               double shift, 
                                               complex<double> scale) 
{
    alpha = complex<double>(value(TruncatedLorentzian::AlphaR), value(TruncatedLorentzian::AlphaI)) * scale;
    tau = value(TruncatedLorentzian::Tau);
    f0 = value(TruncatedLorentzian::F0) + shift;
}


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const TruncatedLorentzianParameters& tlp)
{
    os << setprecision(12) 
        << "T=" << tlp.T 
        << " tau=" << tlp.tau 
        << " alpha=" << tlp.alpha 
        << " f0=" << tlp.f0
        << " amplitude=" << abs(tlp.alpha)
        << " phase=" << arg(tlp.alpha);

    return os;
}


PWIZ_API_DECL bool operator==(const TruncatedLorentzianParameters& t, const TruncatedLorentzianParameters& u)
{
    return (t.T == u.T &&
            t.tau == u.tau &&
            t.alpha == u.alpha &&
            t.f0 == u.f0);
}


PWIZ_API_DECL bool operator!=(const TruncatedLorentzianParameters& t, const TruncatedLorentzianParameters& u)
{
    return !(t==u);
}


} // namespace frequency
} // namespace pwiz

