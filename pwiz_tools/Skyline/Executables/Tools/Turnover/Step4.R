#Written by Cameron Wehrfritz
#and Natan Basisty, PhD
#Schilling Lab, Buck Institute for Research on Aging
#Novato, California, USA
#March, 2020
#updated: September 18, 2020

# PROTEIN TURNOVER ANALYSIS
# STEP 5
# TURNOVER STATISTICS AND 
# DESCRIPTION: Linear model of log(Percent.Newly.Synthesized) by timepoints and cohorts and their interaction
# OUTPUT: PDF of plots of Percent Newly Synthesized vs. Time, and Data table with statistics

######################
#### Begin Program ###
######################



cat("\n---------------------------------------------------------------------------------------")
cat(" STEP 4 STARTED ")
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
# Combine Single Leucine and Multiple Leucine data sets together for modeling

df <- data.m %>%
  bind_rows(data.s) %>% # retains all columns; fills missing columns in with NA
  filter(Perc.New.Synth>0) %>% # retain data with positive values of percent newly synthesized
  mutate_at(vars(Perc.New.Synth), list(~.*100)) # scale percent newly synthesized by 100 - we will be taking the log of this later
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# PREP FOR MODEL #
# vectors for looping through, and plotting

# Cohorts
cohorts <- unique(df$Cohort)

# Proteins
prots <- unique(df$Protein.Accession)

# Genes
genes <- unique(df$Protein.Gene)

# time points
time <- sort(unique(df$Timepoint))

# peptides
peps <- unique(df$Modified.Peptide.Seq)
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# LINEAR MODELLING #

# vector of cohorts
cohorts <- sort(unique(df$Cohort), decreasing = TRUE) 
# number of combinations
no.comparisons <- choose(length(cohorts), 2) # pairwise; choosing 2 from number of cohorts

# create modified time by subtracting the median x-intercept time (shifting left toward the origin)
# unless x-intercepts are negative, then modified.time is same as time
for(i in 1:length(cohorts)){
  if(all(df.x.int.medians[1,]>0, df.x.int.medians[1,]<min(time))){ # if all median x-intercepts are positive and less than minimum timepoint, then modify timepoints by translating left by respective median x-intercept
    df$Modified.Time[ df$Cohort == cohorts[i] ] <- df[df$Cohort == cohorts[i], "Timepoint"] - df.x.int.medians[1, colnames(df.x.int.medians)== cohorts[i] ] # translate left
  } else { 
    df$Modified.Time[ df$Cohort == cohorts[i] ] <- df[df$Cohort == cohorts[i], "Timepoint"] # do not modify
  }
} # end for


# initialize data frame to write out results from linear model 
# first figure out your column names since the number of columns is built into the
# row size = number of comparisons * number of proteins (hopefully the same proteins for each comparison group)
# col size = number of names in col.names
col.names <- c("Protein.Accession", "Gene", "Comparison", "No.Peptides", "No.Points", "Interaction" , "Std.Error",  "t.value", "Unadj.P", "Qvalue", "DF",
               "Slope.Numerator", "Slope.Denominator", "Half.Life.Numerator", "Half.Life.Denominator")

df.model.output <- data.frame(matrix(nrow = no.comparisons*length(prots), ncol = length(col.names)))
names(df.model.output) <- col.names

#Initiate PDF
pdf(file="Turnover_step5_plots.pdf") 
par(mfrow=c(2,3))

# LOOP 
row.index <- 0 # counter

for(j in 1:(length(cohorts)-1)){
  for(k in 1:(length(cohorts)-j)){
    for(i in seq_along(prots)){
      # increase counter
      row.index <- row.index + 1
      
      print(c(j,k,i, row.index))
      
      # create name of comparison
      pairname <- paste(cohorts[j], "vs", cohorts[j+k], sep=" ") # Cohort A vs. Cohort B
      # write pairname out
      df.model.output[row.index, colnames(df.model.output)=="Comparison"] <- pairname
      
      # subset data for given protein
      data_loop <- subset(df, Protein.Accession == prots[i] & (Cohort == cohorts[j] | Cohort == cohorts[j+k]) )
      
      # write out Protein.Accession
      df.model.output[row.index, colnames(df.model.output)=="Protein.Accession"] <- prots[i]
      
      # write out Gene name
      df.model.output[row.index, colnames(df.model.output)=="Gene"] <- ifelse(length(unique(data_loop[ ,"Protein.Gene"]))==0, NA, unique(data_loop[ ,"Protein.Gene"]))
      
      # calculate number of peptides for given protein
      no.peps <- length(unique(data_loop$Modified.Peptide.Seq))
      # write out number of peptides
      df.model.output[row.index, colnames(df.model.output)=="No.Peptides"] <- no.peps
      
      # calculate number of data points, which is the number of rows in data_loop
      no.points <- nrow(data_loop)
      df.model.output[row.index, colnames(df.model.output)=="No.Points"] <- no.points
      
      
      # LINEAR MODEL #
      # if there are at least 1.5 times as many data points as there are time points then do linear model (should be enough data to model)
      if( no.points >= 1.5*length(time) ){
        tryCatch(
          expr={ 
            #### Model #### 
            model <- lm( formula = log(Perc.New.Synth) ~ Cohort*Modified.Time, data = data_loop)

            # write out statistics:
            # model results: estimate, std. error, t value, unadj. P 
            df.model.output[row.index, c(6:9)] <- round(summary(model)$coef[3, 1:4], 4) # model results

            p.value <- round(summary(model)$coef[3,4], 4) # p-value of iteraction term
            
            # degrees of freedom
            df.model.output[row.index, colnames(df.model.output)=="DF"] <- summary(model)$df[2] # fixed element
            
            
            #### Model ####
            # model cohort "j" against its timepoints; results used for confidence interval
            fit.j <- data_loop %>%
              filter(Cohort==cohorts[j]) %>%
              select(c(Modified.Time, Perc.New.Synth)) %>% 
              arrange(Modified.Time)
            
            mod.time.j <- select(fit.j, Modified.Time)
            
            modelj <- lm( log(fit.j$Perc.New.Synth) ~ fit.j$Modified.Time)
            
            
            #### Model ####
            # model cohort "j+k" against its time points; results used for confidence interval
            fit.k <- data_loop %>%
              filter(Cohort==cohorts[j+k]) %>%
              select(c(Modified.Time, Perc.New.Synth)) %>% 
              arrange(Modified.Time)
            
            mod.time.k <- select(fit.k, Modified.Time)
            
            modelk <- lm( log(fit.k$Perc.New.Synth) ~ fit.k$Modified.Time)
            
            
            # add slopes from modelj and modelk to df
            # numerator is modelj
            df.model.output[row.index, "Slope.Numerator"] <- round( coef(modelj)[2], 4)
            # denominator is modelk
            df.model.output[row.index, "Slope.Denominator"] <- round( coef(modelk)[2], 4)
            # half lifes of slopes
            df.model.output[row.index, "Half.Life.Numerator"] <- round( log(2)/coef(modelj)[2], 4)
            df.model.output[row.index, "Half.Life.Denominator"] <- round( log(2)/coef(modelk)[2], 4)
      
            
            ########
            # PLOT #
            
            # main title = Gene Name + Cohort j vs. Cohort j+k
            main_title <- paste( unique(data_loop$Protein.Gene), unique(data_loop$Protein.Accession), sep = " ")
            
            # make x and y for easy plotting
            x <- data_loop$Modified.Time
            y <- data_loop$Perc.New.Synth
            # initialize empty plot
            plot(x, log(y), type = "n", xlim = c(0, max(time)), ylim = c(0,5), main = main_title, 
                 xlab = "Time (Days)", ylab = "Log Percent Newly Synthesized")
            
            # plot Data Points
            # cohort j 
            with(fit.j, points(Modified.Time, log(Perc.New.Synth), pch=20, col = "blue"))
            # cohort j+k
            with(fit.k, points(Modified.Time, log(Perc.New.Synth), pch=20, col = "red"))
            
            # predicted values and confidence intervals from the linear model of cohort j
            yhatj <- predict(modelj, newdata = mod.time.j, interval = c("confidence"), level = 0.95, type = c("response")) %>%
              unique()
            
            # predicted values and confidence intervals from the linear model of cohort j+k
            yhatk <- predict(modelk, newdata = mod.time.k, interval = c("confidence"), level = 0.95, type = c("response") ) %>%
              unique()
            
            # plot predicted lines and Confidence Intervals
            # cohort j
            matplot(unique(mod.time.j), yhatj, type="l", add=TRUE, col = "blue", lty = c(1,2,2))
            # cohort j+k
            matplot(unique(mod.time.k), yhatk, type="l", add=TRUE, col = "red", lty = c(1,2,2))
            

            # legend
            # p value from model
            #leg <- paste("Interaction p =", interaction_p, sep = " ")
            leg_pval <- paste("p =", p.value, sep = " ")
            # peptides present in this data, used for legend
            leg_peps <- unique(peps[peps %in% data_loop$Modified.Peptide.Seq])
            leg2 <- paste("Peptide:", leg_peps , sep = " ")
            
            # these legends look good on a matrix plot (2 rows by 3 columns) PDF:
            legend("top", inset = 0.01, legend = c(cohorts[j], cohorts[j+k]), ncol = 2, cex = 0.8, lty = 1, col = c("blue", "red") )
            legend("top", inset = 0.11, legend = leg_pval, cex = 0.6 )
            #legend("top", inset = 0.19, legend = leg_peps, cex = 0.5) # legend for peptides ... but when there are so many it is messy
            
            # END PLOT #
            ############
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
        
      } else{ # otherwise there will not be enough data points to run the model ... then write out NA's and continue looping
        df.model.output[row.index, c(6:9)] <- NA
        df.model.output[row.index, colnames(df.model.output)=="DF"] <- NA
        
      }
      ################
    } # end for; protein level
  } # end for; cohort level
} # end for; cohort level

graphics.off()
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# Clean Up output data frame

df.model.output <- df.model.output %>%
  select(-Qvalue) %>%  # Qvalue is not working ... remove it
  arrange(Unadj.P) # arrange best Pvalue top down
#------------------------------------------------------------------------------------


#------------------------------------------------------------------------------------
# writing out data frame
write.csv(df.model.output, file = "Table_step5_output.csv", row.names = FALSE)
#------------------------------------------------------------------------------------



#######
# END #
#######