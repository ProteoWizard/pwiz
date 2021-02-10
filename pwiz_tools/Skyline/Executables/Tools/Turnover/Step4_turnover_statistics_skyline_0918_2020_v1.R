#Written by Cameron Wehrfritz
#and Natan Basisty, PhD
#Schilling Lab, Buck Institute for Research on Aging
#Novato, California, USA
#March, 2020
#updated: February 09, 2021

# PROTEIN TURNOVER ANALYSIS
# STEP 4:
# Linear model of log(Percent.Newly.Synthesized) by timepoints and cohorts and their interaction
#
# OUTPUT:
# i. Data table with statistics
# ii. PDF of linear regression plots: Percent Newly Synthesized vs. Time

### Begin Script Step 4 ###


#------------------------------------------------------------------------------------
# START CODE FOR RUNNING IN RSTUDIO (comment out if running from TurnoveR)
#------------------------------------------------------------------------------------

# 
# #------------------------------------------------------------------------------------
# # set working directory
# #setwd("/Volumes/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Cameron_development/Step4") # MAC
# setwd("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Cameron_development/Step4") # PC
# #------------------------------------------------------------------------------------
# 
# 
# #------------------------------------------------------------------------------------
# # PACKAGES #
# packages = c("tidyr", "dplyr", "reshape2", "seqinr", "ggplot2", "coefplot", "plyr")
# 
# package.check <- lapply(packages, FUN = function(x) {
#   if (!require(x, character.only = TRUE)) {
#     install.packages(x, dependencies = TRUE)
#     library(x, character.only = TRUE)
#   }
# })
# #------------------------------------------------------------------------------------
# 
# 
# #------------------------------------------------------------------------------------
# # LOAD DATA
# 
# # single leucine data set (1 leucine)
# data.s <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Step0_Data_Output_Skyline_singleleucine_peps_test.csv", stringsAsFactors = F) # WINDOWS
# 
# # multiple leucine data set (2,3,4 leucines)
# data.m <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Step0_Data_Output_Skyline_multileucine_peps_test.csv", stringsAsFactors = F) # WINDOWS
# 
# # medians of x-intercepts by cohort from step 3
# # df.x.int.medians <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Table_step3_xintercepts.csv", stringsAsFactors = F) # WINDOWS
# df.x.int.medians <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Cameron_development/Step2/Table_step2_xintercepts_0208_2021_v1.csv", stringsAsFactors = F) # WINDOWS - cameron_development
# #------------------------------------------------------------------------------------
# 
# 
# # reference cohort
# reference.cohort <- "OCon" # this should be assigned by the user # TO DO

#------------------------------------------------------------------------------------
# END CODE FOR RUNNING IN RSTUDIO
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Combine Single Leucine and Multiple Leucine data sets together for modeling

df <- data.m %>%
  bind_rows(data.s) %>% # combine data; retains all columns, fills missing columns in with NA
  filter(Perc.New.Synth>0) # filter out data with negative percent newly synthesized value
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# PREP FOR MODEL 

# cohorts
cohorts <- unique(df$Cohort)

# make a cohort vector for looping through all comparisons
cohorts.loop <- cohorts[!cohorts==reference.cohort] # keep all cohorts except for the reference cohort

# proteins
prots <- unique(df$Protein.Accession)

# genes
genes <- unique(df$Protein.Gene)

# time points
time <- sort(unique(df$Timepoint))
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# create modified time 
# by subtracting the median x-intercept time (shifting left toward the origin)
# unless x-intercepts are negative, then modified.time is simply the same as time
for(i in cohorts){
  if(all(df.x.int.medians %>% pull(Median.x.intercept)>0, df.x.int.medians %>% pull(Median.x.intercept)<min(time))){ # check if all median x-intercepts are positive and less than minimum timepoint
    df$Modified.Time[df$Cohort==i] <- df %>% filter(Cohort==i) %>% pull(Timepoint) - df.x.int.medians %>% filter(Cohort==i) %>% pull(Median.x.intercept) # modify timepoints by translating left by respective median x-intercept
  } else { 
    df$Modified.Time[df$Cohort==i] <- df %>% filter(Cohort==i) %>% pull(Timepoint) # else do not modify
  }
} # end for
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# LINEAR MODELLING

# initialize data frame to write out results from linear model 
# first figure out column names since the number of columns is built into col size
col.names <- c("Protein.Accession", "Gene", "Comparison", "No.Peptides", "No.Points", "Interaction" , "Std.Error",  "t.value", "Unadj.P", "Qvalue", "DF",
               "Slope.Numerator", "Slope.Denominator", "Half.Life.Numerator", "Half.Life.Denominator")
df.model.output <- data.frame(matrix(nrow = length(cohorts.loop)*length(prots), ncol = length(col.names)))
names(df.model.output) <- col.names

# Loop
row.index <- 1 # counter
for(i in prots){
  
  # subset data for protein 
  data.protein.loop <- subset(df, Protein.Accession == i) 
  
  # subset data from reference cohort - do this prior to the cohorts loop below
  data.ref <- subset(data.protein.loop, Cohort==reference.cohort) # ref refers to the user defined reference cohort
  
  # loop through cohorts - in order to generate each comparison to the reference cohort
  for(j in cohorts.loop){
    
    # subset data for variable cohort and reference cohort; for use in combined linear model
    data.cohort.loop <- subset(data.protein.loop, Cohort == j | Cohort == reference.cohort )
    
    # subset data for variable cohort
    data.var <- subset(data.protein.loop, Cohort==j) # var refers to the variable cohorts, which to compare against the user defined reference cohort
    
    # create name of comparison
    comparison <- paste(j, "vs", reference.cohort, sep=" ") # variable cohort vs reference cohort
  
    # write out comparison name
    df.model.output[row.index, colnames(df.model.output)=="Comparison"] <- comparison
    
    # write out Protein.Accession
    df.model.output[row.index, colnames(df.model.output)=="Protein.Accession"] <- i 
    
    # write out Gene name
    df.model.output[row.index, colnames(df.model.output)=="Gene"] <- data.protein.loop %>% pull(Protein.Gene) %>% unique() 
    
    # write out number of unique peptides
    df.model.output[row.index, colnames(df.model.output)=="No.Peptides"] <- data.cohort.loop %>% pull(Modified.Peptide.Seq) %>% unique() %>% length()
    
    # write out number of data points
    df.model.output[row.index, colnames(df.model.output)=="No.Points"] <- nrow(data.cohort.loop)

    # LINEAR MODEL #
    if( nrow(data.cohort.loop) >= 1.5*length(time) ){ # quick assessment: if the number of data points is greater than length of time points then the model should hopefully converge
      tryCatch(
        expr={ 
          # Model
          # model <- lm( formula = log(Perc.New.Synth) ~ Cohort*Modified.Time, data = data.cohort.loop)
          model <- lm(log(1-Perc.New.Synth) ~ 0 + Cohort*Modified.Time, data = data.cohort.loop %>% filter(Perc.New.Synth<1)) # this model matches the model used in step 3
          
          # # p.value rounded; for use in plot legend
          # p.value <- summary(model)$coef[3,4] %>% round(., digits=4) # pvalue of iteraction term
          
          # write out statistics from combined model
          df.model.output[row.index, c(6:8)] <- summary(model)$coef[3, 1:3] %>% round(., digits=4) # model statistics: estimate, standard error, t value
          df.model.output[row.index, 9] <- summary(model)$coef[3, 4] # p-value from combined linear model; not rounded, so we can sort by this variable
          df.model.output[row.index, colnames(df.model.output)=="DF"] <- summary(model)$df[2] # degrees of freedom

          # model reference cohort against its timepoints
          model.ref <- lm( log(1-Perc.New.Synth) ~ 0 + Modified.Time, data = data.ref %>% filter(Perc.New.Synth<1)) # this model matches the model used in step 3
          # model variable cohort against its timepoints
          model.var <- lm( log(1-Perc.New.Synth) ~ 0 + Modified.Time, data = data.var %>% filter(Perc.New.Synth<1)) # this model matches the model used in step 3
          
          # write out slopes and half-lives from individual linear models
          # variable cohort
          df.model.output[row.index, "Slope.Numerator"] <- -summary(model.var)$coef[1] %>% round(., digits=4) # slope of linear model
          df.model.output[row.index, "Half.Life.Numerator"] <- -log(2)/summary(model.var)$coef[1] %>% round(., digits=4) # half life
          # reference cohort
          df.model.output[row.index, "Slope.Denominator"] <- -summary(model.ref)$coef[1] %>% round(., digits=4) # slope of linear model
          df.model.output[row.index, "Half.Life.Denominator"] <- -log(2)/summary(model.ref)$coef[1] %>% round(., digits=4) # half life
        },
        error=function(e){
          message("Caught an error!")
          print(e)
        },
        warning=function(w){
          message("Caught a warning!")
          print(w)
        },
        finally={
          message("All done, quitting.")
        }
      ) # end tryCatch
      
    } else{ # otherwise there may not be enough data points to run the model, then write out NA and continue looping
      df.model.output[row.index, c(6:9)] <- NA
      df.model.output[row.index, colnames(df.model.output)=="DF"] <- NA
    } # end else
    # increment row.index counter before iterating through cohort loop
    row.index <- row.index + 1
  } # end for; cohort level
} # end for; protein level
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# clean model output
df.model.output <- df.model.output %>%
  select(-Qvalue) %>%  # Qvalue package is not working, remove Qvalue variable
  arrange(Unadj.P) # arrange best Pvalue top down
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# write out model output
write.csv(df.model.output, file = "Table_step4_output_date.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


# END 