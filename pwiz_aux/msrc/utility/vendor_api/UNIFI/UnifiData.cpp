﻿//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

#ifndef PWIZ_READER_UNIFI
#error compiler is not MSVC or DLL not available
#else // PWIZ_READER_UNIFI

#pragma unmanaged
#include "UnifiData.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/Exception.hpp"

#pragma warning(push)
#pragma warning(disable: 4400 4538 4564)

#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
#include "ParallelDownloadQueue.hpp"
#using <System.dll>
#using <System.Core.dll>
#using <System.Xml.dll>
#using <System.Net.dll>
#using <System.Web.dll>
#using <System.Net.Http.dll>
#using <System.Net.Http.WebRequest.dll>
using namespace pwiz::util;
using namespace System;
using namespace System::Web;
using namespace System::Net;
using namespace System::Net::Http;
using namespace System::Net::Http::Headers;
using namespace Newtonsoft::Json;
using namespace Newtonsoft::Json::Linq;
using namespace System::Runtime::Caching::Generic;
using namespace System::Collections::Generic;
using namespace System::Collections::Concurrent;
using namespace Microsoft::Extensions::DependencyInjection;
using System::Threading::Tasks::Task;
using System::Threading::Tasks::TaskScheduler;
using System::Threading::Tasks::Schedulers::QueuedTaskScheduler;
using System::Uri;
using IdentityModel::Client::TokenClient;
using IdentityModel::Client::TokenResponse;
using std::size_t;


namespace pwiz {
namespace vendor_api {


namespace
{
    public ref struct AccessTokenRequest
    {
        String^ Uri;
        String^ Username;
        String^ Password;
        String^ Scope;
        String^ Secret;
        String^ ClientId;
    };
}


namespace UNIFI {

[ProtoBuf::ProtoContract]
enum class ProtoEnergyLevel
{
    Unknown = 0,
    Low = 1,
    High = 2
};

[ProtoBuf::ProtoContract]
enum class ProtoPolarity
{
    Unknown = 0,
    Negative = 1,
    Positive = 2
};

[ProtoBuf::ProtoContract]
enum class ProtoMSType
{
    UnknownMSType = 0,
    MS1 = 1,
    MS2 = 2
};

ref class MSeMassSpectrum;
ref class DDAMassSpectrum;
ref class MassSpectrum;

[ProtoBuf::ProtoContract]
[ProtoBuf::ProtoInclude(200, MassSpectrum::typeid)]
public ref class Spectrum abstract
{
public:

    [ProtoBuf::ProtoMember(1)]
    property cli::array<double>^ Intensities;

    virtual ~Spectrum() { if (intensityArray != nullptr) delete intensityArray; intensityArray = nullptr; }
    !Spectrum() { delete this; }

    std::vector<double>* intensityArray;
};

[ProtoBuf::ProtoContract]
[ProtoBuf::ProtoInclude(100, MSeMassSpectrum::typeid)]
[ProtoBuf::ProtoInclude(101, DDAMassSpectrum::typeid)]
public ref class MassSpectrum abstract : Spectrum
{
public:

    [ProtoBuf::ProtoMember(1)]
    property cli::array<double>^ Masses;

    [ProtoBuf::ProtoMember(2)]
    property cli::array<int>^ ScanSize;

    virtual ~MassSpectrum() { if (mzArray != nullptr) delete mzArray; mzArray = nullptr; }
    !MassSpectrum() { delete this; }

    std::vector<double>* mzArray;
};

[ProtoBuf::ProtoContract]
public ref class MSeMassSpectrum : MassSpectrum
{
public:

    [ProtoBuf::ProtoMember(1)]
    property double RetentionTime;

    [ProtoBuf::ProtoMember(2)]
    property ProtoEnergyLevel EnergyLevel;

    [ProtoBuf::ProtoMember(3)]
    property ProtoPolarity IonizationPolarity;

    virtual ~MSeMassSpectrum() { if (driftTimeArray != nullptr) delete driftTimeArray; driftTimeArray = nullptr; }
    !MSeMassSpectrum() { delete this; }

    property System::Collections::Generic::List<int>^ ScanIndexes;
    std::vector<double>* driftTimeArray;
};

[ProtoBuf::ProtoContract]
public ref class DDAMassSpectrum : MassSpectrum
{
public:

    [ProtoBuf::ProtoMember(1)]
    property double RetentionTime;

    [ProtoBuf::ProtoMember(2)]
    property float SetMass;

    [ProtoBuf::ProtoMember(3)]
    property ProtoMSType MSType;

    virtual ~DDAMassSpectrum() {}
    !DDAMassSpectrum() { delete this; }
};

class UnifiData::Impl
{
    public:
    virtual void connect(const std::string& sampleResultUrl, bool combineIonMobilitySpectra)
    {
        _combineIonMobilitySpectra = combineIonMobilitySpectra;

        try
        {
            Uri^ temp;
            if (bal::starts_with(sampleResultUrl, "http://") || bal::starts_with(sampleResultUrl, "https://"))
                temp = gcnew Uri(ToSystemString(sampleResultUrl));
            else
                temp = gcnew Uri(ToSystemString("https://" + sampleResultUrl));

            _apiVersion = temp->Port == 50034 ? 3 : 4;
            String^ defaultIdentityServer = System::String::Format("{0}://{1}@{2}:{3}", temp->Scheme, temp->UserInfo, temp->Host, _apiVersion == 3 ? 50333 : 48333);
            String^ defaultClientScope = (_apiVersion == 3 ? L"unifi" : L"webapi");

            auto queryVars = System::Web::HttpUtility::ParseQueryString(temp->Query);
            _identityServerUrl = gcnew Uri(queryVars[L"identity"] == nullptr ? defaultIdentityServer : queryVars[L"identity"]);
            _clientScope = queryVars[L"scope"] == nullptr ? defaultClientScope : queryVars[L"scope"];
            _clientSecret = queryVars[L"secret"] == nullptr ? L"secret" : queryVars[L"secret"];
            _clientId = queryVars[L"clientId"] == nullptr ? L"resourceownerclient" : queryVars[L"clientId"];
            _sampleResultUrl = gcnew Uri(temp->GetLeftPart(UriPartial::Path));

            initHttpClient();
            getAccessToken();
            getHttpClient();

            getSampleMetadata();
            getNumberOfSpectra();
            //Console::WriteLine("numLogicalSpectra: {0}, numNetworkSpectra: {1}", _numLogicalSpectra, _numNetworkSpectra);

            auto unifiDebug = System::Environment::GetEnvironmentVariable("UNIFI_DEBUG");
            _unifiDebug = unifiDebug != nullptr && unifiDebug != "0";

            _chunkSize = 20;// Math::Max(10, (int)std::ceil(_numNetworkSpectra / 500.0));

#ifdef _WIN64
            int idealChunkReadahead = _chunkReadahead = 8;
#else
            int idealChunkReadahead = _chunkReadahead = 2;
#endif
            auto firstSpectrumDownloadInfo = gcnew ParallelDownloadQueue::DownloadInfo();
            firstSpectrumDownloadInfo->spectrumIndexStart = 0;
            firstSpectrumDownloadInfo->spectrumIndexEnd = 1;
            firstSpectrumDownloadInfo->userdata = gcnew IntPtr(this);
            spectrumEndpoint(firstSpectrumDownloadInfo);
            String^ acceptHeader = "application/octet-stream";
            _chunkReadahead = ParallelDownloadQueue::GetRequestLimit(firstSpectrumDownloadInfo->spectrumEndpoint->ToString(), _httpClientFactory, _accessToken, acceptHeader, _chunkReadahead);
            _chunkSize = (double) idealChunkReadahead / _chunkReadahead * _chunkSize;

            if (queryVars[L"chunkSize"] != nullptr) _chunkSize = lexical_cast<int>(ToStdString(queryVars[L"chunkSize"]));
            if (queryVars[L"chunkReadahead"] != nullptr) _chunkReadahead = lexical_cast<int>(ToStdString(queryVars[L"chunkReadahead"]));

            _cacheSize = _chunkSize * _chunkReadahead * 2;

            _cache = gcnew MemoryCache<int, Object^>(_cacheSize);
            _cache->SetPolicy(LruEvictionPolicy<int, MSeMassSpectrum^>::typeid->GetGenericTypeDefinition());

            // I would have preferred to use a virtual member function for spectrumEndpoint, but I couldn't figure out how to put it in an Action
            auto spectrumEndpointAction = gcnew Action<ParallelDownloadQueue::DownloadInfo^>(&Impl::spectrumEndpoint);
            auto getSpectraFromStreamAction = gcnew Action<IO::Stream^, ParallelDownloadQueue::DownloadInfo^>(&Impl::getSpectraFromStreamLoop);
            _queue = gcnew ParallelDownloadQueue(_sampleResultUrl, _accessToken, _httpClientFactory, _numNetworkSpectra, _binToDriftTime, _chunkSize, _chunkReadahead,
                acceptHeader, spectrumEndpointAction, getSpectraFromStreamAction, gcnew IntPtr(this));
        }
        CATCH_AND_FORWARD_EX(sampleResultUrl)
    }

    virtual RemoteApi getRemoteApiType() { return RemoteApi::Unifi; }

    virtual int getMsLevel(size_t index) const
    {
        bool evenIndex = index % 2 == 0;
        return evenIndex ? 1 : 2;
    }

    virtual void getChannelAndScanIndex(size_t index, int& channelIndex, int& scanIndexInChannel) const
    {
        throw std::runtime_error("UNIFI spectra are not organized by channel");
    }

    friend class UnifiData;

    virtual ~Impl() {}

    protected:
    System::String^ tokenEndpoint() { return System::Uri(_identityServerUrl, _apiVersion == 3 ? L"/identity/connect/token" : L"/connect/token").ToString(); }

    /// returns JSON describing the sampleResult, for example:
    //{
    //  "id" : "d686e598-7621-4ad6-9efb-321c015ff59a",
    //  "name" : "MM  5.0 ug/kg",
    //  "description" : "Matrix matched standard 5.0 ug/kg",
    //  "sample" : {
    //    "description": "",
    //    "originalSampleId" : null,
    //    "id" : "f0f33a91-f668-4f9e-a153-4734109cdf41",
    //    "gender" : "",
    //    "name" : "MM2",
    //    "assayConditions" : "",
    //    "bracketGroup" : "",
    //    "dose" : "NaN",
    //    "dosingRoute" : "",
    //    "day" : "",
    //    "eCordId" : "",
    //    "experimentalConcentration" : "",
    //    "groupId" : "",
    //    "injectionId" : "",
    //    "matrix" : "",
    //    "solventDelay" : "NaN",
    //    "species" : "",
    //    "studyId" : "",
    //    "subjectId" : "",
    //    "molForm" : "",
    //    "preparation" : "",
    //    "sampleType" : "Standard",
    //    "batchId" : "",
    //    "studyName" : "",
    //    "sampleLevel" : "Unspecified",
    //    "sampleWeight" : 1,
    //    "dilution" : 1,
    //    "replicateNumber" : 1,
    //    "wellPosition" : "1:C,4",
    //    "injectionVolume" : 10,
    //    "acquisitionRunTime" : 17,
    //    "acquisitionStartTime" : "0001-01-01T00:00:00Z",
    //    "time" : "NaN",
    //    "processingOptions" : "QuantitationStd",
    //    "processingFunction" : "",
    //    "processingSequenceNumber" : 0
    //    }
    //}
    System::String^ sampleResultMetadataEndpoint() { return _sampleResultUrl->AbsoluteUri; }

    /// returns a JSON array describing the 'functions' (aka scan events), for example:
    //{
    //  "value": [
    //    {
    //      "id": "2f52bf3f-d4b5-472d-ab6d-4f55d6b7665e",
    //      "name": "1: TOF MSᴱ (50-1200) 4eV ESI+ - Low CE",
    //      "isCentroidData": false,
    //      "isRetentionData": true,
    //      "isIonMobilityData": false,
    //      "hasCCSCalibration": false,
    //      "detectorType": "MS",
    //      "analyticalTechnique": {
    //        "hardwareName": "",
    //        "scanningMethod": "MS",
    //        "massAnalyser": "QTOF",
    //        "ionisationMode": "+",
    //        "ionisationType": "ESI",
    //        "lowMass": 50,
    //        "highMass": 1200,
    //        "adcGroup": {
    //          "acquisitionMode": "ADC_PD",
    //          "acquisitionFrequency": "NaN",
    //          "ionResponses": [
    //            {
    //              "ionType": "LEU_ENK",
    //              "charge": 1,
    //              "averageIonArea": 33.887678
    //            }
    //          ]
    //        },
    //        "tofGroup": {
    //          "nominalResolution": 7000,
    //          "mseLevel": "Low",
    //          "pusherFrequency": "NaN"
    //        },
    //        "quadGroup": null
    //      },
    //      "axisX": {
    //        "label": "Observed mass",
    //        "unit": "m/z",
    //        "lowerBound": 50,
    //        "upperBound": 1200
    //      },
    //      "axisY": {
    //        "label": "Intensity",
    //        "unit": "Counts",
    //        "lowerBound": "NaN",
    //        "upperBound": "NaN"
    //      }
    //    },
    //    {
    //      "id": "bbcfa906-4545-49ba-aa84-d020eb41e518",
    //      "name": "Reference TOF MSᴱ (50-1200) 6eV ESI+",
    //      "isCentroidData": false,
    //      "isRetentionData": true,
    //      "isIonMobilityData": false,
    //      "hasCCSCalibration": false,
    //      "detectorType": "MS",
    //      "analyticalTechnique": {
    //        "hardwareName": "",
    //        "scanningMethod": "MS",
    //        "massAnalyser": "QTOF",
    //        "ionisationMode": "+",
    //        "ionisationType": "ESI",
    //        "lowMass": 50,
    //        "highMass": 1200,
    //        "adcGroup": {
    //          "acquisitionMode": "ADC_PD",
    //          "acquisitionFrequency": "NaN",
    //          "ionResponses": [
    //            {
    //              "ionType": "LEU_ENK",
    //              "charge": 1,
    //              "averageIonArea": 33.887678
    //            }
    //          ]
    //        },
    //        "tofGroup": {
    //          "nominalResolution": 7000,
    //          "mseLevel": "Unknown",
    //          "pusherFrequency": "NaN"
    //        },
    //        "quadGroup": null
    //      },
    //      "axisX": {
    //        "label": "Observed mass",
    //        "unit": "m/z",
    //        "lowerBound": 50,
    //        "upperBound": 1200
    //      },
    //      "axisY": {
    //        "label": "Intensity",
    //        "unit": "Counts",
    //        "lowerBound": "NaN",
    //        "upperBound": "NaN"
    //      }
    //    },
    //    {
    //      "id": "290805b1-faf2-44ca-af1b-9f8c5d3873ac",
    //      "name": "2: TOF MSᴱ (50-1200) 10-45eV ESI+ - High CE",
    //      "isCentroidData": false,
    //      "isRetentionData": true,
    //      "isIonMobilityData": false,
    //      "hasCCSCalibration": false,
    //      "detectorType": "MS",
    //      "analyticalTechnique": {
    //        "hardwareName": "",
    //        "scanningMethod": "MS",
    //        "massAnalyser": "QTOF",
    //        "ionisationMode": "+",
    //        "ionisationType": "ESI",
    //        "lowMass": 50,
    //        "highMass": 1200,
    //        "adcGroup": {
    //          "acquisitionMode": "ADC_PD",
    //          "acquisitionFrequency": "NaN",
    //          "ionResponses": [
    //            {
    //              "ionType": "LEU_ENK",
    //              "charge": 1,
    //              "averageIonArea": 33.887678
    //            }
    //          ]
    //        },
    //        "tofGroup": {
    //          "nominalResolution": 7000,
    //          "mseLevel": "High",
    //          "pusherFrequency": "NaN"
    //        },
    //        "quadGroup": null
    //      },
    //      "axisX": {
    //        "label": "Observed mass",
    //        "unit": "m/z",
    //        "lowerBound": 50,
    //        "upperBound": 1200
    //      },
    //      "axisY": {
    //        "label": "Intensity",
    //        "unit": "Counts",
    //        "lowerBound": "NaN",
    //        "upperBound": "NaN"
    //      }
    //    }
    //  ]
    //}
    System::String^ functionInfoEndpoint() { return _sampleResultUrl + "/spectrumInfos"; }

    /// returns a JSON array describing the 'chromatograms', for example:
    /*
    "value": [
        {
            "id": "9fdfae70-24bf-4d0f-a4aa-00f524c28282",
            "name": "FLR A",
            "detectorType": "FLR",
            "analyticalTechnique": {
                "hardwareName": "ACQ-FLR#K06UPF015R"
            },
            "axisX": null,
            "axisY": null
        },
        {
            "id": "8a233325-4958-414e-a5da-0898fd0b6bd7",
            "name": "1: TOF MSe (50-2000) 45V ESI+ (TIC)",
            "detectorType": "MS",
            "analyticalTechnique": {
                "@odata.type": "#Waters.WebApi.Common.Models.MSTechnique",
                "hardwareName": "",
                "scanningMethod": "MS",
                "massAnalyser": "TIME OF FLIGHT",
                "ionisationMode": "+",
                "ionisationType": "ESI",
                "lowMass": 50.0,
                "highMass": 2000.0,
                "adcGroup": {
                    "acquisitionMode": "ADC_PD",
                    "acquisitionFrequency": "NaN",
                    "ionResponses": [
                        {
                            "ionType": "PEPTIDE",
                            "charge": 1,
                            "averageIonArea": 25.8815208039415
                        }
                    ]
                },
                "tofGroup": {
                    "nominalResolution": 10000.0,
                    "mseLevel": "Low",
                    "pusherFrequency": 21739.1304347826,
                    "lteff": 800.0,
                    "veff": 3307.71621789606,
                    "samplingFrequency": 2.7
                },
                "quadGroup": null
            },
            "axisX": {
                "label": "Retention Time",
                "unit": "min",
                "lowerBound": 0,
                "upperBound": 55
            },
            "axisY": {
                "label": "TIC",
                "unit": "Counts",
                "lowerBound": 0,
                "upperBound": "NaN"
            }
        },
        {
            "id": "8a233325-4958-414e-a5da-0898fd0b6bd8",
            "name": "1: TOF MSe (50-2000) 45V ESI+ (BPI)",
            "detectorType": "MS",
            "analyticalTechnique": {
                "@odata.type": "#Waters.WebApi.Common.Models.MSTechnique",
                "hardwareName": "",
                "scanningMethod": "MS",
                "massAnalyser": "TIME OF FLIGHT",
                "ionisationMode": "+",
                "ionisationType": "ESI",
                "lowMass": 50.0,
                "highMass": 2000.0,
                "adcGroup": {
                    "acquisitionMode": "ADC_PD",
                    "acquisitionFrequency": "NaN",
                    "ionResponses": [
                        {
                            "ionType": "PEPTIDE",
                            "charge": 1,
                            "averageIonArea": 25.8815208039415
                        }
                    ]
                },
                "tofGroup": {
                    "nominalResolution": 10000.0,
                    "mseLevel": "Low",
                    "pusherFrequency": 21739.1304347826,
                    "lteff": 800.0,
                    "veff": 3307.71621789606,
                    "samplingFrequency": 2.7
                },
                "quadGroup": null
            },
            "axisX": {
                "label": "Retention Time",
                "unit": "min",
                "lowerBound": 0,
                "upperBound": 55
            },
            "axisY": {
                "label": "BPI",
                "unit": "Counts",
                "lowerBound": 0,
                "upperBound": "NaN"
            }
        },
        {
            "id": "5b135f4c-c703-4e0b-8524-cbf46a86211c",
            "name": "2: TOF MSe (50-2000) 60-80V ESI+ (TIC)",
            "detectorType": "MS",
            "analyticalTechnique": {
                "@odata.type": "#Waters.WebApi.Common.Models.MSTechnique",
                "hardwareName": "",
                "scanningMethod": "MS",
                "massAnalyser": "TIME OF FLIGHT",
                "ionisationMode": "+",
                "ionisationType": "ESI",
                "lowMass": 50.0,
                "highMass": 2000.0,
                "adcGroup": {
                    "acquisitionMode": "ADC_PD",
                    "acquisitionFrequency": "NaN",
                    "ionResponses": [
                        {
                            "ionType": "PEPTIDE",
                            "charge": 1,
                            "averageIonArea": 25.8815208039415
                        }
                    ]
                },
                "tofGroup": {
                    "nominalResolution": 10000.0,
                    "mseLevel": "High",
                    "pusherFrequency": 21739.1304347826,
                    "lteff": 800.0,
                    "veff": 3307.71621789606,
                    "samplingFrequency": 2.7
                },
                "quadGroup": null
            },
            "axisX": {
                "label": "Retention Time",
                "unit": "min",
                "lowerBound": 0,
                "upperBound": 55
            },
            "axisY": {
                "label": "TIC",
                "unit": "Counts",
                "lowerBound": 0,
                "upperBound": "NaN"
            }
        },
        {
            "id": "5b135f4c-c703-4e0b-8524-cbf46a86211d",
            "name": "2: TOF MSe (50-2000) 60-80V ESI+ (BPI)",
            "detectorType": "MS",
            "analyticalTechnique": {
                "@odata.type": "#Waters.WebApi.Common.Models.MSTechnique",
                "hardwareName": "",
                "scanningMethod": "MS",
                "massAnalyser": "TIME OF FLIGHT",
                "ionisationMode": "+",
                "ionisationType": "ESI",
                "lowMass": 50.0,
                "highMass": 2000.0,
                "adcGroup": {
                    "acquisitionMode": "ADC_PD",
                    "acquisitionFrequency": "NaN",
                    "ionResponses": [
                        {
                            "ionType": "PEPTIDE",
                            "charge": 1,
                            "averageIonArea": 25.8815208039415
                        }
                    ]
                },
                "tofGroup": {
                    "nominalResolution": 10000.0,
                    "mseLevel": "High",
                    "pusherFrequency": 21739.1304347826,
                    "lteff": 800.0,
                    "veff": 3307.71621789606,
                    "samplingFrequency": 2.7
                },
                "quadGroup": null
            },
            "axisX": {
                "label": "Retention Time",
                "unit": "min",
                "lowerBound": 0,
                "upperBound": 55
            },
            "axisY": {
                "label": "BPI",
                "unit": "Counts",
                "lowerBound": 0,
                "upperBound": "NaN"
            }
        },
        {
            "id": "5c279069-fbae-4178-9a7c-18007283c446",
            "name": "3: MS LockSpray Reference Data (551-562) 30V ESI+ (TIC)",
            "detectorType": "MS",
            "analyticalTechnique": {
                "@odata.type": "#Waters.WebApi.Common.Models.MSTechnique",
                "hardwareName": "",
                "scanningMethod": "MS",
                "massAnalyser": "TIME OF FLIGHT",
                "ionisationMode": "+",
                "ionisationType": "ESI",
                "lowMass": 551.0,
                "highMass": 562.0,
                "adcGroup": {
                    "acquisitionMode": "ADC_PD",
                    "acquisitionFrequency": "NaN",
                    "ionResponses": [
                        {
                            "ionType": "PEPTIDE",
                            "charge": 1,
                            "averageIonArea": 25.8815208039415
                        }
                    ]
                },
                "tofGroup": {
                    "nominalResolution": 10000.0,
                    "mseLevel": "Unknown",
                    "pusherFrequency": 21739.1304347826,
                    "lteff": 800.0,
                    "veff": 3307.71621789606,
                    "samplingFrequency": 2.7
                },
                "quadGroup": null
            },
            "axisX": {
                "label": "Retention Time",
                "unit": "min",
                "lowerBound": 0,
                "upperBound": 55
            },
            "axisY": {
                "label": "TIC",
                "unit": "Counts",
                "lowerBound": 0,
                "upperBound": "NaN"
            }
        },
        {
            "id": "5c279069-fbae-4178-9a7c-18007283c447",
            "name": "3: MS LockSpray Reference Data (551-562) 30V ESI+ (BPI)",
            "detectorType": "MS",
            "analyticalTechnique": {
                "@odata.type": "#Waters.WebApi.Common.Models.MSTechnique",
                "hardwareName": "",
                "scanningMethod": "MS",
                "massAnalyser": "TIME OF FLIGHT",
                "ionisationMode": "+",
                "ionisationType": "ESI",
                "lowMass": 551.0,
                "highMass": 562.0,
                "adcGroup": {
                    "acquisitionMode": "ADC_PD",
                    "acquisitionFrequency": "NaN",
                    "ionResponses": [
                        {
                            "ionType": "PEPTIDE",
                            "charge": 1,
                            "averageIonArea": 25.8815208039415
                        }
                    ]
                },
                "tofGroup": {
                    "nominalResolution": 10000.0,
                    "mseLevel": "Unknown",
                    "pusherFrequency": 21739.1304347826,
                    "lteff": 800.0,
                    "veff": 3307.71621789606,
                    "samplingFrequency": 2.7
                },
                "quadGroup": null
            },
            "axisX": {
                "label": "Retention Time",
                "unit": "min",
                "lowerBound": 0,
                "upperBound": 55
            },
            "axisY": {
                "label": "BPI",
                "unit": "Counts",
                "lowerBound": 0,
                "upperBound": "NaN"
            }
        },
        {
            "id": "c30400a2-e8d4-499f-98a6-1a8ea06c14a6",
            "name": "Integrated : FLR A",
            "detectorType": "FLR",
            "analyticalTechnique": {
                "hardwareName": "ACQ-FLR#K06UPF015R"
            },
            "axisX": null,
            "axisY": null
        }
    ]
}
    */
    System::String^ chromatogramInfoEndpoint() { return _sampleResultUrl + "/chromatogramInfos"; }

    //{
    //  "value": [
    //      {
    //          "id": "8c2a6c13-d8d3-4440-a19e-573b05b0ca91",
    //          "valuesX" : [546.8483,546.8513,546.8543,...],
    //          "valuesY": [0,10,0,...],
    //          "retentionTime": 0.00176544255,
    //          "retentionTimeIndex" : 0,
    //          "totalNumberOfSpectra" : 36
    //      }
    //  ]
    //}
    System::String^ functionDataEndpoint(System::String^ spectrumInfoId) { return _sampleResultUrl + "/spectrumInfos(" + spectrumInfoId + ")/data?$top=1"; }

    /// returns a simple integer count of the number of spectra in this sampleResult, but does not multiply by 200 for IMS multiscan spectra
    System::String^ spectrumCountEndpoint() { return _sampleResultUrl + "/spectra/mass.mse/$count"; }

    /// returns a JSON array or protobuf stream of the spectral intensities and masses (if the HTTP Accept header specifies 'application/octet-stream')
    static void spectrumEndpoint(ParallelDownloadQueue::DownloadInfo^ downloadInfo)
    {
        auto instance = static_cast<Impl*>(safe_cast<IntPtr^>(downloadInfo->userdata)->ToPointer());
        downloadInfo->spectrumEndpoint = instance->_sampleResultUrl + "/spectra/mass.mse?$skip=" + downloadInfo->taskIndex + "&$top=" + downloadInfo->chunkSize;
    }

    /// returns a JSON array of the chromatogram times and intensities
    /*{
     "id":"9fdfae70-24bf-4d0f-a4aa-00f524c28282",
     "retentionTimes":[0,0.008333334,0.0166666675],
     "intensities":[0,-0.000260543835,-0.000520992267],
     "peaks":[...not used by pwiz...]
    }*/
    System::String^ chromatogramEndpoint(System::String^ chromatogramInfoId) { return _sampleResultUrl + "/chromatogramInfos(" + chromatogramInfoId + ")/data"; }

    /// a POST request to this with a JSON body of (1-based?) bin indexes, e.g. {"bins": [1,2,3,4,5]}
    System::String^ binsToDriftTimesEndPoint() { return _sampleResultUrl + "/spectra/mass.mse/convertbintodrifttime"; }

    gcroot<Uri^> _sampleResultUrl;
    gcroot<Uri^> _identityServerUrl;
    gcroot<System::String^> _clientScope;
    gcroot<System::String^> _clientSecret;
    gcroot<System::String^> _clientId;
    gcroot<System::String^> _accessToken;
    inline static gcroot<IHttpClientFactory^> _httpClientFactory;
    gcroot<HttpClient^> _httpClient;
    gcroot<ParallelDownloadQueue^> _queue;
    bool _unifiDebug;

    int _apiVersion;
    bool _combineIonMobilitySpectra; // do not treat drift bins as separate spectra
    int _numNetworkSpectra; // number of spectra without accounting for drift scans
    int _numLogicalSpectra; // number of spectra with IMS spectra counting as 200 logical spectra

    inline static gcroot<ConcurrentDictionary<String^, Object^>^> globalResponseCache = gcnew ConcurrentDictionary<String^, Object^>();

    gcroot<System::Collections::Generic::List<System::String^>^> _chromatogramIds; //  chromatogram GUIDs
    vector<UnifiChromatogramInfo> _chromatogramInfo;

    string _sampleName;
    string _sampleDescription;
    int _replicateNumber;
    string _wellPosition;
    boost::optional<blt::local_date_time> _acquisitionStartTime; // UTC

    struct FunctionInfo
    {
        FunctionInfo(int function) : function(function), numSpectra(0) {}

        int function; // 0-based index into SpectrumInfo array
        string id;
        bool isCentroidData;
        bool isRetentionData;
        bool isIonMobilityData;
        bool hasCCSCalibration;
        int numSpectra;
        double lowMass, highMass;
        EnergyLevel energyLevel;
    };

    vector<FunctionInfo> _functionInfo;
    bool _hasAnyIonMobilityData;
    vector<double> _binToDriftTime; // drift time for each of the 200 bins (0-base indexed) 

    gcroot<MemoryCache<int, Object^>^> _cache;
    int _chunkSize;
    int _chunkReadahead;
    int _cacheSize;

    static void getSpectraFromStreamLoop(IO::Stream^ stream, ParallelDownloadQueue::DownloadInfo^ downloadInfo)
    {
        auto instance = static_cast<Impl*>(safe_cast<IntPtr^>(downloadInfo->userdata)->ToPointer());
        for (int i = downloadInfo->spectrumIndexStart; i < downloadInfo->spectrumIndexEnd; ++i)
        {
            getSpectraFromStream(instance, stream, downloadInfo, downloadInfo->taskIndex, i);
        }
    }

    static void getSpectraFromStream(const Impl* instance, IO::Stream^ stream, ParallelDownloadQueue::DownloadInfo^ downloadInfo, int taskIndex, int spectrumIndex)
    {
        auto _cache = instance->_cache;
        auto spectrum = ProtoBuf::Serializer::DeserializeWithLengthPrefix<MSeMassSpectrum^>(stream, ProtoBuf::PrefixStyle::Base128);
        if (spectrum == nullptr)
            throw gcnew Exception(System::String::Format("deserialized null spectrum for index {0} (spectrum {1})", spectrumIndex, taskIndex + spectrumIndex));

        if (spectrum->Masses == nullptr)
        {
            spectrum->mzArray = new vector<double>();
            spectrum->intensityArray = new vector<double>();
            spectrum->driftTimeArray = nullptr;
            if (!_cache->Contains(taskIndex + spectrumIndex))
            {
                //Console::WriteLine("Adding result to cache: {0}", taskIndex + i);
                _cache->Add(taskIndex + spectrumIndex, spectrum);
            }
            return;
        }
        downloadInfo->bytesDownloaded += sizeof(double) * spectrum->Masses->Length * 2;

        if (downloadInfo->bytesDownloaded == 0)
            throw gcnew Exception(System::String::Format("empty array for index {0} (spectrum {1})", spectrumIndex, taskIndex + spectrumIndex));

        // if a spectrum takes more than 5 seconds, something's wrong
        auto timeElapsed = DateTime::UtcNow - downloadInfo->lastSpectrumRetrievedTime;
        if (timeElapsed.TotalSeconds > 10.0)
            throw gcnew Exception(System::String::Format("download is going too slow in chunk {0} on thread {1}: one spectrum ({2}) took {3}s", taskIndex, downloadInfo->currentThreadId, taskIndex + spectrumIndex, timeElapsed.TotalSeconds));
        downloadInfo->lastSpectrumRetrievedTime = DateTime::UtcNow;

        spectrum->mzArray = new vector<double>();
        spectrum->intensityArray = new vector<double>();
        spectrum->driftTimeArray = nullptr;
        auto& mzArray = *spectrum->mzArray;
        auto& intensityArray = *spectrum->intensityArray;

        ToStdVector(spectrum->Masses, mzArray);
        ToStdVector(spectrum->Intensities, intensityArray);

        if (spectrum->ScanSize->Length > 1)
        {
            if (spectrum->ScanSize->Length != 200)
                throw gcnew Exception("assumed ion-mobility spectrum but ScanSize.Length != 200");

            spectrum->driftTimeArray = new vector<double>();
            auto& driftTimeArray = *spectrum->driftTimeArray;
            driftTimeArray.reserve(mzArray.size());

            // calculate cumulative scan indexes
            spectrum->ScanIndexes = gcnew System::Collections::Generic::List<int>(spectrum->ScanSize->Length);
            spectrum->ScanIndexes->Add(0);
            for (int j = 0; j < spectrum->ScanSize->Length; ++j)
            {
                if (j > 0) spectrum->ScanIndexes->Add(spectrum->ScanIndexes[j - 1] + spectrum->ScanSize[j - 1]);
                for (int k = 0; k < spectrum->ScanSize[j]; ++k)
                    driftTimeArray.push_back(instance->_binToDriftTime[j]);
            }
        }
        if (!_cache->Contains(taskIndex + spectrumIndex))
        {
            //Console::WriteLine("Adding result to cache: {0}", taskIndex + i);
            _cache->Add(taskIndex + spectrumIndex, spectrum);
        }
        return;
    }

    size_t taskIndexFromSpectrumIndex(size_t index) { return (index / _chunkSize) * _chunkSize; }

    size_t networkIndexFromLogicalIndex(size_t logicalIndex)
    {
        if (!_hasAnyIonMobilityData || _combineIonMobilitySpectra)
            return logicalIndex;

        // 0-199 -> 0
        // 200-399 -> 1
        // 400-599 -> 2
        // etc.
        return (size_t) floor(logicalIndex / 200.0);
    }

    static System::Net::Http::HttpMessageHandler^ getWebRequestHandler()
    {
        auto handler = gcnew System::Net::Http::WebRequestHandler();
        handler->UnsafeAuthenticatedConnectionSharing = true;
        handler->PreAuthenticate = true;
        handler->MaxConnectionsPerServer = 10;
        return handler;
    }

    void initHttpClient()
    {
        if (!_httpClientFactory)
        {
            auto services = gcnew ServiceCollection();
            auto builder = HttpClientFactoryServiceCollectionExtensions::AddHttpClient(services, "customClient");
            Func<HttpMessageHandler^>^ getWebRequestDelegate = gcnew Func<HttpMessageHandler^>(&Impl::getWebRequestHandler);
            HttpClientBuilderExtensions::ConfigurePrimaryHttpMessageHandler(builder, getWebRequestDelegate);
            auto provider = ServiceCollectionContainerBuilderExtensions::BuildServiceProvider(services);
            _httpClientFactory = ServiceProviderServiceExtensions::GetService<IHttpClientFactory^>(provider);
        }
        _httpClient = _httpClientFactory->CreateClient("customClient");
    }

    void getAccessToken()
    {
        cli::array<String^>^ userPassPair;
        String^ username, ^password;
        if (String::IsNullOrEmpty(_sampleResultUrl->UserInfo))
        {
            username = Environment::GetEnvironmentVariable("UNIFI_USERNAME");
            password = Environment::GetEnvironmentVariable("UNIFI_PASSWORD");
            if (username == nullptr || password == nullptr || username->Length == 0 || password->Length == 0)
                throw user_error("UserInfo null; username and password must be specified in the sample result URL (e.g. username:password@unifiserver.com:{port}/{sampleResultPath})");
        }
        else
        {
            userPassPair = _sampleResultUrl->UserInfo->Split(':');
            if (userPassPair->Length != 2 || String::IsNullOrEmpty(userPassPair[0]) || String::IsNullOrEmpty(userPassPair[1]))
                throw user_error("UserInfo not a pair of values; username and password must be specified in the sample result URL (e.g. username:password@unifiserver.com:{port}/{sampleResultPath})");
            username = userPassPair[0];
            password = userPassPair[1];
        }

        auto fields = gcnew System::Collections::Generic::Dictionary<System::String^, System::String^>();
        fields->Add(IdentityModel::OidcConstants::TokenRequest::GrantType, IdentityModel::OidcConstants::GrantTypes::Password);
        fields->Add(IdentityModel::OidcConstants::TokenRequest::UserName, username);
        fields->Add(IdentityModel::OidcConstants::TokenRequest::Password, password);
        fields->Add(IdentityModel::OidcConstants::TokenRequest::Scope, _clientScope);

        auto tokenClient = gcnew TokenClient(tokenEndpoint(), _clientId, _clientSecret, nullptr, IdentityModel::Client::AuthenticationStyle::BasicAuthentication);
        TokenResponse^ response = tokenClient->RequestAsync(fields, System::Threading::CancellationToken::None)->Result;
        if (response->IsError)
            throw user_error("authentication error: incorrect hostname, username or password? (" + ToStdString(response->Error) + ")");

        _accessToken = response->AccessToken;
        //Console::WriteLine(_accessToken);
    }

    void getHttpClient()
    {
        _httpClient->DefaultRequestHeaders->Authorization = gcnew AuthenticationHeaderValue("Bearer", _accessToken);
        _httpClient->BaseAddress = gcnew Uri(_sampleResultUrl->GetLeftPart(System::UriPartial::Authority));
    }

    void getSampleMetadata()
    {
        String^ json;
        try
        {
            auto response = _httpClient->GetAsync(sampleResultMetadataEndpoint())->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + sampleResultMetadataEndpoint());

            json = response->Content->ReadAsStringAsync()->Result;
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting sample result metadata: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }

        try
        {
            auto o = JObject::Parse(json);
            _sampleName = ToStdString(o->SelectToken("$.name")->ToString());
            _sampleDescription = ToStdString(o->SelectToken("$.description")->ToString());
            _replicateNumber = Convert::ToInt32(o->SelectToken("$.sample.replicateNumber")->ToString());
            _wellPosition = ToStdString(o->SelectToken("$.sample.wellPosition")->ToString());

            auto acquisitionTime = (System::DateTime) o->SelectToken("$.sample.acquisitionStartTime");

            // these are Boost.DateTime restrictions enforced because one of the test files had a corrupt date
            if (acquisitionTime.Year > 10000)
                acquisitionTime = acquisitionTime.AddYears(10000 - acquisitionTime.Year);
            else if (acquisitionTime.Year < 1400)
                acquisitionTime = acquisitionTime.AddYears(1400 - acquisitionTime.Year);

            bpt::ptime pt(boost::gregorian::date(acquisitionTime.Year, boost::gregorian::greg_month(acquisitionTime.Month), acquisitionTime.Day),
                bpt::time_duration(acquisitionTime.Hour, acquisitionTime.Minute, acquisitionTime.Second, bpt::millisec(acquisitionTime.Millisecond).fractional_seconds()));
            _acquisitionStartTime = blt::local_date_time(pt, blt::time_zone_ptr()); // UTC
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error parsing sample result metadata: " + ToStdString(e->Message));
        }
        catch (std::exception& e)
        {
            throw e;
        }

        bool hasMSeData = false;

        try
        {
            auto response = _httpClient->GetAsync(functionInfoEndpoint())->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + functionInfoEndpoint());

            json = response->Content->ReadAsStringAsync()->Result;

            _numLogicalSpectra = 0;
            _hasAnyIonMobilityData = false;

            auto o = JObject::Parse(json);
            for each (auto spectrumInfo in o->SelectToken("$.value")->Children())
            {
                // skip non-MS and non-retention-data functions
                auto detectorType = spectrumInfo->SelectToken("$.detectorType")->ToString();
                bool isRetentionData = (bool)spectrumInfo->SelectToken("$.isRetentionData");
                if (detectorType != "MS" || !isRetentionData)
                    continue;

                _functionInfo.emplace_back(_functionInfo.size());
                FunctionInfo& fi = _functionInfo.back();

                auto id = spectrumInfo->SelectToken("$.id")->ToString();
                fi.id = ToStdString(id);
                fi.isCentroidData = (bool)spectrumInfo->SelectToken("$.isCentroidData");
                fi.isRetentionData = isRetentionData;
                fi.isIonMobilityData = (bool)spectrumInfo->SelectToken("$.isIonMobilityData");
                fi.hasCCSCalibration = (bool)spectrumInfo->SelectToken("$.hasCCSCalibration");
                fi.lowMass = Convert::ToDouble(spectrumInfo->SelectToken("$.analyticalTechnique.lowMass")->ToString());
                fi.highMass = Convert::ToDouble(spectrumInfo->SelectToken("$.analyticalTechnique.highMass")->ToString());

                _hasAnyIonMobilityData |= fi.isIonMobilityData;

                //Console::WriteLine("{0}:\n{1}", id, spectrumInfo->ToString());

                // skip non-MSe functions for now; UNIFI API doesn't allow downloading their data (!!!)
                auto mseLevel = spectrumInfo->SelectToken("$.analyticalTechnique.tofGroup.mseLevel");
                if (System::Object::ReferenceEquals(mseLevel, nullptr))
                    continue;
                fi.energyLevel = (EnergyLevel) (ProtoEnergyLevel) Enum::Parse(ProtoEnergyLevel::typeid, mseLevel->ToString());
                if (fi.energyLevel == EnergyLevel::Unknown)
                    continue;

                hasMSeData = true;

                try
                {
                    auto response = _httpClient->GetAsync(functionDataEndpoint(id))->Result;
                    if (!response->IsSuccessStatusCode)
                        throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + functionDataEndpoint(id));

                    json = response->Content->ReadAsStringAsync()->Result;
                    auto o2 = JObject::Parse(json); // there should only be one spectrum but it's in a JSON array
                    for each (auto spectrum in o2->SelectToken("$.value")->Children())
                        fi.numSpectra = isRetentionData ? (int)spectrum->SelectToken("$.totalNumberOfSpectra") : 1;

                    //if (fi.isIonMobilityData)
                    //    fi.numSpectra *= 200;
                }
                catch (Exception^ e)
                {
                    throw gcnew Exception("error getting data for spectrumInfo " + id + ": " + e->ToString()->Split(L'\n')[0]);
                }
            }

            auto energyLevelSortOrder = [](EnergyLevel el)
            {
                switch (el)
                {
                    case EnergyLevel::Unknown: return 2;
                    case EnergyLevel::Low: return 0;
                    case EnergyLevel::High: return 1;
                    default: throw gcnew Exception("unsupported energy level");
                }
            };

            sort(_functionInfo.begin(), _functionInfo.end(), [=](const auto& lhs, const auto& rhs)
            {
                return energyLevelSortOrder(lhs.energyLevel) < energyLevelSortOrder(rhs.energyLevel);
            });

            for (const auto& fi : _functionInfo)
            {
                if (!_combineIonMobilitySpectra && _hasAnyIonMobilityData) // assume that only ion mobility functions contribute to final spectra count; i.e. lockmass won't count
                {
                    if (fi.isIonMobilityData)
                        _numLogicalSpectra += fi.isIonMobilityData ? fi.numSpectra * 200 : 0;
                }
                else
                    _numLogicalSpectra += fi.numSpectra;
            }
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting function spectrumInfos: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }
        catch (std::exception& e)
        {
            throw e;
        }

        if (!hasMSeData)
            throw std::runtime_error("only MSe and HD-MSe data is supported at this time");

        try
        {
            auto response = _httpClient->GetAsync(chromatogramInfoEndpoint())->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + chromatogramInfoEndpoint());

            json = response->Content->ReadAsStringAsync()->Result;

            /*"value": [
            {
                "id": "9fdfae70-24bf-4d0f-a4aa-00f524c28282",
                "name": "FLR A",
                "detectorType": "FLR",
                "analyticalTechnique": {
                    "hardwareName": "ACQ-FLR#K06UPF015R"
                },
                "axisX": null,
                "axisY": null
            },
            {
                "id": "8a233325-4958-414e-a5da-0898fd0b6bd7",
                "name": "1: TOF MSe (50-2000) 45V ESI+ (TIC)",
                "detectorType": "MS",
                "analyticalTechnique": {
                    "@odata.type": "#Waters.WebApi.Common.Models.MSTechnique",
                    "hardwareName": "",
                    "scanningMethod": "MS",
                    "massAnalyser": "TIME OF FLIGHT",
                    "ionisationMode": "+",
                    "ionisationType": "ESI",
                    "lowMass": 50.0,
                    "highMass": 2000.0,
                    "adcGroup": {
                        "acquisitionMode": "ADC_PD",
                        "acquisitionFrequency": "NaN",
                        "ionResponses": [
                            {
                                "ionType": "PEPTIDE",
                                "charge": 1,
                                "averageIonArea": 25.8815208039415
                            }
                        ]
                    },
                    "tofGroup": {
                        "nominalResolution": 10000.0,
                        "mseLevel": "Low",
                        "pusherFrequency": 21739.1304347826,
                        "lteff": 800.0,
                        "veff": 3307.71621789606,
                        "samplingFrequency": 2.7
                    },
                    "quadGroup": null
                },
            },
            and so on...
            ]*/
            auto o = JObject::Parse(json);
            _chromatogramIds = gcnew System::Collections::Generic::List<System::String^>();
            for each (auto chromatogramInfo in o->SelectToken("$.value")->Children())
            {
                _chromatogramIds->Add(chromatogramInfo->SelectToken("$.id")->ToString());

                UnifiChromatogramInfo info;
                info.index = _chromatogramInfo.size();
                info.name = ToStdString(chromatogramInfo->SelectToken("$.name")->ToString());
                auto detectorType = chromatogramInfo->SelectToken("$.detectorType")->ToString();
                if (detectorType == "MS")
                {
                    if (bal::contains(info.name, "TIC"))
                        info.type = UnifiChromatogramInfo::TIC;
                }
                else if (detectorType == "UV") info.type = UnifiChromatogramInfo::UV;
                else if (detectorType == "FLR") info.type = UnifiChromatogramInfo::FLR;
                else if (detectorType == "IR") info.type = UnifiChromatogramInfo::IR;
                else if (detectorType == "NMR") info.type = UnifiChromatogramInfo::NMR;
                _chromatogramInfo.emplace_back(info);
            }
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting chromatogramInfos: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }
        catch (std::exception& e)
        {
            throw e;
        }

        if (!_hasAnyIonMobilityData)
            return;

        try
        {
            auto postContent = gcnew System::Net::Http::StringContent("{\"bins\": [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,"
                                                                        "50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,"
                                                                        "100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,123,124,125,126,127,128,129,130,131,132,133,134,135,136,137,138,139,140,141,142,143,144,145,146,147,148,149,150,"
                                                                        "151,152,153,154,155,156,157,158,159,160,161,162,163,164,165,166,167,168,169,170,171,172,173,174,175,176,177,178,179,180,181,182,183,184,185,186,187,188,189,190,191,192,193,194,195,196,197,198,199,200]}");
            postContent->Headers->ContentType->MediaType = "application/json";
            auto response = _httpClient->PostAsync(binsToDriftTimesEndPoint(), postContent)->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + binsToDriftTimesEndPoint());

            json = response->Content->ReadAsStringAsync()->Result; // {"value": [0.071,0.142,0.213,...]}

            _binToDriftTime.reserve(200);

            auto o = JObject::Parse(json);
            for each (auto value in o->SelectToken("$.value")->Children())
                _binToDriftTime.push_back((double) value);

            if (_binToDriftTime.size() != 200)
                throw gcnew Exception("convertbintodrifttime result did not contain 200 values as expected");
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting drift time values: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }
    }

    void getNumberOfSpectra()
    {
        auto response = _httpClient->GetAsync(spectrumCountEndpoint())->Result;
        if (!response->IsSuccessStatusCode)
            throw std::runtime_error("error getting number of spectra (" + ToStdString(response->StatusCode.ToString()) + ")");
        if (!Int32::TryParse(response->Content->ReadAsStringAsync()->Result, _numNetworkSpectra))
            throw std::runtime_error("error parsing number of spectra \"" + ToStdString(response->Content->ReadAsStringAsync()->Result) + "\"");
    }

    void convertWatersToPwizSpectrum(MSeMassSpectrum^ spectrum, UnifiSpectrum& result, int logicalIndex, bool getBinaryData)
    {
        result.retentionTime = spectrum->RetentionTime;
        result.scanPolarity = (Polarity)spectrum->IonizationPolarity;
        result.energyLevel = (EnergyLevel)spectrum->EnergyLevel;
        int functionIndex = result.energyLevel == EnergyLevel::Low ? 0 : 1;
        result.msLevel = functionIndex + 1;
        result.scanRange.first = _functionInfo.at(functionIndex).lowMass;
        result.scanRange.second = _functionInfo.at(functionIndex).highMass;
        result.dataIsContinuous = true;

        if (_combineIonMobilitySpectra || !_hasAnyIonMobilityData)
        {
            if (!_hasAnyIonMobilityData && spectrum->ScanSize->Length > 1)
                throw std::runtime_error("non-ion-mobility spectrum with ScanSize.Length > 1");

            result.driftTime = 0;
            result.arrayLength = spectrum->mzArray->size();

            if (getBinaryData && result.arrayLength > 0)
            {
                //ToStdVector(spectrum->Masses, result.mzArray);
                //ToStdVector(spectrum->Intensities, result.intensityArray);
                result.mzArray = *spectrum->mzArray;
                result.intensityArray = *spectrum->intensityArray;
                if (spectrum->driftTimeArray)
                    result.driftTimeArray = *spectrum->driftTimeArray;
            }
        }
        else
        {
            if (spectrum->ScanSize->Length == 1)
                throw std::runtime_error("assumed ion-mobility spectrum but ScanSize.Length == 1");

            if (spectrum->ScanSize->Length != 200)
                throw std::runtime_error("assumed ion-mobility spectrum but ScanSize.Length != 200");

            int driftScanIndex = logicalIndex % 200;
            result.driftTime = _binToDriftTime[driftScanIndex];
            result.arrayLength = spectrum->ScanSize[driftScanIndex];

            if (getBinaryData && result.arrayLength > 0)
            {
                int driftScanArrayOffset = spectrum->ScanIndexes[driftScanIndex];
                ToBinaryData(spectrum->Masses, driftScanArrayOffset, result.mzArray, 0, result.arrayLength);
                ToBinaryData(spectrum->Intensities, driftScanArrayOffset, result.intensityArray, 0, result.arrayLength);
            }
        }
    }

    virtual void getSpectrum(size_t index, UnifiSpectrum& result, bool getBinaryData, bool doCentroid)
    {
        try
        {
            MSeMassSpectrum^ spectrum = nullptr;
            int networkIndex = networkIndexFromLogicalIndex(index);
            int taskIndex = taskIndexFromSpectrumIndex(networkIndex);
            if (_cache->TryGet(networkIndex, spectrum) && spectrum != nullptr)
            {
                convertWatersToPwizSpectrum(spectrum, result, index, getBinaryData);

                // also queue the next _chunkReadahead chunks
                for (int i = 1; i < _chunkReadahead; ++i)
                {
                    if ((taskIndex + _chunkSize * i) > _numNetworkSpectra)
                        break;
                    //Console::WriteLine("Queueing chunk {0} after finding {1} in cache", taskIndex + _chunkSize * i, taskIndex);

                    int lastNetworkIndexOfChunk = taskIndex + ((_chunkSize + 1) * i - 1);
                    if (!_cache->Contains(lastNetworkIndexOfChunk)) // if cache contains last index for chunk, don't requeue it
                        _queue->getChunkTask(taskIndex + _chunkSize * i, false, false, false);
                }

                // remove earlier spectra from the cache
                /*if (index > _chunkSize && (index % (_chunkSize/2)) == 0)
                {
                    for (size_t i = index - _chunkSize/2; i > 0; --i)
                        _cache->Remove(i - 1);
#ifdef _WIN32 //DEBUG
                    Console::WriteLine(System::String::Format("Removed tasks {0}-{1} from cache", "0", index - _chunkSize));
#endif
                }*/

                return;
            }

            // get the primary chunkTask;
            // if cache is empty, wait for primary chunk download to start before starting other chunks
            auto chunkTask = _queue->getChunkTask(taskIndex, false, true, _cache->Count == 0);

            // also queue the next _chunkReadahead chunks
            for (int i = 1; i < _chunkReadahead; ++i)
            {
                if ((taskIndex + _chunkSize * i) > _numNetworkSpectra)
                    break;
                //Console::WriteLine("Queueing chunk {0} after waiting for {1}", taskIndexFromSpectrumIndex(index) + _chunkSize * i, taskIndex);

                int lastNetworkIndexOfChunk = taskIndex + ((_chunkSize + 1) * i - 1);
                if (!_cache->Contains(lastNetworkIndexOfChunk)) // if cache contains last index for chunk, don't requeue it
                    _queue->getChunkTask(taskIndex + _chunkSize * i, false, false, false);
            }
            if (_unifiDebug)
                Console::Error->WriteLine("WAITING for chunk {0}", taskIndex);
            chunkTask->Wait(); // wait for the task to finish

            spectrum = (MSeMassSpectrum^) _cache->Get(networkIndex);
            if (spectrum == nullptr)
                throw std::runtime_error("spectrum still null after task finished: " + lexical_cast<string>(index));

            convertWatersToPwizSpectrum(spectrum, result, index, getBinaryData);

        } CATCH_AND_FORWARD_EX(index)
    }

    const std::vector<UnifiChromatogramInfo>& chromatogramInfo()
    {
        return _chromatogramInfo;
    }

    virtual void getChromatogram(size_t index, UnifiChromatogram& chromatogram, bool getBinaryData)
    {
        try
        {
            if (index > _chromatogramInfo.size())
                throw gcnew ArgumentOutOfRangeException("index");

            chromatogram.name = _chromatogramInfo[index].name;
            chromatogram.index = _chromatogramInfo[index].index;
            chromatogram.type = _chromatogramInfo[index].type;

            auto response = _httpClient->GetAsync(chromatogramEndpoint(_chromatogramIds->default[index]))->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + chromatogramInfoEndpoint());

            auto json = response->Content->ReadAsStringAsync()->Result;

            /*{
             "id":"9fdfae70-24bf-4d0f-a4aa-00f524c28282",
             "retentionTimes":[0,0.008333334,0.0166666675],
             "intensities":[0,-0.000260543835,-0.000520992267],
             "peaks":[...not used by pwiz...]
            }*/
            auto o = JObject::Parse(json);

            if (!getBinaryData)
            {
                chromatogram.arrayLength = 0;
                for each (auto value in o->SelectToken("$.retentionTimes")->Children())
                {
                    ++chromatogram.arrayLength;
                    (void)value; // suppress warning
                }
                return;
            }

            for each (auto value in o->SelectToken("$.retentionTimes")->Children())
                chromatogram.timeArray.push_back((double) value);

            chromatogram.intensityArray.reserve(chromatogram.timeArray.size());
            for each (auto value in o->SelectToken("$.intensities")->Children())
                chromatogram.intensityArray.push_back((double) value);
            if (chromatogram.intensityArray.size() != chromatogram.timeArray.size())
                throw gcnew Exception("retentionTimes and intensities array had different sizes: UNIFI bug?");

            chromatogram.arrayLength = chromatogram.timeArray.size();
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting chromatogramInfos: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }
        catch (std::exception& e)
        {
            throw e;
        }
    }

};

} // UNIFI
} // vendor_api
} // pwiz


#include "WatersConnectData.ipp"


namespace pwiz {
namespace vendor_api {
namespace UNIFI {

PWIZ_API_DECL
UnifiData::UnifiData(const std::string& sampleResultUrl, bool combineIonMobilitySpectra)
{
    if (bal::icontains(sampleResultUrl, "unifi/v1/"))
        _impl.reset(new Impl);
    else if (bal::contains(sampleResultUrl, "injectionId"))
        _impl.reset(new waters_connect::WatersConnectImpl);
    else
        throw std::runtime_error("unsupported URL: " + sampleResultUrl);

    _impl->connect(sampleResultUrl, combineIonMobilitySpectra);
}

PWIZ_API_DECL
UnifiData::~UnifiData()
{
}

PWIZ_API_DECL size_t UnifiData::numberOfSpectra() const { return (size_t) _impl->_numLogicalSpectra; }

PWIZ_API_DECL void UnifiData::getSpectrum(size_t index, UnifiSpectrum& spectrum, bool getBinaryData, bool doCentroid) const { return _impl->getSpectrum(index, spectrum, getBinaryData, doCentroid); }
PWIZ_API_DECL int UnifiData::getMsLevel(size_t index) const { return _impl->getMsLevel(index); }
PWIZ_API_DECL void UnifiData::getChannelAndScanIndex(size_t index, int& channelIndex, int& scanIndexInChannel) const { _impl->getChannelAndScanIndex(index, channelIndex, scanIndexInChannel); }

PWIZ_API_DECL const std::vector<UnifiChromatogramInfo>& UnifiData::chromatogramInfo() const { return _impl->_chromatogramInfo;  }
PWIZ_API_DECL void UnifiData::getChromatogram(size_t index, UnifiChromatogram& chromatogram, bool getBinaryData) const { return _impl->getChromatogram(index, chromatogram, getBinaryData); }

PWIZ_API_DECL const boost::local_time::local_date_time& UnifiData::getAcquisitionStartTime() const { return _impl->_acquisitionStartTime.value(); }
PWIZ_API_DECL const std::string& UnifiData::getSampleName() const { return _impl->_sampleName; }
PWIZ_API_DECL const std::string& UnifiData::getSampleDescription() const { return _impl->_sampleDescription; }
PWIZ_API_DECL int UnifiData::getReplicateNumber() const { return _impl->_replicateNumber; }
PWIZ_API_DECL const std::string& UnifiData::getWellPosition() const { return _impl->_wellPosition; }


PWIZ_API_DECL bool UnifiData::hasIonMobilityData() const { return _impl->_hasAnyIonMobilityData; }

PWIZ_API_DECL bool UnifiData::canConvertDriftTimeAndCCS() const { return false; }
PWIZ_API_DECL double UnifiData::driftTimeToCCS(double driftTimeInMilliseconds, double mz, int charge) const { return 0; }
PWIZ_API_DECL double UnifiData::ccsToDriftTime(double ccs, double mz, int charge) const { return 0; }

PWIZ_API_DECL UnifiData::RemoteApi UnifiData::getRemoteApiType() const { return _impl->getRemoteApiType(); }

} // UNIFI
} // vendor_api
} // pwiz

#pragma warning(pop)

#endif