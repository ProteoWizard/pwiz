namespace ExampleArgCollector
{
    class ExampleToolUtil
    {
    }
    //User define ARGUMENT_COUNT for arguemnts being sent from UI.  Do not include 
    //arguments in TestArgsCollector.properties
    static class Constants
    {
        public const string TRUE_STRING = "1";             
        public const string FALSE_STRING = "0";             
        public const int ARGUMENT_COUNT = 3;
    }
    public enum ArgumentIndices
    {
        check_box,
        text_box,
        combo_box
    }
}
