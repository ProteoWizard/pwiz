using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public Server Server { get; private set; }
        public string DownloadPath { get; private set; }
        
        public static SkypFile Create(string skypPath, IEnumerable<Server> servers)
        {
            if (string.IsNullOrEmpty(skypPath))
            {
                return null;
            }
            var skyp = new SkypFile
            {
                SkypPath = skypPath,
                SkylineDocUri = GetSkyFileUrl(skypPath),
            };
            skyp.Server = servers?.FirstOrDefault(server => server.URI.Host.Equals(skyp.SkylineDocUri.Host));

            var downloadDir = Path.GetDirectoryName(skyp.SkypPath);
            skyp.DownloadPath = GetNonExistentPath(downloadDir ?? string.Empty, skyp.GetSkylineDocName());

            return skyp;
        }
    
        private static Uri GetSkyFileUrl(string skypPath)
        {
            using (var reader = new StreamReader(skypPath))
            {
                return GetSkyFileUrl(reader);
            }
        }

        public static Uri GetSkyFileUrl(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    var urlString = line;
                    if (!urlString.EndsWith(SrmDocumentSharing.EXT))
                    {
                        // This is not a shared Skyline zip.
                        throw new InvalidDataException(string.Format(
                            Resources
                                .SkypFile_GetSkyFileUrl_Expected_the_URL_of_a_shared_Skyline_document_archive___0___in_the_skyp_file__Found__1__instead_,
                            SrmDocumentSharing.EXT_SKY_ZIP, urlString));
                    }

                    var validUrl = Uri.TryCreate(urlString, UriKind.Absolute, out var skyFileUri)
                                   && (skyFileUri.Scheme == Uri.UriSchemeHttp || skyFileUri.Scheme == Uri.UriSchemeHttps);
                    if (!validUrl)
                    {
                        throw new InvalidDataException(string.Format(
                            Resources.SkypFile_GetSkyFileUrl__0__is_not_a_valid_URL_on_a_Panorama_server_, urlString));
                    }

                    return skyFileUri;
                }
            }

            throw new InvalidDataException(string.Format(
                Resources.SkypFile_GetSkyFileUrl_File_does_not_contain_the_URL_of_a_shared_Skyline_archive_file___0___on_a_Panorama_server_,
                SrmDocumentSharing.EXT_SKY_ZIP));
        }

        public string GetSkylineDocName()
        {
            return SkylineDocUri != null ? Path.GetFileName(SkylineDocUri.AbsolutePath) : null;
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
    }
}