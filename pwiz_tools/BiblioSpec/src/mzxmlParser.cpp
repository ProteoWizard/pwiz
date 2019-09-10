#include "BlibException.h"
#include "mzxmlParser.h"
#include "Verbosity.h"
#include "BlibUtils.h" // For IONMOBILITY_TYPE enum
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"

#include <cstdlib>

using namespace BiblioSpec;
using namespace pwiz::msdata;

MzXMLParser::MzXMLParser()
    : lastIndex_(-1) {
}

MzXMLParser::~MzXMLParser() {
}


void MzXMLParser::openFile(const char* filename, bool mzSort) {
    filename_ = filename;
    msd_ = MSDataPtr(new MSDataFile(filename));
}

bool MzXMLParser::getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks) {
    size_t index;
    switch (findBy)
    {
        case SCAN_NUM_ID: index = msd_->run.spectrumListPtr->find("scan=" + boost::lexical_cast<string>(identifier)); break;
        default: index = identifier;  break;
    }

    if (index >= msd_->run.spectrumListPtr->size()) {
        Verbosity::error("index %d out of range in mzXML file '%s'", index, filename_.c_str());
        return false;
    }

    SpectrumPtr s = msd_->run.spectrumListPtr->spectrum(index, true);
    returnData.id = s->index;
    returnData.retentionTime = s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds() / 60;
    returnData.numPeaks = s->defaultArrayLength;

    if (s->precursors.size() > 0 && s->precursors[0].selectedIons.size() > 0)
        returnData.mz = s->precursors[0].selectedIons[0].cvParamValueOrDefault(MS_selected_ion_m_z, 0.0);

    if (s->scanList.scans.size() > 0) {
        CVParam driftTime = s->scanList.scans[0].cvParam(MS_ion_mobility_drift_time);
        if (!driftTime.empty())
        {
            returnData.ionMobility = driftTime.valueAs<float>();
            returnData.ionMobilityType = IONMOBILITY_DRIFTTIME_MSEC;
        }

        UserParam ccs = s->scanList.scans[0].userParam("CCS");
        if (!ccs.empty())
            returnData.ccs = ccs.valueAs<float>();
    }

    returnData.mzs = new double[returnData.numPeaks];
    returnData.intensities = new float[returnData.numPeaks];
    const auto& mzArray = s->getMZArray()->data;
    const auto& intensityArray = s->getIntensityArray()->data;
    for (size_t i = 0; i < returnData.numPeaks; ++i) {
        returnData.mzs[i] = mzArray[i];
        returnData.intensities[i] = intensityArray[i];
    }
    lastIndex_ = index;
    return true;
}

bool MzXMLParser::getSpectrum(string identifier, SpecData& returnData,  bool getPeaks) {
    return getSpectrum((int) msd_->run.spectrumListPtr->find(identifier), returnData, INDEX_ID, getPeaks);
}

bool MzXMLParser::getNextSpectrum(SpecData& returnData, bool getPeaks) {
    bool result = getSpectrum(lastIndex_+1, returnData, INDEX_ID, getPeaks);
    ++lastIndex_;
    return result;
}
