
///
/// eharmony.cpp
///

#include "eharmony.hpp"
#include "PeptideMatcher.hpp"
#include "Feature2PeptideMatcher.hpp"
#include "Exporter.hpp"
#include "Matrix.hpp"
#include "NeighborJoiner.hpp"
#include "EharmonyAgglomerator.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "boost/tuple/tuple_comparison.hpp"
#include "boost/filesystem.hpp"
#include <algorithm>
#include <string>

using namespace std;
using namespace pwiz::minimxml;
using namespace pwiz;
using namespace pwiz::eharmony;
using namespace pwiz::proteome;

namespace{

    boost::shared_ptr<SearchNeighborhoodCalculator> translateSearchNeighborhoodCalculator(const string& curr_str)
    {      
        const char* curr = curr_str.c_str();
        const char* naive = "naive";
        const char* normalDistribution = "normalDistribution";

        if (!strncmp(curr, naive, 5))
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


                return boost::shared_ptr<SearchNeighborhoodCalculator>(new SearchNeighborhoodCalculator(boost::lexical_cast<double>(mzTolerance), boost::lexical_cast<double>(rtTolerance)));
       
            }
        
        else if (!strncmp(curr, normalDistribution, 18)){}
           
         
    }

    boost::shared_ptr<NormalDistributionSearch> translateNormalDistributionSearch(const string& curr_str)
    {
       
        const char* open = "[";
        const char* close = "]";

        size_t open_index = curr_str.find(open) + 1;
        size_t close_index = curr_str.find(close);

        string numberOfStdDevs(curr_str.substr(open_index, close_index));
       

        NormalDistributionSearch result(boost::lexical_cast<double>(numberOfStdDevs));

        return boost::shared_ptr<NormalDistributionSearch>(new NormalDistributionSearch(boost::lexical_cast<double>(numberOfStdDevs)));

    }

    WarpFunctionEnum translateWarpFunctionCalculator(const string& wfe_string)
    {
  
              const char* linear = "linear";
              const char* piecewiseLinear = "piecewiseLinear";
              const char* curr = wfe_string.c_str();
              if (!strncmp(linear, curr, 6))
                  {
                    return Linear;
                    
                  }

              if (!strncmp(piecewiseLinear, curr, 15))
                  {
                    return PiecewiseLinear;

                  }

  

              return Default;

    }

    DistanceAttribute translateDistanceAttribute(const string& dist_attr_str)
    {
        const char* curr = dist_attr_str.c_str();

        const char* random = "randomDistance";
        const char* rt = "rtDistributionDistance";
        //        const char* hamming = "WeightedHammingDistance";

        if (!strncmp(random, curr, 14)) return RandomDistance();
        else if (!strncmp(rt, curr, 22)) return RTDiffDistribution();
        //        if (!strncmp(hamming, curr, 15)) return WeightedHammingDistance();
        
        else 
            {
                throw runtime_error(("[eharmony] Unrecognized distance attribute : " + dist_attr_str).c_str());
                return DistanceAttribute();

            }

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
 
    if ( _config.generateAMTDatabase )
        {
            vector<AMTContainer> amtv;
	   
            vector<boost::shared_ptr<AMTContainer> > sp;

            vector<string>::iterator run_it = _config.filenames.begin();
            for(; run_it != _config.filenames.end(); ++run_it)
	        {		    
                PidfPtr pidf =  _peptideData.find(*run_it)->second;
                FdfPtr fdf = _featureData.find(*run_it)->second;
                string id = *run_it;

                sp.push_back(boost::shared_ptr<AMTContainer>(new AMTContainer(pidf,fdf)));
                sp.back()->_id = id;

	        }
	    
	    NeighborJoiner nj(sp);
	    //      boost::shared_ptr<NumberOfMS2IDs> num(new NumberOfMS2IDs());
        //	    boost::shared_ptr<RTDiffDistribution> num(new RTDiffDistribution());
        //boost::shared_ptr<DummyDistance> num(new DummyDistance());
        boost::shared_ptr<WeightedHammingDistance> num(new WeightedHammingDistance(sp));
        //boost::shared_ptr<RandomDistance> num(new RandomDistance());
	    nj._attributes.push_back(num);
	    nj.calculateDistanceMatrix();
        
        ofstream beforeMatrix("beforeMatrix.txt");	
        nj.write(beforeMatrix);

	    nj.joinAll();

	    vector<pair<int, int> >::iterator njit = nj._tree.begin();
	    cout << "Tree: " << endl;
	    for(; njit!= nj._tree.end(); ++njit) cout << njit->first << "  "  << njit->second << endl;
	    //	    _config.warpFunction = PiecewiseLinear;
	    boost::shared_ptr<AMTContainer> amtDatabase = generateAMTDatabase(sp, nj._tree, _config.warpFunction, _config.parsedNDS);
        

        // boost::shared_ptr<AMTContainer> amtDatabase = generateAMTDatabase(sp);
	    
	    ///
	    /// Exporting
	    ///

	    Exporter exporter_amt(amtDatabase->_pm, amtDatabase->_f2pm);
	    
	    string outputDir = "amt";
	    if (!boost::filesystem::exists(_config.outputPath)) boost::filesystem::create_directory(_config.outputPath);
	    outputDir = _config.outputPath + "/" + outputDir;
	    boost::filesystem::create_directory(outputDir);

	    ofstream ofs_pm((outputDir + "/pm.xml").c_str());
	    exporter_amt.writePM(ofs_pm);

	    ofstream ofs_f2pm((outputDir + "/f2pm.xml").c_str());
	    exporter_amt.writeF2PM(ofs_f2pm);

	    ofstream ofs_roc((outputDir + "/roc.txt").c_str());
	    exporter_amt.writeROCStats(ofs_roc);

       
	    /* TODO: Decide what a pepXML output for the AMT database generation schema should be and if the concept even makes sense
	    ofstream ofs_pepxml((outputDir + "/ms1_5.pep.xml").c_str());
	    exporter_amt.writePepXML(mspa, ofs_pepxml);

	    ofstream ofs_combxml((outputDir + "/ms2_ms1_5.pep.xml").c_str());
	    exporter_amt.writeCombinedPepXML(mspa, ofs_combxml);
	    */
	    ofstream ofs_r((outputDir + "/r_input.txt").c_str());
	    exporter_amt.writeRInputFile(ofs_r);

	    ///
	    /// write AMT database
	    ///
	    
	    ofstream ofs_amt((outputDir + "/database.xml").c_str());
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

    // parse SNC and NDS
    cout << "[eharmony] Parsing search neighborhood calculator ... " << endl;
    if (_config.searchNeighborhoodCalculator.size() != 0 ) _config.parsedSNC = *(translateSearchNeighborhoodCalculator(_config.searchNeighborhoodCalculator));
    cout << "[eharmony] Parsing normal distribution search ... " << endl;
    if (_config.normalDistributionSearch.size() != 0 ) _config.parsedNDS = *(translateNormalDistributionSearch(_config.normalDistributionSearch));
    
    // same with WFC
    cout << "[eharmony] Parsing warp function calculator ... " << endl;
    _config.warpFunction = translateWarpFunctionCalculator(_config.warpFunctionCalculator);

    // process each pair of run IDs
    cout << "[eharmony] Processing files ... " << endl;
    processFiles();

    cout << "[eharmony] Done." << endl;

}

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




