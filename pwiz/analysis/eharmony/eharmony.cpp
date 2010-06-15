//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

#include "PeptideMatcher.hpp"
#include "Feature2PeptideMatcher.hpp"
#include "Exporter.hpp"
#include "Matrix.hpp"
#include "NeighborJoiner.hpp"
#include "WarpFunction.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "boost/filesystem.hpp"
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/tuple/tuple_comparison.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::minimxml;
using namespace pwiz::cv;
using namespace pwiz::eharmony;
using namespace pwiz::proteome;
using namespace pwiz::eharmony;
using namespace pwiz::data::pepxml;

namespace{

WarpFunctionEnum translateWarpFunction(const string& wfe_string)
{

    const char* linear = "linear";
    const char* piecewiseLinear = "piecewiseLinear";
    const char* curr = wfe_string.c_str();
    if (!strncmp(linear, curr, 6))
        {
            cout << "Translated: linear" << endl;
            return Linear;

        }

    if (!strncmp(piecewiseLinear, curr, 15))
        {
            cout << "Translated: piecewiseLinear" << endl;
            return PiecewiseLinear;

        }


    cout << "Translated: default" << endl;
    return Default;

}


DistanceAttributeEnum translateDistanceAttribute(const string& dist_attr_str)
{
    const char* curr = dist_attr_str.c_str();

    const char* hamming = "hammingDistance";
    const char* numberOfMS2IDs = "numberOfMS2IDs";
    const char* random = "randomDistance";
    const char* rt = "rtDistributionDistance";
    const char* weightedHamming = "weightedHammingDistance";


    if (!strncmp(hamming, curr, 15)) return _Hamming;
    if (!strncmp(numberOfMS2IDs, curr, 14)) return _NumberOfMS2IDs;
    if (!strncmp(random, curr, 14)) return _Random;
    if (!strncmp(rt, curr, 22)) return _RTDiff;
    if (!strncmp(weightedHamming, curr, 23)) return _WeightedHamming;

    // if distance attribute is unrecognized, throw and exit                                                                                        
    throw runtime_error(("[eharmony] Unrecognized distance attribute : " + dist_attr_str).c_str());
    return _Random; // so compiler doesn't whine                                                                                                    

}


struct Config
{
    std::vector<std::string> filenames;
    std::string inputPath;
    std::string outputPath;
    std::string batchFileName;

    WarpFunctionEnum warpFunction;
    DistanceAttributeEnum distanceAttribute;

    Config() : inputPath("."), outputPath(".") {}
    bool operator==(const Config& that);
    bool operator!=(const Config& that);

};

bool Config::operator==(const Config& that)
{
    return filenames == that.filenames &&
        inputPath == that.inputPath &&
        outputPath == that.outputPath &&
        batchFileName == that.batchFileName &&
        warpFunction == that.warpFunction &&
        distanceAttribute == that.distanceAttribute;       

}


bool Config::operator!=(const Config& that)
{
    return !(*this == that);

}

class Matcher
{

public:

    Matcher(){}
    Matcher(Config& config);

    void checkSourceFiles();
    void readSourceFiles();
    void processFiles();


private:

    Config _config;

    std::map<std::string, PidfPtr> _peptideData;
    std::map<std::string, FdfPtr> _featureData;

};



void processFile(Config& config)
{
    Matcher matcher(config);
    return;

}


Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;
    Config config;

    ostringstream usage;
    usage << "Usage: eharmony [options] [filenames] \n"
          << endl;


    // define local variables to be read in as strings and translated
    string warpFunctionCalculator;
    string distanceAttribute;

    // define command line options
    po::options_description od_config("Options");
    od_config.add_options()
        ("inputPath,i", po::value<string>(&config.inputPath)," : specify location of input files")
        ("outputPath,o", po::value<string>(&config.outputPath), " : specify output path")
        ("filename,f", po::value<string>(&config.batchFileName)," : specify file listing input runIDs (e.g., 20090109-B-Run)")        
        ("warpFunctionCalculator,w", po::value<string >(&warpFunctionCalculator), " : specify method of calculating the rt-calibrating warp function.\nOptions:\nlinear, piecewiseLinear")
        ("distanceAttribute,d", po::value<string>(&distanceAttribute), " : specify distance attribute.\nOptions:\nhammingDistance, numberOfMS2IDs, randomDistance, rtDistributionDistance, weightedHammingDistance");

    // append options to usage string
    usage << od_config;

    // handle positional args
    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);

    po::options_description od_parse;
    od_parse.add(od_config).add(od_args);

    // parse command line
    po::variables_map vm;
    po::store(po::command_line_parser(argc, (char**)argv).options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // get filenames
    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    // translate local variables
    if (warpFunctionCalculator.size() > 0) config.warpFunction = translateWarpFunction(warpFunctionCalculator);
    if (distanceAttribute.size() > 0) config.distanceAttribute = translateDistanceAttribute(distanceAttribute);

    // usage if incorrect
    if (config.filenames.empty() && config.batchFileName == "")
        throw runtime_error(usage.str());

    return config;

}

void Matcher::checkSourceFiles()
{
    const string& inputPath = _config.inputPath;
    vector<string>::const_iterator file_it = _config.filenames.begin();
    for(; file_it != _config.filenames.end(); ++file_it)
        {
            string runID = *file_it;
            if (!boost::filesystem::exists((inputPath + runID + ".pep.xml").c_str()))
                {
                    throw runtime_error(("[msmatchmaker] The following file is missing : " + inputPath + runID + ".pep.xml").c_str());
                    return;

                }

            if (!boost::filesystem::exists((inputPath + runID + ".features").c_str()) && !boost::filesystem::exists((inputPath + runID + ".mzML").c_str()) && !boost::filesystem::exists((inputPath + runID + ".mzML").c_str()))
                {
                    throw runtime_error(("[msmatchmaker] At least one of the following files is necessary; all are missing: \n" + inputPath + runID + ".mzXML" + "\n" + inputPath + runID + ".mzML" +  "\n" + inputPath + runID + ".features").c_str());
                    return;

                }

            // TODO temporary, embed feature detection in the above control flow?
            if (!boost::filesystem::exists((inputPath + runID + ".features").c_str()))
                {
                    throw runtime_error("[msmatchmaker] do the feature detection separately for now, and move the .features file to the inputPath location.");
                    return;

                }

        }

    return;

}

void Matcher::readSourceFiles()
{
    vector<string>::const_iterator run_it = _config.filenames.begin();
    for(; run_it != _config.filenames.end(); ++run_it)
        {
            ifstream ifs_pep((_config.inputPath + *run_it + ".pep.xml").c_str());
            ifstream ifs_feat((_config.inputPath + *run_it + ".features").c_str());

            cout << (_config.inputPath  + *run_it + ".pep.xml").c_str() << endl;
            PidfPtr pidf(new PeptideID_dataFetcher(ifs_pep));

            cout << (_config.inputPath  + *run_it + ".features").c_str() << endl;
            FdfPtr fdf(new Feature_dataFetcher(ifs_feat));

            _peptideData.insert(pair<string, PidfPtr>(*run_it, pidf));
            _featureData.insert(pair<string, FdfPtr>(*run_it, fdf));
            
        }

    return;

}

void Matcher::processFiles()
{
 

            vector<AMTContainer> amtv;
	   
            vector<boost::shared_ptr<AMTContainer> > sp;
            
            vector<string>::iterator run_it = _config.filenames.begin();
            for(; run_it != _config.filenames.end(); ++run_it)
	        {		    
                PidfPtr pidf =  _peptideData.find(*run_it)->second;
                FdfPtr fdf = _featureData.find(*run_it)->second;
                string id = *run_it;
                
                DataFetcherContainer dfc(pidf, pidf, fdf, fdf); // dummy
                dfc.adjustRT(true, false); // just do this up front
                pidf->setRtAdjustedFlag(true);
                
                sp.push_back(boost::shared_ptr<AMTContainer>(new AMTContainer()));
                sp.back()->_pidf = pidf;
                sp.back()->_fdf = fdf;
                sp.back()->_id = id;
                sp.back()->rtAdjusted = true;
	        }

        // big switch here
        NeighborJoiner nj(sp, _config.warpFunction);
        switch (_config.distanceAttribute)
            {
            case _Hamming:
                {
                    nj._attributes.push_back(boost::shared_ptr<HammingDistance>(new HammingDistance(sp)));
                }

                break;

            case _NumberOfMS2IDs:
                {
                    nj._attributes.push_back(boost::shared_ptr<NumberOfMS2IDs>(new NumberOfMS2IDs()));
                }
                
                break;

            case _Random:
                {
                    nj._attributes.push_back(boost::shared_ptr<RandomDistance>(new RandomDistance()));
                }
                
                break;

            case _RTDiff:
                {
                    nj._attributes.push_back(boost::shared_ptr<RTDiffDistribution>(new RTDiffDistribution()));
                }
                
                break;
            
            case _WeightedHamming:
                {
                    nj._attributes.push_back(boost::shared_ptr<WeightedHammingDistance>(new WeightedHammingDistance(sp)));
                }

                break;

            default:
                {
                    throw runtime_error("[eharmony] We shouldn't be here. Improper translation of DistanceAttribute.");
                }
                
                break;
            }
            
	    nj.calculateDistanceMatrix();
        
        ofstream beforeMatrix("beforeMatrix.txt");	
        nj.write(beforeMatrix);

	    nj.joinAll();

	    vector<pair<int, int> >::iterator njit = nj._tree.begin();
	    cout << "Tree: " << endl;
	    for(; njit!= nj._tree.end(); ++njit) cout << njit->first << "  "  << njit->second << endl;
        
        //        vector<pair<int, int> > oldTree = nj._tree;
        
        boost::shared_ptr<AMTContainer> amtDatabase(new AMTContainer(nj._rowEntries.at(0)));
	    

	    ///
	    /// Exporting
	    ///

	    Exporter exporter_amt(amtDatabase->_pm, amtDatabase->_f2pm);
	    
	    string outputDir = "amt_barf";
	    if (!boost::filesystem::exists(_config.outputPath)) boost::filesystem::create_directory(_config.outputPath);
	    outputDir = _config.outputPath + "/" + outputDir;
	    boost::filesystem::create_directory(outputDir);

	    ofstream ofs_pm((outputDir + "/pm.xml").c_str());
	    exporter_amt.writePM(ofs_pm);

	    ofstream ofs_roc((outputDir + "/roc.txt").c_str());
	    exporter_amt.writeROCStats(ofs_roc);
       
	    ofstream ofs_r((outputDir + "/r_input.txt").c_str());
	    exporter_amt.writeRInputFile(ofs_r);

	    ///
	    /// write AMT database
	    ///
	    
	    ofstream ofs_amt((outputDir + "/database.xml").c_str());
        ofs_amt.precision(16);
	    XMLWriter writer(ofs_amt);
	    amtDatabase->write(writer);
        
        ofstream ofs_amt_tsv((outputDir + "/database.tsv").c_str());
        ofstream ofs_mass((outputDir + "/database_mass.tsv").c_str());

        vector<boost::shared_ptr<SpectrumQuery> > v = amtDatabase->_pidf->getAllContents();
        vector<boost::shared_ptr<SpectrumQuery> >::iterator vsq_it = v.begin();
        for(; vsq_it != v.end(); ++vsq_it)
            {
                ofs_amt_tsv << Ion::mz((*vsq_it)->precursorNeutralMass, (*vsq_it)->assumedCharge) << "\t" << (*vsq_it)->retentionTimeSec << "\t" << (*vsq_it)->searchResult.searchHit.peptide << "\n";

            }

        ofstream ofs_rr((outputDir + "/allvsall_r.txt").c_str());
        ofs_rr.precision(16);
        ofstream ofs_rr_mass((outputDir + "/allvsall_mass_r.txt").c_str());
        vector<boost::shared_ptr<SpectrumQuery> >::iterator it = v.begin();
        for(; it != v.end(); ++it)
            {
                ofs_rr << Ion::mz((*it)->precursorNeutralMass, (*it)->assumedCharge) << "\t" << (*it)->retentionTimeSec << "\n";
                ofs_mass <<  (*it)->precursorNeutralMass << "\t" << (*it)->retentionTimeSec << "\t" << (*it)->searchResult.searchHit.peptide <<"\n";
                ofs_rr_mass << (*it)->precursorNeutralMass << "\t" << (*it)->retentionTimeSec <<"\n";
               
            }
        
	    return;
        
}

Matcher::Matcher(Config& config) : _config(config)
{
    if (_config.batchFileName != "")
        {
            ifstream ifs((_config.batchFileName).c_str());
            string runID;
            while(ifs >> runID)
                _config.filenames.push_back(runID);

        }
 
    // check source files to make sure all are there
    cout << "[eharmony] Checking for necessary data files ... " << endl;
    checkSourceFiles();
    
    // read in source files
    cout << "[eharmony] Reading data files ...  " << endl;
    readSourceFiles();
   
    // process each pair of run IDs
    cout << "[eharmony] Processing files ... " << endl;
    processFiles();

    cout << "[eharmony] Done." << endl;

}

} // anonymous namespace

int main(int argc, const char* argv[])
{
    namespace bfs = boost::filesystem;
    bfs::path::default_name_check(bfs::native);

    try
        {
            Config config = parseCommandLine(argc, argv);
            processFile(config);
            return 0;

        }

    catch (exception& e)
        {
            cout << e.what() << endl;

        }

    catch (...)
        {
            cout << "[eharmony.cpp::main()] Abnormal termination.\n";

        }

    return 1;

}




