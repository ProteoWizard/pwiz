# Written by Alexandria D'Souza and Birgit Schilling
#
# All publications that utilize this software MS1Probe
# should provide acknowledgement to the Buck Institute for Research on Aging,
# the Gibson Laboratory/Chemistry Core
# and refer to the following website link
# http://www.gibsonproteomics.org/resources/MS1Probe
#
import numpy as np
from scipy import stats
import scipy.stats as st
import sys
import csv
import os

#taking in the file name, there should be eleven arguments.
if(len(sys.argv) == 11):
    fn = sys.argv[1]
else:
    raw_input('Please make sure you are using the correct External Tool from Skyline. Press Enter.')
    sys.exit()
with open(fn, 'rb') as myfile:
    skylineFile = csv.reader(myfile, dialect='excel')
    data = []
    for row in skylineFile:
        data.append(row)
    myfile.close()
    
try:
    #reading in the necessary information into a 1 by 1 array
    peakAreaCol = data[0].index("light Area")
    peakAreas2 = [row[peakAreaCol] for row in data[1 :]]
    x=0
    peakAreas=[]
    while x<len(peakAreas2):
        if peakAreas2[x] == '#N/A':
            peakAreas.append('N/A')
    
        else:
            peakAreas.append(float(peakAreas2[x]))
        x+=1

    ionNamesCol = data[0].index("FragmentIon")
    ionNames = [row[ionNamesCol] for row in data[1 :]]

    peptideNamesCol = data[0].index("PeptideSequence")
    peptideNames = [row[peptideNamesCol] for row in data[1 :]]

    lightprecMZCol = data[0].index("light PrecursorMz")
    lprecs = [row[lightprecMZCol] for row in data[1 :]]

    lightprodMZCol = data[0].index("light ProductMz")
    lprods = [row[lightprodMZCol] for row in data[1 :]]

    isoCol = data[0].index("IsotopeDistRank")
    iso = [row[isoCol] for row in data[1 :]]

    rname = data[0].index("ReplicateName")
    repname = [row[rname] for row in data[1 :]]

    modseq = data[0].index("light ModifiedSequence")
    mseq = [row[modseq] for row in data[1 :]]

    prch = data[0].index("PrecursorCharge")
    precch = [row[prch] for row in data[1 :]]

    proch = data[0].index("ProductCharge")
    prodch = [row[proch] for row in data[1 :]]

    protname = data[0].index("ProteinName")
    proname = [row[protname] for row in data[1 :]]

    rettime = data[0].index("light RetentionTime")
    rtime = [row[rettime] for row in data[1 :]]

    librank = data[0].index("LibraryRank")
    lrank = [row[librank] for row in data[1 :]]

    protDesc = data[0].index("ProteinDescription")
    pdes = [row[protDesc] for row in data[1 :]]

    
    if "good signal" in data[0]:
        goodsigCol = data[0].index("good signal")
        gsig = [row[goodsigCol] for row in data[1:]]
    else:
        gsig = []
    
    if "do not use" in data[0]:
        donotuseCol = data[0].index("do not use")
        dnu = [row[donotuseCol] for row in data[1:]]
    else:
        dnu = []
    if "light PrecursorNote" in data[0]:
        pnCol = data[0].index("light PrecursorNote")
        precnotes = [row[pnCol] for row in data[1 :]]
    else:
        precnotes=[]
except:
    raw_input('Please use the MS1Filtering_09_2012 input report, press any key then press enter to exit.')
    sys.exit()



# Copyright (C) 2008 Simone Leo - CRS4. All Rights Reserved.
# 
# Permission to use, copy, modify, and distribute this software and its
# documentation for educational, research, and not-for-profit purposes, without
# fee and without a signed licensing agreement, is hereby granted, provided
# that the above copyright notice, this paragraph and the following two
# paragraphs appear in all copies, modifications, and distributions. Contact
# CRS4, Parco Scientifico e Tecnologico, Edificio 1, 09010 PULA (CA - Italy),
# +3907092501 for commercial licensing opportunities.
# 
# IN NO EVENT SHALL CRS4 BE LIABLE TO ANY PARTY FOR DIRECT, INDIRECT, SPECIAL,
# INCIDENTAL, OR CONSEQUENTIAL DAMAGES, INCLUDING LOST PROFITS, ARISING OUT OF
# THE USE OF THIS SOFTWARE AND ITS DOCUMENTATION, EVEN IF CRS4 HAS BEEN ADVISED
# OF THE POSSIBILITY OF SUCH DAMAGE.
# 
# CRS4 SPECIFICALLY DISCLAIMS ANY WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
# THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
# PURPOSE. THE SOFTWARE AND ACCOMPANYING DOCUMENTATION, IF ANY, PROVIDED
# HEREUNDER IS PROVIDED "AS IS". CRS4 HAS NO OBLIGATION TO PROVIDE MAINTENANCE,
# SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.

"""
Functions for computing the False Discovery Rate (FDR) of a multiple test
procedure. The FDR is the expected proportion of falsely rejected hypotheses.

@see:
  Yoav Benjamini and Yosef Hochberg, I{Controlling the False Discovery Rate:
  A Practical and Powerful Approach to Multiple Testing}. Journal of the Royal
  Statistical Society, Series B (Methodological), Vol. 57, No. 1 (1995), pp.
  289-300.

  Yekutieli, D. and Benjamini, Y., I{Resampling-based false discovery rate
  controlling multiple test procedures for correlated test statistics}. J. of
  Statistical Planning and Inference, 82, pp. 171-196, 1999.

  John D. Storey (2002), I{A direct approach to false discovery rates}. Journal
  of the Royal Statistical Society: Series B (Statistical Methodology) 64 (3),
  479-498.
"""

__docformat__ = 'epytext en'


import operator
import random


def bh_rejected(pv, threshold):
    """
    Return the list of rejected p-values from C{pv} at FDR level C{threshold}.

    The downside of this approach is that the FDR level must be chosen in
    advance - the L{bh_qvalues} function yields a p-value-like output instead.

    @type pv: list
    @param pv: p-values from a multiple statistical test
    @type threshold: float
    @param threshold: the level at which FDR rate should be controlled

    @rtype: list
    @return: p-values of rejected null hypotheses
    """
    if threshold < 0 or threshold > 1:
        raise ValueError("the threshold must be between 0 and 1")
    if not pv:
        return []
    pv = sorted(pv)
    if pv[0] < 0 or pv[-1] > 1:
        raise ValueError("p-values must be between 0 and 1")
    m = len(pv)
    for i in xrange(m-1, -1, -1):
        if pv[i] <= float(i+1)*threshold/m:
            return pv[:i+1]
    return []


def bh_qvalues(pv):
    """
    Return Benjamini-Hochberg FDR q-values corresponding to p-values C{pv}.

    This function implements an algorithm equivalent to L{bh_rejected} but
    yields a list of 'adjusted p-values', allowing for rejection decisions
    based on any given threshold.

    @type pv: list
    @param pv: p-values from a multiple statistical test

    @rtype: list
    @return: adjusted p-values to be compared directly with the desired FDR
      level
    """
    if not pv:
        return []
    m = len(pv)
    args, pv = zip(*sorted(enumerate(pv), None, operator.itemgetter(1)))
    if pv[0] < 0 or pv[-1] > 1:
        raise ValueError("p-values must be between 0 and 1")
    qvalues = m * [0]
    mincoeff = pv[-1]
    qvalues[args[-1]] = mincoeff
    for j in xrange(m-2, -1, -1):
        coeff = m*pv[j]/float(j+1)
        if coeff < mincoeff:
            mincoeff = coeff
        qvalues[args[j]] = mincoeff
    return qvalues


def get_pi0(pv, lambdas):
    """
    Compute Storey's C{pi0} from p-values C{pv} and C{lambda}.

    this function is equivalent to::
    
        m = len(pv)
        return [sum(p >= l for p in pv)/((1.0-l) * m) for l in lambdas]
        
    but the above is C{O(m*n)}, while it needs only be C{O(m+n)
    (n = len(lambdas))}

    @type pv: list
    @param pv: B{SORTED} p-values vector
    @type lambdas: list
    @param lambdas: B{SORTED} lambda values vector

    @rtype: list
    @return: estimated proportion of null hypotheses C{pi0} for each lambda
    """
    m = len(pv)
    i = m - 1
    pi0 = []
    for l in reversed(lambdas):
        while i >= 0 and pv[i] >= l:
            i -= 1
        pi0.append((m-i-1)/((1.0-l)*m))
    pi0.reverse()
    return pi0


def storey_qvalues(pv, l=None):
    """
    Return Storey FDR q-values corresponding to p-values C{pv}.

    The main difference between B-H's and Storey's q-values is that the latter
    are weighted by the estimated proportion C{pi0} of true null hypotheses.

    @type pv: list
    @param pv: p-values from a multiple statistical test
    @type l: float
    @param l: lambda value for C{pi0} (fraction of null p-values) estimation

    @rtype: list
    @return: storey q-values corresponding to C{pv}
    """
    if not pv:
        return []
    m = len(pv)
    args, pv = zip(*sorted(enumerate(pv), None, operator.itemgetter(1)))
    if pv[0] < 0 or pv[-1] > 1:
        raise ValueError("p-values must be between 0 and 1")

    if l is None:
        # optimal lambda/pi0 estimation
        lambdas = [i/100.0 for i in xrange(0, 91, 5)]
        n = len(lambdas)
        pi0 = get_pi0(pv, lambdas)
        min_pi0 = min(pi0)
        mse = [0] * n        
        for i in xrange(1, 101):
            # compute bootstrap sample with replacement
            pv_boot = [pv[int(random.random()*m)] for j in xrange(m)]
            pi0_boot = get_pi0(sorted(pv_boot), lambdas)
            for j in xrange(n):
                mse[j] += (pi0_boot[j] - min_pi0) * (pi0_boot[j] - min_pi0)
        min_mse = min(mse)
        argmin_mse = [i for i, mse_i in enumerate(mse) if mse_i == min_mse]
        pi0 = min(pi0[i] for i in argmin_mse)
        pi0 = min(pi0, 1)
    else:
        try:
            l = float(l)
        except ValueError:
            raise TypeError("lambda must be a number")
        if l < 0 or l >= 1:
            raise ValueError("lambda must be within [0,1)")
        pi0 = get_pi0(pv, [l])
        pi0 = min(pi0[0], 1)

    qvalues = m * [0]
    mincoeff = pi0 * pv[-1]
    qvalues[args[-1]] = mincoeff
    for j in xrange(m-2, -1, -1):
        coeff = pi0*m*pv[j]/float(j+1)
        if coeff < mincoeff:
            mincoeff = coeff
        qvalues[args[j]] = mincoeff
    return qvalues

conditions = 2 # int(raw_input('How many conditions are there? '))

x=0
condAr=[]
condAr.append(sys.argv[5])
condAr.append(sys.argv[6])
#while x<conditions:
#   conds = raw_input('Enter the distinguishing factor of condition ' + str(x+1) +': ' )
#   condAr.append(conds)
#    x+=1
numRatio = 1 # int(raw_input('How many ratio calculations do you want? '))
x=0
raAr=[]
while x < numRatio:           
	ratioNum = sys.argv[8] #(raw_input('What condition is the numerator of ratio ' + str(x+1) +'? '))
	ratioDen = sys.argv[9] #(raw_input('What condition is the denominator of ratio ' + str(x+1) +'? '))
	raAr.append(ratioNum)
	raAr.append(ratioDen)
	x+=1
# ques = raw_input('Have you entered the information correctly? (yes/no)')
# if ques == 'no':
    # conditions = int(raw_input('How many conditions are there? '))
    # x=0
    # condAr=[]
    # while x<conditions:
        # conds =raw_input('Enter the distinguishing factor of condition ' + str(x+1) +': ' )
        # condAr.append(conds)
        # x+=1
    # numRatio = int(raw_input('How many ratio calculations do you want? '))
    # x=0
    # raAr=[]
    # while x < numRatio:           
        # ratioNum = (raw_input('What condition is the numerator of ratio ' + str(x+1) +'? '))
        # ratioDen = (raw_input('What condition is the denominator of ratio ' + str(x+1) +'? '))
        # raAr.append(ratioNum)
        # raAr.append(ratioDen)
        # x+=1
i=0
j=0
k=1
z=0
precAr=[[]]
repNameAr=[[]]
pepAr=[[]]
sucAr=[[]]
ionAr=[[]]
noteAr=[[]]
rankAr=[[]]
mseqAr=[[]]
precMzAr=[[]]
prodMzAr=[[]]
proNameAr=[[]]
precChAr=[[]]
prodChAr=[[]]
precNoteAr=[[]]
rtAr=[[]]
lrankAr=[[]]
prodesAr=[[]]
gsigAr=[[]]
dnuAr=[[]]
try:
    #splitting up the peakareas and corresponding properties from skyline
    #into a list of lists based on the product m/z since this is what varies
    #between the transitions.
    while k<len(lprods):
       
        if all([lprods[j] == lprods[k], mseq[j] == mseq[k], precch[j] == precch[k]]):
            precAr[z].append(peakAreas[j])
            repNameAr[z].append(repname[j])
            pepAr[z].append(peptideNames[j])
            ionAr[z].append(ionNames[j])
            mseqAr[z].append(mseq[j])
            rankAr[z].append(iso[j])
            precMzAr[z].append(lprecs[j])
            prodMzAr[z].append(lprods[j])
            proNameAr[z].append(proname[j])
            precChAr[z].append(precch[j])
            prodChAr[z].append(prodch[j])
            rtAr[z].append(rtime[j])
            lrankAr[z].append(lrank[j])
            prodesAr[z].append(pdes[j])
            if gsig != []:
                gsigAr[z].append(gsig[j])
            if dnu != []:
                dnuAr[z].append(dnu[j])
            if precnotes != []:
                precNoteAr[z].append(precnotes[j])
            if k == len(lprods)-1:
                precAr[z].append(peakAreas[k])
                repNameAr[z].append(repname[k])
                pepAr[z].append(peptideNames[k])
                ionAr[z].append(ionNames[k])
                mseqAr[z].append(mseq[k])
                rankAr[z].append(iso[k])
                precMzAr[z].append(lprecs[k])
                prodMzAr[z].append(lprods[k])
                proNameAr[z].append(proname[k])
                precChAr[z].append(precch[k])
                prodChAr[z].append(prodch[k])
                rtAr[z].append(rtime[k])
                lrankAr[z].append(lrank[k])
                prodesAr[z].append(pdes[k])
                if gsig != []:
                    gsigAr[z].append(gsig[k])
                if dnu != []:
                    dnuAr[z].append(dnu[k])
        elif any([lprods[j] != lprods[k], mseq[j] != mseq[k], precch[j] != precch[k]]):
            precAr[z].append(peakAreas[k-1])
            repNameAr[z].append(repname[k-1])
            pepAr[z].append(peptideNames[k-1])
            ionAr[z].append(ionNames[k-1])
            mseqAr[z].append(mseq[k-1])
            rankAr[z].append(iso[k-1])
            precMzAr[z].append(lprecs[k-1])
            prodMzAr[z].append(lprods[k-1])
            proNameAr[z].append(proname[k-1])
            precChAr[z].append(precch[k-1])
            prodChAr[z].append(prodch[k-1])
            rtAr[z].append(rtime[k-1])
            lrankAr[z].append(lrank[k-1])
            prodesAr[z].append(pdes[k-1])
            if gsig != []:
                gsigAr[z].append(gsig[k-1])
            if dnu != []:
                dnuAr[z].append(dnu[k-1])
            if precnotes != []:
                precNoteAr[z].append(precnotes[k-1])
            precAr.append([])
            repNameAr.append([])
            pepAr.append([])
            ionAr.append([])
            mseqAr.append([])
            rankAr.append([])
            precMzAr.append([])
            prodMzAr.append([])
            proNameAr.append([])
            precChAr.append([])
            prodChAr.append([])
            rtAr.append([])
            lrankAr.append([])
            prodesAr.append([])
            if gsig != []:
                gsigAr.append([])
            if dnu != []:
                dnuAr.append([])
            if precnotes != []:
                precNoteAr.append([])
            z+=1
        
        else:
            break
        j+=1
        k+=1

    
    while k<len(lprecs):
        l=0
        if all([lprods[j] == lprods[k], mseq[j] == mseq[k], precch[j] == precch[k]]):
            precAr[z].append(peakAreas[j])
            if k == len(lprods)-1:
                precAr[z].append(peakAreas[k])
        elif any([lprods[j] != lprods[k], mseq[j] != mseq[k], precch[j] != precch[k]]):
            precAr[z].append(peakAreas[k-1])
            precAr.append([])
            z+=1
        elif k == len(lprods)-1:
            precAr[z].append(peakAreas[k])
            z+=1
        else:
            break
        j+=1
        k+=1

except:
    raw_input('Unsuccesful, check your peak areas or your condition descriptions. Press enter and try again')
    sys.exit()

def t_test(array1, array2):
    num1 = len(array1)
    var1 = np.var(array1, ddof=1)
    num2 = len(array2)
    var2 = np.var(array2, ddof=1)
    degf = ((var1/num1 + var2/num2)**(2.0))/((var1/num1)**(2.0)/(num1-1) + (var2/num2)**(2.0)/(num2-1))
    t = (np.mean(array1) - np.mean(array2)) / np.sqrt(var1/num1 + var2/num2)
    pvalue = 1.0 - ( st.t.cdf(np.abs(t),degf) - st.t.cdf(-np.abs(t),degf) )    
    return pvalue

#based on the description provided, the information is split up into a list
#of lists with the KO/mutant data first and WT following.
i=0
KOthenWT=[]
assign=[]
k=0
y=0
ratioIndex=[]
try:
    while y<len(raAr):
        z=0
        while z<len(condAr):
            if condAr[z] == raAr[y]:
                ratioIndex.append(z)

            z+=1
        y+=1

    assign2=[]
    i=0
    while i<len(precAr):
        assign2=[]
        k=0
        while k<len(condAr):
            j=0
            assign=[]
            while j<len((precAr[i])):                
                if condAr[k] in repNameAr[i][j]:
                    assign.append(precAr[i][j])                                  
                j+=1                
            assign2.append(assign)
            k+=1
        KOthenWT.append(assign2)
        i+=1

except:
    raw_input('Check your condition descriptions and try again.')
    sys.exit()

w=0
KOthenWT2=[]
try:    
    while w<len(KOthenWT):
        y=0
        kos=[]
        while y<len(KOthenWT[w]):
            x=0
            kos2=[]
            while x<len(KOthenWT[w][y]):
            
                if all ([KOthenWT[w][y][x] != 'N/A', KOthenWT[w][y][x] != 0]):
                    kos2.append(KOthenWT[w][y][x])
                x+=1
            kos.append(kos2)
            y+=1
        KOthenWT2.append(kos)
        w+=1
except:
    raw_input('There are too many zero readings.')
    sys.exit()

w=0
k=0
ratioAr=[]
pvalueAr=[]
Mn=[]
Sd=[]
Cv=[]
#calculating the mean, sd, and cv for the data of KO/mutant and WT.
#calculates the ratio of KO/mutant mean:WT mean and then calculates the
#p values and q values of these lists
try:
    while w<len(KOthenWT2):
        y=0
        while y<len(KOthenWT2[w]):
            for num in KOthenWT2[w][y]:
                if num == 0:
                    KOthenWT2[w][y].remove(num)
            mean = np.mean(KOthenWT2[w][y])
            Mn.append(mean)
            
            sd = np.std(KOthenWT2[w][y])
            Sd.append(sd)
            if mean != 0:
                cv = (sd/mean)*100
            else:
                cv =0
            Cv.append(cv)
            y+=1
        x=0
        z=1            
        while z<len(ratioIndex):              
            if np.mean(KOthenWT2[w][ratioIndex[z]]) != 0:
                a=  np.mean(KOthenWT2[w][ratioIndex[x]])
                b=  np.mean(KOthenWT2[w][ratioIndex[z]])
                c=a/b
                ratioAr.append(c)
            else:
                ratioAr.append(0)            
            if any([len(KOthenWT2[w][ratioIndex[z]]) < 2, len(KOthenWT2[w][ratioIndex[x]])<2]):                
                pvalueAr.append(0)
            else:
                pv=t_test(KOthenWT2[w][ratioIndex[z]], KOthenWT2[w][ratioIndex[x]])
                pvalueAr.append(pv)
            qvalueList = bh_qvalues(pvalueAr)
            x+=2
            z+=2   
        w+=1

    ry=0
    formattedFinal=[]
    labelAr=[]
    labelAr.append('Peptide')
    labelAr.append('Transition')
    labelAr.append('Rank')
    labelAr.append('LibraryRank')
    labelAr.append('Modified Sequence')
    labelAr.append('Precursor M/Z')
    labelAr.append('Product M/Z')
    labelAr.append('Protein Name')
    labelAr.append('Protein Description')
    labelAr.append('Precursor Charge')
    labelAr.append('Product Charge')
    labelAr.append('Retention Time')
    u=0
    while u<len(condAr):
        labelAr.append('Mean '+condAr[u])
        labelAr.append('Stan Dev '+condAr[u])
        labelAr.append('CV '+condAr[u])
        u+=1
    v=0
    while v+1<len(raAr):
        labelAr.append('Ratio '+raAr[v]+'/'+raAr[v+1])
        labelAr.append('P-Value '+raAr[v]+', '+raAr[v+1])
        labelAr.append('Q-Value '+raAr[v]+', '+raAr[v+1])
        v+=2
    if gsig != []:
        labelAr.append('good signal')
    if dnu != []:
        labelAr.append('do not use')
    if precnotes != []:
        labelAr.append('Precursor Note')
    i=0
    j=0
    while ry<len(pepAr):
        formatAr=[]
        formatAr.append(pepAr[ry][1])
        formatAr.append(ionAr[ry][1])
        formatAr.append(rankAr[ry][1])
        formatAr.append(lrankAr[ry][1])
        formatAr.append(mseqAr[ry][1])
        formatAr.append(precMzAr[ry][1])
        formatAr.append(prodMzAr[ry][1])
        formatAr.append(proNameAr[ry][1])
        formatAr.append(prodesAr[ry][1])
        formatAr.append(precChAr[ry][1])
        formatAr.append(prodChAr[ry][1])
        formatAr.append(rtAr[ry][1])
        x=0
        while x < conditions:
            formatAr.append(Mn[i])
            formatAr.append(Sd[i])
            formatAr.append(Cv[i])
            i+=1  
            x+=1
        e=0
        while e<numRatio:
            formatAr.append(ratioAr[j])
            formatAr.append(pvalueAr[j])
            formatAr.append(qvalueList[j])
            j+=1
            e+=1
        if gsig != []:
            formatAr.append(gsigAr[ry][1])
        if dnu != []:
            formatAr.append(dnuAr[ry][1])        
        if precnotes != []:
            formatAr.append(precNoteAr[ry][1])
        ry+=1
        formattedFinal.append(formatAr)

    #allows the user to name the output statistics file, that should execute
    #automatically
    appendInfo = sys.argv[10] #raw_input('What would you like to append to the Statistical Output? ')  
    fn = sys.argv[3] + '\\' + sys.argv[2] + '_stat_' + appendInfo + '.csv'
    with open(fn, 'wb') as myfile:
        outputFile = csv.writer(myfile)
        i=0       
        outputFile.writerow(labelAr)
        while i<len(formattedFinal):            
            outputFile.writerows([formattedFinal[i]])
            i+=1            
        myfile.close()

    #allows the user to name the Skyline report, this is placed into the same
    #folder as the Skyline file
    appendInfo2 = sys.argv[10] #raw_input('What would you like to append to the Skyline report? ')
    fnTwo= sys.argv[3] + '\\' + sys.argv[2] + '_' + appendInfo2 + '.csv'
    with open(fnTwo, 'wb') as inputFile:
        inputfn = csv.writer(inputFile)
        i=0
        while i<len(data):
            inputfn.writerows([data[i]])
            i+=1        
        inputFile.close()
    os.startfile(fn)
    raw_input('Your Statistics File has been successfully written. The Skyline report has been written to' + fnTwo)    
except:
    raw_input('Your Statistics File has not been successfully written. Press Enter, and try again.')



