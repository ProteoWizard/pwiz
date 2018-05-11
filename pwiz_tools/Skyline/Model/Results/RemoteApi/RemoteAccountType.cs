using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public abstract class RemoteAccountType
    {
        public static readonly RemoteAccountType CHORUS = new Chorus();
        public static readonly RemoteAccountType UNIFI = new Unifi();
        public static readonly ImmutableList<RemoteAccountType> ALL = ImmutableList.ValueOf(new[] {UNIFI, CHORUS});

        public abstract string Name { get;  }
        public abstract string Label { get; }
        public abstract RemoteUrl GetEmptyUrl();
        public abstract RemoteAccount GetEmptyAccount();
        public override string ToString()
        {
            return Label;
        }

        private class Chorus : RemoteAccountType
        {
            public override string Label
            {
                get { return "Chorus"; }
            }

            public override string Name
            {
                get { return "chorus"; }
            }

            public override RemoteUrl GetEmptyUrl()
            {
                return ChorusUrl.EMPTY;
            }

            public override RemoteAccount GetEmptyAccount()
            {
                return ChorusAccount.BLANK;
            }
        }

        private class Unifi : RemoteAccountType
        {
            public override RemoteUrl GetEmptyUrl()
            {
                return UnifiUrl.EMPTY;
            }

            public override string Label
            {
                get { return "Unifi"; }
            }

            public override string Name
            {
                get { return "unifi"; }
            }

            public override RemoteAccount GetEmptyAccount()
            {
                return new UnifiAccount(null, null).ChangeServerUrl("https://unifiapi.waters.com:50034");
            }
        }
    }
}
