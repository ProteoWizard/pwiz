source("../MSstatsExternalTool.R")
inputFile <- normalizePath("Human_plasma.csv")
outputdir <- file.path(getwd(), "TestResult");
outputdir <- paste(outputdir, "/", sep="")
dir.create(outputdir, showWarnings = FALSE)
DesignSampleSize(inputFile, FALSE, "all", TRUE, 0.8, 0.05, 1.25, 1.75, address=outputdir)