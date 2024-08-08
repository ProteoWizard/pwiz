#Install External R Packages
#When a user installs a tool in Skyline this checks to make sure the required packages are also installed.  If the packages don't exist on the users machine skyline will
#require the user to install them before they can continue using the tool.  This InstallPackages.r file checks for ______________ R packages.
a<-installed.packages()
packages<-a[,1]

cat("INSTALLING")



install.packages(c("tidyr", "plyr", "dplyr", "reshape2", "seqinr", "ggplot2", "coefplot", 
                   "forcats", "tibble", "stringr", "purrr", "gridExtra", "pracma", "hablar", "ade4"), 
                 repos='http://cran.us.r-project.org')


if (!requireNamespace("BiocManager", quietly = TRUE))
  install.packages("BiocManager", repos='http://cran.us.r-project.org')
BiocManager::install("qvalue")






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