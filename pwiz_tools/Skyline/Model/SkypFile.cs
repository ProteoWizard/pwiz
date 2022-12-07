using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using JetBrains.Annotations;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Path = System.IO.Path;

namespace pwiz.Skyline.Model
{
    public class SkypFile
    {
        public const string EXT = ".skyp";

        public static string FILTER_SKYP => TextUtil.FileDialogFilter(Resources.SkypFile_FILTER_SKYP_Skyline_Document_Pointer, EXT);

        public string SkypPath { get; private set; }
        public Uri SkylineDocUri { get; private set; }
        public Server ServerMatch { get; private set; }
        public string DownloadingUser { get; private set; }
        public long? Size { get; private set; }
        public string DownloadPath { get; private set; }

        private SkypFile()
        {
        }

        [NotNull]
        public static SkypFile Create([NotNull] string skypPath, IEnumerable<Server> servers)
        {
            var skyp = new SkypFile
            {
                SkypPath = skypPath
            };
            using (var reader = new StreamReader(skyp.SkypPath))
            {
                ReadSkyp(skyp, reader);
            }

            skyp.ServerMatch = servers?.FirstOrDefault(server => server.URI.Host.Equals(skyp.SkylineDocUri.Host));

            var downloadDir = Path.GetDirectoryName(skyp.SkypPath);
            skyp.DownloadPath = GetNonExistentPath(downloadDir ?? string.Empty, skyp.GetSkylineDocName());

            return skyp;
        }

        public static SkypFile CreateForTest(string skypText)
        {
            var skyp = new SkypFile();
            ReadSkyp(skyp, new StringReader(skypText));
            return skyp;
        }

        private static void ReadSkyp(SkypFile skyp, TextReader reader)
        {
            string line;
            var first = true;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    if (first)
                    {
                        // First line in the skyp file is the URL of the skyp.zip file on a Panorama server
                        skyp.SkylineDocUri = GetSkyFileUrl(line);
                        first = false;
                    }
                    else
                    {
                        // The remaining lines should contain colon separated key value pairs, e.g. FileSize:475831
                        // The only thing required is the WebDAV URL of the sky.zip file. FileSize and DownloadingUser
                        // are for better user experience so we will not throw an exception if we are unable to parse
                        // these values. 
                        var parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            if (@"FileSize".Equals(key) && value.Length > 0)
                            {
                                if (long.TryParse(value, out var size) && size > 0)
                                {
                                    skyp.Size = size;
                                }
                            }
                            else if (@"DownloadingUser".Equals(key) && value.Length > 0)
                            {
                                skyp.DownloadingUser = value;
                            }
                        }
                    }
                }
            }
            if (skyp.SkylineDocUri == null)
            {
                throw new InvalidDataException(string.Format(
                    Resources
                        .SkypFile_GetSkyFileUrl_File_does_not_contain_the_URL_of_a_shared_Skyline_archive_file___0___on_a_Panorama_server_,
                    SrmDocumentSharing.EXT_SKY_ZIP));
            }
        }

        private static Uri GetSkyFileUrl(string line)
        {
            var urlString = line;

            var validUrl = Uri.TryCreate(urlString, UriKind.Absolute, out var skyFileUri)
                           && (skyFileUri.Scheme == Uri.UriSchemeHttp || skyFileUri.Scheme == Uri.UriSchemeHttps);
            if (!validUrl)
            {
                throw new InvalidDataException(string.Format(
                    Resources.SkypFile_GetSkyFileUrl__0__is_not_a_valid_URL_on_a_Panorama_server_, urlString));
            }

            if (!urlString.EndsWith(SrmDocumentSharing.EXT))
            {

                // This is not a shared Skyline zip.
                throw new InvalidDataException(string.Format(
                    Resources.SkypFile_GetSkyFileUrl_Expected_the_URL_of_a_shared_Skyline_document_archive_file___0____Found_filename__1__instead_in_the_URL__2__,
                    SrmDocumentSharing.EXT_SKY_ZIP, GetDecodedUriFileName(skyFileUri), urlString));
            }

            return skyFileUri;
        }

        public string GetSkylineDocName()
        {
            // Return the name of the .sky.zip file. URI in the skyp is URL_Encoded. Return the decoded file name. 
            return GetDecodedUriFileName(SkylineDocUri);
        }

        private static string GetDecodedUriFileName(Uri uri)
        {
            return HttpUtility.UrlDecode(Path.GetFileName(uri.AbsolutePath));
        }

        public string GetDocUrlNoName()
        {
            var decodedUri = HttpUtility.UrlDecode(SkylineDocUri.AbsoluteUri);
            var idx = decodedUri.IndexOf(@"@files", StringComparison.Ordinal);
            if (idx != -1)
            {
                decodedUri = decodedUri.Substring(0, idx);
            }

            return decodedUri;
        }

        public static string GetNonExistentPath(string dir, string sharedSkyFile)
        {
            if (string.IsNullOrEmpty(sharedSkyFile))
            {
                throw new ArgumentException(Resources
                    .SkypFile_GetNonExistentPath_Name_of_shared_Skyline_archive_cannot_be_null_or_empty_);
            }

            var count = 1;
            var isSkyZip = PathEx.HasExtension(sharedSkyFile, SrmDocumentSharing.EXT_SKY_ZIP);
            var ext = isSkyZip ? SrmDocumentSharing.EXT_SKY_ZIP : SrmDocumentSharing.EXT;
            var fileNameNoExt = isSkyZip
                ? sharedSkyFile.Substring(0, sharedSkyFile.Length - SrmDocumentSharing.EXT_SKY_ZIP.Length)
                : Path.GetFileNameWithoutExtension(sharedSkyFile);

            var path = Path.Combine(dir, sharedSkyFile);
            while (File.Exists(path) || Directory.Exists(Path.Combine(dir, SrmDocumentSharing.ExtractDir(path))))
            {
                // Add a suffix if a file with the given name already exists OR
                // a directory that would be created when opening this file exists.
                sharedSkyFile = fileNameNoExt + @"(" + count + @")" + ext;
                path = Path.Combine(dir, sharedSkyFile);
                count++;
            }
            return Path.Combine(dir, sharedSkyFile);
        }

        public bool HasCredentials()
        {
            return ServerMatch != null && !string.IsNullOrEmpty(ServerMatch.Username) && !string.IsNullOrEmpty(ServerMatch.Password);
        }
        public bool UsernameMismatch()
        {
            return ServerMatch != null && DownloadingUser != null && !Equals(DownloadingUser, ServerMatch.Username);
        }

        // Return a Server object with the server URI set to the hostname + port number from the sky.zip WebDAV URL,
        // and the username set to the downloading user's email address from the skyp file.
        public Server GetSkylineDocServer()
        {
            return new Server(SkylineDocUri.GetLeftPart(UriPartial.Authority), // get the host name and port number
                DownloadingUser ?? string.Empty, string.Empty);
        }

        public string GetServerName()
        {
            return ServerMatch != null ? ServerMatch.URI.ToString() : GetSkylineDocServer().URI.ToString();
        }

        public bool HasSize()
        {
            return Size.HasValue && Size > 0;
        }
    }
}