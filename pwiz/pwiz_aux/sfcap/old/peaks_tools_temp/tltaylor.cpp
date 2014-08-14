//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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


#include "peaks/TruncatedLorentzianParameters.hpp"
#include "peaks/TruncatedLorentzianEstimator.hpp"
#include "extmath/Stats.hpp"
#include "extmath/Random.hpp"
#include <iostream>
#include <fstream>
#include <vector>
#include <iomanip>


using namespace std;
using namespace pwiz::peaks;
using namespace pwiz::extmath;
namespace ublas = boost::numeric::ublas;


class TaylorApproximation
{
    public:
   
    TaylorApproximation(const TruncatedLorentzian& L, const ublas::vector<double>& p, double f);
    complex<double> operator()(double f0);

    private: 
    double f0_;
    complex<double> z_; 
    complex<double> dz_; 
    complex<double> d2z_; 
};


TaylorApproximation::TaylorApproximation(const TruncatedLorentzian& L, const ublas::vector<double>& p, double f)
:   f0_(p(TruncatedLorentzian::F0)) 
{
    z_ = L(f,p);
    dz_ = L.dp(f, p)(TruncatedLorentzian::F0); 
    d2z_ = L.dp2(f, p)(TruncatedLorentzian::F0, TruncatedLorentzian::F0); 
}


complex<double> TaylorApproximation::operator()(double f0)
{
    double delta = f0 - f0_; 
    return z_ + dz_*delta + .5*d2z_*delta*delta;
}


void test(const TruncatedLorentzianParameters& tlp)
{
    cout << tlp << endl;
    TruncatedLorentzian L(tlp.T);

    for (double f=tlp.f0-1; f<tlp.f0+1; f+=.2)
    {
        cout << "f: " << f << endl;
        TaylorApproximation ta(L, tlp.parameters(), f);

        for (double shift=1.; shift>1e-7; shift/=10.)
        {
            TruncatedLorentzianParameters tlp_shifted(tlp);
            tlp_shifted.f0 += shift; 

            cout << setw(7) << shift << " " << L(f, tlp_shifted.parameters()) << " " << ta(tlp_shifted.f0) << endl;
        }
        
        cout << endl;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc < 2)
        {
            cout << "Usage: tltaylor model.tlp\n"; 
            return 1; 
        }

        const string& filename = argv[1];
        TruncatedLorentzianParameters tlp(filename);
        test(tlp);
        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

