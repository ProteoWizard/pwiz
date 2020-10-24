#Written by Cameron Wehrfritz
#and Natan Basisty, PhD
#Schilling Lab, Buck Institute for Research on Aging
#Novato, California, USA
#March, 2020
#updated: September 28, 2020

# PROTEIN TURNOVER ANALYSIS
# STEP 4
# NLS MODEL THROUGH THE ORIGIN, with one parameter: rate of change
# OUTPUT: PDF of plots of Percent Newly Synthesized vs. Time, and Data table with statistics

######################
#### Begin Program ###
######################


cat("\n---------------------------------------------------------------------------------------")
cat(" STEP 3 STARTED ")
cat("---------------------------------------------------------------------------------------\n\n")



#------------------------------------------------------------------------------------
# LOAD DATA #

# single leucine data set (1 leucine)
data.s <- read.csv(paste(getwd(), "Step0_Data_Output_Skyline_singleleucine_peps_test.csv", sep ="/"), stringsAsFactors = F) #VPN

# multiple leucine data set (2,3,4 leucines)
data.m <- read.csv(paste(getwd(), "Step0_Data_Output_Skyline_multileucine_peps_test.csv", sep ="/"), stringsAsFactors = F) #VPN

# medians of x-intercepts by cohort from step 3
df.x.int.medians <- read.csv(paste(getwd(), "Table_step3_xintercepts.csv", sep ="/"), stringsAsFactors = F) #VPN
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

# Genes
genes <- unique(df$Protein.Gene)

# time points
time <- sort(unique(df$Timepoint))


# create modified time by subtracting the median x-intercept time (shifting left toward the origin)
# unless x-intercepts are negative, then modified.time is same as time
for(i in 1:length(cohorts)){
  if(all(df.x.int.medians[1,]>0, df.x.int.medians[1,]<min(time))){ # if all median x-intercepts are positive and less than minimum timepoint, then modify timepoints by translating left by respective median x-intercept
    df$Modified.Time[ df$Cohort == cohorts[i] ] <- df[df$Cohort == cohorts[i], "Timepoint"] - df.x.int.medians[1, colnames(df.x.int.medians)== cohorts[i] ] # translate left
  } else { 
    df$Modified.Time[ df$Cohort == cohorts[i] ] <- df[df$Cohort == cohorts[i], "Timepoint"] # do not modify
  }
} # end for
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# NLS MODEL #

# initialize data frame to write out results from nonlinear model 
# first figure out your column names since the number of columns is built off that
# row size = (number of cohorts) * (number of proteins) ... per cohort, which is hopefully constant
# col size = number of names in col.names
col.names <- c("Protein.Accession", "Gene", "Cohort", "No.Peptides", "No.Points", "b", "Pvalue.b", "Qvalue", "Res.Std.Error")
df.model.output <- data.frame(matrix(nrow = length(cohorts)*length(prots), ncol = length(col.names)))
names(df.model.output) <- col.names

#Initiate PDF
pdf(file="Turnover_step4_plots.pdf")
par(mfrow=c(2,3))

row.index <- 1 
for(i in seq_along(unique(df$Protein.Accession)) ){
  print(i)
  
  # subset combined data for cohort and protein 
  data_loop <- subset(df, Protein.Accession == prots[i])
  
  # split data by cohort
  data.OCR <- subset(data_loop, Cohort == "OCR") 
  data.OCon <- subset(data_loop, Cohort == "OCon") 
  
  # subset Time and Percent.Label to fit with model, and relabel to x and y
  fit.OCR <- subset(data.OCR, select = c("Modified.Time", "Perc.New.Synth")) %>%
    dplyr::rename(x=Modified.Time, y=Perc.New.Synth) %>%
    arrange(x) # order fit by Modified.Time from small to larger time
  
  fit.OCon <- subset(data.OCon, select = c("Modified.Time", "Perc.New.Synth")) %>%
    dplyr::rename(x=Modified.Time, y=Perc.New.Synth) %>%
    arrange(x)  # order fit by Modified.Time from small to larger time

  # calculate number of data points
  no.points.OCR <- dim(fit.OCR)[1]
  no.points.OCon <- dim(fit.OCon)[1]
  
  if(no.points.OCR > length(time)-1 & no.points.OCon > length(time)-1 ){ # if number of data points for each cohort is greater than length of time points then the model should be okay (it'll probably converge)
    tryCatch(
      expr={
        # MODEL;
        
        # first set m.OCR and m.OCon (models) to NA, so the ones from the previous iteration aren't plotted when the model does not successfully run on the current iteration
        m.OCR <- NA
        m.OCon <- NA
        
        # exponential model through the origin
        m.OCR <- nls( y ~ I(1-exp(b*x)), data = fit.OCR, start=list(b = -0.5), trace = T) 
        m.OCon <- nls( y ~ I(1-exp(b*x)), data = fit.OCon, start=list(b = -0.5), trace = T) 
        
        
        # WRITE results out to df.model.output
        
        #OCR:
        # protein
        df.model.output[row.index, "Protein.Accession"] <- prots[i]
        
        # gene
        df.model.output[row.index, "Gene"] <- unique(data.OCR[, "Protein.Gene"])
        
        # cohort
        df.model.output[ row.index, "Cohort"] <- unique(data.OCR[, "Cohort"])
        
        # number of peptides
        df.model.output[row.index, "No.Peptides"] <- length(unique(data.OCR$Modified.Peptide.Seq))
        
        # number of points
        df.model.output[row.index, "No.Points"] <- no.points.OCR
        
        # b
        df.model.output[row.index, "b"] <- round( summary(m.OCR)$coef["b", "Estimate"], 4)
        
        # p-value for b
        df.model.output[row.index, "Pvalue.b"] <- round( summary(m.OCR)$coefficients["b", "Pr(>|t|)"], 4)
        
        # residual standard error
        df.model.output[row.index, "Res.Std.Error"] <- round( summary(m.OCR)$sigma, 4)
        
        
        # OCon:
        # protein
        df.model.output[row.index +1, "Protein.Accession"] <- prots[i]
        
        # gene
        df.model.output[row.index +1, "Gene"] <- unique(data.OCon[, "Protein.Gene"])
        
        # cohort
        df.model.output[ row.index +1, "Cohort"] <- unique(data.OCon[, "Cohort"])
        
        # number of peptides
        df.model.output[row.index +1, "No.Peptides"] <- length(unique(data.OCon$Modified.Peptide.Seq))
        
        # number of points
        df.model.output[row.index +1, "No.Points"] <- no.points.OCon
        
        # b
        df.model.output[row.index +1, "b"] <- round( summary(m.OCon)$coef["b", "Estimate"], 4)
        
        # p-value for b
        df.model.output[row.index +1, "Pvalue.b"] <- round( summary(m.OCon)$coefficients["b", "Pr(>|t|)"], 4)
        
        # residual standard error
        df.model.output[row.index +1, "Res.Std.Error"] <- round( summary(m.OCon)$sigma, 4)
      
        
        # Now do model with both cohorts present - in order to get p-value statistic for plotting in legend
        # Combined Model
        model.combined <- lm( formula = log(Perc.New.Synth) ~ Cohort*Modified.Time, data = data_loop ) 
        p.value <- round(summary(model.combined)$coef[3,4], 4) # p-value from combined model, rounding to 4 decimals
        
        # PLOTTING
        # plot (x,y) points
        plot(fit.OCR, xlab = "Time (Days)", ylab = "Percent Newly Synthesized", xlim = c(0, max(time)), ylim = c(0,1), main=paste(unique(data.OCon[, "Protein.Accession"]), unique(data.OCon[, "Protein.Gene"])), pch=2, col="blue") # OCR blue triangles
        points(fit.OCon, pch=1, col="red") # OCon red circles
        # plot nls model
        #xg <- seq(from = 0, to = max(fit.OCR$x), length = 3*max(fit.OCR$x)) # create vector of inputs for predict function to use for graphing below - use in both OCR and OCon model curves
        xg <- seq(from = 0, to = max(time), length = 3*max(time)) # create vector of inputs for predict function to use for graphing below - use in both OCR and OCon model curves
        lines(xg, predict(m.OCR, list(x = xg)), col = "blue") # OCR model is blue
        lines(xg, predict(m.OCon, list(x = xg)), col = "red") # OCon model is red
        # legend
        legend("top", inset = 0.01, legend = c(unique(data.OCR[, "Cohort"]), unique(data.OCon[, "Cohort"])), ncol = 2, cex = 0.8, lty = 1, col = c("blue", "red")) # these legends look good on a matrix plot (2 rows by 3 columns) PDF
        # legend showing pvalue
        leg_pval <- paste("p =", p.value, sep = " ")
        legend("top", inset = 0.11, legend = leg_pval, cex = 0.6 )
        
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
    # OCR:
    # protein
    df.model.output[row.index, "Protein.Accession"] <- prots[i] # Protein
    
    # gene
    df.model.output[row.index, "Gene"] <- ifelse(length(unique(data.OCR[, "Protein.Gene"]))==0, NA, unique(data.OCR[, "Protein.Gene"])) # Gene
    
    # cohort
    df.model.output[ row.index, "Cohort"] <- ifelse(length(unique(data.OCR[, "Cohort"]))==0, NA, unique(data.OCR[, "Cohort"])) # OCR Cohort
    
    # number of peptides
    df.model.output[row.index, "No.Peptides"] <- ifelse(length(unique(data.OCR[, "Cohort"]))==0, NA, length(unique(data.OCR$Modified.Peptide.Seq))) # number of Modified.Peptide.Seq in OCR
    
    # number of points
    df.model.output[row.index, "No.Points"] <- ifelse(length(unique(data.OCR[, "Cohort"]))==0, NA, no.points.OCR) # Number of points in OCR
    
    
    # OCon:
    # protein
    df.model.output[row.index +1, "Protein.Accession"] <- prots[i] # Protein
    
    # gene
    df.model.output[row.index +1, "Gene"] <- ifelse(length(unique(data.OCon[, "Protein.Gene"]))==0, NA, unique(data.OCon[, "Protein.Gene"])) # Gene
    
    # cohort
    df.model.output[ row.index +1, "Cohort"] <- ifelse(length(unique(data.OCon[, "Cohort"]))==0, NA, unique(data.OCon[, "Cohort"])) # OCon Cohort
    
    # number of peptides
    df.model.output[row.index +1, "No.Peptides"] <- ifelse(length(unique(data.OCon[, "Cohort"]))==0, NA, length(unique(data.OCon$Modified.Peptide.Seq))) # number of Modified.Peptide.Seq in OCon
    
    # number of points
    df.model.output[row.index +1, "No.Points"] <-  ifelse(length(unique(data.OCon[, "Cohort"]))==0, NA, no.points.OCon) # Number of points in OCon
    
  }
  
  # increase row.index counter by 2 each cycle, since we are writing out data for both cohorts (OCon and OCR) during each iteration on 2 separate rows
  row.index <- row.index + 2
  
} # end for; protein level

# timestamp
#mtext(date(), side=1, line=4, adj=0) # side (1=bottom, 2=left, 3=top, 4=right)
graphics.off()
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Q value #

# # calculate Qvalues
# qobj <- qvalue(p = df.model.output$Pvalue.b, pi0 = 1)
# qvals <- qobj$qvalues
# df.model.output$Qvalue <- round( qvals, 4)


# since Qvalue isn't working let's get rid of Qvalue column
df.model.output <- df.model.output %>%
  select(-Qvalue) %>% # drop Qvalue for now
  na.omit() %>% # drop rows that did not run the model
  arrange(Pvalue.b)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# write out data frame 
write.csv(df.model.output, file = "Table_step4_output.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Filter
# pvalue of fit < 0.05
# rate of change < 0
df.filtered <- subset(df.model.output, b<0 & Pvalue.b<0.05)

# write out filtered df
write.csv(df.filtered, "Table_step4_output_filtered.csv", row.names = FALSE)
#------------------------------------------------------------------------------------


# END NLS MODEL #


#------------------------------------------------------------------------------------
# Some Cool Plots

perc.new.vs.time <- df %>%
  ggplot(aes(x=Timepoint, y=Perc.New.Synth, fill=Condition)) + # optional: use linetype=group to use different linetypes
  geom_point(aes(x=Timepoint, y=Perc.New.Synth, col=Condition, alpha=0.1)) +
  labs(title="Percent New Synthesized vs. Time", x="Time (Days)", y="Percent New Synthesized") +
  theme_bw() 

# Percent.New.Synthesized vs. Time 
# facet by Cohort
perc.new.vs.time.facet <- df %>%
  mutate(Condition = fct_relevel(Condition, "OCon_D3", "OCR_D3", "OCon_D7", "OCR_D7", "OCon_D12", "OCR_D12", "OCon_D17", "OCR_D17")) %>%
  ggplot(aes(x=Timepoint, y=Perc.New.Synth, fill=Condition)) + # optional: use linetype=group to use different linetypes
  geom_point(aes(x=Timepoint, y=Perc.New.Synth, col=Condition, alpha=0.1)) +
  facet_wrap(~ Cohort, ncol = 2, scales = "fixed") + 
  labs(title="Percent New Synthesized vs. Time", x="Time (Days)", y="Percent New Synthesized") +
  theme_bw() 
#------------------------------------------------------------------------------------



## END STEP 4 SCRIPT ##