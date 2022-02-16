source("../MSstatsExternalTool.R")
inputFile <- normalizePath("Human_plasma.csv")
outputdir <- file.path(getwd(), "TestResult");
outputdir <- paste(outputdir, "/", sep="")
dir.create(outputdir, showWarnings = FALSE)
QualityControl(inputFile, FALSE, "all", 100, 200, address=outputdir)