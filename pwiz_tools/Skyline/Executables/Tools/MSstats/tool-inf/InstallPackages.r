install.packages(c("gplots", "lme4", "reshape", "reshape2",
                   "ggplot2", "ggrepel", "data.table", "dplyr", "tidyr",
                   "survival", "doSNOW", "snow", "foreach", 'stringr',
                   "randomForest", "minpack.lm", "optparse"), 
                 repos='http://cran.us.r-project.org')


if (!requireNamespace("BiocManager", quietly = TRUE))
    install.packages("BiocManager", repos='http://cran.us.r-project.org')
BiocManager::install("MSstats")
