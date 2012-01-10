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


#include <iostream>
#include <fstream>
#include <sstream>
#include <vector>
#include <iterator>
#include <stdexcept>
#include <cmath>
#include <iomanip>


using namespace std;


struct MZZ
{
    double mz;
    double z;
};


MZZ mzz_[] = {

    // Angiotensin II
    1046.5418, 1,
    523.7745, 2, 
    349.5188, 3,
    262.3909, 4,

    // Bombessin
    1619.8223, 1,
    810.4148, 2,
    540.6123, 3,
    405.7110, 4,

    // Substance P
    1347.7354, 1,
    674.3713, 2,
    449.9167, 3,

    // Neurotensin
    1672.9170, 1,
    836.9621, 2,
    558.3105, 3,
    418.9847, 4,
    335.3892, 5,

    // Alpha1-6
    882.4621, 1,
    441.7347, 2,
    294.8255, 3,
};


const int mzzSize_ = sizeof(mzz_)/sizeof(MZZ);


vector<MZZ> mzz_sorted_;


bool hasSmallerMZ(const MZZ& a, const MZZ& b)
{
    return a.mz < b.mz;
}


void initializeMZZ()
{
    copy(mzz_, mzz_+mzzSize_, back_inserter(mzz_sorted_));
    sort(mzz_sorted_.begin(), mzz_sorted_.end(), hasSmallerMZ);
}


class IsAlmostEqual
{
    public:
    IsAlmostEqual(const MZZ& value) : value_(value) {}
    bool operator()(const MZZ& arg) {return (fabs(arg.mz-value_.mz) < .01);}

    private:
    MZZ value_;
};


MZZ match(double mass)
{
    MZZ dummy;
    dummy.mz = mass;
    dummy.z = 1;

    vector<MZZ>::iterator it = find_if(mzz_sorted_.begin(), 
                                          mzz_sorted_.end(), 
                                          IsAlmostEqual(dummy));
    
    MZZ zero;
    zero.mz = 0;
    zero.z = 0;
    return (it != mzz_sorted_.end()) ? *it : zero;
}


void filter(const string& filename, ostream& os)
{
    ifstream is(filename.c_str());
    if (!is) throw runtime_error(("Unable to open file " + filename).c_str());

    while (is)
    {
        string buffer;
        getline(is, buffer);
        if (!is) break;
        
        if (buffer.empty() || buffer[0] == '#')
        {
            os << buffer << endl;
            continue;
        }

        istringstream iss(buffer);
        double f = 0;
        int z = 0;
        double mz = 0;
        double m = 0;
        
        iss >> f >> z >> mz >> m;
        MZZ mzz_true = match(mz);
        if (mzz_true.mz)
        {
            double relativeDeviation = (mz - mzz_true.mz)/mzz_true.mz;
            os << 
                f << " " <<
                mzz_true.z << " " <<
                mz << " " << 
                m << " " << 
                relativeDeviation << endl;
        }
    }
}


int main(int argc, char* argv[])
{
    try
    {
        cout.precision(12);

        if (argc < 2)
        {
            cout << "Usage: 5pepfilt calibratorLog.txt\n";
            cout << "Parameters:\n";
            cout << "  calibratorLog.txt:  4-column data (f z m/z m)\n";
            return 1; 
        }

        const string& filename = argv[1];
   
        initializeMZZ();
        filter(filename, cout); 

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

