#Written by Cameron Wehrfritz
#and Natan Basisty, PhD
#Schilling Lab, Buck Institute for Research on Aging
#Novato, California, USA
#March, 2020
#updated: February 08, 2021

# PROTEIN TURNOVER ANALYSIS
# STEP 3:
# NON LINEAR FIT OF TURNOVER, THROUGH THE ORIGIN with single parameter: rate of change
#
# OUTPUT: 
# i. PDF of non-linear regression plots: Percent Newly Synthesized vs. Time
# ii. Data table of non-linear model statistics


### Begin Script Step 3 ###


#------------------------------------------------------------------------------------
# START CODE FOR RUNNING IN RSTUDIO (comment out if running from TurnoveR)
#------------------------------------------------------------------------------------

# 
# #------------------------------------------------------------------------------------
# # set working directory
# #setwd("/Volumes/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Cameron_development/Step3") # MAC
# setwd("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Cameron_development/Step3") # WINDOWS
# #------------------------------------------------------------------------------------
# 
# 
# #------------------------------------------------------------------------------------
# # PACKAGES
# packages = c("tidyr", "dplyr", "forcats", "reshape2", "seqinr", "ggplot2", "coefplot", "plyr", "qvalue")
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
# data.s <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Step0_Data_Output_Skyline_singleleucine_peps_test.csv", stringsAsFactors = F) # PC
# 
# # multiple leucine data set (2,3,4 leucines)
# data.m <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Step0_Data_Output_Skyline_multileucine_peps_test.csv", stringsAsFactors = F) # PC
# 
# # medians of x-intercepts by cohort from Step 2 
# # df.x.int.medians <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Table_step3_xintercepts.csv", stringsAsFactors = F) # PC
# df.x.int.medians <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Cameron_development/Step2/Table_step2_xintercepts_0208_2021_v1.csv", stringsAsFactors = F) # PC - cameron_development
# #------------------------------------------------------------------------------------
# 
# # reference cohort
# reference.cohort <- "OCon" # this should be assigned by the user # TO DO


#------------------------------------------------------------------------------------
# END CODE FOR RUNNING IN RSTUDIO
#------------------------------------------------------------------------------------



#------------------------------------------------------------------------------------
# FILTER

# filter multiple leucine data set by average turnover score
# between [0,1) where 1 is most stringent
# the default should be 0
ATS.threshold <- 0 # average turnover score value, used for filtering data

data.m <- data.m %>%
  filter(Avg.Turnover.Score>ATS.threshold) 
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Combine Single Leucine and Multiple Leucine data sets together for modeling
df <- data.m %>%
  bind_rows(data.s) # retains all columns; fills missing columns in with NA 
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Remove observations with negative percent.new.synthesized values
df <- df %>%
  filter(Perc.New.Synth>0)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# PREP FOR MODEL

# cohorts
cohorts <- unique(df$Cohort)

# make a cohort vector for looping through all comparisons
cohorts.loop <- cohorts[!cohorts==reference.cohort] # keep all cohorts except for the reference cohort

# proteins
prots <- unique(df$Protein.Accession)

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
# NLS MODEL 

# initialize data frame to write out results from nonlinear model 
# first figure out your column names since the number of columns is built off that
# row size = (number of cohorts) * (number of proteins) ... per cohort, which is hopefully constant
# col size = number of names in col.names
col.names <- c("Protein.Accession", "Gene", "Cohort", "No.Peptides", "No.Points", "b", "Pvalue.b", "Qvalue", "Res.Std.Error")
df.model.output <- data.frame(matrix(nrow = 2*length(cohorts.loop)*length(prots), ncol = length(col.names)))
names(df.model.output) <- col.names

#Initiate PDF
pdf(file="Turnover_step3_plots_date.pdf")
par(mfrow=c(2,3))

row.index <- 1 
for(i in seq_along(unique(df$Protein.Accession))){
  print(i)
  
  # subset combined data for cohort and protein 
  data.protein.loop <- subset(df, Protein.Accession == prots[i]) 
  
  # subset reference data - do this before cohorts loop below
  data.ref <- subset(data.protein.loop, Cohort==reference.cohort) # ref will always be the user defined reference cohort
  # subset variables Time and Percent.Newly.Synthesized to fit with model
  fit.ref <- subset(data.ref, select = c("Modified.Time", "Perc.New.Synth")) %>% 
    dplyr::rename(x=Modified.Time, y=Perc.New.Synth) %>% # rename to x and y for simiplicity in model
    dplyr::arrange(x) # ascending order by time.point
  
  # loop through cohorts 
  for(j in cohorts.loop){
    
    # subset variable cohort data
    data.var <- subset(data.protein.loop, Cohort==j)
    
    # subset variables Time and Percent.Newly.Synthesized to fit with model
    fit.var <- subset(data.var, select = c("Modified.Time", "Perc.New.Synth")) %>%
      dplyr::rename(x=Modified.Time, y=Perc.New.Synth) %>% # rename to x and y for simiplicity in model
      dplyr::arrange(x) # ascending order by time.point
  
    if(nrow(fit.ref)>length(time) & nrow(fit.var)>length(time) ){ # quick assessment: if the number of data points for each cohort is greater than length of time points then the model should hopefully converge
      tryCatch(
        expr={
          # Model:
          
          # first set models to NA, so that the models from the previous iteration aren't plotted (accidentally) when the model does not successfully run on the current iteration
          model.ref <- NA
          model.var <- NA
          
          # exponential model through the origin
          model.ref <- nls( y ~ I(1-exp(b*x)), data = fit.ref, start = c(b = -0.5), trace = T ) # model reference cohort
          model.var <- nls( y ~ I(1-exp(b*x)), data = fit.var, start= c(b = -0.5), trace = T ) # model variable cohort
          
          # write out results from models:
          
          # REFERENCE COHORT:
          # protein
          df.model.output[row.index, "Protein.Accession"] <- prots[i]
          
          # gene
          df.model.output[row.index, "Gene"] <- data.ref %>% pull(Protein.Gene) %>% unique()
          
          # cohort
          df.model.output[row.index, "Cohort"] <- data.ref %>% pull(Cohort) %>% unique()
          
          # number of unique peptides
          df.model.output[row.index, "No.Peptides"] <- data.ref %>% pull(Modified.Peptide.Seq) %>% unique() %>% length()
          
          # number of data points
          df.model.output[row.index, "No.Points"] <- nrow(data.ref)
          
          # parameter b
          df.model.output[row.index, "b"] <- summary(model.ref)$coef["b", "Estimate"] %>% round(., digits=4)
          
          # p-value for parameter b
          df.model.output[row.index, "Pvalue.b"] <- summary(model.ref)$coef["b", "Pr(>|t|)"] # %>% round(., digits=4)
          
          # residual standard error
          df.model.output[row.index, "Res.Std.Error"] <- summary(model.ref)$sigma %>% round(., digits=4)
          
          
          # VARIABLE COHORT:
          # protein
          df.model.output[row.index+1, "Protein.Accession"] <- prots[i]
          
          # gene
          df.model.output[row.index+1, "Gene"] <- data.var %>% pull(Protein.Gene) %>% unique()
          
          # cohort
          df.model.output[row.index+1, "Cohort"] <- data.var %>% pull(Cohort) %>% unique()
          
          # number of unique peptides
          df.model.output[row.index+1, "No.Peptides"] <- data.var %>% pull(Modified.Peptide.Seq) %>% unique() %>% length()
          
          # number of data points
          df.model.output[row.index+1, "No.Points"] <- nrow(data.var)
          
          # parameter b
          df.model.output[row.index+1, "b"] <- summary(model.var)$coef["b", "Estimate"] %>% round(., digits=4)
          
          # p-value for parameter b
          df.model.output[row.index+1, "Pvalue.b"] <- summary(model.var)$coef["b", "Pr(>|t|)"] # %>% round(., digits=4)
          
          # residual standard error
          df.model.output[row.index+1, "Res.Std.Error"] <- summary(model.var)$sigma %>% round(., digits=4)
  
          # Now do combined model with both cohorts present - in order to get p-value statistic for plotting in legend
          model.combined <- lm( formula = log(Perc.New.Synth) ~ Cohort*Modified.Time, data = data.protein.loop ) # combined model
          p.value <- summary(model.combined)$coef[3,4] %>% round(., digits=4) # p-value from combined model, rounding to 4 decimals
      
          # Plot
          color.ref <- "blue" # set reference cohort color to blue
          color.var <- "red" # set variable cohort color to red
          # plot (x,y) data points
          plot(fit.ref, xlab = "Time (Days)", ylab = "Percent Newly Synthesized", xlim = c(0, max(time)), ylim = c(0,1), main=paste(unique(data.var[, "Protein.Accession"]), unique(data.var[, "Protein.Gene"])), pch=2, col=color.ref) # blue triangles
          points(fit.var, pch=1, col=color.var) # variable cohort data points are red circles
          # plot model curves
          xg <- seq(from = 0, to = max(time), length = 3*max(time)) # create vector of inputs for predict function to use for graphing below - used for generating model curves
          lines(xg, predict(model.ref, list(x = xg)), col = color.ref) # reference cohort model is blue curve
          lines(xg, predict(model.var, list(x = xg)), col = color.var) # variable cohort model is red curve
          # legend
          legend("top", inset = 0.01, legend = c(unique(data.ref[, "Cohort"]), unique(data.var[, "Cohort"])), ncol = 2, cex = 0.8, lty = 1, col = c(color.ref, color.var)) # color code
          leg_pval <- paste("p =", p.value, sep = " ")
          legend("top", inset = 0.11, legend = leg_pval, cex = 0.6 ) # combined model pvalue
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
    } else {
      print("skip") # else if the there are not enough data points then the model will probably not converge ... print "skip", write out some basic information and continue looping
      
      # Write out basic information from the loop - even though it was not modeled:
      
      # Reference cohort:
      # protein
      df.model.output[row.index, "Protein.Accession"] <- prots[i] # protein
      
      # gene
      df.model.output[row.index, "Gene"] <- ifelse(length(unique(data.ref[, "Protein.Gene"]))==0, NA, unique(data.ref[, "Protein.Gene"])) # gene
      
      # cohort
      df.model.output[ row.index, "Cohort"] <- ifelse(length(unique(data.ref[, "Cohort"]))==0, NA, unique(data.ref[, "Cohort"])) # reference cohort
      
      # number of peptides
      df.model.output[row.index, "No.Peptides"] <- ifelse(length(unique(data.ref[, "Cohort"]))==0, NA, length(unique(data.ref$Modified.Peptide.Seq))) # number of peptides in reference cohort
      
      # number of points
      df.model.output[row.index, "No.Points"] <- ifelse(length(unique(data.ref[, "Cohort"]))==0, NA, no.points.a) # number of points reference cohort
      
      
      # Variable cohort:
      # protein
      df.model.output[row.index +1, "Protein.Accession"] <- prots[i] # Protein
      
      # gene
      df.model.output[row.index +1, "Gene"] <- ifelse(length(unique(data.var[, "Protein.Gene"]))==0, NA, unique(data.var[, "Protein.Gene"])) # gene
      
      # cohort
      df.model.output[ row.index +1, "Cohort"] <- ifelse(length(unique(data.var[, "Cohort"]))==0, NA, unique(data.var[, "Cohort"])) # variable cohort
      
      # number of peptides
      df.model.output[row.index +1, "No.Peptides"] <- ifelse(length(unique(data.var[, "Cohort"]))==0, NA, length(unique(data.var$Modified.Peptide.Seq))) # number of Modified.Peptide.Seq in variable cohort
      
      # number of points
      df.model.output[row.index +1, "No.Points"] <-  ifelse(length(unique(data.var[, "Cohort"]))==0, NA, no.points.b) # number of points in variable cohort
      
    } # end trycatch
    # increase row.index counter by 2 each cycle, since we are writing out data for both cohorts (reference and variable) during each iteration, on separate rows
    row.index <- row.index + 2
  } # end for; cohort level
} # end for; protein level

# timestamp
graphics.off()
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Clean model output

# Qvalue package isn't always working...
# # calculate Qvalues
# qobj <- qvalue(p = df.model.output$Pvalue.b, pi0 = 1)
# qvals <- qobj$qvalues
# df.model.output$Qvalue <- round( qvals, 4)

df.model.output <- df.model.output %>%
  select(-Qvalue) %>% # since Qvalue isn't working let's get rid of the Qvalue variable
  na.omit() %>% # drop rows that did not run the model
  unique() %>% # get rid of duplicated rows from modeling the reference cohort multiple times
  arrange(Pvalue.b)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# write out model output
write.csv(df.model.output, file = "Table_step3_output.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Filter
# pvalue of fit < 0.05
# rate of change < 0
df.model.output.filtered <- df.model.output %>%
  filter(b<0 & Pvalue.b<0.05)

# write out filtered df
write.csv(df.model.output.filtered, "Table_step3_output_filtered.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


# END