///
/// Matcher.cpp
///

#include "Matcher.hpp"
#include "PeptideMatcher.hpp"
#include "Peptide2FeatureMatcher.hpp"
#include "Exporter.hpp"

#include "boost/tuple/tuple_comparison.hpp"
#include "boost/filesystem.hpp"
#include <algorithm>
#include <string>

using namespace std;
using namespace pwiz::minimxml;
using namespace pwiz;
using namespace pwiz::eharmony;

namespace{

    SearchNeighborhoodCalculator translateNaiveSearchNbhdCalculator(const string& curr_str)
    {
        const char* open = "[";
        const char* split = ",";
        const char* close = "]";

        size_t open_index = curr_str.find(open) + 1;
        size_t split_index = curr_str.find(split);
        size_t close_index = curr_str.find(close);

        string mzTolerance(curr_str.substr(open_index, split_index));

        split_index += 1;
        string rtTolerance(curr_str.substr(split_index, close_index));

        SearchNeighborhoodCalculator result(boost::lexical_cast<double>(mzTolerance), boost::lexical_cast<double>(rtTolerance));
       
        return result;

    }

    NormalDistributionSearch translateNormalDistributionSearch(const string& curr_str)
    {
        const char* open = "[";
        const char* close = "]";

        size_t open_index = curr_str.find(open) + 1;
        size_t close_index = curr_str.find(close);

        string numberOfStdDevs(curr_str.substr(open_index, close_index));

        NormalDistributionSearch result(boost::lexical_cast<double>(numberOfStdDevs));

        return result;

    }

    vector<SearchNeighborhoodCalculator> translateSearchNbhdCalculatorStrings(const vector<string>& sncs)
    {
        vector<SearchNeighborhoodCalculator> result;
        vector<string>::const_iterator snc_it = sncs.begin();
        for(; snc_it != sncs.end(); ++snc_it)
            {
                const char* naive = "naive";
                const char* normalDistribution = "normalDistribution";
                string curr_str = *snc_it;
                const char* curr = snc_it->c_str();

                cout << "SearchNeighborhoodCalculator: " << curr << endl;

                if (!strncmp(naive, curr,5))
                    {
                        result.push_back(translateNaiveSearchNbhdCalculator(curr_str));
                        result.back()._id = curr_str;

                    }

                if (!strncmp(normalDistribution, curr, 18))
                    {
                        result.push_back(translateNormalDistributionSearch(curr_str));
                        result.back()._id = curr_str;

                    }

            }

        return result;

    }

    vector<WarpFunctionEnum> translateWarpFunctionCalculatorStrings(const vector<string>& wfcs)
    {
  
        vector<WarpFunctionEnum> result;
        vector<string>::const_iterator it = wfcs.begin();
        
        for(; it != wfcs.end(); ++it)
          {
              const char* linear = "linear";
              const char* piecewiseLinear = "piecewiseLinear";
              const char* curr = it->c_str();
              if (!strncmp(linear, curr, 6))
                  {
                    result.push_back(Linear);
                    
                  }

              if (!strncmp(piecewiseLinear, curr, 15))
                  {
                    result.push_back(PiecewiseLinear);

                  }

          }

         return result;

    }

} // anonymous namespace

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
            PeptideID_dataFetcher pidf(ifs_pep);

            cout << (_config.inputPath  + *run_it + ".features").c_str() << endl;
            Feature_dataFetcher fdf(ifs_feat);

            _peptideData.insert(pair<string, PeptideID_dataFetcher>(*run_it, pidf));
            _featureData.insert(pair<string, Feature_dataFetcher>(*run_it, fdf));
            
        }

    return;

}

void Matcher::processFiles()
{
    vector<string>::iterator running_it = _config.filenames.begin();
    for(; running_it != _config.filenames.end(); ++running_it)
        {
            vector<string>::iterator runner_it = _config.filenames.begin();
            for(; runner_it != _config.filenames.end(); ++runner_it)
                {

                    if (*running_it == *runner_it) continue;

                    string run_A = *running_it;
                    string run_B = *runner_it;
                    cout << "---" << run_A << " vs. " << run_B << "---" << endl;

                    PeptideID_dataFetcher pidf_a = _peptideData.find(run_A)->second;
                    PeptideID_dataFetcher pidf_b = _peptideData.find(run_B)->second;

                    Feature_dataFetcher fdf_a = _featureData.find(run_A)->second;
                    Feature_dataFetcher fdf_b = _featureData.find(run_B)->second;

                    // make DataFetcherContainer (dfc) out of run names and maps
                    DataFetcherContainer dfc(pidf_a, pidf_b, fdf_a, fdf_b);
                    dfc.adjustRT();                   
                    /*
                    if ( dfc._pidf_a.getRtAdjustedFlag() ) // TODO fix to only erase and insert the one that was changed (both may not be)
                        {
                            _peptideData.erase(_peptideData.find(run_A));
                            _peptideData.insert(make_pair(run_A,dfc._pidf_a));

                            _featureData.erase(_featureData.find(run_A));
                            _featureData.insert(make_pair(run_A, dfc._fdf_a));

                        }

                    if ( dfc._pidf_b.getRtAdjustedFlag() )
                        {
                            _peptideData.erase(_peptideData.find(run_B));
                            _peptideData.insert(make_pair(run_B,dfc._pidf_a));

                            _featureData.erase(_featureData.find(run_B));
                            _featureData.insert(make_pair(run_B, dfc._fdf_a));

                        }
		    */
                    //msmatchmake it for each SNC.

                    SearchNeighborhoodCalculator snc;

                    // construct the original pep.xml object. TODO: do this when reading in the file normally

                    ifstream ifs_original((_config.inputPath + "/" + run_B + ".pep.xml").c_str());
                    MSMSPipelineAnalysis mspa;
                    mspa.read(ifs_original);

                    if (_config.parsedSNCs.size() > 0 )
                        {
                            vector<SearchNeighborhoodCalculator>::iterator it = _config.parsedSNCs.begin();

                            for(; it != _config.parsedSNCs.end(); ++it)
                                {
                                    string dirName = (run_A + "_" + run_B + "_"  + it->_id);
                                    cout << "eharmonizing: " << it->_id << endl;
                                    msmatchmake(dfc,*it, mspa, dirName);

                                }

                        }

                    else 
                        {
                            string dirName = (run_A + "_" + run_B + "_default");
                            cout << "eharmonizing: " << snc._id << endl;
                            msmatchmake(dfc,snc, mspa, dirName);

                        }

                }

        }

    return;
}

void Matcher::msmatchmake(DataFetcherContainer& dfc, SearchNeighborhoodCalculator& snc, MSMSPipelineAnalysis& mspa, string& outputDir) // pass in original mspa for writing
{   

  // for each warp function calculator, do the below. first test for just one.
    
    if (_config.parsedWarpFunctionCalculators.size() == 0 ) _config.parsedWarpFunctionCalculators.push_back(Default);
    WarpFunctionEnum wfe = *_config.parsedWarpFunctionCalculators.begin();
    
    dfc.warpRT(wfe);

    snc.calculateTolerances(dfc);

    PeptideMatcher pm(dfc);
    Peptide2FeatureMatcher p2fm(dfc._pidf_a, dfc._fdf_b, snc);
    
    // TODO add a --export option and flag in config
    if (!boost::filesystem::exists(_config.outputPath)) boost::filesystem::create_directory(_config.outputPath);
    outputDir = _config.outputPath + "/" + outputDir;
    boost::filesystem::create_directory(outputDir);

    Exporter exporter(pm, p2fm);
    
    ofstream ofs_pm((outputDir + "/pm.xml").c_str());
    exporter.writePM(ofs_pm);
    
    ofstream ofs_p2fm((outputDir + "/p2fm.xml").c_str());
    exporter.writeP2FM(ofs_p2fm);
    
    ofstream ofs_roc((outputDir + "/roc.txt").c_str());
    exporter.writeROCStats(ofs_roc);

    ofstream ofs_pepxml((outputDir + "/ms1_5.pep.xml").c_str());
    exporter.writePepXML(mspa, ofs_pepxml);

    ofstream ofs_r((outputDir + "/r_input.txt").c_str());
    exporter.writeRInputFile(ofs_r);

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

    // turn vector<string> of SNCs into actual SNCS
    cout << "[eharmony] Parsing search neighborhood calculators ... " << endl;
    _config.parsedSNCs = translateSearchNbhdCalculatorStrings(_config.searchNbhdCalculators);
    
    // same with WFCs
    cout << "[eharmony] Parsing warp function calculators ... " << endl;
    _config.parsedWarpFunctionCalculators = translateWarpFunctionCalculatorStrings(_config.warpFunctionCalculators);

    // process each pair of run IDs
    cout << "[eharmony] Processing files ... " << endl;
    processFiles();

    cout << "[eharmony] Done." << endl;

}

//  LocalWords:  getRtAdjustedFlag
