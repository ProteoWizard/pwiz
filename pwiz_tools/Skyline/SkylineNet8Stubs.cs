// Stubs for Skyline types whose real implementations are excluded on net10.
// These satisfy compile-time references; runtime should never reach them.

namespace System.Deployment.Application
{
    // ClickOnce ApplicationDeployment isn't available on net8 — provide a stub
    // for source references; the net472 call sites are gone.
    public class ApplicationDeployment
    {
        public static bool IsNetworkDeployed => false;
        public static ApplicationDeployment CurrentDeployment => new ApplicationDeployment();
    }

    public class TrustNotGrantedException : Exception
    {
        public TrustNotGrantedException() { }
        public TrustNotGrantedException(string message) : base(message) { }
    }
}
