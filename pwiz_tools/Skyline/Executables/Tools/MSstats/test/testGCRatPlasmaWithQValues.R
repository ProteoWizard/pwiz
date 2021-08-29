source("../MSstatsExternalTool.R")
inputFile <- normalizePath("Rat_plasma_with_qvalues.csv")
outputdir <- file.path(getwd(), "TestResult");
outputdir <- paste(outputdir, "/", sep="")
dir.create(outputdir, showWarnings=FALSE)
MsStatsExternalTool(c("GC", "--dataFileName", inputFile, "--normalization", "globalStandards", "--qValueCutoff", ".01", "--outputFolder", outputdir))
