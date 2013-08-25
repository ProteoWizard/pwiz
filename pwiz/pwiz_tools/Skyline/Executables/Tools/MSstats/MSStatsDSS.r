runDSS<-function() {

cat("\n\n =======================================")
cat("\n ** Loading the required library..... \n")

# load the library
library(MSstats)

# Input data
arguments<-commandArgs(trailingOnly=TRUE);

raw<-read.csv(arguments[1])

# remove the rows for iRT peptides
raw<-raw[raw$StandardType!="iRT",]

# change column name as Intensity
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
# Function: designSampleSize
# calulate number of biological replicates per group for your next experiment

cat("\n\n =======================================")
cat("\n ** Calculating sample size..... \n")

## input for options
# num of sample?
# cat("\n Number of Biological Replicates :  ")
inputnsample<-arguments[2]
#inputnsample<-readline()
if(inputnsample!="TRUE") inputnsample<-as.numeric(inputnsample)
if(inputnsample=="") inputnsample<-"TRUE"

# num of peptide?
# cat("\n Number of Peptides per protein :  ")
inputnpep<-arguments[3]
#inputnpep<-readline()
if(inputnpep!="TRUE") inputnpep<-as.numeric(inputnpep)
if(inputnpep=="") inputnpep<-"TRUE"

# num of peptide?
# cat("\n Number of Transitions per peptides :  ")
inputntran<-arguments[4]
#inputntran<-readline()
if(inputntran!="TRUE") inputntran<-as.numeric(inputntran)
if(inputntran=="") inputntran<-"TRUE"

# power?
# cat("\n power : ")
inputpower<-arguments[5]
#inputpower<-readline()
if(inputpower!="TRUE") inputpower<-as.numeric(inputpower)
if(inputpower=="") inputpower<-"TRUE"

# FDR?
# cat("\n FDR : (Default is 0.05)")
inputfdr<-arguments[6]
#inputfdr<-readline()
if(inputfdr=="TRUE") inputfdr<-0.05
inputfdr<-as.numeric(inputfdr)

# desiredFC?
# cat("\n lower desired Fold Change : (Default is 1.25) ")
inputlower<-arguments[7]
#inputlower<-readline()
if(inputlower=="TRUE") inputlower<-1.25
inputlower<-as.numeric(inputlower)

#cat("\n upper desired Fold Change : (Default is 1.75)")
inputupper<-arguments[8]
#inputupper<-readline()
if(inputupper=="TRUE") inputupper<-1.75
inputupper<-as.numeric(inputupper)


result.sample<-designSampleSize(data=quantData,numSample=inputnsample,numPep=inputnpep,numTran=inputntran,desiredFC=c(inputlower,inputupper),FDR=inputfdr,power=inputpower)
#result.sample<-designSampleSize(data=quantData,numSample=TRUE,numPep=2,numTran=3,desiredFC=c(1.25,1.75),FDR=0.05,power=0.8)

if(class(result.sample)!="try-error"){
	write.csv(result.sample,"SampleSizeCalculation.csv")
	cat("\n Saved the Sample Size Calculation. \n")
}else{
	cat("\n Error : Can't analyze. \n")
}

#=====================
# Function: designSampleSizePlots
# visualization for sample size calculation

designSampleSizePlots(data=result.sample, address="")
cat("\n Saved SampleSizePlot.pdf \n")

}

#=====================
tryCatch({runDSS()}, 
finally = {
cat("Finished.")
})