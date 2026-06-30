using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
// The AssemblyTitle / AssemblyDescription / AssemblyCompany / AssemblyProduct /
// AssemblyCopyright / AssemblyTrademark / AssemblyCulture attributes are
// supplied by the SDK-style csproj on net8.0-windows. On net472 they stay here
// so the legacy build output matches today verbatim.
#if NET472
[assembly: AssemblyTitle("Common")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("University of Washington")]
[assembly: AssemblyProduct("Common")]
[assembly: AssemblyCopyright("Copyright © University of Washington 2009-2011")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
#endif

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("b0573050-6806-494e-a1aa-ca89f18a8f17")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
#if NET472
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
#endif

[assembly: InternalsVisibleTo("CommonTest")]
