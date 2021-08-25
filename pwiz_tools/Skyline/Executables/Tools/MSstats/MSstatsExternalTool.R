require(dplyr)
require(MSstats)
ensureUniqueFileName <- function(baseName, folder, extension) {
  allfiles <- list.files(folder)
  num <- 0
  finalfile <- paste(baseName, extension, sep="")
  
  while (is.element(finalfile,allfiles)) {
    num <- num + 1
    finalfile <- paste(paste(baseName, num, sep="-"), extension, sep="")
  }
  return(file.path(folder, finalfile))
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
    distinctRows <- distinct(select(data, c(possibleUniqueColumn, groupColumns, definiteUniqueColumn)))
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

GroupComparison <- function(dataFileName, inputNormalize, fillIncompleteRows, address="") {
  logFile <- ensureUniqueFileName("MSstats_GroupComparison", address, ".log")
  data <- read.csv(dataFileName)
  data <- prepareSkylineDataSet(data)
  # TODO
  standardPepName <- c()
  input_feature_selection <- "all"
  quantData <- dataProcess(
    data, 
    normalization=inputNormalize,
    fillIncompleteRows=fillIncompleteRows,
    nameStandards=standardPepName, 
    featureSubset=input_feature_selection, 
    summaryMethod = "TMP", 
    censoredInt="0", log_file_path=logFile, append=TRUE)
  resultComparison <- try(groupComparison(contrast.matrix="pairwise", data=quantData, log_file_path=logFile, append=TRUE))
  
  if (class(resultComparison) != "try-error") {
    write.csv(resultComparison$ComparisonResult, file=ensureUniqueFileName("TestingResult", address, ".csv"))
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
    groupComparisonPlots(data=resultComparison$ComparisonResult, type="VolcanoPlot", address=address)
    cat("\n Saved VolcanoPlot.pdf \n")
    
    ## Visualization 2: Heatmap (required more than one comparisons)
    if (length(unique(resultComparison$ComparisonResult$Label)) > 1) {
      groupComparisonPlots(data=resultComparison$ComparisonResult, type="Heatmap", address=address)
      cat("\n Saved Heatmap.pdf \n")
    } else {
      cat("\n No Heatmap. Need more than 1 comparison for Heatmap. \n")
    }
    
    ## Visualization 3: Comparison plot
    groupComparisonPlots(data=resultComparison$ComparisonResult, type="ComparisonPlot", address=address)
    cat("\n Saved ComparisonPlot.pdf \n \n")
  } ## draw only for comparison working
}

GroupComparisonCmd <- function(arguments) {
  dataFileName <- arguments[1]
  comparisonName <- arguments[2]
  optionNormalize <- arguments[3]
  fillIncompleteRows <- arguments[4]
  featureSelection <- arguments[5]

  GroupComparison(dataFileName, optionNormalize, fillIncompleteRows)
}

MsStatsExternalTool <- function(arguments) {
  command <- arguments[1]
  if (command == "GC") {
    GroupComparisonCmd(arguments[-1])
  }
}

if (sys.nframe() == 0L) {
  arguments <- commandArgs(trailingOnly=TRUE);
  MsStatsExternalTool(arguments)  
}
