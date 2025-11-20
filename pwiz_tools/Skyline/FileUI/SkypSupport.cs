/*
 * Original author: vsharma .at. uw.edu
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Net; // HttpStatusCode
using System.Windows.Forms;
using pwiz.PanoramaClient;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public class SkypSupport
    {
        private readonly SkylineWindow _skyline;

        public SkypSupport(SkylineWindow skyline)
        {
            _skyline = skyline;
        }

        public bool Open(string skypPath, IEnumerable<Server> servers, FormEx parentWindow = null)
        {
            Assume.IsFalse(string.IsNullOrEmpty(skypPath));
            
            SkypFile skyp;

            try
            {
                skyp = SkypFile.Create(skypPath!, servers); // Read the skyp file
            }
            catch (Exception e)
            {
                if (ExceptionUtil.IsProgrammingDefect(e))
                    throw;
                
                var message = TextUtil.LineSeparate(FileUIResources.SkypSupport_Open_Failure_opening_skyp_file_, e.Message);
                MessageDlg.ShowWithException(parentWindow ?? _skyline, message, e);
                return false;
            }

            try
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = FileUIResources.SkypSupport_Open_Downloading_Skyline_Document_Archive;
                    var progressStaus = longWaitDlg.PerformWork(parentWindow ?? _skyline, 1000, progressMonitor => Download(skyp, progressMonitor));
                    if (longWaitDlg.IsCanceled)
                        return false;
                }
                
                return _skyline.OpenSharedFile(skyp.DownloadPath);
            }
            catch (Exception e)
            {
                if (ExceptionUtil.IsProgrammingDefect(e))
                    throw;

                var statusCode = NetworkRequestException.GetHttpStatusCode(e);
                var message = GetMessage(skyp, e, statusCode);
                
                if (statusCode is HttpStatusCode.Unauthorized)
                {
                    return skyp.ServerMatch == null
                        ? AddServerAndOpen(skyp, message, parentWindow)  // Server not saved in Skyline. Offer to add the server.
                        : EditServerAndOpen(skyp, message, parentWindow); // Server saved in Skyline but credentials are invalid. Offer to edit server credentials.
                }
                else if (statusCode is HttpStatusCode.Forbidden && skyp.UsernameMismatch())
                {
                    // Server is saved in Skyline but the user in the saved credentials does not have enough permissions. 
                    // The downloading user in the skyp file is different from saved user.  Offer to edit server credentials. 
                    return EditServerAndOpen(skyp, message, parentWindow);
                }

                MessageDlg.ShowWithException(parentWindow ?? _skyline, message, e);
                return false;
            }
        }

        private bool AddServerAndOpen(SkypFile skyp, string message, FormEx parentWindow)
        {
            using (var alertDlg = new AlertDlg(message, MessageBoxButtons.OKCancel))
            {
                if (alertDlg.ShowDialog(parentWindow ?? _skyline) == DialogResult.OK)
                {
                    var allServers = Settings.Default.ServerList;
                    // We did not find a matching server saved in Skyline so create a Server object from the sky.zip WebDAV URL
                    // and the downloading user's username from the skyp file.
                    var skylineDocServer = skyp.GetSkylineDocServer();
                    var newServer = allServers.EditItem(parentWindow, skylineDocServer, allServers, false);
                    if (newServer == null)
                        return false;
                    allServers.Add(newServer); // server should not have the same Uri as an existing server in the list. EditServerDlg takes care of that.

                    return Open(skyp.SkypPath, allServers, parentWindow);
                }

                return false;
            }
        }

        private bool EditServerAndOpen(SkypFile skyp, string message, FormEx parentWindow)
        {
            using (var alertDlg = new AlertDlg(message, MessageBoxButtons.OKCancel))
            {
                alertDlg.ShowDialog(parentWindow ?? _skyline);
                if (alertDlg.DialogResult == DialogResult.OK)
                {
                    var allServers = Settings.Default.ServerList;
                    var skypServerMatch = skyp.ServerMatch;

                    var editedServer = allServers.EditCredentials(parentWindow, skypServerMatch, allServers,
                        skyp.DownloadingUser ?? skypServerMatch.Username, // Use the downloading username from the skyp file, if present
                        string.Empty);
                    
                    if (editedServer == null)
                        return false;

                    var idx = allServers.IndexOf(skypServerMatch);
                    allServers[idx] = editedServer;
                    
                    return Open(skyp.SkypPath, allServers, parentWindow);
                }

                return false;
            }
        }


        #region Error Handling Helpers

        private static string GetMessage(SkypFile skyp, Exception ex, HttpStatusCode? statusCode)
        {
            var message =
                string.Format(
                    FileUIResources.SkypSupport_ShowDownloadError_There_was_an_error_downloading_the_Skyline_document__0__from__1__,
                    skyp.GetSkylineDocName(), skyp.GetDocUrlNoName());

            if (ex != null)
            {
                var exceptionMsg = ex.Message;
                message = TextUtil.LineSeparate(message, exceptionMsg);
            }

            var serverName = skyp.GetServerName();

            if (statusCode is HttpStatusCode.Unauthorized) // 401 -  No credentials provided or invalid credentials
            {
                if (skyp.ServerMatch == null)
                {
                    message = BuildMessage(message,
                        string.Format(
                            Resources
                                .SkypDownloadException_GetMessage_Would_you_like_to_add__0__as_a_Panorama_server_in_Skyline_,
                            serverName));
                }
                else if (skyp.UsernameMismatch())
                {
                    // User that downloaded the skyp file is not the same as the username in the saved server credentials.
                    message = BuildInvalidCredsMessage(message, serverName,
                        string.Format(
                            Resources
                                .SkypDownloadException_GetMessage_The_skyp_file_was_downloaded_by_the_user__0___Credentials_saved_in_Skyline_for_this_server_are_for_the_user__1__,
                            skyp.DownloadingUser, skyp.ServerMatch.Username));
                }
                else
                {
                    // The skyp file either does not have a DownloadingUser, or the username in the skyp is the same as the username in the saved server credentials.
                    message = BuildInvalidCredsMessage(message, serverName);
                }
            }
            else if (statusCode is HttpStatusCode.Forbidden) // 403 - Valid credentials but not enough permissions
            {
                if (skyp.UsernameMismatch() && skyp.ServerMatch != null)
                {
                    message = BuildMessage(true, message,
                        string.Format(
                            Resources.SkypDownloadException_GetMessage_Credentials_saved_in_Skyline_for_the_Panorama_server__0__are_for_the_user__1___This_user_does_not_have_permissions_to_download_the_file__The_skyp_file_was_downloaded_by__2__,
                            serverName, skyp.ServerMatch.Username, skyp.DownloadingUser));
                }
                else
                {
                    message = BuildMessage(message, string.Format(Resources.SkypSupport_Download_You_do_not_have_permissions_to_download_this_file_from__0__,serverName));
                }
            }

            return message;
        }

        private static string BuildMessage(string mainMessage, params string[] otherLines)
        {
            return BuildMessage(false, mainMessage, otherLines);
        }

        private static string BuildInvalidCredsMessage(string mainMessage, string serverName, params string[] otherLines)
        {
            var invalidCredsTxt = string.Format(
                Resources
                    .SkypDownloadException_GetMessage_Credentials_saved_in_Skyline_for_the_Panorama_server__0__are_invalid_,
                serverName);
            var otherMessages = new List<string> { invalidCredsTxt };
            otherMessages.AddRange(otherLines);
            return BuildMessage(true, mainMessage, otherMessages.ToArray());
        }

        private static string BuildMessage(bool withUpdatePrompt, string mainMessage, params string[] otherLines)
        {
            var allMessages = new List<string> { mainMessage };
            allMessages.AddRange(otherLines);
            if (withUpdatePrompt)
            {
                allMessages.Add(string.Empty);
                allMessages.Add(Resources.SkypDownloadException_GetMessage_Would_you_like_to_update_the_credentials_);
            }

            return TextUtil.LineSeparate(allMessages);
        }

        #endregion

        private void Download(SkypFile skyp, IProgressMonitor progressMonitor)
        {
            var progressStatus = new ProgressStatus(string.Format(FileUIResources.SkypSupport_Download_Downloading__0__from__1_, skyp.GetSkylineDocName(), skyp.GetDocUrlNoName()));
            progressMonitor.UpdateProgress(progressStatus);

            using var fileSaver = new FileSaver(skyp.DownloadPath);
            skyp.DownloadTempPath = fileSaver.SafeName;
            
            using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
            
            // Add authorization header if credentials are available
            if (skyp.HasCredentials())
            {
                httpClient.AddAuthorizationHeader(
                    PanoramaServer.GetBasicAuthHeader(skyp.ServerMatch.Username, skyp.ServerMatch.Password));
            }

            // Use the known file size from .skyp file for accurate progress reporting
            // If not available, HttpClientWithProgress will try Content-Length header
            httpClient.DownloadFile(skyp.SkylineDocUri, skyp.SafePath, skyp.Size);
            
            fileSaver.Commit();
        }
    }
}