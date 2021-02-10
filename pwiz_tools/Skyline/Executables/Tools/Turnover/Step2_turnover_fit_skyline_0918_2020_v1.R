#Written by Cameron Wehrfritz
#and Natan Basisty, PhD
#Schilling Lab, Buck Institute for Research on Aging
#Novato, California, USA
#March, 2020
#updated: February 09, 2021

# PROTEIN TURNOVER ANALYSIS
# STEP 2:
# NON LINEAR FIT OF TURNOVER 
# CALCULATE X-INTERCEPTS
#
# OUTPUT: 
# i. PDF of non-linear regression plots: Percent Newly Synthesized vs. Time
# ii. Data table of non-linear model statistics
# iii. Average x-intercepts by cohort


### Begin Script Step 2 ###


#------------------------------------------------------------------------------------
# START CODE FOR RUNNING IN RSTUDIO (comment out if running from TurnoveR)
#------------------------------------------------------------------------------------
# 
# 
# #------------------------------------------------------------------------------------
# # set working directory
# #setwd("/Volumes/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Cameron_development/")
# setwd("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Cameron_development/") #VPN
# #------------------------------------------------------------------------------------
# 
# 
# #------------------------------------------------------------------------------------
# # PACKAGES
# packages = c("dplyr", "reshape2", "seqinr", "ggplot2", "coefplot", "plyr")
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
# # data.s <- read.csv("/Volumes/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Step0_Data_Output_Skyline_singleleucine_peps_test.csv", stringsAsFactors = F) # MAC
# data.s <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Step0_Data_Output_Skyline_singleleucine_peps_test.csv", stringsAsFactors = F) # PC
# 
# # multiple leucine data set (2,3,4 leucines)
# # data.m <- read.csv("/Volumes/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Step0_Data_Output_Skyline_multileucine_peps_test.csv", stringsAsFactors = F) # MAC
# data.m <- read.csv("//bigrock/GibsonLab/users/Cameron/2020_0814_Skyline_Turnover_Tool/Turnover_R_scripts/Step0_Data_Output_Skyline_multileucine_peps_test.csv", stringsAsFactors = F) # PC
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
# NLS MODEL

# initialize data frame to write out results from nonlinear model 
# first figure out your column names since the number of columns is built off that
# row size = (number of cohorts) * (number of proteins) ... per cohort, which is hopefully constant
# col size = number of names in col.names
col.names <- c("Protein.Accession", "Gene", "Cohort", "No.Peptides", "No.Points", "a", "Pvalue.a", "b", "Pvalue.b", "Qvalue", "Res.Std.Error", "X.Intercept")
df.model.output <- data.frame(matrix(nrow = 2*length(cohorts.loop)*length(prots), ncol = length(col.names)))
names(df.model.output) <- col.names

#Initiate PDF
pdf(file="Turnover_step2_plots_date.pdf")
par(mfrow=c(2,3))

row.index <- 1 
for(i in seq_along(unique(df$Protein.Accession))){
  print(i)
  
  # subset combined data for cohort and protein 
  data.protein.loop <- subset(df, Protein.Accession == prots[i]) 
  
  # subset reference data - do this before cohorts loop below
  data.ref <- subset(data.protein.loop, Cohort==reference.cohort) # ref will always be the user defined reference cohort
  # subset variables Time and Percent.Newly.Synthesized to fit with model
  fit.ref <- subset(data.ref, select = c("Timepoint", "Perc.New.Synth")) %>% 
    dplyr::rename(x=Timepoint, y=Perc.New.Synth) %>% # rename to x and y for simiplicity in model
    dplyr::arrange(x) # ascending order by time.point
  
  # loop through cohorts 
  for(j in cohorts.loop){
    
    # subset variable cohort data
    data.var <- subset(data.protein.loop, Cohort==j)
    
    # subset variables Time and Percent.Newly.Synthesized to fit with model
    fit.var <- subset(data.var, select = c("Timepoint", "Perc.New.Synth")) %>%
      dplyr::rename(x=Timepoint, y=Perc.New.Synth) %>% # rename to x and y for simiplicity in model
      dplyr::arrange(x) # ascending order by time.point
    
    if( nrow(fit.ref)>length(time) & nrow(fit.var)>length(time) ){ # quick assessment: if the number of data points for each cohort is greater than length of time points then the model should hopefully converge
      tryCatch(
        expr={
          # MODEL:
          
          # first set models to NA, so the ones from the previous iteration aren't plotted when the model does not successfully run on the current iteration
          m.ref <- NA
          m.var <- NA
          
          # linearizing nls first to get starting values
          fm0.ref <- nls(log(y) ~ log(I(1-a*exp(b*x))), data = fit.ref, start = c(a = 1, b = -0.5)) # a=1 goes through origin, and b is the rate of change of the exponential, which is used in half-life calculation
          m.ref <- nls( y ~ I(1-a*exp(b*x)), data = fit.ref, start=coef(fm0.ref), trace = T ) # model with starting values
          
          # linearizing nls first to get starting values
          fm0.var <- nls(log(y) ~ log(I(1-a*exp(b*x))), data = fit.var, start = c(a = 1, b = -0.5)) # a=1 goes through origin, and b is the rate of change of the exponential, which is used in half-life calculation
          m.var <- nls( y ~ I(1-a*exp(b*x)), data = fit.var, start=coef(fm0.var), trace = T ) # model with starting values
          
          # write out model results 
          
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

          # parameter a
          df.model.output[row.index, "a"] <- summary(m.ref)$coef["a", "Estimate"] %>% round(., digits=4)

          # p-value for parameter a
          df.model.output[row.index, "Pvalue.a"] <- summary(m.ref)$coef["a", "Pr(>|t|)"] #%>% round(., digits=4)

          # parameter b
          df.model.output[row.index, "b"] <- summary(m.ref)$coef["b", "Estimate"] %>% round(., digits=4)

          # p-value for parameter b
          df.model.output[row.index, "Pvalue.b"] <- summary(m.ref)$coef["b", "Pr(>|t|)"] #%>% round(., digits=4)

          # residual standard error
          df.model.output[row.index, "Res.Std.Error"] <- summary(m.ref)$sigma %>% round(., digits=4)

          # calculate and write out x-intercept
          df.model.output[row.index, "X.Intercept"] <- (1/summary(m.ref)$coef["b", "Estimate"])*log(1/summary(m.ref)$coef["a", "Estimate"]) # x intercept: x=ln(1/a)*b
          
          
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
          
          # parameter a
          df.model.output[row.index+1, "a"] <- summary(m.var)$coef["a", "Estimate"] %>% round(., digits=4)
          
          # p-value for parameter a
          df.model.output[row.index+1, "Pvalue.a"] <- summary(m.var)$coef["a", "Pr(>|t|)"] #%>% round(., digits=4)
          
          # parameter b
          df.model.output[row.index+1, "b"] <- summary(m.var)$coef["b", "Estimate"] %>% round(., digits=4)
          
          # p-value for parameter b
          df.model.output[row.index+1, "Pvalue.b"] <- summary(m.var)$coef["b", "Pr(>|t|)"] #%>% round(., digits=4)
          
          # residual standard error
          df.model.output[row.index+1, "Res.Std.Error"] <- summary(m.var)$sigma %>% round(., digits=4)
          
          # calculate and write out x-intercept
          df.model.output[row.index+1, "X.Intercept"] <- (1/summary(m.var)$coef["b", "Estimate"])*log(1/summary(m.var)$coef["a", "Estimate"]) # x intercept: x=ln(1/a)*b
          
          
          # PLOT
          color.ref <- "blue" # set reference cohort color to blue
          color.var <- "red" # set variable cohort color to red
          # plot (x,y) data points
          plot(fit.ref, xlab = "Time (Days)", ylab = "Percent Newly Synthesized", xlim = c(0, max(time)), ylim = c(0,1), main=paste(unique(data.var[, "Protein.Accession"]), unique(data.var[, "Protein.Gene"])), pch=2, col=color.ref) # blue triangles
          points(fit.var, pch=1, col=color.var) # variable cohort data points are red circles
          # plot model curves
          xg <- seq(from = 0, to = max(time), length = 3*max(time)) # create vector of inputs for predict function to use for graphing below - used for generating model curves
          lines(xg, predict(m.ref, list(x = xg)), col = color.ref) # reference cohort model is blue curve
          lines(xg, predict(m.var, list(x = xg)), col = color.var) # variable cohort model is red curve
          # legend
          legend("top", inset = 0.01, legend = c(unique(data.ref[, "Cohort"]), unique(data.var[, "Cohort"])), ncol = 2, cex = 0.8, lty = 1, col = c(color.ref, color.var)) # these legends look good on a matrix plot (2 rows by 3 columns) PDF
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
    # increase row.index counter by 2 each cycle, since we are writing out data for two cohorts during each iteration of j through cohorts.loop
    row.index <- row.index + 2
  } # end for; cohort level
} # end for; protein level

graphics.off()
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Q value #

## since Qvalue package isn't working, let's remove Qvalue column
df.model.output <- df.model.output %>%
  select(-Qvalue)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# write out model output

df.model.output <- df.model.output %>%
  na.omit() %>% # drop rows with NA
  unique() # get rid of duplicated rows from modeling the reference cohort multiple times
  
write.csv(df.model.output, file = "Table_step2_output_date.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Filter output
# pvalue of fit < 0.05
# rate of change < 0
# x-intercept > 0
# x-intercept < minimum of time points
df.model.output.filtered <- df.model.output %>%
  filter(b<0 &  X.Intercept<min(time) & X.Intercept>0 & Pvalue.a<0.05 & Pvalue.b<0.05)

# write out filtered output
write.csv(df.model.output.filtered, file = "Table_step2_output_filtered_date.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Calculate median x-intercepts for each cohort group
df.x.int.medians <- df.model.output %>%
  dplyr::group_by(Cohort) %>%
  dplyr::summarise(Median.x.intercept=median(X.Intercept))

# write out median
write.csv(df.x.int.medians, file = "Table_step2_xintercepts_date.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


# END