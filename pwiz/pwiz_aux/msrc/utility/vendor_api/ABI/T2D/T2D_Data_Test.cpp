//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "T2D_Data.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::vendor_api::ABI::T2D;


ostream* os_ = 0;


void summarizeMzIntensityPairs(ostream& os, const vector<double>& mz, const vector<double>& intensities, size_t count)
{
    for (size_t i=0; i < count && i < mz.size(); ++i)
        os << '(' << mz[i] << ',' << intensities[i] << ") ";
    os << "... ";
    for (size_t i=count; i > 0; --i)
        os << '(' << mz[mz.size()-i] << ',' << intensities[mz.size()-i] << ") ";
}

#define PRINT_INSTRUMENT_SETTING(setting) \
    {double value = spectrumPtr->getInstrumentSetting(InstrumentSetting_##setting); \
    if (value >= 0) cout << "\t\t" << BOOST_STRINGIZE(setting) << " = " << value << endl;}

#define PRINT_INSTRUMENT_STRING_PARAM(param) \
    {string value = spectrumPtr->getInstrumentStringParam(InstrumentStringParam_##param); \
    if (!value.empty()) cout << "\t\t" << BOOST_STRINGIZE(param) << " = " << value << endl;}

void test(const string& rawpath)
{
    DataPtr dataPtr = Data::create(rawpath);
    size_t spectrumCount = dataPtr->getSpectrumCount();
    cout << "getSpectrumCount: " << spectrumCount << endl;

    for (size_t i=0 ; i < spectrumCount; ++i)
    {
        SpectrumPtr spectrumPtr = dataPtr->getSpectrum(i);
        cout << "Spectrum " << i << " (" << dataPtr->getSpectrumFilenames()[i] << ")" << endl;

        cout << "\tgetType: " << spectrumPtr->getType() << endl;
        cout << "\tgetMsLevel: " << spectrumPtr->getMsLevel() << endl;
        cout << "\tgetPolarity: " << spectrumPtr->getPolarity() << endl;
        cout << "\tgetTIC: " << spectrumPtr->getTIC() << endl;

        cout << "\tInstrumentSettings:" << endl;
        PRINT_INSTRUMENT_SETTING(NozzlePotential);
        PRINT_INSTRUMENT_SETTING(MinimumAnalyzerMass);
        PRINT_INSTRUMENT_SETTING(MaximumAnalyzerMass);
        PRINT_INSTRUMENT_SETTING(Skimmer1Potential);
        PRINT_INSTRUMENT_SETTING(SpectrumXPosAbs);
        PRINT_INSTRUMENT_SETTING(SpectrumYPosAbs);
        PRINT_INSTRUMENT_SETTING(SpectrumXPosRel);
        PRINT_INSTRUMENT_SETTING(SpectrumYPosRel);
        PRINT_INSTRUMENT_SETTING(PulsesAccepted);
        PRINT_INSTRUMENT_SETTING(DigitizerStartTime);
        PRINT_INSTRUMENT_SETTING(DigitzerBinSize);
        PRINT_INSTRUMENT_SETTING(SourcePressure);
        PRINT_INSTRUMENT_SETTING(MirrorPressure);
        PRINT_INSTRUMENT_SETTING(TC2Pressure);
        PRINT_INSTRUMENT_SETTING(PreCursorIon);

        cout << "\tInstrumentStringParams:" << endl;
        PRINT_INSTRUMENT_STRING_PARAM(SampleWell);
        PRINT_INSTRUMENT_STRING_PARAM(PlateID);
        PRINT_INSTRUMENT_STRING_PARAM(InstrumentName);
        PRINT_INSTRUMENT_STRING_PARAM(SerialNumber);
        PRINT_INSTRUMENT_STRING_PARAM(PlateTypeFilename);
        PRINT_INSTRUMENT_STRING_PARAM(LabName);

        double bpmz, bpi;
        spectrumPtr->getBasePeak(bpmz, bpi);
        cout << "\tgetBasePeak: (" << fixed << setprecision(4) << bpmz << ',' << bpi << ')' << endl;

        vector<double> mz, intensities;

        cout << "\tgetPeakDataSize: " << spectrumPtr->getPeakDataSize() << endl;
        cout << "\tgetRawDataSize: " << spectrumPtr->getRawDataSize() << endl;

        spectrumPtr->getPeakData(mz, intensities);
        cout << "\tgetPeakData: ";
        summarizeMzIntensityPairs(cout << fixed << setprecision(2), mz, intensities, 3);
        cout << endl;

        spectrumPtr->getRawData(mz, intensities);
        cout << "\tgetRawData: ";
        summarizeMzIntensityPairs(cout << fixed << setprecision(2), mz, intensities, 3);
        cout << endl;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        vector<string> rawpaths;

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) os_ = &cout;
            else rawpaths.push_back(argv[i]);
        }

        vector<string> args(argv, argv+argc);
        if (rawpaths.empty())
            throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                                "\nUsage: T2D_Data_Test [-v] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            test(rawpaths[i]);
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
