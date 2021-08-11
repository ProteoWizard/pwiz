using System;
using System.IO;
using System.Threading;
using System.Xml;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class SkylineTemplate
    {

        private string _path;

        private string _zippedPath;

        public static SkylineTemplate ExistingTemplate(string templateFilePath)
        {
            return FromUi(templateFilePath, string.Empty, null);
        }

        public static SkylineTemplate DependentTemplate(string dependentTemplatePath, string dependentConfigName)
        {
            return new SkylineTemplate(dependentTemplatePath, null, dependentConfigName, null);
        }

        public static SkylineTemplate FromUi(string filePath, string dependentConfigName, PanoramaFile panoramaFile)
        {
            string path = null;
            string zippedFilePath = null;

            if (filePath != null && filePath.EndsWith(TextUtil.EXT_ZIP))
                zippedFilePath = filePath;
            else
                path = filePath;
            if (panoramaFile != null)
                zippedFilePath = panoramaFile.FilePath;
            return new SkylineTemplate(path, zippedFilePath, dependentConfigName, panoramaFile);
        }

        public SkylineTemplate(string path, string zipFilePath, string dependentConfigName, PanoramaFile panoramaFile)
        {
            PanoramaFile = panoramaFile;
            _path = !string.IsNullOrEmpty(path) ? path : null;
            _zippedPath = zipFilePath ?? string.Empty;
            DependentConfigName = dependentConfigName ?? string.Empty;
        }
        
        public readonly PanoramaFile PanoramaFile;
        public readonly string DependentConfigName;

        public string FilePath => _path ?? (PanoramaFile == null ? _zippedPath : PanoramaFile.FilePath);

        public bool Downloaded(ServerFilesManager serverFiles)
        {
            if (PanoramaFile == null) return true;
            return serverFiles.GetPanoramaFilesToDownload(PanoramaFile).Count == 0;
        }

        public bool Exists()
        {
            return File.Exists(FilePath);
        }

        public string DisplayPath => !string.IsNullOrEmpty(FilePath) ? FilePath : PanoramaFile?.DownloadFolder;
        
        public bool IsIndependent() => string.IsNullOrEmpty(DependentConfigName);
        public string FileName() => Path.GetFileName(FilePath);

        public bool Zipped => FilePath.EndsWith(TextUtil.EXT_ZIP) || !File.Exists(FilePath);

        public string ZippedFileName => Path.GetFileName(_zippedPath ?? PanoramaFile.FilePath);

        public void Validate()
        {
            if (PanoramaFile != null)
            {
                PanoramaFile.Validate();
            }
            else if (string.IsNullOrEmpty(DependentConfigName))
            {
                Exception validationError;
                try
                {
                    ValidateTemplateFile(FilePath, PanoramaFile != null);
                    return;
                }
                catch (ArgumentException e)
                {
                    if (string.IsNullOrEmpty(_zippedPath)) throw;
                    validationError = e;
                }
                try
                {
                    ValidateTemplateFile(_zippedPath, PanoramaFile != null);
                }
                catch (ArgumentException)
                {
                    throw validationError;
                }
            }
        }

        public static void ValidateTemplateFileDownloading(string templateFile)
        {
            ValidateTemplateFile(templateFile, true);
        }

        public static void ValidateTemplateFileNotDownloading(string templateFile)
        {
            ValidateTemplateFile(templateFile, false);
        }

        public static void ValidateTemplateFile(string templateFile, bool downloading)
        {

            FileUtil.ValidateNotEmptyPath(templateFile, Resources.MainSettings_ValidateSkylineFile_Skyline_file);
            if (!File.Exists(templateFile))
            {
                if (!downloading)
                    throw new ArgumentException(string.Format(Resources.MainSettings_ValidateSkylineFile_The_Skyline_template_file__0__does_not_exist_, templateFile) + Environment.NewLine +
                                                Resources.MainSettings_ValidateSkylineFile_Please_provide_a_valid_file_);
                if (!Directory.Exists(FileUtil.GetDirectorySafe(templateFile)))
                    throw new ArgumentException(string.Format(Resources.SkylineTemplate_ValidateTemplateFile_The_folder_of_the_downloading_template_file__0__does_not_exist_, templateFile) + Environment.NewLine +
                                                Resources.SkylineTemplate_ValidateTemplateFile_Please_provide_a_valid_folder_to_download_the_template_file_into_);
            }
            FileUtil.ValidateNotInDownloads(templateFile, Resources.MainSettings_ValidateSkylineFile_Skyline_file);
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out SkylineTemplate pathReplacedTemplate)
        {
            var preferReplace = Program.FunctionalTest;
            pathReplacedTemplate = this;
            string replacedFilePath = null;
            var templateFileValidator = PanoramaFile == null
                ? ValidateTemplateFileNotDownloading
                : (Validator) ValidateTemplateFileDownloading;
            var pathReplaced = _path != null && TextUtil.SuccessfulReplace(templateFileValidator, oldRoot, newRoot, _path,
                preferReplace, out replacedFilePath);
            var zipPathReplaced = TextUtil.SuccessfulReplace(templateFileValidator, oldRoot, newRoot, _zippedPath,
                preferReplace, out string replacedZippedFilePath);

            PanoramaFile replacedPanoramaFile = null;
            var panoramaReplaced = false;
            if (PanoramaFile != null)
                panoramaReplaced = PanoramaFile.TryPathReplace(oldRoot, newRoot, out replacedPanoramaFile);
            
            pathReplacedTemplate = new SkylineTemplate(replacedFilePath, replacedZippedFilePath, DependentConfigName, replacedPanoramaFile);
            return pathReplaced || zipPathReplaced || panoramaReplaced;
        }

        public SkylineTemplate ForcePathReplace(string oldRoot, string newRoot)
        {
            var path = !string.IsNullOrEmpty(_path) ? FileUtil.ForceReplaceRoot(oldRoot, newRoot, _path) : null;
            var zipPath = !string.IsNullOrEmpty(_zippedPath) ? FileUtil.ForceReplaceRoot(oldRoot, newRoot, _zippedPath) : null;
            var panoramaFile = PanoramaFile?.ForcePathReplace(oldRoot, newRoot);

            return new SkylineTemplate(path, zipPath, DependentConfigName, panoramaFile);
        }

        public void ExtractTemplate(Update progressHandler, CancellationToken cancelToken)
        {
            if (!Zipped) return;
            var documentExtractor = new SrmDocumentSharing(_zippedPath ?? PanoramaFile.FilePath);
            var skyFile = documentExtractor.Extract(progressHandler, cancelToken);
            _path = skyFile;
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XMLElements.TEMPLATE_FILE);
            writer.WriteAttributeIfString(XML_TAGS.path, _path);
            writer.WriteAttributeIfString(XML_TAGS.zip_path, _zippedPath);
            writer.WriteAttributeIfString(XML_TAGS.dependent_configuration, DependentConfigName);
            if (PanoramaFile != null) PanoramaFile.WriteXml(writer);
            writer.WriteEndElement();
        }

        public static SkylineTemplate ReadXml(XmlReader reader)
        {
            var path = reader.GetAttribute(XML_TAGS.path);
            var zippedPath = reader.GetAttribute(XML_TAGS.zip_path);
            var dependentConfigName = reader.GetAttribute(XML_TAGS.dependent_configuration);
            var panoramaFile = PanoramaFile.ReadXml(reader, string.IsNullOrEmpty(path) ? zippedPath : path);

            return new SkylineTemplate(path, zippedPath, dependentConfigName, panoramaFile);
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

        private string _inputUrl;


        public static PanoramaFile PanoramaFileFromUI(Server server, string path, CancellationToken cancelToken)
        {
            ValidateInputs(server.URI.AbsoluteUri, server.Username, server.Password, out _);
            var fileName = ValidatePanoramaServer(server, cancelToken);
            if (cancelToken.IsCancellationRequested) return null;
            return new PanoramaFile(server, path, fileName);
        }

        public PanoramaFile(Server server, string downloadFolder, string fileName) : base (server.URI, server.Username, server.Password, server.Encrypt)
        {
            _inputUrl = server.URI.AbsoluteUri;

            DownloadFolder = downloadFolder;
            FileName = fileName;
            ExpectedSize = -1;
        }
        
        public readonly string DownloadFolder;

        public readonly string FileName;

        public long ExpectedSize;

        public string FilePath =>
            !string.IsNullOrEmpty(DownloadFolder) ? 
                Path.Combine(DownloadFolder, FileName?? string.Empty) : "\\" + FileName;

        public string DownloadUrl => URI.AbsoluteUri;

        public string UserName => Username?? string.Empty;

        public new string Password => base.Password ?? string.Empty;
        

        public PanoramaFile ReplacedFolder(string newFolder)
        {
            return new PanoramaFile(new Server(URI, UserName, Password, Encrypt), newFolder, FileName);
        }

        public void AddDownloadingFile(ServerFilesManager serverFiles)
        {
            serverFiles.AddServer(this);
        }

        public PanoramaFile ReplaceFolder(string newFolder)
        {
            return new PanoramaFile(this, newFolder, FileName);
        }
        

        public void Validate()
        {
             ValidateDownloadFolder(DownloadFolder);
        }

        public static string ValidatePanoramaServer(Server server, CancellationToken cancelToken)
        {
            var serverConnector = new PanoramaServerConnector();
            serverConnector.Add(server);
            string fileName;
            try
            {
                serverConnector.Connect((a, b) => { }, cancelToken);
                if (cancelToken.IsCancellationRequested) return null;
                var fileInfo = serverConnector.GetFile(server, string.Empty, out Exception connectionException);
                if (connectionException != null)
                    throw connectionException;
                fileName = fileInfo.FileName;
            }
            catch (Exception e)
            {
                throw new ArgumentException(e.Message);
            }

            return fileName;
        }

        public static void ValidateDownloadFolder(string folderPath)
        {
            FileUtil.ValidateNotEmptyPath(folderPath, Resources.PanoramaFile_ValidateDownloadFolder_template_download_folder);
            if (!Directory.Exists(folderPath) || !FileUtil.PathHasDriveName(folderPath))
                throw new ArgumentException(Resources.PanoramaFile_ValidateDownloadFolder_The_folder_for_the_Skyline_template_file_does_not_exist__Please_enter_a_valid_folder_);
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out PanoramaFile replacedPanoramaFile)
        {
            var replaced = TextUtil.SuccessfulReplace(ValidateDownloadFolder, oldRoot, newRoot, DownloadFolder,
                Program.FunctionalTest, out string replacedFolderPath);
            replacedPanoramaFile = new PanoramaFile(new Server(URI, UserName, Password, Encrypt), replacedFolderPath, FileName);
            return replaced;
        }

        public PanoramaFile ForcePathReplace(string oldRoot, string newRoot)
        {
            var replacedFolder = FileUtil.ForceReplaceRoot(oldRoot, newRoot, DownloadFolder);
            return ReplaceFolder(replacedFolder);
        }

        public new void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XMLElements.REMOTE_FILE);
            base.WriteXml(writer);
            writer.WriteEndElement();
        }

        public static PanoramaFile ReadXml(XmlReader reader, string filePath)
        {
            if (!reader.ReadToDescendant(XMLElements.REMOTE_FILE))
                return null;
            var downloadFolder = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);
            var server = Server.ReadXml(reader);

            return new PanoramaFile(server, downloadFolder, fileName);
        }

        public new static PanoramaFile ReadXmlVersion_20_2(XmlReader reader)
        {
            if (!reader.ReadToDescendant("panorama_file")) return null;
            var downloadFolder = reader.GetAttribute(XmlUpdater.OLD_XML_TAGS.DownloadFolder);
            var fileName = reader.GetAttribute(XmlUpdater.OLD_XML_TAGS.FileName);
            var server = Server.ReadXmlVersion_20_2(reader);
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
