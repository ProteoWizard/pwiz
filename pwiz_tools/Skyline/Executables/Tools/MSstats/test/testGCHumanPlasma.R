source("../MSstatsExternalTool.R")
inputFile <- normalizePath("Human_plasma.csv")
outputdir <- file.path(getwd(), "TestResult");
outputdir <- paste(outputdir, "/", sep="")
dir.create(outputdir, showWarnings=FALSE)
MsStatsExternalTool(c("GC", "--dataFileName", inputFile, "--outputFolder", outputdir))
