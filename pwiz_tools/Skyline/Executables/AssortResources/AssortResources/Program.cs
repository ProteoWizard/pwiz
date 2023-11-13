using System.Runtime.CompilerServices;
using AssortResources;

if (args.Length != 2)
{
    Console.Error.WriteLine("AssortResources.exe pathToCsProj pathToResourceFile");
    return 1;
}

var rootFolder = Directory.GetCurrentDirectory();
var csprojFile = Path.Combine(rootFolder, args[0]);
if (!File.Exists(csprojFile))
{
    Console.Error.WriteLine("{0} does not exist", csprojFile);
    return 1;
}

if (!csprojFile.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase))
{
    Console.Error.WriteLine("{0} is not a .csproj file", csprojFile);
    return 1;
}

var resourceFile = Path.Combine(rootFolder, args[1]);
if (!File.Exists(resourceFile))
{
    Console.Error.WriteLine("{0} does not exist", resourceFile);
    return 1;
}

if (!resourceFile.EndsWith(".resx", StringComparison.InvariantCultureIgnoreCase))
{
    Console.Error.WriteLine("{0} is not a .resx file", resourceFile);
    return 1;
}

var resourceAssorter = new ResourceAssorter(rootFolder, csprojFile, resourceFile);
resourceAssorter.DoWork();
return 0;