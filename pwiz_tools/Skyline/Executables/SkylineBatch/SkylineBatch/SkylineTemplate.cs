using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using FluentFTP;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class SkylineTemplate
    {

        private string _path;

        public static SkylineTemplate ExistingTemplate(string templateFilePath)
        {
            return new SkylineTemplate(templateFilePath, string.Empty, null);
        }

        public static SkylineTemplate DependentTemplate(string dependentTemplatePath, string dependentConfigName)
        {
            return new SkylineTemplate(dependentTemplatePath, dependentConfigName, null);
        }

        public SkylineTemplate(string filePath, string dependentConfigName, PanoramaFile panoramaFile)
        {
            _path = filePath ?? string.Empty;
            if (PanoramaFile != null) _path = null;
            DependentConfigName = dependentConfigName ?? string.Empty;
            PanoramaFile = panoramaFile;
        }
        
        public readonly PanoramaFile PanoramaFile;
        public readonly string DependentConfigName;

        public string FilePath => PanoramaFile == null ? _path : PanoramaFile.FilePath;

        public bool Downloaded()
        {
            if (PanoramaFile == null) return true;
            return !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);
        }

        public string DisplayPath => !string.IsNullOrEmpty(FilePath) ? FilePath : PanoramaFile?.DownloadFolder;
        
        public bool IsIndependent() => string.IsNullOrEmpty(DependentConfigName);
        public string FileName() => Path.GetFileName(FilePath);

        public void Validate()
        {
            if (PanoramaFile != null)
            {
                PanoramaFile.Validate();
            }
            else if (string.IsNullOrEmpty(DependentConfigName))
            {
                ValidateTemplateFile(FilePath);
            }
        }

        public static void ValidateTemplateFile(string templateFile)
        {

            FileUtil.ValidateNotEmptyPath(templateFile, Resources.MainSettings_ValidateSkylineFile_Skyline_file);
            if (!File.Exists(templateFile))

                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateSkylineFile_The_Skyline_template_file__0__does_not_exist_, templateFile) + Environment.NewLine +
                                            Resources.MainSettings_ValidateSkylineFile_Please_provide_a_valid_file_);
            FileUtil.ValidateNotInDownloads(templateFile, Resources.MainSettings_ValidateSkylineFile_Skyline_file);
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out SkylineTemplate pathReplacedTemplate)
        {
            var preferReplace = Program.FunctionalTest;
            pathReplacedTemplate = this;
            var filePathReplaced = TextUtil.SuccessfulReplace(ValidateTemplateFile, oldRoot, newRoot, FilePath,
                preferReplace, out string replacedFilePath);

            PanoramaFile replacedPanoramaFile = null;
            var panoramaReplaced = false;
            if (PanoramaFile != null)
                panoramaReplaced = PanoramaFile.TryPathReplace(oldRoot, newRoot, out replacedPanoramaFile);
            
            pathReplacedTemplate = new SkylineTemplate(replacedFilePath, DependentConfigName, replacedPanoramaFile);
            return filePathReplaced || panoramaReplaced;
        }

        private enum Attr
        {
            FilePath,
            DependentConfigName,
        };

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("template_file");
            writer.WriteAttributeIfString(Attr.FilePath, FilePath);
            writer.WriteAttributeIfString(Attr.DependentConfigName, DependentConfigName);
            if (PanoramaFile != null) PanoramaFile.WriteXml(writer);
            writer.WriteEndElement();
        }

        public static SkylineTemplate ReadXml(XmlReader reader)
        {
            if (!XmlUtil.ReadNextElement(reader, "template_file"))
                throw new Exception("Mishandled config file from earlier version");
            var filePath = reader.GetAttribute(Attr.FilePath);
            var dependentConfigName = reader.GetAttribute(Attr.DependentConfigName);
            var panoramaFile = PanoramaFile.ReadXml(reader);

            return new SkylineTemplate(filePath, dependentConfigName, panoramaFile);
        }

        protected bool Equals(SkylineTemplate other)
        {
            return Equals(PanoramaFile, other.PanoramaFile)
                   && Equals(DependentConfigName, other.DependentConfigName)
                   && Equals(_path, other._path);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SkylineTemplate)obj);
        }

        public override int GetHashCode()
        {
            return _path.GetHashCode() +
                   DependentConfigName.GetHashCode() +
                   PanoramaFile.GetHashCode();
        }

    }

    public class PanoramaFile : Server
    {
        public static PanoramaFile PanoramaFileFromUI(Server server, string path)
        {
            server = ParseServer(server);
            var fileName = ValidatePanoramaServer(server);
            return new PanoramaFile(server, path, fileName);
        }

        public PanoramaFile(Server server, string downloadFolder, string fileName) : base (server.URI, server.Username, server.Password)
        {
            DownloadFolder = downloadFolder;
            FileName = fileName;
        }
        
        public readonly string DownloadFolder;

        public string FileName;

        public string FilePath =>
            !string.IsNullOrEmpty(DownloadFolder) ? Path.Combine(DownloadFolder, FileName?? string.Empty) : string.Empty;

        public string DownloadUrl => URI.AbsoluteUri;

        public string UserName => Username?? string.Empty;

        public new string Password => base.Password ?? string.Empty;

        // TODO (Ali): Use URI for this - improve
        public static Server ParseServer(Server server)
        {
            var url = server.URI.AbsoluteUri;
            var idRegex = new Regex("id=[0-9]+");
            var splitUrl = url.Split('/').ToList();
            int i = 0;
            var length = splitUrl.Count;
            while (i < length && !splitUrl[i].Equals("panoramaweb.org")) i++;
            if (i == length) throw new ArgumentException("Url must be on Panorama.");
            i++;
            if (!splitUrl[i].Contains("Public") && (string.IsNullOrEmpty(server.Username) || string.IsNullOrEmpty(server.Password)))
                throw new ArgumentException("This is not a Panorama Public URL and requires a username and password.");
            while (i < length && !idRegex.IsMatch(splitUrl[i])) i++;
            if (i == length) throw new ArgumentException("Could not find a Skyline document at the URL. Make sure the URL provided links to a Skyline document and contains an id (ie: id=1000).");


            var queryString = splitUrl[i];
            var viewRegex = new Regex("-[a-z]+\\.view", RegexOptions.IgnoreCase);
            if (!viewRegex.IsMatch(queryString))
                throw new ArgumentException("The Panorama URL is not formatted correctly. Try copying the URL again.");
            queryString = viewRegex.Replace(queryString, "-downloadDocument.view");
            var fileType = new Regex("&.*$");
            queryString = fileType.Replace(queryString, "");
            queryString += "&view=skyp";
            splitUrl[i] = queryString;

            return new Server(splitUrl.Join("/"), server.Username, server.Password);
        }
        
        public PanoramaFile ReplacedFolder(string newFolder)
        {
            return new PanoramaFile(new Server(URI, UserName, Password), newFolder, FileName);
        }

        public void AddDownloadingFile(ServerFilesManager serverFiles)
        {
            serverFiles.AddServer(this);
        }

        public void Download(Update progressHandler, CancellationToken cancelToken)
        {
            var skypFilePath = Path.Combine(DownloadFolder, "DownloadingFileReference" + TextUtil.EXT_SKYP);
            var skypFile = PanoramaFile.DownloadSkyp(skypFilePath, this);
            var skylineFileZip = new SkypSupport().Open(skypFilePath, skypFile.Server, progressHandler, cancelToken);
            var documentExtractor = new SrmDocumentSharing(skylineFileZip);
            var skyFile = documentExtractor.Extract(progressHandler, cancelToken);
            if (cancelToken.IsCancellationRequested) return;
            File.Delete(skypFilePath);
            if (!File.Exists(skyFile))
            {
                progressHandler(-1, new Exception(Resources.PanoramaFile_Download_The_template_file_was_not_downloaded));
                return;
            }
            FileName = Path.GetFileName(skyFile);
        }

        private enum Attr
        {
            DownloadFolder,
            FileName
        };

        public void Validate()
        {
             ValidateDownloadFolder(DownloadFolder);
        }

        public static string ValidatePanoramaServer(Server server)
        {
            try
            {
                var temporarySkypFile = Path.Combine(Path.GetTempPath(), FileUtil.GetSafeName(server.URI.AbsoluteUri));
                var skypFile = DownloadSkyp(temporarySkypFile, server);
                var fileName = skypFile.GetSkylineDocName().Replace(TextUtil.EXT_ZIP, string.Empty);
                File.Delete(temporarySkypFile);
                return fileName;
            }
            catch (Exception e)
            {
                throw new ArgumentException(e.Message);
            }
        }

        public static void ValidateDownloadFolder(string folderPath)
        {
            FileUtil.ValidateNotEmptyPath(folderPath, Resources.PanoramaFile_ValidateDownloadFolder_template_download_folder);
            if (!Directory.Exists(folderPath))
                throw new ArgumentException(Resources.PanoramaFile_ValidateDownloadFolder_The_folder_for_the_Skyline_template_file_does_not_exist__Please_enter_a_valid_folder_);
        }

        public static SkypFile DownloadSkyp(string filePath, Server server)
        {
            var skypDownloader = new WebDownloadClient((percent, error) => { }, new CancellationToken());
            skypDownloader.Download(server.URI, filePath, server.Username, server.Password);
            return SkypFile.Create(filePath, server);
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out PanoramaFile replacedPanoramaFile)
        {
            var replaced = TextUtil.SuccessfulReplace(ValidateDownloadFolder, oldRoot, newRoot, DownloadFolder,
                Program.FunctionalTest, out string replacedFolderPath);
            replacedPanoramaFile = new PanoramaFile(new Server(URI, UserName, Password), replacedFolderPath, FileName);
            return replaced;
        }

        public new void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("panorama_file");
            writer.WriteAttributeIfString(Attr.DownloadFolder, DownloadFolder);
            writer.WriteAttributeIfString(Attr.FileName, FileName);
            base.WriteXml(writer);
            writer.WriteEndElement();
        }

        public new static PanoramaFile ReadXml(XmlReader reader)
        {
            if (!reader.ReadToDescendant("panorama_file")) return null;
            var downloadFolder = reader.GetAttribute(Attr.DownloadFolder);
            var fileName = reader.GetAttribute(Attr.FileName);
            var server = Server.ReadXml(reader);

            return new PanoramaFile(server, downloadFolder, fileName);
        }


        protected bool Equals(PanoramaFile other)
        {
            return base.Equals(other)
                   && Equals(DownloadFolder, other.DownloadFolder);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PanoramaFile)obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() +
                   DownloadFolder.GetHashCode();
        }
    }


}
