/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.Linq;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// The Skyline versions a document can be shared as for a Panorama server, from the .skyd
    /// version the server reports it supports.
    /// </summary>
    public class PanoramaShareVersions
    {
        public PanoramaShareVersions(IPanoramaClient panoramaClient)
        {
            PanoramaClient = panoramaClient;
        }

        public IPanoramaClient PanoramaClient { get; }

        public ShareType DecideShareTypeVersion(SrmDocument document, ShareType shareType)
        {
            var cacheVersion = GetDocumentCacheVersion(document);

            if (!cacheVersion.HasValue)
            {
                // The document may not have any chromatogram data.
                return shareType;
            }

            var supportedSkylineVersion = GetSupportedVersionForCacheFormat(cacheVersion);
            CacheFormatVersion supportedVersion = supportedSkylineVersion.CacheFormatVersion;
            if (supportedVersion >= cacheVersion.Value)
            {
                return shareType;
            }
            
            return shareType.ChangeSkylineVersion(supportedSkylineVersion);
        }

        public SkylineVersion GetSupportedVersionForCacheFormat(CacheFormatVersion? cacheVersion)
        {
            var supportedSkydVersion = GetSupportedSkydVersion();
            if (cacheVersion == null || supportedSkydVersion >= CacheFormat.GetVersionRequired(cacheVersion.Value))
            {
                // Either the document does not have any chromatograms or the server supports the document's cache version. 
                // Since the cache version does not change when the document is shared, it can be shared as the latest Skyline
                // version even if the cache version associated with that version is higher than what the server supports. 
                // Example scenario:
                // Document cache version is 14; max version supported by server is 14; current Skyline version is associated
                // with cache version 15. In this case the document can be shared as the current Skyline version even though
                // the cache version associated with the current version is higher than what the server supports. When the document
                // is shared the cache format of the document will remain at 14. Only the document format (.sky XML) will change.
                return SkylineVersion.SupportedForSharing().First();
            }
            // The server does not support the document's cache version.
            // Find the highest Skyline version consistent with the cache version supported by the server.
            foreach (var skylineVersion in SkylineVersion.SupportedForSharing())
            {
                if (supportedSkydVersion >= CacheFormat.GetVersionRequired(skylineVersion.CacheFormatVersion))
                {
                    return skylineVersion;
                }
            }
            throw new PanoramaServerException(string.Format(
                Resources.PublishDocumentDlg_ServerSupportsSkydVersion_, (int)cacheVersion.Value));
        }

        public static CacheFormatVersion? GetDocumentCacheVersion(SrmDocument document)
        {
            var settings = document.Settings;
            Assume.IsTrue(document.IsLoaded);
            return settings.HasResults ? settings.MeasuredResults.CacheVersion : null;
        }

        private CacheFormatVersion GetSupportedSkydVersion()
        {
            var serverVersionsJson = PanoramaClient.SupportedVersionsJson();
            if (serverVersionsJson == null)
            {
                // There was an error getting the server-supported skyd version for some reason.
                // Perhaps this is an older server that did not understand the request, or
                // the returned JSON was malformed. Let the document upload continue.
                return CacheFormatVersion.CURRENT;
            }

            JToken serverSkydVersion;
            if (serverVersionsJson.TryGetValue(@"SKYD_version", out serverSkydVersion))
            {
                int version;
                if (int.TryParse(serverSkydVersion.Value<string>(), out version))
                {
                    return (CacheFormatVersion) version;
                }
            }

            return CacheFormatVersion.CURRENT;
        }
    }
}
