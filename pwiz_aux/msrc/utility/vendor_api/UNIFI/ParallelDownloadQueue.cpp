//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2024 University of Washington
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


#include "ParallelDownloadQueue.hpp"
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"

namespace pwiz {
namespace util {

auto toDouble = [](const auto& i) {return i;};

generic<typename T, typename TResult>
public ref class Bind1
{
    initonly T arg;
    Func<T, TResult>^ const f;
    TResult _() { return f(arg); }

public:
    initonly Func<TResult>^ binder;
    Bind1(Func<T, TResult>^ f, T arg) : f(f), arg(arg) {
        binder = gcnew Func<TResult>(this, &Bind1::_);
    }
};

generic<typename T, typename T2, typename TResult>
public ref class Bind2
{
    initonly T arg1;
    initonly T2 arg2;
    Func<T, T2, TResult>^ const f;
    TResult _() { return f(arg1, arg2); }

public:
    initonly Func<TResult>^ binder;
    Bind2(Func<T, T2, TResult>^ f, T arg1, T2 arg2) : f(f), arg1(arg1), arg2(arg2) {
        binder = gcnew Func<TResult>(this, &Bind2::_);
    }
};

generic<typename T, typename T2, typename T3, typename TResult>
public ref class Bind3
{
    initonly T arg1;
    initonly T2 arg2;
    initonly T3 arg3;
    Func<T, T2, T3, TResult>^ const f;
    TResult _() { return f(arg1, arg2, arg3); }

public:
    initonly Func<TResult>^ binder;
    Bind3(Func<T, T2, T3, TResult>^ f, T arg1, T2 arg2, T3 arg3) : f(f), arg1(arg1), arg2(arg2) ,arg3(arg3) {
        binder = gcnew Func<TResult>(this, &Bind3::_);
    }
};

generic<typename T, typename T2, typename T3, typename T4, typename TResult>
public ref class Bind4
{
    initonly T arg1;
    initonly T2 arg2;
    initonly T3 arg3;
    initonly T4 arg4;
    Func<T, T2, T3, T4, TResult>^ const f;
    TResult _() { return f(arg1, arg2, arg3, arg4); }

public:
    initonly Func<TResult>^ binder;
    Bind4(Func<T, T2, T3, T4, TResult>^ f, T arg1, T2 arg2, T3 arg3, T4 arg4) : f(f), arg1(arg1), arg2(arg2), arg3(arg3), arg4(arg4) {
        binder = gcnew Func<TResult>(this, &Bind4::_);
    }
};

public ref class Binder abstract sealed // static
{
public:
    generic<typename T, typename TResult>
    static Func<TResult>^ Create(Func<T, TResult>^ f, T arg) {
        return (gcnew Bind1<T, TResult>(f, arg))->binder;
    }

    generic<typename T, typename T2, typename TResult>
    static Func<TResult>^ Create(Func<T, T2, TResult>^ f, T arg1, T2 arg2) {
        return (gcnew Bind2<T, T2, TResult>(f, arg1, arg2))->binder;
    }

    generic<typename T, typename T2, typename T3, typename TResult>
    static Func<TResult>^ Create(Func<T, T2, T3, TResult>^ f, T arg1, T2 arg2, T3 arg3) {
        return (gcnew Bind3<T, T2, T3, TResult>(f, arg1, arg2, arg3))->binder;
    }

    generic<typename T, typename T2, typename T3, typename T4, typename TResult>
    static Func<TResult>^ Create(Func<T, T2, T3, T4, TResult>^ f, T arg1, T2 arg2, T3 arg3, T4 arg4) {
        return (gcnew Bind4<T, T2, T3, T4, TResult>(f, arg1, arg2, arg3, arg4))->binder;
    }
};

ParallelDownloadQueue::ParallelDownloadQueue(Uri^ url, System::String^ token, IHttpClientFactory^ clientFactory, int numSpectra, const std::vector<double>& binToDriftTime,
    int chunkSize, int concurrentTasks, String^ acceptHeader,
    Action<DownloadInfo^>^ spectrumEndpoint,
    Action<System::IO::Stream^ /*stream*/, DownloadInfo^ /*downloadInfo*/>^ getSpectraFromStream,
    Object^ userdata)
    : _chunkSize(chunkSize), _concurrentTasks(concurrentTasks)
{
    _sampleResultUrl = url;
    _accessToken = token;
    _clientFactory = clientFactory;
    _numSpectra = numSpectra;
    _binToDriftTime = ToSystemArray<double>(binToDriftTime, toDouble);
    _tasksByIndexProfile = gcnew ConcurrentDictionary<int, Task^>();
    _tasksByIndexCentroid = gcnew ConcurrentDictionary<int, Task^>();
    _httpClients = gcnew System::Collections::Concurrent::ConcurrentQueue<HttpClient^>();
    _cancelTokenSource = gcnew System::Threading::CancellationTokenSource();
    _acceptHeader = acceptHeader;
    _spectrumEndpoint = spectrumEndpoint;
    _getSpectraFromStream = getSpectraFromStream;
    _userdata = userdata;
    for (int i = 0; i < concurrentTasks+2; ++i)
    {
        auto httpClient = _clientFactory->CreateClient("customClient");
        httpClient->BaseAddress = gcnew Uri(_sampleResultUrl->GetLeftPart(System::UriPartial::Authority));
        httpClient->DefaultRequestHeaders->Authorization = gcnew AuthenticationHeaderValue("Bearer", _accessToken);
        httpClient->DefaultRequestHeaders->Accept->Add(gcnew MediaTypeWithQualityHeaderValue(_acceptHeader));
        httpClient->Timeout = System::TimeSpan::FromSeconds(60);

        _httpClients->Enqueue(httpClient);
    }

    auto unifiDebug = System::Environment::GetEnvironmentVariable("UNIFI_DEBUG");
    _unifiDebug = unifiDebug != nullptr && unifiDebug != "0";

    if (_unifiDebug)
        Console::Error->WriteLine("Chunk size: {0}, Num. spectra: {1}", chunkSize, numSpectra);

    _queueScheduler = gcnew QueuedTaskScheduler();
    _primaryScheduler = _queueScheduler->ActivateNewQueue(0);
    _readaheadScheduler = _queueScheduler->ActivateNewQueue(1);

    _waitForStart = gcnew System::Threading::EventWaitHandle(false, System::Threading::EventResetMode::AutoReset);
    _startTime = DateTime::UtcNow;

    //_chunkQueue = gcnew ConcurrentQueue<int>();
    //for (int i = 0; i < _numSpectra; i += chunkSize)
    //    _chunkQueue->Enqueue(i);
}

ParallelDownloadQueue::~ParallelDownloadQueue()
{
    Console::Error->WriteLine("Disposing queue and cancelling requests.");
    _cancelTokenSource->Cancel();
    //for each (Task^ task in _tasksByIndex->Values)
    //    task->Wait();
}

int ParallelDownloadQueue::getRequestLimitTask(System::Uri^ url, IHttpClientFactory^ clientFactory, String^ accessToken, String^ acceptHeader)
{
    auto httpClient = clientFactory->CreateClient("customClient");
    httpClient->BaseAddress = gcnew Uri(url->GetLeftPart(System::UriPartial::Authority));
    httpClient->DefaultRequestHeaders->Authorization = gcnew AuthenticationHeaderValue("Bearer", accessToken);
    httpClient->DefaultRequestHeaders->Accept->Add(gcnew MediaTypeWithQualityHeaderValue(acceptHeader));
    httpClient->Timeout = System::TimeSpan::FromSeconds(60);

    System::Net::Http::HttpRequestMessage^ request;
    System::Net::Http::HttpResponseMessage^ response;
    for (int streamRetryCount = 1, streamMaxRetryCount = 15; streamRetryCount <= streamMaxRetryCount; ++streamRetryCount)
    {
        try
        {
            for (int requestRetryCount = 1, requestMaxRetryCount = 15; requestRetryCount <= requestMaxRetryCount; ++requestRetryCount)
            {
                try
                {
                    request = gcnew System::Net::Http::HttpRequestMessage(System::Net::Http::HttpMethod::Get, url);
                    response = httpClient->SendAsync(request, System::Net::Http::HttpCompletionOption::ResponseContentRead)->Result;
                    if (response->IsSuccessStatusCode)
                        break;
                }
                catch (Exception^ e)
                {
                    if (requestRetryCount < requestMaxRetryCount)
                    {
                        // try again
                        System::Threading::Thread::Sleep(2000 * Math::Pow(2, requestRetryCount));
                    }
                    else
                        throw gcnew Exception(System::String::Format("error requesting spectra chunk {0} ({2})", "0", e->ToString()->Replace("\r", "")->Split(L'\n')[0]));
                }
            }
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception(System::String::Format("error requesting spectra chunk {0} (HTTP {1})", "0", response->StatusCode));
        }
        catch (Exception^ e)
        {
            // if TooManyRequests, deactivate current httpClient and retry the task with the next available httpClient
            if (response->StatusCode == (HttpStatusCode) 429)
            {
                return 1;
            }

            if (streamRetryCount >= streamMaxRetryCount)
                throw gcnew Exception(System::String::Format("error deserializing spectra chunk {0} protobuf: {1}", "0", e->ToString()->Replace("\r", "")->Split(L'\n')[0]), e);
        }
        finally
        {
            delete request;

            if (response != nullptr)
                delete response;
        }
    }
    return 0;
}

// launch maxConcurrentTasks HTTP requests at the same time and see how many fail with 429; return the number that did not fail
int ParallelDownloadQueue::GetRequestLimit(System::String^ url, IHttpClientFactory^ clientFactory, String^ accessToken, String^ acceptHeader, int maxConcurrentTasks)
{
    DateTime start = DateTime::UtcNow;
    auto tasks = gcnew System::Collections::Generic::List<System::Threading::Tasks::Task<int>^>();
    auto uri = gcnew Uri(url);
    for (int i=0; i < maxConcurrentTasks; ++i)
    {
        auto f = gcnew Func<Uri^, IHttpClientFactory^, String^, String^, int>(&ParallelDownloadQueue::getRequestLimitTask);
        tasks->Add(Task::Factory->StartNew(Binder::Create(f, uri, clientFactory, accessToken, acceptHeader)));
    }

    for each (auto task in tasks)
    {
        if (task->Result)
            --maxConcurrentTasks;
    }
    return Math::Max(1, maxConcurrentTasks);
}

Task^ ParallelDownloadQueue::getChunkTask(size_t taskIndex, bool doCentroid, bool primary, bool waitForStart)
{
    // if the taskIndex is greater than the number of spectra, do nothing
    //if (taskIndex >= _numSpectra)
    //    return nullptr;

    auto tasksByIndex = doCentroid ? _tasksByIndexCentroid : _tasksByIndexProfile;

    Task^ chunkTask;
    if (!tasksByIndex->TryGetValue(taskIndex, chunkTask))
    {
        auto f = gcnew Func<size_t, bool, int>(this, &ParallelDownloadQueue::runChunkTask);
        chunkTask = Task::Factory->StartNew(Binder::Create(f, taskIndex, doCentroid), System::Threading::CancellationToken::None, System::Threading::Tasks::TaskCreationOptions::PreferFairness, primary ? _primaryScheduler : _readaheadScheduler);
        if (waitForStart)
            _waitForStart->WaitOne();
        tasksByIndex->GetOrAdd(taskIndex, chunkTask);
    }
    return chunkTask;
}

int ParallelDownloadQueue::runChunkTask(size_t chunkIndex, bool doCentroid)
{
    int currentThreadId = System::Threading::Thread::CurrentThread->ManagedThreadId;
    if (_unifiDebug)
        Console::Error->WriteLine((DateTime::UtcNow - _startTime).ToString("\\[h\\:mm\\:ss\\]\\ ") + "Requesting chunk {0} on thread {1}", chunkIndex, currentThreadId);

    _waitForStart->Set();

    HttpClient^ httpClient = nullptr;
    while (!_httpClients->TryDequeue(httpClient)) {}
    /*if (!_httpClientByThread->ContainsKey(currentThreadId))
    {
        auto webRequestHandler = gcnew System::Net::Http::WebRequestHandler();
        webRequestHandler->UnsafeAuthenticatedConnectionSharing = true;
        webRequestHandler->PreAuthenticate = true;

        httpClient = gcnew HttpClient(webRequestHandler);
        httpClient->BaseAddress = gcnew Uri(_sampleResultUrl->GetLeftPart(System::UriPartial::Authority));
        httpClient->DefaultRequestHeaders->Authorization = gcnew AuthenticationHeaderValue("Bearer", _accessToken);
        httpClient->DefaultRequestHeaders->Accept->Add(gcnew MediaTypeWithQualityHeaderValue("application/octet-stream"));
        httpClient->Timeout = System::TimeSpan::FromSeconds(60);

        _httpClientByThread[currentThreadId] = httpClient;
        Console::WriteLine("CREATING NEW HTTPCLIENT FOR THREAD {0}: {1} clients total", currentThreadId, _httpClientByThread->Count);
    }
    else
        httpClient = _httpClientByThread[currentThreadId];*/

    DateTime start = DateTime::UtcNow;
    System::Net::Http::HttpRequestMessage^ request;
    System::Net::Http::HttpResponseMessage^ response;
    auto downloadInfo = gcnew DownloadInfo();
    downloadInfo->bytesDownloaded = 0;
    downloadInfo->taskIndex = chunkIndex;
    downloadInfo->getCentroidData = doCentroid;
    downloadInfo->chunkSize = _chunkSize;
    downloadInfo->currentThreadId = currentThreadId;
    downloadInfo->userdata = _userdata;
    downloadInfo->spectrumIndexStart = 0;
    int spectraLeft = _numSpectra - (int)chunkIndex;
    downloadInfo->spectrumIndexEnd = _chunkSize < spectraLeft ? _chunkSize : spectraLeft;
    _spectrumEndpoint(downloadInfo);
    for (int streamRetryCount = 1, streamMaxRetryCount = 15; streamRetryCount <= streamMaxRetryCount; ++streamRetryCount)
    {
        try
        {
            auto requestStart = start;
            for (int requestRetryCount = 1, requestMaxRetryCount = 15; requestRetryCount <= requestMaxRetryCount; ++requestRetryCount)
            {
                try
                {
                    requestStart = DateTime::UtcNow;
                    request = gcnew System::Net::Http::HttpRequestMessage(System::Net::Http::HttpMethod::Get, downloadInfo->spectrumEndpoint);
                    response = httpClient->SendAsync(request, System::Net::Http::HttpCompletionOption::ResponseContentRead, _cancelTokenSource->Token)->Result;
                    if (response->IsSuccessStatusCode)
                        break;
                }
                catch (Exception^ e)
                {
                    if (requestRetryCount < requestMaxRetryCount)
                    {
                        // try again
                        if (_unifiDebug)
                            Console::Error->WriteLine((DateTime::UtcNow - _startTime).ToString("\\[h\\:mm\\:ss\\]\\ ") + System::String::Format("Retrying spectra chunk request {0} on thread {1} (attempt #{3}) due to error ({2})", chunkIndex, currentThreadId, e->ToString()->Replace("\r", "")->Split(L'\n')[0], requestRetryCount));
                        System::Threading::Thread::Sleep(2000 * Math::Pow(2, requestRetryCount));
                    }
                    else
                        throw gcnew Exception(System::String::Format("error requesting spectra chunk {0} on thread {1} ({2})", chunkIndex, currentThreadId, e->ToString()->Replace("\r", "")->Split(L'\n')[0]));
                }
            }
            if (!response->IsSuccessStatusCode)
                throw gcnew Exception(System::String::Format("error requesting spectra chunk {0} (HTTP {1})", chunkIndex, response->StatusCode));

            DateTime stop = DateTime::UtcNow;

            downloadInfo->bytesDownloaded = response->Content->Headers->ContentLength.GetValueOrDefault(0);
            //if (streamRetryCount == 1)
            if (_unifiDebug)
                Console::Error->WriteLine((DateTime::UtcNow - _startTime).ToString("\\[h\\:mm\\:ss\\]\\ ") + "Starting chunk {0} ({1}ms to send request and receive {2} bytes; {3:0.}KB/s)",
                    chunkIndex, (stop - requestStart).TotalMilliseconds, downloadInfo->bytesDownloaded, downloadInfo->bytesDownloaded / 1024 / (stop - requestStart).TotalSeconds);

            start = DateTime::UtcNow;
            downloadInfo->lastSpectrumRetrievedTime = start;
            auto stream = response->Content->ReadAsStreamAsync()->Result;
            _getSpectraFromStream(stream, downloadInfo);
            break; // successfully streamed chunk
        }
        catch (Exception^ e)
        {
            // if TooManyRequests, deactivate current httpClient and retry the task with the next available httpClient
            if (response->StatusCode == (HttpStatusCode)429)
            {
                Console::Error->WriteLine((DateTime::UtcNow - _startTime).ToString("\\[h\\:mm\\:ss\\]\\ ") + System::String::Format("Deactivating HTTP client due to TooManyRequests response"));
                delete httpClient;
                while (!_httpClients->TryDequeue(httpClient)) {}
            }

            if (streamRetryCount < streamMaxRetryCount || response->StatusCode == (HttpStatusCode)429)
            {
                // try again
                if (_unifiDebug)
                    Console::Error->WriteLine((DateTime::UtcNow - _startTime).ToString("\\[h\\:mm\\:ss\\]\\ ") + System::String::Format("Retrying spectra chunk download {0} on thread {1} (attempt #{3}) due to error ({2})", chunkIndex, currentThreadId, e->ToString()->Replace("\r", "")->Split(L'\n')[0], streamRetryCount));

                if (response->StatusCode != (HttpStatusCode)429)
                    System::Threading::Thread::Sleep(2000 * Math::Pow(2, streamRetryCount));
                downloadInfo->bytesDownloaded = 0;
            }
            else
                throw gcnew Exception(System::String::Format("error deserializing spectra chunk {0} protobuf: {1}", chunkIndex, e->ToString()->Replace("\r", "")->Split(L'\n')[0]), e);
        }
        finally
        {
            delete request;

            if (response != nullptr)
                delete response;
        }
    }

    if (_unifiDebug)
    {
        DateTime stop = DateTime::UtcNow;
        Console::Error->WriteLine((DateTime::UtcNow - _startTime).ToString("\\[h\\:mm\\:ss\\]\\ ") + "FINISHED chunk {0} on thread {1} ({2} bytes in {3}s)", chunkIndex, currentThreadId, downloadInfo->bytesDownloaded, (stop - start).TotalSeconds);
    }
    auto tasksByIndex = doCentroid ? _tasksByIndexCentroid : _tasksByIndexProfile;
    Task^ task;
    tasksByIndex->TryRemove(chunkIndex, task); // remove the task
    _httpClients->Enqueue(httpClient); // add client back to queue

    return 0;
}

} // namespace util
} // namespace pwiz