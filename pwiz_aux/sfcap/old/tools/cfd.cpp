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
#include <map>
#include <stdexcept>


using namespace std;
using namespace pwiz::peaks;
using namespace pwiz::data;
using namespace pwiz::math;


typedef int (*Subcommand)(const vector<string>& args);
map<string, Subcommand> subcommands_;
string usage_("Usage: cfd subcommand [args]\nSubcommands:\n");


int cat(const vector<string>& args)
{
    if (args.size() != 1) throw runtime_error(usage_);
    const string& filename = args[0];
    
    cout.precision(14);

    const FrequencyData fd(filename);
    fd.write(cout, FrequencyData::Text);

    return 0;
}


int cathead(const vector<string>& args)
{
    if (args.size() != 1) throw runtime_error(usage_);
    const string& filename = args[0];

    const FrequencyData fd(filename);
    cout << "Scan number: " << fd.scanNumber() << endl;
    cout << "Retention time: " << fd.retentionTime() << endl;
    cout << "Calibration parameters: " << fd.calibrationParameters() << endl;
    cout << "Observation duration: " << fd.observationDuration() << endl;
    cout << "Noise floor: " << fd.noiseFloor() << endl;

    return 0;
}


int create(const vector<string>& args)
{
    if (args.size() != 2) throw runtime_error(usage_);
    const string& inputFilename = args[0];
    const string& outputFilename = args[1];

    const FrequencyData fd(inputFilename);
    fd.write(outputFilename);

    return 0;
}


int edit(const vector<string>& args)
{
    if (args.size() < 1) throw runtime_error(usage_);
    const string& filename = args[0];

    FrequencyData fd(filename);

    for (vector<string>::const_iterator it=args.begin()+1; it!=args.end(); ++it)
    {
        istringstream iss(*it);
        string name;
        getline(iss, name, '=');
        double value = 0;
        iss >> value;

        if (name == "A")
        {
            CalibrationParameters temp = fd.calibrationParameters();
            temp.A = value;
            fd.calibrationParameters(temp);
        }
        else if (name == "B")
        {
            CalibrationParameters temp = fd.calibrationParameters();
            temp.B = value;
            fd.calibrationParameters(temp);
        }
        else if (name == "T")
        {
            fd.observationDuration(value);
        }
        else if (name == "NF")
        {
            fd.noiseFloor(value);
        }
        else
        {
            cout << "Ignoring unknown parameter '" << name << "'\n";
        }
    }

    cout << "Calibration parameters: " << fd.calibrationParameters() << endl;
    cout << "Observation duration: " << fd.observationDuration() << endl;
    cout << "Noise floor: " << fd.noiseFloor() << endl;

    fd.write(filename);

    return 0;
}


int window(const vector<string>& args)
{
    if (args.size() != 4) throw runtime_error(usage_);
    const string& inputFilename = args[0];
    const string& outputFilename = args[1];
    double frequency = atof(args[2].c_str());
    int radius = atoi(args[3].c_str());

    const FrequencyData fd(inputFilename);
    FrequencyData::const_iterator center = fd.findNearest(frequency);

    const FrequencyData window(fd, center, radius);  
    window.write(outputFilename);

    return 0;
}


int normalize(const vector<string>& args)
{
    if (args.size() != 3) throw runtime_error(usage_);
    const string& inputFilename = args[0];
    const string& outputFilename = args[1];
    const string& tlpFilename = args[2];

    TruncatedLorentzianParameters tlp(tlpFilename);
    
    double shift = tlp.f0;

    const double& T = tlp.T;
    const double& t = tlp.tau;
    const complex<double>& a = tlp.alpha;
     
    complex<double> scale = a*t*(1-exp(-T/t));

    cout << "shift: " << shift << endl;;
    cout << "scale: " << scale << endl;;

    FrequencyData fd(inputFilename);
    fd.transform(-shift, 1./scale);
    fd.write(outputFilename);

    return 0;
}


int transform(const vector<string>& args)
{
    if (args.size() != 4) throw runtime_error(usage_);
    const string& inputFilename = args[0];
    const string& outputFilename = args[1];
    double shift = atof(args[2].c_str());

    complex<double> scale;
    istringstream iss(args[3]);
    iss >> scale;

    cout << "shift: " << shift << endl;;
    cout << "scale: " << scale << endl;;

    FrequencyData fd(inputFilename);
    fd.transform(shift, scale);
    fd.write(outputFilename);

    return 0;
}


int add(const vector<string>& args)
{
    if (args.size() != 3) throw runtime_error(usage_);
    const string& inputFilename1 = args[0];
    const string& inputFilename2 = args[1];
    const string& outputFilename = args[2];

    FrequencyData fd1(inputFilename1);
    const FrequencyData fd2(inputFilename2);

    fd1 += fd2;
    fd1.write(outputFilename);

    return 0;
}


int noise(const vector<string>& args)
{
    if (args.size() != 2) throw runtime_error(usage_);
    const string& inputFilename = args[0];
    const string& outputFilename = args[1];

    const FrequencyData in(inputFilename);
    FrequencyData out;

    FrequencyData::container& data = out.data();
    const double sd = 1/sqrt(2.);

    Random::initialize();

    for (FrequencyData::const_iterator it=in.data().begin(); it!=in.data().end(); ++it)
    {
        complex<double> noise(Random::gaussian(sd), Random::gaussian(sd));
        data.push_back(FrequencyDatum(it->x, noise));
    }

    out.analyze();
    out.write(outputFilename);

    return 0;
}


void initializeSubcommands()
{
    subcommands_["cat"] = cat;
    usage_ += "    cat filename.cfd\n";
    
    subcommands_["cathead"] = cathead;
    usage_ += "    cathead filename.cfd\n";
    
    subcommands_["create"] = create;
    usage_ += "    create input.txt output.cfd\n";

    subcommands_["edit"] = edit;
    usage_ += "    edit filename.cfd [A=?] [B=?] [T=?] [NF=?]\n";
    usage_ += "        A,B : calibration parameters\n";
    usage_ += "        T   : observation duration\n";
    usage_ += "        NF  : noise floor\n";
    
    subcommands_["window"] = window;
    usage_ += "    window input.cfd output.cfd frequency radius\n";

    subcommands_["normalize"] = normalize;
    usage_ += "    normalize input.cfd output.cfd model.tlp\n";

    subcommands_["transform"] = transform;
    usage_ += "    transform input.cfd output.cfd shift scale\n";

    subcommands_["add"] = add;
    usage_ += "    add input1.cfd input2.cfd output.cfd\n";

    subcommands_["noise"] = noise;
    usage_ += "    noise template.cfd output.cfd\n";
}


int main(int argc, char* argv[])
{
    try
    {
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

