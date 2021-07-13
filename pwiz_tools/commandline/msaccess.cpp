//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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

/// \file msaccess.cpp
/// \brief Contains the code for the msaccess executable.
///
/// This program bundles passive analysis classes in
/// pwiz/analysis/passive that are applicable to mass spectra files to
/// provide direct access to analysis tools. The
/// MSDataAnalyzerApplication and MSDataAnalyzerContainer frameworks
/// are used to manage user selection, processing, and output.

#include "pwiz_tools/common/MSDataAnalyzerApplication.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/analysis/passive/MetadataReporter.hpp"
#include "pwiz/analysis/passive/RunSummary.hpp"
#include "pwiz/analysis/passive/SpectrumTable.hpp"
#include "pwiz/analysis/passive/SpectrumBinaryData.hpp"
#include "pwiz/analysis/passive/RegionSlice.hpp"
#include "pwiz/analysis/passive/RegionTIC.hpp"
#include "pwiz/analysis/passive/RegionSIC.hpp"
#include "pwiz/analysis/passive/Pseudo2DGel.hpp"
#include "pwiz/Version.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::analysis;


template <typename analyzer_type>
void printCommandUsage(ostream& os)
{
    os << "  " << analyzer_strings<analyzer_type>::id()
       << " " << analyzer_strings<analyzer_type>::argsFormat() << endl
       << "    (" << analyzer_strings<analyzer_type>::description() << ")\n";

    vector<string> usage = analyzer_strings<analyzer_type>::argsUsage();
    for (vector<string>::const_iterator it=usage.begin(); it!=usage.end(); ++it)
        os << "      " << *it << endl;

    os << endl;
}

// check for match, and construct list of expected values
// in case of no match at all
static string supportedAnalyzers;
static bool supportedAnalyzer(const string &name,const string &id)
{
    supportedAnalyzers += " \"";
    supportedAnalyzers += id;
    supportedAnalyzers += "\"";
    return name==id;
}

void initializeAnalyzers(MSDataAnalyzerContainer& analyzers,
                         const vector<string>& commands)
{
    shared_ptr<MSDataCache> cache(new MSDataCache);
    analyzers.push_back(cache);

    for (vector<string>::const_iterator it=commands.begin(); it!=commands.end(); ++it)
    {
        string name, args;
        istringstream iss(*it);
        iss >> name;
        getlinePortable(iss, args);

        if (supportedAnalyzer(name, analyzer_strings<MetadataReporter>::id()))
        {
            MSDataAnalyzerPtr anal(new MetadataReporter);
            analyzers.push_back(anal);
        }
        else if (supportedAnalyzer(name, analyzer_strings<RunSummary>::id()))
        {
            MSDataAnalyzerPtr anal(new RunSummary(*cache, args));
            analyzers.push_back(anal);
        }
        else if (supportedAnalyzer(name, analyzer_strings<SpectrumTable>::id()))
        {
            MSDataAnalyzerPtr anal(new SpectrumTable(*cache, args));
            analyzers.push_back(anal);
        }
        else if (supportedAnalyzer(name, analyzer_strings<SpectrumBinaryData>::id()))
        {
            MSDataAnalyzerPtr anal(new SpectrumBinaryData(*cache, args));
            analyzers.push_back(anal);
        }
        else if (supportedAnalyzer(name, analyzer_strings<RegionSlice>::id()))
        {
            MSDataAnalyzerPtr anal(new RegionSlice(*cache, args));
            analyzers.push_back(anal);
        }
        else if (supportedAnalyzer(name, analyzer_strings<RegionTIC>::id()))
        {
            MSDataAnalyzerPtr anal(new RegionTIC(*cache, args));
            analyzers.push_back(anal);
        }
        else if (supportedAnalyzer(name, analyzer_strings<RegionSIC>::id()))
        {
            MSDataAnalyzerPtr anal(new RegionSIC(*cache, args));
            analyzers.push_back(anal);
        }
        else if (supportedAnalyzer(name, analyzer_strings<Pseudo2DGel>::id()))
        {
            MSDataAnalyzerPtr anal(new Pseudo2DGel(*cache, args));
            analyzers.push_back(anal);
        }
        else
        {
            cerr << "Unknown analysis command \"" << name << "\".  Expected one of:" << supportedAnalyzers << "." << endl;
        }
    }
}


string usage(const MSDataAnalyzerApplication& app)
{
    ostringstream oss;

    oss << "Usage: msaccess [options] [filenames]\n"
        << "MassSpecAccess - command line access to mass spec data files\n"
        << "                 uses -x/--exec to specify analysis command.\n"
        << "\n"
        << "Options:\n"
        << "\n"
        << app.usageOptions
        << "\n"
        << "Analysis commands (used with -x/--exec):\n"
        << "\n";

    printCommandUsage<MetadataReporter>(oss);
    printCommandUsage<RunSummary>(oss);
    printCommandUsage<SpectrumTable>(oss);
    printCommandUsage<SpectrumBinaryData>(oss);
    printCommandUsage<RegionSlice>(oss);
    printCommandUsage<RegionTIC>(oss);
    printCommandUsage<RegionSIC>(oss);
    printCommandUsage<Pseudo2DGel>(oss);

    oss << "\nExamples:\n\n"
        << "msaccess data.mzML -x \"tic " TIC_MZRANGE_ARG "=409-410\" --filter=\"msLevel 2\"\n"
        << "(creates data.mzML.tic.409.00-410.00.txt with total ion current info for mass range 409-410 in ms2 scans)\n"
        << "This could also be achieved as \"msaccess data.mzML -c mycfg.txt\" where mycfg.txt is a file containing the lines\n"
        << "   exec = tic " TIC_MZRANGE_ARG "=409-410\n"
        << "   filter = msLevel 2\n\n"
        << "msaccess data.mzML -x spectrum_table\n"
        << "(creates data.mzML.spectrum_table.txt with summary information for all spectra as read from the scan headers)\n\n"
        << "msaccess data.mzML -x \"binary " BINARY_INDEX_ARG "=0-3\"\n"
        << "(creates files data.mzML.binary.*.txt with binary data for spectra 0 through 3)\n\n"
        << "msaccess data.mzML -x \"slice " SLICE_INDEX_ARG "=2,6 " SLICE_MZRANGE_ARG "=100,600 delimiter=tab\"\n"
        << "(creates data.mzML.slice.index_2-6.mz_100.0000-600.0000.tsv with a table of data in the selected range)\n\n"
        << "msaccess data.mzML -x \"image width=800 height=600\"\n"
        << "(creates data.mzML.image* with pseudo-2D-gel image of the data file)\n\n";

    oss << endl
        << "Questions, comments, and bug reports:\n"
        << "https://github.com/ProteoWizard\n"
        << "support@proteowizard.org\n"
        << "\n"
        << "ProteoWizard release: " << pwiz::Version::str() << endl
        << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    return oss.str();
}


int main(int argc, const char* argv[])
{
    try
    {
        MSDataAnalyzerApplication app(argc, argv);
        MSDataAnalyzerContainer analyzers;
        initializeAnalyzers(analyzers, app.commands);
        if (app.filenames.empty() && !app.commands.empty())
            cerr << "no files to process.\n";
        if (!app.filenames.empty() && app.commands.empty())
            cerr << "no -x/--exec command given.\n";
        if (app.filenames.empty() || app.commands.empty())
            throw runtime_error(usage(app).c_str());

        app.run(analyzers, &cerr);

        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }

    return 1;
}
