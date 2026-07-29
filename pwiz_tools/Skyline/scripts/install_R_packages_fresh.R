# Cran packages
install.packages("rmarkdown", repos = "https://mran.revolutionanalytics.com/snapshot/2016-01-02")

install.packages(c("DBI", "XML", "sp", "RSQLite", "Rcpp", 
				"MatrixModels", "robustbase", "lme4", "MALDIquant", "colorspace",
				"cellranger", "data.table", "uuid", "readr", "aplpack",
				"gplots", "missForest", "outliers", "ROCR", "R.utils", "tidyr",
				"VIM", "vioplot", "xtable", "psych"))

install.packages("digest")

# Bioconductor packages
source("https://bioconductor.org/biocLite.R")
biocLite(c("MSnbase", "Biobase", "limma", "marray", "S4Vectors", 
			"IRanges", "vsn", "mzR", "mzID", "affy", 
			"pcaMethods", "MSstats"))	

install.packages("devtools")
library(devtools)
# WARNING: Rtools is required to build R packages, but is not currently installed.
# Please download and install Rtools 3.3 from http://cran.r-project.org/bin/windows/Rtools/ and then run find_rtools().
# C:\Rtools\bin may need to be added to the path
 
find_rtools()

install_github("IFIproteomics/LFQbench")
