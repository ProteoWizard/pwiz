
// ReSharper disable InconsistentNaming
namespace SharedBatch
{
    public class PanoramaJsonObject
    {
        public string schemaName { get; set; }
        public string queryName { get; set; }
        public float formatVersion { get; set; }
        public Metadata metaData { get; set; }
        public Columnmodel[] columnModel { get; set; }
        public Row[] rows { get; set; }
        public int rowCount { get; set; }
    }

    public class Metadata
    {
        public Importtemplate[] importTemplates { get; set; }
        public string root { get; set; }
        public string totalProperty { get; set; }
        public string description { get; set; }
        public string id { get; set; }
        public Field[] fields { get; set; }
        public string title { get; set; }
        public object importMessage { get; set; }
    }

    public class Importtemplate
    {
        public string label { get; set; }
        public string url { get; set; }
    }

    public class Field
    {
        public Ext ext { get; set; }
        public string name { get; set; }
        public string align { get; set; }
        public string friendlyType { get; set; }
        public string type { get; set; }
        public string jsonType { get; set; }
        public string sqlType { get; set; }
        public object defaultValue { get; set; }
        public string fieldKey { get; set; }
        public string[] fieldKeyArray { get; set; }
        public string fieldKeyPath { get; set; }
        public bool isAutoIncrement { get; set; }
        public bool autoIncrement { get; set; }
        public bool isHidden { get; set; }
        public bool hidden { get; set; }
        public bool isKeyField { get; set; }
        public bool keyField { get; set; }
        public bool isMvEnabled { get; set; }
        public bool mvEnabled { get; set; }
        public bool isNullable { get; set; }
        public bool nullable { get; set; }
        public bool required { get; set; }
        public bool isReadOnly { get; set; }
        public bool readOnly { get; set; }
        public bool isUserEditable { get; set; }
        public bool userEditable { get; set; }
        public bool calculated { get; set; }
        public bool isVersionField { get; set; }
        public bool versionField { get; set; }
        public bool isSelectable { get; set; }
        public bool selectable { get; set; }
        public bool shownInInsertView { get; set; }
        public bool shownInUpdateView { get; set; }
        public bool shownInDetailsView { get; set; }
        public bool dimension { get; set; }
        public bool measure { get; set; }
        public bool recommendedVariable { get; set; }
        public string defaultScale { get; set; }
        public string phi { get; set; }
        public bool excludeFromShifting { get; set; }
        public bool sortable { get; set; }
        public object conceptURI { get; set; }
        public object rangeURI { get; set; }
        public string displayField { get; set; }
        public string displayFieldSqlType { get; set; }
        public string displayFieldJsonType { get; set; }
        public string inputType { get; set; }
        public string shortCaption { get; set; }
        public string facetingBehaviorType { get; set; }
        public string caption { get; set; }
        public Lookup lookup { get; set; }
        public string format { get; set; }
        public string extFormatFn { get; set; }
        public string extFormat { get; set; }
    }

    public class Ext
    {
    }

    public class Lookup
    {
        public string schema { get; set; }
        public string keyColumn { get; set; }
        public bool _public { get; set; }
        public string displayColumn { get; set; }
        public bool isPublic { get; set; }
        public string queryName { get; set; }
        public string schemaName { get; set; }
        public string table { get; set; }
    }

    public class Columnmodel
    {
        public bool hidden { get; set; }
        public string dataIndex { get; set; }
        public bool editable { get; set; }
        public string width { get; set; }
        public string header { get; set; }
        public int scale { get; set; }
        public bool sortable { get; set; }
        public string align { get; set; }
        public bool required { get; set; }
    }

    public class Row
    {
        public int RepresentativeDataState { get; set; }
        public int Owner { get; set; }
        public string Modified { get; set; }
        public string Description { get; set; }
        public string _labkeyurl_TransitionCount { get; set; }
        public string FileName { get; set; }
        public string _labkeyurl_SmallMoleculeCount { get; set; }
        public int ModifiedBy { get; set; }
        public string _labkeyurl_CreatedBy { get; set; }
        public string Created { get; set; }
        public int CalibrationCurveCount { get; set; }
        public string Container { get; set; }
        public int PeptideGroupCount { get; set; }
        public int DataId { get; set; }
        public string _labkeyurl_CalibrationCurveCount { get; set; }
        public object iRTScaleId { get; set; }
        public object SkydDataId { get; set; }
        public int PrecursorCount { get; set; }
        public string _labkeyurl_PeptideGroupCount { get; set; }
        public string FormatVersion { get; set; }
        public bool Deleted { get; set; }
        public string _labkeyurl_PrecursorCount { get; set; }
        public int ListCount { get; set; }
        public object Status { get; set; }
        public string _labkeyurl_Owner { get; set; }
        public int CreatedBy { get; set; }
        public string _labkeyurl_ModifiedBy { get; set; }
        public string _labkeyurl_Container { get; set; }
        public int SmallMoleculeCount { get; set; }
        public int ReplicateCount { get; set; }
        public string DocumentGUID { get; set; }
        public string SoftwareVersion { get; set; }
        public string _labkeyurl_ListCount { get; set; }
        public int PeptideCount { get; set; }
        public string _labkeyurl_ReplicateCount { get; set; }
        public string _labkeyurl_DataId { get; set; }
        public int TransitionCount { get; set; }
        public string _labkeyurl_PeptideCount { get; set; }
        public int Id { get; set; }
        public int AuditLogEntriesCount { get; set; }
        public string _labkeyurl_SkydDataId { get; set; }
    }
}
