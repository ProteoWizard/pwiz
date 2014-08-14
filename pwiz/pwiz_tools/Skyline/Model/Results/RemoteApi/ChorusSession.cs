/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public class ChorusSession
    {
        private readonly object _lock = new object();
        private readonly Dictionary<ChorusAccount, ChorusContentsResponse> _chorusContentsByServerUrl 
            = new Dictionary<ChorusAccount, ChorusContentsResponse>();
        private readonly HashSet<ChorusAccount> _fetchRequests 
            = new HashSet<ChorusAccount>();
        private readonly CancellationTokenSource _cancellationTokenSource 
            = new CancellationTokenSource();

        public ChorusContents FetchContents(ChorusAccount chorusAccount, Uri uri)
        {
            HttpWebRequest webRequest = (HttpWebRequest) WebRequest.Create(uri); // Not L10N
            AddAuthHeader(chorusAccount, webRequest);
            return SendRequest(webRequest, webResponse =>
            {
                string strResponse = string.Empty;
                var responseStream = webResponse.GetResponseStream();
                if (responseStream != null)
                {
                    var streamReader = new StreamReader(responseStream);
                    strResponse = streamReader.ReadToEnd();
                }
                var chorusContents = JsonConvert.DeserializeObject<ChorusContents>(strResponse);
                return chorusContents;
            });
        }

        public void Login(ChorusAccount chorusAccount, CookieContainer cookieContainer)
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(chorusAccount.ServerUrl + "/j_spring_security_check"));  // Not L10N
            // ReSharper disable NonLocalizedString
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST"; 
            webRequest.CookieContainer = cookieContainer;
            string postData = "j_username=" + Uri.EscapeDataString(chorusAccount.Username) + "&j_password=" +
                                Uri.EscapeDataString(chorusAccount.Password);
            // ReSharper restore NonLocalizedString
            byte[] postDataBytes = Encoding.UTF8.GetBytes(postData);
            webRequest.ContentLength = postDataBytes.Length;
            var requestStream = webRequest.GetRequestStream();
            requestStream.Write(postDataBytes, 0, postDataBytes.Length);
            requestStream.Close();
            bool loginSuccessful = SendRequest(webRequest, response =>!response.ResponseUri.ToString().Contains("login.html")); // Not L10N
            if (!loginSuccessful)
            {
                throw new ChorusServerException(Resources.ChorusSession_Login_Unable_to_log_in___Username_or_password_is_incorrect_);
            }
        }

        private T SendRequest<T>(HttpWebRequest request, Func<HttpWebResponse, T> responseTransformer)
        {
            using (_cancellationTokenSource.Token.Register(request.Abort))
            {
                using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
                {
                    return responseTransformer(response);
                }
            }
        }

        public void AddAuthHeader(ChorusAccount chorusAccount, HttpWebRequest webRequest)
        {
            if (null != chorusAccount)
            {
                // ReSharper disable NonLocalizedString
                byte[] authBytes = Encoding.UTF8.GetBytes(chorusAccount.Username + ':' + chorusAccount.Password);
                var authHeader = "Basic " + Convert.ToBase64String(authBytes);
                // ReSharper restore NonLocalizedString
                webRequest.Headers.Add(HttpRequestHeader.Authorization, authHeader);
            }
        }

        public ChromatogramCache GenerateChromatograms(ChorusAccount chorusAccount, 
            ChorusUrl chorusUrl, 
            ChromatogramRequestDocument chromatogramRequestDocument)
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(chorusUrl.GetChromExtractionUri());
            AddAuthHeader(chorusAccount, webRequest);
            webRequest.Method = "POST"; // Not L10N
            var xmlSerializer = new XmlSerializer(typeof (ChromatogramRequestDocument));
            xmlSerializer.Serialize(webRequest.GetRequestStream(), chromatogramRequestDocument);
            webRequest.GetRequestStream().Close();
            return SendRequest(webRequest, response =>
            {
                MemoryStream memoryStream = new MemoryStream();
                var responseStream = response.GetResponseStream();
                if (responseStream != null)
                {
                    byte[] buffer = new byte[65536];
                    int count;
                    while ((count = responseStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        memoryStream.Write(buffer, 0, count);
                    }
                }
                if (0 == memoryStream.Length)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new IOException(string.Format("Empty response: status = {0}", response.StatusCode)); // Not L10N
                    }
                    Debug.WriteLine("Zero byte response"); // Not L10N
                    return null;
                }
                ChromatogramCache.RawData rawData;
                ChromatogramCache.LoadStructs(memoryStream, out rawData);
                var chromCacheFile = rawData.ChromCacheFiles[0];
                rawData.ChromCacheFiles = new[]
                {
                    new ChromCachedFile(chorusUrl, chromCacheFile.Flags, chromCacheFile.FileWriteTime,
                        chromCacheFile.RunStartTime, chromCacheFile.MaxRetentionTime, chromCacheFile.MaxIntensity,
                        chromCacheFile.InstrumentInfoList),
                };
                return new ChromatogramCache(string.Empty, rawData,
                    new ChromatogramGeneratorTask.MemoryPooledStream(memoryStream));
            });
        }

        public void Abort()
        {
            _cancellationTokenSource.Cancel();
        }

        public void RetryFetchContents(ChorusAccount chorusAccount, ChorusUrl chorusUrl)
        {
            lock (_lock)
            {
                if (_fetchRequests.Contains(chorusAccount))
                {
                    return;
                }
                _chorusContentsByServerUrl.Remove(chorusAccount);
                ChorusServerException exceptionIgnore;
                AsyncFetchContents(chorusAccount, chorusUrl, out exceptionIgnore);
            }
        }

        public bool AsyncFetchContents(ChorusAccount chorusAccount, ChorusUrl chorusUrl, out ChorusServerException chorusException)
        {
            lock (_lock)
            {
                ChorusContentsResponse chorusContentsResponse;
                if (_chorusContentsByServerUrl.TryGetValue(chorusAccount, out chorusContentsResponse))
                {
                    chorusException = chorusContentsResponse.ChorusException;
                    return chorusContentsResponse.IsComplete;
                }
                chorusException = null;
                if (!_fetchRequests.Add(chorusAccount))
                {
                    return false;
                }
            }
            ActionUtil.RunAsync(() => FetchAndStoreContents(chorusAccount, chorusUrl));
            return false;
        }

        public MsDataSpectrum GetSpectrum(ChorusAccount chorusAccount, ChorusUrl chorusUrl, ChromSource source, double precursor, int scanId)
        {
            string strSource;
            int msLevel = 1;
            // ReSharper disable NonLocalizedString
            switch (source)
            {
                case ChromSource.ms1:
                    strSource = "ms1";
                    break;
                case ChromSource.sim:
                    strSource = "sim";
                    break;
                case ChromSource.fragment:
                    strSource = "ms2";
                    msLevel = 2;
                    break;
                default:
                    throw new ArgumentException("Unknown source " + source);
            }
          
            string strUri = string.Format(CultureInfo.InvariantCulture, 
                "{0}/skyline/api/chroextract/file/{1}/source/{2}/precursor/{3}/{4}",
                chorusUrl.ServerUrl,
                chorusUrl.FileId,
                strSource,
                precursor,
                scanId);
            // ReSharper restore NonLocalizedString

            var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(strUri));
                AddAuthHeader(chorusAccount, webRequest);
            return SendRequest(webRequest, response =>
            {
                string strResponse = string.Empty;
                var responseStream = response.GetResponseStream();
                if (null != responseStream)
                {
                    var streamReader = new StreamReader(responseStream);
                    strResponse = streamReader.ReadToEnd();
                }
                // ReSharper disable NonLocalizedString
                JObject jObject = JsonConvert.DeserializeObject<JObject>(strResponse);
                string strMzs = jObject["mzs-base64"].ToString();
                string strIntensities = jObject["intensities-base64"].ToString();
                byte[] mzBytes = Convert.FromBase64String(strMzs);
                byte[] intensityBytes = Convert.FromBase64String(strIntensities);
                double[] mzs = PrimitiveArrays.FromBytes<double>(
                    PrimitiveArrays.ReverseBytesInBlocks(mzBytes, sizeof (double)));
                float[] intensityFloats = PrimitiveArrays.FromBytes<float>(
                    PrimitiveArrays.ReverseBytesInBlocks(intensityBytes, sizeof (float)));
                double[] intensities = intensityFloats.Select(f => (double) f).ToArray();
                MsDataSpectrum spectrum = new MsDataSpectrum
                {
                    Index = jObject["index"].ToObject<int>(),
                    Level = msLevel,
                    RetentionTime = jObject["rt"].ToObject<double>(),
                    Mzs = mzs,
                    Intensities = intensities,
                };
                // ReSharper restore NonLocalizedString
                return spectrum;
            });
        }

        private void FetchAndStoreContents(ChorusAccount chorusAccount, ChorusUrl chorusUrl)
        {
            ChorusContents contents = new ChorusContents();
            try
            {
                var urisToFetchFrom = new[]
                    {
                        // ReSharper disable NonLocalizedString
                        new Uri(chorusUrl.ServerUrl + "/skyline/api/contents/my"),
                        new Uri(chorusUrl.ServerUrl + "/skyline/api/contents/shared"),
                        new Uri(chorusUrl.ServerUrl + "/skyline/api/contents/public"),
                        // ReSharper restore NonLocalizedString
                    };
                for (int iUri = 0; iUri < urisToFetchFrom.Length; iUri++)
                {
                    contents = contents.Merge(FetchContents(chorusAccount, urisToFetchFrom[iUri]));
                    bool isComplete = iUri == urisToFetchFrom.Length - 1;
                    StoreContentsResponse(chorusAccount, new ChorusContentsResponse(contents) {IsComplete = isComplete});
                }
            }
            catch (Exception exception)
            {
                ChorusServerException chorusException = exception as ChorusServerException;
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (null == chorusException)
                {
                    chorusException = new ChorusServerException(
                        Resources.ChorusSession_FetchContents_There_was_an_error_communicating_with_the_server__
                        + exception.Message, exception);
                }
                StoreContentsResponse(chorusAccount, new ChorusContentsResponse(contents)
                {
                    ChorusException = chorusException,
                    IsComplete = true,
                });
            }
            finally
            {
                lock (_lock)
                {
                    _fetchRequests.Remove(chorusAccount);
                }
            }
        }

        private void StoreContentsResponse(ChorusAccount chorusAccount, ChorusContentsResponse chorusContentsResponse)
        {
            lock (_lock)
            {
                _chorusContentsByServerUrl[chorusAccount] = chorusContentsResponse;
            }
            var contentsAvailableEvent = ContentsAvailable;
            if (null != contentsAvailableEvent)
            {
                contentsAvailableEvent();
            }
        }
        
        public IEnumerable<ChorusItem> ListContents(ChorusAccount chorusAccount, ChorusUrl chorusUrl)
        {
            ChorusContents contents;
            lock (_lock)
            {
                ChorusContentsResponse chorusContentsResponse;
                if (!_chorusContentsByServerUrl.TryGetValue(chorusAccount, out chorusContentsResponse))
                {
                    return new ChorusItem[0];
                }
                contents = chorusContentsResponse.ChorusContents;
            }

            var pathParts = chorusUrl.GetPathParts().ToArray();
            if (pathParts.Length == 0)
            {
                return TOP_LEVEL_ITEMS.Where(item => item.ListItems(contents).Any())
                    .Select(item =>
                        new ChorusItem(chorusUrl.AddPathPart(item.Name), item.Label, DataSourceUtil.FOLDER_TYPE, null, 0));
            }
            TopLevelContents topLevelContents = TOP_LEVEL_ITEMS.FirstOrDefault(item => item.Name == pathParts[0]);
            if (null == topLevelContents)
            {
                return new ChorusItem[0];
            }
            
            if (pathParts.Length == 1)
            {
                return MakeItems(chorusUrl, topLevelContents.ListItems(contents));
            }
            IEnumerable<ChorusContents.IChorusItem> children = topLevelContents.ListItems(contents);
            for (int iPathPart = 1; iPathPart < pathParts.Length; iPathPart ++)
            {
                if (null == children)
                {
                    return new ChorusItem[0];
                }
                var item = children.FirstOrDefault(chorusItem => chorusItem.GetName() == pathParts[iPathPart]);
                if (null == item)
                {
                    return new ChorusItem[0];
                }
                children = item.ListChildren();
            }
            return MakeItems(chorusUrl, children);
        }

        private IEnumerable<ChorusItem> MakeItems(ChorusUrl folderUrl,
            IEnumerable<ChorusContents.IChorusItem> chorusItems)
        {
            if (null == chorusItems)
            {
                return new ChorusItem[0];
            }
            return chorusItems.Select(chorusItem =>
            {
                ChorusContents.File fileItem = chorusItem as ChorusContents.File;
                if (null != fileItem)
                {
                    return new ChorusItem(folderUrl.AddPathPart(chorusItem.GetName()).SetFileId(fileItem.id), chorusItem.GetName(), GetFileType(fileItem), 
                        fileItem.GetDateTime(), 
                        fileItem.fileSizeBytes);
                }
                return new ChorusItem(folderUrl.AddPathPart(chorusItem.GetName()), chorusItem.GetName(), DataSourceUtil.FOLDER_TYPE, chorusItem.GetDateTime(), 0);
            });
        }

        private string GetFileType(ChorusContents.File file)
        {
            return GetFileTypeFromInstrumentModel(file.instrumentModel) ?? DataSourceUtil.UNKNOWN_TYPE;
        }

        private static readonly IList<TopLevelContents> TOP_LEVEL_ITEMS = new[]
        {
            // ReSharper disable NonLocalizedString
            new TopLevelContents("myProjects", Resources.ChorusSession_TOP_LEVEL_ITEMS_My_Projects, chorusContents => chorusContents.myProjects),
            new TopLevelContents("myExperiments", Resources.ChorusSession_TOP_LEVEL_ITEMS_My_Experiments, chorusContents=>chorusContents.myExperiments), 
            new TopLevelContents("myFiles", Resources.ChorusSession_TOP_LEVEL_ITEMS_My_Files, chorusContents=>chorusContents.myFiles), 
            new TopLevelContents("sharedProjects", Resources.ChorusSession_TOP_LEVEL_ITEMS_Shared_Projects, chorusContents => chorusContents.sharedProjects),
            new TopLevelContents("sharedExperiments", Resources.ChorusSession_TOP_LEVEL_ITEMS_Shared_Experiments, chorusContents=>chorusContents.sharedExperiments), 
            new TopLevelContents("sharedFiles", Resources.ChorusSession_TOP_LEVEL_ITEMS_Shared_Files, chorusContents=>chorusContents.sharedFiles), 
            new TopLevelContents("publicProjects", Resources.ChorusSession_TOP_LEVEL_ITEMS_Public_Projects, chorusContents => chorusContents.publicProjects),
            new TopLevelContents("publicExperiments", Resources.ChorusSession_TOP_LEVEL_ITEMS_Public_Experiments, chorusContents=>chorusContents.publicExperiments), 
            new TopLevelContents("publicFiles", Resources.ChorusSession_TOP_LEVEL_ITEMS_Public_Files, chorusContents=>chorusContents.publicFiles), 
            // ReSharper restore NonLocalizedString
        };

        private class TopLevelContents
        {
            private readonly Func<ChorusContents, IEnumerable<ChorusContents.IChorusItem>> _fnListItems;
            public TopLevelContents(string name, string label, Func<ChorusContents, IEnumerable<ChorusContents.IChorusItem>> listItems)
            {
                Name = name;
                Label = label;
                _fnListItems = listItems;
            }

            public string Name { get; private set; }
            public string Label { get; private set; }

            public IEnumerable<ChorusContents.IChorusItem> ListItems(ChorusContents chorusContents)
            {
                return _fnListItems(chorusContents) ?? new ChorusContents.IChorusItem[0];
            }
        }

        private class ChorusContentsResponse
        {
            public ChorusContentsResponse(ChorusContents chorusContents)
            {
                ChorusContents = chorusContents;
            }

            public ChorusContents ChorusContents { get; private set; }
            public ChorusServerException ChorusException { get; set; }
            public bool IsComplete { get; set; }
        }

        public event Action ContentsAvailable;

        public static string GetFileTypeFromInstrumentModel(string instrumentModel)
        {
            var instrumentModelLower = instrumentModel.ToLowerInvariant();
            // ReSharper disable NonLocalizedString
            if (instrumentModelLower.StartsWith("thermo"))
            {
                return DataSourceUtil.TYPE_THERMO_RAW;
            }
            if (instrumentModelLower.StartsWith("waters"))
            {
                return DataSourceUtil.TYPE_WATERS_RAW;
            }
            if (instrumentModelLower.StartsWith("ab sciex"))
            {
                return DataSourceUtil.TYPE_WIFF;
            }
            if (instrumentModelLower.StartsWith("agilent"))
            {
                return DataSourceUtil.TYPE_AGILENT;
            }
            if (instrumentModelLower.StartsWith("shimadzu"))
            {
                return DataSourceUtil.TYPE_SHIMADZU;
            }
            if (instrumentModelLower.StartsWith("bruker"))
            {
                return DataSourceUtil.TYPE_BRUKER;
            }
            // ReSharper restore NonLocalizedString
            return null;
        }
    }

}
