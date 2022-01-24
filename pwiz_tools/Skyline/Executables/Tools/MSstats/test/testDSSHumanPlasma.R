source("../MSstatsExternalTool.R")
inputFile <- normalizePath("Human_plasma.csv")
outputdir <- file.path(getwd(), "TestResult");
outputdir <- paste(outputdir, "/", sep="")
dir.create(outputdir, showWarnings = FALSE)
MsStatsExternalTool(c("DSS", "--dataFileName", inputFile, "--numSample", "2", #"--power", ".8", 
                      "--FDR", ".05", "--ldfc", "1.25", "--udfc", "1.75", "--outputFolder", outputdir))
