#Install MSstats Packages
a<-installed.packages()
packages<-a[,1]

if (!is.element("gplots",packages)){
install.packages("gplots" , repos='http://cran.us.r-project.org')
}

if (!is.element("lme4",packages)){
install.packages("lme4" , repos='http://cran.us.r-project.org')
}

if (!is.element("reshape",packages)){
install.packages("reshape" , repos='http://cran.us.r-project.org')
}

if (!is.element("ggplot2",packages)){
install.packages("ggplot2" , repos='http://cran.us.r-project.org')
}

if (!is.element("limma",packages)){
source("http://bioconductor.org/biocLite.R")
biocLite("limma")
}

if (!is.element("preprocessCore",packages)){
source("http://bioconductor.org/biocLite.R")
biocLite("preprocessCore")
}

if (!is.element("marray",packages)){
source("http://bioconductor.org/biocLite.R")
biocLite("marray")
}

if (!is.element("Rcpp",packages)){
install.packages("Rcpp" , repos='http://cran.us.r-project.org')
}

if (!is.element("MSnbase",packages)){
source("http://bioconductor.org/biocLite.R")
biocLite("MSnbase")
}

if (!is.element("MSstats",packages) || packageVersion("MSstats") < "2.1.3" ){
directory <- tempdir()
gsub("\\", "/", directory, fixed = TRUE)
filename <- "MSstats_2.1.3.tar.gz"
path <- file.path(directory, filename)
MSstatsPackage <-normalizePath(path)
download.file("http://www.stat.purdue.edu/~choi67/MSstats_2.1.3.tar.gz", MSstatsPackage)

install.packages(MSstatsPackage, repos = NULL, type="source")
}