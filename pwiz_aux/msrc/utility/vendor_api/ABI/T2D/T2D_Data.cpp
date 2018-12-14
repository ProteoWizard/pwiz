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

#define PWIZ_SOURCE


#pragma unmanaged
#include "T2D_Data.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include <iostream>
#include <comdef.h>


#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;


using System::String;
using System::Object;
using System::IntPtr;
using System::Runtime::InteropServices::Marshal;


namespace DE = DataExplorer;


namespace pwiz {
namespace vendor_api {
namespace ABI {
namespace T2D {


class DataImpl : public Data
{
    public:
    DataImpl(const std::string& datapath)
    {
        application_ = gcnew DE::ApplicationClass();

        // disable GUI updates and hide the Data Explorer window
        application_->AutomatedProcessing = 1;
        application_->Visible = 0;

        // read spectrum processing settings from .SET files
        application_->RestoreSettingsFromFile = 1;

        bfs::path real_datapath = datapath;
        if (!bfs::is_directory(datapath))
        {
            if (!bal::iends_with(datapath, ".t2d"))
                throw runtime_error("[T2D::Data::ctor] filepath is not a T2D file: " + datapath);
            //real_datapath = bfs::path(datapath).parent_path();
            t2d_filepaths.push_back(datapath);
        }
        else
        pwiz::util::expand_pathmask(real_datapath / "*.t2d", t2d_filepaths);
        pwiz::util::expand_pathmask(real_datapath / "MS/*.t2d", t2d_filepaths);
        pwiz::util::expand_pathmask(real_datapath / "MSMS/*.t2d", t2d_filepaths);
        std::sort(t2d_filepaths.begin(), t2d_filepaths.end());
        if (t2d_filepaths.empty())
            throw runtime_error("[T2D::Data::ctor] directory contains no T2D files: " + real_datapath.string());
    }

    virtual ~DataImpl() {/*application_->Quit();*/}

    virtual size_t getSpectrumCount() const {return t2d_filepaths.size();}
    virtual SpectrumPtr getSpectrum(size_t index) const;

    virtual const vector<bfs::path>& getSpectrumFilenames() const {return t2d_filepaths;}

    virtual blt::local_date_time getSampleAcquisitionTime() const {return blt::local_date_time(bdt::not_a_date_time);}

    private:
    gcroot<DE::Application^> application_;
    vector<bfs::path> t2d_filepaths;
};


struct SpectrumImpl : public Spectrum
{
    private:
    // HACK: the offset for spectrum type appears to be 348
    static SpectrumType readType(const bfs::path& t2d_filepath)
    {
        bfs::ifstream t2d_file(t2d_filepath, ios::binary);
        t2d_file.seekg(348);
        int type = t2d_file.get();
        if (type < 0 || type > 4) // sanity check
            throw runtime_error("[T2D::Spectrum::readType] Unable to read T2D spectrum type");

        return SpectrumType(type+1);
    }

    public:
    SpectrumImpl(DE::Application^ application, const bfs::path& t2d_filepath)
        : application_(application),
          t2d_filepath_(t2d_filepath)
    {
        bfs::path t2d_filepath_copy = t2d_filepath;

        // TODO: come up with a fallback plan if the T2Ds are in a read-only location

        // delete processing history file
        bfs::remove(t2d_filepath_copy.replace_extension(".cts"));

        // (over)write the settings file before opening the document
        bfs::ofstream settingsFile(t2d_filepath_copy.replace_extension(".set"), ios::trunc);
        settingsFile.write(commonSettings(), strlen(commonSettings()));

        // must know the type of a T2D before opening it to know what settings to write,
        // but can't use the API's SpectrumType property before opening it!
        switch (readType(t2d_filepath))
        {
            case SpectrumType_Linear:
                settingsFile.write(linearSettings(), strlen(linearSettings()));
                break;

            case SpectrumType_Reflector:
                settingsFile.write(reflectorSettings(), strlen(reflectorSettings()));
                break;

            case SpectrumType_MSMS:
                settingsFile.write(msmsSettings(), strlen(msmsSettings()));
                break;

            default:
            case SpectrumType_PSD:
                break;
        }
        settingsFile.close();

        document_ = application->Documents->Open(ToSystemString(t2d_filepath.string()));
        specView_ = document_->SpecView;
        specSetup_ = document_->SpecSetup;
    }

    virtual ~SpectrumImpl()
    {
        try
        {
            document_->Close();
        }
        catch (...)
        {
            // TODO: log this strange but apparently benign error
            //std::cerr << "Error closing spectrum document." << std::endl;
        }
    }

    virtual SpectrumType getType() const {return (SpectrumType) specView_->SpectrumType;}

    virtual int getMsLevel() const
    {
        switch ((SpectrumType) specView_->SpectrumType)
        {
            case SpectrumType_Linear:
            case SpectrumType_Reflector:
            case SpectrumType_PSD:
                return 1;

            case SpectrumType_MSMS:
                return 2;

            default:
                throw runtime_error("[T2D::Spectrum::getMsLevel] Unknown spectrum type");
        }
    }

    virtual IonMode getPolarity() const
    {
        int value;
        document_->InstrumentSettings->GetState(DE::DeInstrumentStateType::deIonMode, 0, value);
        return value < 0 || value > 2 ? IonMode_Unknown : (IonMode) value;
    }

    virtual size_t getPeakDataSize() const
    {
        System::Object^ peakDataObject;
        specView_->GetPeakData(DE::DeSpecPeakData::deSpecPeakMass, DE::DeSpecPeakSortOrder::deSpecPeakSortMass, 0, System::Double::MaxValue, peakDataObject);
        cli::array<double,2>^ peakDataArray = (cli::array<double,2>^) peakDataObject;
        return peakDataArray->Length;
    }

    virtual void getPeakData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const
    {
        System::Object^ peakDataObject;
        specView_->GetPeakData(DE::DeSpecPeakData::deSpecPeakAll, DE::DeSpecPeakSortOrder::deSpecPeakSortMass, 0, System::Double::MaxValue, peakDataObject);
        cli::array<double,2>^ peakDataArray = (cli::array<double,2>^) peakDataObject;

        // peak data array has 7 dimensions:
        // apex m/z, apex intensity, centroid m/z, peak area, peak charge, peak start m/z, peak end m/z, 
        mz.resize(peakDataArray->Length / 7);
        intensities.resize(mz.size());
        for (size_t i=0, end=mz.size(); i < end; ++i)
        {
            mz[i] = peakDataArray[i, 2]; // centroid m/z
            intensities[i] = peakDataArray[i, 3]; // peak area
        }
    }

    virtual size_t getRawDataSize() const {return specView_->RawDataSize;}
    virtual void getRawData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const
    {
        System::Object^ rawDataObject;
        specView_->GetRawData(0, System::Double::MaxValue, rawDataObject);
        cli::array<double,2>^ rawDataArray = (cli::array<double,2>^) rawDataObject;
        mz.resize(rawDataArray->Length / 2);
        intensities.resize(mz.size());
        for (size_t i=0, end=mz.size(); i < end; ++i)
        {
            mz[i] = rawDataArray[i,0];
            intensities[i] = rawDataArray[i,1];
        }
    }

    virtual double getTIC() const
    {
        // TODO: find out why this doesn't work:
        //return specView_->GetTIC();

        // sum all peak areas
        System::Object^ peakDataObject;
        specView_->GetPeakData(DE::DeSpecPeakData::deSpecPeakArea, DE::DeSpecPeakSortOrder::deSpecPeakSortMass, 0, System::Double::MaxValue, peakDataObject);
        cli::array<double,2>^ peakDataArray = (cli::array<double,2>^) peakDataObject;
        double tic = 0;
        for (size_t i=0, end=peakDataArray->Length; i < end; ++i)
            tic += peakDataArray[i,0];
        return tic;
    }

    virtual void getBasePeak(double& mz, double& intensity) const {specView_->GetBasePeak(mz, intensity);}

    virtual string getInstrumentStringParam(InstrumentStringParam param) const
    {
        String^ value;
        document_->InstrumentSettings->GetStringParam((DE::DeInstrumentStringParamType) param, 0, value);
        return ToStdString(value);
    }

    virtual double getInstrumentSetting(InstrumentSetting setting) const
    {
        double value;
        document_->InstrumentSettings->GetSetting((DE::DeInstrumentSettingType) setting, 0, value);
        return value;
    }


    private:

    gcroot<DE::Application^> application_;
    gcroot<DE::Document^> document_;
    gcroot<DE::SpecView^> specView_;
    gcroot<DE::SpecSetup^> specSetup_;
    const bfs::path& t2d_filepath_;

    static const char* commonSettings()
    {
        return
        "Version\\Version=3.1\r\n" \
        "SpecView\\GlobalPkDetectBPIntensity=0.0\r\n" \
        "SpecView\\GlobalPkDetectBPArea=2.0\r\n" \
        "SpecView\\SmoothFilterType=0\r\n" \
        "SpecView\\SmoothFilterWidth=5\r\n" \
        "SpecView\\NoiseReductionType=1\r\n" \
        "SpecView\\NoiseReductionStdev=2.\r\n" \
        "SpecView\\PPLCoeffFactor=0.7\r\n" \
        "SpecView\\SmoothResolution=5000.\r\n" \
        "SpecView\\BkSubRangeOnly=0\r\n" \
        "SpecView\\BaselineMethod=1\r\n" \
        "SpecView\\BkSubLBaselineX=0.\r\n" \
        "SpecView\\BkSubLBaselineY=0.\r\n" \
        "SpecView\\BkSubRBaselineX=0.\r\n" \
        "SpecView\\BkSubRBaselineY=0.\r\n" \
        "SpecView\\MultiChgDeconAdduct=1.007825\r\n" \
        "SpecView\\MultiChgDeconGenerate=1\r\n" \
        "SpecView\\MultiChgDeconSelectType=0\r\n" \
        "SpecView\\SingleChargeAdduct=H\r\n" \
        "SpecView\\SingleChargePolarity=0\r\n" \
        "SpecView\\ResCalcPctHeight=50.\r\n" \
        "SpecView\\ZeroChgConAdduct=1.007825\r\n" \
        "SpecView\\ZeroChgConOutCenter=15000.\r\n" \
        "SpecView\\ZeroChgConOutStep=1.\r\n" \
        "SpecView\\ZeroChgConOutWidth=5000.\r\n" \
        "SpecView\\IsotopeCalcThreshold=1.\r\n" \
        "SpecView\\IsotopeCalcResDalton=0.1\r\n" \
        "SpecView\\IsotopeCalcResPPM=5.\r\n" \
        "SpecView\\IsotopeCalcResPower=10000.\r\n" \
        "SpecView\\IsotopeCalcFormula=0\r\n" \
        "SpecView\\IsotopeCalcResUnit=2\r\n" \
        "SpecView\\IsotopeCalcCharge=1\r\n" \
        "SpecView\\IsotopeCalcPolarity=0\r\n" \
        "SpecView\\IsotopeCalcResType=0\r\n" \
        "SpecView\\IsotopeCalcAdduct=0\r\n" \
        "SpecView\\IsotopeCalcPeptidePlusH20=0\r\n" \
        "SpecView\\DeIsotopeAdduct=H\r\n" \
        "SpecView\\DeIsotopeRepeatFormula=C6H5NO\r\n" \
        "SpecView\\DualSpecOpTolerancePPM=10.\r\n" \
        "SpecView\\DualSpecOpToleranceDa=0.1\r\n" \
        "SpecView\\DualSpecOpToleranceType=1\r\n" \
        "SpecView\\DualSpecOpDestTraceType=0\r\n" \
        "SpecView\\PkDCVModelHeight=0.\r\n" \
        "SpecView\\PkDCVModelCenter=0.\r\n" \
        "SpecView\\PkDCVModelRShape=0.\r\n" \
        "SpecView\\PkDCVModelRWidth=0.\r\n" \
        "SpecView\\PkDCVModelLShape=0.\r\n" \
        "SpecView\\PkDCVModelLWidth=0.\r\n" \
        "SpecView\\PkDCVDegree=0.2\r\n" \
        "SpecView\\SPLPeakChargeState=1\r\n" \
        "SpecView\\SPLPeakIsotope=0.1\r\n" \
        "SpecView\\SPLPeakDeisoAdduct=H\r\n" \
        "SpecView\\SPLPeakDeisoFormula=C6H5NO\r\n" \
        "SpecView\\SPLFlagPeaksCalcSNFromClusterArea=0\r\n" \
        "SpecView\\SPLFlagPeaksSNFromClusterAreaThreshold=10.\r\n" \
        "SpecView\\SAPeakCentroidPct=50.\r\n" \
        "SpecView\\SAPeakSNThreshold=4.\r\n" \
        "SpecView\\SANumOfRanges=3\r\n" \
        "SpecView\\SAPeakRangeMass0=100.\r\n" \
        "SpecView\\SAPeakRangeResolution0=5000.\r\n" \
        "SpecView\\SAPeakRangeMass1=1000.\r\n" \
        "SpecView\\SAPeakRangeResolution1=8000.\r\n" \
        "SpecView\\SAPeakRangeMass2=2000.\r\n" \
        "SpecView\\SAPeakRangeResolution2=12000.\r\n";
    }

    static const char* linearSettings()
    {
        return
        "SpecView\\PPLBaselineDegree=0.1\r\n" \
        "SpecView\\PPLBaselineFlex=0.5\r\n" \
        "SpecView\\PPLBaselinePeakWidth=32\r\n" \
        "SpecView\\SPLPeakCentroidPct=50.\r\n" \
        "SpecView\\SPLPeakSNThreshold=2.\r\n" \
        "SpecView\\SPLPeakNoiseWindowWidth=0.\r\n" \
        "SpecView\\SPLPeakIntegrateMethod=0\r\n" \
        "SpecView\\SPLNumOfRanges=8\r\n" \
        "SpecView\\SPLPeakRangeMass0=2000.\r\n" \
        "SpecView\\SPLPeakRangeResolution0=2000.\r\n" \
        "SpecView\\SPLPeakRangeMass1=2500.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=1000.\r\n" \
        "SpecView\\SPLPeakRangeMass1=5000.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=800.\r\n" \
        "SpecView\\SPLPeakRangeMass1=10000.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=600.\r\n" \
        "SpecView\\SPLPeakRangeMass1=20000.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=400.\r\n" \
        "SpecView\\SPLPeakRangeMass1=40000.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=200.\r\n" \
        "SpecView\\SPLPeakRangeMass1=80000.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=100.\r\n" \
        "SpecView\\SPLPeakRangeMass1=100000.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=50.\r\n";
    }

    static const char* reflectorSettings()
    {
        return
        "SpecView\\PPLBaselineDegree=0.1\r\n" \
        "SpecView\\PPLBaselineFlex=0.5\r\n" \
        "SpecView\\PPLBaselinePeakWidth=32\r\n" \
        "SpecView\\SPLPeakCentroidPct=50.\r\n" \
        "SpecView\\SPLPeakSNThreshold=3.\r\n" \
        "SpecView\\SPLPeakNoiseWindowWidth=0.\r\n" \
        "SpecView\\SPLPeakIntegrateMethod=0\r\n" \
        "SpecView\\SPLNumOfRanges=2\r\n" \
        "SpecView\\SPLPeakRangeMass0=1000.\r\n" \
        "SpecView\\SPLPeakRangeResolution0=10000.\r\n" \
        "SpecView\\SPLPeakRangeMass1=6000.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=18000.\r\n";
    }

    static const char* msmsSettings()
    {
        return
        "SpecView\\PPLBaselineDegree=0.1\r\n" \
        "SpecView\\PPLBaselineFlex=0.5\r\n" \
        "SpecView\\PPLBaselinePeakWidth=32\r\n" \
        "SpecView\\SPLPeakCentroidPct=50.\r\n" \
        "SpecView\\SPLPeakSNThreshold=4.\r\n" \
        "SpecView\\SPLPeakNoiseWindowWidth=0.\r\n" \
        "SpecView\\SPLPeakIntegrateMethod=0\r\n" \
        "SpecView\\SPLNumOfRanges=2\r\n" \
        "SpecView\\SPLPeakRangeMass0=100.\r\n" \
        "SpecView\\SPLPeakRangeResolution0=1000.\r\n" \
        "SpecView\\SPLPeakRangeMass1=600.\r\n" \
        "SpecView\\SPLPeakRangeResolution1=6000.\r\n";
    }
};


SpectrumPtr DataImpl::getSpectrum(std::size_t index) const
{
    if (index >= t2d_filepaths.size())
        throw out_of_range("[T2D::Data::getSpectrum] index out of range");

    return SpectrumPtr(new SpectrumImpl(application_, t2d_filepaths[index]));
}


#pragma unmanaged
PWIZ_API_DECL DataPtr Data::create(const string& datapath)
{
    return DataPtr(new DataImpl(datapath));
}


} // T2D
} // ABI
} // vendor_api
} // pwiz
