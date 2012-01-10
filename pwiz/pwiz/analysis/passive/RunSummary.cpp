//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#define PWIZ_SOURCE

#include "RunSummary.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


using namespace pwiz::util;
namespace bfs = boost::filesystem;


PWIZ_API_DECL RunSummary::Config::Config(const std::string& args)
:   delimiter(Config::Delimiter_FixedWidth)
{
    istringstream iss(args);
    vector<string> tokens;
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

    msLevels.parse("1-");
    charges.parse("1-");

    const string delimiterArgKey = "delimiter=";
    const string msLevelsArgKey = "msLevels=";
    const string chargesArgKey = "charges=";

    BOOST_FOREACH(const string& arg, tokens)
    {
        if (bal::starts_with(arg, delimiterArgKey))
        {
            string delimiterStr = arg.substr(delimiterArgKey.length());
            if (delimiterStr == "space")
                delimiter = Config::Delimiter_Space;
            else if (delimiterStr == "tab")
                delimiter = Config::Delimiter_Tab;
            else if (delimiterStr == "comma")
                delimiter = Config::Delimiter_Comma;
            else if (delimiterStr != "fixed")
                cerr << "[RunSummary] Invalid delimiter. Must be one of {fixed, space, tab, comma}." << endl;
        }
        else if (bal::starts_with(arg, msLevelsArgKey))
        {
            msLevels = IntegerSet();
            msLevels.parse(arg.substr(msLevelsArgKey.length()));
        }
        else if (bal::starts_with(arg, chargesArgKey))
        {
            charges = IntegerSet();
            charges.parse(arg.substr(chargesArgKey.length()));
        }
        else
            cerr << "[RunSummary] Unknown option: " << arg << endl;
    }
}


PWIZ_API_DECL RunSummary::RunSummary(const MSDataCache& cache, const Config& config)
:   cache_(cache), config_(config)
{}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
RunSummary::updateRequested(const DataInfo& dataInfo, 
                            const SpectrumIdentity& spectrumIdentity) const 
{
    // make sure everything gets cached by MSDataCache, even though we don't 
    // actually look at the update() message

    return UpdateRequest_NoBinary;
}


namespace {

template <typename value_type>
struct DistributionInfo
{
    value_type mean, median;
    value_type min, max;
    value_type Q1, Q2, Q3;

    DistributionInfo()
    : mean(0), median(0), min(0), max(0), Q1(0), Q2(0), Q3(0)
    {}

    template <typename input_iterator>
    DistributionInfo(input_iterator begin, input_iterator end)
    : mean(0), median(0), min(0), max(0), Q1(0), Q2(0), Q3(0)
    {
        std::vector<value_type> sorted_distribution(begin, end);
        if (sorted_distribution.empty())
            return;

        std::sort(sorted_distribution.begin(), sorted_distribution.end());
        min = sorted_distribution.front();
        max = sorted_distribution.back();
        Q1 = sorted_distribution[sorted_distribution.size()/4];
        Q2 = sorted_distribution[sorted_distribution.size()/2];
        Q3 = sorted_distribution[sorted_distribution.size()*3/4];
        median = Q2;
        mean = std::accumulate(sorted_distribution.begin(), sorted_distribution.end(), 0) / sorted_distribution.size();
    }
};

struct InstrumentInfo
{
    string vendorName;
    string modelName;
    string serialNumber;

    InstrumentInfo(const MSData& msd)
    {
        if (!msd.instrumentConfigurationPtrs.empty() &&
            msd.instrumentConfigurationPtrs[0]->hasCVParamChild(MS_instrument_model))
        {
            CVTermInfo model = cvTermInfo(msd.instrumentConfigurationPtrs[0]->cvParamChild(MS_instrument_model).cvid);
            CVTermInfo vendor = model;
            while (!vendor.parentsIsA.empty() &&
                   find(vendor.parentsIsA.begin(), vendor.parentsIsA.end(), MS_instrument_model) == vendor.parentsIsA.end())
                vendor = cvTermInfo(vendor.parentsIsA[0]);

            vendorName = vendor.shortName();
            bal::replace_all(vendorName, " instrument model", "");

            if (vendor.cvid != model.cvid)
                modelName = model.shortName();

            serialNumber = msd.instrumentConfigurationPtrs[0]->cvParam(MS_instrument_serial_number).value;
        }

        if (vendorName.empty()) vendorName = "unknown";
        if (modelName.empty()) modelName = "unknown";
        if (serialNumber.empty()) serialNumber = "unknown";
    }
};

// HACK/TODO: replace with a config-based "format" string approach to set order and static presence of columns
string lastColumnHeaderRow = "";

} // namespace


PWIZ_API_DECL void RunSummary::close(const DataInfo& dataInfo)
{
    char delimiter;

    switch (config_.delimiter)
    {
        default:
        case Config::Delimiter_FixedWidth:
            delimiter = 0;
            break;
        case Config::Delimiter_Space:
            delimiter = ' ';
            break;
        case Config::Delimiter_Comma:
            delimiter = ',';
            break;
        case Config::Delimiter_Tab:
            delimiter = '\t';
            break;
    }

    typedef map<int, int> IntIntMap;
    typedef map<int, vector<size_t> > IntVectorSizeMap;
    typedef map<int, DistributionInfo<int> > IntDistributionInfoMap;
    typedef map<double, double> DoubleDoubleMap;

    IntIntMap msLevelCount;
    IntIntMap knownChargeCount;
    IntVectorSizeMap defaultArrayLengthsByMsLevel;

    int otherMsLevels = 0;
    int knownCharges = 0;
    int otherCharges = 0;
    int zoomScanCount = 0;
    double totalBPI = 0;
    DoubleDoubleMap retentionTimeToBPI;

    IntegerSet::const_iterator itr;

    if (config_.msLevels.size() < (size_t) numeric_limits<int>::max())
        for (itr = config_.msLevels.begin(); itr != config_.msLevels.end(); ++itr)
        {
            msLevelCount[*itr] = 0;
            defaultArrayLengthsByMsLevel[*itr] = vector<size_t>();
        }

    if (config_.charges.size() < (size_t) numeric_limits<int>::max())
        for (itr = config_.charges.begin(); itr != config_.charges.end(); ++itr)
            knownChargeCount[*itr] = 0;

    // accumulate statistics over all spectra
    BOOST_FOREACH(const SpectrumInfo& si, cache_)
    {
        if (config_.msLevels.contains(si.msLevel))
            ++msLevelCount[si.msLevel];
        else
            ++otherMsLevels;

        if (!si.precursors.empty() && si.precursors[0].charge > 0)
        {
            ++knownCharges;
            int charge = (int) si.precursors[0].charge;
            if (config_.charges.contains(charge))
                ++knownChargeCount[charge];
            else
                ++otherCharges;
        }

        if (si.isZoomScan)
            ++zoomScanCount;

        defaultArrayLengthsByMsLevel[si.msLevel].push_back(si.dataSize);
        totalBPI += si.basePeakIntensity;
        retentionTimeToBPI[si.retentionTime] = si.basePeakIntensity;
    }

    // calculate distribution statistics for default array lengths (data point counts)
    IntDistributionInfoMap distributionInfoByMsLevel;
    BOOST_FOREACH(IntVectorSizeMap::const_reference kvp, defaultArrayLengthsByMsLevel)
        distributionInfoByMsLevel[kvp.first] = DistributionInfo<int>(kvp.second.begin(), kvp.second.end());

    double minRT = retentionTimeToBPI.empty() ? 0 : retentionTimeToBPI.begin()->first;
    double maxRT = retentionTimeToBPI.empty() ? 0 : retentionTimeToBPI.rbegin()->first;
    double q1RT = 0, q2RT = 0, q3RT = 0;
    double sumBPI = 0;
    BOOST_FOREACH(DoubleDoubleMap::const_reference kvp, retentionTimeToBPI)
    {
        sumBPI += kvp.second;
        double percentOfTotal = sumBPI / totalBPI;
        if (percentOfTotal > 0.75)
        {
            q3RT = kvp.first;
            break;
        }
        else if (q2RT == 0 && percentOfTotal > 0.5)
            q2RT = kvp.first;
        else if (q1RT == 0 && percentOfTotal > 0.25)
            q1RT = kvp.first;
    }

    // get instrument metadata
    InstrumentInfo instrumentInfo(dataInfo.msd);

    // TODO: support writing direct to a file?
    ostream& os = cout;

    if (delimiter == 0)
    {
        const size_t width_scanCount = 9;
        const size_t width_pointCount = 14;
        const size_t width_retentionTime = 11;
        const size_t width_timeStamp = 22; // 2006-11-12T10:35:43Z
        const size_t width_instrumentMake = 26; // Thermo Fisher Scientific
        const size_t width_instrumentModel = 38; // 6510 Quadrupole Time-of-Flight LC/MS
        const size_t width_instrumentSN = 30; // a wild guess

        os << setfill(' ');

        stringstream columnHeaderRow;
        columnHeaderRow << setfill(' ');

        // write column headers per MS level, e.g. "MS1s  MS2s  MS3s"
        BOOST_FOREACH(IntIntMap::const_reference kvp, msLevelCount)
            if (config_.msLevels.contains(kvp.first))
                columnHeaderRow << setw(width_scanCount) << ("MS" + lexical_cast<string>(kvp.first) + "s");

        if (config_.msLevels.size() < (size_t) numeric_limits<int>::max())
            columnHeaderRow << setw(12) << "MS(others)";

        columnHeaderRow << setw(width_scanCount) << "Zooms";
        columnHeaderRow << setw(width_scanCount) << "Charges";

        // write column headers per charge state, e.g. "+1s  +2s  +3s  +5s  ..."
        BOOST_FOREACH(IntIntMap::const_reference kvp, knownChargeCount)
            if (config_.charges.contains(kvp.first))
                columnHeaderRow << setw(width_scanCount) << ("+" + lexical_cast<string>(kvp.first) + "s");

        if (config_.charges.size() < (size_t) numeric_limits<int>::max())
            columnHeaderRow << setw(11) << "+(others)";

        BOOST_FOREACH(IntDistributionInfoMap::const_reference kvp, distributionInfoByMsLevel)
        {
            if (config_.msLevels.contains(kvp.first))
                columnHeaderRow << setw(width_pointCount) << ("MS" + lexical_cast<string>(kvp.first) + " PtsMean")
                                << setw(width_pointCount) << ("MS" + lexical_cast<string>(kvp.first) + " PtsMin")
                                << setw(width_pointCount) << ("MS" + lexical_cast<string>(kvp.first) + " PtsQ1")
                                << setw(width_pointCount) << ("MS" + lexical_cast<string>(kvp.first) + " PtsQ2")
                                << setw(width_pointCount) << ("MS" + lexical_cast<string>(kvp.first) + " PtsQ3")
                                << setw(width_pointCount) << ("MS" + lexical_cast<string>(kvp.first) + " PtsMax");
        }

        columnHeaderRow << setw(width_retentionTime) << "MinRT";
        columnHeaderRow << setw(width_retentionTime) << "RT@25%BPI";
        columnHeaderRow << setw(width_retentionTime) << "RT@50%BPI";
        columnHeaderRow << setw(width_retentionTime) << "RT@75%BPI";
        columnHeaderRow << setw(width_retentionTime) << "MaxRT";
        columnHeaderRow << setw(width_timeStamp) << "Timestamp";
        columnHeaderRow << setw(width_instrumentMake) << "Vendor";
        columnHeaderRow << setw(width_instrumentModel) << "Model";
        columnHeaderRow << setw(width_instrumentSN) << "Serial#";
        columnHeaderRow << "  Filename";

        // only write the column header if it's different than the last one
        if (columnHeaderRow.str() != lastColumnHeaderRow)
        {
            lastColumnHeaderRow = columnHeaderRow.str();
            os << lastColumnHeaderRow << endl;
        }

        // now write the data row

        // write column per MS level
        BOOST_FOREACH(IntIntMap::const_reference kvp, msLevelCount)
            if (config_.msLevels.contains(kvp.first))
                os << setw(width_scanCount) << kvp.second;

        if (config_.msLevels.size() < (size_t) numeric_limits<int>::max())
            os << setw(12) << otherMsLevels;

        os << setw(width_scanCount) << zoomScanCount;

        os << setw(width_scanCount) << knownCharges;

        // write column per charge state
        BOOST_FOREACH(IntIntMap::const_reference kvp, knownChargeCount)
            if (config_.charges.contains(kvp.first))
                os << setw(width_scanCount) << kvp.second;

        if (config_.charges.size() < (size_t) numeric_limits<int>::max())
            os << setw(11) << otherCharges;

        BOOST_FOREACH(IntDistributionInfoMap::const_reference kvp, distributionInfoByMsLevel)
        {
            if (config_.msLevels.contains(kvp.first))
                os << setw(width_pointCount) << kvp.second.mean
                   << setw(width_pointCount) << kvp.second.min
                   << setw(width_pointCount) << kvp.second.Q1
                   << setw(width_pointCount) << kvp.second.Q2
                   << setw(width_pointCount) << kvp.second.Q3
                   << setw(width_pointCount) << kvp.second.max;
        }

        os << setw(width_retentionTime) << fixed << setprecision(0) << minRT;
        os << setw(width_retentionTime) << fixed << setprecision(0) << q1RT;
        os << setw(width_retentionTime) << fixed << setprecision(0) << q2RT;
        os << setw(width_retentionTime) << fixed << setprecision(0) << q3RT;
        os << setw(width_retentionTime) << fixed << setprecision(0) << maxRT;
        os << setw(width_timeStamp) << dataInfo.msd.run.startTimeStamp;
        os << setw(width_instrumentMake) << instrumentInfo.vendorName;
        os << setw(width_instrumentModel) << instrumentInfo.modelName;
        os << setw(width_instrumentSN) << instrumentInfo.serialNumber;
        os << "  " << dataInfo.sourceFilename;

        os << endl;
    }
    else
    {
        stringstream columnHeaderRow;

        columnHeaderRow << "Filename" << delimiter;
        columnHeaderRow << "Timestamp" << delimiter;
        columnHeaderRow << "Vendor" << delimiter;
        columnHeaderRow << "Model" << delimiter;
        columnHeaderRow << "Serial#" << delimiter;

        // write column headers per MS level, e.g. "MS1s  MS2s  MS3s"
        BOOST_FOREACH(IntIntMap::const_reference kvp, msLevelCount)
            if (config_.msLevels.contains(kvp.first))
                columnHeaderRow << ("MS" + lexical_cast<string>(kvp.first) + "s") << delimiter;

        if (config_.msLevels.size() < (size_t) numeric_limits<int>::max())
            columnHeaderRow << "MS(others)" << delimiter;

        columnHeaderRow << "Zooms" << delimiter;
        columnHeaderRow << "Charges" << delimiter;

        // write column headers per charge state, e.g. "+1s  +2s  +3s  +5s  ..."
        BOOST_FOREACH(IntIntMap::const_reference kvp, knownChargeCount)
            if (config_.charges.contains(kvp.first))
                columnHeaderRow << ("+" + lexical_cast<string>(kvp.first) + "s") << delimiter;

        if (config_.charges.size() < (size_t) numeric_limits<int>::max())
            columnHeaderRow << "+(others)" << delimiter;

        BOOST_FOREACH(IntDistributionInfoMap::const_reference kvp, distributionInfoByMsLevel)
        {
            if (config_.msLevels.contains(kvp.first))
                columnHeaderRow << ("MS" + lexical_cast<string>(kvp.first) + " PtsMean") << delimiter
                                << ("MS" + lexical_cast<string>(kvp.first) + " PtsMin") << delimiter
                                << ("MS" + lexical_cast<string>(kvp.first) + " PtsQ1") << delimiter
                                << ("MS" + lexical_cast<string>(kvp.first) + " PtsQ2") << delimiter
                                << ("MS" + lexical_cast<string>(kvp.first) + " PtsQ3") << delimiter
                                << ("MS" + lexical_cast<string>(kvp.first) + " PtsMax") << delimiter;
        }

        columnHeaderRow << "MinRT" << delimiter;
        columnHeaderRow << "RT@25%BPI" << delimiter;
        columnHeaderRow << "RT@50%BPI" << delimiter;
        columnHeaderRow << "RT@75%BPI" << delimiter;
        columnHeaderRow << "MaxRT";

        // only write the column header if it's different than the last one
        if (columnHeaderRow.str() != lastColumnHeaderRow)
        {
            lastColumnHeaderRow = columnHeaderRow.str();
            os << lastColumnHeaderRow << endl;
        }

        // now write the data row

        os << dataInfo.sourceFilename << delimiter;
        os << dataInfo.msd.run.startTimeStamp << delimiter;
        os << instrumentInfo.vendorName << delimiter;
        os << instrumentInfo.modelName << delimiter;
        os << instrumentInfo.serialNumber << delimiter;

        // write column per MS level
        BOOST_FOREACH(IntIntMap::const_reference kvp, msLevelCount)
            if (config_.msLevels.contains(kvp.first))
                os << kvp.second << delimiter;

        if (config_.msLevels.size() < (size_t) numeric_limits<int>::max())
            os << otherMsLevels << delimiter;

        os << zoomScanCount << delimiter;

        os << knownCharges << delimiter;

        // write column per charge state
        BOOST_FOREACH(IntIntMap::const_reference kvp, knownChargeCount)
            if (config_.charges.contains(kvp.first))
                os << kvp.second << delimiter;

        if (config_.charges.size() < (size_t) numeric_limits<int>::max())
            os << otherCharges << delimiter;

        BOOST_FOREACH(IntDistributionInfoMap::const_reference kvp, distributionInfoByMsLevel)
        {
            if (config_.msLevels.contains(kvp.first))
                os << kvp.second.mean << delimiter
                   << kvp.second.min << delimiter
                   << kvp.second.Q1 << delimiter
                   << kvp.second.Q2 << delimiter
                   << kvp.second.Q3 << delimiter
                   << kvp.second.max << delimiter;
        }

        os << fixed << setprecision(0) << minRT << delimiter;
        os << fixed << setprecision(0) << q1RT << delimiter;
        os << fixed << setprecision(0) << q2RT << delimiter;
        os << fixed << setprecision(0) << q3RT << delimiter;
        os << fixed << setprecision(0) << maxRT << endl;
    }
}


} // namespace analysis 
} // namespace pwiz

