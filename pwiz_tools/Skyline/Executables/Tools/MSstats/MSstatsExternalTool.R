require(dplyr)
require(MSstats)
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

GroupComparison <- function(dataFileName, inputNormalize) {
  browser()
  data <- read.csv(dataFileName)
  data <- prepareSkylineDataSet(data)
  # TODO
  standardPepName <- c()
  input_feature_selection <- FALSE
  quantData <- dataProcess(
    data, 
    normalization=inputNormalize, 
    nameStandards=standardPepName, 
    featureSubset=input_feature_selection, 
    summaryMethod = "TMP", 
    censoredInt="0")
  print(quantData)
}

GroupComparisonCmd <- function(arguments) {
  dataFileName <- arguments[1]
  optionnormalize <- arguments[2]
  
  ## input is character??
  inputnormalize <- FALSE 
  if (optionnormalize == 1) { 
    inputnormalize <- "equalizeMedians" 
  } else if (optionnormalize == 2) { 
    inputnormalize <- "quantile" 
  } else if (optionnormalize == 3) { 
    inputnormalize <- "globalStandards" 
  }
  
  GroupComparison(dataFileName, inputNormalize)
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
