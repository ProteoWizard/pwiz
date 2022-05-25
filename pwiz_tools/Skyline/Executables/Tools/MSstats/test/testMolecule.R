source("../MSstatsExternalTool.R")
inputFile <- normalizePath("ThreeMolecules.csv")
outputdir <- file.path(getwd(), "TestResult");
outputdir <- paste(outputdir, "/", sep="")
dir.create(outputdir, showWarnings=FALSE)
MsStatsExternalTool(c("GC", "--dataFileName", inputFile, "--outputFolder", outputdir))
