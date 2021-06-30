
namespace SkylineBatch
{

    public static class XMLElements
    {
        public const string BATCH_CONFIG = "skylinebatch_config";

        public const string MAIN_SETTINGS = "file_settings";
        public const string TEMPLATE_FILE = "template_file";
        public const string DATA_FOLDER = "data_folder";
        public const string ANNOTATIONS_FILE = "annotations_file";
        public const string FILE_SETTINGS = "import_settings";
        public const string REFINE_SETTINGS = "refine_settings";
        public const string COMMAND_ARGUMENT = "command_argument";
        public const string REPORT_SETTINGS = "report_settings";
        public const string REPORT_INFO = "report_info";
        public const string R_SCRIPT = "r_script";
        public const string SKYLINE_SETTINGS = "configuration_skyline_settings";

        public const string REMOTE_FILE = "remote_file";
        public const string REMOTE_FILE_SET = "remote_file_set";
    }




    public enum XML_TAGS
    {
        version,
        name,
        enabled,
        modified,
        analysis_folder_path,
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
        use_refined_file

    }
}
