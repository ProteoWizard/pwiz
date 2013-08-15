runGC<-function() {

cat("\n\n =======================================")
cat("\n ** Loading the required library..... \n")

# load the library
library(MSstats)

# Input data
arguments<-commandArgs(trailingOnly=TRUE);

raw<-read.csv(arguments[1])
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
# Function: groupComparison
# generate testing results of protein inferences across concentrations

## cat("\n\n =======================================")
## at("\n ** Let's compare Groups..... \n")


## input from groupComparison
# comparison matrix
# namelevel<-levels(quantData$GROUP_ORIGINAL)[1]

# for(i in 2:length(levels(quantData$GROUP_ORIGINAL))){
	# namelevel<-paste(namelevel, levels(quantData$GROUP_ORIGINAL)[i], sep=", ")
# }

## cat(paste("\n\n Groups are (",namelevel, ")\n",sep=""))

## first, number of comparisons
## cat("\n How many comparisons do you want? : ")
numcomparison<-1

## then, what comparisons?

alllevel<-levels(quantData$GROUP_ORIGINAL)

comparison<-NULL

## get length of command line argument array
len<-length(arguments)

for(k in 1:numcomparison){

## cat(paste("\n Comparison ",k," : Enter constants for each group \n",sep=""))

temp<-NULL

for(i in 1:length(alllevel)){
## cat(paste(" ", alllevel[i], " : ",sep=""))
inputtemp<-arguments[i+1]
#inputtemp<-readline()
temp<-c(temp,as.numeric(inputtemp))
}

comparison<-rbind(comparison, matrix(temp,nrow=1))

## cat(" Name of this comparison : ")
inputrowname<-arguments[len-4]
#inputrowname<-readline()
row.names(comparison)[k]<-inputrowname

}

## input for options
## cat("\n\n ** Enter your choices for model..... \n")

# labeled or label-free?
## cat("\n Label-free(FALSE-F)? or Labeled(TRUE-T)?. Default is TRUE : ")
inputlabel<-arguments[len-3]
#inputlabel<-readline()


if(inputlabel=="" | inputlabel=="T") inputlabel<-"TRUE"
if(inputlabel=="F") inputlabel<-"FALSE"
if(inputlabel!="TRUE" & inputlabel!="FALSE"){
	## cat("\n Wrong input. will use the default=TRUE.\n")
	inputlabel<-"TRUE"
}


# scope of biological replicate?
## cat("\n Scope of Biological Replicate (expanded-E/restricted-R)? Default is R : ")
inputbio<-arguments[len-2]
#inputbio<-readline()

if(inputbio=="" | inputbio=="R") inputbio<-"restricted"
if(inputbio=="E") inputbio<-"expanded"
if(inputbio!="expanded" & inputbio!="restricted"){
	##  cat("\n Wrong input. will use the default=restricted. \n")
	inputbio<-"restricted" 
}


# scope of technical replicate?
# cat("\n Scope of Technical Replicate (expanded-E/restricted-R)? Default is E : ")
inputtech<-arguments[len-1]
#inputtech<-readline()

if(inputtech=="" | inputtech=="E") inputtech<-"expanded"
if(inputtech=="R") inputtech<-"restricted"
if(inputtech!="expanded" & inputtech!="restricted"){
	## cat("\n Wrong input. will use the default=expanded. \n")
	inputtech<-"expanded" 
}

# interference or not?
# cat("\n Contain inference transitions (TRUE) or not (FALSE)? : ")
# cat(arguments[len])
inputinfer<-arguments[len]
#inputinfer<-readline()

if(inputinfer=="" | inputinfer=="T") inputinfer<-"TRUE"
if(inputinfer=="F") inputinfer<-"FALSE"
if(inputinfer!="TRUE" & inputinfer!="FALSE"){
	## cat("\n Wrong input. will use the default=TRUE.\n")
	inputinfer<-"TRUE"
}

## then testing with inputs from users
cat("\n Starting comparison... \n")

resultComparison<-groupComparison(contrast.matrix=comparison,data=quantData,label=inputlabel,scopeOfBioReplication=inputbio, scopeOfTechReplication=inputtech, interference=inputinfer)

if(class(resultComparison)!="try-error"){
	write.csv(resultComparison,"TestingResult.csv")
	cat("\n Saved the testing result. \n")
}else{
	cat("\n Error : Can't analyze. \n")
}

#=====================
# Function: groupComparisonPlots
# visualization for testing results

cat("\n\n =======================================")
cat("\n ** Visualizing testing results..... \n")


# Visualization 1: Volcano plot
# default setup: FDR cutoff = 0.05; fold change cutoff = NA
groupComparisonPlots(data=resultComparison,type="VolcanoPlot",address="")
cat("\n Saved VolcanoPlot.pdf \n")

# Visualization 2: Heatmap (required more than one comparisons)
# if(numcomparison>1){
  # groupComparisonPlots(data=resultComparison,type="Heatmap",address="")
  # cat("\n Saved Heatmap.pdf \n")
# } else{
  # cat("\n No Heatmap. Need more than 1 comparison for Heatmap. \n")
# }

# Visualization 3: Comparison plot
groupComparisonPlots(data=resultComparison,type="ComparisonPlot",address="")
cat("\n Saved ComparisonPlot.pdf \n")

}

tryCatch({runGC()}, 
finally = {
cat("Finished.")
})