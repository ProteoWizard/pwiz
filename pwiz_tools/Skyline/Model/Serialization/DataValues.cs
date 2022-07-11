using System.ComponentModel;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Serialization
{
    public static class DataValues
    {
        public static bool? FromOptional(SkylineDocumentProto.Types.OptionalBool optionalBool)
        {
            switch (optionalBool)
            {
                case SkylineDocumentProto.Types.OptionalBool.Missing:
                    return null;
                case SkylineDocumentProto.Types.OptionalBool.False:
                    return false;
                case SkylineDocumentProto.Types.OptionalBool.True:
                    return true;
            }
            return null;
        }

        public static SkylineDocumentProto.Types.OptionalBool ToOptional(bool? nullableBool)
        {
            return nullableBool.HasValue
                ? nullableBool.Value
                    ? SkylineDocumentProto.Types.OptionalBool.True
                    : SkylineDocumentProto.Types.OptionalBool.False
                : SkylineDocumentProto.Types.OptionalBool.Missing;
        }

        public static UserSet FromUserSet(SkylineDocumentProto.Types.UserSet userSet)
        {
            switch (userSet)
            {
                case SkylineDocumentProto.Types.UserSet.False:
                    return UserSet.FALSE;
                case SkylineDocumentProto.Types.UserSet.Imported:
                    return UserSet.IMPORTED;
                case SkylineDocumentProto.Types.UserSet.Matched:
                    return UserSet.MATCHED;
                case SkylineDocumentProto.Types.UserSet.Reintegrated:
                    return UserSet.REINTEGRATED;
                case SkylineDocumentProto.Types.UserSet.True:
                    return UserSet.TRUE;
            }
            return UserSet.FALSE;
        }

        public static SkylineDocumentProto.Types.UserSet ToUserSet(UserSet userSet)
        {
            switch (userSet)
            {
                case UserSet.FALSE:
                    return SkylineDocumentProto.Types.UserSet.False;
                case UserSet.IMPORTED:
                    return SkylineDocumentProto.Types.UserSet.Imported;
                case UserSet.MATCHED:
                    return SkylineDocumentProto.Types.UserSet.Matched;
                case UserSet.REINTEGRATED:
                    return SkylineDocumentProto.Types.UserSet.Reintegrated;
                case UserSet.TRUE:
                    return SkylineDocumentProto.Types.UserSet.True;
            }
            return SkylineDocumentProto.Types.UserSet.False;
        }

        public static double? FromOptional(SkylineDocumentProto.Types.OptionalDouble optionalDouble)
        {
            return optionalDouble == null ? (double?) null : optionalDouble.Value;
        }

        public static SkylineDocumentProto.Types.OptionalDouble ToOptional(double? doubleValue)
        {
            return doubleValue.HasValue
                ? new SkylineDocumentProto.Types.OptionalDouble {Value = doubleValue.Value} : null;
        }

        public static float? FromOptional(SkylineDocumentProto.Types.OptionalFloat optionalFloat)
        {
            return optionalFloat == null ? (float?)null : optionalFloat.Value;
        }

        public static string FromOptional(SkylineDocumentProto.Types.OptionalString optionalString)
        {
            return optionalString == null ? null : optionalString.Value;
        }

        public static SkylineDocumentProto.Types.OptionalString ToOptional(string value)
        {
            return value == null ? null : new SkylineDocumentProto.Types.OptionalString {Value = value};
        }

        public static SkylineDocumentProto.Types.OptionalFloat ToOptional(float? floatValue)
        {
            return floatValue.HasValue
                ? new SkylineDocumentProto.Types.OptionalFloat {Value = floatValue.Value}
                : null;
        }

        public static IonType FromIonType(SkylineDocumentProto.Types.IonType ionType)
        {
            switch (ionType)
            {
                case SkylineDocumentProto.Types.IonType.A:
                    return IonType.a;
                case SkylineDocumentProto.Types.IonType.B:
                    return IonType.b;
                case SkylineDocumentProto.Types.IonType.C:
                    return IonType.c;
                case SkylineDocumentProto.Types.IonType.Custom:
                    return IonType.custom;
                case SkylineDocumentProto.Types.IonType.Precursor:
                    return IonType.precursor;
                case SkylineDocumentProto.Types.IonType.X:
                    return IonType.x;
                case SkylineDocumentProto.Types.IonType.Y:
                    return IonType.y;
                case SkylineDocumentProto.Types.IonType.Z:
                    return IonType.z;
                case SkylineDocumentProto.Types.IonType.ZH:
                    return IonType.zh;
                case SkylineDocumentProto.Types.IonType.ZHh:
                    return IonType.zhh;
            }
            throw new InvalidEnumArgumentException();
        }

        public static SkylineDocumentProto.Types.IonType ToIonType(IonType ionType)
        {
            switch (ionType)
            {
                case IonType.a:
                    return SkylineDocumentProto.Types.IonType.A;
                case IonType.b:
                    return SkylineDocumentProto.Types.IonType.B;
                case IonType.c:
                    return SkylineDocumentProto.Types.IonType.C;
                case IonType.custom:
                    return SkylineDocumentProto.Types.IonType.Custom;
                case IonType.precursor:
                    return SkylineDocumentProto.Types.IonType.Precursor;
                case IonType.x:
                    return SkylineDocumentProto.Types.IonType.X;
                case IonType.y:
                    return SkylineDocumentProto.Types.IonType.Y;
                case IonType.z:
                    return SkylineDocumentProto.Types.IonType.Z;
                case IonType.zh:
                    return SkylineDocumentProto.Types.IonType.ZH;
                case IonType.zhh:
                    return SkylineDocumentProto.Types.IonType.ZHh;
            }
            throw new InvalidEnumArgumentException();
        }

        public static SkylineDocumentProto.Types.OptionalInt ToOptional(int? intValue)
        {
            return intValue.HasValue ? new SkylineDocumentProto.Types.OptionalInt() {Value = intValue.Value} : null;
        }

        public static int? FromOptional(SkylineDocumentProto.Types.OptionalInt optionalInt)
        {
            return optionalInt == null ? (int?) null : optionalInt.Value;
        }

        public static LossInclusion FromLossInclusion(SkylineDocumentProto.Types.LossInclusion lossInclusion)
        {
            switch (lossInclusion)
            {
                case SkylineDocumentProto.Types.LossInclusion.Always:
                    return LossInclusion.Always;
                case SkylineDocumentProto.Types.LossInclusion.Library:
                    return LossInclusion.Library;
                case SkylineDocumentProto.Types.LossInclusion.Never:
                    return LossInclusion.Never;
            }
            return LossInclusion.Library;
        }

        public static SkylineDocumentProto.Types.LossInclusion ToLossInclusion(LossInclusion lossInclusion)
        {
            switch (lossInclusion)
            {
                case LossInclusion.Always:
                    return SkylineDocumentProto.Types.LossInclusion.Always;
                case LossInclusion.Library:
                    return SkylineDocumentProto.Types.LossInclusion.Library;
                case LossInclusion.Never:
                    return SkylineDocumentProto.Types.LossInclusion.Never;
            }
            return SkylineDocumentProto.Types.LossInclusion.Library;
        }
    }
}
