#Install External R Packages
#When a user installs a tool in Skyline this checks to make sure the required packages are also installed.  If the packages don't exist on the users machine skyline will
#require the user to install them before they can continue using the tool.  This InstallPackages.r file checks for the qcc and gplots R packages.
a<-installed.packages()
packages<-a[,1]

if (!is.element("qcc",packages)){
install.packages("qcc" , repos='http://cran.us.r-project.org')
}

if (!is.element("gplots",packages)){
install.packages("gplots" , repos='http://cran.us.r-project.org')
}

##The following code can install a custom package from a URL.
#if (!is.element("ExampleTool",packages) || packageVersion("ExampleTool") < "1.0" ){
#directory <- tempdir()
#gsub("\\", "/", directory, fixed = TRUE)
#filename <- "ExampleTool_1.0.tar.gz"
#path <- file.path(directory, filename)
#ExampleToolPackage <-normalizePath(path)
#download.file("http://www.exampletoolwebsite.com/ExampleTool_1.0.tar.gz", ExampleToolPackage)
#install.packages(ExampleToolPackage, repos = NULL, type="source")
#}