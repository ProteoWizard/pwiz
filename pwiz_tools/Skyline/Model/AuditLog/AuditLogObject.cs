using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.AuditLog
{
    public class AuditLogObject : IAuditLogObject
    {
        public AuditLogObject(object obj)
        {
            Object = obj;
        }

        public string AuditLogText
        {
            get
            {
                if (Object == null)
                    return LogMessage.MISSING;

                return AuditLogToStringHelper.InvariantToString(Object) ??
                       AuditLogToStringHelper.KnownTypeToString(Object) ??
                       Reflector.ToString(Object.GetType(), null, Object, true, false, 0, 0); // This will always return some non-null string representation
            }
        }

        public bool IsName
        {
            get { return false; }
        }

        public object Object { get; private set; }

        public static object GetObject(IAuditLogObject auditLogObj)
        {
            var obj = auditLogObj as AuditLogObject;
            return obj != null ? obj.Object : auditLogObj;
        }

        public static IAuditLogObject GetAuditLogObject(object obj)
        {
            bool usesReflection;
            return GetAuditLogObject(obj, out usesReflection);
        }

        public static IAuditLogObject GetAuditLogObject(object obj, out bool usesReflection)
        {
            var auditLogObj = obj as IAuditLogObject;
            usesReflection = auditLogObj == null && !Reflector.HasToString(obj) && !AuditLogToStringHelper.IsKnownType(obj);
            return auditLogObj ?? new AuditLogObject(obj);
        }
    }
}