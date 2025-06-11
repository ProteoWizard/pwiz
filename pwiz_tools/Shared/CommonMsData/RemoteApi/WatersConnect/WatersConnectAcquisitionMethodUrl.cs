using pwiz.Common.Collections;
using System.IO;
using System;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public class WatersConnectAcquisitionMethodUrl : WatersConnectUrl
    {
        public new static string UrlPrefix
        {
            get { return RemoteAccountType.WATERS_CONNECT.Name + @":acquisition_method:"; }
        }

        public Guid MethodVersionId { get; private set; }
        public string AcquisitionMethodId { get; private set; }
        public string MethodName { get; private set; }

        public WatersConnectAcquisitionMethodUrl(string watersConnectUrl) : base(watersConnectUrl)
        {
        }

        public WatersConnectAcquisitionMethodUrl ChangeMethodVersionId(Guid id)
        {
            return ChangeProp(ImClone(this), im => im.MethodVersionId = id);
        }
        public WatersConnectAcquisitionMethodUrl ChangeAcquisitionMethodId(string id)
        {
            return ChangeProp(ImClone(this), im => im.AcquisitionMethodId = id);
        }
        public WatersConnectAcquisitionMethodUrl ChangeMethodName(string name)
        {
            return ChangeProp(ImClone(this), im => im.MethodName = name);
        }


        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            if (MethodVersionId != Guid.Empty)
                result.SetValue(@"methodVersionId", MethodVersionId.ToString());
            if (AcquisitionMethodId != null)
                result.SetValue(@"acquisitionMethodId", AcquisitionMethodId);
            if (MethodName != null)
                result.SetValue(@"methodName", MethodName);
            return result;
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            var methodVersionId = nameValueParameters.GetValue(@"methodVersionId");
            if (!string.IsNullOrEmpty(methodVersionId))
            {
                Guid id;
                if (Guid.TryParse(methodVersionId, out id))
                {
                    MethodVersionId = id;
                }
                else
                {
                    throw new InvalidDataException(string.Format("Invalid method version Id {0}", methodVersionId));
                }
            }
            AcquisitionMethodId = nameValueParameters.GetValue(@"acquisitionMethodId");
            MethodName = nameValueParameters.GetValue(@"methodName");
            Type = ItemType.method;
        }
    }
}