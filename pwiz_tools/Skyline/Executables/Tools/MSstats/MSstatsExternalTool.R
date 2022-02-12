require(dplyr)
require(optparse)
require(MSstats)

booleanOrString <- function(argument) {
  if (argument == "FALSE") {
    return(FALSE)
  }
  if (argument == "TRUE") {
    return(TRUE)
  }
  return(argument)
}
booleanOrNumber <- function(argument) {
  if (argument == "FALSE") {
    return(FALSE)
  }
  if (argument == "TRUE") {
    return(TRUE)
  }
  return(as.numeric(argument))
}

ensureUniqueFileName <- function(baseName, folder, extension) {
  if (folder == "") {
    folder = getwd();
  }
  allfiles <- list.files(folder)
  num <- 0
  finalfile <- paste(baseName, extension, sep="")
  
  while (is.element(finalfile,allfiles)) {
    num <- num + 1
    finalfile <- paste(paste(baseName, num, sep="-"), extension, sep="")
  }
  return(file.path(folder, finalfile))
}

ensurePathSeparatorEnd <- function(folderName) {
  if (folderName == "" || endsWith(folderName, "/") || endsWith(folderName, "\\")) {
    return(folderName);
  }
  return(paste(folderName, .Platform$file.sep, sep=""));
}

# Returns a new dataFrame where targetColumn is a value which either contains the 
# value from one of the "possibleUniqueColumns", or from "definiteUniqueColumn".
# If, for a given set of values of the groupColumns, one of the possibleUniqueColumns
# is as unique as definiteUniqueColumn, then the possibleUniqueColumn value is used.
ensureUniqueColumn <- function(data, targetColumn, possibleUniqueColumns, definiteUniqueColumn, groupColumns = c()) {
  result <- data[]
  if (!any(colnames(result) == targetColumn)) {
    result[targetColumn] <- data[definiteUniqueColumn]
  }
  result <- result[0,]
  for (possibleUniqueColumn in possibleUniqueColumns) {
    distinctRows <- distinct(select(data, c(all_of(possibleUniqueColumn), all_of(groupColumns), all_of(definiteUniqueColumn))))
    counts<-count(distinctRows, distinctRows[c(possibleUniqueColumn, groupColumns)],name="counts")
    dataJoinedWithCounts <- left_join(data, counts, by=c(possibleUniqueColumn, groupColumns))
    uniqueData <- dataJoinedWithCounts[dataJoinedWithCounts$counts == 1,]
    uniqueData[targetColumn] <- uniqueData[possibleUniqueColumn]
    uniqueData <- uniqueData[, colnames(uniqueData) != "counts"]
    result <- rbind(result, uniqueData)
    data <- dataJoinedWithCounts[dataJoinedWithCounts$count != 1,colnames(dataJoinedWithCounts) != "counts"]
  }
  data[targetColumn] <- data[definiteUniqueColumn]
  result <- rbind(result, data);
  return(result);
}

prepareSkylineDataSet <- function(data) {
  data <- ensureUniqueColumn(data, "ProteinName", "Protein", "ProteinLocator")
  data <- ensureUniqueColumn(data, "PeptideSequence", "Peptide", "PeptideLocator")
  data <- ensureUniqueColumn(data, "Run", "Replicate", "ResultFileLocator")
  data <- data[!(colnames(data) %in% c("ProteinLocator", "Peptide", "Replicate", "ResultFileLocator"))]
  colnames(data)[colnames(data) == "Area"] <- "Intensity"
  return(data);
}

CallDataProcess <- function(dataFileName, logFilePath, qValueCutoff = NULL, normalization = FALSE, featureSubset="all", ...) {
  data <- read.csv(dataFileName)
  data <- prepareSkylineDataSet(data)
  standardPepName <- c()
  if (normalization == "globalStandards") {
    standardPepName = unique(data[data$StandardType == 'Normalization',]$PeptideSequence)
  }
  if (is.numeric(qValueCutoff)) {
    data <- data[data$DetectionQValue <= qValueCutoff | data$StandardType != "",]
  }
  
  return(dataProcess(
    data, 
    normalization=normalization,
    nameStandards=standardPepName, 
    featureSubset=featureSubset, 
    summaryMethod = "TMP", 
    censoredInt="0", log_file_path=logFilePath, append=TRUE))
}

GroupComparison <- function(dataFileName, ..., outputFolder="") {
  outputFolder<-ensurePathSeparatorEnd(outputFolder)
  logFile <- ensureUniqueFileName("MSstats_GroupComparison", outputFolder, ".log")
  quantData <- CallDataProcess(dataFileName, logFile, ...)
  resultComparison <- try(groupComparison(contrast.matrix="pairwise", data=quantData, log_file_path=logFile, append=TRUE))
  
  if (class(resultComparison) != "try-error") {
    write.csv(resultComparison$ComparisonResult, file=ensureUniqueFileName("TestingResult", outputFolder, ".csv"))
    cat("\n Saved the testing result. \n")
  } else {
    cat("\n Error : Can't compare the groups. \n")
  }
  
  ## ---------------------------------
  ## Function: groupComparisonPlots
  ## visualization for testing results
  ## ---------------------------------
  
  if (class(resultComparison) != "try-error") {
    
    cat("\n\n =======================================")
    cat("\n ** Visualizing testing results..... \n")
    ## Visualization 1: Volcano plot
    ## default setup: FDR cutoff = 0.05; fold change cutoff = NA
    groupComparisonPlots(data=resultComparison$ComparisonResult, type="VolcanoPlot", address=outputFolder)
    cat("\n Saved VolcanoPlot.pdf in", outputFolder, "\n")
    
    ## Visualization 2: Heatmap (required more than one comparisons)
    if (length(unique(resultComparison$ComparisonResult$Label)) > 1) {
      groupComparisonPlots(data=resultComparison$ComparisonResult, type="Heatmap", address=outputFolder)
      cat("\n Saved Heatmap.pdf in", outputFolder, "\n")
    } else {
      cat("\n No Heatmap. Need more than 1 comparison for Heatmap. \n")
    }
    
    ## Visualization 3: Comparison plot
    groupComparisonPlots(data=resultComparison$ComparisonResult, type="ComparisonPlot", address=outputFolder)
    cat("\n Saved ComparisonPlot.pdf \n \n")
  } ## draw only for comparison working
}

QualityControl <- function(dataFileName, width, height, ..., outputFolder="") {
  outputFolder<-ensurePathSeparatorEnd(outputFolder)
  logFile <- ensureUniqueFileName("MSstats_DataProcess", outputFolder, ".log")
  quantData <- CallDataProcess(dataFileName, logFile, ...)
  
  
  if (class(quantData) != "try-error") {
    write.csv(quantData$ProcessedData, file=ensureUniqueFileName("dataProcessedData", outputFolder, ".csv"))
    cat("\n Saved dataProcessedData.csv \n")
  } else {
    stop(message("\n Error : Can't process the data. \n"))
  }
  
  ## --------------------------
  ## Function: dataProcessPlots
  ## visualization 
  ## --------------------------
  
  if (class(quantData) != "try-error") {
    cat("\n\n =======================================")
    cat("\n ** Generating dataProcess Plots..... \n \n")
    
    dataProcessPlots(data=quantData, type="ProfilePlot", 
                     address=outputFolder, width=width, height=height)
    cat("\n Saved ProfilePlot.pdf in", outputFolder, "\n \n")
    
    dataProcessPlots(data=quantData, type="QCPlot",
                     which.Protein = 'allonly',
                     address=outputFolder, width=width, height=height)
    cat("\n Saved QCPlot.pdf in", outputFolder, "\n \n")
    
    dataProcessPlots(data=quantData, type="ConditionPlot", 
                     address=outputFolder)
    cat("\n Saved ConditionPlot.pdf in", outputFolder, "\n ")
  }
}

DesignSampleSize <- function(dataFileName,
                             numSample = TRUE,
                             power = TRUE,
                             FDR,
                             ldfc,
                             udfc, ... , outputFolder="") {
  outputFolder<-ensurePathSeparatorEnd(outputFolder)
  logFile <- ensureUniqueFileName("MSstats_DesignSampleSize", outputFolder, ".log")
  quantData <- CallDataProcess(dataFileName, logFile, ...)
  if (class(quantData) != "try-error") {
    
    write.csv(quantData$ProcessedData, file=ensureUniqueFileName("dataProcessedData", outputFolder, ".csv"))
    cat("\n Saved dataProcessedData.csv \n")
  } else {
    stop(message("\n Error : Can't process the data. \n"))
  }

  
  ## --------------------------------------------------------------------
  ## Function: groupComparison
  ## generate testing results of protein inferences across concentrations
  ## --------------------------------------------------------------------
  
  ## here is the issues : labeled, and interference need to be binary, not character
  resultComparison <- try(groupComparison(contrast.matrix="pairwise", data=quantData, log_file_path=logFile, append=TRUE))
  
  if (class(resultComparison) == "try-error") {
    cat("\n Error : Can't get variance components. \n")
  }
  
  
  ## ---------------------------------------------------------------------------
  ## Function: designSampleSize
  ## calulate number of biological replicates per group for your next experiment
  ## ---------------------------------------------------------------------------
  
  cat("\n\n =======================================")
  cat("\n ** Calculating sample size..... \n")
  
  ## if t-test, can't sample size calculation
  countnull <- 0
  
  for (k in 1:length(resultComparison$fittedmodel)) {
    if (is.null(resultComparison$fittedmodel[[k]])) {
      countnull <- countnull + 1
    }
  }
  
  if(countnull == length(resultComparison$fittedmodel)){
    stop(message("\n Can't calculate sample size with log sum method. \n"))
  }
  
  result.sample <- try(designSampleSize(data=resultComparison$FittedModel, numSample=numSample, desiredFC=c(ldfc, udfc), FDR=FDR, power=power, log_file_path=logFile, append=TRUE))

  if (class(result.sample) != "try-error") {
    
    write.csv(result.sample, file=ensureUniqueFileName("SampleSizeCalculation", outputFolder, ".csv"))
    cat("\n Saved the Sample Size Calculation. \n")
  } else {
    stop(message("\n Error : Can't analyze. \n"))
  }
  
  if (class(result.sample) != "try-error") {
    
    pdf(ensureUniqueFileName("SampleSizePlot", outputFolder, ".pdf"))
    designSampleSizePlots(data=result.sample)
    dev.off()
    cat("\n Saved SampleSizePlot.pdf in", outputFolder, "\n \n")
  }
  
}

MsStatsExternalTool <- function(arguments) {
  options <- list(
    make_option("--dataFileName"),
    make_option("--normalization"),
    make_option("--msLevel", type="integer"),
    make_option("--featureSelection"),
    make_option("--outputFolder"),
    make_option("--qValueCutoff", type="double")
  )

  command <- arguments[1]

  if (command == "GC") {
    parser<-OptionParser(option_list = options)
    parsedArgs<-parse_args(parser, args=arguments[-1])
    do.call(GroupComparison, parsedArgs)
  } else if (command == "QC") {
    options <- c(options, 
                 make_option("--width", type="integer"),
                 make_option("--height", type="integer"))
    parser<-OptionParser(option_list = options)
    parsedArgs<-parse_args(parser, args=arguments[-1])
    do.call(QualityControl, parsedArgs)
  } else if (command == "DSS") {
    options <- c(options,
                 make_option("--numSample", type="integer"),
                 make_option("--power", type="double"),
                 make_option("--FDR", type="double"),
                 make_option("--ldfc", type="double"),
                 make_option("--udfc", type="double"))
    parser<-OptionParser(option_list = options)
    parsedArgs<-parse_args(parser, args=arguments[-1])
    do.call(DesignSampleSize, parsedArgs)
  }
}

if (sys.nframe() == 0L) {
  arguments <- commandArgs(trailingOnly=TRUE);
  MsStatsExternalTool(arguments)  
}
