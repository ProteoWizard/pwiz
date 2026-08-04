# Necessary libraries
suppressPackageStartupMessages(library(LFQbench))

# Parse the variables out of the command line, if called from command line
command_args <- commandArgs(trailingOnly = FALSE)
script_args <- commandArgs(trailingOnly = TRUE)
# print(command_args)
file.arg.name <- "--file="

if (length(script_args) > 0 && length(grep(file.arg.name, command_args)) > 0) {
  script_file <- sub(file.arg.name, "", command_args[grep(file.arg.name, command_args)])
  script_dir <- dirname(script_file)
  if (script_dir != "") {
    script_dir <- paste(script_dir, "/", sep = "")
  }
  
  working_dir <- script_args[1]
  species_mix <- "HYE"
  if (length(script_args) > 1) {
    species_mix <- script_args[2]
  }
  print(working_dir)
} else {
  # otherwise, use this hardcoded working directory
  working_dir <- "D:/test/20191010_Bruker_diaPASEF/HYE/raw/HYE_diaPASEF_rp30hs_rt10_all"
  species_mix <- "HYE"
}

# Data sets being processed - can specify more in this structure in the future
speciesNamesHYE = c("HUMAN", "YEAST", "ECOLI")
speciesNamesYAS = c("YARROWIA", "ARTH", "STREP")
aSample = c( 65, 30, 5 )
bSample = c( 65, 20, 15 )
cSample = c( 65, 5, 30 )
dSample = c( 65, 15, 20 )
aFiles = c( "HYE A_200ng_100min_dia_Slot1-46_1_5000", "HYE A_200ng_100min_dia_Slot1-46_1_5001", "HYE A_200ng_100min_dia_Slot1-46_1_5002" )
bFiles = c( "HYE B_200ng_100min_dia_Slot1-47_1_5004", "HYE B_200ng_100min_dia_Slot1-47_1_5005", "HYE B_200ng_100min_dia_Slot1-47_1_5006" )
cFiles = c( "1922004-1_C_3ul_diaPASEF_Slot1-37_1_2072", "1922004-1_C_3ul_diaPASEF_Slot1-37_1_2073", "1922004-1_C_3ul_diaPASEF_Slot1-37_1_2074" )
dFiles = c( "1922004-2_D_3ul_diaPASEF_Slot1-38_1_2076", "1922004-2_D_3ul_diaPASEF_Slot1-38_1_2077", "1922004-2_D_3ul_diaPASEF_Slot1-38_1_2078" )
aRows = c("A1", "A2", "A3")
bRows = c("B1", "B2", "B3")
cRows = c("C1", "C2", "C3")
dRows = c("D1", "D2", "D3")
sampleCompHYE = data.frame( species = speciesNamesHYE, A = aSample, B = bSample)
sampleCompYAS = data.frame( species = speciesNamesYAS, C = cSample, D = dSample)
dataSetsHYE = data.frame( "diaPASEF_HYE" = c(aFiles, bFiles), row.names = c(aRows, bRows) )
dataSetsYAS = data.frame( "diaPASEF_YAS" = c(aFiles, bFiles), row.names = c(aRows, bRows) )

# Species tags from the FASTA sequence names
speciesTagsHYE = list( HUMAN = "_HUMAN", YEAST = "_YEAS", ECOLI = "_ECOLI" )
speciesTagsYAS = list( YARROWIA = "_YAR", ARABIDOPSIS = "_ARTH", STREP = "_STREP" )

# --- ProteoBench DIA-LFQ AIF (PXD028735) sample composition. ---------------
# Engineered ratios: Human 1:1, Yeast 2:1 (A heavy), E.coli 1:4 (B heavy)
# → log2(B/A): HUMAN 0, YEAST -1, ECOLI +2  (matches what TestDiannSearchTutorial asserts).
# Replicate file stems are stripped of the "LFQ_Orbitrap_AIF_" prefix that
# ImportResultsNameDlg removes by default in Skyline.
aSamplePB = c( 65, 30, 5 )
bSamplePB = c( 65, 15, 20 )
aFilesPB = c( "Condition_A_Sample_Alpha_01", "Condition_A_Sample_Alpha_02", "Condition_A_Sample_Alpha_03" )
bFilesPB = c( "Condition_B_Sample_Alpha_01", "Condition_B_Sample_Alpha_02", "Condition_B_Sample_Alpha_03" )
sampleCompHYE_PB = data.frame( species = speciesNamesHYE, A = aSamplePB, B = bSamplePB )
dataSetsHYE_PB = data.frame( "ProteoBench_AIF_HYE" = c(aFilesPB, bFilesPB),
                             row.names = c(aRows, bRows) )
# ProteoBench's HYE FASTA uses standard UniProt suffixes — "_YEAST", not Bruker's "_YEAS".
speciesTagsHYE_PB = list( HUMAN = "_HUMAN", YEAST = "_YEAST", ECOLI = "_ECOLI" )

if (species_mix == "HYE") {
  sampleComp = sampleCompHYE
  dataSets = dataSetsHYE
  speciesTags = speciesTagsHYE
} else if (species_mix == "HYE_PROTEOBENCH") {
  sampleComp = sampleCompHYE_PB
  dataSets = dataSetsHYE_PB
  speciesTags = speciesTagsHYE_PB
} else {
  sampleComp = sampleCompYAS
  dataSets = dataSetsYAS
  speciesTags = speciesTagsYAS
}

addSkylineConfiguration <- function(name, qcutoff) {
  FSWE.addSoftwareConfiguration(name,
                                input_format = "long", 
                                nastrings = "#N/A", 
                                input.extension = ".csv", 
                                quantitative.var = "TotalArea", 
                                filename.var = "FileName", 
                                protein.var = "ProteinName", 
                                sequence.mod.var = "PeptideModifiedSequence", 
                                charge.var = "PrecursorCharge", 
                                decoy.var = "IsDecoy", 
                                qvalue.var = "annotation_QValue", 
                                q_filter_threshold = qcutoff)
}


# Generate reports and plots

LFQbench.initConfiguration( SampleComposition = sampleComp )
LFQbench.setDataRootFolder( rootFolder = working_dir, createSubfolders = T )
#LFQbench.changeConfiguration(LogIntensityPlotRange = c (10, 25),
#                             LogRatioPlotRange = c (4 ,4))

FSWE.initConfiguration( injectionNames = dataSets, speciesTags = speciesTags )
addSkylineConfiguration("Skyline 0.0001", 0.0001)
FSWE.generateReports("SWATHbenchmark.csv",
                     softwareSource = "Skyline",
                     keep_original_names = T,
                     singleHits = F,
                     plotHistogram = T,
                     plotHistNAs = T,
                     reportSequences = F)

hye.res = LFQbench.batchProcessRootFolder()
