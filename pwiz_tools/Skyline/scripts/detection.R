join <- function(...) {
  paste(..., sep = "")
}

#
# Global values for sharing scales between sites (comment out for site-specific scales)
#

acceptQCutoff = 0.01
acceptCvCutoff = 0.2
acceptRunsPercentile = 0.5

#
# Reading data
#

command_args <- commandArgs(trailingOnly = FALSE)
script_args <- commandArgs(trailingOnly = TRUE)
# print(command_args)

isCommand = length(script_args) > 0
if (isCommand) {
  reportPath <- script_args[1]
} else {
  reportPath = "D:/brendanx/20150518_Ben_pan_human/MultiSite/Panhuman-18rp5rt-40kd/PeakAreasScored.csv"
}
reportPath = gsub("\\\\", "/", reportPath)
siteName = strsplit(reportPath, "/")[[1]][4]
if (length(grep("^MultiSite", siteName)) != 0) {
  maxHistCount = 6000
  maxHistRunCount = 10000
  maxRunCount = 100
  maxPepRunCount = 75
} else {
  maxHistCount = 8000
  maxHistRunCount = 30000
  maxRunCount = 70
  maxPepRunCount = 55
}
rootPath = dirname(dirname(dirname(reportPath))) # <root>/Site#/Trial/report.csv - in analysis root
#rootPath = dirname(reportPath) # <root>/Site#/Trial/report.csv - in the same directory
pdfPath = join(rootPath, "/", siteName, "-PeakAreasCvs.pdf")
csvPath = join(rootPath, "/", siteName, "-PeakAreasNoQuantCvs.csv")
summaryPath = join(rootPath, "/Summary.csv")

cat("Reading ", reportPath, "\n")
cat("Generating", pdfPath, "\n")
peaksTable = read.csv(reportPath, stringsAsFactors = F, na.strings = "#N/A")
#sapply(peaksTable, class)
#head(peaksTable)
#summary(peaksTable)

#
# Utility functions for selecting rows with q value and cv restrictions
#

whichAcceptQ <- function(acceptTable, qcol, qcutoff = acceptQCutoff) {
  which(acceptTable[[qcol]] < qcutoff)  
}
whichAcceptQAndAreaNa <- function(acceptTable, qcol, acol, qcutoff = acceptQCutoff) {
  which(acceptTable[[qcol]] < qcutoff & is.na(acceptTable[[acol]]))
}
whichNotAcceptQ <- function(acceptTable, qcol, qcutoff = acceptQCutoff) {
  which(acceptTable[[qcol]] >= qcutoff)  
}
whichAcceptCv <- function(acceptTable, qcol, cvcol, qcutoff= acceptQCutoff) {
  which(acceptTable[[qcol]] < qcutoff & !is.na(acceptTable[[cvcol]]))  
}
whichAcceptQuant <- function(acceptTable, qcol, cvcol, qcutoff= acceptQCutoff, cvCutoff = acceptCvCutoff) {
  which(acceptTable[[qcol]] < qcutoff & !is.na(acceptTable[[cvcol]]) & acceptTable[[cvcol]] <= cvCutoff)  
}
whichAcceptNotQuant <- function(acceptTable, qcol, cvcol, qcutoff= acceptQCutoff, cvCutoff = acceptCvCutoff) {
  which(acceptTable[[qcol]] < qcutoff & !is.na(acceptTable[[cvcol]]) & acceptTable[[cvcol]] > cvCutoff)  
}

#
# Summarize data for plotting
#

setNaAreas <- function(areasTable, acol, qcol) {
  if (length(whichAcceptQAndAreaNa(areasTable, acol, qcol)) > 0) {
    # Set acceptable q values with #N/A q value to 1 to keep them from getting counted
    areasTable[whichAcceptQAndAreaNa(areasTable, acol, qcol),][[qcol]] <- 1
  }
  areasTable[whichNotAcceptQ(areasTable, qcol),][[acol]] <- NA
  return (areasTable)
}
setNaAreasNaQValues <- function(areasTable, acol, qcol) {
  if (length(which(is.na(areasTable[[acol]]))) > 0) {
    areasTable[is.na(areasTable[[acol]]),][[qcol]] <- NA
  }
  return (areasTable)
}
setNoNaQValues <- function(areasTable, qcol) {
  if (length(which(is.na(areasTable[[qcol]]))) > 0) {
    areasTable[is.na(areasTable[[qcol]]),][[qcol]] <- 1
  }
  return (areasTable)
}

# Store indices for q value and area columns
idColCount = 4
repColCount = 2
if (length(grep("User", names(peaksTable))) > 0) {
  repColCount = 3
}

countReps = (ncol(peaksTable) - 4)/repColCount
acols = c() # peak area columns
qcols = c() # q value columns
ucols = c() # q value columns in unique peptide table
qnas = 0
anas = 0
reintegrated = 0
for (n in 1:countReps) {
  # 3 descriptive columns followed by area, q value pairs
  acol = idColCount + 1 + (n-1)*repColCount
  anas = anas + length(which(is.na(peaksTable[[acol]])))
  acols = c(acols, acol)
  qcol = acol + 1
  peaksTable = setNaAreasNaQValues(peaksTable, acol, qcol)
  qnas = qnas + length(which(is.na(peaksTable[[qcol]])))
  if (repColCount > 2) {
    rcol = qcol + 1
    reintegrated = reintegrated + length(which(peaksTable[[rcol]] != "FALSE"))
  }
  # set area values for replicates with q value greater than cut-off to NA (missing)
  peaksTable = setNoNaQValues(peaksTable, qcol)
  peaksTable = setNaAreas(peaksTable, acol, qcol)
  # columns for unique peptides table about to be created
  qcols = c(qcols, qcol)
  ucols = c(ucols, 1 + n) # just best q value for each unique peptide
}
cat("starting area na count =", anas, "\n")
cat("q value na count =", qnas, "\n")
if (repColCount > 2) {
  cat("reintegrated peaks = ", reintegrated, "\n")
}
# Create the unique peptides table
seqColName = "ModifiedSequence"
if (length(grep(seqColName, names(peaksTable))) == 0) {
  seqColName = "Modified.Sequence"
}
uniqueTable = aggregate(x = peaksTable[,qcols], by = list(peaksTable[[seqColName]]), FUN = "min")

# Report total number of precursors and peptides queried
precursorsCount = nrow(peaksTable)
peptidesCount = nrow(uniqueTable)
cat("queried:  precursors = ", precursorsCount, "\n",
    "          unique pep = ", peptidesCount, "\n", sep = "")

# Count non-NA area columns for each precursor and add Min and Max q value columns
peaksTable$Count = apply(peaksTable[,acols], 1, function(v) {length(which(!is.na(v)))})
#peaksTable$Count = apply(peaksTable[,qcols], 1, function(v) {length(which(v < acceptQCutoff))})
minNoNA <- function(x) {min(x, na.rm = TRUE)}
maxNoNA <- function(x) {max(x, na.rm = TRUE)}
peaksTable$Min = apply(peaksTable[,qcols], 1, min)
peaksTable$Max = apply(peaksTable[,qcols], 1, max)
# Discard all rows in the peaks table which do not have at least one peak meating the q value cut-off
peaksTable = peaksTable[whichAcceptQ(peaksTable, "Min"),]

# Calculate cumulative min and max columns for lines in the bar plots showing
# Min: false-positive accumulation
# Max: loss of completeness
mqcols = c()
xqcols = c()
for (n in 1:countReps) {
  peaksTable[[join("Min", n)]] = apply(peaksTable[,qcols[1:n],drop=FALSE], 1, min)
  mqcols = c(mqcols, ncol(peaksTable))
  peaksTable[[join("Max", n)]] = apply(peaksTable[,qcols[1:n],drop=FALSE], 1, max)
  xqcols = c(xqcols, ncol(peaksTable))
}

# Repeat above for unique peptides table
uniqueTable$Min = apply(uniqueTable[,ucols], 1, min)
uniqueTable$Max = apply(uniqueTable[,ucols], 1, max)
uniqueTable = uniqueTable[whichAcceptQ(uniqueTable, "Min"),]
mucols = c()
xucols = c()
for (n in 1:countReps) {
  uniqueTable[[join("Min", n)]] = apply(uniqueTable[,ucols[1:n],drop=FALSE], 1, min)
  mucols = c(mucols, ncol(uniqueTable))
  uniqueTable[[join("Max", n)]] = apply(uniqueTable[,ucols[1:n],drop=FALSE], 1, max)
  xucols = c(xucols, ncol(uniqueTable))
}

#
# Acceptance and reporting utility functions
#
acceptCount <- function(colName) {length(whichAcceptQ(peaksTable, colName))}
acceptPercent <- function(colName) {round(acceptCount(colName) / precursorsCount * 100, 1)}
acceptPercentOfDetected <- function(colName) {round(acceptCount(colName) / nrow(peaksTable) * 100, 1)}
acceptText <- function(colName) {join(acceptCount(colName), " - ", acceptPercent(colName), "%")}
acceptTextOfDetected <- function(colName) {join(acceptCount(colName), " - ", acceptPercentOfDetected(colName), "%")}
acceptQuant <- function(colName, colCv, cvCutoff = acceptCvCutoff) {length(whichAcceptQuant(peaksTable, colName, colCv, cvCutoff = cvCutoff))}
acceptUCount <- function(colName) {length(whichAcceptQ(uniqueTable, colName))}
acceptUPercent <- function(colName) {round(acceptUCount(colName) / peptidesCount * 100, 1)}
acceptUPercentOfDetected <- function(colName) {round(acceptUCount(colName) / nrow(uniqueTable) * 100, 1)}
acceptUText <- function(colName) {join(acceptUCount(colName), " - ", acceptUPercent(colName), "%")}
acceptUTextOfDetected <- function(colName) {join(acceptUCount(colName), " - ", acceptUPercentOfDetected(colName), "%")}
acceptSet <- function(cols, acceptFunc) {
  acceptSet = c()
  for (col in cols) {
    acceptSet = c(acceptSet, acceptFunc(col))
  }
  return (acceptSet)
}

getModifierText <- function(percentile, caps = FALSE)
{
  if (percentile < 1.0) {
    if (caps) {
      return ("at Least")
    } else {
      return ("at least")
    }
  }
  if (caps) {
    return ("All")
  }
  return ("all")
}

getRunsText <- function(runs, percentile, caps = FALSE)
{
  text = paste(getModifierText(percentile, caps), round(runs*percentile))
  if (percentile < 1.0) {
    text = paste(text, join("(of ", runs, ")"))
  }
  return (text)
}

catAcceptAt <- function(colName, runs, percentile) {
  cat(getRunsText(runs, percentile), " runs: precursors = ", acceptTextOfDetected(colName), "\n",
      "          unique pep = ", acceptUTextOfDetected(colName), "\n", sep = "")
}

# Calculate initial global statistics on detections
detectRuns = acceptSet(qcols, acceptCount)
detectRunsGrowth = acceptSet(mqcols, acceptCount)
detectRunsComplete = acceptSet(xqcols, acceptCount)
detectRunsU = acceptSet(ucols, acceptUCount)
detectRunsGrowthU = acceptSet(mucols, acceptUCount)
detectRunsCompleteU = acceptSet(xucols, acceptUCount)

percentRunDetections = round(mean(detectRuns) / acceptCount("Min") * 100, 1)
percentRunDetectionsU = round(mean(detectRunsU) / acceptUCount("Min") * 100, 1)

# Report unrestricted detections and then complete (all runs) detections
cat("detected: precursors = ", acceptText("Min"), " (", paste(detectRuns, collapse = ", "), ", mean = ", mean(detectRuns), ") - ", percentRunDetections, "%\n",
    "          unique pep = ", acceptUText("Min"), " (", paste(detectRunsU, collapse = ", "), ", mean = ", mean(detectRunsU), ") - ", percentRunDetectionsU, "%\n", sep = "")
catAcceptAt("Max", countReps, 1.0)

# Calculate statistics at varying percentiles with q value < cutoff
percentiles = c(1.0, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2)
#percentiles = c(0.5)
percentileColumn <- function(p) {if (p == 1.0)  "Max" else join("P", p*100)}
percentileText <- function(p) {join(p*100, "% complete")}

medsTable = data.frame(percentiles) # medians for median normalization

for (p in percentiles) {
  colPercent = percentileColumn(p)
  if (p != 1.0) {
    peaksTable[[colPercent]] = apply(peaksTable[,qcols], 1, function(v) {quantile(v, p, na.rm = TRUE)})
    uniqueTable[[colPercent]] = apply(uniqueTable[,ucols], 1, function(v) {quantile(v, p, na.rm = TRUE)})
    catAcceptAt(colPercent, countReps, p)
  }
  for (i in 1:countReps) {
    medsTable[which(medsTable$percentiles == p), c(join("Run", i))] = median(peaksTable[whichAcceptQ(peaksTable, colPercent), c(acols[i])], na.rm = TRUE)
  }
}
medsTable$median = apply(medsTable[,2:(countReps+1)], 1, median) # medians of medians

getMeds <- function(p) {as.numeric(as.vector(medsTable[which(medsTable$percentiles == p), 2:(countReps + 1)]))}
getMedOfMeds <- function(p) {as.numeric(medsTable[which(medsTable$percentiles == p), c("median")])}

# Calculate CVs and plot
meanIgnoreNA <- function(v) {mean(v, na.rm = TRUE)}
stdevIgnoreNA <- function(v) {sd(v, na.rm = TRUE)}
meanPercentileColumn <- function(p) {join("Mean.", percentileColumn(p))}
stdevPercentileColumn <- function(p) {join("SD.", percentileColumn(p))}
cvPercentileColumn <- function(p) {join("CV.", percentileColumn(p))}
peaksTable$Mean.Total.Area = apply(peaksTable[,acols], 1, meanIgnoreNA)
peaksTable$SD.Total.Area = apply(peaksTable[,acols], 1, stdevIgnoreNA)
peaksTable$CV.Total.Area = peaksTable$SD.Total.Area/peaksTable$Mean.Total.Area
for (p in percentiles) {
  colPercent = percentileColumn(p)
  normalizedValues <- function(v) {exp(log(v) - log(getMeds(p)) + log(getMedOfMeds(p)))}
  meanNormalized <- function(v) {meanIgnoreNA(normalizedValues(v))}
  sdNormalized <- function(v) {stdevIgnoreNA(normalizedValues(v))}
  peaksTable[[meanPercentileColumn(p)]] = apply(peaksTable[,acols], 1, meanNormalized)
  peaksTable[[stdevPercentileColumn(p)]] = apply(peaksTable[,acols], 1, sdNormalized)
  peaksTable[[cvPercentileColumn(p)]] = peaksTable[[stdevPercentileColumn(p)]]/peaksTable[[meanPercentileColumn(p)]]
  cat("normalized cvs at", p, "\n")
}

#
# Plotting
#

# Colors (2D histogram and shades of blue)
invisible(library(gplots))
library(RColorBrewer)
rf <- colorRampPalette(rev(brewer.pal(11,'Spectral')))
r <- rf(32)
r2 = c(rgb(255,255,255,maxColorValue = 255), r[2:length(r)])
blues1 <- brewer.pal(9, 'Blues')
#display.brewer.all()

#plot(0, type="n", ylab="", xlab="",
#     axes=FALSE, ylim=c(1,0), xlim=c(1,length(r2)))

#for (i in 1:length(r2))
#{
#  rect(i-0.5,0, i+0.5,1, border="black", col=r2[i])
#}

# Runs bargraph plot
plotRunsBarAndLine <- function(itemType, runsCounts, runsGrowth, runsComplete, count50, countQuant = NULL, isPeps = FALSE, normalizedQuant = FALSE) {
  factor = 1000
  runsCounts = runsCounts/factor
  runsGrowth = runsGrowth/factor
  runsComplete = runsComplete/factor
  count50 = count50/factor
  runsMean = round(mean(runsCounts), 1)
  runsSD = round(sd(runsComplete), 1)
  
  # Plot run bars
  ymax = max(runsGrowth) * 1.05;
  if (!isPeps & exists("maxRunCount")) {
    ymax = maxRunCount
  }
  if (isPeps & exists("maxPepRunCount")) {
    ymax = maxPepRunCount
  }
  df.bar = barplot(runsCounts, names.arg = 1:length(runsCounts), ylim = c(0, ymax),
                   main = paste(itemType, "Detections by Run"), xlab = "Run Number", ylab = "Detections (thousands)", col = blues1[5])
  text(0, ymax, paste("mean:", runsMean, "\nstddev:", runsSD), srt = 0.2, adj = c(0, 1))
  
  # Plot cumulative line
  colCumulative = r2[23]
  lines(x = df.bar, y = runsGrowth, lwd = 2, col = colCumulative)
  cumulativeLast = round(runsGrowth[length(runsGrowth)], 1)
  text(countReps, cumulativeLast, cumulativeLast, srt=0.2, adj=c(0, 0), col = colCumulative)
  
  # Complete line
  colComplete = blues1[9]
  lines(x = df.bar, y = runsComplete, lwd = 2, col = colComplete)
  completeLast = round(runsComplete[length(runsComplete)], 1)
  text(countReps, completeLast, completeLast, srt=0.2, adj = c(0, 1), col = colComplete)
  
  # 50% complete line
  col50 = "red"
  abline(h = count50, lty = 2, col = col50, lwd = 2)
  
  legendText = c("cumulative", "all runs", paste(getRunsText(countReps, 0.5), "-", round(count50, 1)))
  legendLty = c(1, 1, 2)
  legendLwd = c(2, 2, 2)
  legendCol = c(colCumulative, colComplete, col50)
  
  if (!is.null(countQuant)) {
    countQuant = countQuant/factor
    colQuant = "black"
    abline(h = countQuant, lty = 4, col = colQuant, lwd = 2)
    cvText = "cv < 20%"
    if (normalizedQuant) cvText = paste("norm", cvText)
    legendText = c(legendText, paste(cvText, "-", round(countQuant, 1)))
    legendLty = c(legendLty, 4)
    legendLwd = c(legendLwd, 2)
    legendCol = c(legendCol, colQuant)
  }
  
  legend(1, 1, yjust = 0, legendText, lty = legendLty, lwd = legendLwd, col = legendCol, bg = "white")
}
pMinColumn = percentileColumn(acceptRunsPercentile)
cvMinColumn = cvPercentileColumn(acceptRunsPercentile)
#plotRunsBarAndLine("Precursors", detectRuns, detectRunsGrowth, detectRunsComplete, acceptCount(pMinColumn), acceptQuant(pMinColumn, cvMinColumn))

# Histogram plot
thousands <- function(x) {
  round(x / 1000, 1)
}

plotHistCvs <- function(qcol, percentile, normalized = FALSE)
{
  qCutoff = acceptQCutoff
  cvCutoff = acceptCvCutoff
  cvCol = "CV.Total.Area"
  normalizedText = ""
  if (normalized) {
    cvCol = cvPercentileColumn(percentile)
    normalizedText = "Normalized"
  }
  detectedCvs = peaksTable[whichAcceptCv(peaksTable, qcol, cvCol), ][[cvCol]]*100
  detectedCvsCut = peaksTable[whichAcceptQuant(peaksTable, qcol, cvCol), ][[cvCol]]*100
  medianCV = median(detectedCvs)
  percentLessThanCuttoff = round((length(detectedCvsCut)/length(detectedCvs))*100, 1)
  # Plot the histogram of CVs
  if (exists("maxHistCount")) {
    df.hist = hist(detectedCvs, breaks = max(detectedCvs)/2, xlim = c(0, 100), ylim = c(0, maxHistCount), col = blues1[3],
         main = paste(normalizedText, "CVs of Precursors Detected in", getRunsText(countReps, percentile, TRUE), "Runs"), xlab = "CV (%)")
    ymax = maxHistCount
  } else {
    df.hist = hist(detectedCvs, breaks = max(detectedCvs)/2, xlim = c(0, 100), col = blues1[3],
         main = paste(normalizedText, "CVs of Precursors Detected in", getRunsText(countReps, percentile, TRUE), "Runs"), xlab = "CV (%)")
    ymax = max(df.hist$counts)
  }

  # Add median line
  abline(v = medianCV, lty = 2, col = "blue", lwd = 2)
  # Add less than cut-off line with percent label
  histMax = par("yaxp")[2] # Maximum y values
  segments(cvCutoff * 100, 0, cvCutoff * 100, histMax * 0.95, lty = 5, col = "red", lwd = 2)
  text(cvCutoff * 100, histMax * 0.95, join(format(percentLessThanCuttoff), "%"), srt=0.2, pos=3, col = "red")
  
  text(100, ymax, paste("total (thousands):", thousands(length(detectedCvs)), "\n", "below 20%:", thousands(length(detectedCvsCut))), adj = c(1, 1))

  normalizedCatText = if (normalized) "normalized" else ""
  cat(join(getRunsText(countReps, percentile), " runs ", normalizedCatText, " cvs: median = ", round(median(detectedCvs), 1), "%"),
      join("below ", cvCutoff * 100, "% = ", percentLessThanCuttoff, "%"),
      join("na = ", length(which(peaksTable[[qcol]] < qCutoff & is.na(peaksTable[[cvCutoff]])))), sep = ", ")
  cat("\n")
}
#plotHistCvs(pMinColumn, 0.5)

# 2D histogram plot
plotCvsByIntensity <- function(qcol, percentile, normalized = FALSE)
{
  cvCol = "CV.Total.Area"
  normalizedText = ""
  if (normalized) {
    cvCol = cvPercentileColumn(percentile)
    normalizedText = "Normalized"
  }
  cvPCol = join(cvCol, ".Percent")
  meanCol = meanPercentileColumn(percentile)
  logMeanCol = join("Log.", meanCol)
  peaksTable[[cvPCol]] = peaksTable[[cvCol]]*100
  peaksTable[[logMeanCol]] = log10(peaksTable[[meanCol]])
  # Use a 0.5 CV cut-off to keep bin count consistent
  detectedCvs = peaksTable[whichAcceptQuant(peaksTable, qcol, cvCol, cvCutoff = 0.5), c(logMeanCol, cvPCol)]

  hist2d(detectedCvs, nbins=c(150, 50), col=r2, xlim = c(3, 8), ylim = c(0, 50),
                   xlab = "Log10 Mean Area", ylab = "CV (%)", main = paste(normalizedText, "CVs by Intensity of Precursors Detected in",
                                                                           getRunsText(countReps, percentile, TRUE), "Runs"))
  
  abline(h = 20, lty = 2, col = "red", lwd = 2)
}
#plotCvsByIntensity(pMinColumn, 0.5, TRUE)

pdf(pdfPath, width = 7, height = 5)

plotRunsBarAndLine("Precursors", detectRuns, detectRunsGrowth, detectRunsComplete,
                   acceptCount(pMinColumn), acceptQuant(pMinColumn, "CV.Total.Area"))
plotRunsBarAndLine("Precursors", detectRuns, detectRunsGrowth, detectRunsComplete,
                   acceptCount(pMinColumn), acceptQuant(pMinColumn, cvMinColumn), normalizedQuant = TRUE)
plotRunsBarAndLine("Peptides", detectRunsU, detectRunsGrowthU, detectRunsCompleteU,
                   acceptUCount(pMinColumn), isPeps = TRUE)

if (exists("maxHistRunCount")) {
  hist(peaksTable[which(peaksTable$Count > 0), c("Count")], breaks = countReps, ylim = c(0, maxHistRunCount),
       main = paste(siteName, "Precursors Detected in Runs"), xlab = "Run Count", col = blues1[3])
} else {
  hist(peaksTable[which(peaksTable$Count > 0), c("Count")], breaks = countReps,
       main = paste(siteName, "Precursors Detected in Runs"), xlab = "Run Count", col = blues1[3])
}

for (p in percentiles) {
  plotHistCvs(percentileColumn(p), p)
}
for (p in percentiles) {
  plotHistCvs(percentileColumn(p), p, normalized = TRUE)
}
for (p in percentiles) {
  plotCvsByIntensity(percentileColumn(p), p)
}
for (p in percentiles) {
  plotCvsByIntensity(percentileColumn(p), p, normalized = TRUE)
}

invisible(dev.off())

# Write out high CV peptides for the site
highCvs = peaksTable[whichAcceptNotQuant(peaksTable, pMinColumn, cvMinColumn), c("ModifiedSequence", "PrecursorCharge", "Mean.P50", "CV.P50", "P50")]
write.csv(highCvs, file = csvPath, na = "#N/A")

# Site,Detected,Complete,HalfComplete,Quantifiable,MeanDetections,StddevDetections
cat(siteName, acceptCount("Min"), acceptCount(pMinColumn), acceptCount("Max"), acceptQuant(pMinColumn, cvMinColumn),
    mean(detectRuns), sd(detectRuns), file = summaryPath, sep = ",", append = TRUE)
cat("\n", file = summaryPath, append = TRUE)

