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
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Chorus
{
    public class ChorusSession : RemoteSession
    {
        public ChorusSession(ChorusAccount account) : base(account)
        {
            
        }

        public ChorusAccount ChorusAccount { get { return (ChorusAccount) Account; } }

        public ChorusContents FetchContents(Uri uri)
        {
            HttpWebRequest webRequest = (HttpWebRequest) WebRequest.Create(uri); // Not L10N
            AddAuthHeader(ChorusAccount, webRequest);
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
                throw new RemoteServerException(Resources.ChorusSession_Login_Unable_to_log_in___Username_or_password_is_incorrect_);
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
                ChromatogramCache.LoadStructs(memoryStream, out rawData, false);
                var chromCacheFile = rawData.ChromCacheFiles[0];
                rawData.ChromCacheFiles = new[]
                {
                    chromCacheFile.ChangeFilePath(chorusUrl)
                };
                return new ChromatogramCache(string.Empty, rawData,
                    new ChromatogramGeneratorTask.MemoryPooledStream(memoryStream));
            });
        }

        public override void Dispose()
        {
            _cancellationTokenSource.Cancel();
            base.Dispose();
        }

        public void Abort()
        {
            Dispose();
        }

        public override void RetryFetchContents(RemoteUrl chorusUrl)
        {
            Uri requestUri = GetContentsUri(ChorusAccount, (ChorusUrl) chorusUrl);
            if (null == requestUri)
            {
                return;
            }
            RetryFetch(requestUri, FetchContents);
        }

        public override bool AsyncFetchContents(RemoteUrl remoteUrl, out RemoteServerException remoteException)
        {
            var chorusUrl = (ChorusUrl) remoteUrl;
            Uri requestUri = GetContentsUri(ChorusAccount, chorusUrl);
            return AsyncFetch(requestUri, FetchContents, out remoteException);
        }

        public MsDataSpectrum[] GetSpectra(ChorusUrl chorusUrl, ChromSource source,
            SignedMz precursor, int scanId)
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
                "{0}/skyline/api/chroextract-drift/file/{1}/source/{2}/precursor/{3}/{4}",
                chorusUrl.ServerUrl,
                chorusUrl.FileId,
                strSource,
                precursor.RawValue,  // This will be a negative value for negative ion mode data
                scanId);
            // ReSharper restore NonLocalizedString

            var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(strUri));
            AddAuthHeader(ChorusAccount, webRequest);
            return SendRequest(webRequest, response =>
            {
                string strResponse = string.Empty;
                var responseStream = response.GetResponseStream();
                if (null != responseStream)
                {
                    var streamReader = new StreamReader(responseStream);
                    strResponse = streamReader.ReadToEnd();
                }
                JObject jObject = JsonConvert.DeserializeObject<JObject>(strResponse);
                JArray array = (JArray) jObject["results"]; // Not L10N
                return array.OfType<JObject>().Select(obj => GetSpectrumFromJObject(obj, msLevel)).ToArray();
            });

        }

        private MsDataSpectrum GetSpectrumFromJObject(JObject jObject, int msLevel)
        {
            // ReSharper disable NonLocalizedString
            string strMzs = jObject["mzs-base64"].ToString();
            string strIntensities = jObject["intensities-base64"].ToString();

            byte[] mzBytes = Convert.FromBase64String(strMzs);
            byte[] intensityBytes = Convert.FromBase64String(strIntensities);
            double[] mzs = PrimitiveArrays.FromBytes<double>(
                PrimitiveArrays.ReverseBytesInBlocks(mzBytes, sizeof(double)));
            float[] intensityFloats = PrimitiveArrays.FromBytes<float>(
                PrimitiveArrays.ReverseBytesInBlocks(intensityBytes, sizeof(float)));
            double[] intensities = intensityFloats.Select(f => (double)f).ToArray();
            IonMobilityValue ionMobility = IonMobilityValue.EMPTY;
            JToken jDriftTime;
            if (jObject.TryGetValue("driftTime", out jDriftTime))
            {
                var driftTime = jDriftTime.ToObject<double>();
                if (driftTime != 0)
                {
                    ionMobility = IonMobilityValue.GetIonMobilityValue(driftTime, MsDataFileImpl.eIonMobilityUnits.drift_time_msec);
                }
            }
            MsDataSpectrum spectrum = new MsDataSpectrum
            {
                Index = jObject["index"].ToObject<int>(),
                RetentionTime = jObject["rt"].ToObject<double>(),
                Mzs = mzs,
                Intensities = intensities,
                IonMobility = ionMobility,
            };
            return spectrum;
            // ReSharper restore NonLocalizedString
        }

        private Uri GetContentsUri(ChorusAccount chorusAccount, ChorusUrl chorusUrl)
        {
            if (chorusUrl.FileId.HasValue)
            {
                return null;
            }
            if (chorusUrl.ExperimentId.HasValue)
            {
                return new Uri(chorusUrl.ServerUrl + "/skyline/api/contents/experiments/" + chorusUrl.ExperimentId + "/files"); // Not L10N
            }
            if (chorusUrl.ProjectId.HasValue)
            {
                return new Uri(chorusUrl.ServerUrl + "/skyline/api/contents/projects/" + chorusUrl.ProjectId + "/experiments"); // Not L10N
            }
            if (!chorusUrl.GetPathParts().Any())
            {
                return null;
            }
            string topLevelName = chorusUrl.GetPathParts().First();
            TopLevelContents topLevelContents = TOP_LEVEL_ITEMS.FirstOrDefault(item => item.Name.Equals(topLevelName));
            if (null != topLevelContents)
            {
                return new Uri(chorusUrl.ServerUrl + "/skyline/api/contents" + topLevelContents.ContentsPath); // Not L10N
            }
            return null;
        }
        

        public override IEnumerable<RemoteItem> ListContents(MsDataFileUri parent)
        {
            var chorusUrl = (ChorusUrl) parent;
            if (!chorusUrl.GetPathParts().Any())
            {
                return TOP_LEVEL_ITEMS.Select(
                    item => new RemoteItem(chorusUrl.AddPathPart(item.Name), item.Label, DataSourceUtil.FOLDER_TYPE, null, 0));
            }
            Uri requestUri = GetContentsUri(ChorusAccount, chorusUrl);
            ChorusContents contents;
            if (!TryGetData(requestUri, out contents) || contents == null)
            {
                return new RemoteItem[0];
            }
            return ListItems(chorusUrl, contents);
        }

        private string GetFileType(ChorusContents.File file)
        {
            return GetFileTypeFromInstrumentModel(file.instrumentModel) ?? DataSourceUtil.UNKNOWN_TYPE;
        }

        private static readonly IList<TopLevelContents> TOP_LEVEL_ITEMS = new[]
        {
            // ReSharper disable NonLocalizedString
            new TopLevelContents("myProjects", Resources.ChorusSession_TOP_LEVEL_ITEMS_My_Projects, "/my/projects"),
            new TopLevelContents("myExperiments", Resources.ChorusSession_TOP_LEVEL_ITEMS_My_Experiments, "/my/experiments"), 
            new TopLevelContents("myFiles", Resources.ChorusSession_TOP_LEVEL_ITEMS_My_Files, "/my/files"),
            new TopLevelContents("sharedProjects", Resources.ChorusSession_TOP_LEVEL_ITEMS_Shared_Projects, "/shared/projects"),
            new TopLevelContents("sharedExperiments", Resources.ChorusSession_TOP_LEVEL_ITEMS_Shared_Experiments, "/shared/experiments"), 
            new TopLevelContents("sharedFiles", Resources.ChorusSession_TOP_LEVEL_ITEMS_Shared_Files, "/shared/files"),
            new TopLevelContents("publicProjects", Resources.ChorusSession_TOP_LEVEL_ITEMS_Public_Projects, "/public/projects"),
            new TopLevelContents("publicExperiments", Resources.ChorusSession_TOP_LEVEL_ITEMS_Public_Experiments, "/public/experiments"),
            new TopLevelContents("publicFiles", Resources.ChorusSession_TOP_LEVEL_ITEMS_Public_Files, "/public/files"),
            // ReSharper restore NonLocalizedString
        };

        private class TopLevelContents
        {
            public TopLevelContents(string name, string label, string contentsPath)
            {
                Name = name;
                Label = label;
                ContentsPath = contentsPath;
            }

            public string Name { get; private set; }
            public string Label { get; private set; }
            public string ContentsPath { get; private set; }
        }

        private IEnumerable<RemoteItem> ListItems(ChorusUrl chorusUrl, ChorusContents chorusContents)
        {
            IEnumerable<RemoteItem> items = new RemoteItem[0];
            if (null != chorusContents.experiments)
            {
                items = items.Concat(chorusContents.experiments.Select(experiment =>
                    new RemoteItem(chorusUrl.SetExperimentId(experiment.id).AddPathPart(experiment.GetName()),
                    experiment.GetName(), DataSourceUtil.FOLDER_TYPE, null, 0)));
            }
            if (null != chorusContents.files)
            {
                items = items.Concat(chorusContents.files.Select(
                    file =>
                    {
                        ChorusUrl fileUrl = (ChorusUrl) chorusUrl.SetFileId(file.id)
                            .AddPathPart(file.GetName())
                            .ChangeRunStartTime(file.GetAcquisitionDateTime())
                            .ChangeModifiedTime(file.GetModifiedDateTime());
                        return new RemoteItem(fileUrl, file.GetName(),
                            GetFileType(file), file.GetModifiedDateTime(), file.fileSizeBytes);
                    }
                        ));
            }
            if (null != chorusContents.projects)
            {
                items = items.Concat(chorusContents.projects.Select(project =>
                new RemoteItem(chorusUrl.SetProjectId(project.id).AddPathPart(project.GetName()),
                project.GetName(), DataSourceUtil.FOLDER_TYPE, null, project.GetSize())));
            }
            return items;
        }

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
            if (instrumentModelLower.StartsWith("ab sciex") || instrumentModelLower.StartsWith("sciex"))
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

        public static RemoteServerException WrapWebException(WebException webException)
        {
            string httpErrorMessage = Resources.ChorusSession_WrapWebException_An_error_occurred_communicating_with_the_server___The_server_return_HTTP_error_response_code__0__;
            try
            {
                if (null == webException.Response)
                {
                    return new RemoteServerException(string.Format(httpErrorMessage, webException.Status), webException); // Not L10N
                }
                using (var responseStream = webException.Response.GetResponseStream())
                {
                    if (null == responseStream)
                    {
                        return
                            new RemoteServerException(
                                string.Format(httpErrorMessage, webException.Status), webException);
                    }
                    MemoryStream memoryStream = new MemoryStream();
                    int count;
                    byte[] buffer = new byte[65536];
                    while ((count = responseStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        memoryStream.Write(buffer, 0, count);
                    }
                    String fullMessage = Encoding.UTF8.GetString(memoryStream.ToArray());
                    var xmlSerializer = new XmlSerializer(typeof (ChorusErrorResponse));
                    ChorusErrorResponse chorusErrorResponse;
                    try
                    {
                        chorusErrorResponse =
                            (ChorusErrorResponse) xmlSerializer.Deserialize(new StringReader(fullMessage));
                    }
                    catch (Exception)
                    {
                        // If there is an error in the XML of the response, then put the full text of the response in an inner exception,
                        // and return them an error.
                        var innerException = new RemoteServerException(fullMessage, webException);
                        return new RemoteServerException(string.Format(Resources.ChorusSession_WrapWebException_An_error_occurred_communicating_with_the_server___The_server_returned_the_HTTP_error_response_code__0__but_the_error_message_could_not_be_parsed_, webException.Status), innerException);
                    }

                    if (!string.IsNullOrEmpty(chorusErrorResponse.StackTrace))
                    {
                        Trace.TraceWarning(chorusErrorResponse.StackTrace);
                    }
                    return new RemoteServerException(chorusErrorResponse.Message, webException);
                }
            }
            catch (Exception exception)
            {
                return new RemoteServerException(exception.Message, new AggregateException(webException, exception));
            }
        }
    }
}
