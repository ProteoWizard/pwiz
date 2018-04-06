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


#include "TransientGenerator.hpp"
#include "pwiz/utility/proteome/Peptide.hpp"
#include "boost/program_options.hpp"
#include <iostream>
#include <fstream>
#include <iterator>
#include <sstream>
#include <complex>


using namespace pwiz::id;
using namespace pwiz::id::model;
using namespace pwiz::data;
using namespace pwiz;
using namespace std;


struct Config
{
    string inputFilename;
    string outputFilename;
    double isotopeCalculatorAbundanceCutoff;
    double isotopeCalculatorMassPrecision;
    double phaseFunctionTimeDelay;

    Config()
    :   isotopeCalculatorAbundanceCutoff(.01),
        isotopeCalculatorMassPrecision(.01),
        phaseFunctionTimeDelay(0)
    {}
};


ostream& operator<<(ostream& os, const Config& config)
{
    os << "inputFilename: " << config.inputFilename << endl;
    os << "outputFilename: " << config.outputFilename << endl;
    os << "isotopeCalculatorAbundanceCutoff: " << config.isotopeCalculatorAbundanceCutoff << endl;
    os << "isotopeCalculatorMassPrecision: " << config.isotopeCalculatorMassPrecision << endl;
    os << "phaseFunctionTimeDelay: " << config.phaseFunctionTimeDelay << endl;
    return os;
}


void writeTransient(const vector<Species>& species, const Config& config)
{
    if (species.empty())
        throw runtime_error("No species specified.");

    proteome::IsotopeCalculator calculator(config.isotopeCalculatorAbundanceCutoff, 
                                           config.isotopeCalculatorMassPrecision);

    for (vector<Species>::const_iterator it=species.begin(); it!=species.end(); ++it)
    {
        cout << "species: " << *it << endl; 
        cout << calculator.distribution(it->formula, 0);
        cout << endl;
    }

    TransientGenerator tg(calculator);

    InstrumentConfiguration ic;
    const double A_ = 1.075339687500000e+008;
    const double B_ = -3.454602661132810e+008;
    ic.calibrationParameters = CalibrationParameters(A_,B_);
    ic.observationDuration = .768; 
    ic.sampleCount = 1048576;
    LinearPhaseFunction pf(config.phaseFunctionTimeDelay);
    ic.phaseFunction = &pf;
    ConstantDecayFunction df(ic.observationDuration);
    ic.decayFunction = &df;

    ChromatographicFraction cf;
    cf.instrumentConfiguration = ic;
    cf.species = species;

    auto_ptr<TransientData> td = tg.createTransientData(cf); 
    td->write(config.outputFilename);
}


struct Input
{
    enum Type {Unknown, Peptide, Formula};
    Type type;
    string text;
    ChargeDistribution chargeDistribution;

    Input() : type(Unknown) {}

    void clear() {type = Unknown; text.clear(); chargeDistribution.clear();}
    Species species() const; 
};


Species Input::species() const
{
    Species result;
    
    switch (type)
    {
        case Peptide:
        {
            proteome::Peptide peptide(text);
            result.formula = peptide.formula();
            break;
        }
        case Formula:
        {    
            result.formula = text;
            break;
        }
        case Unknown:
        default:
        {
            break;
        }
    }

    result.chargeDistribution = chargeDistribution;
    
    return result;
}


istream& operator>>(istream& is, Input& input)
{
    input.clear();

    string buffer;
    getline(is, buffer);

    istringstream iss(buffer);

    string type;
    iss >> type >> input.text;
    if (type == "peptide")
        input.type = Input::Peptide;
    else if (type == "formula")
        input.type = Input::Formula;
    else
        input.type = Input::Unknown;

    copy(istream_iterator<ChargeAbundance>(iss), 
         istream_iterator<ChargeAbundance>(),
         back_inserter(input.chargeDistribution));

    return is;
}


ostream& operator<<(ostream& os, const Input& input)
{
    switch (input.type)
    {
        case Input::Peptide:
            os << "peptide ";
            break;
        case Input::Formula:
            os << "formula ";
            break;
        case Input::Unknown:
        default:
            os << "unknown ";
            break;
    }
    os << input.text << " ";
    
    copy(input.chargeDistribution.begin(), input.chargeDistribution.end(), 
         ostream_iterator<ChargeAbundance>(cout, " "));

    return os;
}


vector<Species> parseInputFile(const string& inputFilename)
{
    cout << "Reading input from file: " << inputFilename << "\n\n";
    ifstream is(inputFilename.c_str());

    vector<Input> inputs;
    copy(istream_iterator<Input>(is), istream_iterator<Input>(), back_inserter(inputs));

    cout << "Input:\n";
    copy(inputs.begin(), inputs.end(), ostream_iterator<Input>(cout, "\n"));
    cout << endl;

    vector<Species> species;
    for (vector<Input>::const_iterator it=inputs.begin(); it!=inputs.end(); ++it)
        if (it->type != Input::Unknown)
            species.push_back(it->species());

    return species;
}


Config parseCommandLine(int argc, char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: transgen inputfile output.dat\n"
          << endl 
          << "Input file format:\n"
          << "  peptide|formula string (charge,abundance)+\n"
          << endl
          << "Input file examples:\n"
          << "  peptide DARREN (1,1) (2,1) (3,1)\n"
          << "  formula H2O1 (1,1)\n"
          << "  formula C6H12O6\n"
          << endl;

    Config config;

    po::options_description od_config("Options");
    od_config.add_options()
        ("cutoff,c",
            po::value<double>(&config.isotopeCalculatorAbundanceCutoff)->
                default_value(config.isotopeCalculatorAbundanceCutoff),
            ": isotope calculator abundance cutoff")
        ("precision,p",
            po::value<double>(&config.isotopeCalculatorMassPrecision)->
                default_value(config.isotopeCalculatorMassPrecision),
            ": isotope calculator mass precision")
        ("timedelay,t",
            po::value<double>(&config.phaseFunctionTimeDelay)->
                default_value(config.phaseFunctionTimeDelay),
            ": time delay for phase calculation")
        ;

    // append options description to usage string

    usage << od_config;

    // handle positional arguments

    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);
   
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args);

    // parse command line

    po::variables_map vm;
    po::store(po::command_line_parser(argc, (char**)argv).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // remember filenames from command line

    vector<string> filenames;
    if (vm.count(label_args))
        filenames = vm[label_args].as< vector<string> >();
        
    if (filenames.size() != 2)
        throw runtime_error(usage.str());

    config.inputFilename = filenames[0];
    config.outputFilename = filenames[1];

    return config;
}


int main(int argc, char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);
        cout << config << endl;
        vector<Species> species = parseInputFile(config.inputFilename);
        writeTransient(species, config);
        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

