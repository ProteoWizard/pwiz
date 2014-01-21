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


#include "data/FrequencyData.hpp"
#include "peaks/TruncatedLorentzianParameters.hpp"
#include "math/Random.hpp"
#include <iostream>
#include <fstream>
#include <map>


using namespace std;
using namespace pwiz::math;
using namespace pwiz::peaks;
using namespace pwiz::data;


typedef int (*Subcommand)(const vector<string>& args);
map<string, Subcommand> subcommands_;
string usage_("Usage: tlp subcommand [args]\nSubcommands:\n");


int cat(const vector<string>& args)
{
    if (args.size() != 1) throw runtime_error(usage_);
    const string& filename = args[0];
    TruncatedLorentzianParameters tlp(filename);
    cout << tlp << endl;
    return 0;
}


int sample(const vector<string>& args)
{
    if (args.size()!=1 && args.size()!=4) throw runtime_error(usage_);

    const string& filename = args[0];
    TruncatedLorentzianParameters tlp(filename);

    cout.precision(10);

    if (args.size() == 1)
    {
        tlp.writeSamples(cout);
    }
    else if (args.size() == 4)
    {    
        double start = atof(args[1].c_str());
        double step = atof(args[2].c_str());
        int count = atoi(args[3].c_str());
        tlp.writeSamples(cout, start, step, count);
    }
    else
    {
        // this should never happen
        throw runtime_error(usage_);
    }

    return 0;
}


int sampleft(const vector<string>& args)
{
    if (args.size()<1) throw runtime_error(usage_);

    const string& filename = args[0];
    int T = args.size()>=2 ? atoi(args[1].c_str()) : 1; // multiples of .768

    TruncatedLorentzianParameters tlp(filename);

    cout.precision(10);

    double start = 1/(T*.768);
    double step = start; 
    int count = (1<<19) * T; // 2^19 * T
    tlp.writeSamples(cout, start, step, count);

    return 0;
}


int edit(const vector<string>& args)
{
    if (args.empty()) throw runtime_error(usage_);
    const string& filename = args[0];
    
    TruncatedLorentzianParameters tlp;
    try {tlp = TruncatedLorentzianParameters(filename);} catch (...) {}

    for (vector<string>::const_iterator it=args.begin()+1; it!=args.end(); ++it)
    {
        istringstream iss(*it);
        string name;
        getline(iss, name, '=');

        if (name == "T")
            iss >> tlp.T;
        else if (name == "tau")
            iss >> tlp.tau;
        else if (name == "alpha")
            iss >> tlp.alpha;
        else if (name == "f0")
            iss >> tlp.f0;
        else if (name == "amplitude")
        {
            double amplitude = 1;
            iss >> amplitude;
            tlp.alpha = polar(amplitude, arg(tlp.alpha));
        }
        else if (name == "phase")
        {
            double phase = 1;
            iss >> phase;
            tlp.alpha = polar(abs(tlp.alpha), phase);
        }
        else if (name == "height")
        {
            double height = 1;
            iss >> height;
            tlp.alpha = 1/(tlp.tau*(1-exp(-tlp.T/tlp.tau))); 
        }
        else
        {
            cout << "Ignoring unknown parameter '" << name << "'\n";
        }
    }

    cout << tlp << endl;
    tlp.write(filename);
    return 0;
}


int noisy_data(const vector<string>& args)
{
    if (args.size() != 4) throw runtime_error(usage_);
    const string& tlpFilename = args[0];
    const string& cfdFilename = args[1];
    double variance = atof(args[2].c_str());
    int sampleCount = atoi(args[3].c_str()); 

    // instantiate our TruncatedLorentzian

    TruncatedLorentzianParameters tlp(tlpFilename);
    if (tlp.T == 0)
        throw runtime_error("TruncatedLorenzianParameter T==0");

    TruncatedLorentzian L(tlp.T);
    ublas::vector<double> p = tlp.parameters();

    // create noisy frequency data 

    FrequencyData fd;
    
    double delta = 1/tlp.T;
    double fBegin = tlp.f0 - delta*(sampleCount/2);
    fBegin += Random::real(-delta/2, delta/2);
    
    for (int i=0; i<sampleCount; i++)
    {
        double f = fBegin + i*delta;
        complex<double> value = L(f, p);
        value += complex<double>(Random::gaussian(variance/2), Random::gaussian(variance/2));
        fd.data().push_back(FrequencyDatum(f,value));
    }

    // write out the data

    fd.calibrationParameters(CalibrationParameters(1.075e8, -3.455e8));
    fd.observationDuration(tlp.T);
    fd.analyze(); // recache
    cout << "Writing file " << cfdFilename << endl;
    fd.write(cfdFilename);

    return 0;
}


void initializeSubcommands()
{
    subcommands_["cat"] = cat;
    usage_ += "    cat filename.tlp\n";
    
    subcommands_["sample"] = sample;
    usage_ += "    sample filename.tlp [freqStart freqStep sampleCount]\n";
    
    subcommands_["sampleft"] = sampleft;
    usage_ += "    sampleft filename.tlp [duration (integer multiples of .768) = 1]\n";
    
    subcommands_["edit"] = edit;
    usage_ += "    edit filename.tlp [T=1] [tau=1] [f0=0] [alpha=(1,0)]\n";
    usage_ += "                      [amplitude=1] [phase=0] [height=?]\n";

    subcommands_["noisy_data"] = noisy_data;
    usage_ += "    noisy_data filename.tlp output.cfd variance sampleCount\n";
}


int main(int argc, char* argv[])
{
    try
    {
        Random::initialize();
        initializeSubcommands();
        Subcommand subcommand = argc>1 ? subcommands_[argv[1]] : 0;
        if (!subcommand) throw runtime_error(usage_);

        vector<string> args;
        copy(argv+2, argv+argc, back_inserter(args));
        
        return subcommand(args);
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

