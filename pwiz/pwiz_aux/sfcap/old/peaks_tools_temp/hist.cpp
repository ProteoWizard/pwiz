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


#include <iostream>
#include <vector>
#include <fstream>


using namespace std;


int main()
{
    vector<double> errors;

    ifstream is("errors");
    while (is)
    {
        double temp = 0;
        is >> temp >> temp >> temp;
        if (is) errors.push_back(temp);
    }

    const double binsize = .01;
    const double rangemax = 2;
    const int bincount = int(rangemax/binsize);  

    vector<int> bins(bincount);

    for (int i=0; i<errors.size(); i++)
    {
        double error = errors[i];
        int index = int(error/binsize);
        bins[index]++;
    }        

    for (int i=0; i<bins.size(); i++)
        cout << i << " " << bins[i] << endl;

    return 0;
}



