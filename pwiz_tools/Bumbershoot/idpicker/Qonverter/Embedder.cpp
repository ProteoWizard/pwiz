//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//


#include "Embedder.hpp"
#include "Qonverter.hpp"
#include "SchemaUpdater.hpp"
#include "Filter.hpp"
#include "sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "boost/foreach_field.hpp"
#include "boost/throw_exception.hpp"
#include "boost/xpressive/xpressive.hpp"
#include "boost/assign.hpp"
#include "boost/multi_array.hpp"
#include <memory>


using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace pwiz::chemistry;
using namespace boost::assign;
//namespace sqlite = sqlite3pp;


BEGIN_IDPICKER_NAMESPACE
namespace Embedder {


string defaultSourceExtensionPriorityList()
{
#ifdef WIN32
    ExtendedReaderList readerList;
#else
    DefaultReaderList readerList;
#endif

    vector<string> extensions{ ".mz5", ".mzML", ".mzXML" };
    set<string> addedExtensions(extensions.begin(), extensions.end());
    set<string> priorityExtensions = addedExtensions;
    for (const auto& typeExtsPair : readerList.getFileExtensionsByType())
        for (const string& ext : typeExtsPair.second)
            if (addedExtensions.count(ext) == 0 && priorityExtensions.count(ext) == 0)
            {
                extensions.push_back(ext);
                addedExtensions.insert(ext);
            }
    extensions.insert(extensions.end(), addedExtensions.begin(), addedExtensions.end()); // addedExtensions is already sorted
    return bal::join(extensions, ";");
}


QuantitationConfiguration::QuantitationConfiguration(QuantitationMethod quantitationMethod, pwiz::chemistry::MZTolerance reporterIonMzTolerance, bool normalizeIntensities)
    : quantitationMethod(quantitationMethod), reporterIonMzTolerance(reporterIonMzTolerance), normalizeIntensities(normalizeIntensities)
{
    if (quantitationMethod == QuantitationMethod::TMT10plex)
    {
        const double midwayBetweenTMT_130C_vs_130N = 130.13798; // (130.14114 + 130.13482) / 2;
        if ((130.14114 - reporterIonMzTolerance) < midwayBetweenTMT_130C_vs_130N)
            throw invalid_argument("[QuantitationConfiguration] resolving TMT10's N vs. C isotopes requires reporter ion m/z tolerance less than 0.0032 m/z (~25 ppm)");
    }
    else
    {
        const double midwayBetweenTMT_131_vs_130C = 130.63966; // (131.13818 + 130.14114) / 2;
        if ((131.13818 - reporterIonMzTolerance) < midwayBetweenTMT_131_vs_130C)
            throw invalid_argument("[QuantitationConfiguration] resolving reporter ions for TMT/iTRAQ requires reporter ion m/z tolerance less than 0.5 m/z (~3800 ppm)");
    }
}

QuantitationConfiguration::operator std::string() const
{
    return (boost::format("%1% ; %2%") % lexical_cast<string>(reporterIonMzTolerance) % (normalizeIntensities ? "1" : "0")).str();
}


namespace {

struct SpectrumSource
{
    sqlite3_int64 id;
    string name;
    vector<sqlite3_int64> spectrumIds;
    vector<string> spectrumNativeIds;
    QuantitationMethod quantitationMethod;

    string filepath;
};

// return the first existing filepath with one of the given extensions in the search path
string findNameInPath(const string& filenameWithoutExtension,
                      const vector<string>& extensions,
                      const vector<string>& searchPath)
{
    ExtendedReaderList readerList;

    for(const string& extension : extensions)
    for(const string& path : searchPath)
    {
        bfs::path filepath(path);
        filepath /= filenameWithoutExtension + extension;

        // if the path exists, check whether MSData can handle it
        if (bfs::exists(filepath) && !readerList.identify(filepath.string()).empty())
            return filepath.string();
    }
    return "";
}

void getSources(sqlite::database& idpDb,
                vector<SpectrumSource>& sources,
                const string& idpDbFilepath,
                const string& sourceSearchPath,
                const string& sourceExtensionPriorityList,
                const map<int, QuantitationConfiguration>& quantitationMethodBySource,
                pwiz::util::IterationListenerRegistry* ilr)
{
    string databaseName = bfs::path(idpDbFilepath).replace_extension("").filename().string();

    // parse the search path
    vector<string> paths;
    bal::split(paths, sourceSearchPath, bal::is_any_of(";"));

    if (paths.empty())
        throw runtime_error("empty search path");

    // parse the extension list
    vector<string> extensions;
    bal::split(extensions, sourceExtensionPriorityList, bal::is_any_of(";"));

    if (extensions.empty())
        throw runtime_error("empty source extension list");

    ITERATION_UPDATE(ilr, 0, 0, "opening database \"" + databaseName + "\"");

    // open the database
    idpDb.connect(idpDbFilepath, sqlite::no_mutex);

    ITERATION_UPDATE(ilr, 0, 0, "querying sources and spectra");

    // get a list of sources from the database
    sqlite::query sourceQuery(idpDb, "SELECT Id, Name, QuantitationMethod FROM SpectrumSource ORDER BY Id");
    for(sqlite::query::rows row : sourceQuery)
    {
        sources.push_back(SpectrumSource());
        SpectrumSource& ss = sources.back();
        ss.id = row.get<sqlite3_int64>(0);
        ss.name = row.get<string>(1);
        ss.quantitationMethod = QuantitationMethod::get_by_index(row.get<int>(2)).get();
    }

    if (sources.empty())
        throw runtime_error("query returned no sources for \"" + databaseName + "\"");

    // use UnfilteredSpectrum table if present
    string spectrumTable = "UnfilteredSpectrum";
    try { sqlite::query(idpDb, "SELECT Id FROM UnfilteredSpectrum LIMIT 1").begin(); }
    catch (sqlite::database_error&) { spectrumTable = "Spectrum"; }

    // get a list of spectra for each source
    vector<SpectrumSource>::iterator itr = sources.begin();
    sqlite::query spectrumQuery(idpDb, ("SELECT Source, Id, NativeID FROM " + spectrumTable + " ORDER BY Source").c_str());
    for(sqlite::query::rows row : spectrumQuery)
    {
        sqlite3_int64 sourceId = row.get<sqlite3_int64>(0);
        if (itr->id != sourceId)
        {
            if (itr->spectrumNativeIds.empty())
                throw runtime_error("query returned no spectra for source \"" + itr->name + "\"");
            if (itr == sources.end())
                throw runtime_error("found Spectrum.Source value that does not correspond to any SpectrumSource: " + lexical_cast<string>(sourceId));
            ++itr;
        }
        itr->spectrumIds.push_back(row.get<sqlite3_int64>(1));
        itr->spectrumNativeIds.push_back(row.get<string>(2));
    }

    ITERATION_UPDATE(ilr, 0, 0, "searching for spectrum sources");

    // look for files for each source
    vector<string> missingSources;
    for(SpectrumSource& source : sources)
    {
        vector<string> perSourcePaths(paths);
        string rootInputDirectory = bfs::path(idpDbFilepath).parent_path().string();
        for(string& path : perSourcePaths)
            bal::replace_all(path, "<RootInputDirectory>", rootInputDirectory);

        source.filepath = findNameInPath(source.name, extensions, perSourcePaths);
        if (source.filepath.empty())
            missingSources.push_back(source.name);
    }

    if (missingSources.size() == 1)
        throw runtime_error("no filepath could be found corresponding to source \"" + missingSources[0] + "\"");
    else if (missingSources.size() > 1)
        throw runtime_error("no filepath could be found corresponding these sources:\n" + bal::join(missingSources, "\n"));
}


struct ReporterIon
{
    double mass;
    int index;
};

struct SpectrumRow
{
    sqlite3_int64 id;
    double precursorMz;
    vector<double> reporterIonIntensities;
};

// iTRAQ
// Label  Mass        4plex  8plex  DeltaMassFromLastReporter
// 113    113.107873  0      1
// 114    114.111228  1      1      1.0034
// 115    115.108263  1      1      0.9970
// 116    116.111618  1      1      1.0034
// 117    117.114973  1      1      1.0034
// 118    118.112008  0      1      0.9970
// 119    119.115363  0      1      1.0034
// 121    121.122072  0      1      2.0067
ReporterIon iTRAQ_masses[8] =
{
    { 113.107873, 0 },
    { 114.111228, 1 },
    { 115.108263, 2 },
    { 116.111618, 3 },
    { 117.114973, 4 },
    { 118.112008, 5 },
    { 119.115363, 6 },
    { 121.122072, 7 }
};

// TMT
// http://www.piercenet.com/instructions/2162457.pdf
// Label  Mass        2plex  6plex  10plex   DeltaMassFromLastReporter
// 126    126.12773   1      1      1
// 127N   127.12476   0      1      1        0.9970
// 127C   127.13108   1      0      1        0.0063
// 128N   128.12811   0      0      1        0.9970
// 128C   128.13443   0      1      1        0.0063
// 129N   129.13147   0      1      1        0.9970
// 129C   129.13779   0      0      1        0.0063
// 130N   130.13482   0      0      1        0.9970
// 130C   130.14114   0      1      1        0.0063
// 131    131.13818   0      1      1        0.9970
ReporterIon TMT_masses[10] =
{
    { 126.12773, 0 },
    { 127.12476, 1 },
    { 127.13108, 2 },
    { 128.12811, 3 },
    { 128.13443, 4 },
    { 129.13147, 5 },
    { 129.13779, 6 },
    { 130.13482, 7 },
    { 130.14114, 8 },
    { 131.13818, 9 }
};

void correctIsotopeImpurities(vector<SpectrumRow>& spectrumRows,
                              vector<double>& totalReporterIonIntensities,
                              const vector<ReporterIon>& reporterIons,
                              QuantitationMethod quantitationMethod,
                              boost::multi_array<double, 2>* userSpecifiedIsotopeCorrectionFactors)
{
    if (spectrumRows.empty())
        return;

    double itraq4plexIsotopeCorrectionFactors[4*4] =
    {
        // -2    -1     +1     +2
        0.000, 0.01, 0.059, 0.002, // 114 (icf[0])
        0.000, 0.02, 0.056, 0.001, // 115 (icf[1])
        0.000, 0.03, 0.045, 0.001, // 116 (icf[2])
        0.001, 0.04, 0.035, 0.001  // 117 (icf[3])
    };

    double itraq8plexIsotopeCorrectionFactors[8*4] =
    {
        //  -2      -1      +1      +2
        0.0000, 0.0000, 0.0689, 0.0024, // 113 (icf[0])
        0.0000, 0.0094, 0.0590, 0.0016, // 114 (icf[1])
        0.0000, 0.0188, 0.0490, 0.0010, // 115 (icf[2])
        0.0000, 0.0282, 0.0390, 0.0007, // 116 (icf[3])
        0.0006, 0.0377, 0.0288, 0.0000, // 117 (icf[4])
        0.0009, 0.0471, 0.0191, 0.0000, // 118 (icf[5])
        0.0014, 0.0566, 0.0000, 0.0000, // 119 (icf[6])
        0.0027, 0.0000, 0.0000, 0.0000  // 121 (icf[7])
    };

    double tmt6plexIsotopeCorrectionFactors[6*4] =
    {
        //  -2      -1      +1      +2
        0.0000, 0.0000, 0.0851, 0.0026, // 126 (icf[0])
        0.0000, 0.0072, 0.0855, 0.0028, // 127 (icf[1])
        0.0003, 0.0124, 0.0622, 0.0009, // 128 (icf[2])
        0.0005, 0.0167, 0.0633, 0.0013, // 129 (icf[3])
        0.0013, 0.0307, 0.0410, 0.0000, // 130 (icf[4])
        0.0011, 0.0290, 0.0000, 0.0000  // 131 (icf[5])
    };

    typedef boost::multi_array<double, 2> matrix;
    typedef boost::const_multi_array_ref<double, 2> const_matrix_ref;

    // start with an array like:
    // -2    -1     +1     +2
    // 0.000, 0.01, 0.059, 0.002, // 114
    // 0.000, 0.02, 0.056, 0.001, // 115
    // 0.000, 0.03, 0.045, 0.001, // 116
    // 0.001, 0.04, 0.035, 0.001  // 117
    std::unique_ptr<const_matrix_ref> isotopeCorrectionFactorsPtr;

    switch (quantitationMethod.value())
    {
        case QuantitationMethod::ITRAQ4plex: isotopeCorrectionFactorsPtr.reset(new const_matrix_ref(itraq4plexIsotopeCorrectionFactors, boost::extents[4][4])); break;
        case QuantitationMethod::ITRAQ8plex: isotopeCorrectionFactorsPtr.reset(new const_matrix_ref(itraq8plexIsotopeCorrectionFactors, boost::extents[8][4])); break;
        //case QuantitationMethod::TMT6plex: isotopeCorrectionFactorsPtr.reset(new const_matrix_ref(tmt6plexIsotopeCorrectionFactors, boost::extents[6][4])); break;

        case QuantitationMethod::TMT2plex: // TODO: get default values for these
        case QuantitationMethod::TMT10plex:
            return;

        default: return;
    }
    const const_matrix_ref& isotopeCorrectionFactors = *isotopeCorrectionFactorsPtr;

    // convert it to an array like:
    // 1-sum, 0.059, 0.002, 0.000
    // 0.020, 1-sum, 0.056, 0.001
    // 0.000, 0.030, 1-sum, 0.045
    // 0.000, 0.001, 0.040, 1-sum

    // output matrix should have one column for each reporter ion
    matrix isotopeCorrectionMatrix(boost::extents[reporterIons.size()][reporterIons.size()]);
    for (int i = 0; i < (int) reporterIons.size(); ++i)
    {
        const auto& icf = isotopeCorrectionFactors[i];
        double totalImpurity = 0;
        if (i - 2 >= 0) { isotopeCorrectionMatrix[i][i - 2] = icf[0]; totalImpurity += icf[0]; }
        if (i - 1 >= 0) { isotopeCorrectionMatrix[i][i - 1] = icf[1]; totalImpurity += icf[1]; }
        if (i + 1 < reporterIons.size()) { isotopeCorrectionMatrix[i][i + 1] = icf[2]; totalImpurity += icf[2]; }
        if (i + 2 < reporterIons.size()) { isotopeCorrectionMatrix[i][i + 2] = icf[3]; totalImpurity += icf[3]; }
        isotopeCorrectionMatrix[i][i] = 1 - totalImpurity;
    }

    /*for (auto r : isotopeCorrectionMatrix)
    {
        for (auto c : r)
            cout << " " << c;
        cout << endl;
    }*/

    vector<double> tmpIntensities(spectrumRows.front().reporterIonIntensities.size());

    for (auto& row : spectrumRows)
    {
        std::fill(tmpIntensities.begin(), tmpIntensities.end(), 0);
        for (size_t i = 0; i < reporterIons.size(); ++i)
        {
            for (size_t j = 0; j < reporterIons.size(); ++j)
                tmpIntensities[reporterIons[i].index] += isotopeCorrectionMatrix[i][j] * row.reporterIonIntensities[reporterIons[j].index];
        }
        row.reporterIonIntensities = tmpIntensities;
    }

    std::fill(tmpIntensities.begin(), tmpIntensities.end(), 0);
    for (size_t i = 0; i < reporterIons.size(); ++i)
    {
        for (size_t j = 0; j < reporterIons.size(); ++j)
            tmpIntensities[reporterIons[i].index] += isotopeCorrectionMatrix[i][j] * totalReporterIonIntensities[reporterIons[j].index];
    }
    totalReporterIonIntensities = tmpIntensities;
}

TEST_CASE("Isobaric quantitation isotope purity correction tests") {

    vector<ReporterIon> itraq4plexIons, itraq8plexIons;
    vector<ReporterIon> tmt2plexIons, tmt6plexIons, tmt10plexIons;

    for (int i = 1; i < 5; ++i) itraq4plexIons.push_back(iTRAQ_masses[i]);
    for (int i = 0; i < 8; ++i) itraq8plexIons.push_back(iTRAQ_masses[i]);

    tmt2plexIons += TMT_masses[0], TMT_masses[2];
    tmt6plexIons += TMT_masses[0], TMT_masses[1], TMT_masses[4], TMT_masses[5], TMT_masses[8], TMT_masses[9];
    tmt10plexIons.assign(TMT_masses, TMT_masses + 10);

    SUBCASE("iTRAQ 4-plex") {
        vector<SpectrumRow> rows
        {
            SpectrumRow{ 1, 123.4, vector<double> { 0, 1, 10, 100, 1000, 0, 0, 0 } },
            SpectrumRow{ 2, 123.4, vector<double> { 0, 100, 1000, 10000, 100000, 0, 0, 0 } }
        };
        vector<double> totals{ 0, 101, 1010, 10100, 101000, 0, 0, 0 };
        correctIsotopeImpurities(rows, totals, itraq4plexIons, QuantitationMethod::ITRAQ4plex, nullptr);

        CHECK(rows[0].reporterIonIntensities == ~(vector<double> { 0, 1.729, 15.85, 137.8, 963.01, 0, 0, 0 }));
        CHECK(rows[1].reporterIonIntensities == ~(vector<double> { 0, 172.9, 1585., 13780, 96301., 0, 0, 0 }));
        CHECK(totals == ~(vector<double> { 0, 174.629, 1600.85, 13917.8, 97264, 0, 0, 0 }));
    }

    SUBCASE("iTRAQ 8-plex") {
        vector<SpectrumRow> rows
        {
            SpectrumRow{ 1, 123.4, vector<double> { 1, 10, 100, 1000, 1000, 100, 10, 1 } }
        };
        vector<double> totals{ 1, 10, 100, 1000, 1000, 100, 10, 1 };
        correctIsotopeImpurities(rows, totals, itraq8plexIons, QuantitationMethod::ITRAQ8plex, nullptr);

        CHECK(rows[0].reporterIonIntensities == ~(vector<double> { 1.8577, 16.8094, 143.308, 973.99, 973.54, 141.481, 16.48, 1.2673 }));
        CHECK(totals == ~(vector<double> { 1.8577, 16.8094, 143.308, 973.99, 973.54, 141.481, 16.48, 1.2673 }));
    }

    SUBCASE("TMT 6-plex") {
        vector<SpectrumRow> rows
        {
            SpectrumRow{ 1, 123.4, vector<double> { 1, 0, 10, 0, 100, 0, 1000, 0, 100, 10 } }
        };
        vector<double> totals{ 1, 0, 10, 0, 100, 0, 1000, 0, 100, 10 };
        correctIsotopeImpurities(rows, totals, tmt6plexIons, QuantitationMethod::TMT6plex, nullptr);

        //CHECK(rows[0].reporterIonIntensities == ~(vector<double> { 2.0233, 20.4022, 0, 0, 154.8343, 926.218, 0, 0, 123.94, 13.699 }));
        //CHECK(totals == ~(vector<double> { 2.0233, 20.4022, 0, 0, 154.8343, 926.218, 0, 0, 123.94, 13.699 }));
        CHECK(rows[0].reporterIonIntensities == ~(vector<double> { 1, 0, 10, 0, 100, 0, 1000, 0, 100, 10 }));
        CHECK(totals == ~(vector<double> { 1, 0, 10, 0, 100, 0, 1000, 0, 100, 10 }));
    }

    SUBCASE("TMT 2-plex") {
        vector<SpectrumRow> rows
        {
            SpectrumRow{ 1, 123.4, vector<double> { 1, 0, 10, 0, 0, 0, 0, 0, 0, 0 } }
        };
        vector<double> totals{ 1, 0, 10, 0, 0, 0, 0, 0, 0, 0 };
        correctIsotopeImpurities(rows, totals, tmt2plexIons, QuantitationMethod::TMT2plex, nullptr);

        CHECK(rows[0].reporterIonIntensities == ~(vector<double> { 1, 0, 10, 0, 0, 0, 0, 0, 0, 0 }));
        CHECK(totals == ~(vector<double> { 1, 0, 10, 0, 0, 0, 0, 0, 0, 0 }));
    }

    SUBCASE("TMT 10-plex") {
        vector<SpectrumRow> rows
        {
            SpectrumRow{ 1, 123.4, vector<double> { 1, 10, 100, 1000, 10000, 10000, 1000, 100, 10, 1 } }
        };
        vector<double> totals{ 1, 10, 100, 1000, 10000, 10000, 1000, 100, 10, 1 };
        correctIsotopeImpurities(rows, totals, tmt10plexIons, QuantitationMethod::TMT10plex, nullptr);

        CHECK(rows[0].reporterIonIntensities == ~(vector<double> { 1, 10, 100, 1000, 10000, 10000, 1000, 100, 10, 1 }));
        CHECK(totals == ~(vector<double> { 1, 10, 100, 1000, 10000, 10000, 1000, 100, 10, 1 }));
    }
}

struct SpectrumList_Quantifier
{
    map<int, int> totalSpectraByMSLevel;
    map<int, double> totalIonCurrentByMSLevel;
    vector<SpectrumRow> spectrumQuantitationRows;

    SpectrumList_Quantifier(const SpectrumListPtr& sl, const IntegerSet& filteredIndexes,
                            const map<string, int>& rowIdByNativeID,
                            QuantitationConfiguration quantitationConfig)
        : rowIdByNativeID(rowIdByNativeID),
          quantitationMethod(quantitationConfig.quantitationMethod),
          tolerance(quantitationConfig.reporterIonMzTolerance)
    {
        for (int i = 1; i < 5; ++i) itraq4plexIons.push_back(iTRAQ_masses[i]);
        for (int i = 0; i < 8; ++i) itraq8plexIons.push_back(iTRAQ_masses[i]);

        tmt2plexIons += TMT_masses[0], TMT_masses[2];
        tmt6plexIons += TMT_masses[0], TMT_masses[1], TMT_masses[4], TMT_masses[5], TMT_masses[8], TMT_masses[9];
        tmt10plexIons.assign(TMT_masses, TMT_masses+10);

        itraqReporterIonIntensities.resize(8);
        itraqTotalReporterIonIntensities.resize(8, 0);
        tmtReporterIonIntensities.resize(10);
        tmtTotalReporterIonIntensities.resize(10, 0);

        if (quantitationMethod == QuantitationMethod::None || quantitationMethod == QuantitationMethod::LabelFree)
            return;

        for (size_t i=0, end=sl->size(); i < end; ++i)
        {
            SpectrumPtr s = sl->spectrum(i, true);

            int msLevel = s->cvParam(MS_ms_level).valueAs<int>();
            BinaryDataArrayPtr mz = s->getMZArray();
            BinaryDataArrayPtr intensities = s->getIntensityArray();
            if (msLevel == 0 || !mz.get() || !intensities.get())
                continue;

            double tic = s->cvParam(MS_TIC).valueAs<double>();
            if (tic == 0.0)
                tic = accumulate(intensities->data.begin(), intensities->data.end(), 0.0);

            ++totalSpectraByMSLevel[msLevel];
            totalIonCurrentByMSLevel[msLevel] += tic;

            if (msLevel > 1 && filteredIndexes.contains(i))
            {
                if (quantitationMethod != QuantitationMethod::None && quantitationMethod != QuantitationMethod::LabelFree)
                {
                    switch (quantitationMethod.value())
                    {
                        case QuantitationMethod::ITRAQ4plex:
                            findReporterIons(s->id, mz->data, intensities->data, itraq4plexIons, itraqReporterIonIntensities, itraqTotalReporterIonIntensities);
                            break;

                        case QuantitationMethod::ITRAQ8plex:
                            findReporterIons(s->id, mz->data, intensities->data, itraq8plexIons, itraqReporterIonIntensities, itraqTotalReporterIonIntensities);
                            break;

                        case QuantitationMethod::TMT2plex:
                            findReporterIons(s->id, mz->data, intensities->data, tmt2plexIons, tmtReporterIonIntensities, tmtTotalReporterIonIntensities);
                            break;

                        case QuantitationMethod::TMT6plex:
                            findReporterIons(s->id, mz->data, intensities->data, tmt6plexIons, tmtReporterIonIntensities, tmtTotalReporterIonIntensities);
                            break;

                        case QuantitationMethod::TMT10plex:
                            findReporterIons(s->id, mz->data, intensities->data, tmt10plexIons, tmtReporterIonIntensities, tmtTotalReporterIonIntensities);
                            break;

                        default: break;
                    }
                }
            }
        }

        // correct isotope impurities and optionally normalize reporter ion intensities to the total for each channel
        switch (quantitationMethod.value())
        {
            case QuantitationMethod::ITRAQ4plex:
                //correctIsotopeImpurities(spectrumQuantitationRows, itraqTotalReporterIonIntensities, itraq4plexIons, quantitationMethod, nullptr);
                if (quantitationConfig.normalizeIntensities)
                    normalizeReporterIons(itraqTotalReporterIonIntensities);
                break;

            case QuantitationMethod::ITRAQ8plex:
                //correctIsotopeImpurities(spectrumQuantitationRows, itraqTotalReporterIonIntensities, itraq8plexIons, quantitationMethod, nullptr);
                if (quantitationConfig.normalizeIntensities)
                    normalizeReporterIons(itraqTotalReporterIonIntensities);
                break;

            case QuantitationMethod::TMT2plex:
                //correctIsotopeImpurities(spectrumQuantitationRows, tmtTotalReporterIonIntensities, tmt2plexIons, quantitationMethod, nullptr);
                if (quantitationConfig.normalizeIntensities)
                    normalizeReporterIons(tmtTotalReporterIonIntensities);
                break;

            case QuantitationMethod::TMT6plex:
                //correctIsotopeImpurities(spectrumQuantitationRows, tmtTotalReporterIonIntensities, tmt6plexIons, quantitationMethod, nullptr);
                if (quantitationConfig.normalizeIntensities)
                    normalizeReporterIons(tmtTotalReporterIonIntensities);
                break;

            case QuantitationMethod::TMT10plex:
                //correctIsotopeImpurities(spectrumQuantitationRows, tmtTotalReporterIonIntensities, tmt10plexIons, quantitationMethod, nullptr);
                if (quantitationConfig.normalizeIntensities)
                    normalizeReporterIons(tmtTotalReporterIonIntensities);
                break;

            default: break;
        }
    }

    private:

    void findReporterIons(const string& nativeID, const vector<double>& mzArray, const vector<double>& intensityArray,
                          const vector<ReporterIon>& reporterIonMZs, vector<double>& reporterIonIntensities,
                          vector<double>& totalReporterIonIntensities)
    {
        std::fill(reporterIonIntensities.begin(), reporterIonIntensities.end(), 0);

        vector<ReporterIon>::const_iterator begin = reporterIonMZs.begin(),end = reporterIonMZs.end(), itr = begin;
        for (size_t i=0; i < mzArray.size(); ++i)
        {
            if (mzArray[i] + tolerance < itr->mass)
                continue;
            else if (mzArray[i] - tolerance > itr->mass)
            {
                ++itr;
                if (itr == end)
                    break;
                --i;
            }
            else
            {
                // use the highest intensity in the tolerance window
                double& currentIonIntensity = reporterIonIntensities[itr->index];
                currentIonIntensity = max(intensityArray[i], currentIonIntensity);
            }
        }

        for (size_t i=0; i < reporterIonIntensities.size(); ++i)
            totalReporterIonIntensities[i] += reporterIonIntensities[i];

        map<string, int>::const_iterator findItr = rowIdByNativeID.find(nativeID);
        if (findItr == rowIdByNativeID.end())
            throw runtime_error("[findReporterIons] nativeID '" + nativeID + "' not found");

        spectrumQuantitationRows.push_back(SpectrumRow());
        spectrumQuantitationRows.back().id = findItr->second;
        spectrumQuantitationRows.back().precursorMz = 0; // TODO
        spectrumQuantitationRows.back().reporterIonIntensities = reporterIonIntensities;
    }

    void normalizeReporterIons(const vector<double>& totalReporterIonIntensities)
    {
        double maxChannelTotal = *std::max_element(totalReporterIonIntensities.begin(), totalReporterIonIntensities.end());
        vector<double> channelCorrectionFactors(totalReporterIonIntensities.size());
        for(size_t j=0; j < totalReporterIonIntensities.size(); ++j)
            if (totalReporterIonIntensities[j] > 0)
                channelCorrectionFactors[j] = maxChannelTotal / totalReporterIonIntensities[j];

        for (size_t i=0; i < spectrumQuantitationRows.size(); ++i)
            for(size_t j=0; j < channelCorrectionFactors.size(); ++j)
                spectrumQuantitationRows[i].reporterIonIntensities[j] *= channelCorrectionFactors[j];
    }

    const map<string, int>& rowIdByNativeID;
    const QuantitationMethod quantitationMethod;
    MZTolerance tolerance;
    vector<ReporterIon> itraq4plexIons, itraq8plexIons;
    vector<ReporterIon> tmt2plexIons, tmt6plexIons, tmt10plexIons;
    vector<double> itraqReporterIonIntensities, itraqTotalReporterIonIntensities;
    vector<double> tmtReporterIonIntensities, tmtTotalReporterIonIntensities;
};

struct SpectrumList_FilterPredicate_ScanStartTimeUpdater : public SpectrumList_Filter::Predicate
{
    SpectrumList_FilterPredicate_ScanStartTimeUpdater(sqlite::database& idpDb,
                                                      int sourceId,
                                                      const map<string, int>& rowIdByNativeID)
        : idpDb(idpDb),
          sourceId(sourceId),
          rowIdByNativeID(rowIdByNativeID),
          updateScanTime(idpDb, "UPDATE Spectrum SET ScanTimeInSeconds = ? WHERE Source = ? AND Id = ?")
    {
        try
        {
            sqlite::query(idpDb, "SELECT Id FROM UnfilteredSpectrum LIMIT 1").begin();
            updateUnfilteredScanTime.reset(new sqlite::command(idpDb, "UPDATE UnfilteredSpectrum SET ScanTimeInSeconds = ? WHERE Source = ? AND Id = ?"));
            hasUnfilteredTables = true;
        }
        catch (sqlite::database_error&)
        {
            hasUnfilteredTables = false;
        }
    }

    virtual boost::logic::tribool accept(const SpectrumIdentity& spectrumIdentity) const
    {
        return boost::logic::indeterminate;
    }

    virtual boost::logic::tribool accept(const Spectrum& spectrum) const
    {
        if (spectrum.scanList.scans.empty())
            return boost::logic::indeterminate;

        double scanTime = spectrum.scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds();

        map<string, int>::const_iterator findItr = rowIdByNativeID.find(spectrum.id);
        if (findItr == rowIdByNativeID.end())
            throw runtime_error("[findReporterIons] nativeID '" + spectrum.id + "' not found");

        updateScanTime.bind(1, scanTime);
        updateScanTime.bind(2, sourceId);
        updateScanTime.bind(3, findItr->second);
        updateScanTime.execute();
        updateScanTime.reset();

        if (hasUnfilteredTables)
        {
            updateUnfilteredScanTime->bind(1, scanTime);
            updateUnfilteredScanTime->bind(2, sourceId);
            updateUnfilteredScanTime->bind(3, findItr->second);
            updateUnfilteredScanTime->execute();
            updateUnfilteredScanTime->reset();
        }

        return true;
    }

    virtual string describe() const { return "updating scan times"; }

    private:
    sqlite::database& idpDb;
    int sourceId;
    const map<string, int>& rowIdByNativeID;
    mutable sqlite::command updateScanTime;
    mutable boost::scoped_ptr<sqlite::command> updateUnfilteredScanTime;
    bool hasUnfilteredTables;
};

} // namespace

void embed(const string& idpDbFilepath,
           const string& sourceSearchPath,
           const map<int, QuantitationConfiguration>& quantitationMethodBySource,
           pwiz::util::IterationListenerRegistry* ilr)
{
    embed(idpDbFilepath, sourceSearchPath, defaultSourceExtensionPriorityList(), quantitationMethodBySource, ilr);
}

void embed(const string& idpDbFilepath,
           const string& sourceSearchPath,
           const string& sourceExtensionPriorityList,
           const map<int, QuantitationConfiguration>& quantitationMethodBySource,
           pwiz::util::IterationListenerRegistry* ilr)
{
    sqlite::database idpDb;

    // get a list of sources from the database
    vector<SpectrumSource> sources;
    try
    {
        getSources(idpDb, sources, idpDbFilepath, sourceSearchPath, sourceExtensionPriorityList, quantitationMethodBySource, ilr);
    }
    catch (runtime_error& e)
    {
        throw runtime_error(string("[embed] ") + e.what());
    }

    ExtendedReaderList readerList;

    for(size_t i=0; i < sources.size(); ++i)
    {
        SpectrumSource& source = sources[i];

        string sourceFilename = bfs::path(source.filepath).filename().string();

        ITERATION_UPDATE(ilr, i, sources.size(), "opening source \"" + sourceFilename + "\"");

        MSDataFile msd(source.filepath, &readerList);

        if (!msd.run.spectrumListPtr.get())
            throw runtime_error("[embed] null spectrum list in \"" + sourceFilename + "\"");

        ITERATION_UPDATE(ilr, i, sources.size(), "filtering spectra from \"" + sourceFilename + "\"");

        // create a filtered spectrum list
        IntegerSet filteredIndexes;
        map<string, int> rowIdByNativeID;
        const SpectrumList& sl = *msd.run.spectrumListPtr;
        for (size_t j=0; j < source.spectrumIds.size(); ++j)
        {
            const string& nativeID = source.spectrumNativeIds[j];
            size_t index = sl.find(nativeID);
            if (index == sl.size())
                throw runtime_error("[embed] nativeID '" + nativeID + "' not found in \"" + sourceFilename + "\"");
            filteredIndexes.insert((int) index);
            rowIdByNativeID[sl.spectrumIdentity(index).id] = source.spectrumIds[j];
        }

        sqlite::transaction transaction(idpDb);

        scoped_ptr<SpectrumList_Quantifier> slq;

        QuantitationConfiguration newQuantitationConfig;

        if (quantitationMethodBySource.count(source.id) > 0)
            newQuantitationConfig = quantitationMethodBySource.find(source.id)->second;
        else if (quantitationMethodBySource.count(0) > 0)
            newQuantitationConfig = quantitationMethodBySource.find(0)->second; // applies to all sources

        // until previously used quantitation settings (e.g. reporter ion tolerance) are stored, we must always redo the quantitation
        //if (newQuantitationConfig.quantitationMethod != source.quantitationMethod)
        {
            ITERATION_UPDATE(ilr, i, sources.size(), "gathering quantitation data from \"" + sourceFilename + "\"");
            slq.reset(new SpectrumList_Quantifier(msd.run.spectrumListPtr, filteredIndexes, rowIdByNativeID, newQuantitationConfig));
        }

        ITERATION_UPDATE(ilr, i, sources.size(), "embedding scan times for \"" + sourceFilename + "\"");

        SpectrumList_FilterPredicate_IndexSet slfp(filteredIndexes);
        SpectrumList_FilterPredicate_ScanStartTimeUpdater slstu(idpDb, source.id, rowIdByNativeID);
        SpectrumDataFilterPtr sdf(new ThresholdFilter(ThresholdFilter::ThresholdingBy_Count, 150.));
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slfp));
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slstu));
        msd.run.spectrumListPtr.reset(new SpectrumList_PeakFilter(msd.run.spectrumListPtr, sdf));

        ITERATION_UPDATE(ilr, i, sources.size(), "creating subset spectra of \"" + sourceFilename + "\"");

        // write a subset mz5 file
        string tmpFilepath = bfs::unique_path("%%%%%%%%.mz5").string();
        MSDataFile::WriteConfig config(MSDataFile::Format_MZ5);
        config.binaryDataEncoderConfig.precisionOverrides[pwiz::cv::MS_intensity_array] = BinaryDataEncoder::Precision_32;
        config.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
        msd.write(tmpFilepath, config);

        // read entire file into memory
        std::ifstream tmpFile(tmpFilepath.c_str(), ios::binary|ios::ate);
        if (!tmpFile)
            throw runtime_error("[embed] error opening temporary file at \"" + tmpFilepath + "\"");

        streamsize tmpSize = tmpFile.tellg();
        char* tmpBuffer = new char[tmpSize];
        tmpFile.seekg(0, ios::beg);
        tmpFile.read(tmpBuffer, tmpSize);
        tmpFile.close();
        bfs::remove(tmpFilepath);

        ITERATION_UPDATE(ilr, i, sources.size(), "embedding subset spectra for \"" + source.name + "\"");

        // embed the file as a blob in the database
        sqlite::command cmd(idpDb, "UPDATE SpectrumSourceMetadata SET MsDataBytes = ? WHERE Id = ?");
        cmd.bind(1, static_cast<void*>(tmpBuffer), tmpSize);
        cmd.bind(2, source.id);
        cmd.execute();
        cmd.reset();

        try { idpDb.execute("DELETE FROM SpectrumQuantitation WHERE Id IN (SELECT Id FROM UnfilteredSpectrum WHERE Source=" + lexical_cast<string>(source.id) + ")"); }
        catch (sqlite::database_error&) { idpDb.execute("DELETE FROM SpectrumQuantitation WHERE Id IN (SELECT Id FROM Spectrum WHERE Source=" + lexical_cast<string>(source.id) + ")"); }

        /*if (newQuantitationConfig.quantitationMethod == source.quantitationMethod)
        {
            transaction.commit();
            continue;
        }*/

        if (newQuantitationConfig.quantitationMethod != QuantitationMethod::None &&
            newQuantitationConfig.quantitationMethod != QuantitationMethod::LabelFree)
        {
            ITERATION_UPDATE(ilr, i, sources.size(), "adding spectrum quantitation data for \"" + source.name + "\"");

            scoped_ptr<sqlite::command> insertSpectrumQuantitation;
            if (newQuantitationConfig.quantitationMethod == QuantitationMethod::ITRAQ4plex ||
                newQuantitationConfig.quantitationMethod == QuantitationMethod::ITRAQ8plex)
                insertSpectrumQuantitation.reset(new sqlite::command(idpDb, "INSERT INTO SpectrumQuantitation (Id, iTRAQ_ReporterIonIntensities) VALUES (?,?)"));
            else if (newQuantitationConfig.quantitationMethod == QuantitationMethod::TMT2plex ||
                     newQuantitationConfig.quantitationMethod == QuantitationMethod::TMT6plex ||
                     newQuantitationConfig.quantitationMethod == QuantitationMethod::TMT10plex)
                insertSpectrumQuantitation.reset(new sqlite::command(idpDb, "INSERT INTO SpectrumQuantitation (Id, TMT_ReporterIonIntensities) VALUES (?,?)"));
            else
                throw runtime_error("[embed] unhandled QuantitationMethod");

            for (size_t i=0; i < slq->spectrumQuantitationRows.size(); ++i)
            {
                SpectrumRow& row = slq->spectrumQuantitationRows[i];
                insertSpectrumQuantitation->bind(1, row.id);
                insertSpectrumQuantitation->bind(2, static_cast<void*>(&row.reporterIonIntensities[0]), row.reporterIonIntensities.size() * sizeof(double));
                insertSpectrumQuantitation->execute();
                insertSpectrumQuantitation->reset();
            }
        }

        // populate the source statistics
        sqlite::command cmd2(idpDb, "UPDATE SpectrumSource SET "
                                    "TotalSpectraMS1 = ?, TotalIonCurrentMS1 = ?, "
                                    "TotalSpectraMS2 = ?, TotalIonCurrentMS2 = ?, "
                                    "QuantitationMethod = ? "
                                    "WHERE Id = ?");
        cmd2.binder() << slq->totalSpectraByMSLevel[1] << slq->totalIonCurrentByMSLevel[1] <<
                         slq->totalSpectraByMSLevel[2] << slq->totalIonCurrentByMSLevel[2] <<
                         newQuantitationConfig.quantitationMethod.value() <<
                         source.id;
        cmd2.execute();
        cmd2.reset();

        transaction.commit();
    }
}


void embedScanTime(const string& idpDbFilepath,
                   const string& sourceSearchPath,
                   const map<int, QuantitationConfiguration>& quantitationMethodBySource,
                   pwiz::util::IterationListenerRegistry* ilr)
{
    embedScanTime(idpDbFilepath, sourceSearchPath, defaultSourceExtensionPriorityList(), quantitationMethodBySource, ilr);
}

void embedScanTime(const string& idpDbFilepath,
                   const string& sourceSearchPath,
                   const string& sourceExtensionPriorityList,
                   const map<int, QuantitationConfiguration>& quantitationMethodBySource,
                   pwiz::util::IterationListenerRegistry* ilr)
{
    sqlite::database idpDb;

    // get a list of sources from the database
    vector<SpectrumSource> sources;
    try
    {
        getSources(idpDb, sources, idpDbFilepath, sourceSearchPath, sourceExtensionPriorityList, quantitationMethodBySource, ilr);
    }
    catch (runtime_error& e)
    {
        throw runtime_error(string("[embedScanTime] ") + e.what());
    }

    ExtendedReaderList readerList;

    for(size_t i=0; i < sources.size(); ++i)
    {
        SpectrumSource& source = sources[i];

        string sourceFilename = bfs::path(source.filepath).filename().string();

        ITERATION_UPDATE(ilr, i, sources.size(), "opening source \"" + sourceFilename + "\"");

        MSDataFile msd(source.filepath, &readerList);

        if (!msd.run.spectrumListPtr.get())
            throw runtime_error("[embedScanTime] null spectrum list in \"" + sourceFilename + "\"");

        ITERATION_UPDATE(ilr, i, sources.size(), "filtering spectra from \"" + sourceFilename + "\"");

        // create a filtered spectrum list
        IntegerSet filteredIndexes;
        map<string, int> rowIdByNativeID;
        const SpectrumList& sl = *msd.run.spectrumListPtr;
        for (size_t j=0; j < source.spectrumIds.size(); ++j)
        {
            const string& nativeID = source.spectrumNativeIds[j];
            size_t index = sl.find(nativeID);
            if (index == sl.size())
                throw runtime_error("[embed] nativeID '" + nativeID + "' not found in \"" + sourceFilename + "\"");
            filteredIndexes.insert((int) index);
            rowIdByNativeID[sl.spectrumIdentity(index).id] = source.spectrumIds[j];
        }

        sqlite::transaction transaction(idpDb);

        scoped_ptr<SpectrumList_Quantifier> slq;

        QuantitationConfiguration newQuantitationConfig;

        if (quantitationMethodBySource.count(source.id) > 0)
            newQuantitationConfig = quantitationMethodBySource.find(source.id)->second;
        else if (quantitationMethodBySource.count(0) > 0)
            newQuantitationConfig = quantitationMethodBySource.find(0)->second; // applies to all sources

        // until previously used quantitation settings (e.g. reporter ion tolerance) are stored, we must always redo the quantitation
        //if (newQuantitationConfig.quantitationMethod != source.quantitationMethod)
        {
            ITERATION_UPDATE(ilr, i, sources.size(), "gathering quantitation data from \"" + sourceFilename + "\"");
            slq.reset(new SpectrumList_Quantifier(msd.run.spectrumListPtr, filteredIndexes, rowIdByNativeID, newQuantitationConfig));
        }

        ITERATION_UPDATE(ilr, i, sources.size(), "embedding scan times for \"" + sourceFilename + "\"");

        SpectrumList_FilterPredicate_IndexSet slfp(filteredIndexes);
        SpectrumList_FilterPredicate_ScanStartTimeUpdater slstu(idpDb, source.id, rowIdByNativeID);
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slfp));
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slstu));

        try { idpDb.execute("DELETE FROM SpectrumQuantitation WHERE Id IN (SELECT Id FROM UnfilteredSpectrum WHERE Source=" + lexical_cast<string>(source.id) + ")"); }
        catch (sqlite::database_error&) { idpDb.execute("DELETE FROM SpectrumQuantitation WHERE Id IN (SELECT Id FROM Spectrum WHERE Source=" + lexical_cast<string>(source.id) + ")"); }

        //if (newQuantitationConfig.quantitationMethod == source.quantitationMethod)
        //    continue;

        if (newQuantitationConfig.quantitationMethod != QuantitationMethod::None &&
            newQuantitationConfig.quantitationMethod != QuantitationMethod::LabelFree)
        {
            ITERATION_UPDATE(ilr, i, sources.size(), "adding spectrum quantitation data for \"" + source.name + "\"");

            scoped_ptr<sqlite::command> insertSpectrumQuantitation;
            if (newQuantitationConfig.quantitationMethod == QuantitationMethod::ITRAQ4plex ||
                newQuantitationConfig.quantitationMethod == QuantitationMethod::ITRAQ8plex)
                insertSpectrumQuantitation.reset(new sqlite::command(idpDb, "INSERT INTO SpectrumQuantitation (Id, iTRAQ_ReporterIonIntensities) VALUES (?,?)"));
            else if (newQuantitationConfig.quantitationMethod == QuantitationMethod::TMT2plex ||
                     newQuantitationConfig.quantitationMethod == QuantitationMethod::TMT6plex ||
                     newQuantitationConfig.quantitationMethod == QuantitationMethod::TMT10plex)
                insertSpectrumQuantitation.reset(new sqlite::command(idpDb, "INSERT INTO SpectrumQuantitation (Id, TMT_ReporterIonIntensities) VALUES (?,?)"));
            else
                throw runtime_error("[embed] unhandled QuantitationMethod");

            for (size_t i=0; i < slq->spectrumQuantitationRows.size(); ++i)
            {
                SpectrumRow& row = slq->spectrumQuantitationRows[i];
                insertSpectrumQuantitation->bind(1, row.id);
                insertSpectrumQuantitation->bind(2, static_cast<void*>(&row.reporterIonIntensities[0]), row.reporterIonIntensities.size() * sizeof(double));
                insertSpectrumQuantitation->execute();
                insertSpectrumQuantitation->reset();
            }
        }

        // populate the source statistics
        sqlite::command cmd(idpDb, "UPDATE SpectrumSource SET "
                                   "TotalSpectraMS1 = ?, TotalIonCurrentMS1 = ?, "
                                   "TotalSpectraMS2 = ?, TotalIonCurrentMS2 = ?, "
                                   "QuantitationMethod = ?, QuantitationSettings = ? "
                                   "WHERE Id = ?");
        cmd.binder() << slq->totalSpectraByMSLevel[1] << slq->totalIonCurrentByMSLevel[1] <<
                        slq->totalSpectraByMSLevel[2] << slq->totalIonCurrentByMSLevel[2] <<
                        newQuantitationConfig.quantitationMethod.value() <<
                        (string) newQuantitationConfig <<
                        source.id;
        cmd.execute();
        cmd.reset();

        transaction.commit();
    }
}


namespace {

int channelsByQuantitationMethod(QuantitationMethod method)
{
    switch (method.value())
    {
        case QuantitationMethod::ITRAQ4plex: return 4;
        case QuantitationMethod::ITRAQ8plex: return 8;
        case QuantitationMethod::TMT2plex: return 2;
        case QuantitationMethod::TMT6plex: return 6;
        case QuantitationMethod::TMT10plex: return 10;
        default: throw runtime_error("[channelsByQuantitationMethod] unhandled QuantitationMethod");
    }
}

} // namespace


void embedIsobaricSampleMapping(const string& idpDbFilepath, const map<string, vector<string> >& isobaricSampleMap)
{
    sqlite3pp::database idpDb(idpDbFilepath);

    map<string, sqlite3_int64> sourceGroupIdByName;
    map<sqlite3_int64, QuantitationMethod> sourceGroupQuantitationMethod;

    sqlite3pp::query sourceGroupByNameQuery(idpDb, "SELECT ssg.Id, ssg.Name, COUNT(DISTINCT ss.QuantitationMethod), ss.QuantitationMethod "
                                                   "FROM SpectrumSource ss, SpectrumSourceGroup ssg "
                                                   "WHERE ss.Group_=ssg.Id AND ss.QuantitationMethod > 0 "
                                                   "GROUP BY ssg.Id");
    for(sqlite3pp::query::rows queryRow : sourceGroupByNameQuery)
    {
        if (queryRow.get<int>(2) > 1)
            throw runtime_error("[embedIsobaricSampleMapping] source group '" + queryRow.get<string>(1) + "' uses more than one quantitation method (each group must use a single quantitation method)");

        sourceGroupIdByName[queryRow.get<string>(1)] = queryRow.get<sqlite3_int64>(0);
        sourceGroupQuantitationMethod[queryRow.get<sqlite3_int64>(0)] = QuantitationMethod::get_by_index(queryRow.get<int>(3)).get();
    }

    idpDb.execute("DROP TABLE IF EXISTS IsobaricSampleMapping;"
                  "CREATE TABLE IsobaricSampleMapping (GroupId INTEGER PRIMARY KEY, Samples TEXT);");
    sqlite3pp::command insertSampleMappingCommand(idpDb, "INSERT INTO IsobaricSampleMapping VALUES (?, ?)");

    BOOST_FOREACH_FIELD((const string& sourceGroup)(const vector<string>& sampleNames), isobaricSampleMap)
    {
        sqlite3_int64 groupId = sourceGroupIdByName[sourceGroup];
        QuantitationMethod quantitationMethod = sourceGroupQuantitationMethod[groupId];
        if (sampleNames.size() != channelsByQuantitationMethod(quantitationMethod))
            throw runtime_error("[embedIsobaricSampleMapping] number of samples (" + lexical_cast<string>(sampleNames.size()) +
                                ") for group " + sourceGroup + " does not match number of channels in the quantitation method (" + lexical_cast<string>(channelsByQuantitationMethod(quantitationMethod)) + ")");
        string sampleNameString = bal::join(sampleNames, ",");
        insertSampleMappingCommand.binder() << groupId << sampleNameString;
        insertSampleMappingCommand.step();
        insertSampleMappingCommand.reset();
    }
}


map<string, vector<string> > getIsobaricSampleMapping(const string& idpDbFilepath)
{
    sqlite3pp::database idpDb(idpDbFilepath);

    sqlite3pp::query isobaricSampleMappingQuery(idpDb, "SELECT ssg.Name, Samples FROM IsobaricSampleMapping ism, SpectrumSourceGroup ssg WHERE GroupId=ssg.Id GROUP BY GroupId");
    map<string, vector<string> > result;

    for(sqlite3pp::query::rows queryRow : isobaricSampleMappingQuery)
    {
        vector<string>& sampleNames = result[queryRow.get<string>(0)];
        string sampleNamesString = queryRow.get<string>(1);
        bal::split(sampleNames, sampleNamesString, bal::is_any_of(","));
    }

    return result;
}


void extract(const string& idpDbFilepath, const string& sourceName, const string& outputFilepath)
{
    // open the database
    sqlite::database idpDb(idpDbFilepath, sqlite::no_mutex);

    // write the associated MSDataBytes to the given filepath
    sqlite::query blobQuery(idpDb, ("SELECT ssmd.MsDataBytes FROM SpectrumSource ss JOIN SpectrumSourceMetadata ssmd ON ss.Id=ssmd.Id AND Name = \"" + sourceName + "\"").c_str());
    for(sqlite::query::rows row : blobQuery)
    {
        const char* bytes = static_cast<const char*>(row.get<const void*>(0));
        int numBytes = row.column_bytes(0);
        ofstream os(outputFilepath.c_str(), ios::binary);
        os.write(bytes, numBytes);
        return;
    }

    throw runtime_error("[extract] source \"" + sourceName + "\" not found in \"" + idpDbFilepath + "\"");
}


bool hasGeneMetadata(const string& idpDbFilepath)
{
    // open the database
    sqlite::database idpDb(idpDbFilepath, sqlite::no_mutex);

    return hasGeneMetadata(idpDb.connected());
}


bool hasGeneMetadata(sqlite3* idpDbConnection)
{
    // open the database
    sqlite::database idpDb(idpDbConnection, false);

    idpDb.execute(IDPICKER_SQLITE_PRAGMA_MMAP);

    // if there is at least 1 non-null GeneId, there is embedded gene metadata
    return sqlite::query(idpDb, "SELECT COUNT(*) FROM Protein WHERE GeneId IS NOT NULL").begin()->get<int>(0) > 0;
}


void embedGeneMetadata(const string& idpDbFilepath, pwiz::util::IterationListenerRegistry* ilr)
{
    if (!bfs::exists("gene2protein.db3"))
        throw runtime_error("[loadGeneMetadata] gene2protein.db3 not found: download it from http://fenchurch.mc.vanderbilt.edu/bin/g2p/gene2protein.db3 and put it in the IDPicker directory.");

    using namespace boost::xpressive;
    sregex refseqRegex = sregex::compile("^(?:generic\\|)?(?:gi\\|\\d+\\|ref\\|)?([NXY]P_\\d+)(?:\\.\\d+)?.*");
    sregex ensemblRegex = sregex::compile("^(?:generic\\|)?(ENSP[A-Za-z0-9]+).*");
    sregex swissProtRegex = sregex::compile("^(?:generic\\|)?(?:sp\\|)?([A-Za-z0-9]+).*");
    sregex lrgRegex = sregex::compile("^(?:generic\\|)?(LRG_[A-Za-z0-9]+)");

    typedef boost::tuple<string, string, string, int, boost::optional<string>, boost::optional<string> > GeneTuple;
    typedef map<int, GeneTuple> GeneMap;
    typedef map<string, GeneMap::const_iterator> ProteinToGeneMap;
    GeneMap geneMap;
    ProteinToGeneMap proteinToGeneMap;
    {
        sqlite::database g2pDb("gene2protein.db3");
        sqlite::query geneQuery(g2pDb, "SELECT GeneId, ApprovedId, ApprovedName, Chromosome, TaxonId, GeneFamily, GeneDescription FROM Gene");
        sqlite::query refseqQuery(g2pDb, "SELECT RefseqProteinAccession, GeneId FROM Refseq");
        sqlite::query ensemblQuery(g2pDb, "SELECT EnsemblProteinAccession, GeneId FROM Ensembl");
        sqlite::query swissProtQuery(g2pDb, "SELECT SwissProtAccession, GeneId FROM SwissProt");
        int geneId, taxonId;
        string approvedId, geneName, chromosome, geneFamily, geneDescription;
        for(sqlite::query::rows row : geneQuery)
        {
            row.getter() >> geneId >> approvedId >> geneName >> chromosome >> taxonId >> geneFamily >> geneDescription;
            geneMap[geneId] = boost::make_tuple(approvedId, geneName, chromosome, taxonId,
                                                boost::make_optional(!geneFamily.empty(), geneFamily),
                                                boost::make_optional(!geneDescription.empty(), geneDescription));
        }

        for (auto* query : {&refseqQuery, &ensemblQuery, &swissProtQuery})
        for (sqlite::query::rows row : *query)
        {

            string accession = row.get<string>(0);
            int geneid = row.get<int>(1);
            if (regex_search(accession, refseqRegex))
            {
                accession = regex_replace(accession, refseqRegex, "$1");
            }
            else if (regex_search(accession, ensemblRegex))
            {
                accession = regex_replace(accession, ensemblRegex, "$1");
            }
            else if (regex_search(accession, swissProtRegex))
            {
                accession = regex_replace(accession, swissProtRegex, "$1");
            }
            else if (!regex_search(accession, lrgRegex))
            {
                throw runtime_error("[loadMetadata] gene2protein.db3 accession " + accession + " does not match one of the supported accession formats");
            }
            auto findItr = geneMap.find(geneid);
            if (findItr == geneMap.end())
                throw runtime_error("[loadMetadata] gene2protein.db3 geneid " + lexical_cast<string>(geneid) + " is in protein table but missing from gene table");

            proteinToGeneMap[accession] = findItr;
        }
    }

    // get existing filter config, if any
    boost::optional<Filter::Config> currentConfig = Filter::currentConfig(idpDbFilepath);

    // drop filtered tables and update schema if necessary
    Qonverter::dropFilters(idpDbFilepath, ilr);

    // open the database
    sqlite::database idpDb(idpDbFilepath, sqlite::no_mutex);

    idpDb.execute("PRAGMA journal_mode=OFF;"
                  "PRAGMA synchronous=OFF;"
                  "PRAGMA cache_size=50000;"
                  IDPICKER_SQLITE_PRAGMA_MMAP);

    // reset GeneId column
    idpDb.execute("UPDATE Protein SET GeneId=NULL");

    sqlite::transaction transaction(idpDb);
    sqlite::query proteinIdAccessions(idpDb, "SELECT Id, Accession FROM Protein WHERE IsDecoy=0");
    sqlite::command proteinQuery(idpDb, "UPDATE Protein SET GeneId=? WHERE Id=?");
    sqlite::command proteinMetadataQuery(idpDb, "UPDATE ProteinMetadata SET GeneName=?, Chromosome=?, TaxonomyId=?, GeneFamily=?, GeneDescription=? WHERE Id=?");
    for(sqlite::query::rows row : proteinIdAccessions)
    {
        sqlite3_int64 id = row.get<sqlite3_int64>(0);
        string accession = row.get<string>(1);

        if (regex_search(accession, refseqRegex))
        {
            accession = regex_replace(accession, refseqRegex, "$1");
        }
        else if (regex_search(accession, ensemblRegex))
        {
            accession = regex_replace(accession, ensemblRegex, "$1");
        }
        else if (regex_search(accession, swissProtRegex))
        {
            accession = regex_replace(accession, swissProtRegex, "$1");
        }

        ProteinToGeneMap::const_iterator findItr = proteinToGeneMap.find(accession);
        if (findItr == proteinToGeneMap.end())
            continue;

        const GeneTuple& geneTuple = findItr->second->second;

        proteinQuery.binder() << geneTuple.get<0>() << id;
        proteinQuery.step();
        proteinQuery.reset();

        if (geneTuple.get<4>().is_initialized())
            proteinMetadataQuery.binder() << geneTuple.get<1>() << geneTuple.get<2>() << geneTuple.get<3>() << geneTuple.get<4>().get() << geneTuple.get<5>().get() << id;
        else
            proteinMetadataQuery.binder() << geneTuple.get<1>() << geneTuple.get<2>() << geneTuple.get<3>() << sqlite::ignore << sqlite::ignore << id;
        proteinMetadataQuery.step();
        proteinMetadataQuery.reset();
    }
    idpDb.execute("UPDATE Protein SET GeneId='Unmapped_'||Accession WHERE GeneId IS NULL");
    //idpDb.execute("UPDATE Protein SET GeneId='Unmapped' WHERE GeneId IS NULL");

    // gene-level filters may no longer be valid
    idpDb.execute("DELETE FROM FilterHistory WHERE GeneLevelFiltering = 1");

    transaction.commit();

    // if a filter was previously applied, re-apply it
    if (currentConfig)
    {
        Filter filter;
        filter.config = currentConfig.get();
        filter.filter(idpDb.connected(), ilr);
    }
}


void EmbedMS1Metrics(const string& idpDbFilepath,
                     const string& sourceSearchPath,
                     const string& sourceExtensionPriorityList,
                     const map<int, QuantitationConfiguration>& quantitationMethodBySource,
                     const map<int, XIC::XICConfiguration>& xicConfigBySource,
                     pwiz::util::IterationListenerRegistry* ilr)
{
    sqlite::database idpDb;
    // get a list of sources from the database
    vector<SpectrumSource> sources;
    try
    {
        getSources(idpDb, sources, idpDbFilepath, sourceSearchPath, sourceExtensionPriorityList, quantitationMethodBySource, ilr);
    }
    catch (runtime_error& e)
    {
        throw runtime_error(string("[embed] ") + e.what());
    }
    idpDb.execute("DROP TABLE IF EXISTS XICMetrics; "
                  "DROP TABLE IF EXISTS XICMetricsSettings; "
                  "CREATE TABLE IF NOT EXISTS XICMetrics (Id INTEGER PRIMARY KEY, DistinctMatch INTEGER, SpectrumSource INTEGER, Peptide INTEGER, PeakIntensity NUMERIC, PeakArea NUMERIC, PeakSNR NUMERIC, PeakTimeInSeconds NUMERIC); "
                  "CREATE INDEX IF NOT EXISTS XICMetrics_MatchSourcePeptide ON XICMetrics(DistinctMatch,SpectrumSource,Peptide);");
    for(size_t i=0; i < sources.size(); ++i)
    {
        SpectrumSource& source = sources[i];

        if (quantitationMethodBySource.count((int) source.id) == 0 ||
            quantitationMethodBySource.find((int) source.id)->second.quantitationMethod != QuantitationMethod::LabelFree)            
                    continue;

        XIC::XICConfiguration config;

        if (xicConfigBySource.count(source.id) > 0)
            config = xicConfigBySource.find(source.id)->second;
        else if (xicConfigBySource.count(0) > 0)
            config = xicConfigBySource.find(0)->second; // applies to all sources

        /*int spectraAdded = */XIC::EmbedMS1ForFile(idpDb, idpDbFilepath, source.filepath, lexical_cast<string>(source.id), config, ilr, i, sources.size());
    }
}

void dropGeneMetadata(const string& idpDbFilepath)
{
    // open the database
    sqlite::database idpDb(idpDbFilepath, sqlite::no_mutex);

    // unset gene columns
    idpDb.execute("UPDATE Protein SET GeneId=NULL, GeneGroup=NULL");
    idpDb.execute("UPDATE ProteinMetadata SET TaxonomyId=NULL, GeneName=NULL, Chromosome=NULL, GeneFamily=NULL, GeneDescription=NULL");
}


} // namespace Embedder
END_IDPICKER_NAMESPACE
