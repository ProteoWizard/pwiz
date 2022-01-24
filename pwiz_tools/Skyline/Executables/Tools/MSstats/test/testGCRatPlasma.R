source("../MSstatsExternalTool.R")
inputFile <- normalizePath("Rat_plasma.csv")
outputdir <- file.path(getwd(), "TestResult");
outputdir <- paste(outputdir, "/", sep="")
dir.create(outputdir, showWarnings=FALSE)
MsStatsExternalTool(c("GC", "--dataFileName", inputFile, "--normalization", "globalStandards","--outputFolder", outputdir))
