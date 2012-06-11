//
// $Id$
//

/// \file chainsaw.cpp
/// \brief Contains the code for the chainsaw executable.
///
/// Creates in silico digest of all proteins listed in a FASTA file.

#include "pwiz/data/proteome/DefaultReaderList.hpp"
#include "pwiz/data/proteome/ProteomeDataFile.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "pwiz/Version.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "boost/program_options.hpp"


using namespace pwiz::cv;
using namespace pwiz::proteome;
using namespace pwiz::util;

struct Config
{
    CVID cleavageAgent;
    string cleavageAgentRegex;
    Digestion::Config digestionConfig;
    vector<string> filenames;
    size_t precision;
    bool benchmark;
    bool indexOnly;
    bool proteinSummary;

    Config()
        :   cleavageAgent(MS_Trypsin_P),
            digestionConfig(0,0,100000),
            precision(12),
            benchmark(false),
            indexOnly(false),
            proteinSummary(false)
    {}

};

CVID translateCleavageAgentName(const string& s)
{
    CVID cleavageAgent = Digestion::getCleavageAgentByName(s);
    if (cleavageAgent == CVID_Unknown)
        throw runtime_error("[chainsaw] Unsupported cleavage agent name: " + s);
    return cleavageAgent;
}

Digestion::Specificity translateSpecificity(const string& s)
{
    if (s == "none") return Digestion::NonSpecific;
    else if (s == "semi") return Digestion::SemiSpecific;
    else if (s == "fully") return Digestion::FullySpecific;
    else throw runtime_error("[chainsaw] Unsupported specificity: " + s);
}

Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;
    Config config;

    ostringstream usage;
    usage << "Usage: chainsaw [options] [filenames] \n"
          << endl;

    // local variables for translation
    string tempEnzyme;
    string tempSpecificity;

    string cleavageAgentNameOptions = bal::join(Digestion::getCleavageAgentNames(), ", ");

    // define command line options
    po::options_description od_config("Options");
    od_config.add_options()
        ("cleavageAgentName,c", po::value<string>(&tempEnzyme), (" : specify cleavage by name. Options: " + cleavageAgentNameOptions + "\nDefault : trypsin").c_str())
        ("cleavageAgentRegex,r", po::value<string>(&config.cleavageAgentRegex), " : specify a cleavage agent regex (e.g. trypsin = \"(?<=[KR])(?!P)\")")
        ("numMissedCleavages,n", po::value<int>(&config.digestionConfig.maximumMissedCleavages)->default_value(config.digestionConfig.maximumMissedCleavages), " : specify number of missed cleavages to allow.")
        ("specificity,s", po::value<string>(&tempSpecificity), " : specify minimum specificity. Options: none, semi, fully. \nDefault: fully")
        ("minLength,m",po::value<int>(&config.digestionConfig.minimumLength)->default_value(config.digestionConfig.minimumLength), " : specify minimum length of digested peptides")
        ("maxLength,M",po::value<int>(&config.digestionConfig.maximumLength)->default_value(config.digestionConfig.maximumLength), " : specify maximum length of digested peptides")
        ("massPrecison,p", po::value<size_t>(&config.precision)->default_value(config.precision), " : specify precision of calculated mass of digested peptides")
        ("benchmark", po::value<bool>(&config.benchmark)->zero_tokens(), " : do not write results")
        ("indexOnly", po::value<bool>(&config.indexOnly)->zero_tokens(), " : create database index (if necessary)")
        ("proteinSummary", po::value<bool>(&config.proteinSummary)->zero_tokens(), " : print a table with index, id, length, MW, and description for each protein");
    
    
    // append options to usage string
    usage << od_config;

    // extra usage

    usage << "Examples:\n"
          << endl
          << "# tryptically digest database.fasta into database.fasta_digestedPeptides.txt\n"
          << "chainsaw database.fasta\n"
          << endl
          << "# test semi-tryptic digestion of all files matching the pattern *.fasta\n"
          << "chainsaw --benchmark *.fasta\n"
          << endl
          << "# create an index file for database.fasta\n"
          << "chainsaw --indexOnly database.fasta\n"
          << endl
          << "# create a summary table for database.fasta\n"
          << "chainsaw --proteinSummary database.fasta\n"
          << endl
          << endl

          << "Questions, comments, and bug reports:\n"
          << "http://proteowizard.sourceforge.net\n"
          << "support@proteowizard.org\n"
          << "\n"
          << "ProteoWizard release: " << pwiz::Version::str() << " (" << pwiz::Version::LastModified() << ")" << endl
          << "ProteoWizard Proteome: " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")" << endl
          << "Build date: " << __DATE__ << " " << __TIME__ << endl;

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
    {
        config.filenames = vm[label_args].as< vector<string> >();

        // expand the filenames by globbing to handle wildcards
        vector<bfs::path> globbedFilenames;
        BOOST_FOREACH(const string& filename, config.filenames)
        {
            expand_pathmask(bfs::path(filename), globbedFilenames);
            if (!globbedFilenames.size())
                 cout << "[chainsaw] no files found matching \"" << filename << "\"" << endl;
        }

        config.filenames.clear();
        BOOST_FOREACH(const bfs::path& filename, globbedFilenames)
            config.filenames.push_back(filename.string());

        // skip usage if user passed some files but none existed
        if (config.filenames.empty())
            throw exception();
    }

    // usage if incorrect
    if (config.filenames.empty())
        throw runtime_error(usage.str());

    // assign local variables to config
    if (tempEnzyme.size() > 0) config.cleavageAgent = translateCleavageAgentName(tempEnzyme);
    if (tempSpecificity.size() > 0) config.digestionConfig.minimumSpecificity = translateSpecificity(tempSpecificity);

    return config;

}

void writeSummary(const Config& config, const ProteomeData& pd)
{
    ofstream ofs;
    if (!config.benchmark)
    {
        ofs.open((pd.id + "_summary.tsv").c_str());
        ofs << "index" 
            << "\t" << "id" 
            << "\t" << "length" 
            << "\t" << "MW"
            << "\t" << "description" 
            << "\n";

        ofs.precision(config.precision);
    }

    const ProteinList& pl = *pd.proteinListPtr;
    cout << "Summarizing " << pl.size() << " proteins..." << endl;
    bpt::ptime start = bpt::microsec_clock::local_time();
    for(size_t index = 0, end=pl.size(); index < end; ++index)
    {
        if (index > 0 && (index % 100) == 0)
        {
            bpt::ptime stop = bpt::microsec_clock::local_time();
            bpt::time_duration duration = stop - start;
            double perSecond = index / (double) duration.total_milliseconds() * 1000;
            cout << std::fixed << setprecision(0) << index << " (" << perSecond << " per second)\r" << flush;
        }

        string id;
        try
        {
            // get protein with sequence for the length column
            ProteinPtr proteinPtr = pl.protein(index, true);
            id = proteinPtr->id;

            // skip output if benchmarking
            if (config.benchmark)
                continue;

            ofs << index
                << "\t" << id
                << "\t" << proteinPtr->sequence().length()
                << "\t" << proteinPtr->molecularWeight()
                << "\t" << proteinPtr->description
                << "\n";
        }
        catch (runtime_error& e)
        {
            cerr << "Error summarizing protein " << index << " (" << id << "): " << e.what() << endl;
        }
        catch (...)
        {
            cerr << "Unknown error summarizing protein " << index << " (" << id << ")" << endl;
        }
    }
    bpt::ptime stop = bpt::microsec_clock::local_time();
    bpt::time_duration duration = stop - start;
    cout << "Summary finished. Time elapsed: " << bpt::to_simple_string(duration) << endl;
}

void writeDigestion(const Config& config, const ProteomeData& pd)
{
    ofstream ofs;
    if (!config.benchmark)
    {
        ofs.open((pd.id + "_digestedPeptides.tsv").c_str());
        ofs << "sequence" 
            << "\t" << "protein" 
            << "\t" << "mass" 
            << "\t" << "missedCleavages" 
            << "\t" << "specificity"
            << "\t" << "nTerminusIsSpecific"
            << "\t" << "cTerminusIsSpecific"
            << "\n";

        ofs.precision(config.precision);
    }

    const ProteinList& pl = *pd.proteinListPtr;
    cout << "Digesting " << pl.size() << " proteins..." << endl;
    bpt::ptime start = bpt::microsec_clock::local_time();
    for(size_t index = 0, end=pl.size(); index < end; ++index)
    {
        if (index > 0 && (index % 100) == 0)
        {
            bpt::ptime stop = bpt::microsec_clock::local_time();
            bpt::time_duration duration = stop - start;
            double perSecond = index / (double) duration.total_milliseconds() * 1000;
            cout << std::fixed << setprecision(0) << index << " (" << perSecond << " per second)\r" << flush;
        }

        string id;
        try
        {
            // digest
            ProteinPtr proteinPtr = pl.protein(index, true);
            id = proteinPtr->id;

            shared_ptr<Digestion> digestion;
            if (!config.cleavageAgentRegex.empty())
                digestion.reset(new Digestion(*proteinPtr, boost::regex(config.cleavageAgentRegex), config.digestionConfig));
            else
                digestion.reset(new Digestion(*proteinPtr, config.cleavageAgent, config.digestionConfig));

            // iterate through digested peptides (and, if not benchmarking, write output)
            if (config.benchmark)
            {
                for (Digestion::const_iterator jt = digestion->begin(); jt != digestion->end(); ++jt)
                {
                    const DigestedPeptide& p = *jt; // instantiate the peptide
                    volatile size_t offset = p.offset(); // prevent compiler optimizing the loop away
                }
                continue;
            }

            for (Digestion::const_iterator jt = digestion->begin(); jt != digestion->end(); ++jt)
                ofs << jt->sequence() 
                    << "\t" << proteinPtr->id
                    << "\t" << jt->monoisotopicMass(0, false) /* unmodified neutral mass + h2o*/ 
                    << "\t" << jt->missedCleavages() 
                    << "\t" << jt->specificTermini() 
                    << "\t" << jt->NTerminusIsSpecific() 
                    << "\t" << jt->CTerminusIsSpecific() 
                    << "\n";
        }
        catch (runtime_error& e)
        {
            cerr << "Error digesting protein " << index << " (" << id << "): " << e.what() << endl;
        }
        catch (...)
        {
            cerr << "Unknown error digesting protein " << index << " (" << id << ")" << endl;
        }
    }
    bpt::ptime stop = bpt::microsec_clock::local_time();
    bpt::time_duration duration = stop - start;
    cout << "Digestion finished. Time elapsed: " << bpt::to_simple_string(duration) << endl;
}

void go(const Config& config)
{
    vector<string>::const_iterator file_it = config.filenames.begin();
    for( ; file_it != config.filenames.end(); ++file_it)
    {
        cout << "Reading database: " << *file_it << endl;
        ProteomeDataFile pd(*file_it, true);
        cout << "Finished reading database." << endl;

        if (config.indexOnly)
            continue;

        if (config.proteinSummary)
            writeSummary(config, pd);
        else
            writeDigestion(config, pd);
    }
}

int main(int argc, const char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);
        go(config);

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
    }

    return 0;
}
