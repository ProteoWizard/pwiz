using AssortResources;

if (args.Length == 0)
{
    Console.Error.WriteLine("AssortResources.exe pathToResourceFile");
    return 1;
}

var rootFolder = Directory.GetCurrentDirectory();
var resourceFilePath = Path.Combine(rootFolder, args[0]);
var resourceIdentifiers = ResourceIdentifiers.FromPath(resourceFilePath);
var resourceAssorter = new ResourceAssorter(resourceIdentifiers);
resourceAssorter.ProcessRootFolder(rootFolder);
return 0;