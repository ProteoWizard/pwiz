
namespace SkylineBatch
{

    public static class XMLElements
    {
        public const string BATCH_CONFIG = "skylinebatch_config";

        public const string TEMPLATE_FILE = "template_file";
        public const string DATA_FOLDER = "data_folder";
        public const string ANNOTATIONS_FILE = "annotations_file";
        public const string COMMAND_ARGUMENT = "command_argument";
        public const string R_SCRIPT = "r_script";

        public const string REMOTE_FILE = "remote_file";
        public const string REMOTE_FILE_SET = "remote_file_set";
    }




    public enum XML_TAGS
    {
        version,
        name,
        enabled,
        log_test_format,
        modified,
        analysis_folder_path,
        use_analysis_folder_name,
        replicate_naming_pattern,
        path,
        zip_path,
        url,
        username,
        password,
        encrypted_password,
        data_naming_pattern,
        add_decoys,
        shuffle_decoys,
        train_m_prophet,
        remove_decoys,
        remove_results,
        output_file_path,
        value,
        type,
        dependent_configuration,
        ms_one_resolving_power,
        ms_ms_resolving_power,
        retention_time,
        culture_specific,
        use_refined_file,
        relative_path

    }

    public enum OLD_XML_TAGS
    {
        SavedConfigsFilePath, // deprecated since SkylineBatch release 20.2.0.475
        SavedPathRoot,

        Name,
        Enabled,
        Modified,

        TemplateFilePath,
        DependentConfigName,
        AnalysisFolderPath,
        DataFolderPath,
        AnnotationsFilePath,
        ReplicateNamingPattern,

        FilePath,
        ZipFilePath,

        DownloadFolder,
        FileName,

        MsOneResolvingPower,
        MsMsResolvingPower,
        RetentionTime,
        AddDecoys,
        ShuffleDecoys,
        TrainMProphet,

        RemoveDecoys,
        RemoveResults,
        OutputFilePath,

        //Name,
        CultureSpecific,
        Path,
        UseRefineFile,

        Type,
        CmdPath,

        ServerUrl,
        ServerUri,
        ServerUserName,
        ServerPassword,
        ServerFolder,
        DataNamingPattern,
        uri,

        script_path
    }
}
