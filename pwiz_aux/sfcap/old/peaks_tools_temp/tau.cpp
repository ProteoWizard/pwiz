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


#include "peaks/TruncatedLorentzian.hpp"
#include <iostream>
#include <sstream>


using namespace std;
using namespace pwiz::peaks;


const double T_ = .384;


double tauToAlpha(double tau)
{
	double c = 100000.;
	return c/tau/(1-exp(-T_/tau));
}


int main()
{
    ublas::vector<double> p(4);
    p(TruncatedLorentzian::AlphaI) = 0;
    p(TruncatedLorentzian::F0) = 0;

    TruncatedLorentzian L(T_);

	for (double tau=.8; tau<=101.; tau+= 10)
	{
		p(TruncatedLorentzian::Tau) = tau;
		p(TruncatedLorentzian::AlphaR) = tauToAlpha(tau);

		ostringstream filename;
		filename << "tau." << tau;
		L.outputSamples(filename.str(), p);

		cout << tau << " " << tauToAlpha(tau) << endl;
	}
}

