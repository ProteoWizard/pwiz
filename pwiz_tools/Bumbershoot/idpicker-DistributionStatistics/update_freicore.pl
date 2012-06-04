# get directory the script is running from
use File::Basename;
my ($scriptFilename, $scriptPath) = fileparse($0);

# update freicore external
system("$scriptPath/freicore/update_externals.pl", "$scriptPath");