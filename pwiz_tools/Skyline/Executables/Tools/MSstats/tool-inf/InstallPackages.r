#Install MSstats Packages
#a <- installed.packages()
#packages <- a[, 1]

install.packages(c("gplots", "lme4", "reshape", "reshape2",
                   "ggplot2", "ggrepel", "data.table", "dplyr", "tidyr",
                   "survival", "doSNOW", "snow", "foreach", 'stringr',
                   "randomForest", "minpack.lm"), 
                 repos='http://cran.us.r-project.org')


if (!requireNamespace("BiocManager", quietly = TRUE))
    install.packages("BiocManager", repos='http://cran.us.r-project.org')
BiocManager::install("MSstats")



#if (!is.element("MSstats",packages) || packageVersion("MSstats") < "3.3.11" ){
#directory <- tempdir()
#directory<-gsub("\\", "/", directory, fixed = TRUE)
#filename <- "MSstats_3.3.11.tar.gz"
#path <- file.path(directory, filename)
#MSstatsPackage <-normalizePath(path)
#download.file("http://www.stat.purdue.edu/~choi67/MSstats_3.3.11.tar.gz",path)

#install.packages(path, repos = NULL, type="source")
#}
