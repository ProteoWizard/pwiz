//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2024 University of Washington
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
#include "UnifiData.hpp"

#ifndef PWIZ_READER_UNIFI
#error compiler is not MSVC or DLL not available
#else // PWIZ_READER_UNIFI

#pragma unmanaged
//#include "WatersConnectData.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/Exception.hpp"

#pragma warning(push)
#pragma warning(disable: 4400 4538 4564)

#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
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
using namespace System::Net::Http::Headers;
using namespace Newtonsoft::Json;
using namespace Newtonsoft::Json::Linq;
using namespace System::Runtime::Caching::Generic;
using namespace System::Collections::Generic;
using namespace System::Collections::Concurrent;
using System::Threading::Tasks::Task;
using System::Threading::Tasks::TaskScheduler;
using System::Threading::Tasks::Schedulers::QueuedTaskScheduler;
using System::Net::Http::HttpClient;
using System::Uri;
using IdentityModel::Client::TokenClient;
using IdentityModel::Client::TokenResponse;
using std::size_t;
#include "WatersConnectProtobuf.hpp"


namespace pwiz {
namespace vendor_api {
namespace waters_connect {

generic <typename T> where T : Enum
static T ParseEnum(String^ value)
{
    return safe_cast<T>(Enum::Parse(T::typeid, value));
}

public enum class ChannelDataShape
{
    Spectrum,
    Spectra,
    Chromatogram,
    Diagnostics,
    Other
};

public enum class ChannelDataType
{
    Unknown,
    MSScanning,
    IMSScanning,
    UVScanning,
    MSSpectrum,
    UVSpectrum,
    MSChromatogram,
    UVChromatogram,
    IRSpectrum,
    IRChromatogram,
    NMRFidSpectrum,
    FLRChromatogram,
    FLRSpectrum,
    FLRScanning,
    NMRSpectrum,
    NMRPeakSpectrum,
    Generic2D,
    Generic2DChromatogram,
    Generic2DSpectrum,
    Generic3DScanning,
    Generic3DSparse,
    Generic
};

public enum class ScanningMethod
{
    Unknown,
    MS,
    PRECURSOR,
    PRODUCT,
    SCAN_WAVE_PRODUCT,
    MSMS,
    SIR,
    MRM,
    NL,
    NG,
    PICS,
    DDA
};


class WatersConnectImpl : public UNIFI::UnifiData::Impl
{
public:
    virtual void connect(const std::string& baseUrlWithInjectionAndSampleSetId, bool combineIonMobilitySpectra)
    {
        _combineIonMobilitySpectra = combineIonMobilitySpectra;
        ServicePointManager::SecurityProtocol = SecurityProtocolType::Tls12;

        try
        {
            Uri^ temp;
            if (bal::starts_with(baseUrlWithInjectionAndSampleSetId, "http://") || bal::starts_with(baseUrlWithInjectionAndSampleSetId, "https://"))
                temp = gcnew Uri(ToSystemString(baseUrlWithInjectionAndSampleSetId));
            else
                temp = gcnew Uri(ToSystemString("https://" + baseUrlWithInjectionAndSampleSetId));

            _apiVersion = 2;
            String^ defaultIdentityServer = System::String::Format("{0}://{1}:{2}", temp->Scheme, temp->Host, 48333);
            String^ defaultClientScope = L"webapi";

            auto queryVars = System::Web::HttpUtility::ParseQueryString(temp->Query);
            _identityServerUrl = gcnew Uri(queryVars[L"identity"] == nullptr ? defaultIdentityServer : queryVars[L"identity"]);
            _clientScope = queryVars[L"scope"] == nullptr ? defaultClientScope : queryVars[L"scope"];
            _clientSecret = queryVars[L"secret"] == nullptr ? L"secret" : queryVars[L"secret"];
            _clientId = queryVars[L"clientId"] == nullptr ? L"resourceownerclient_jwt" : queryVars[L"clientId"];
            _sampleResultUrl = _baseUrl = gcnew Uri(temp->GetLeftPart(UriPartial::Path));
            _sampleSetId = queryVars[L"sampleSetId"] != nullptr ? queryVars[L"sampleSetId"] : throw gcnew ArgumentException("sampleSetId parameter is required");
            _injectionId = queryVars[L"injectionId"] != nullptr ? queryVars[L"injectionId"] : throw gcnew ArgumentException("injectionId parameter is required");

            initHttpClient();
            getAccessToken();
            getHttpClient();

            _spectrumEndpoint = gcnew Action<ParallelDownloadQueue::DownloadInfo^>(&WatersConnectImpl::spectrumEndpoint);
            _getSpectrumFromStream = gcnew Action<IO::Stream^, ParallelDownloadQueue::DownloadInfo^>(&WatersConnectImpl::getSpectraFromStream);

            getSampleMetadata();
            getNumberOfSpectra();
            //Console::WriteLine("numLogicalSpectra: {0}, numNetworkSpectra: {1}", _numLogicalSpectra, _numNetworkSpectra);

            auto wcDebug = System::Environment::GetEnvironmentVariable("WC_DEBUG");
            _wcDebug = wcDebug != nullptr && wcDebug != "0";

            _chunkSize = 20;// Math::Max(10, (int)std::ceil(_numNetworkSpectra / 500.0));

#ifdef _WIN64
            int idealChunkReadahead = _chunkReadahead = 2;
#else
            int idealChunkReadahead = _chunkReadahead = 2;
#endif
            String^ acceptHeader = "application/x-protobuf";
            if (_numNetworkSpectra > 0)
            {
                auto firstSpectrumDownloadInfo = gcnew ParallelDownloadQueue::DownloadInfo();
                firstSpectrumDownloadInfo->spectrumIndexStart = 0;
                firstSpectrumDownloadInfo->spectrumIndexEnd = 1;
                firstSpectrumDownloadInfo->chunkSize = 1;
                firstSpectrumDownloadInfo->userdata = gcnew IntPtr(this);
                spectrumEndpoint(firstSpectrumDownloadInfo);
                _chunkReadahead = ParallelDownloadQueue::GetRequestLimit(firstSpectrumDownloadInfo->spectrumEndpoint, _httpClientFactory, _accessToken, acceptHeader, _chunkReadahead);
            }
            _chunkSize = (double)idealChunkReadahead / _chunkReadahead * _chunkSize;

            if (queryVars[L"chunkSize"] != nullptr) _chunkSize = lexical_cast<int>(ToStdString(queryVars[L"chunkSize"]));
            if (queryVars[L"chunkReadahead"] != nullptr) _chunkReadahead = lexical_cast<int>(ToStdString(queryVars[L"chunkReadahead"]));

            _cacheSize = _chunkSize * _chunkReadahead * 2;

            _profileCache = gcnew MemoryCache<int, MzSpectrumItemDtoV2^>(_cacheSize);
            _profileCache->SetPolicy(LruEvictionPolicy<int, MzSpectrumItemDtoV2^>::typeid->GetGenericTypeDefinition());
            _centroidCache = gcnew MemoryCache<int, MzSpectrumItemDtoV2^>(_cacheSize);
            _centroidCache->SetPolicy(LruEvictionPolicy<int, MzSpectrumItemDtoV2^>::typeid->GetGenericTypeDefinition());

            if (_numNetworkSpectra > 0)
            {
                int perChannelReadahead = std::max(1, _chunkReadahead / static_cast<int>(_channelInfo.size()));
                for (auto& ci : _channelInfo)
                {
                    ci.channelQueue = gcnew ParallelDownloadQueue(_baseUrl, _accessToken, _httpClientFactory, ci.numSpectra, _binToDriftTime, _chunkSize, perChannelReadahead,
                        acceptHeader, _spectrumEndpoint, _getSpectrumFromStream, gcnew IntPtr(this));
                }
            }
        }
        CATCH_AND_FORWARD_EX(baseUrlWithInjectionAndSampleSetId)
    }

    virtual ~WatersConnectImpl() {}

    virtual UNIFI::UnifiData::RemoteApi getRemoteApiType() { return UNIFI::UnifiData::RemoteApi::Waters_Connect; }

    virtual int getMsLevel(size_t index) const
    {
        const auto& channelInfo = *_spectrumIndex.at(index).channelInfo;
        switch ((ScanningMethod) channelInfo.scanningMethod)
        {
            default:
            case ScanningMethod::Unknown:
            case ScanningMethod::SCAN_WAVE_PRODUCT:
                return 0;
            case ScanningMethod::MS:
            case ScanningMethod::SIR:
                return 1;
            case ScanningMethod::PRECURSOR:
                return -1;
            case ScanningMethod::PRODUCT:
            case ScanningMethod::MSMS:
            case ScanningMethod::DDA:
            case ScanningMethod::MRM:
            case ScanningMethod::NL:
            case ScanningMethod::NG:
            case ScanningMethod::PICS:
                return 2;
        }
    }

    virtual void getChannelAndScanIndex(size_t index, int& channelIndex, int& scanIndexInChannel) const
    {
        const auto& spectrumId = _spectrumIndex.at(index);
        channelIndex = spectrumId.channelInfo->index;
        scanIndexInChannel = spectrumId.scanIndexInChannel;
    }

    friend class UnifiData;

private:
    System::String^ tokenEndpoint() { return System::Uri(_identityServerUrl, L"/connect/token").ToString(); }

    /// returns a JSON array describing the injections in the sample set
    System::String^ injectionsMetadataEndpoint() { return _baseUrl + "waters_connect/v2.0/sample-sets/" + _sampleSetId + "/injection-data"; }

    /// returns a JSON array describing the 'functions' (aka scan events), for example:
    System::String^ channelMetadataEndpoint() { return _baseUrl + "waters_connect/v2.0/injection-data/" + _injectionId + "/channels"; }

    /// returns a JSON array with a TIC chromatogram for each channel
    System::String^ allChannelsTicEndpoint() { return _baseUrl + "waters_connect/v2.0/injection-data/" + _injectionId + "/channels/ms/tic"; }

    /// returns a JSON array with MRM chromatograms
    System::String^ mrmChromatogramsEndpoint() { return _baseUrl + "waters_connect/v2.0/injection-data/" + _injectionId + "/channels/mrm"; }

    /// returns a JSON array or protobuf stream of the spectral intensities and masses (if the HTTP Accept header specifies 'application/x-protobuf')
    static void spectrumEndpoint(ParallelDownloadQueue::DownloadInfo^ downloadInfo)
    {
        auto instance = static_cast<WatersConnectImpl*>(safe_cast<IntPtr^>(downloadInfo->userdata)->ToPointer());
        const auto& indexEntry = instance->_spectrumIndex.at(downloadInfo->taskIndex);
        const auto& channelInfo = *indexEntry.channelInfo;
        int adjustedTaskIndex = downloadInfo->spectrumIndexStart = indexEntry.scanIndexInChannel;
        downloadInfo->spectrumIndexEnd = adjustedTaskIndex + downloadInfo->chunkSize;
        downloadInfo->spectrumEndpoint = instance->_baseUrl + "waters_connect/v2.0/injection-data/" + instance->_injectionId + "/ms/spectra" +
            "?channelFilter=channelId eq " + ToSystemString(channelInfo.id) +
            "&scanFilter=index ge " + adjustedTaskIndex + " and index lt " + downloadInfo->spectrumIndexEnd +
            "&spectrumType=" + (downloadInfo->getCentroidData ? "Centered" : "Continuum");
        //downloadInfo->taskIndex = adjustedTaskIndex;
    }

    /// returns a JSON array of the chromatogram times and intensities
    /*{
     "id":"9fdfae70-24bf-4d0f-a4aa-00f524c28282",
     "retentionTimes":[0,0.008333334,0.0166666675],
     "intensities":[0,-0.000260543835,-0.000520992267],
     "peaks":[...not used by pwiz...]
    }*/
    System::String^ chromatogramEndpoint(System::String^ chromatogramInfoId) { return _baseUrl + "/chromatogramInfos(" + chromatogramInfoId + ")/data"; }

    /// a POST request to this with a JSON body of (1-based?) bin indexes, e.g. {"bins": [1,2,3,4,5]}
    System::String^ binsToDriftTimesEndPoint() { return _baseUrl + "/spectra/mass.mse/convertbintodrifttime"; }

    gcroot<Uri^> _baseUrl;
    gcroot<System::String^> _sampleSetId;
    gcroot<System::String^> _injectionId;
    bool _wcDebug;
    gcroot<MemoryCache<int, MzSpectrumItemDtoV2^>^> _profileCache, _centroidCache;
    gcroot<Action<ParallelDownloadQueue::DownloadInfo^>^> _spectrumEndpoint;
    gcroot<Action<IO::Stream^, ParallelDownloadQueue::DownloadInfo^>^> _getSpectrumFromStream;

    struct ChannelInfo
    {
        int index;
        string id;
        string name;
        gcroot<ChannelDataShape> channelDataShape;
        gcroot<ChannelDataType> channelDataType;
        gcroot<ScanningMethod> scanningMethod;
        string ionizationType;
        UNIFI::Polarity ionizationMode;
        bool isIonMobilityData;
        //bool hasCCSCalibration;
        int numSpectra;
        int scanIndexOffset; // sum of numSpectra for channels before this one in enumeration order
        double lowMass, highMass;
        UNIFI::EnergyLevel energyLevel;
        gcroot<ParallelDownloadQueue^> channelQueue;
        const UNIFI::UnifiChromatogram* ticChromatogram;
    };

    struct SpectrumId
    {
        SpectrumId(double rt, const ChannelInfo* ci, int index)
            : retentionTime(rt), channelInfo(ci), scanIndexInChannel(index)
        {}

        double retentionTime;
        const ChannelInfo* channelInfo;
        int scanIndexInChannel;
    };
    vector<SpectrumId> _spectrumIndex;
    vector<ChannelInfo> _channelInfo;
    map<string, ChannelInfo*> _channelInfoById;
    //map<string, ChannelInfo*> _channelInfoByTitle;
    map<pair<int, int>, size_t> _spectrumIdByChannelAndScanIndex;
    vector<UNIFI::UnifiChromatogram> _chromatograms;
    map<string, const UNIFI::UnifiChromatogram*> _chromatogramByName;

    ChannelInfo& getChannelInfoById(const string& id) const
    {
        auto findItr = _channelInfoById.find(id);
        if (findItr == _channelInfoById.end())
            throw std::runtime_error("channel with id " + id + " not found");
        return *findItr->second;
    }

    static void getSpectraFromStream(IO::Stream^ stream, ParallelDownloadQueue::DownloadInfo^ downloadInfo)
    {
        auto instance = static_cast<WatersConnectImpl*>(safe_cast<IntPtr^>(downloadInfo->userdata)->ToPointer());
        bool doCentroid = downloadInfo->getCentroidData;
        auto cache = doCentroid ? instance->_centroidCache : instance->_profileCache;
        auto collection = ProtoBuf::Serializer::Deserialize<MzSpectraDtoV2Collection^>(stream);
        if (collection == nullptr || collection->Items->Count == 0)
            throw gcnew Exception(String::Format("deserialized null spectra collection for block [{0}, {1})", downloadInfo->spectrumIndexStart, downloadInfo->spectrumIndexEnd));

        auto spectra = collection->Items[0];
        //if (spectra->Values->Count != downloadInfo->spectrumIndexEnd - downloadInfo->spectrumIndexStart)
        //    throw gcnew Exception(String::Format("deserialized spectra collection has {2} spectra for block [{0}, {1})", downloadInfo->spectrumIndexStart, downloadInfo->spectrumIndexEnd, spectra->Values->Count));

        //int taskIndex = downloadInfo->taskIndex;
        const auto& taskSpectrumId = instance->_spectrumIndex[downloadInfo->taskIndex];
        for (int i=0; i < spectra->Values->Count; ++i)
        {
            int spectrumIndex;
            if (i > 0)
            {
                // if spectrum is not the first, its position in the spectrum index is probably not consecutive, so we have to look it up in the index by channel and scan index within its channel
                auto spectrumIdInChannel = make_pair(taskSpectrumId.channelInfo->index, taskSpectrumId.scanIndexInChannel + i);
                spectrumIndex = static_cast<int>(instance->_spectrumIdByChannelAndScanIndex.find(spectrumIdInChannel)->second);
            }
            else
            {
                spectrumIndex = downloadInfo->taskIndex;
            }

            auto spectrum = spectra->Values[i];
            if (doCentroid && (spectrum->CentroidSpectrumData == nullptr || spectrum->CentroidSpectrumData->MOverZs == nullptr) ||
                !doCentroid && (spectrum->ContinuumSpectrumData == nullptr || spectrum->ContinuumSpectrumData->Masses == nullptr))
            {
                spectrum->mzArray = new vector<double>();
                spectrum->intensityArray = new vector<double>();
                spectrum->driftTimeArray = nullptr;
                if (!cache->Contains(spectrumIndex))
                {
                    //Console::WriteLine("Adding result to cache: {0}", taskIndex + i);
                    cache->Add(spectrumIndex, spectrum);
                }
                continue;
            }

            int dataPointCount = doCentroid ? spectrum->CentroidSpectrumData->MOverZs->Length : spectrum->ContinuumSpectrumData->Masses->Length;
            downloadInfo->bytesDownloaded += sizeof(double) * dataPointCount * 2;

            if (downloadInfo->bytesDownloaded == 0)
                throw gcnew Exception(System::String::Format("empty array for index {0} (spectrum {1})", i, spectrumIndex));

            spectrum->mzArray = new vector<double>();
            spectrum->intensityArray = new vector<double>();
            spectrum->driftTimeArray = nullptr;
            spectrum->MsTechnique = spectra->MsTechnique;
            auto& mzArray = *spectrum->mzArray;
            auto& intensityArray = *spectrum->intensityArray;

            if (!doCentroid)
            {
                auto profileData = spectrum->ContinuumSpectrumData;
                ToStdVector(profileData->Masses, mzArray);
                ToStdVector(profileData->Intensities, intensityArray);
            }
            else
            {
                auto centroidData = spectrum->CentroidSpectrumData;
                ToStdVector(centroidData->MOverZs, mzArray);
                ToStdVector(centroidData->Intensities, intensityArray);
            }

            if (!cache->Contains(spectrumIndex))
            {
                //Console::WriteLine("Adding result to cache: {0}", taskIndex + i);
                cache->Add(spectrumIndex, spectrum);
            }
        }

        // if parsing spectra takes more than 10 seconds, something's wrong
        auto timeElapsed = DateTime::UtcNow - downloadInfo->lastSpectrumRetrievedTime;
        //if (timeElapsed.TotalSeconds > 10.0)
         //   throw gcnew Exception(System::String::Format("download is going too slow in chunk {0} on thread {1}: one chunk ({2}) took {3}s", taskIndex, downloadInfo->currentThreadId, taskIndex, timeElapsed.TotalSeconds));
        downloadInfo->lastSpectrumRetrievedTime = DateTime::UtcNow;
    }

    size_t taskIndexFromSpectrumIndex(const ChannelInfo& ci, size_t index)
    {
        return index;
        //int naiveTaskIndex = (index / _chunkSize) * _chunkSize;
        //return std::max(ci.scanIndexOffset, naiveTaskIndex);
    }

    size_t networkIndexFromLogicalIndex(size_t logicalIndex)
    {
        if (!_hasAnyIonMobilityData || _combineIonMobilitySpectra)
            return logicalIndex;

        // 0-199 -> 0
        // 200-399 -> 1
        // 400-599 -> 2
        // etc.
        return (size_t)floor(logicalIndex / 200.0);
    }

    static Object^ getAccessTokenResult(String^ uri, AccessTokenRequest^ request)
    {
        auto fields = gcnew System::Collections::Generic::Dictionary<System::String^, System::String^>();
        fields->Add(IdentityModel::OidcConstants::TokenRequest::GrantType, IdentityModel::OidcConstants::GrantTypes::Password);
        fields->Add(IdentityModel::OidcConstants::TokenRequest::UserName, request->Username);
        fields->Add(IdentityModel::OidcConstants::TokenRequest::Password, request->Password);
        fields->Add(IdentityModel::OidcConstants::TokenRequest::Scope, request->Scope);

        auto tokenClient = gcnew TokenClient(request->Uri, request->ClientId, request->Secret, nullptr);
        TokenResponse^ response = tokenClient->RequestAsync(fields, System::Threading::CancellationToken::None)->Result;
        if (response->IsError)
            throw user_error("authentication error: incorrect hostname, username or password? (" + ToStdString(response->Error) + ")");
        return gcnew KeyValuePair<String^, DateTime>(response->AccessToken, DateTime::UtcNow.AddSeconds(response->ExpiresIn));
    }

    void getAccessToken()
    {
        cli::array<String^>^ userPassPair;
        String^ username, ^ password;
        if (String::IsNullOrEmpty(_baseUrl->UserInfo))
        {
            username = Environment::GetEnvironmentVariable("WC_USERNAME");
            password = Environment::GetEnvironmentVariable("WC_PASSWORD");
            if (username == nullptr || password == nullptr || username->Length == 0 || password->Length == 0)
                throw user_error("UserInfo null; username and password must be specified in the waters_connect URL (e.g. username:password@watersconnectserver.com:{port}/?sampleSetId={id}&injectionId={id})");
        }
        else
        {
            userPassPair = _baseUrl->UserInfo->Split(':');
            if (userPassPair->Length != 2 || String::IsNullOrEmpty(userPassPair[0]) || String::IsNullOrEmpty(userPassPair[1]))
                throw user_error("UserInfo not a pair of values; username and password must be specified in the waters_connect URL (e.g. username:password@watersconnectserver.com:{port}/?sampleSetId={id}&injectionId={id})");
            username = userPassPair[0];
            password = userPassPair[1];
        }

        AccessTokenRequest^ request = gcnew AccessTokenRequest();
        request->Username = username;
        request->Password = password;
        request->Scope = _clientScope;
        request->Secret = _clientSecret;
        request->ClientId = _clientId;
        request->Uri = tokenEndpoint();

        auto tokenEndpointUrlSerialized = tokenEndpoint() + "/" + username + "/" + password + "/" + _clientScope + "/" + _clientSecret + "/" + _clientId;
        auto delgt = gcnew Func<String^, AccessTokenRequest^, Object^>(getAccessTokenResult);
        auto cachedToken = safe_cast<KeyValuePair<String^, DateTime>^>(globalResponseCache->GetOrAdd(tokenEndpointUrlSerialized, delgt, request));
        if (DateTime::UtcNow >= cachedToken->Value)
        {
            String^ d;
            globalResponseCache->TryRemove(tokenEndpointUrlSerialized, d);
            cachedToken = safe_cast<KeyValuePair<String^, DateTime>^>(globalResponseCache->GetOrAdd(tokenEndpointUrlSerialized, delgt, request));
        }
        _accessToken = cachedToken->Key;
        //Console::WriteLine(_accessToken);
    }

    /*void getHttpClient()
    {
        _httpClient->DefaultRequestHeaders->Authorization = gcnew AuthenticationHeaderValue("Bearer", _accessToken);
        _httpClient->BaseAddress = gcnew Uri(_baseUrl->GetLeftPart(System::UriPartial::Authority));
    }*/

    static Object^ getHttpResult(String^ uri, HttpClient^ httpClient)
    {
        try
        {
            auto response = httpClient->GetAsync(uri)->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + uri);

            return response->Content->ReadAsStringAsync()->Result;
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting sample result metadata: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }
    }

    void getSampleMetadata()
    {
        auto delgt = gcnew Func<String^, HttpClient^, Object^>(getHttpResult);
        HttpClient^ httpClient = _httpClient;
        String^ json = safe_cast<String^>(globalResponseCache->GetOrAdd(injectionsMetadataEndpoint(), delgt, httpClient));

        try
        {
            /* Injections metadata JSON (can be more than one)
             [
             {
                "id": "847a6591-67f2-4d5f-8922-51de74b66e38",
                "name": "Hazell_Data_00004",
                "description": "mix 1",
                "createdDate": "2024-08-22T09:23:52.537+01:00",
                "lastModifiedDate": "2024-08-22T09:23:52.537+01:00",
                "acquisitionStatus": "COMPLETE",
                "injectionProperties": {
                    "instrumentSystemName": "XEVO-TQSmicro#QEA0215",
                    "instrumentSystemType": null,
                    "actualReplicateCount": 1,
                    "replicateCount": 1,
                    "customProperties": {
                        "Acquired Date": "28-Jan-2019",
                        "Acquired Time": "12:53:14",
                        "Job Code": "For Hans MRM Opt",
                        "Cal MS1 Static Params": "72,2020,0.00,5.00,7.5,15.1,-0.3,ESI Calibration TQ ResCal,STATMS1",
                        "Cal MS1 Dynamic Params": "10,2048,20.3800,-1.00,7.5,15.1,-0.3,ESI Calibration TQ ResCal,SCNMS1",
                        "Cal MS1 Fast Params": "10,2048,1.0190,-1.00,7.5,15.1,-0.3,ESI Calibration TQ ResCal,FASTMS1",
                        "Cal MS2 Static Params": "72,2020,0.00,5.00,11.6,14.8,0.3,ESI Calibration TQ ResCal,STATMS2",
                        "Cal MS2 Dynamic Params": "10,2048,20.3800,-1.00,11.6,14.8,0.3,ESI Calibration TQ ResCal,SCNMS2",
                        "Cal MS2 Fast Params": "10,2048,0.2038,-1.00,11.6,14.8,0.3,ESI Calibration TQ ResCal,FASTMS2",
                        "Version": "01.00",
                        "SampleID": "Hazell_Data_00004",
                        "Plate Desc": "01TL,YX,SD,1: 6,2: 8,3:140.0,4:140.0,5:  0.0,6:  0.0,7:O,8:O,9:1280.0,10:860.0,11:320.0,12:120.0,13:150.0,14: 83.0",
                        "Cal MS1 Static": "-2.596651864123462e-1,1.000444710218909e0,-7.559249678797051e-7,5.126033846915850e-10,-1.163544707673988e-13,T0",
                        "Cal MS2 Static": "-2.348563764391141e-1,1.000384157168179e0,-4.655120708431079e-7,2.076951340936362e-10,-3.211216865467979e-14,T0",
                        "Cal Time": "14:28",
                        "Cal Date": "01/09/19",
                        "Analog Channel 1 Offset": "0.000",
                        "Analog Channel 2 Offset": "0.000",
                        "Analog Channel 3 Offset": "0.000",
                        "Analog Channel 4 Offset": "0.000",
                        "Mux Stream": "0",
                        "Inlet Method": "C:\\MassLynx\\Targeted Peptides .pro\\ACQUDB\\Targeted_Method_10_Mins _Peptides slow flow",
                        "MS Method": "C:\\MassLynx\\Targeted Peptides .pro\\ACQUDB\\StepAwithFalseNegative.EXP",
                        "Tune Method": "C:\\MassLynx\\Targeted Peptides .pro\\ACQUDB\\Targeted_Methods_Peptides.IPR",
                        "PIC Scan Function": "0,0",
                        "Calibration File": "C:\\MassLynx\\IntelliStart\\Results\\Unit Resolution\\Calibration_20190109_3.cal"
                    },
                    "sampleId": "00000000000000000000000000000000",
                    "sampleType": "Unknown",
                    "injectionVolume": 2.0,
                    "samplePosition": "2:48",
                    "replicates": 1,
                    "replicateIndex": 1,
                    "acquisitionMethodVersionId": "00000000-0000-0000-0000-000000000000",
                    "acquisitionMethodName": "",
                    "submitterName": "",
                    "extendedProperties": {
                        "RunTime": 90.0,
                        "SampleWeight": "NaN",
                        "Dilution": "NaN",
                        "AcquisitionRunTime": 90.0,
                        "LegacyAcquisitionStartTime": "Mon, 28 Jan 2019 12:53:14 (00:00)",
                        "Time": "NaN",
                        "ProcessingSequenceNumber": 0,
                        "Dose": "NaN",
                        "SolventDelay": "NaN"
                    }
                },
                "values": {}
            }
            ]
            */
            auto injectionsMetadata = JArray::Parse(json);
            JToken^ injectionMetadata = nullptr;
            for each (auto injection in injectionsMetadata)
            {
                if (injection->SelectToken("$.id")->ToString() == _injectionId)
                {
                    injectionMetadata = injection;
                    break;
                }
            }
            if (injectionMetadata == nullptr)
                throw gcnew Exception("injection with id " + _injectionId + " not found in sample set " + _sampleSetId);

            _sampleName = ToStdString(injectionMetadata->SelectToken("$.name")->ToString());
            _sampleDescription = ToStdString(injectionMetadata->SelectToken("$.description")->ToString());
            _replicateNumber = Convert::ToInt32(injectionMetadata->SelectToken("$.injectionProperties.replicateIndex")->ToString());
            //_wellPosition = ToStdString(injectionMetadata->SelectToken("$.sample.wellPosition")->ToString());

            // acquisition time can occur at different places in the metadata and in different formats
            DateTime acquisitionTime = DateTime::MinValue;
            auto acquisitionTimeToken = injectionMetadata->SelectToken("$.injectionProperties.extendedProperties.LegacyAcquisitionStartTime");
            if (acquisitionTimeToken != nullptr)
            {
                // formatted like "Mon, 28 Jan 2019 12:53:14 (00:00)"
                auto acquisitionTimeStr = Text::RegularExpressions::Regex::Replace(acquisitionTimeToken->ToString(), "(.*) \\((\\d\\d\\:\\d\\d)\\)", "$1 +$2"); // add offset plus sign if it's missing and remove parentheses
                acquisitionTime = DateTime::ParseExact(acquisitionTimeStr, "ddd, dd MMM yyyy HH:mm:ss zzz", System::Globalization::CultureInfo::InvariantCulture);
            }
            else
            {
                // formatted like "2018-05-16T10:47:15+01:00"
                acquisitionTimeToken = injectionMetadata->SelectToken("$.acquisitionStartDateTime");
                if (acquisitionTimeToken != nullptr)
                {
                    acquisitionTime = safe_cast<DateTime>(acquisitionTimeToken);
                }
            }

            if (acquisitionTime > DateTime::MinValue)
            {
                // these are Boost.DateTime restrictions enforced because one of the test files had a corrupt date
                if (acquisitionTime.Year > 10000)
                    acquisitionTime = acquisitionTime.AddYears(10000 - acquisitionTime.Year);
                else if (acquisitionTime.Year < 1400)
                    acquisitionTime = acquisitionTime.AddYears(1400 - acquisitionTime.Year);

                bpt::ptime pt(boost::gregorian::date(acquisitionTime.Year, boost::gregorian::greg_month(acquisitionTime.Month), acquisitionTime.Day),
                    bpt::time_duration(acquisitionTime.Hour, acquisitionTime.Minute, acquisitionTime.Second, bpt::millisec(acquisitionTime.Millisecond).fractional_seconds()));
                _acquisitionStartTime = blt::local_date_time(pt, blt::time_zone_ptr()); // UTC
            }
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
            /* Channel metadata JSON for a single injection:
             * [
                {
                    "id": "069266697478480da63800cb86043560",
                    "name": "1: MS (72.41-77.41) 5V ESI+",
                    "description": "",
                    "acquisitionMode": "Ms",
                    "scanCount": 53,
                    "channelDataShape": "Spectra",
                    "channelDataType": "MSScanning",
                    "technique": {
                        "instrumentId": null,
                        "instrumentInternalName": null,
                        "targetProperties": {
                            "targetId": "00000000-0000-0000-0000-000000000000",
                            "targetName": "",
                            "targetGroup": ""
                        },
                        "basicMsProperties": {
                            "precursorMz": "NaN",
                            "productMz": -1.0,
                            "massAnalyser": "TRIPLE QUAD",
                            "scanningMethod": "MS",
                            "scanTime": 0.00166666666666667,
                            "interScanDelay": "NaN",
                            "setMasses": [
                                -1.0
                            ],
                            "acquiredMOverZRange": {
                                "start": 72.41,
                                "end": 77.41
                            },
                            "ionisationMode": "Positive",
                            "ionisationType": "ESI",
                            "coneVoltage": "NaN"
                        },
                        "msCalibration": {
                            "resolution": 30000.0
                        },
                        "isLockMassData": false,
                        "fragmentationProperties": {
                            "energyLevelType": "NotSet",
                            "collisionEnergy": 4.0
                        },
                        "tofProperties": null
                    }
                },
                {
                    "id": "1b4c10ab4c604b1898fbdd8d0a074c31",
                    "name": "10: MS (72.41-77.41) 90V ESI+",
                    "description": "",
                    "acquisitionMode": "Ms",
                    "scanCount": 53,
                    "channelDataShape": "Spectra",
                    "channelDataType": "MSScanning",
                    "technique": {
                        "instrumentId": null,
                        "instrumentInternalName": null,
                        "targetProperties": {
                            "targetId": "00000000-0000-0000-0000-000000000000",
                            "targetName": "",
                            "targetGroup": ""
                        },
                        "basicMsProperties": {
                            "precursorMz": "NaN",
                            "productMz": -1.0,
                            "massAnalyser": "TRIPLE QUAD",
                            "scanningMethod": "MS",
                            "scanTime": 0.00166666666666667,
                            "interScanDelay": "NaN",
                            "setMasses": [
                                -1.0
                            ],
                            "acquiredMOverZRange": {
                                "start": 72.41,
                                "end": 77.41
                            },
                            "ionisationMode": "Positive",
                            "ionisationType": "ESI",
                            "coneVoltage": "NaN"
                        },
                        "msCalibration": {
                            "resolution": 30000.0
                        },
                        "isLockMassData": false,
                        "fragmentationProperties": {
                            "energyLevelType": "NotSet",
                            "collisionEnergy": 4.0
                        },
                        "tofProperties": null
                    }
                }
            ]
            */
            auto response = _httpClient->GetAsync(channelMetadataEndpoint())->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + channelMetadataEndpoint());

            json = response->Content->ReadAsStringAsync()->Result;

            _numLogicalSpectra = 0;
            _hasAnyIonMobilityData = false;

            auto channelsJson = JArray::Parse(json);
            for each (auto channelJson in channelsJson)
            {
                // skip non-MS and non-retention-data functions
                auto channelDataShape = channelJson->SelectToken("$.channelDataShape")->ToString();
                if (channelDataShape != "Spectra" && channelDataShape != "Spectrum")
                    continue;

                auto channelDataType = channelJson->SelectToken("$.channelDataType")->ToString();

                _channelInfo.emplace_back();
                ChannelInfo& fi = _channelInfo.back();
                //fi.index = _channelInfo.size() - 1;
                fi.id = ToStdString(channelJson->SelectToken("$.id")->ToString());
                auto name = channelJson->SelectToken("$.name")->ToString();
                fi.name = ToStdString(name);
                auto channelIndexStr = Text::RegularExpressions::Regex::Replace(name, "(\\d+)\\:.*", "$1");
                if (channelIndexStr == name)
                    fi.index = -1;
                else
                    fi.index = Convert::ToInt32(channelIndexStr) - 1; // parse channel number from name field and subtract one to use as the index

                //fi.hasCCSCalibration = (bool)spectrumInfo->SelectToken("$.hasCCSCalibration");
                fi.channelDataShape = ParseEnum<ChannelDataShape>(channelDataShape);
                fi.channelDataType = ParseEnum<ChannelDataType>(channelDataType);
                if (fi.channelDataType == ChannelDataType::IMSScanning ||
                    fi.channelDataType == ChannelDataType::MSScanning)
                {
                    auto scanningMethod = channelJson->SelectToken("$.technique.basicMsProperties.scanningMethod")->ToString();
                    fi.ionizationType = ToStdString(channelJson->SelectToken("$.technique.basicMsProperties.ionisationType")->ToString());
                    fi.scanningMethod = ParseEnum<ScanningMethod>(scanningMethod);
                    fi.lowMass = Convert::ToDouble(channelJson->SelectToken("$.technique.basicMsProperties.acquiredMOverZRange.start")->ToString());
                    fi.highMass = Convert::ToDouble(channelJson->SelectToken("$.technique.basicMsProperties.acquiredMOverZRange.end")->ToString());
                    fi.numSpectra = Convert::ToInt32(channelJson->SelectToken("$.scanCount")->ToString());
                    fi.isIonMobilityData = fi.channelDataType == ChannelDataType::IMSScanning;

                    auto polarity = channelJson->SelectToken("$.technique.basicMsProperties.ionisationMode")->ToString();
                    if (polarity == "Positive")
                        fi.ionizationMode = UNIFI::Polarity::Positive;
                    else if (polarity == "Negative")
                        fi.ionizationMode = UNIFI::Polarity::Negative;
                    else
                        fi.ionizationMode = UNIFI::Polarity::Unknown;

                    auto energyLevel = channelJson->SelectToken("$.technique.fragmentationProperties.energyLevelType")->ToString();;
                    if (energyLevel == "High")
                        fi.energyLevel = UNIFI::EnergyLevel::High;
                    else if (energyLevel == "Low")
                        fi.energyLevel = UNIFI::EnergyLevel::Low;
                    else
                        fi.energyLevel = UNIFI::EnergyLevel::Unknown;
                }
                else
                {
                    fi.scanningMethod = ScanningMethod::Unknown;
                }

                _hasAnyIonMobilityData |= fi.isIonMobilityData;

                //Console::WriteLine("{0}:\n{1}", id, spectrumInfo->ToString());
            }

            hasMSeData = true;

            /*auto energyLevelSortOrder = [](UNIFI::EnergyLevel el)
                {
                    switch (el)
                    {
                    case UNIFI::EnergyLevel::Unknown: return 2;
                    case UNIFI::EnergyLevel::Low: return 0;
                    case UNIFI::EnergyLevel::High: return 1;
                    default: throw gcnew Exception("unsupported energy level");
                    }
                };

            sort(_channelInfo.begin(), _channelInfo.end(), [=](const auto& lhs, const auto& rhs)
                {
                    return energyLevelSortOrder(lhs.energyLevel) < energyLevelSortOrder(rhs.energyLevel);
                });*/

            for (auto& fi : _channelInfo)
            {
                _channelInfoById[fi.id] = &fi;
                //_channelInfoByTitle[fi.name] = &fi;

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

        _chromatogramIds = gcnew List<String^>();
        getTicChromatograms();
        getMrmChromatograms();

        for (const auto& chromatogram : _chromatograms)
        {
            _chromatogramByName[chromatogram.name] = &chromatogram;
        }

        for (auto& ci : _channelInfo)
        {
            if (ci.scanningMethod == ScanningMethod::Unknown)
                continue;

            auto findItr = std::find_if(_chromatograms.begin(), _chromatograms.end(), [&ci](const auto& x) { return ci.id == x.altId; });
            if (findItr == _chromatograms.end())
                throw std::runtime_error("error finding TIC chromatogram for channel " + ci.name);
            ci.ticChromatogram = &*findItr;
        }

        if (!_hasAnyIonMobilityData)
            return;

        /*try
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
                _binToDriftTime.push_back((double)value);

            if (_binToDriftTime.size() != 200)
                throw gcnew Exception("convertbintodrifttime result did not contain 200 values as expected");
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting drift time values: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }*/
    }

    void getTicChromatograms()
    {

        try
        {
            auto response = _httpClient->GetAsync(allChannelsTicEndpoint())->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + allChannelsTicEndpoint());

            auto json = response->Content->ReadAsStringAsync()->Result;

            /* [
                {
                    "title": "1: MS (72.41-77.41) 5V ESI+ (TIC)",
                    "retentionTimes": [
                        0.0001368945,
                        0.982937753
                    ],
                    "intensities": [
                        0.0,
                        1.0
                    ],
                    "id": "06926669-7478-480d-a638-00cb86043561",
                    "parentChannelId": "069266697478480da63800cb86043560",
                    "acquisitionMode": "Ms",
                    "msTechnique": {
                        "msSource": {
                            "ionisationMode": "Positive"
                        },
                        "msCalibration": {
                            "resolution": 30000.0
                        },
                        "isLockMassData": false,
                        "energyLevel": "NotSet",
                        "basicMsProperties": {
                            "precursorMz": "NaN",
                            "productMz": -1.0,
                            "setMassMz": -1.0,
                            "massAnalyser": "TRIPLE QUAD",
                            "scanningMethod": "MS",
                            "scanTime": 0.00166666666666667,
                            "interScanDelay": "NaN",
                            "ionisationType": "ESI",
                            "coneVoltage": "NaN"
                        },
                        "targetProperties": {
                            "targetId": "00000000-0000-0000-0000-000000000000",
                            "targetName": "",
                            "targetGroup": ""
                        },
                        "fragmentationProperties": {
                            "energyLevelType": "NotSet",
                            "collisionEnergy": 4.0
                        },
                        "tofProperties": null
                    }
                },
                and so on...
            ]*/
            auto ticChromatograms = JArray::Parse(json);
            for each (auto ticChromatogram in ticChromatograms)
            {
                auto channelId = ToStdString(ticChromatogram->SelectToken("$.parentChannelId")->ToString());
                auto retentionTimes = safe_cast<JArray^>(ticChromatogram->SelectToken("$.retentionTimes"));
                auto intensities = safe_cast<JArray^>(ticChromatogram->SelectToken("$.intensities"));
                //channelInfo.retentionTimes.reserve(retentionTimes->Count);
                //for each (auto rt in retentionTimes)
                //    channelInfo.retentionTimes.push_back(Convert::ToSingle(rt));

                _chromatograms.emplace_back();
                auto& chromatogram = _chromatograms.back();
                chromatogram.index = _chromatograms.size() - 1;
                chromatogram.type = UNIFI::UnifiChromatogramInfo::TIC;
                auto name = ticChromatogram->SelectToken("$.title")->ToString();
                chromatogram.name = ToStdString(name);
                chromatogram.altId = channelId;
                ToBinaryData(retentionTimes->ToObject<cli::array<double>^>(), chromatogram.timeArray);
                ToBinaryData(intensities->ToObject<cli::array<double>^>(), chromatogram.intensityArray);
                chromatogram.arrayLength = chromatogram.timeArray.size();
                _chromatogramInfo.push_back(chromatogram);

                _chromatogramIds->Add(name);
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
    }

    void getMrmChromatograms()
    {
        try
        {
            auto response = _httpClient->GetAsync(mrmChromatogramsEndpoint())->Result;
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception("response status code does not indicate success (" + response->StatusCode.ToString() + "); URL was: " + mrmChromatogramsEndpoint());

            auto json = response->Content->ReadAsStringAsync()->Result;

            _numLogicalSpectra = 0;
            _hasAnyIonMobilityData = false;

            /*
            [
            {
                "msTechnique": {
                    "instrumentId": null,
                    "instrumentInternalName": null,
                    "targetProperties": {
                        "targetId": "",
                        "targetName": "",
                        "targetGroup": ""
                    },
                    "basicMsProperties": {
                        "precursorMz": 445.12,
                        "productMz": 0.0,
                        "massAnalyser": "QTOF",
                        "scanningMethod": "MRM",
                        "scanTime": 0.0166666666666667,
                        "interScanDelay": 0.00025,
                        "setMasses": null,
                        "ionisationMode": "Positive",
                        "ionisationType": "ESI",
                        "coneVoltage": 8.0
                    },
                    "msCalibration": {
                        "resolution": 20000.0
                    },
                    "isLockMassData": false,
                    "fragmentationProperties": {
                        "energyLevelType": "NotSet",
                        "collisionEnergy": 6.0
                    },
                    "tofProperties": {
                        "timeZero": -39.0,
                        "pusherFrequency": 14440.0,
                        "lteff": 1800.0,
                        "veff": 7198.71,
                        "adcAcquisitionMode": "AdcPeakDetecting",
                        "adcIonResponse": {
                            "massOverCharge": 556.276575,
                            "charge": 1,
                            "averageSingleIonResponse": 20.0
                        }
                    }
                },
                "title": "Enter Name: 1: TOF MRM 445.1200>0 6eV, 4eV ESI+ (TIC)",
                "retentionTimes": [
                    0.0535,
                    0.0704,
                    0.08731667
                ],
                "intensities": [
                    188011.0,
                    185332.0,
                    177679.0
                ],
                "id": "2483806c-606d-47d8-88df-58d163ed691f",
                "parentChannelId": "2483806c606d47d888df58d163ed691e",
                "acquisitionMode": "Mrm"
            },
            */
            auto mrmChannelsJson = JArray::Parse(json);
            for each (auto mrmChannelJson in mrmChannelsJson)
            {
                auto nameStr = mrmChannelJson->SelectToken("$.title")->ToString();
                _chromatogramIds->Add(nameStr);

                _chromatogramInfo.emplace_back();
                auto& info = _chromatogramInfo.back();
                info.index = _chromatogramInfo.size() - 1;
                info.name = ToStdString(nameStr);
                info.Q1 = Convert::ToDouble(mrmChannelJson->SelectToken("$.msTechnique.basicMsProperties.precursorMz")->ToString());
                info.Q3 = Convert::ToDouble(mrmChannelJson->SelectToken("$.msTechnique.basicMsProperties.productMz")->ToString());
                info.type = UNIFI::UnifiChromatogramInfo::MRM;

                auto polarityStr = mrmChannelJson->SelectToken("$.msTechnique.basicMsProperties.ionisationMode")->ToString();
                if (polarityStr == "Positive")
                    info.polarity = UNIFI::Polarity::Positive;
                else if (polarityStr == "Negative")
                    info.polarity = UNIFI::Polarity::Negative;
                else
                    info.polarity = UNIFI::Polarity::Unknown;

                auto retentionTimes = mrmChannelJson->SelectToken("$.retentionTimes");
                auto intensities = mrmChannelJson->SelectToken("$.intensities");

                _chromatograms.emplace_back();
                auto& mrm = _chromatograms.back();
                static_cast<UNIFI::UnifiChromatogramInfo&>(mrm) = info; // copy metadata
                ToBinaryData(retentionTimes->ToObject<cli::array<double>^>(), mrm.timeArray);
                ToBinaryData(intensities->ToObject<cli::array<double>^>(), mrm.intensityArray);
                mrm.arrayLength = mrm.timeArray.size();
            }
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting MRM chromatograms: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }
    }

    void getNumberOfSpectra()
    {
        _numNetworkSpectra = 0;
        for (auto& ci : _channelInfo)
        {
            if (ci.scanningMethod == ScanningMethod::Unknown)
                continue;

            ci.scanIndexOffset = _numNetworkSpectra;
            _numNetworkSpectra += ci.numSpectra;
            if (ci.ticChromatogram->timeArray.size() != ci.numSpectra)
                throw std::runtime_error("number of retention times in TIC chromatogram (" + toString(ci.ticChromatogram->timeArray.size()) +
                    ") does not match number of spectra in channel " + ci.id + " (" + toString(ci.numSpectra) + ")");
        }
        _numLogicalSpectra = _numNetworkSpectra;

        _spectrumIndex.reserve(_numNetworkSpectra);

        for (auto& ci : _channelInfo)
        {
            if (ci.scanningMethod == ScanningMethod::Unknown)
                continue;

            for (size_t i = 0; i < ci.ticChromatogram->timeArray.size(); ++i)
                _spectrumIndex.emplace_back(ci.ticChromatogram->timeArray[i], &ci, static_cast<int>(i));
        }

        // sort index by retention time
        std::sort(_spectrumIndex.begin(), _spectrumIndex.end(), [](const auto& lhs, const auto& rhs) { return lhs.retentionTime < rhs.retentionTime; });

        // build map from channel and scan index to RT-sorted spectrum index
        for (size_t i=0; i < _spectrumIndex.size(); ++i)
        {
            const auto& id = _spectrumIndex[i];
            _spectrumIdByChannelAndScanIndex[make_pair(id.channelInfo->index, id.scanIndexInChannel)] = i;
        }
    }

    void convertWatersToPwizSpectrum(MzSpectrumItemDtoV2^ spectrum, UNIFI::UnifiSpectrum& result, int logicalIndex, bool getBinaryData, const ChannelInfo& channelInfo)
    {
        result.retentionTime = spectrum->RetentionTime;
        result.scanPolarity = UNIFI::Polarity::Unknown;
        result.energyLevel = UNIFI::EnergyLevel::Unknown;

        if (spectrum->MsTechnique != nullptr)
        {
            if (spectrum->MsTechnique->BasicMsProperties != nullptr)
                switch(spectrum->MsTechnique->BasicMsProperties->IonisationMode)
                {
                    case IonisationModeDto::IonisationModeDtoPositive:
                        result.scanPolarity = UNIFI::Polarity::Positive;
                        break;
                    case IonisationModeDto::IonisationModeDtoNegative:
                        result.scanPolarity = UNIFI::Polarity::Negative;
                        break;
                }

            if (spectrum->MsTechnique->FragmentationProperties != nullptr)
                switch(spectrum->MsTechnique->FragmentationProperties->EnergyLevelType)
                {
                    case EnergyLevelDto::EnergyLevelDtoHigh:
                        result.energyLevel = UNIFI::EnergyLevel::High;
                        break;
                    case EnergyLevelDto::EnergyLevelDtoLow:
                        result.energyLevel = UNIFI::EnergyLevel::Low;
                        break;
                    case EnergyLevelDto::EnergyLevelDtoNotSet:
                        result.energyLevel = UNIFI::EnergyLevel::Unknown;
                        break;
                }
        }

        if (result.scanPolarity == UNIFI::Polarity::Unknown)
            result.scanPolarity = channelInfo.ionizationMode;

        if (result.energyLevel == UNIFI::EnergyLevel::Unknown)
            result.energyLevel = channelInfo.energyLevel;

        result.scanRange.first = channelInfo.lowMass;
        result.scanRange.second = channelInfo.highMass;

        result.dataIsContinuous = spectrum->ContinuumSpectrumData != nullptr;

        if (_combineIonMobilitySpectra || !_hasAnyIonMobilityData)
        {
            //if (!_hasAnyIonMobilityData && spectrum->ScanSize->Length > 1)
            //    throw std::runtime_error("non-ion-mobility spectrum with ScanSize.Length > 1");

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
        /*else
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
        }*/
    }

    virtual void getSpectrum(size_t index, UNIFI::UnifiSpectrum& result, bool getBinaryData, bool doCentroid)
    {
        try
        {
            MzSpectrumItemDtoV2^ spectrum = nullptr;
            int networkIndex = networkIndexFromLogicalIndex(index);
            const auto& entry = _spectrumIndex[index];
            const auto& channelInfo = *entry.channelInfo;
            int taskIndex = taskIndexFromSpectrumIndex(channelInfo, networkIndex);
            auto& queue = channelInfo.channelQueue;

            auto cache = doCentroid ? _centroidCache : _profileCache;
            if (cache->TryGet(networkIndex, spectrum))
            {
                convertWatersToPwizSpectrum(spectrum, result, index, getBinaryData, channelInfo);

                // also queue the next _chunkReadahead chunks
                for (int i = 1; i < _chunkReadahead; ++i)
                {
                    if ((taskIndex + _chunkSize * i) > _numNetworkSpectra)
                        break;
                    //Console::WriteLine("Queueing chunk {0} after finding {1} in cache", taskIndex + _chunkSize * i, taskIndex);

                    int lastNetworkIndexOfChunk = taskIndex + ((_chunkSize + 1) * i - 1);
                    if (!cache->Contains(lastNetworkIndexOfChunk)) // if cache contains last index for chunk, don't requeue it
                        queue->getChunkTask(taskIndex + _chunkSize * i, doCentroid, false, false);
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
            auto chunkTask = queue->getChunkTask(taskIndex, doCentroid, true, cache->Count == 0);

            // also queue the next _chunkReadahead chunks
            for (int i = 1; i < _chunkReadahead; ++i)
            {
                if ((taskIndex + _chunkSize * i) > _numNetworkSpectra)
                    break;
                //Console::WriteLine("Queueing chunk {0} after waiting for {1}", taskIndexFromSpectrumIndex(index) + _chunkSize * i, taskIndex);

                int lastNetworkIndexOfChunk = taskIndex + ((_chunkSize + 1) * i - 1);
                if (!cache->Contains(lastNetworkIndexOfChunk)) // if cache contains last index for chunk, don't requeue it
                    queue->getChunkTask(taskIndex + _chunkSize * i, doCentroid, false, false);
            }
            if (_wcDebug)
                Console::Error->WriteLine("WAITING for chunk {0}", taskIndex);
            chunkTask->Wait(); // wait for the task to finish

            spectrum = cache->Get(networkIndex);
            if (spectrum == nullptr)
                throw std::runtime_error("spectrum still null after task finished: " + lexical_cast<string>(index));

            convertWatersToPwizSpectrum(spectrum, result, index, getBinaryData, channelInfo);

        } CATCH_AND_FORWARD_EX(index)
    }

    virtual const std::vector<UNIFI::UnifiChromatogramInfo>& chromatogramInfo()
    {
        return _chromatogramInfo;
    }

    virtual void getChromatogram(size_t index, UNIFI::UnifiChromatogram& chromatogram, bool getBinaryData)
    {
        try
        {
            if (index > _chromatogramInfo.size())
                throw gcnew ArgumentOutOfRangeException("index");

            static_cast<UNIFI::UnifiChromatogramInfo&>(chromatogram) = _chromatogramInfo[index]; // copy metadata
            /*chromatogram.name = _chromatogramInfo[index].name;
            chromatogram.index = _chromatogramInfo[index].index;
            chromatogram.type = _chromatogramInfo[index].type;
            chromatogram.polarity = _chromatogramInfo[index].polarity;*/

            const auto& cachedChromatogram = _chromatograms[index];
            //chromatogram.Q1 = _chromatogramInfo[index].Q1;
            //chromatogram.Q3 = _chromatogramInfo[index].Q3;

            if (getBinaryData)
            {
                chromatogram.timeArray = cachedChromatogram.timeArray;
                chromatogram.intensityArray = cachedChromatogram.intensityArray;
            }
            chromatogram.arrayLength = cachedChromatogram.arrayLength;
        }
        catch (Exception^ e)
        {
            throw std::runtime_error("error getting chromatogram: " + ToStdString(e->ToString()->Split(L'\n')[0]));
        }
        catch (std::exception& e)
        {
            throw e;
        }
    }

};

} // waters_connect
} // vendor_api
} // pwiz

#pragma warning(pop)

#endif