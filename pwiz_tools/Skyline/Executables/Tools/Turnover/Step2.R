#Written by Cameron Wehrfritz
#and Natan Basisty, PhD
#Schilling Lab, Buck Institute for Research on Aging
#Novato, California, USA
#March, 2020
#updated: September 28, 2020

# PROTEIN TURNOVER ANALYSIS
# STEP 3:
# NON LINEAR FIT OF TURNOVER 
# CALCULATE X-INTERCEPTS
#
# OUTPUT: 
# PDF of regression plots (both cohorts in the same plot) 
# output data table of statistics and average x-intercepts by cohort


######################
#### Begin Program ###
######################



cat("\n---------------------------------------------------------------------------------------")
cat(" STEP 2 STARTED ")
cat("---------------------------------------------------------------------------------------\n\n")



#------------------------------------------------------------------------------------
# LOAD DATA #

# single leucine data set (1 leucine)
data.s <- read.csv(paste(getwd(), "Step0_Data_Output_Skyline_singleleucine_peps_test.csv", sep ="/"), stringsAsFactors = F) #VPN

# multiple leucine data set (2,3,4 leucines)
data.m <- read.csv(paste(getwd(), "Step0_Data_Output_Skyline_multileucine_peps_test.csv", sep ="/"), stringsAsFactors = F) #VPN
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# FILTER #

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
# PREP FOR MODEL #

# Cohorts
cohorts <- unique(df$Cohort)

# Proteins
prots <- unique(df$Protein.Accession)

# time points
time <- sort(unique(df$Timepoint))
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# NLS MODEL #

# initialize data frame to write out results from nonlinear model 
# first figure out your column names since the number of columns is built off that
# row size = (number of cohorts) * (number of proteins) ... per cohort, which is hopefully constant
# col size = number of names in col.names
col.names <- c("Protein.Accession", "Gene", "Cohort", "No.Peptides", "No.Points", "a", "Pvalue.a", "b", "Pvalue.b", "Qvalue", "Res.Std.Error", "X.Intercept")
df.model.output <- data.frame(matrix(nrow = length(cohorts)*length(prots), ncol = length(col.names)))
names(df.model.output) <- col.names

#Initiate PDF
pdf(file="Turnover_step3_plots.pdf")
par(mfrow=c(2,3))

row.index <- -1 # start w/ negative 1 so that when we add 2 in the first iteration we really start with 1
for(i in 1:length(unique(df$Protein.Accession)) ){
  print(i)
  
  # increase row.index counter by 2 each cycle, since we are writing out data for both cohorts (WT and KO) during each iteration
  row.index <- row.index + 2
  
  # subset combined data for cohort and protein 
  data_loop <- subset(df, Protein.Accession == prots[i]) 
  
  # split data by cohort
  data.ko <- subset(data_loop, Cohort == "OCR") # calorie restriction
  data.wt <- subset(data_loop, Cohort == "OCon") # control (aka WildType 'WT')
  
  # subset Time and Percent.Newly.Synthesized to fit with model
  fit.ko <- subset(data.ko, select = c("Timepoint", "Perc.New.Synth"))
  fit.wt <- subset(data.wt, select = c("Timepoint", "Perc.New.Synth"))
  
  
  # rename to x and y for simiplicity in model
  colnames(fit.ko) <- c("x", "y")
  colnames(fit.wt) <- c("x", "y")
  
  # order fit by time.point from small to larger time
  fit.ko <- arrange(fit.ko, x)
  fit.wt <- arrange(fit.wt, x)
  
  # ADD median x-intercept for this specific cohort to the data that will be modeled
  #fit <- rbind( c(df.x.int.medians[1, colnames(df.x.int.medians) == cohorts[j] ], 0), fit)
  
  # calculate number of data points
  no.points.ko <- dim(fit.ko)[1]
  no.points.wt <- dim(fit.wt)[1]
  
  if(no.points.ko > length(time) & no.points.wt > length(time) ){ # if number of data points for each cohort is greater than length of time points then the model should be okay (hopefully it will converge)
    tryCatch(
      expr={
        # MODEL:
        
        # first set m.ko and m.wt (models) to NA, so the ones from the previous iteration aren't plotted when the model does not successfully run on the current iteration
        m.ko <- NA
        m.wt <- NA
        
        # linearizing nls first to get starting values
        fm0.ko <- nls(log(y) ~ log(I(1-a*exp(b*x))), data = fit.ko, start = c(a = 1, b = -0.5)) # a=1 goes through origin, and b is the rate of change of the exponential, which is used in half-live calculation
        m.ko <- nls( y ~ I(1-a*exp(b*x)), data = fit.ko, start=coef(fm0.ko), trace = T ) # model with starting values
        
        # linearizing nls first to get starting values
        fm0.wt <- nls(log(y) ~ log(I(1-a*exp(b*x))), data = fit.wt, start = c(a = 1, b = -0.5)) # a=1 goes through origin, and b is the rate of change of the exponential, which is used in half-live calculation
        m.wt <- nls( y ~ I(1-a*exp(b*x)), data = fit.wt, start=coef(fm0.ko), trace = T ) # model with starting values
        
        
        # WRITE results out to df.model.output
        
        #KO:
        # protein
        df.model.output[row.index, "Protein.Accession"] <- prots[i]
        
        # gene
        df.model.output[row.index, "Gene"] <- unique(data.ko[, "Protein.Gene"])
        
        # cohort
        df.model.output[ row.index, "Cohort"] <- unique(data.ko[, "Cohort"])
        
        # number of peptides
        df.model.output[row.index, "No.Peptides"] <- length(unique(data.ko$Modified.Peptide.Seq))
        
        # number of points
        df.model.output[row.index, "No.Points"] <- no.points.ko
        
        # a
        df.model.output[row.index, "a"] <- round( summary(m.ko)$coef["a", "Estimate"], 4)
        
        # p-value for a
        df.model.output[row.index, "Pvalue.a"] <- round( summary(m.ko)$coefficients["a", "Pr(>|t|)"], 4)
        
        # b
        df.model.output[row.index, "b"] <- round( summary(m.ko)$coef["b", "Estimate"], 4)
        
        # p-value for b
        df.model.output[row.index, "Pvalue.b"] <- round( summary(m.ko)$coefficients["b", "Pr(>|t|)"], 4)
        
        # residual standard error
        df.model.output[row.index, "Res.Std.Error"] <- round( summary(m.ko)$sigma, 4)
        
        # calculate and write out x-intercept
        df.model.output[row.index, "X.Intercept"] <- (1/summary(m.ko)$coef["b", "Estimate"])*log(1/summary(m.ko)$coef["a", "Estimate"]) # x intercept: x=ln(1/a)*b
        
        
        # WT:
        # protein
        df.model.output[row.index +1, "Protein.Accession"] <- prots[i]
        
        # gene
        df.model.output[row.index +1, "Gene"] <- unique(data.wt[, "Protein.Gene"])
        
        # cohort
        df.model.output[ row.index +1, "Cohort"] <- unique(data.wt[, "Cohort"])
        
        # number of peptides
        df.model.output[row.index +1, "No.Peptides"] <- length(unique(data.wt$Modified.Peptide.Seq))
        
        # number of points
        df.model.output[row.index +1, "No.Points"] <- no.points.wt
        
        # a
        df.model.output[row.index +1, "a"] <- round( summary(m.wt)$coef["a", "Estimate"], 4)
        
        # p-value for a
        df.model.output[row.index +1, "Pvalue.a"] <- round( summary(m.wt)$coefficients["a", "Pr(>|t|)"], 4)
        
        # b
        df.model.output[row.index +1, "b"] <- round( summary(m.wt)$coef["b", "Estimate"], 4)
        
        # p-value for b
        df.model.output[row.index +1, "Pvalue.b"] <- round( summary(m.wt)$coefficients["b", "Pr(>|t|)"], 4)
        
        # residual standard error
        df.model.output[row.index +1, "Res.Std.Error"] <- round( summary(m.wt)$sigma, 4)
        
        # calculate and write out x-intercept
        df.model.output[row.index +1, "X.Intercept"] <- (1/summary(m.wt)$coef["b", "Estimate"])*log(1/summary(m.wt)$coef["a", "Estimate"]) # x intercept: x=ln(1/a)*b
        
        
        # PLOTTING
        # plot (x,y) points
        plot(fit.ko, xlab = "Time (Days)", ylab = "Percent Newly Synthesized", xlim = c(0, max(time)), ylim = c(0,1), main=paste(unique(data.wt[, "Protein.Accession"]), unique(data.wt[, "Protein.Gene"])), pch=2, col="blue") # KO blue triangles
        points(fit.wt, pch=1, col="red") # WT red circles
        ## add colored data points where multi-leucine peptides are present in data
        ##with(subset(data.ko, Number.Leucine>1), points(Timepoint, Percent.Label, pch=2, col="blue")) # KO : Blue where multi-leucine peptides are present
        ##with(subset(data.wt, Number.Leucine>1), points(Timepoint, Percent.Label, pch=1, col="red")) # WT : Red where multi-leucine peptides are present
        # plot nls model
        #xg <- seq(from = 0, to = max(fit.ko$x), length = 3*max(fit.ko$x)) # create vector of inputs for predict function to use for graphing below - use in both KO and WT model curves
        xg <- seq(from = 0, to = max(time), length = 3*max(time)) # create vector of inputs for predict function to use for graphing below - use in both KO and WT model curves
        lines(xg, predict(m.ko, list(x = xg)), col = "blue") # KO model is blue
        lines(xg, predict(m.wt, list(x = xg)), col = "red") # WT model is red
        # legend
        legend("top", inset = 0.01, legend = c(unique(data.ko[, "Cohort"]), unique(data.wt[, "Cohort"])), ncol = 2, cex = 0.8, lty = 1, col = c("blue", "red")) # these legends look good on a matrix plot (2 rows by 3 columns) PDF
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
    # KO:
    # protein
    df.model.output[row.index, "Protein.Accession"] <- prots[i] # Protein
    
    # gene
    df.model.output[row.index, "Gene"] <- ifelse(length(unique(data.ko[, "Protein.Gene"]))==0, NA, unique(data.ko[, "Protein.Gene"])) # Gene
    
    # cohort
    df.model.output[ row.index, "Cohort"] <- ifelse(length(unique(data.ko[, "Cohort"]))==0, NA, unique(data.ko[, "Cohort"])) # KO Cohort
    
    # number of peptides
    df.model.output[row.index, "No.Peptides"] <- ifelse(length(unique(data.ko[, "Cohort"]))==0, NA, length(unique(data.ko$Modified.Peptide.Seq))) # number of Modified.Peptide.Seq in KO
    
    # number of points
    df.model.output[row.index, "No.Points"] <- ifelse(length(unique(data.ko[, "Cohort"]))==0, NA, no.points.ko) # Number of points in KO
    
    
    # WT:
    # protein
    df.model.output[row.index +1, "Protein.Accession"] <- prots[i] # Protein
    
    # gene
    df.model.output[row.index +1, "Gene"] <- ifelse(length(unique(data.wt[, "Protein.Gene"]))==0, NA, unique(data.wt[, "Protein.Gene"])) # Gene
    
    # cohort
    df.model.output[ row.index +1, "Cohort"] <- ifelse(length(unique(data.wt[, "Cohort"]))==0, NA, unique(data.wt[, "Cohort"])) # WT Cohort
    
    # number of peptides
    df.model.output[row.index +1, "No.Peptides"] <- ifelse(length(unique(data.wt[, "Cohort"]))==0, NA, length(unique(data.wt$Modified.Peptide.Seq))) # number of Modified.Peptide.Seq in WT
    
    # number of points
    df.model.output[row.index +1, "No.Points"] <-  ifelse(length(unique(data.wt[, "Cohort"]))==0, NA, no.points.wt) # Number of points in WT
    
  }
} # end for; protein level

# timestamp
#mtext(date(), side=1, line=4, adj=0) # side (1=bottom, 2=left, 3=top, 4=right)
graphics.off()
#------------------------------------------------------------------------------------



#------------------------------------------------------------------------------------
# Q value #

## since Qvalue package isn't working, let's remove Qvalue column
df.model.output <- df.model.output %>%
  select(-Qvalue)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# write out df frame 

df.model.output <- df.model.output %>%
  na.omit() # drop rows with NA

write.csv(df.model.output, file = "Table_step3_output_date.csv", row.names = FALSE)
#------------------------------------------------------------------------------------

#------------------------------------------------------------------------------------
# Filter output
# pvalue of fit < 0.05
# rate of change < 0
# x-intercept > 0
# x-intercept < minimum of time points
df.filtered <- subset(df.model.output, b<0 &  X.Intercept<min(time) & X.Intercept>0 & Pvalue.a<0.05 & Pvalue.b<0.05)

# write out filtered df frame
write.csv( df.filtered, file = "Table_step3_output_filtered.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Calculate median of x-intercepts for each cohort group
# initiate df for storing medians of x.intercepts
df.x.int.medians <- data.frame(matrix(nrow = 1, ncol = length(cohorts) ))
colnames(df.x.int.medians) <- cohorts
for(i in 1:length(cohorts)){
  dat_loop <- subset(df.model.output, Cohort == cohorts[i] )
  median <- median(dat_loop$X.Intercept)
  df.x.int.medians[ 1, colnames(df.x.int.medians) == cohorts[i] ] <- median
} # end for 

# write out medians
write.csv(df.x.int.medians, file = "Table_step3_xintercepts.csv", row.names = FALSE)
#------------------------------------------------------------------------------------



## END STEP 3 SCRIPT ##