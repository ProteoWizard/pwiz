isoCsv = read.csv("C:\\pwiz.git\\DIA-Umpire\\DIA-Umpire\\src\\resource\\IsotopicPatternRange.csv", header=FALSE, col.names = c("Mass", rep_len(c("Mean", "SD"), 18)))
isoCsv = isoCsv[!is.nan(isoCsv[,2]),] # exclude NaN rows
options(stringsAsFactors = FALSE)
result = data.frame(isoCsv[,1])
for (i in 1:((ncol(isoCsv)-1)/2)) {
  mean = isoCsv[,2+(i-1)*2]
  sd = isoCsv[,3+(i-1)*2]
  lower = mean-3.3*sd
  upper = mean+3.3*sd
  result = cbind(result, paste(upper, lower, sep=','))
  #cat(paste("{", paste(upper, lower, sep=','), "}\n"))
}
cat("const IsotopePattern isotopePatternArray[] = {\n")
for (i in 1:nrow(isoCsv)) {
  cat(paste0("{ ", result[i,1], ", { ", paste("{", result[i,2:ncol(result)], "}, ", sep="", collapse=""), "} },\n"))
}
cat("};\n")
cat(paste0("const int isotopePatternArraySize = ", nrow(isoCsv), ";"))
