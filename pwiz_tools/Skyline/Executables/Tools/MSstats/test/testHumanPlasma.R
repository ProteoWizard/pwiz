source("../MSstatsExternalTool.R")
inputFile <- normalizePath("Human_plasma.csv")
outputdir <- file.path(getwd(), "TestResult");
outputdir <- paste(outputdir, "/", sep="")
dir.create(outputdir)
GroupComparison(inputFile, FALSE, address=outputdir)