
#------------------------------------------------------------------------------------
# PACKAGES #

packages = c("tidyr", "plyr", "dplyr", "reshape2", "seqinr", "ggplot2", "coefplot", 
             "forcats", "tibble", "stringr", "purrr", "gridExtra", "pracma", "hablar")  

invisible(lapply(packages, library, character.only = TRUE)) # add imported packages to library

#------------------------------------------------------------------------------------





#------------------------------------------------------------------------------------
# LOAD ARGUMENTS FROM SKYLINE #

arguments <- commandArgs(trailingOnly=TRUE)
cat(length (arguments))
if ( length (arguments) != 6)
  # expected arguments not present -- error
  stop ("USAGE: R --slave --no-save --args '<textbox><textbox><textbox><textbox>'")
for (i in 1:6) {
  arg <- arguments [i]
  # remove leading and trailing blanks
  arg <- gsub ("^ *", "", arg)
  arg <- gsub (" *$", "", arg)
  # remove any embedded quotation marks
  arg <- gsub ("['\'\"]", "", arg)
  #report file is brought in as an argument, this is specified in TestArgsCollector.properties
  if (i==1) filepath <<- arg
  if (i==2) tool.dir <<- arg
  if (i==3) diet.enrichment <- as.numeric (arg) # Leucine percent enrichment in diet
  if (i==4) min.avg.turnover.score <<- as.numeric (arg)
  if (i==5) min.isotope.dot.product <<- as.numeric (arg)
  if (i==6) folder.name <- arg
}

dir.create(file.path(getwd(), folder.name), showWarnings = FALSE) # Create folder for script output
setwd(file.path(getwd(), folder.name))
#------------------------------------------------------------------------------------



#------------------------------------------------------------------------------------
# SET CONSTANT ARGUMENTS #
min.abundance <- 0.0001 #as.numeric (arg) # minimum abundance
resolution <- 0.1 #as.numeric (arg) # resolution for distinguishing peaks
p.tolerance <- 0.05 #as.numeric (arg) # tolerance for combining masses in observed data

#------------------------------------------------------------------------------------



#------------------------------------------------------------------------------------
# LOAD DATA #

df <- read.csv(file = filepath, stringsAsFactors = F)


if (is.null(df$Replicate.Name)) {
  # rename df columns with periods between words
  df <- rename_with(df, .fn = function(vector){
    return(c("Protein", "Replicate.Name", "Protein.Description", "Protein.Accession", "Protein.Gene", "Peptide", 
             "File.Name", "Timepoint", "Condition", "Precursor.Charge", "Precursor.Mz", "Molecule.Formula", 
             "Precursor.Neutral.Mass", "Modified.Sequence", "Is.Decoy", "Detection.Q.Value", "Total.Area.Ms1", 
             "Isotope.Dot.Product", "Product.Mz", "Product.Charge", "Fragment.Ion", "Isotope.Dist.Index", 
             "Isotope.Dist.Rank", "Isotope.Dist.Proportion", "Fragment.Ion.Type", "Area"))
    
  })
}
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# RUN ALL STEPS #
source(paste(tool.dir, "Step1.R", sep="/"))
source(paste(tool.dir, "Step2.R", sep="/"))
source(paste(tool.dir, "Step3.R", sep="/"))
source(paste(tool.dir, "Step4.R", sep="/"))

#------------------------------------------------------------------------------------




cat("\n---------------------------------------------------------------------------------------")
cat(" ALL COMPLETED ")
cat("---------------------------------------------------------------------------------------\n\n")
cat("Output at: ")
cat(getwd())







