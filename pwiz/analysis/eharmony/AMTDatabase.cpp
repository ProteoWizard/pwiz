///
/// AMTDatabase.cpp
///

#include "AMTDatabase.hpp"
#include "Exporter.hpp"
#include "pwiz/utility/proteome/Ion.hpp"

using namespace std;
using namespace pwiz;
using namespace eharmony;
using namespace pwiz::proteome;

AMTDatabase::AMTDatabase(const AMTContainer& amtContainer)
{
    vector<SpectrumQuery> ms2 = amtContainer._sqs;
    _peptides = PeptideID_dataFetcher(ms2);
    cout << _peptides.getAllContents().size() << endl;

}

vector<boost::shared_ptr<SpectrumQuery> > AMTDatabase::query(const Feature& f) 
{
    double mz = f.mzMonoisotopic;           
    double rt = f.retentionTime;
    return _peptides.getSpectrumQueries(mz,rt);

}

vector<boost::shared_ptr<SpectrumQuery> > AMTDatabase::query(const double& mz, const double& rt) 
{
    cout << "querying ... " << endl;
    cout << "result size: " << _peptides.getSpectrumQueries(mz,rt).size() << endl;
    cout << "_peptides.size(): " << _peptides.getAllContents().size() << endl;
    cout << "some contents: " << Ion::mz(_peptides.getAllContents().begin()->precursorNeutralMass, _peptides.getAllContents().begin()->assumedCharge) << "   " << _peptides.getAllContents().begin()->retentionTimeSec << endl;
    return _peptides.getSpectrumQueries(mz,rt);

}

// pass in data fetcher container of AMTDatabase peptides, dummy features, query run peptides, query run features

vector<SpectrumQuery> AMTDatabase::query(DataFetcherContainer& dfc, const WarpFunctionEnum& wfe, const SearchNeighborhoodCalculator& snc)
{
    dfc.adjustRT();
    dfc.warpRT(wfe);

    Peptide2FeatureMatcher p2fm(dfc._pidf_a, dfc._fdf_b, snc);

    vector<SpectrumQuery> result;
    vector<Match> matches = p2fm.getMatches();
    vector<Match>::iterator it = matches.begin();
    for(; it != matches.end(); ++it) result.push_back(it->spectrumQuery);

    return result;

}

vector<SpectrumQuery> AMTDatabase::query(DataFetcherContainer& dfc, const WarpFunctionEnum& wfe, const NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in)
{
    cout << "querying ... " << endl;
    string outputDir = "./amtdb_query";
    boost::filesystem::create_directory(outputDir);

    dfc.adjustRT();
    dfc.warpRT(wfe);

    cout << "constructing pm ... " << endl;
    PeptideMatcher pm(dfc);
    cout << "constructing p2fm ... " << endl;
    Peptide2FeatureMatcher p2fm(dfc._pidf_a, dfc._fdf_b, nds);

    Exporter exporter(pm, p2fm);

    ofstream ofs_pm((outputDir + "/pm.xml").c_str());
    exporter.writePM(ofs_pm);

    ofstream ofs_p2fm((outputDir + "/p2fm.xml").c_str());
    exporter.writeP2FM(ofs_p2fm);

    ofstream ofs_roc((outputDir + "/roc.txt").c_str());
    exporter.writeROCStats(ofs_roc);

    ofstream ofs_r((outputDir + "/r_input.txt").c_str());
    exporter.writeRInputFile(ofs_r);

    vector<SpectrumQuery> result;
    vector<Match> matches = p2fm.getMatches();
    vector<Match>::iterator it = matches.begin();
    for(; it != matches.end(); ++it) result.push_back(it->spectrumQuery);

    mspa_in.msmsRunSummary.spectrumQueries = result;

    ofstream ofs_pepxml((outputDir + "/ms1_5.pep.xml").c_str());
    exporter.writePepXML(mspa_in, ofs_pepxml);

    return result;
}

int main(int argc, char* argv[])
{
    ifstream dbFile("./amt_6mix/amt/database.xml");
    AMTContainer amt;
    cout << "[amtdb] reading database file ... " << endl;
    amt.read(dbFile);
    cout << "[amtdb] constructing database for query ... " << endl;
    AMTDatabase db(amt);

    cout << "[amtdb] reading peptide file ... " << endl;
    ifstream queryPeptideFile("./tempData/6mix/20080619-A-6mixtestRG_Data08_msprefix.pep.xml");
    PeptideID_dataFetcher pidf_query(queryPeptideFile);
    MSMSPipelineAnalysis mspa_query;
    mspa_query.read(queryPeptideFile);

    cout << "[amtdb] reading feature file ... " << endl;
    ifstream queryFeatureFile("./tempData/6mix/20080619-A-6mixtestRG_Data08_msprefix.features");
    Feature_dataFetcher fdf_query(queryFeatureFile);

    DataFetcherContainer dfc(db._peptides, pidf_query, amt._fdf, fdf_query);
    WarpFunctionEnum wfe = Linear;
    //  SearchNeighborhoodCalculator snc(.001,60);
    NormalDistributionSearch nds(3);   
    cout << "[amtdb] querying amt database ... " << endl;
    db.query(dfc,wfe,nds,mspa_query);
    
    /*
    double mz;
    double rt;
    bool done;

        while(!done)
      {
	  cout << "query mz:" << endl;
	  cin >> mz;
	  cout << "query rt: " << endl;
	  cin >> rt;

	  vector<SpectrumQuery> result = db.query(mz,rt);
    
	  ostringstream oss;
	  XMLWriter writer(oss);
	  vector<SpectrumQuery>::iterator it = result.begin();
	  for(; it != result.end(); ++it) it->write(writer);

	  cout << oss.str();
	   
      }
    */
    return 0;

}
