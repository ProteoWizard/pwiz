require(dplyr)
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

peptide<-c('ELVIS', 'ELVIS', 'LIVES')
replicate<-c('A','A','A')
resultFile<-c('B','C','D')
initialData<-data.frame(replicate, peptide, resultFile)
uniqueData<-ensureUniqueColumn(initialData, "replicate2", "replicate", "resultFile", "peptide")
print(format(uniqueData))
