/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.IO;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class ArdiaUrl : RemoteUrl
    {
        public static readonly ArdiaUrl Empty = new ArdiaUrl(UrlPrefix);
        public static string UrlPrefix { get { return RemoteAccountType.ARDIA.Name + @":"; } }
        public string ServerApiUrl => ServerUrl.Replace(@"https://", @"https://api.");

        // The Ardia API expects calls routed through '/session-management/bff' include auth information:
        //  (1) applicationCode header
        //  (2) Bff-Host cookie
        public string NavigationBaseUrl => $@"{ServerApiUrl}/session-management/bff/navigation/api/v1/navigation";
        public string SequenceBaseUrl => $@"{ServerApiUrl}/session-management/bff/standard-sequence/api/v1/";
        public string SynchronizedSequenceBaseUrl => $@"{ServerApiUrl}/session-management/bff/sequence/api/v1/";
        private string RawDataUrl => $@"{ServerApiUrl}/session-management/bff/raw-data/api/v1/rawdata/";

        public ArdiaUrl(string ardiaUrl) : base(ardiaUrl)
        {
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            Id = nameValueParameters.GetValue(@"id");
            SequenceKey = nameValueParameters.GetValue(@"resourceKey");
            StorageId = nameValueParameters.GetValue(@"storageId");
            RawName = nameValueParameters.GetValue(@"rawName");
            RawSize = nameValueParameters.GetLongValue(@"rawSize");
        }

        public string Id { get; private set; }
        public string SequenceKey { get; private set; }
        public string StorageId { get; private set; }
        public string RawName { get; private set; }
        public long? RawSize { get; private set; }

        public ArdiaUrl ChangeId(string id)
        {
            return ChangeProp(ImClone(this), im => im.Id = id);
        }

        public ArdiaUrl ChangeSequenceKey(string key)
        {
            return ChangeProp(ImClone(this), im => im.SequenceKey = key);
        }

        public ArdiaUrl ChangeStorageId(string key)
        {
            return ChangeProp(ImClone(this), im => im.StorageId = key);
        }

        public ArdiaUrl ChangeRawName(string key)
        {
            return ChangeProp(ImClone(this), im => im.RawName = key);
        }

        public ArdiaUrl ChangeRawSize(long? key)
        {
            return ChangeProp(ImClone(this), im => im.RawSize = key);
        }

        public override bool IsWatersLockmassCorrectionCandidate()
        {
            return false;
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.ARDIA; }
        }

        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            result.SetValue(@"id", Id);
            result.SetValue(@"resourceKey", SequenceKey);
            result.SetValue(@"storageId", StorageId);
            result.SetValue(@"rawName", RawName);
            result.SetLongValue(@"rawSize", RawSize);
            return result;
        }

        public string SequenceUrl
        {
            get
            {
                //Assume.IsNotNull(SequenceKey);
                if (SequenceKey.Contains(@"synchronized"))
                    return SynchronizedSequenceBaseUrl + SequenceKey;
                else
                    return SequenceBaseUrl + SequenceKey;
            }
        }

        // Retrieves a pre-signed download URL for raw file from the Ardia platform.
        private string GetPresignedUrl(HttpClient client, string storageId)
        {
            // Encode the storageId to be used in the URL
            var encodedStorageId = WebUtility.UrlEncode(storageId);
            var url = new Uri(RawDataUrl + encodedStorageId);

            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            var responseString = response.Content.ReadAsStringAsync().Result;
            var presignedUrlJson = JObject.Parse(responseString);
            return presignedUrlJson[@"presignedUrl"]!.ToString();
        }

        /// <summary>
        /// A wrapper around MsDataFileImpl that deletes the input file on Dispose().
        /// </summary>
        public class TempMsDataFileImpl : MsDataFileImpl
        {
            public TempMsDataFileImpl(string path, int sampleIndex = 0, LockMassParameters lockmassParameters = null,
                bool simAsSpectra = false, bool srmAsSpectra = false, bool acceptZeroLengthSpectra = true,
                bool requireVendorCentroidedMS1 = false, bool requireVendorCentroidedMS2 = false,
                bool ignoreZeroIntensityPoints = false, int preferOnlyMsLevel = 0,
                bool combineIonMobilitySpectra = true, bool trimNativeId = true) : base(path, sampleIndex,
                lockmassParameters, simAsSpectra, srmAsSpectra, acceptZeroLengthSpectra, requireVendorCentroidedMS1,
                requireVendorCentroidedMS2, ignoreZeroIntensityPoints, preferOnlyMsLevel, combineIonMobilitySpectra,
                trimNativeId)
            {
            }

            public override void Dispose()
            {
                base.Dispose();
                FileEx.SafeDelete(FilePath);
            }
        }

        public class ProgressEventArgs : EventArgs
        {
            private float _progress;

            public ProgressEventArgs(float progress)
            {
                _progress = progress;
            }

            public float Progress => _progress;

        }

        // Approach to reporting progress from reading from a FileStream adapted from:
        //      https://stackoverflow.com/a/57439154/638445
        public override MsDataFileImpl OpenMsDataFile(OpenMsDataFileParams openMsDataFileParams)
        {
            var rawFilepath = Path.Combine(openMsDataFileParams.DownloadPath, RawName);
            if (File.Exists(rawFilepath))
                return openMsDataFileParams.OpenLocalFile(rawFilepath, 0, LockMassParameters);

            var account = FindMatchingAccount() as ArdiaAccount;
            if (account == null)
            {
                throw new RemoteServerException(string.Format(ArdiaResources.UnifiUrl_OpenMsDataFile_Cannot_find_account_for_username__0__and_server__1__, 
                    Username, ServerUrl));
            }

            if (StorageId.IsNullOrEmpty())
                throw new InvalidDataException(ArdiaResources.ArdiaUrl_OpenMsDataFile_cannot_open_an_ArdiaUrl_because_it_is_not_a_RAW_file_URL_with_a_StorageId);

            var p = openMsDataFileParams;
            using var client = account.GetAuthenticatedHttpClient();
            string presignedUrl = GetPresignedUrl(client, StorageId);

            p.ProgressStatus = p.ProgressStatus?.ChangeSegments(p.ProgressStatus.Segment, Math.Max(1, p.ProgressStatus.SegmentCount) + 1);
            p.ProgressMonitor?.UpdateProgress(p.ProgressStatus);

            var response = client.GetAsync(presignedUrl, HttpCompletionOption.ResponseHeadersRead, openMsDataFileParams.CancellationToken).Result;
            response.EnsureSuccessStatusCode();
            var responseStream = response.Content.ReadAsStreamAsync().Result;
            using var progressStream = new ProgressStream(responseStream, RawSize ?? 1);
            if (p.ProgressMonitor != null)
            {
                progressStream.SetProgressMonitor(p.ProgressMonitor, p.ProgressStatus, false);
            }
            using (var fileStream = new FileStream(rawFilepath, FileMode.CreateNew))
            {
                progressStream.CopyToAsync(fileStream, 1 << 16, openMsDataFileParams.CancellationToken).Wait();
            }

            p.ProgressStatus = p.ProgressStatus?.NextSegment();
            p.ProgressMonitor?.UpdateProgress(p.ProgressStatus);

            if (account.DeleteRawAfterImport)
                return new TempMsDataFileImpl(rawFilepath, 0, LockMassParameters, p.SimAsSpectra,
                    requireVendorCentroidedMS1: p.CentroidMs1, requireVendorCentroidedMS2: p.CentroidMs2,
                    ignoreZeroIntensityPoints: p.IgnoreZeroIntensityPoints, preferOnlyMsLevel: p.PreferOnlyMs1 ? 1 : 0);
            else
                return new MsDataFileImpl(rawFilepath, 0, LockMassParameters, p.SimAsSpectra,
                    requireVendorCentroidedMS1: p.CentroidMs1, requireVendorCentroidedMS2: p.CentroidMs2,
                    ignoreZeroIntensityPoints: p.IgnoreZeroIntensityPoints, preferOnlyMsLevel: p.PreferOnlyMs1 ? 1 : 0);
        }
    }

}
