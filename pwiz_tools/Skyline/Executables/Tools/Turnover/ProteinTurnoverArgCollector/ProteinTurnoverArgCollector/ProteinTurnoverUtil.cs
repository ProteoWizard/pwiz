namespace ProteinTurnoverArgCollector
{
    class ProteinTurnoverUtil
    {
    }
    //User define ARGUMENT_COUNT for arguments being sent from UI.  Do not include 
    //arguments in TestArgsCollector.properties
    static class Constants
    {
        public const string TRUE_STRING = "1"; // Not L10N             
        public const string FALSE_STRING = "0"; // Not L10N              
        public const int ARGUMENT_COUNT = 7;
    }
    public enum ArgumentIndices
    {
        diet_enrichment,
        average_turnover,
        IDP,
        folder_name,
        reference_group,
        Q_value,
        has_Q_values
    }
}
