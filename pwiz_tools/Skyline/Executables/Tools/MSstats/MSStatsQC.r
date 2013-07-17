runQC<-function() {

cat("\n\n =======================================")
cat("\n ** Loading the required library..... \n")

# load the library
library(MSstats)

# Input data
filename<-commandArgs(trailingOnly=TRUE)[1];

raw<-read.csv(filename)
colnames(raw)[10]<-"Intensity"
raw$Intensity<-as.character(raw$Intensity)
raw$Intensity<-as.numeric(raw$Intensity)

## impute zero to NA
raw[!is.na(raw$Intensity)&raw$Intensity==0,"Intensity"]<-NA

#=====================
# Function: dataProcess
# pre-processing data: quality control of MS runs

cat("\n\n =======================================")
cat("\n ** Data Processing for analysis..... \n")

quantData<-dataProcess(raw)

write.csv(quantData,file="dataProcessedData.csv")
cat("\n Saved dataProcessedData.csv \n")



#=====================
# Function: dataProcessPlots
# visualization 

cat("\n\n =======================================")
cat("\n ** Generating dataProcess Plots..... \n")


dataProcessPlots(data=quantData,type="ProfilePlot",address="")
cat("\n Saved ProfilePlot.pdf \n")

dataProcessPlots(data=quantData,type="QCPlot",address="")
cat("\n Saved QCPlot.pdf \n")

}

tryCatch({runQC()}, 
finally = {
cat("Finished")
})