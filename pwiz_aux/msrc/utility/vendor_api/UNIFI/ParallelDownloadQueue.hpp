
#pragma once

//#pragma unmanaged
#include <utility>
#include <vector>

//#pragma managed
#using <System.dll>
#using <System.Core.dll>
#using <System.Xml.dll>
#using <System.Net.dll>
#using <System.Web.dll>
#using <System.Net.Http.dll>
#using <System.Net.Http.WebRequest.dll>


namespace pwiz {
namespace util {

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
using System::Threading::Tasks::Task;
using System::Threading::Tasks::TaskScheduler;
using System::Threading::Tasks::Schedulers::QueuedTaskScheduler;
using System::Uri;

ref class ParallelDownloadQueue
{
    /// returns a JSON array or protobuf stream of the spectral intensities and masses (if the HTTP Accept header specifies 'application/octet-stream')
    System::String^ spectrumEndpoint(size_t skip, size_t top) { return _sampleResultUrl + "/spectra/mass.mse?$skip=" + skip + "&$top=" + top; }

    Uri^ _sampleResultUrl;
    System::String^ _accessToken;
    IHttpClientFactory^ _clientFactory;
    int _numSpectra;
    cli::array<double>^ _binToDriftTime; // drift time for each of the 200 bins (0-base indexed) 
    ConcurrentDictionary<int, Task^>^ _tasksByIndexProfile;
    ConcurrentDictionary<int, Task^>^ _tasksByIndexCentroid;
    System::Threading::CancellationTokenSource^ _cancelTokenSource;
    int _chunkSize;
    int _concurrentTasks;
    QueuedTaskScheduler^ _queueScheduler;
    TaskScheduler^ _primaryScheduler;
    TaskScheduler^ _readaheadScheduler;
    System::Threading::EventWaitHandle^ _waitForStart;
    DateTime _startTime;
    bool _unifiDebug;

    //ConcurrentQueue<int>^ _chunkQueue;
    ConcurrentQueue<HttpClient^>^ _httpClients;

    public:

    ref class DownloadInfo
    {
        public:
        /// input: the task/chunk index to download
        int taskIndex;

        /// input: whether to download centroided data or profile
        bool getCentroidData;

        /// output: how many bytes were downloaded
        long bytesDownloaded;

        /// input/output: the size of the chunk to download
        int chunkSize;

        /// input/output: the first index of the chunk
        int spectrumIndexStart;

        /// input/output: the last index (exclusive) of the chunk
        int spectrumIndexEnd;

        /// output: thread used to download the task
        int currentThreadId;
        DateTime lastSpectrumRetrievedTime;

        /// output: the userdata the ParallelDownloadQueue was created with for static callback functions to use to get instance data
        Object^ userdata;

        /// input: the URL to download from as set by the spectrumEndpoint callback
        System::String^ spectrumEndpoint;
    };

    ParallelDownloadQueue(Uri^ url, System::String^ token, IHttpClientFactory^ clientFactory, int numSpectra, const std::vector<double>& binToDriftTime,
        int chunkSize, int concurrentTasks, String^ acceptHeader,
        Action<DownloadInfo^>^ spectrumEndpoint,
        Action<System::IO::Stream^ /*stream*/, DownloadInfo^ /*downloadInfo*/>^ getSpectraFromStream,
        Object^ userdata);

    ~ParallelDownloadQueue();

    // launch maxConcurrentTasks HTTP requests at the same time and see how many fail with 429; return the number that did not fail
    static int GetRequestLimit(System::String^ url, IHttpClientFactory^ clientFactory, String^ accessToken, String^ acceptHeader, int maxConcurrentTasks);

    Task^ getChunkTask(size_t taskIndex, bool doCentroid, bool primary, bool waitForStart);

    private:

    static int getRequestLimitTask(System::Uri^ url, IHttpClientFactory^ clientFactory, System::String^ accessToken, String^ acceptHeader);
    int runChunkTask(size_t taskIndex, bool doCentroid);

    String^ _acceptHeader;
    Action<System::IO::Stream^ /*stream*/, DownloadInfo^>^ _getSpectraFromStream;
    Action<DownloadInfo^>^ _spectrumEndpoint;
    Object^ _userdata;
};

    
} // namespace util
} // namespace pwiz