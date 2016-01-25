#Install MSstats Packages
a<-installed.packages()
packages<-a[,1]

#if (!is.element("gplots",packages)){
install.packages("gplots" , repos='http://cran.us.r-project.org')
#}

#if (!is.element("lme4",packages)){
install.packages("lme4" , repos='http://cran.us.r-project.org')
#}

#if (!is.element("reshape",packages)){
install.packages("reshape" , repos='http://cran.us.r-project.org')
#}

#if (!is.element("reshape2",packages)){
install.packages("reshape2" , repos='http://cran.us.r-project.org')
#}

#if (!is.element("grid",packages)){
install.packages("grid" , repos='http://cran.us.r-project.org')
#}

#if (!is.element("ggplot2",packages)){
install.packages("ggplot2" , repos='http://cran.us.r-project.org')
#}

install.packages("ggrepel" , repos='http://cran.us.r-project.org')


#if (!is.element("data.table",packages)){
install.packages("data.table" , repos='http://cran.us.r-project.org')
#}

#if (!is.element("limma",packages)){
source("http://bioconductor.org/biocLite.R")
biocLite("limma")
#}

#if (!is.element("preprocessCore",packages)){
source("http://bioconductor.org/biocLite.R")
biocLite("preprocessCore")
#}

#if (!is.element("marray",packages)){
source("http://bioconductor.org/biocLite.R")
biocLite("marray")
#}

#if (!is.element("Rcpp",packages)){
install.packages("Rcpp" , repos='http://cran.us.r-project.org')
#}

install.packages("XML" , repos='http://cran.us.r-project.org')

#if (!is.element("MSnbase",packages)){
source("http://bioconductor.org/biocLite.R")
biocLite("MSnbase")
#}


if (!is.element("MSstats",packages) || packageVersion("MSstats") < "3.3.4" ){
directory <- tempdir()
directory<-gsub("\\", "/", directory, fixed = TRUE)
filename <- "MSstats_3.3.4.tar.gz"
path <- file.path(directory, filename)
#MSstatsPackage <-normalizePath(path)
download.file("http://www.stat.purdue.edu/~choi67/MSstats_3.3.4.tar.gz",path)

install.packages(path, repos = NULL, type="source")
}