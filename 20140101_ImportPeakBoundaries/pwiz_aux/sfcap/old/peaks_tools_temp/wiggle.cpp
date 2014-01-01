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
#include "peaks/FrequencyData.hpp"
#include <iostream>
#include <sstream>


using namespace pwiz::peaks;
using namespace std;


struct PeakInfo
{
    int index;
    const char* peak;
};


PeakInfo peakInfo_[] =
{
    1, "261",
    2, "094",
    3, "851",
    4, "37",
    5, "086",
    6, "100",
    7, "094",
    8, "084",
    9, "852",
    10, "070",
    11, "1220",
    12, "689",
    13, "1196",
    14, "597",
    15, "1118",
    16, "086",
    17, "081",
    18, "098",
    19, "2102",
    20, "1201"
};


string filename(int i, const string& extension)
{
    const PeakInfo& info = peakInfo_[i];
    ostringstream oss;
    oss << info.index << "/peak." << info.peak << extension;
    return oss.str(); 
}


double scaleFactor(const TruncatedLorentzianParameters& tlp)
{
    double A = abs(tlp.alpha);
    const double& tau = tlp.tau;
    const double& T = tlp.T;
    return A*tau*(1-exp(-T/tau));
}


int main()
{

    for (int i=0; i<20; i++)
    {
        const PeakInfo& info = peakInfo_[i];
        string tlpFilename = filename(i, ".final.tlp");
        string cfdFilename = filename(i, ".cfd");
   
        TruncatedLorentzianParameters tlp(tlpFilename);

        double scale = scaleFactor(tlp);
        double shift = tlp.f0; 

        cout.precision(10);
        cout << "# " << info.index << " " << info.peak << endl;
        cout << "# " << "scale: " << scale << endl;
        cout << "# " << "shift: " << shift << endl;
    
        FrequencyData fd(cfdFilename);
        for (FrequencyData::iterator it=fd.data().begin(); it!=fd.data().end(); ++it)
        {
            cout << it->x - shift << " 0 " << it->y.real()/scale << " " << it->y.imag()/scale << " " << abs(it->y)/scale << endl;
        }
        cout << "\n\n";
    }

    return 0;
}

