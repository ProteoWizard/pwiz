// N.B. The ALGLIB project is GPL licensed, but the ProteoWizard project has negotiated a separate 
// license agreement which allows its use without making Skyline also subject to GPL.  
// Details of the agreement can be found at:
// https://skyline.gs.washington.edu/labkey/wiki/home/software/Skyline/page.view?name=LicenseAgreement
// If you wish to use ALGLIB elsewhere, consult the terms of the standard license described below.

/*************************************************************************
Copyright (c) Sergey Bochkanov (ALGLIB project).

>>> SOURCE LICENSE >>>
This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation (www.fsf.org); either version 2 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

A copy of the GNU General Public License is available at
http://www.fsf.org/licensing/licenses
>>> END OF LICENSE >>>
*************************************************************************/
#pragma warning disable 162
#pragma warning disable 219
using System;

public partial class alglib
{



}
public partial class alglib
{


    /*************************************************************************
    This object stores state of the nonlinear CG optimizer.

    You should use ALGLIB functions to work with this object.
    *************************************************************************/
    public class mincgstate
    {
        //
        // Public declarations
        //
        public bool needf { get { return _innerobj.needf; } set { _innerobj.needf = value; } }
        public bool needfg { get { return _innerobj.needfg; } set { _innerobj.needfg = value; } }
        public bool xupdated { get { return _innerobj.xupdated; } set { _innerobj.xupdated = value; } }
        public double f { get { return _innerobj.f; } set { _innerobj.f = value; } }
        public double[] g { get { return _innerobj.g; } }
        public double[] x { get { return _innerobj.x; } }

        public mincgstate()
        {
            _innerobj = new mincg.mincgstate();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private mincg.mincgstate _innerobj;
        public mincg.mincgstate innerobj { get { return _innerobj; } }
        public mincgstate(mincg.mincgstate obj)
        {
            _innerobj = obj;
        }
    }


    /*************************************************************************

    *************************************************************************/
    public class mincgreport
    {
        //
        // Public declarations
        //
        public int iterationscount { get { return _innerobj.iterationscount; } set { _innerobj.iterationscount = value; } }
        public int nfev { get { return _innerobj.nfev; } set { _innerobj.nfev = value; } }
        public int varidx { get { return _innerobj.varidx; } set { _innerobj.varidx = value; } }
        public int terminationtype { get { return _innerobj.terminationtype; } set { _innerobj.terminationtype = value; } }

        public mincgreport()
        {
            _innerobj = new mincg.mincgreport();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private mincg.mincgreport _innerobj;
        public mincg.mincgreport innerobj { get { return _innerobj; } }
        public mincgreport(mincg.mincgreport obj)
        {
            _innerobj = obj;
        }
    }

    /*************************************************************************
            NONLINEAR CONJUGATE GRADIENT METHOD

    DESCRIPTION:
    The subroutine minimizes function F(x) of N arguments by using one of  the
    nonlinear conjugate gradient methods.

    These CG methods are globally convergent (even on non-convex functions) as
    long as grad(f) is Lipschitz continuous in  a  some  neighborhood  of  the
    L = { x : f(x)<=f(x0) }.


    REQUIREMENTS:
    Algorithm will request following information during its operation:
    * function value F and its gradient G (simultaneously) at given point X


    USAGE:
    1. User initializes algorithm state with MinCGCreate() call
    2. User tunes solver parameters with MinCGSetCond(), MinCGSetStpMax() and
       other functions
    3. User calls MinCGOptimize() function which takes algorithm  state   and
       pointer (delegate, etc.) to callback function which calculates F/G.
    4. User calls MinCGResults() to get solution
    5. Optionally, user may call MinCGRestartFrom() to solve another  problem
       with same N but another starting point and/or another function.
       MinCGRestartFrom() allows to reuse already initialized structure.


    INPUT PARAMETERS:
        N       -   problem dimension, N>0:
                    * if given, only leading N elements of X are used
                    * if not given, automatically determined from size of X
        X       -   starting point, array[0..N-1].

    OUTPUT PARAMETERS:
        State   -   structure which stores algorithm state

      -- ALGLIB --
         Copyright 25.03.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgcreate(int n, double[] x, out mincgstate state)
    {
        state = new mincgstate();
        mincg.mincgcreate(n, x, state.innerobj);
        return;
    }
    public static void mincgcreate(double[] x, out mincgstate state)
    {
        int n;

        state = new mincgstate();
        n = ap.len(x);
        mincg.mincgcreate(n, x, state.innerobj);

        return;
    }

    /*************************************************************************
    The subroutine is finite difference variant of MinCGCreate(). It uses
    finite differences in order to differentiate target function.

    Description below contains information which is specific to this function
    only. We recommend to read comments on MinCGCreate() in order to get more
    information about creation of CG optimizer.

    INPUT PARAMETERS:
        N       -   problem dimension, N>0:
                    * if given, only leading N elements of X are used
                    * if not given, automatically determined from size of X
        X       -   starting point, array[0..N-1].
        DiffStep-   differentiation step, >0

    OUTPUT PARAMETERS:
        State   -   structure which stores algorithm state

    NOTES:
    1. algorithm uses 4-point central formula for differentiation.
    2. differentiation step along I-th axis is equal to DiffStep*S[I] where
       S[] is scaling vector which can be set by MinCGSetScale() call.
    3. we recommend you to use moderate values of  differentiation  step.  Too
       large step will result in too large truncation  errors, while too small
       step will result in too large numerical  errors.  1.0E-6  can  be  good
       value to start with.
    4. Numerical  differentiation  is   very   inefficient  -   one   gradient
       calculation needs 4*N function evaluations. This function will work for
       any N - either small (1...10), moderate (10...100) or  large  (100...).
       However, performance penalty will be too severe for any N's except  for
       small ones.
       We should also say that code which relies on numerical  differentiation
       is  less  robust  and  precise.  L-BFGS  needs  exact  gradient values.
       Imprecise  gradient may slow down  convergence,  especially  on  highly
       nonlinear problems.
       Thus  we  recommend to use this function for fast prototyping on small-
       dimensional problems only, and to implement analytical gradient as soon
       as possible.

      -- ALGLIB --
         Copyright 16.05.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgcreatef(int n, double[] x, double diffstep, out mincgstate state)
    {
        state = new mincgstate();
        mincg.mincgcreatef(n, x, diffstep, state.innerobj);
        return;
    }
    public static void mincgcreatef(double[] x, double diffstep, out mincgstate state)
    {
        int n;

        state = new mincgstate();
        n = ap.len(x);
        mincg.mincgcreatef(n, x, diffstep, state.innerobj);

        return;
    }

    /*************************************************************************
    This function sets stopping conditions for CG optimization algorithm.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        EpsG    -   >=0
                    The  subroutine  finishes  its  work   if   the  condition
                    |v|<EpsG is satisfied, where:
                    * |.| means Euclidian norm
                    * v - scaled gradient vector, v[i]=g[i]*s[i]
                    * g - gradient
                    * s - scaling coefficients set by MinCGSetScale()
        EpsF    -   >=0
                    The  subroutine  finishes  its work if on k+1-th iteration
                    the  condition  |F(k+1)-F(k)|<=EpsF*max{|F(k)|,|F(k+1)|,1}
                    is satisfied.
        EpsX    -   >=0
                    The subroutine finishes its work if  on  k+1-th  iteration
                    the condition |v|<=EpsX is fulfilled, where:
                    * |.| means Euclidian norm
                    * v - scaled step vector, v[i]=dx[i]/s[i]
                    * dx - ste pvector, dx=X(k+1)-X(k)
                    * s - scaling coefficients set by MinCGSetScale()
        MaxIts  -   maximum number of iterations. If MaxIts=0, the  number  of
                    iterations is unlimited.

    Passing EpsG=0, EpsF=0, EpsX=0 and MaxIts=0 (simultaneously) will lead to
    automatic stopping criterion selection (small EpsX).

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetcond(mincgstate state, double epsg, double epsf, double epsx, int maxits)
    {

        mincg.mincgsetcond(state.innerobj, epsg, epsf, epsx, maxits);
        return;
    }

    /*************************************************************************
    This function sets scaling coefficients for CG optimizer.

    ALGLIB optimizers use scaling matrices to test stopping  conditions  (step
    size and gradient are scaled before comparison with tolerances).  Scale of
    the I-th variable is a translation invariant measure of:
    a) "how large" the variable is
    b) how large the step should be to make significant changes in the function

    Scaling is also used by finite difference variant of CG optimizer  -  step
    along I-th axis is equal to DiffStep*S[I].

    In   most   optimizers  (and  in  the  CG  too)  scaling is NOT a form  of
    preconditioning. It just  affects  stopping  conditions.  You  should  set
    preconditioner by separate call to one of the MinCGSetPrec...() functions.

    There  is  special  preconditioning  mode, however,  which  uses   scaling
    coefficients to form diagonal preconditioning matrix. You  can  turn  this
    mode on, if you want.   But  you should understand that scaling is not the
    same thing as preconditioning - these are two different, although  related
    forms of tuning solver.

    INPUT PARAMETERS:
        State   -   structure stores algorithm state
        S       -   array[N], non-zero scaling coefficients
                    S[i] may be negative, sign doesn't matter.

      -- ALGLIB --
         Copyright 14.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetscale(mincgstate state, double[] s)
    {

        mincg.mincgsetscale(state.innerobj, s);
        return;
    }

    /*************************************************************************
    This function turns on/off reporting.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        NeedXRep-   whether iteration reports are needed or not

    If NeedXRep is True, algorithm will call rep() callback function if  it is
    provided to MinCGOptimize().

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetxrep(mincgstate state, bool needxrep)
    {

        mincg.mincgsetxrep(state.innerobj, needxrep);
        return;
    }

    /*************************************************************************
    This function sets CG algorithm.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        CGType  -   algorithm type:
                    * -1    automatic selection of the best algorithm
                    * 0     DY (Dai and Yuan) algorithm
                    * 1     Hybrid DY-HS algorithm

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetcgtype(mincgstate state, int cgtype)
    {

        mincg.mincgsetcgtype(state.innerobj, cgtype);
        return;
    }

    /*************************************************************************
    This function sets maximum step length

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        StpMax  -   maximum step length, >=0. Set StpMax to 0.0,  if you don't
                    want to limit step length.

    Use this subroutine when you optimize target function which contains exp()
    or  other  fast  growing  functions,  and optimization algorithm makes too
    large  steps  which  leads  to overflow. This function allows us to reject
    steps  that  are  too  large  (and  therefore  expose  us  to the possible
    overflow) without actually calculating function value at the x+stp*d.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetstpmax(mincgstate state, double stpmax)
    {

        mincg.mincgsetstpmax(state.innerobj, stpmax);
        return;
    }

    /*************************************************************************
    This function allows to suggest initial step length to the CG algorithm.

    Suggested  step  length  is used as starting point for the line search. It
    can be useful when you have  badly  scaled  problem,  i.e.  when  ||grad||
    (which is used as initial estimate for the first step) is many  orders  of
    magnitude different from the desired step.

    Line search  may  fail  on  such problems without good estimate of initial
    step length. Imagine, for example, problem with ||grad||=10^50 and desired
    step equal to 0.1 Line  search function will use 10^50  as  initial  step,
    then  it  will  decrease step length by 2 (up to 20 attempts) and will get
    10^44, which is still too large.

    This function allows us to tell than line search should  be  started  from
    some moderate step length, like 1.0, so algorithm will be able  to  detect
    desired step length in a several searches.

    Default behavior (when no step is suggested) is to use preconditioner,  if
    it is available, to generate initial estimate of step length.

    This function influences only first iteration of algorithm. It  should  be
    called between MinCGCreate/MinCGRestartFrom() call and MinCGOptimize call.
    Suggested step is ignored if you have preconditioner.

    INPUT PARAMETERS:
        State   -   structure used to store algorithm state.
        Stp     -   initial estimate of the step length.
                    Can be zero (no estimate).

      -- ALGLIB --
         Copyright 30.07.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsuggeststep(mincgstate state, double stp)
    {

        mincg.mincgsuggeststep(state.innerobj, stp);
        return;
    }

    /*************************************************************************
    Modification of the preconditioner: preconditioning is turned off.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state

    NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
    iterations.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetprecdefault(mincgstate state)
    {

        mincg.mincgsetprecdefault(state.innerobj);
        return;
    }

    /*************************************************************************
    Modification  of  the  preconditioner:  diagonal of approximate Hessian is
    used.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        D       -   diagonal of the approximate Hessian, array[0..N-1],
                    (if larger, only leading N elements are used).

    NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
    iterations.

    NOTE 2: D[i] should be positive. Exception will be thrown otherwise.

    NOTE 3: you should pass diagonal of approximate Hessian - NOT ITS INVERSE.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetprecdiag(mincgstate state, double[] d)
    {

        mincg.mincgsetprecdiag(state.innerobj, d);
        return;
    }

    /*************************************************************************
    Modification of the preconditioner: scale-based diagonal preconditioning.

    This preconditioning mode can be useful when you  don't  have  approximate
    diagonal of Hessian, but you know that your  variables  are  badly  scaled
    (for  example,  one  variable is in [1,10], and another in [1000,100000]),
    and most part of the ill-conditioning comes from different scales of vars.

    In this case simple  scale-based  preconditioner,  with H[i] = 1/(s[i]^2),
    can greatly improve convergence.

    IMPRTANT: you should set scale of your variables with MinCGSetScale() call
    (before or after MinCGSetPrecScale() call). Without knowledge of the scale
    of your variables scale-based preconditioner will be just unit matrix.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state

    NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
    iterations.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetprecscale(mincgstate state)
    {

        mincg.mincgsetprecscale(state.innerobj);
        return;
    }

    /*************************************************************************
    This function provides reverse communication interface
    Reverse communication interface is not documented or recommended to use.
    See below for functions which provide better documented API
    *************************************************************************/
    public static bool mincgiteration(mincgstate state)
    {

        bool result = mincg.mincgiteration(state.innerobj);
        return result;
    }
    /*************************************************************************
    This family of functions is used to launcn iterations of nonlinear optimizer

    These functions accept following parameters:
        func    -   callback which calculates function (or merit function)
                    value func at given point x
        grad    -   callback which calculates function (or merit function)
                    value func and gradient grad at given point x
        rep     -   optional callback which is called after each iteration
                    can be null
        obj     -   optional object which is passed to func/grad/hess/jac/rep
                    can be null

    NOTES:

    1. This function has two different implementations: one which  uses  exact
       (analytical) user-supplied  gradient, and one which uses function value
       only  and  numerically  differentiates  function  in  order  to  obtain
       gradient.

       Depending  on  the  specific  function  used to create optimizer object
       (either MinCGCreate()  for analytical gradient  or  MinCGCreateF()  for
       numerical differentiation) you should  choose  appropriate  variant  of
       MinCGOptimize() - one which accepts function AND gradient or one  which
       accepts function ONLY.

       Be careful to choose variant of MinCGOptimize()  which  corresponds  to
       your optimization scheme! Table below lists different  combinations  of
       callback (function/gradient) passed  to  MinCGOptimize()  and  specific
       function used to create optimizer.


                      |         USER PASSED TO MinCGOptimize()
       CREATED WITH   |  function only   |  function and gradient
       ------------------------------------------------------------
       MinCGCreateF() |     work                FAIL
       MinCGCreate()  |     FAIL                work

       Here "FAIL" denotes inappropriate combinations  of  optimizer  creation
       function and MinCGOptimize() version. Attemps to use  such  combination
       (for  example,  to create optimizer with  MinCGCreateF()  and  to  pass
       gradient information to MinCGOptimize()) will lead to  exception  being
       thrown. Either  you  did  not  pass  gradient when it WAS needed or you
       passed gradient when it was NOT needed.

      -- ALGLIB --
         Copyright 20.04.2009 by Bochkanov Sergey

    *************************************************************************/
    public static void mincgoptimize(mincgstate state, ndimensional_func func, ndimensional_rep rep, object obj)
    {
        if( func==null )
            throw new alglibexception("ALGLIB: error in 'mincgoptimize()' (func is null)");
        while( alglib.mincgiteration(state) )
        {
            if( state.needf )
            {
                func(state.x, ref state.innerobj.f, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'mincgoptimize' (some derivatives were not provided?)");
        }
    }


    public static void mincgoptimize(mincgstate state, ndimensional_grad grad, ndimensional_rep rep, object obj)
    {
        if( grad==null )
            throw new alglibexception("ALGLIB: error in 'mincgoptimize()' (grad is null)");
        while( alglib.mincgiteration(state) )
        {
            if( state.needfg )
            {
                grad(state.x, ref state.innerobj.f, state.innerobj.g, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'mincgoptimize' (some derivatives were not provided?)");
        }
    }



    /*************************************************************************
    Conjugate gradient results

    INPUT PARAMETERS:
        State   -   algorithm state

    OUTPUT PARAMETERS:
        X       -   array[0..N-1], solution
        Rep     -   optimization report:
                    * Rep.TerminationType completetion code:
                        * -7    gradient verification failed.
                                See MinCGSetGradientCheck() for more information.
                        *  1    relative function improvement is no more than
                                EpsF.
                        *  2    relative step is no more than EpsX.
                        *  4    gradient norm is no more than EpsG
                        *  5    MaxIts steps was taken
                        *  7    stopping conditions are too stringent,
                                further improvement is impossible,
                                we return best X found so far
                        *  8    terminated by user
                    * Rep.IterationsCount contains iterations count
                    * NFEV countains number of function calculations

      -- ALGLIB --
         Copyright 20.04.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgresults(mincgstate state, out double[] x, out mincgreport rep)
    {
        x = new double[0];
        rep = new mincgreport();
        mincg.mincgresults(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    Conjugate gradient results

    Buffered implementation of MinCGResults(), which uses pre-allocated buffer
    to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
    intended to be used in the inner cycles of performance critical algorithms
    where array reallocation penalty is too large to be ignored.

      -- ALGLIB --
         Copyright 20.04.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgresultsbuf(mincgstate state, ref double[] x, mincgreport rep)
    {

        mincg.mincgresultsbuf(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    This  subroutine  restarts  CG  algorithm from new point. All optimization
    parameters are left unchanged.

    This  function  allows  to  solve multiple  optimization  problems  (which
    must have same number of dimensions) without object reallocation penalty.

    INPUT PARAMETERS:
        State   -   structure used to store algorithm state.
        X       -   new starting point.

      -- ALGLIB --
         Copyright 30.07.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgrestartfrom(mincgstate state, double[] x)
    {

        mincg.mincgrestartfrom(state.innerobj, x);
        return;
    }

    /*************************************************************************

    This  subroutine  turns  on  verification  of  the  user-supplied analytic
    gradient:
    * user calls this subroutine before optimization begins
    * MinCGOptimize() is called
    * prior to  actual  optimization, for each component  of  parameters being
      optimized X[i] algorithm performs following steps:
      * two trial steps are made to X[i]-TestStep*S[i] and X[i]+TestStep*S[i],
        where X[i] is i-th component of the initial point and S[i] is a  scale
        of i-th parameter
      * F(X) is evaluated at these trial points
      * we perform one more evaluation in the middle point of the interval
      * we  build  cubic  model using function values and derivatives at trial
        points and we compare its prediction with actual value in  the  middle
        point
      * in case difference between prediction and actual value is higher  than
        some predetermined threshold, algorithm stops with completion code -7;
        Rep.VarIdx is set to index of the parameter with incorrect derivative.
    * after verification is over, algorithm proceeds to the actual optimization.

    NOTE 1: verification  needs  N (parameters count) gradient evaluations. It
            is very costly and you should use  it  only  for  low  dimensional
            problems,  when  you  want  to  be  sure  that  you've   correctly
            calculated  analytic  derivatives.  You  should  not use it in the
            production code (unless you want to check derivatives provided  by
            some third party).

    NOTE 2: you  should  carefully  choose  TestStep. Value which is too large
            (so large that function behaviour is significantly non-cubic) will
            lead to false alarms. You may use  different  step  for  different
            parameters by means of setting scale with MinCGSetScale().

    NOTE 3: this function may lead to false positives. In case it reports that
            I-th  derivative was calculated incorrectly, you may decrease test
            step  and  try  one  more  time  - maybe your function changes too
            sharply  and  your  step  is  too  large for such rapidly chanding
            function.

    INPUT PARAMETERS:
        State       -   structure used to store algorithm state
        TestStep    -   verification step:
                        * TestStep=0 turns verification off
                        * TestStep>0 activates verification

      -- ALGLIB --
         Copyright 31.05.2012 by Bochkanov Sergey
    *************************************************************************/
    public static void mincgsetgradientcheck(mincgstate state, double teststep)
    {

        mincg.mincgsetgradientcheck(state.innerobj, teststep);
        return;
    }

}
public partial class alglib
{


    /*************************************************************************
    This object stores nonlinear optimizer state.
    You should use functions provided by MinBLEIC subpackage to work with this
    object
    *************************************************************************/
    public class minbleicstate
    {
        //
        // Public declarations
        //
        public bool needf { get { return _innerobj.needf; } set { _innerobj.needf = value; } }
        public bool needfg { get { return _innerobj.needfg; } set { _innerobj.needfg = value; } }
        public bool xupdated { get { return _innerobj.xupdated; } set { _innerobj.xupdated = value; } }
        public double f { get { return _innerobj.f; } set { _innerobj.f = value; } }
        public double[] g { get { return _innerobj.g; } }
        public double[] x { get { return _innerobj.x; } }

        public minbleicstate()
        {
            _innerobj = new minbleic.minbleicstate();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private minbleic.minbleicstate _innerobj;
        public minbleic.minbleicstate innerobj { get { return _innerobj; } }
        public minbleicstate(minbleic.minbleicstate obj)
        {
            _innerobj = obj;
        }
    }


    /*************************************************************************
    This structure stores optimization report:
    * InnerIterationsCount      number of inner iterations
    * OuterIterationsCount      number of outer iterations
    * NFEV                      number of gradient evaluations
    * TerminationType           termination type (see below)

    TERMINATION CODES

    TerminationType field contains completion code, which can be:
      -10   unsupported combination of algorithm settings:
            1) StpMax is set to non-zero value,
            AND 2) non-default preconditioner is used.
            You can't use both features at the same moment,
            so you have to choose one of them (and to turn
            off another one).
      -7    gradient verification failed.
            See MinBLEICSetGradientCheck() for more information.
      -3    inconsistent constraints. Feasible point is
            either nonexistent or too hard to find. Try to
            restart optimizer with better initial
            approximation
       4    conditions on constraints are fulfilled
            with error less than or equal to EpsC
       5    MaxIts steps was taken
       7    stopping conditions are too stringent,
            further improvement is impossible,
            X contains best point found so far.

    ADDITIONAL FIELDS

    There are additional fields which can be used for debugging:
    * DebugEqErr                error in the equality constraints (2-norm)
    * DebugFS                   f, calculated at projection of initial point
                                to the feasible set
    * DebugFF                   f, calculated at the final point
    * DebugDX                   |X_start-X_final|
    *************************************************************************/
    public class minbleicreport
    {
        //
        // Public declarations
        //
        public int inneriterationscount { get { return _innerobj.inneriterationscount; } set { _innerobj.inneriterationscount = value; } }
        public int outeriterationscount { get { return _innerobj.outeriterationscount; } set { _innerobj.outeriterationscount = value; } }
        public int nfev { get { return _innerobj.nfev; } set { _innerobj.nfev = value; } }
        public int varidx { get { return _innerobj.varidx; } set { _innerobj.varidx = value; } }
        public int terminationtype { get { return _innerobj.terminationtype; } set { _innerobj.terminationtype = value; } }
        public double debugeqerr { get { return _innerobj.debugeqerr; } set { _innerobj.debugeqerr = value; } }
        public double debugfs { get { return _innerobj.debugfs; } set { _innerobj.debugfs = value; } }
        public double debugff { get { return _innerobj.debugff; } set { _innerobj.debugff = value; } }
        public double debugdx { get { return _innerobj.debugdx; } set { _innerobj.debugdx = value; } }
        public int debugfeasqpits { get { return _innerobj.debugfeasqpits; } set { _innerobj.debugfeasqpits = value; } }
        public int debugfeasgpaits { get { return _innerobj.debugfeasgpaits; } set { _innerobj.debugfeasgpaits = value; } }

        public minbleicreport()
        {
            _innerobj = new minbleic.minbleicreport();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private minbleic.minbleicreport _innerobj;
        public minbleic.minbleicreport innerobj { get { return _innerobj; } }
        public minbleicreport(minbleic.minbleicreport obj)
        {
            _innerobj = obj;
        }
    }

    /*************************************************************************
                         BOUND CONSTRAINED OPTIMIZATION
           WITH ADDITIONAL LINEAR EQUALITY AND INEQUALITY CONSTRAINTS

    DESCRIPTION:
    The  subroutine  minimizes  function   F(x)  of N arguments subject to any
    combination of:
    * bound constraints
    * linear inequality constraints
    * linear equality constraints

    REQUIREMENTS:
    * user must provide function value and gradient
    * starting point X0 must be feasible or
      not too far away from the feasible set
    * grad(f) must be Lipschitz continuous on a level set:
      L = { x : f(x)<=f(x0) }
    * function must be defined everywhere on the feasible set F

    USAGE:

    Constrained optimization if far more complex than the unconstrained one.
    Here we give very brief outline of the BLEIC optimizer. We strongly recommend
    you to read examples in the ALGLIB Reference Manual and to read ALGLIB User Guide
    on optimization, which is available at http://www.alglib.net/optimization/

    1. User initializes algorithm state with MinBLEICCreate() call

    2. USer adds boundary and/or linear constraints by calling
       MinBLEICSetBC() and MinBLEICSetLC() functions.

    3. User sets stopping conditions for underlying unconstrained solver
       with MinBLEICSetInnerCond() call.
       This function controls accuracy of underlying optimization algorithm.

    4. User sets stopping conditions for outer iteration by calling
       MinBLEICSetOuterCond() function.
       This function controls handling of boundary and inequality constraints.

    5. Additionally, user may set limit on number of internal iterations
       by MinBLEICSetMaxIts() call.
       This function allows to prevent algorithm from looping forever.

    6. User calls MinBLEICOptimize() function which takes algorithm  state and
       pointer (delegate, etc.) to callback function which calculates F/G.

    7. User calls MinBLEICResults() to get solution

    8. Optionally user may call MinBLEICRestartFrom() to solve another problem
       with same N but another starting point.
       MinBLEICRestartFrom() allows to reuse already initialized structure.


    INPUT PARAMETERS:
        N       -   problem dimension, N>0:
                    * if given, only leading N elements of X are used
                    * if not given, automatically determined from size ofX
        X       -   starting point, array[N]:
                    * it is better to set X to a feasible point
                    * but X can be infeasible, in which case algorithm will try
                      to find feasible point first, using X as initial
                      approximation.

    OUTPUT PARAMETERS:
        State   -   structure stores algorithm state

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleiccreate(int n, double[] x, out minbleicstate state)
    {
        state = new minbleicstate();
        minbleic.minbleiccreate(n, x, state.innerobj);
        return;
    }
    public static void minbleiccreate(double[] x, out minbleicstate state)
    {
        int n;

        state = new minbleicstate();
        n = ap.len(x);
        minbleic.minbleiccreate(n, x, state.innerobj);

        return;
    }

    /*************************************************************************
    The subroutine is finite difference variant of MinBLEICCreate().  It  uses
    finite differences in order to differentiate target function.

    Description below contains information which is specific to  this function
    only. We recommend to read comments on MinBLEICCreate() in  order  to  get
    more information about creation of BLEIC optimizer.

    INPUT PARAMETERS:
        N       -   problem dimension, N>0:
                    * if given, only leading N elements of X are used
                    * if not given, automatically determined from size of X
        X       -   starting point, array[0..N-1].
        DiffStep-   differentiation step, >0

    OUTPUT PARAMETERS:
        State   -   structure which stores algorithm state

    NOTES:
    1. algorithm uses 4-point central formula for differentiation.
    2. differentiation step along I-th axis is equal to DiffStep*S[I] where
       S[] is scaling vector which can be set by MinBLEICSetScale() call.
    3. we recommend you to use moderate values of  differentiation  step.  Too
       large step will result in too large truncation  errors, while too small
       step will result in too large numerical  errors.  1.0E-6  can  be  good
       value to start with.
    4. Numerical  differentiation  is   very   inefficient  -   one   gradient
       calculation needs 4*N function evaluations. This function will work for
       any N - either small (1...10), moderate (10...100) or  large  (100...).
       However, performance penalty will be too severe for any N's except  for
       small ones.
       We should also say that code which relies on numerical  differentiation
       is  less  robust and precise. CG needs exact gradient values. Imprecise
       gradient may slow  down  convergence, especially  on  highly  nonlinear
       problems.
       Thus  we  recommend to use this function for fast prototyping on small-
       dimensional problems only, and to implement analytical gradient as soon
       as possible.

      -- ALGLIB --
         Copyright 16.05.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleiccreatef(int n, double[] x, double diffstep, out minbleicstate state)
    {
        state = new minbleicstate();
        minbleic.minbleiccreatef(n, x, diffstep, state.innerobj);
        return;
    }
    public static void minbleiccreatef(double[] x, double diffstep, out minbleicstate state)
    {
        int n;

        state = new minbleicstate();
        n = ap.len(x);
        minbleic.minbleiccreatef(n, x, diffstep, state.innerobj);

        return;
    }

    /*************************************************************************
    This function sets boundary constraints for BLEIC optimizer.

    Boundary constraints are inactive by default (after initial creation).
    They are preserved after algorithm restart with MinBLEICRestartFrom().

    INPUT PARAMETERS:
        State   -   structure stores algorithm state
        BndL    -   lower bounds, array[N].
                    If some (all) variables are unbounded, you may specify
                    very small number or -INF.
        BndU    -   upper bounds, array[N].
                    If some (all) variables are unbounded, you may specify
                    very large number or +INF.

    NOTE 1: it is possible to specify BndL[i]=BndU[i]. In this case I-th
    variable will be "frozen" at X[i]=BndL[i]=BndU[i].

    NOTE 2: this solver has following useful properties:
    * bound constraints are always satisfied exactly
    * function is evaluated only INSIDE area specified by  bound  constraints,
      even  when  numerical  differentiation is used (algorithm adjusts  nodes
      according to boundary constraints)

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetbc(minbleicstate state, double[] bndl, double[] bndu)
    {

        minbleic.minbleicsetbc(state.innerobj, bndl, bndu);
        return;
    }

    /*************************************************************************
    This function sets linear constraints for BLEIC optimizer.

    Linear constraints are inactive by default (after initial creation).
    They are preserved after algorithm restart with MinBLEICRestartFrom().

    INPUT PARAMETERS:
        State   -   structure previously allocated with MinBLEICCreate call.
        C       -   linear constraints, array[K,N+1].
                    Each row of C represents one constraint, either equality
                    or inequality (see below):
                    * first N elements correspond to coefficients,
                    * last element corresponds to the right part.
                    All elements of C (including right part) must be finite.
        CT      -   type of constraints, array[K]:
                    * if CT[i]>0, then I-th constraint is C[i,*]*x >= C[i,n+1]
                    * if CT[i]=0, then I-th constraint is C[i,*]*x  = C[i,n+1]
                    * if CT[i]<0, then I-th constraint is C[i,*]*x <= C[i,n+1]
        K       -   number of equality/inequality constraints, K>=0:
                    * if given, only leading K elements of C/CT are used
                    * if not given, automatically determined from sizes of C/CT

    NOTE 1: linear (non-bound) constraints are satisfied only approximately:
    * there always exists some minor violation (about Epsilon in magnitude)
      due to rounding errors
    * numerical differentiation, if used, may  lead  to  function  evaluations
      outside  of the feasible  area,   because   algorithm  does  NOT  change
      numerical differentiation formula according to linear constraints.
    If you want constraints to be  satisfied  exactly, try to reformulate your
    problem  in  such  manner  that  all constraints will become boundary ones
    (this kind of constraints is always satisfied exactly, both in  the  final
    solution and in all intermediate points).

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetlc(minbleicstate state, double[,] c, int[] ct, int k)
    {

        minbleic.minbleicsetlc(state.innerobj, c, ct, k);
        return;
    }
    public static void minbleicsetlc(minbleicstate state, double[,] c, int[] ct)
    {
        int k;
        if( (ap.rows(c)!=ap.len(ct)))
            throw new alglibexception("Error while calling 'minbleicsetlc': looks like one of arguments has wrong size");

        k = ap.rows(c);
        minbleic.minbleicsetlc(state.innerobj, c, ct, k);

        return;
    }

    /*************************************************************************
    This function sets stopping conditions for the underlying nonlinear CG
    optimizer. It controls overall accuracy of solution. These conditions
    should be strict enough in order for algorithm to converge.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        EpsG    -   >=0
                    The  subroutine  finishes  its  work   if   the  condition
                    |v|<EpsG is satisfied, where:
                    * |.| means Euclidian norm
                    * v - scaled gradient vector, v[i]=g[i]*s[i]
                    * g - gradient
                    * s - scaling coefficients set by MinBLEICSetScale()
        EpsF    -   >=0
                    The  subroutine  finishes  its work if on k+1-th iteration
                    the  condition  |F(k+1)-F(k)|<=EpsF*max{|F(k)|,|F(k+1)|,1}
                    is satisfied.
        EpsX    -   >=0
                    The subroutine finishes its work if  on  k+1-th  iteration
                    the condition |v|<=EpsX is fulfilled, where:
                    * |.| means Euclidian norm
                    * v - scaled step vector, v[i]=dx[i]/s[i]
                    * dx - ste pvector, dx=X(k+1)-X(k)
                    * s - scaling coefficients set by MinBLEICSetScale()

    Passing EpsG=0, EpsF=0 and EpsX=0 (simultaneously) will lead to
    automatic stopping criterion selection.

    These conditions are used to terminate inner iterations. However, you
    need to tune termination conditions for outer iterations too.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetinnercond(minbleicstate state, double epsg, double epsf, double epsx)
    {

        minbleic.minbleicsetinnercond(state.innerobj, epsg, epsf, epsx);
        return;
    }

    /*************************************************************************
    This function sets stopping conditions for outer iteration of BLEIC algo.

    These conditions control accuracy of constraint handling and amount of
    infeasibility allowed in the solution.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        EpsX    -   >0, stopping condition on outer iteration step length
        EpsI    -   >0, stopping condition on infeasibility

    Both EpsX and EpsI must be non-zero.

    MEANING OF EpsX

    EpsX  is  a  stopping  condition for outer iterations. Algorithm will stop
    when  solution  of  the  current  modified  subproblem will be within EpsX
    (using 2-norm) of the previous solution.

    MEANING OF EpsI

    EpsI controls feasibility properties -  algorithm  won't  stop  until  all
    inequality constraints will be satisfied with error (distance from current
    point to the feasible area) at most EpsI.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetoutercond(minbleicstate state, double epsx, double epsi)
    {

        minbleic.minbleicsetoutercond(state.innerobj, epsx, epsi);
        return;
    }

    /*************************************************************************
    This function sets scaling coefficients for BLEIC optimizer.

    ALGLIB optimizers use scaling matrices to test stopping  conditions  (step
    size and gradient are scaled before comparison with tolerances).  Scale of
    the I-th variable is a translation invariant measure of:
    a) "how large" the variable is
    b) how large the step should be to make significant changes in the function

    Scaling is also used by finite difference variant of the optimizer  - step
    along I-th axis is equal to DiffStep*S[I].

    In  most  optimizers  (and  in  the  BLEIC  too)  scaling is NOT a form of
    preconditioning. It just  affects  stopping  conditions.  You  should  set
    preconditioner  by  separate  call  to  one  of  the  MinBLEICSetPrec...()
    functions.

    There is a special  preconditioning  mode, however,  which  uses   scaling
    coefficients to form diagonal preconditioning matrix. You  can  turn  this
    mode on, if you want.   But  you should understand that scaling is not the
    same thing as preconditioning - these are two different, although  related
    forms of tuning solver.

    INPUT PARAMETERS:
        State   -   structure stores algorithm state
        S       -   array[N], non-zero scaling coefficients
                    S[i] may be negative, sign doesn't matter.

      -- ALGLIB --
         Copyright 14.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetscale(minbleicstate state, double[] s)
    {

        minbleic.minbleicsetscale(state.innerobj, s);
        return;
    }

    /*************************************************************************
    Modification of the preconditioner: preconditioning is turned off.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetprecdefault(minbleicstate state)
    {

        minbleic.minbleicsetprecdefault(state.innerobj);
        return;
    }

    /*************************************************************************
    Modification  of  the  preconditioner:  diagonal of approximate Hessian is
    used.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        D       -   diagonal of the approximate Hessian, array[0..N-1],
                    (if larger, only leading N elements are used).

    NOTE 1: D[i] should be positive. Exception will be thrown otherwise.

    NOTE 2: you should pass diagonal of approximate Hessian - NOT ITS INVERSE.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetprecdiag(minbleicstate state, double[] d)
    {

        minbleic.minbleicsetprecdiag(state.innerobj, d);
        return;
    }

    /*************************************************************************
    Modification of the preconditioner: scale-based diagonal preconditioning.

    This preconditioning mode can be useful when you  don't  have  approximate
    diagonal of Hessian, but you know that your  variables  are  badly  scaled
    (for  example,  one  variable is in [1,10], and another in [1000,100000]),
    and most part of the ill-conditioning comes from different scales of vars.

    In this case simple  scale-based  preconditioner,  with H[i] = 1/(s[i]^2),
    can greatly improve convergence.

    IMPRTANT: you should set scale of your variables  with  MinBLEICSetScale()
    call  (before  or after MinBLEICSetPrecScale() call). Without knowledge of
    the scale of your variables scale-based preconditioner will be  just  unit
    matrix.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetprecscale(minbleicstate state)
    {

        minbleic.minbleicsetprecscale(state.innerobj);
        return;
    }

    /*************************************************************************
    This function allows to stop algorithm after specified number of inner
    iterations.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        MaxIts  -   maximum number of inner iterations.
                    If MaxIts=0, the number of iterations is unlimited.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetmaxits(minbleicstate state, int maxits)
    {

        minbleic.minbleicsetmaxits(state.innerobj, maxits);
        return;
    }

    /*************************************************************************
    This function turns on/off reporting.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        NeedXRep-   whether iteration reports are needed or not

    If NeedXRep is True, algorithm will call rep() callback function if  it is
    provided to MinBLEICOptimize().

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetxrep(minbleicstate state, bool needxrep)
    {

        minbleic.minbleicsetxrep(state.innerobj, needxrep);
        return;
    }

    /*************************************************************************
    This function sets maximum step length

    IMPORTANT: this feature is hard to combine with preconditioning. You can't
    set upper limit on step length, when you solve optimization  problem  with
    linear (non-boundary) constraints AND preconditioner turned on.

    When  non-boundary  constraints  are  present,  you  have to either a) use
    preconditioner, or b) use upper limit on step length.  YOU CAN'T USE BOTH!
    In this case algorithm will terminate with appropriate error code.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        StpMax  -   maximum step length, >=0. Set StpMax to 0.0,  if you don't
                    want to limit step length.

    Use this subroutine when you optimize target function which contains exp()
    or  other  fast  growing  functions,  and optimization algorithm makes too
    large  steps  which  lead   to overflow. This function allows us to reject
    steps  that  are  too  large  (and  therefore  expose  us  to the possible
    overflow) without actually calculating function value at the x+stp*d.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetstpmax(minbleicstate state, double stpmax)
    {

        minbleic.minbleicsetstpmax(state.innerobj, stpmax);
        return;
    }

    /*************************************************************************
    This function provides reverse communication interface
    Reverse communication interface is not documented or recommended to use.
    See below for functions which provide better documented API
    *************************************************************************/
    public static bool minbleiciteration(minbleicstate state)
    {

        bool result = minbleic.minbleiciteration(state.innerobj);
        return result;
    }
    /*************************************************************************
    This family of functions is used to launcn iterations of nonlinear optimizer

    These functions accept following parameters:
        func    -   callback which calculates function (or merit function)
                    value func at given point x
        grad    -   callback which calculates function (or merit function)
                    value func and gradient grad at given point x
        rep     -   optional callback which is called after each iteration
                    can be null
        obj     -   optional object which is passed to func/grad/hess/jac/rep
                    can be null

    NOTES:

    1. This function has two different implementations: one which  uses  exact
       (analytical) user-supplied gradient,  and one which uses function value
       only  and  numerically  differentiates  function  in  order  to  obtain
       gradient.

       Depending  on  the  specific  function  used to create optimizer object
       (either  MinBLEICCreate() for analytical gradient or  MinBLEICCreateF()
       for numerical differentiation) you should choose appropriate variant of
       MinBLEICOptimize() - one  which  accepts  function  AND gradient or one
       which accepts function ONLY.

       Be careful to choose variant of MinBLEICOptimize() which corresponds to
       your optimization scheme! Table below lists different  combinations  of
       callback (function/gradient) passed to MinBLEICOptimize()  and specific
       function used to create optimizer.


                         |         USER PASSED TO MinBLEICOptimize()
       CREATED WITH      |  function only   |  function and gradient
       ------------------------------------------------------------
       MinBLEICCreateF() |     work                FAIL
       MinBLEICCreate()  |     FAIL                work

       Here "FAIL" denotes inappropriate combinations  of  optimizer  creation
       function  and  MinBLEICOptimize()  version.   Attemps   to   use   such
       combination (for  example,  to  create optimizer with MinBLEICCreateF()
       and  to  pass  gradient  information  to  MinCGOptimize()) will lead to
       exception being thrown. Either  you  did  not pass gradient when it WAS
       needed or you passed gradient when it was NOT needed.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey

    *************************************************************************/
    public static void minbleicoptimize(minbleicstate state, ndimensional_func func, ndimensional_rep rep, object obj)
    {
        if( func==null )
            throw new alglibexception("ALGLIB: error in 'minbleicoptimize()' (func is null)");
        while( alglib.minbleiciteration(state) )
        {
            if( state.needf )
            {
                func(state.x, ref state.innerobj.f, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minbleicoptimize' (some derivatives were not provided?)");
        }
    }


    public static void minbleicoptimize(minbleicstate state, ndimensional_grad grad, ndimensional_rep rep, object obj)
    {
        if( grad==null )
            throw new alglibexception("ALGLIB: error in 'minbleicoptimize()' (grad is null)");
        while( alglib.minbleiciteration(state) )
        {
            if( state.needfg )
            {
                grad(state.x, ref state.innerobj.f, state.innerobj.g, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minbleicoptimize' (some derivatives were not provided?)");
        }
    }



    /*************************************************************************
    BLEIC results

    INPUT PARAMETERS:
        State   -   algorithm state

    OUTPUT PARAMETERS:
        X       -   array[0..N-1], solution
        Rep     -   optimization report. You should check Rep.TerminationType
                    in  order  to  distinguish  successful  termination  from
                    unsuccessful one.
                    More information about fields of this  structure  can  be
                    found in the comments on MinBLEICReport datatype.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicresults(minbleicstate state, out double[] x, out minbleicreport rep)
    {
        x = new double[0];
        rep = new minbleicreport();
        minbleic.minbleicresults(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    BLEIC results

    Buffered implementation of MinBLEICResults() which uses pre-allocated buffer
    to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
    intended to be used in the inner cycles of performance critical algorithms
    where array reallocation penalty is too large to be ignored.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicresultsbuf(minbleicstate state, ref double[] x, minbleicreport rep)
    {

        minbleic.minbleicresultsbuf(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    This subroutine restarts algorithm from new point.
    All optimization parameters (including constraints) are left unchanged.

    This  function  allows  to  solve multiple  optimization  problems  (which
    must have  same number of dimensions) without object reallocation penalty.

    INPUT PARAMETERS:
        State   -   structure previously allocated with MinBLEICCreate call.
        X       -   new starting point.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicrestartfrom(minbleicstate state, double[] x)
    {

        minbleic.minbleicrestartfrom(state.innerobj, x);
        return;
    }

    /*************************************************************************
    This  subroutine  turns  on  verification  of  the  user-supplied analytic
    gradient:
    * user calls this subroutine before optimization begins
    * MinBLEICOptimize() is called
    * prior to  actual  optimization, for each component  of  parameters being
      optimized X[i] algorithm performs following steps:
      * two trial steps are made to X[i]-TestStep*S[i] and X[i]+TestStep*S[i],
        where X[i] is i-th component of the initial point and S[i] is a  scale
        of i-th parameter
      * if needed, steps are bounded with respect to constraints on X[]
      * F(X) is evaluated at these trial points
      * we perform one more evaluation in the middle point of the interval
      * we  build  cubic  model using function values and derivatives at trial
        points and we compare its prediction with actual value in  the  middle
        point
      * in case difference between prediction and actual value is higher  than
        some predetermined threshold, algorithm stops with completion code -7;
        Rep.VarIdx is set to index of the parameter with incorrect derivative.
    * after verification is over, algorithm proceeds to the actual optimization.

    NOTE 1: verification  needs  N (parameters count) gradient evaluations. It
            is very costly and you should use  it  only  for  low  dimensional
            problems,  when  you  want  to  be  sure  that  you've   correctly
            calculated  analytic  derivatives.  You  should  not use it in the
            production code (unless you want to check derivatives provided  by
            some third party).

    NOTE 2: you  should  carefully  choose  TestStep. Value which is too large
            (so large that function behaviour is significantly non-cubic) will
            lead to false alarms. You may use  different  step  for  different
            parameters by means of setting scale with MinBLEICSetScale().

    NOTE 3: this function may lead to false positives. In case it reports that
            I-th  derivative was calculated incorrectly, you may decrease test
            step  and  try  one  more  time  - maybe your function changes too
            sharply  and  your  step  is  too  large for such rapidly chanding
            function.

    INPUT PARAMETERS:
        State       -   structure used to store algorithm state
        TestStep    -   verification step:
                        * TestStep=0 turns verification off
                        * TestStep>0 activates verification

      -- ALGLIB --
         Copyright 15.06.2012 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetgradientcheck(minbleicstate state, double teststep)
    {

        minbleic.minbleicsetgradientcheck(state.innerobj, teststep);
        return;
    }

}
public partial class alglib
{


    /*************************************************************************

    *************************************************************************/
    public class minlbfgsstate
    {
        //
        // Public declarations
        //
        public bool needf { get { return _innerobj.needf; } set { _innerobj.needf = value; } }
        public bool needfg { get { return _innerobj.needfg; } set { _innerobj.needfg = value; } }
        public bool xupdated { get { return _innerobj.xupdated; } set { _innerobj.xupdated = value; } }
        public double f { get { return _innerobj.f; } set { _innerobj.f = value; } }
        public double[] g { get { return _innerobj.g; } }
        public double[] x { get { return _innerobj.x; } }

        public minlbfgsstate()
        {
            _innerobj = new minlbfgs.minlbfgsstate();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private minlbfgs.minlbfgsstate _innerobj;
        public minlbfgs.minlbfgsstate innerobj { get { return _innerobj; } }
        public minlbfgsstate(minlbfgs.minlbfgsstate obj)
        {
            _innerobj = obj;
        }
    }


    /*************************************************************************

    *************************************************************************/
    public class minlbfgsreport
    {
        //
        // Public declarations
        //
        public int iterationscount { get { return _innerobj.iterationscount; } set { _innerobj.iterationscount = value; } }
        public int nfev { get { return _innerobj.nfev; } set { _innerobj.nfev = value; } }
        public int varidx { get { return _innerobj.varidx; } set { _innerobj.varidx = value; } }
        public int terminationtype { get { return _innerobj.terminationtype; } set { _innerobj.terminationtype = value; } }

        public minlbfgsreport()
        {
            _innerobj = new minlbfgs.minlbfgsreport();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private minlbfgs.minlbfgsreport _innerobj;
        public minlbfgs.minlbfgsreport innerobj { get { return _innerobj; } }
        public minlbfgsreport(minlbfgs.minlbfgsreport obj)
        {
            _innerobj = obj;
        }
    }

    /*************************************************************************
            LIMITED MEMORY BFGS METHOD FOR LARGE SCALE OPTIMIZATION

    DESCRIPTION:
    The subroutine minimizes function F(x) of N arguments by  using  a  quasi-
    Newton method (LBFGS scheme) which is optimized to use  a  minimum  amount
    of memory.
    The subroutine generates the approximation of an inverse Hessian matrix by
    using information about the last M steps of the algorithm  (instead of N).
    It lessens a required amount of memory from a value  of  order  N^2  to  a
    value of order 2*N*M.


    REQUIREMENTS:
    Algorithm will request following information during its operation:
    * function value F and its gradient G (simultaneously) at given point X


    USAGE:
    1. User initializes algorithm state with MinLBFGSCreate() call
    2. User tunes solver parameters with MinLBFGSSetCond() MinLBFGSSetStpMax()
       and other functions
    3. User calls MinLBFGSOptimize() function which takes algorithm  state and
       pointer (delegate, etc.) to callback function which calculates F/G.
    4. User calls MinLBFGSResults() to get solution
    5. Optionally user may call MinLBFGSRestartFrom() to solve another problem
       with same N/M but another starting point and/or another function.
       MinLBFGSRestartFrom() allows to reuse already initialized structure.


    INPUT PARAMETERS:
        N       -   problem dimension. N>0
        M       -   number of corrections in the BFGS scheme of Hessian
                    approximation update. Recommended value:  3<=M<=7. The smaller
                    value causes worse convergence, the bigger will  not  cause  a
                    considerably better convergence, but will cause a fall in  the
                    performance. M<=N.
        X       -   initial solution approximation, array[0..N-1].


    OUTPUT PARAMETERS:
        State   -   structure which stores algorithm state


    NOTES:
    1. you may tune stopping conditions with MinLBFGSSetCond() function
    2. if target function contains exp() or other fast growing functions,  and
       optimization algorithm makes too large steps which leads  to  overflow,
       use MinLBFGSSetStpMax() function to bound algorithm's  steps.  However,
       L-BFGS rarely needs such a tuning.


      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgscreate(int n, int m, double[] x, out minlbfgsstate state)
    {
        state = new minlbfgsstate();
        minlbfgs.minlbfgscreate(n, m, x, state.innerobj);
        return;
    }
    public static void minlbfgscreate(int m, double[] x, out minlbfgsstate state)
    {
        int n;

        state = new minlbfgsstate();
        n = ap.len(x);
        minlbfgs.minlbfgscreate(n, m, x, state.innerobj);

        return;
    }

    /*************************************************************************
    The subroutine is finite difference variant of MinLBFGSCreate().  It  uses
    finite differences in order to differentiate target function.

    Description below contains information which is specific to  this function
    only. We recommend to read comments on MinLBFGSCreate() in  order  to  get
    more information about creation of LBFGS optimizer.

    INPUT PARAMETERS:
        N       -   problem dimension, N>0:
                    * if given, only leading N elements of X are used
                    * if not given, automatically determined from size of X
        M       -   number of corrections in the BFGS scheme of Hessian
                    approximation update. Recommended value:  3<=M<=7. The smaller
                    value causes worse convergence, the bigger will  not  cause  a
                    considerably better convergence, but will cause a fall in  the
                    performance. M<=N.
        X       -   starting point, array[0..N-1].
        DiffStep-   differentiation step, >0

    OUTPUT PARAMETERS:
        State   -   structure which stores algorithm state

    NOTES:
    1. algorithm uses 4-point central formula for differentiation.
    2. differentiation step along I-th axis is equal to DiffStep*S[I] where
       S[] is scaling vector which can be set by MinLBFGSSetScale() call.
    3. we recommend you to use moderate values of  differentiation  step.  Too
       large step will result in too large truncation  errors, while too small
       step will result in too large numerical  errors.  1.0E-6  can  be  good
       value to start with.
    4. Numerical  differentiation  is   very   inefficient  -   one   gradient
       calculation needs 4*N function evaluations. This function will work for
       any N - either small (1...10), moderate (10...100) or  large  (100...).
       However, performance penalty will be too severe for any N's except  for
       small ones.
       We should also say that code which relies on numerical  differentiation
       is   less  robust  and  precise.  LBFGS  needs  exact  gradient values.
       Imprecise gradient may slow  down  convergence,  especially  on  highly
       nonlinear problems.
       Thus  we  recommend to use this function for fast prototyping on small-
       dimensional problems only, and to implement analytical gradient as soon
       as possible.

      -- ALGLIB --
         Copyright 16.05.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgscreatef(int n, int m, double[] x, double diffstep, out minlbfgsstate state)
    {
        state = new minlbfgsstate();
        minlbfgs.minlbfgscreatef(n, m, x, diffstep, state.innerobj);
        return;
    }
    public static void minlbfgscreatef(int m, double[] x, double diffstep, out minlbfgsstate state)
    {
        int n;

        state = new minlbfgsstate();
        n = ap.len(x);
        minlbfgs.minlbfgscreatef(n, m, x, diffstep, state.innerobj);

        return;
    }

    /*************************************************************************
    This function sets stopping conditions for L-BFGS optimization algorithm.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        EpsG    -   >=0
                    The  subroutine  finishes  its  work   if   the  condition
                    |v|<EpsG is satisfied, where:
                    * |.| means Euclidian norm
                    * v - scaled gradient vector, v[i]=g[i]*s[i]
                    * g - gradient
                    * s - scaling coefficients set by MinLBFGSSetScale()
        EpsF    -   >=0
                    The  subroutine  finishes  its work if on k+1-th iteration
                    the  condition  |F(k+1)-F(k)|<=EpsF*max{|F(k)|,|F(k+1)|,1}
                    is satisfied.
        EpsX    -   >=0
                    The subroutine finishes its work if  on  k+1-th  iteration
                    the condition |v|<=EpsX is fulfilled, where:
                    * |.| means Euclidian norm
                    * v - scaled step vector, v[i]=dx[i]/s[i]
                    * dx - ste pvector, dx=X(k+1)-X(k)
                    * s - scaling coefficients set by MinLBFGSSetScale()
        MaxIts  -   maximum number of iterations. If MaxIts=0, the  number  of
                    iterations is unlimited.

    Passing EpsG=0, EpsF=0, EpsX=0 and MaxIts=0 (simultaneously) will lead to
    automatic stopping criterion selection (small EpsX).

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetcond(minlbfgsstate state, double epsg, double epsf, double epsx, int maxits)
    {

        minlbfgs.minlbfgssetcond(state.innerobj, epsg, epsf, epsx, maxits);
        return;
    }

    /*************************************************************************
    This function turns on/off reporting.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        NeedXRep-   whether iteration reports are needed or not

    If NeedXRep is True, algorithm will call rep() callback function if  it is
    provided to MinLBFGSOptimize().


      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetxrep(minlbfgsstate state, bool needxrep)
    {

        minlbfgs.minlbfgssetxrep(state.innerobj, needxrep);
        return;
    }

    /*************************************************************************
    This function sets maximum step length

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        StpMax  -   maximum step length, >=0. Set StpMax to 0.0 (default),  if
                    you don't want to limit step length.

    Use this subroutine when you optimize target function which contains exp()
    or  other  fast  growing  functions,  and optimization algorithm makes too
    large  steps  which  leads  to overflow. This function allows us to reject
    steps  that  are  too  large  (and  therefore  expose  us  to the possible
    overflow) without actually calculating function value at the x+stp*d.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetstpmax(minlbfgsstate state, double stpmax)
    {

        minlbfgs.minlbfgssetstpmax(state.innerobj, stpmax);
        return;
    }

    /*************************************************************************
    This function sets scaling coefficients for LBFGS optimizer.

    ALGLIB optimizers use scaling matrices to test stopping  conditions  (step
    size and gradient are scaled before comparison with tolerances).  Scale of
    the I-th variable is a translation invariant measure of:
    a) "how large" the variable is
    b) how large the step should be to make significant changes in the function

    Scaling is also used by finite difference variant of the optimizer  - step
    along I-th axis is equal to DiffStep*S[I].

    In  most  optimizers  (and  in  the  LBFGS  too)  scaling is NOT a form of
    preconditioning. It just  affects  stopping  conditions.  You  should  set
    preconditioner  by  separate  call  to  one  of  the  MinLBFGSSetPrec...()
    functions.

    There  is  special  preconditioning  mode, however,  which  uses   scaling
    coefficients to form diagonal preconditioning matrix. You  can  turn  this
    mode on, if you want.   But  you should understand that scaling is not the
    same thing as preconditioning - these are two different, although  related
    forms of tuning solver.

    INPUT PARAMETERS:
        State   -   structure stores algorithm state
        S       -   array[N], non-zero scaling coefficients
                    S[i] may be negative, sign doesn't matter.

      -- ALGLIB --
         Copyright 14.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetscale(minlbfgsstate state, double[] s)
    {

        minlbfgs.minlbfgssetscale(state.innerobj, s);
        return;
    }

    /*************************************************************************
    Modification  of  the  preconditioner:  default  preconditioner    (simple
    scaling, same for all elements of X) is used.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state

    NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
    iterations.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetprecdefault(minlbfgsstate state)
    {

        minlbfgs.minlbfgssetprecdefault(state.innerobj);
        return;
    }

    /*************************************************************************
    Modification of the preconditioner: Cholesky factorization of  approximate
    Hessian is used.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        P       -   triangular preconditioner, Cholesky factorization of
                    the approximate Hessian. array[0..N-1,0..N-1],
                    (if larger, only leading N elements are used).
        IsUpper -   whether upper or lower triangle of P is given
                    (other triangle is not referenced)

    After call to this function preconditioner is changed to P  (P  is  copied
    into the internal buffer).

    NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
    iterations.

    NOTE 2:  P  should  be nonsingular. Exception will be thrown otherwise.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetpreccholesky(minlbfgsstate state, double[,] p, bool isupper)
    {

        minlbfgs.minlbfgssetpreccholesky(state.innerobj, p, isupper);
        return;
    }

    /*************************************************************************
    Modification  of  the  preconditioner:  diagonal of approximate Hessian is
    used.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        D       -   diagonal of the approximate Hessian, array[0..N-1],
                    (if larger, only leading N elements are used).

    NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
    iterations.

    NOTE 2: D[i] should be positive. Exception will be thrown otherwise.

    NOTE 3: you should pass diagonal of approximate Hessian - NOT ITS INVERSE.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetprecdiag(minlbfgsstate state, double[] d)
    {

        minlbfgs.minlbfgssetprecdiag(state.innerobj, d);
        return;
    }

    /*************************************************************************
    Modification of the preconditioner: scale-based diagonal preconditioning.

    This preconditioning mode can be useful when you  don't  have  approximate
    diagonal of Hessian, but you know that your  variables  are  badly  scaled
    (for  example,  one  variable is in [1,10], and another in [1000,100000]),
    and most part of the ill-conditioning comes from different scales of vars.

    In this case simple  scale-based  preconditioner,  with H[i] = 1/(s[i]^2),
    can greatly improve convergence.

    IMPRTANT: you should set scale of your variables  with  MinLBFGSSetScale()
    call  (before  or after MinLBFGSSetPrecScale() call). Without knowledge of
    the scale of your variables scale-based preconditioner will be  just  unit
    matrix.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetprecscale(minlbfgsstate state)
    {

        minlbfgs.minlbfgssetprecscale(state.innerobj);
        return;
    }

    /*************************************************************************
    This function provides reverse communication interface
    Reverse communication interface is not documented or recommended to use.
    See below for functions which provide better documented API
    *************************************************************************/
    public static bool minlbfgsiteration(minlbfgsstate state)
    {

        bool result = minlbfgs.minlbfgsiteration(state.innerobj);
        return result;
    }
    /*************************************************************************
    This family of functions is used to launcn iterations of nonlinear optimizer

    These functions accept following parameters:
        func    -   callback which calculates function (or merit function)
                    value func at given point x
        grad    -   callback which calculates function (or merit function)
                    value func and gradient grad at given point x
        rep     -   optional callback which is called after each iteration
                    can be null
        obj     -   optional object which is passed to func/grad/hess/jac/rep
                    can be null

    NOTES:

    1. This function has two different implementations: one which  uses  exact
       (analytical) user-supplied gradient,  and one which uses function value
       only  and  numerically  differentiates  function  in  order  to  obtain
       gradient.

       Depending  on  the  specific  function  used to create optimizer object
       (either MinLBFGSCreate() for analytical gradient  or  MinLBFGSCreateF()
       for numerical differentiation) you should choose appropriate variant of
       MinLBFGSOptimize() - one  which  accepts  function  AND gradient or one
       which accepts function ONLY.

       Be careful to choose variant of MinLBFGSOptimize() which corresponds to
       your optimization scheme! Table below lists different  combinations  of
       callback (function/gradient) passed to MinLBFGSOptimize()  and specific
       function used to create optimizer.


                         |         USER PASSED TO MinLBFGSOptimize()
       CREATED WITH      |  function only   |  function and gradient
       ------------------------------------------------------------
       MinLBFGSCreateF() |     work                FAIL
       MinLBFGSCreate()  |     FAIL                work

       Here "FAIL" denotes inappropriate combinations  of  optimizer  creation
       function  and  MinLBFGSOptimize()  version.   Attemps   to   use   such
       combination (for example, to create optimizer with MinLBFGSCreateF() and
       to pass gradient information to MinCGOptimize()) will lead to exception
       being thrown. Either  you  did  not pass gradient when it WAS needed or
       you passed gradient when it was NOT needed.

      -- ALGLIB --
         Copyright 20.03.2009 by Bochkanov Sergey

    *************************************************************************/
    public static void minlbfgsoptimize(minlbfgsstate state, ndimensional_func func, ndimensional_rep rep, object obj)
    {
        if( func==null )
            throw new alglibexception("ALGLIB: error in 'minlbfgsoptimize()' (func is null)");
        while( alglib.minlbfgsiteration(state) )
        {
            if( state.needf )
            {
                func(state.x, ref state.innerobj.f, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minlbfgsoptimize' (some derivatives were not provided?)");
        }
    }


    public static void minlbfgsoptimize(minlbfgsstate state, ndimensional_grad grad, ndimensional_rep rep, object obj)
    {
        if( grad==null )
            throw new alglibexception("ALGLIB: error in 'minlbfgsoptimize()' (grad is null)");
        while( alglib.minlbfgsiteration(state) )
        {
            if( state.needfg )
            {
                grad(state.x, ref state.innerobj.f, state.innerobj.g, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minlbfgsoptimize' (some derivatives were not provided?)");
        }
    }



    /*************************************************************************
    L-BFGS algorithm results

    INPUT PARAMETERS:
        State   -   algorithm state

    OUTPUT PARAMETERS:
        X       -   array[0..N-1], solution
        Rep     -   optimization report:
                    * Rep.TerminationType completetion code:
                        * -7    gradient verification failed.
                                See MinLBFGSSetGradientCheck() for more information.
                        * -2    rounding errors prevent further improvement.
                                X contains best point found.
                        * -1    incorrect parameters were specified
                        *  1    relative function improvement is no more than
                                EpsF.
                        *  2    relative step is no more than EpsX.
                        *  4    gradient norm is no more than EpsG
                        *  5    MaxIts steps was taken
                        *  7    stopping conditions are too stringent,
                                further improvement is impossible
                    * Rep.IterationsCount contains iterations count
                    * NFEV countains number of function calculations

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgsresults(minlbfgsstate state, out double[] x, out minlbfgsreport rep)
    {
        x = new double[0];
        rep = new minlbfgsreport();
        minlbfgs.minlbfgsresults(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    L-BFGS algorithm results

    Buffered implementation of MinLBFGSResults which uses pre-allocated buffer
    to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
    intended to be used in the inner cycles of performance critical algorithms
    where array reallocation penalty is too large to be ignored.

      -- ALGLIB --
         Copyright 20.08.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgsresultsbuf(minlbfgsstate state, ref double[] x, minlbfgsreport rep)
    {

        minlbfgs.minlbfgsresultsbuf(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    This  subroutine restarts LBFGS algorithm from new point. All optimization
    parameters are left unchanged.

    This  function  allows  to  solve multiple  optimization  problems  (which
    must have same number of dimensions) without object reallocation penalty.

    INPUT PARAMETERS:
        State   -   structure used to store algorithm state
        X       -   new starting point.

      -- ALGLIB --
         Copyright 30.07.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgsrestartfrom(minlbfgsstate state, double[] x)
    {

        minlbfgs.minlbfgsrestartfrom(state.innerobj, x);
        return;
    }

    /*************************************************************************
    This  subroutine  turns  on  verification  of  the  user-supplied analytic
    gradient:
    * user calls this subroutine before optimization begins
    * MinLBFGSOptimize() is called
    * prior to  actual  optimization, for each component  of  parameters being
      optimized X[i] algorithm performs following steps:
      * two trial steps are made to X[i]-TestStep*S[i] and X[i]+TestStep*S[i],
        where X[i] is i-th component of the initial point and S[i] is a  scale
        of i-th parameter
      * if needed, steps are bounded with respect to constraints on X[]
      * F(X) is evaluated at these trial points
      * we perform one more evaluation in the middle point of the interval
      * we  build  cubic  model using function values and derivatives at trial
        points and we compare its prediction with actual value in  the  middle
        point
      * in case difference between prediction and actual value is higher  than
        some predetermined threshold, algorithm stops with completion code -7;
        Rep.VarIdx is set to index of the parameter with incorrect derivative.
    * after verification is over, algorithm proceeds to the actual optimization.

    NOTE 1: verification  needs  N (parameters count) gradient evaluations. It
            is very costly and you should use  it  only  for  low  dimensional
            problems,  when  you  want  to  be  sure  that  you've   correctly
            calculated  analytic  derivatives.  You  should  not use it in the
            production code (unless you want to check derivatives provided  by
            some third party).

    NOTE 2: you  should  carefully  choose  TestStep. Value which is too large
            (so large that function behaviour is significantly non-cubic) will
            lead to false alarms. You may use  different  step  for  different
            parameters by means of setting scale with MinLBFGSSetScale().

    NOTE 3: this function may lead to false positives. In case it reports that
            I-th  derivative was calculated incorrectly, you may decrease test
            step  and  try  one  more  time  - maybe your function changes too
            sharply  and  your  step  is  too  large for such rapidly chanding
            function.

    INPUT PARAMETERS:
        State       -   structure used to store algorithm state
        TestStep    -   verification step:
                        * TestStep=0 turns verification off
                        * TestStep>0 activates verification

      -- ALGLIB --
         Copyright 24.05.2012 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetgradientcheck(minlbfgsstate state, double teststep)
    {

        minlbfgs.minlbfgssetgradientcheck(state.innerobj, teststep);
        return;
    }

}
public partial class alglib
{



}
public partial class alglib
{


    /*************************************************************************
    This object stores nonlinear optimizer state.
    You should use functions provided by MinQP subpackage to work with this
    object
    *************************************************************************/
    public class minqpstate
    {
        //
        // Public declarations
        //

        public minqpstate()
        {
            _innerobj = new minqp.minqpstate();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private minqp.minqpstate _innerobj;
        public minqp.minqpstate innerobj { get { return _innerobj; } }
        public minqpstate(minqp.minqpstate obj)
        {
            _innerobj = obj;
        }
    }


    /*************************************************************************
    This structure stores optimization report:
    * InnerIterationsCount      number of inner iterations
    * OuterIterationsCount      number of outer iterations
    * NCholesky                 number of Cholesky decomposition
    * NMV                       number of matrix-vector products
                                (only products calculated as part of iterative
                                process are counted)
    * TerminationType           completion code (see below)

    Completion codes:
    * -5    inappropriate solver was used:
            * Cholesky solver for semidefinite or indefinite problems
            * Cholesky solver for problems with non-boundary constraints
    * -3    inconsistent constraints (or, maybe, feasible point is
            too hard to find). If you are sure that constraints are feasible,
            try to restart optimizer with better initial approximation.
    * -1    solver error
    *  4    successful completion
    *  5    MaxIts steps was taken
    *  7    stopping conditions are too stringent,
            further improvement is impossible,
            X contains best point found so far.
    *************************************************************************/
    public class minqpreport
    {
        //
        // Public declarations
        //
        public int inneriterationscount { get { return _innerobj.inneriterationscount; } set { _innerobj.inneriterationscount = value; } }
        public int outeriterationscount { get { return _innerobj.outeriterationscount; } set { _innerobj.outeriterationscount = value; } }
        public int nmv { get { return _innerobj.nmv; } set { _innerobj.nmv = value; } }
        public int ncholesky { get { return _innerobj.ncholesky; } set { _innerobj.ncholesky = value; } }
        public int terminationtype { get { return _innerobj.terminationtype; } set { _innerobj.terminationtype = value; } }

        public minqpreport()
        {
            _innerobj = new minqp.minqpreport();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private minqp.minqpreport _innerobj;
        public minqp.minqpreport innerobj { get { return _innerobj; } }
        public minqpreport(minqp.minqpreport obj)
        {
            _innerobj = obj;
        }
    }

    /*************************************************************************
                        CONSTRAINED QUADRATIC PROGRAMMING

    The subroutine creates QP optimizer. After initial creation,  it  contains
    default optimization problem with zero quadratic and linear terms  and  no
    constraints. You should set quadratic/linear terms with calls to functions
    provided by MinQP subpackage.

    INPUT PARAMETERS:
        N       -   problem size

    OUTPUT PARAMETERS:
        State   -   optimizer with zero quadratic/linear terms
                    and no constraints

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpcreate(int n, out minqpstate state)
    {
        state = new minqpstate();
        minqp.minqpcreate(n, state.innerobj);
        return;
    }

    /*************************************************************************
    This function sets linear term for QP solver.

    By default, linear term is zero.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        B       -   linear term, array[N].

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpsetlinearterm(minqpstate state, double[] b)
    {

        minqp.minqpsetlinearterm(state.innerobj, b);
        return;
    }

    /*************************************************************************
    This function sets quadratic term for QP solver.

    By default quadratic term is zero.

    IMPORTANT: this solver minimizes following  function:
        f(x) = 0.5*x'*A*x + b'*x.
    Note that quadratic term has 0.5 before it. So if  you  want  to  minimize
        f(x) = x^2 + x
    you should rewrite your problem as follows:
        f(x) = 0.5*(2*x^2) + x
    and your matrix A will be equal to [[2.0]], not to [[1.0]]

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        A       -   matrix, array[N,N]
        IsUpper -   (optional) storage type:
                    * if True, symmetric matrix  A  is  given  by  its  upper
                      triangle, and the lower triangle isn’t used
                    * if False, symmetric matrix  A  is  given  by  its lower
                      triangle, and the upper triangle isn’t used
                    * if not given, both lower and upper  triangles  must  be
                      filled.

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpsetquadraticterm(minqpstate state, double[,] a, bool isupper)
    {

        minqp.minqpsetquadraticterm(state.innerobj, a, isupper);
        return;
    }
    public static void minqpsetquadraticterm(minqpstate state, double[,] a)
    {
        bool isupper;
        if( !alglib.ap.issymmetric(a) )
            throw new alglibexception("'a' parameter is not symmetric matrix");

        isupper = false;
        minqp.minqpsetquadraticterm(state.innerobj, a, isupper);

        return;
    }

    /*************************************************************************
    This function sets starting point for QP solver. It is useful to have
    good initial approximation to the solution, because it will increase
    speed of convergence and identification of active constraints.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        X       -   starting point, array[N].

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpsetstartingpoint(minqpstate state, double[] x)
    {

        minqp.minqpsetstartingpoint(state.innerobj, x);
        return;
    }

    /*************************************************************************
    This  function sets origin for QP solver. By default, following QP program
    is solved:

        min(0.5*x'*A*x+b'*x)

    This function allows to solve different problem:

        min(0.5*(x-x_origin)'*A*(x-x_origin)+b'*(x-x_origin))

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        XOrigin -   origin, array[N].

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpsetorigin(minqpstate state, double[] xorigin)
    {

        minqp.minqpsetorigin(state.innerobj, xorigin);
        return;
    }

    /*************************************************************************
    This function tells solver to use Cholesky-based algorithm.

    Cholesky-based algorithm can be used when:
    * problem is convex
    * there is no constraints or only boundary constraints are present

    This algorithm has O(N^3) complexity for unconstrained problem and  is  up
    to several times slower on bound constrained  problems  (these  additional
    iterations are needed to identify active constraints).

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpsetalgocholesky(minqpstate state)
    {

        minqp.minqpsetalgocholesky(state.innerobj);
        return;
    }

    /*************************************************************************
    This function sets boundary constraints for QP solver

    Boundary constraints are inactive by default (after initial creation).
    After  being  set,  they  are  preserved  until explicitly turned off with
    another SetBC() call.

    INPUT PARAMETERS:
        State   -   structure stores algorithm state
        BndL    -   lower bounds, array[N].
                    If some (all) variables are unbounded, you may specify
                    very small number or -INF (latter is recommended because
                    it will allow solver to use better algorithm).
        BndU    -   upper bounds, array[N].
                    If some (all) variables are unbounded, you may specify
                    very large number or +INF (latter is recommended because
                    it will allow solver to use better algorithm).

    NOTE: it is possible to specify BndL[i]=BndU[i]. In this case I-th
    variable will be "frozen" at X[i]=BndL[i]=BndU[i].

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpsetbc(minqpstate state, double[] bndl, double[] bndu)
    {

        minqp.minqpsetbc(state.innerobj, bndl, bndu);
        return;
    }

    /*************************************************************************
    This function sets linear constraints for QP optimizer.

    Linear constraints are inactive by default (after initial creation).

    INPUT PARAMETERS:
        State   -   structure previously allocated with MinQPCreate call.
        C       -   linear constraints, array[K,N+1].
                    Each row of C represents one constraint, either equality
                    or inequality (see below):
                    * first N elements correspond to coefficients,
                    * last element corresponds to the right part.
                    All elements of C (including right part) must be finite.
        CT      -   type of constraints, array[K]:
                    * if CT[i]>0, then I-th constraint is C[i,*]*x >= C[i,n+1]
                    * if CT[i]=0, then I-th constraint is C[i,*]*x  = C[i,n+1]
                    * if CT[i]<0, then I-th constraint is C[i,*]*x <= C[i,n+1]
        K       -   number of equality/inequality constraints, K>=0:
                    * if given, only leading K elements of C/CT are used
                    * if not given, automatically determined from sizes of C/CT

    NOTE 1: linear (non-bound) constraints are satisfied only approximately  -
            there always exists some minor violation (about 10^10...10^13) due
            to rounding errors.

      -- ALGLIB --
         Copyright 19.06.2012 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpsetlc(minqpstate state, double[,] c, int[] ct, int k)
    {

        minqp.minqpsetlc(state.innerobj, c, ct, k);
        return;
    }
    public static void minqpsetlc(minqpstate state, double[,] c, int[] ct)
    {
        int k;
        if( (ap.rows(c)!=ap.len(ct)))
            throw new alglibexception("Error while calling 'minqpsetlc': looks like one of arguments has wrong size");

        k = ap.rows(c);
        minqp.minqpsetlc(state.innerobj, c, ct, k);

        return;
    }

    /*************************************************************************
    This function solves quadratic programming problem.
    You should call it after setting solver options with MinQPSet...() calls.

    INPUT PARAMETERS:
        State   -   algorithm state

    You should use MinQPResults() function to access results after calls
    to this function.

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpoptimize(minqpstate state)
    {

        minqp.minqpoptimize(state.innerobj);
        return;
    }

    /*************************************************************************
    QP solver results

    INPUT PARAMETERS:
        State   -   algorithm state

    OUTPUT PARAMETERS:
        X       -   array[0..N-1], solution
        Rep     -   optimization report. You should check Rep.TerminationType,
                    which contains completion code, and you may check  another
                    fields which contain another information  about  algorithm
                    functioning.

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpresults(minqpstate state, out double[] x, out minqpreport rep)
    {
        x = new double[0];
        rep = new minqpreport();
        minqp.minqpresults(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    QP results

    Buffered implementation of MinQPResults() which uses pre-allocated  buffer
    to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
    intended to be used in the inner cycles of performance critical algorithms
    where array reallocation penalty is too large to be ignored.

      -- ALGLIB --
         Copyright 11.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minqpresultsbuf(minqpstate state, ref double[] x, minqpreport rep)
    {

        minqp.minqpresultsbuf(state.innerobj, ref x, rep.innerobj);
        return;
    }

}
public partial class alglib
{


    /*************************************************************************
    Levenberg-Marquardt optimizer.

    This structure should be created using one of the MinLMCreate???()
    functions. You should not access its fields directly; use ALGLIB functions
    to work with it.
    *************************************************************************/
    public class minlmstate
    {
        //
        // Public declarations
        //
        public bool needf { get { return _innerobj.needf; } set { _innerobj.needf = value; } }
        public bool needfg { get { return _innerobj.needfg; } set { _innerobj.needfg = value; } }
        public bool needfgh { get { return _innerobj.needfgh; } set { _innerobj.needfgh = value; } }
        public bool needfi { get { return _innerobj.needfi; } set { _innerobj.needfi = value; } }
        public bool needfij { get { return _innerobj.needfij; } set { _innerobj.needfij = value; } }
        public bool xupdated { get { return _innerobj.xupdated; } set { _innerobj.xupdated = value; } }
        public double f { get { return _innerobj.f; } set { _innerobj.f = value; } }
        public double[] fi { get { return _innerobj.fi; } }
        public double[] g { get { return _innerobj.g; } }
        public double[,] h { get { return _innerobj.h; } }
        public double[,] j { get { return _innerobj.j; } }
        public double[] x { get { return _innerobj.x; } }

        public minlmstate()
        {
            _innerobj = new minlm.minlmstate();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private minlm.minlmstate _innerobj;
        public minlm.minlmstate innerobj { get { return _innerobj; } }
        public minlmstate(minlm.minlmstate obj)
        {
            _innerobj = obj;
        }
    }


    /*************************************************************************
    Optimization report, filled by MinLMResults() function

    FIELDS:
    * TerminationType, completetion code:
        * -7    derivative correctness check failed;
                see Rep.WrongNum, Rep.WrongI, Rep.WrongJ for
                more information.
        *  1    relative function improvement is no more than
                EpsF.
        *  2    relative step is no more than EpsX.
        *  4    gradient is no more than EpsG.
        *  5    MaxIts steps was taken
        *  7    stopping conditions are too stringent,
                further improvement is impossible
    * IterationsCount, contains iterations count
    * NFunc, number of function calculations
    * NJac, number of Jacobi matrix calculations
    * NGrad, number of gradient calculations
    * NHess, number of Hessian calculations
    * NCholesky, number of Cholesky decomposition calculations
    *************************************************************************/
    public class minlmreport
    {
        //
        // Public declarations
        //
        public int iterationscount { get { return _innerobj.iterationscount; } set { _innerobj.iterationscount = value; } }
        public int terminationtype { get { return _innerobj.terminationtype; } set { _innerobj.terminationtype = value; } }
        public int funcidx { get { return _innerobj.funcidx; } set { _innerobj.funcidx = value; } }
        public int varidx { get { return _innerobj.varidx; } set { _innerobj.varidx = value; } }
        public int nfunc { get { return _innerobj.nfunc; } set { _innerobj.nfunc = value; } }
        public int njac { get { return _innerobj.njac; } set { _innerobj.njac = value; } }
        public int ngrad { get { return _innerobj.ngrad; } set { _innerobj.ngrad = value; } }
        public int nhess { get { return _innerobj.nhess; } set { _innerobj.nhess = value; } }
        public int ncholesky { get { return _innerobj.ncholesky; } set { _innerobj.ncholesky = value; } }

        public minlmreport()
        {
            _innerobj = new minlm.minlmreport();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private minlm.minlmreport _innerobj;
        public minlm.minlmreport innerobj { get { return _innerobj; } }
        public minlmreport(minlm.minlmreport obj)
        {
            _innerobj = obj;
        }
    }

    /*************************************************************************
                    IMPROVED LEVENBERG-MARQUARDT METHOD FOR
                     NON-LINEAR LEAST SQUARES OPTIMIZATION

    DESCRIPTION:
    This function is used to find minimum of function which is represented  as
    sum of squares:
        F(x) = f[0]^2(x[0],...,x[n-1]) + ... + f[m-1]^2(x[0],...,x[n-1])
    using value of function vector f[] and Jacobian of f[].


    REQUIREMENTS:
    This algorithm will request following information during its operation:

    * function vector f[] at given point X
    * function vector f[] and Jacobian of f[] (simultaneously) at given point

    There are several overloaded versions of  MinLMOptimize()  function  which
    correspond  to  different LM-like optimization algorithms provided by this
    unit. You should choose version which accepts fvec()  and jac() callbacks.
    First  one  is used to calculate f[] at given point, second one calculates
    f[] and Jacobian df[i]/dx[j].

    You can try to initialize MinLMState structure with VJ  function and  then
    use incorrect version  of  MinLMOptimize()  (for  example,  version  which
    works  with  general  form function and does not provide Jacobian), but it
    will  lead  to  exception  being  thrown  after first attempt to calculate
    Jacobian.


    USAGE:
    1. User initializes algorithm state with MinLMCreateVJ() call
    2. User tunes solver parameters with MinLMSetCond(),  MinLMSetStpMax() and
       other functions
    3. User calls MinLMOptimize() function which  takes algorithm  state   and
       callback functions.
    4. User calls MinLMResults() to get solution
    5. Optionally, user may call MinLMRestartFrom() to solve  another  problem
       with same N/M but another starting point and/or another function.
       MinLMRestartFrom() allows to reuse already initialized structure.


    INPUT PARAMETERS:
        N       -   dimension, N>1
                    * if given, only leading N elements of X are used
                    * if not given, automatically determined from size of X
        M       -   number of functions f[i]
        X       -   initial solution, array[0..N-1]

    OUTPUT PARAMETERS:
        State   -   structure which stores algorithm state

    NOTES:
    1. you may tune stopping conditions with MinLMSetCond() function
    2. if target function contains exp() or other fast growing functions,  and
       optimization algorithm makes too large steps which leads  to  overflow,
       use MinLMSetStpMax() function to bound algorithm's steps.

      -- ALGLIB --
         Copyright 30.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmcreatevj(int n, int m, double[] x, out minlmstate state)
    {
        state = new minlmstate();
        minlm.minlmcreatevj(n, m, x, state.innerobj);
        return;
    }
    public static void minlmcreatevj(int m, double[] x, out minlmstate state)
    {
        int n;

        state = new minlmstate();
        n = ap.len(x);
        minlm.minlmcreatevj(n, m, x, state.innerobj);

        return;
    }

    /*************************************************************************
                    IMPROVED LEVENBERG-MARQUARDT METHOD FOR
                     NON-LINEAR LEAST SQUARES OPTIMIZATION

    DESCRIPTION:
    This function is used to find minimum of function which is represented  as
    sum of squares:
        F(x) = f[0]^2(x[0],...,x[n-1]) + ... + f[m-1]^2(x[0],...,x[n-1])
    using value of function vector f[] only. Finite differences  are  used  to
    calculate Jacobian.


    REQUIREMENTS:
    This algorithm will request following information during its operation:
    * function vector f[] at given point X

    There are several overloaded versions of  MinLMOptimize()  function  which
    correspond  to  different LM-like optimization algorithms provided by this
    unit. You should choose version which accepts fvec() callback.

    You can try to initialize MinLMState structure with VJ  function and  then
    use incorrect version  of  MinLMOptimize()  (for  example,  version  which
    works with general form function and does not accept function vector), but
    it will  lead  to  exception being thrown after first attempt to calculate
    Jacobian.


    USAGE:
    1. User initializes algorithm state with MinLMCreateV() call
    2. User tunes solver parameters with MinLMSetCond(),  MinLMSetStpMax() and
       other functions
    3. User calls MinLMOptimize() function which  takes algorithm  state   and
       callback functions.
    4. User calls MinLMResults() to get solution
    5. Optionally, user may call MinLMRestartFrom() to solve  another  problem
       with same N/M but another starting point and/or another function.
       MinLMRestartFrom() allows to reuse already initialized structure.


    INPUT PARAMETERS:
        N       -   dimension, N>1
                    * if given, only leading N elements of X are used
                    * if not given, automatically determined from size of X
        M       -   number of functions f[i]
        X       -   initial solution, array[0..N-1]
        DiffStep-   differentiation step, >0

    OUTPUT PARAMETERS:
        State   -   structure which stores algorithm state

    See also MinLMIteration, MinLMResults.

    NOTES:
    1. you may tune stopping conditions with MinLMSetCond() function
    2. if target function contains exp() or other fast growing functions,  and
       optimization algorithm makes too large steps which leads  to  overflow,
       use MinLMSetStpMax() function to bound algorithm's steps.

      -- ALGLIB --
         Copyright 30.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmcreatev(int n, int m, double[] x, double diffstep, out minlmstate state)
    {
        state = new minlmstate();
        minlm.minlmcreatev(n, m, x, diffstep, state.innerobj);
        return;
    }
    public static void minlmcreatev(int m, double[] x, double diffstep, out minlmstate state)
    {
        int n;

        state = new minlmstate();
        n = ap.len(x);
        minlm.minlmcreatev(n, m, x, diffstep, state.innerobj);

        return;
    }

    /*************************************************************************
        LEVENBERG-MARQUARDT-LIKE METHOD FOR NON-LINEAR OPTIMIZATION

    DESCRIPTION:
    This  function  is  used  to  find  minimum  of general form (not "sum-of-
    -squares") function
        F = F(x[0], ..., x[n-1])
    using  its  gradient  and  Hessian.  Levenberg-Marquardt modification with
    L-BFGS pre-optimization and internal pre-conditioned  L-BFGS  optimization
    after each Levenberg-Marquardt step is used.


    REQUIREMENTS:
    This algorithm will request following information during its operation:

    * function value F at given point X
    * F and gradient G (simultaneously) at given point X
    * F, G and Hessian H (simultaneously) at given point X

    There are several overloaded versions of  MinLMOptimize()  function  which
    correspond  to  different LM-like optimization algorithms provided by this
    unit. You should choose version which accepts func(),  grad()  and  hess()
    function pointers. First pointer is used to calculate F  at  given  point,
    second  one  calculates  F(x)  and  grad F(x),  third one calculates F(x),
    grad F(x), hess F(x).

    You can try to initialize MinLMState structure with FGH-function and  then
    use incorrect version of MinLMOptimize() (for example, version which  does
    not provide Hessian matrix), but it will lead to  exception  being  thrown
    after first attempt to calculate Hessian.


    USAGE:
    1. User initializes algorithm state with MinLMCreateFGH() call
    2. User tunes solver parameters with MinLMSetCond(),  MinLMSetStpMax() and
       other functions
    3. User calls MinLMOptimize() function which  takes algorithm  state   and
       pointers (delegates, etc.) to callback functions.
    4. User calls MinLMResults() to get solution
    5. Optionally, user may call MinLMRestartFrom() to solve  another  problem
       with same N but another starting point and/or another function.
       MinLMRestartFrom() allows to reuse already initialized structure.


    INPUT PARAMETERS:
        N       -   dimension, N>1
                    * if given, only leading N elements of X are used
                    * if not given, automatically determined from size of X
        X       -   initial solution, array[0..N-1]

    OUTPUT PARAMETERS:
        State   -   structure which stores algorithm state

    NOTES:
    1. you may tune stopping conditions with MinLMSetCond() function
    2. if target function contains exp() or other fast growing functions,  and
       optimization algorithm makes too large steps which leads  to  overflow,
       use MinLMSetStpMax() function to bound algorithm's steps.

      -- ALGLIB --
         Copyright 30.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmcreatefgh(int n, double[] x, out minlmstate state)
    {
        state = new minlmstate();
        minlm.minlmcreatefgh(n, x, state.innerobj);
        return;
    }
    public static void minlmcreatefgh(double[] x, out minlmstate state)
    {
        int n;

        state = new minlmstate();
        n = ap.len(x);
        minlm.minlmcreatefgh(n, x, state.innerobj);

        return;
    }

    /*************************************************************************
    This function sets stopping conditions for Levenberg-Marquardt optimization
    algorithm.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        EpsG    -   >=0
                    The  subroutine  finishes  its  work   if   the  condition
                    |v|<EpsG is satisfied, where:
                    * |.| means Euclidian norm
                    * v - scaled gradient vector, v[i]=g[i]*s[i]
                    * g - gradient
                    * s - scaling coefficients set by MinLMSetScale()
        EpsF    -   >=0
                    The  subroutine  finishes  its work if on k+1-th iteration
                    the  condition  |F(k+1)-F(k)|<=EpsF*max{|F(k)|,|F(k+1)|,1}
                    is satisfied.
        EpsX    -   >=0
                    The subroutine finishes its work if  on  k+1-th  iteration
                    the condition |v|<=EpsX is fulfilled, where:
                    * |.| means Euclidian norm
                    * v - scaled step vector, v[i]=dx[i]/s[i]
                    * dx - ste pvector, dx=X(k+1)-X(k)
                    * s - scaling coefficients set by MinLMSetScale()
        MaxIts  -   maximum number of iterations. If MaxIts=0, the  number  of
                    iterations   is    unlimited.   Only   Levenberg-Marquardt
                    iterations  are  counted  (L-BFGS/CG  iterations  are  NOT
                    counted because their cost is very low compared to that of
                    LM).

    Passing EpsG=0, EpsF=0, EpsX=0 and MaxIts=0 (simultaneously) will lead to
    automatic stopping criterion selection (small EpsX).

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmsetcond(minlmstate state, double epsg, double epsf, double epsx, int maxits)
    {

        minlm.minlmsetcond(state.innerobj, epsg, epsf, epsx, maxits);
        return;
    }

    /*************************************************************************
    This function turns on/off reporting.

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        NeedXRep-   whether iteration reports are needed or not

    If NeedXRep is True, algorithm will call rep() callback function if  it is
    provided to MinLMOptimize(). Both Levenberg-Marquardt and internal  L-BFGS
    iterations are reported.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmsetxrep(minlmstate state, bool needxrep)
    {

        minlm.minlmsetxrep(state.innerobj, needxrep);
        return;
    }

    /*************************************************************************
    This function sets maximum step length

    INPUT PARAMETERS:
        State   -   structure which stores algorithm state
        StpMax  -   maximum step length, >=0. Set StpMax to 0.0,  if you don't
                    want to limit step length.

    Use this subroutine when you optimize target function which contains exp()
    or  other  fast  growing  functions,  and optimization algorithm makes too
    large  steps  which  leads  to overflow. This function allows us to reject
    steps  that  are  too  large  (and  therefore  expose  us  to the possible
    overflow) without actually calculating function value at the x+stp*d.

    NOTE: non-zero StpMax leads to moderate  performance  degradation  because
    intermediate  step  of  preconditioned L-BFGS optimization is incompatible
    with limits on step size.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmsetstpmax(minlmstate state, double stpmax)
    {

        minlm.minlmsetstpmax(state.innerobj, stpmax);
        return;
    }

    /*************************************************************************
    This function sets scaling coefficients for LM optimizer.

    ALGLIB optimizers use scaling matrices to test stopping  conditions  (step
    size and gradient are scaled before comparison with tolerances).  Scale of
    the I-th variable is a translation invariant measure of:
    a) "how large" the variable is
    b) how large the step should be to make significant changes in the function

    Generally, scale is NOT considered to be a form of preconditioner.  But LM
    optimizer is unique in that it uses scaling matrix both  in  the  stopping
    condition tests and as Marquardt damping factor.

    Proper scaling is very important for the algorithm performance. It is less
    important for the quality of results, but still has some influence (it  is
    easier  to  converge  when  variables  are  properly  scaled, so premature
    stopping is possible when very badly scalled variables are  combined  with
    relaxed stopping conditions).

    INPUT PARAMETERS:
        State   -   structure stores algorithm state
        S       -   array[N], non-zero scaling coefficients
                    S[i] may be negative, sign doesn't matter.

      -- ALGLIB --
         Copyright 14.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmsetscale(minlmstate state, double[] s)
    {

        minlm.minlmsetscale(state.innerobj, s);
        return;
    }

    /*************************************************************************
    This function sets boundary constraints for LM optimizer

    Boundary constraints are inactive by default (after initial creation).
    They are preserved until explicitly turned off with another SetBC() call.

    INPUT PARAMETERS:
        State   -   structure stores algorithm state
        BndL    -   lower bounds, array[N].
                    If some (all) variables are unbounded, you may specify
                    very small number or -INF (latter is recommended because
                    it will allow solver to use better algorithm).
        BndU    -   upper bounds, array[N].
                    If some (all) variables are unbounded, you may specify
                    very large number or +INF (latter is recommended because
                    it will allow solver to use better algorithm).

    NOTE 1: it is possible to specify BndL[i]=BndU[i]. In this case I-th
    variable will be "frozen" at X[i]=BndL[i]=BndU[i].

    NOTE 2: this solver has following useful properties:
    * bound constraints are always satisfied exactly
    * function is evaluated only INSIDE area specified by bound constraints
      or at its boundary

      -- ALGLIB --
         Copyright 14.01.2011 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmsetbc(minlmstate state, double[] bndl, double[] bndu)
    {

        minlm.minlmsetbc(state.innerobj, bndl, bndu);
        return;
    }

    /*************************************************************************
    This function is used to change acceleration settings

    You can choose between three acceleration strategies:
    * AccType=0, no acceleration.
    * AccType=1, secant updates are used to update quadratic model after  each
      iteration. After fixed number of iterations (or after  model  breakdown)
      we  recalculate  quadratic  model  using  analytic  Jacobian  or  finite
      differences. Number of secant-based iterations depends  on  optimization
      settings: about 3 iterations - when we have analytic Jacobian, up to 2*N
      iterations - when we use finite differences to calculate Jacobian.

    AccType=1 is recommended when Jacobian  calculation  cost  is  prohibitive
    high (several Mx1 function vector calculations  followed  by  several  NxN
    Cholesky factorizations are faster than calculation of one M*N  Jacobian).
    It should also be used when we have no Jacobian, because finite difference
    approximation takes too much time to compute.

    Table below list  optimization  protocols  (XYZ  protocol  corresponds  to
    MinLMCreateXYZ) and acceleration types they support (and use by  default).

    ACCELERATION TYPES SUPPORTED BY OPTIMIZATION PROTOCOLS:

    protocol    0   1   comment
    V           +   +
    VJ          +   +
    FGH         +

    DAFAULT VALUES:

    protocol    0   1   comment
    V               x   without acceleration it is so slooooooooow
    VJ          x
    FGH         x

    NOTE: this  function should be called before optimization. Attempt to call
    it during algorithm iterations may result in unexpected behavior.

    NOTE: attempt to call this function with unsupported protocol/acceleration
    combination will result in exception being thrown.

      -- ALGLIB --
         Copyright 14.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmsetacctype(minlmstate state, int acctype)
    {

        minlm.minlmsetacctype(state.innerobj, acctype);
        return;
    }

    /*************************************************************************
    This function provides reverse communication interface
    Reverse communication interface is not documented or recommended to use.
    See below for functions which provide better documented API
    *************************************************************************/
    public static bool minlmiteration(minlmstate state)
    {

        bool result = minlm.minlmiteration(state.innerobj);
        return result;
    }
    /*************************************************************************
    This family of functions is used to launcn iterations of nonlinear optimizer

    These functions accept following parameters:
        func    -   callback which calculates function (or merit function)
                    value func at given point x
        grad    -   callback which calculates function (or merit function)
                    value func and gradient grad at given point x
        hess    -   callback which calculates function (or merit function)
                    value func, gradient grad and Hessian hess at given point x
        fvec    -   callback which calculates function vector fi[]
                    at given point x
        jac     -   callback which calculates function vector fi[]
                    and Jacobian jac at given point x
        rep     -   optional callback which is called after each iteration
                    can be null
        obj     -   optional object which is passed to func/grad/hess/jac/rep
                    can be null

    NOTES:

    1. Depending on function used to create state  structure,  this  algorithm
       may accept Jacobian and/or Hessian and/or gradient.  According  to  the
       said above, there ase several versions of this function,  which  accept
       different sets of callbacks.

       This flexibility opens way to subtle errors - you may create state with
       MinLMCreateFGH() (optimization using Hessian), but call function  which
       does not accept Hessian. So when algorithm will request Hessian,  there
       will be no callback to call. In this case exception will be thrown.

       Be careful to avoid such errors because there is no way to find them at
       compile time - you can see them at runtime only.

      -- ALGLIB --
         Copyright 10.03.2009 by Bochkanov Sergey

    *************************************************************************/
    public static void minlmoptimize(minlmstate state, ndimensional_fvec  fvec, ndimensional_rep rep, object obj)
    {
        if( fvec==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (fvec is null)");
        while( alglib.minlmiteration(state) )
        {
            if( state.needfi )
            {
                fvec(state.x, state.innerobj.fi, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minlmoptimize' (some derivatives were not provided?)");
        }
    }


    public static void minlmoptimize(minlmstate state, ndimensional_fvec  fvec, ndimensional_jac  jac, ndimensional_rep rep, object obj)
    {
        if( fvec==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (fvec is null)");
        if( jac==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (jac is null)");
        while( alglib.minlmiteration(state) )
        {
            if( state.needfi )
            {
                fvec(state.x, state.innerobj.fi, obj);
                continue;
            }
            if( state.needfij )
            {
                jac(state.x, state.innerobj.fi, state.innerobj.j, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minlmoptimize' (some derivatives were not provided?)");
        }
    }


    public static void minlmoptimize(minlmstate state, ndimensional_func func, ndimensional_grad grad, ndimensional_hess hess, ndimensional_rep rep, object obj)
    {
        if( func==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (func is null)");
        if( grad==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (grad is null)");
        if( hess==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (hess is null)");
        while( alglib.minlmiteration(state) )
        {
            if( state.needf )
            {
                func(state.x, ref state.innerobj.f, obj);
                continue;
            }
            if( state.needfg )
            {
                grad(state.x, ref state.innerobj.f, state.innerobj.g, obj);
                continue;
            }
            if( state.needfgh )
            {
                hess(state.x, ref state.innerobj.f, state.innerobj.g, state.innerobj.h, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minlmoptimize' (some derivatives were not provided?)");
        }
    }


    public static void minlmoptimize(minlmstate state, ndimensional_func func, ndimensional_jac  jac, ndimensional_rep rep, object obj)
    {
        if( func==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (func is null)");
        if( jac==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (jac is null)");
        while( alglib.minlmiteration(state) )
        {
            if( state.needf )
            {
                func(state.x, ref state.innerobj.f, obj);
                continue;
            }
            if( state.needfij )
            {
                jac(state.x, state.innerobj.fi, state.innerobj.j, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minlmoptimize' (some derivatives were not provided?)");
        }
    }


    public static void minlmoptimize(minlmstate state, ndimensional_func func, ndimensional_grad grad, ndimensional_jac  jac, ndimensional_rep rep, object obj)
    {
        if( func==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (func is null)");
        if( grad==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (grad is null)");
        if( jac==null )
            throw new alglibexception("ALGLIB: error in 'minlmoptimize()' (jac is null)");
        while( alglib.minlmiteration(state) )
        {
            if( state.needf )
            {
                func(state.x, ref state.innerobj.f, obj);
                continue;
            }
            if( state.needfg )
            {
                grad(state.x, ref state.innerobj.f, state.innerobj.g, obj);
                continue;
            }
            if( state.needfij )
            {
                jac(state.x, state.innerobj.fi, state.innerobj.j, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minlmoptimize' (some derivatives were not provided?)");
        }
    }



    /*************************************************************************
    Levenberg-Marquardt algorithm results

    INPUT PARAMETERS:
        State   -   algorithm state

    OUTPUT PARAMETERS:
        X       -   array[0..N-1], solution
        Rep     -   optimization report;
                    see comments for this structure for more info.

      -- ALGLIB --
         Copyright 10.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmresults(minlmstate state, out double[] x, out minlmreport rep)
    {
        x = new double[0];
        rep = new minlmreport();
        minlm.minlmresults(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    Levenberg-Marquardt algorithm results

    Buffered implementation of MinLMResults(), which uses pre-allocated buffer
    to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
    intended to be used in the inner cycles of performance critical algorithms
    where array reallocation penalty is too large to be ignored.

      -- ALGLIB --
         Copyright 10.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmresultsbuf(minlmstate state, ref double[] x, minlmreport rep)
    {

        minlm.minlmresultsbuf(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    This  subroutine  restarts  LM  algorithm from new point. All optimization
    parameters are left unchanged.

    This  function  allows  to  solve multiple  optimization  problems  (which
    must have same number of dimensions) without object reallocation penalty.

    INPUT PARAMETERS:
        State   -   structure used for reverse communication previously
                    allocated with MinLMCreateXXX call.
        X       -   new starting point.

      -- ALGLIB --
         Copyright 30.07.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmrestartfrom(minlmstate state, double[] x)
    {

        minlm.minlmrestartfrom(state.innerobj, x);
        return;
    }

    /*************************************************************************
    This is obsolete function.

    Since ALGLIB 3.3 it is equivalent to MinLMCreateVJ().

      -- ALGLIB --
         Copyright 30.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmcreatevgj(int n, int m, double[] x, out minlmstate state)
    {
        state = new minlmstate();
        minlm.minlmcreatevgj(n, m, x, state.innerobj);
        return;
    }
    public static void minlmcreatevgj(int m, double[] x, out minlmstate state)
    {
        int n;

        state = new minlmstate();
        n = ap.len(x);
        minlm.minlmcreatevgj(n, m, x, state.innerobj);

        return;
    }

    /*************************************************************************
    This is obsolete function.

    Since ALGLIB 3.3 it is equivalent to MinLMCreateFJ().

      -- ALGLIB --
         Copyright 30.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmcreatefgj(int n, int m, double[] x, out minlmstate state)
    {
        state = new minlmstate();
        minlm.minlmcreatefgj(n, m, x, state.innerobj);
        return;
    }
    public static void minlmcreatefgj(int m, double[] x, out minlmstate state)
    {
        int n;

        state = new minlmstate();
        n = ap.len(x);
        minlm.minlmcreatefgj(n, m, x, state.innerobj);

        return;
    }

    /*************************************************************************
    This function is considered obsolete since ALGLIB 3.1.0 and is present for
    backward  compatibility  only.  We  recommend  to use MinLMCreateVJ, which
    provides similar, but more consistent and feature-rich interface.

      -- ALGLIB --
         Copyright 30.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmcreatefj(int n, int m, double[] x, out minlmstate state)
    {
        state = new minlmstate();
        minlm.minlmcreatefj(n, m, x, state.innerobj);
        return;
    }
    public static void minlmcreatefj(int m, double[] x, out minlmstate state)
    {
        int n;

        state = new minlmstate();
        n = ap.len(x);
        minlm.minlmcreatefj(n, m, x, state.innerobj);

        return;
    }

    /*************************************************************************
    This  subroutine  turns  on  verification  of  the  user-supplied analytic
    gradient:
    * user calls this subroutine before optimization begins
    * MinLMOptimize() is called
    * prior to actual optimization, for  each  function Fi and each  component
      of parameters  being  optimized X[j] algorithm performs following steps:
      * two trial steps are made to X[j]-TestStep*S[j] and X[j]+TestStep*S[j],
        where X[j] is j-th parameter and S[j] is a scale of j-th parameter
      * if needed, steps are bounded with respect to constraints on X[]
      * Fi(X) is evaluated at these trial points
      * we perform one more evaluation in the middle point of the interval
      * we  build  cubic  model using function values and derivatives at trial
        points and we compare its prediction with actual value in  the  middle
        point
      * in case difference between prediction and actual value is higher  than
        some predetermined threshold, algorithm stops with completion code -7;
        Rep.VarIdx is set to index of the parameter with incorrect derivative,
        Rep.FuncIdx is set to index of the function.
    * after verification is over, algorithm proceeds to the actual optimization.

    NOTE 1: verification  needs  N (parameters count) Jacobian evaluations. It
            is  very  costly  and  you  should use it only for low dimensional
            problems,  when  you  want  to  be  sure  that  you've   correctly
            calculated  analytic  derivatives.  You should not  use  it in the
            production code  (unless  you  want  to check derivatives provided
            by some third party).

    NOTE 2: you  should  carefully  choose  TestStep. Value which is too large
            (so large that function behaviour is significantly non-cubic) will
            lead to false alarms. You may use  different  step  for  different
            parameters by means of setting scale with MinLMSetScale().

    NOTE 3: this function may lead to false positives. In case it reports that
            I-th  derivative was calculated incorrectly, you may decrease test
            step  and  try  one  more  time  - maybe your function changes too
            sharply  and  your  step  is  too  large for such rapidly chanding
            function.

    INPUT PARAMETERS:
        State       -   structure used to store algorithm state
        TestStep    -   verification step:
                        * TestStep=0 turns verification off
                        * TestStep>0 activates verification

      -- ALGLIB --
         Copyright 15.06.2012 by Bochkanov Sergey
    *************************************************************************/
    public static void minlmsetgradientcheck(minlmstate state, double teststep)
    {

        minlm.minlmsetgradientcheck(state.innerobj, teststep);
        return;
    }

}
public partial class alglib
{


    /*************************************************************************

    *************************************************************************/
    public class minasastate
    {
        //
        // Public declarations
        //
        public bool needfg { get { return _innerobj.needfg; } set { _innerobj.needfg = value; } }
        public bool xupdated { get { return _innerobj.xupdated; } set { _innerobj.xupdated = value; } }
        public double f { get { return _innerobj.f; } set { _innerobj.f = value; } }
        public double[] g { get { return _innerobj.g; } }
        public double[] x { get { return _innerobj.x; } }

        public minasastate()
        {
            _innerobj = new mincomp.minasastate();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private mincomp.minasastate _innerobj;
        public mincomp.minasastate innerobj { get { return _innerobj; } }
        public minasastate(mincomp.minasastate obj)
        {
            _innerobj = obj;
        }
    }


    /*************************************************************************

    *************************************************************************/
    public class minasareport
    {
        //
        // Public declarations
        //
        public int iterationscount { get { return _innerobj.iterationscount; } set { _innerobj.iterationscount = value; } }
        public int nfev { get { return _innerobj.nfev; } set { _innerobj.nfev = value; } }
        public int terminationtype { get { return _innerobj.terminationtype; } set { _innerobj.terminationtype = value; } }
        public int activeconstraints { get { return _innerobj.activeconstraints; } set { _innerobj.activeconstraints = value; } }

        public minasareport()
        {
            _innerobj = new mincomp.minasareport();
        }

        //
        // Although some of declarations below are public, you should not use them
        // They are intended for internal use only
        //
        private mincomp.minasareport _innerobj;
        public mincomp.minasareport innerobj { get { return _innerobj; } }
        public minasareport(mincomp.minasareport obj)
        {
            _innerobj = obj;
        }
    }

    /*************************************************************************
    Obsolete function, use MinLBFGSSetPrecDefault() instead.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetdefaultpreconditioner(minlbfgsstate state)
    {

        mincomp.minlbfgssetdefaultpreconditioner(state.innerobj);
        return;
    }

    /*************************************************************************
    Obsolete function, use MinLBFGSSetCholeskyPreconditioner() instead.

      -- ALGLIB --
         Copyright 13.10.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minlbfgssetcholeskypreconditioner(minlbfgsstate state, double[,] p, bool isupper)
    {

        mincomp.minlbfgssetcholeskypreconditioner(state.innerobj, p, isupper);
        return;
    }

    /*************************************************************************
    This is obsolete function which was used by previous version of the  BLEIC
    optimizer. It does nothing in the current version of BLEIC.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetbarrierwidth(minbleicstate state, double mu)
    {

        mincomp.minbleicsetbarrierwidth(state.innerobj, mu);
        return;
    }

    /*************************************************************************
    This is obsolete function which was used by previous version of the  BLEIC
    optimizer. It does nothing in the current version of BLEIC.

      -- ALGLIB --
         Copyright 28.11.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minbleicsetbarrierdecay(minbleicstate state, double mudecay)
    {

        mincomp.minbleicsetbarrierdecay(state.innerobj, mudecay);
        return;
    }

    /*************************************************************************
    Obsolete optimization algorithm.
    Was replaced by MinBLEIC subpackage.

      -- ALGLIB --
         Copyright 25.03.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minasacreate(int n, double[] x, double[] bndl, double[] bndu, out minasastate state)
    {
        state = new minasastate();
        mincomp.minasacreate(n, x, bndl, bndu, state.innerobj);
        return;
    }
    public static void minasacreate(double[] x, double[] bndl, double[] bndu, out minasastate state)
    {
        int n;
        if( (ap.len(x)!=ap.len(bndl)) || (ap.len(x)!=ap.len(bndu)))
            throw new alglibexception("Error while calling 'minasacreate': looks like one of arguments has wrong size");
        state = new minasastate();
        n = ap.len(x);
        mincomp.minasacreate(n, x, bndl, bndu, state.innerobj);

        return;
    }

    /*************************************************************************
    Obsolete optimization algorithm.
    Was replaced by MinBLEIC subpackage.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minasasetcond(minasastate state, double epsg, double epsf, double epsx, int maxits)
    {

        mincomp.minasasetcond(state.innerobj, epsg, epsf, epsx, maxits);
        return;
    }

    /*************************************************************************
    Obsolete optimization algorithm.
    Was replaced by MinBLEIC subpackage.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minasasetxrep(minasastate state, bool needxrep)
    {

        mincomp.minasasetxrep(state.innerobj, needxrep);
        return;
    }

    /*************************************************************************
    Obsolete optimization algorithm.
    Was replaced by MinBLEIC subpackage.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minasasetalgorithm(minasastate state, int algotype)
    {

        mincomp.minasasetalgorithm(state.innerobj, algotype);
        return;
    }

    /*************************************************************************
    Obsolete optimization algorithm.
    Was replaced by MinBLEIC subpackage.

      -- ALGLIB --
         Copyright 02.04.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minasasetstpmax(minasastate state, double stpmax)
    {

        mincomp.minasasetstpmax(state.innerobj, stpmax);
        return;
    }

    /*************************************************************************
    This function provides reverse communication interface
    Reverse communication interface is not documented or recommended to use.
    See below for functions which provide better documented API
    *************************************************************************/
    public static bool minasaiteration(minasastate state)
    {

        bool result = mincomp.minasaiteration(state.innerobj);
        return result;
    }
    /*************************************************************************
    This family of functions is used to launcn iterations of nonlinear optimizer

    These functions accept following parameters:
        grad    -   callback which calculates function (or merit function)
                    value func and gradient grad at given point x
        rep     -   optional callback which is called after each iteration
                    can be null
        obj     -   optional object which is passed to func/grad/hess/jac/rep
                    can be null


      -- ALGLIB --
         Copyright 20.03.2009 by Bochkanov Sergey

    *************************************************************************/
    public static void minasaoptimize(minasastate state, ndimensional_grad grad, ndimensional_rep rep, object obj)
    {
        if( grad==null )
            throw new alglibexception("ALGLIB: error in 'minasaoptimize()' (grad is null)");
        while( alglib.minasaiteration(state) )
        {
            if( state.needfg )
            {
                grad(state.x, ref state.innerobj.f, state.innerobj.g, obj);
                continue;
            }
            if( state.innerobj.xupdated )
            {
                if( rep!=null )
                    rep(state.innerobj.x, state.innerobj.f, obj);
                continue;
            }
            throw new alglibexception("ALGLIB: error in 'minasaoptimize' (some derivatives were not provided?)");
        }
    }



    /*************************************************************************
    Obsolete optimization algorithm.
    Was replaced by MinBLEIC subpackage.

      -- ALGLIB --
         Copyright 20.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minasaresults(minasastate state, out double[] x, out minasareport rep)
    {
        x = new double[0];
        rep = new minasareport();
        mincomp.minasaresults(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    Obsolete optimization algorithm.
    Was replaced by MinBLEIC subpackage.

      -- ALGLIB --
         Copyright 20.03.2009 by Bochkanov Sergey
    *************************************************************************/
    public static void minasaresultsbuf(minasastate state, ref double[] x, minasareport rep)
    {

        mincomp.minasaresultsbuf(state.innerobj, ref x, rep.innerobj);
        return;
    }

    /*************************************************************************
    Obsolete optimization algorithm.
    Was replaced by MinBLEIC subpackage.

      -- ALGLIB --
         Copyright 30.07.2010 by Bochkanov Sergey
    *************************************************************************/
    public static void minasarestartfrom(minasastate state, double[] x, double[] bndl, double[] bndu)
    {

        mincomp.minasarestartfrom(state.innerobj, x, bndl, bndu);
        return;
    }

}
public partial class alglib
{
    public class optserv
    {
        /*************************************************************************
        This subroutine is used to prepare threshold value which will be used for
        trimming of the target function (see comments on TrimFunction() for more
        information).

        This function accepts only one parameter: function value at the starting
        point. It returns threshold which will be used for trimming.

          -- ALGLIB --
             Copyright 10.05.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void trimprepare(double f,
            ref double threshold)
        {
            threshold = 0;

            threshold = 10*(Math.Abs(f)+1);
        }


        /*************************************************************************
        This subroutine is used to "trim" target function, i.e. to do following
        transformation:

                           { {F,G}          if F<Threshold
            {F_tr, G_tr} = {
                           { {Threshold, 0} if F>=Threshold
                           
        Such transformation allows us to  solve  problems  with  singularities  by
        redefining function in such way that it becomes bounded from above.

          -- ALGLIB --
             Copyright 10.05.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void trimfunction(ref double f,
            ref double[] g,
            int n,
            double threshold)
        {
            int i = 0;

            if( (double)(f)>=(double)(threshold) )
            {
                f = threshold;
                for(i=0; i<=n-1; i++)
                {
                    g[i] = 0.0;
                }
            }
        }


        /*************************************************************************
        This function enforces boundary constraints in the X.

        This function correctly (although a bit inefficient) handles BL[i] which
        are -INF and BU[i] which are +INF.

        We have NMain+NSlack  dimensional  X,  with first NMain components bounded
        by BL/BU, and next NSlack ones bounded by non-negativity constraints.

        INPUT PARAMETERS
            X       -   array[NMain+NSlack], point
            BL      -   array[NMain], lower bounds
                        (may contain -INF, when bound is not present)
            HaveBL  -   array[NMain], if HaveBL[i] is False,
                        then i-th bound is not present
            BU      -   array[NMain], upper bounds
                        (may contain +INF, when bound is not present)
            HaveBU  -   array[NMain], if HaveBU[i] is False,
                        then i-th bound is not present

        OUTPUT PARAMETERS
            X       -   X with all constraints being enforced

        It returns True when constraints are consistent,
        False - when constraints are inconsistent.

          -- ALGLIB --
             Copyright 10.01.2012 by Bochkanov Sergey
        *************************************************************************/
        public static bool enforceboundaryconstraints(ref double[] x,
            double[] bl,
            bool[] havebl,
            double[] bu,
            bool[] havebu,
            int nmain,
            int nslack)
        {
            bool result = new bool();
            int i = 0;

            result = false;
            for(i=0; i<=nmain-1; i++)
            {
                if( (havebl[i] && havebu[i]) && (double)(bl[i])>(double)(bu[i]) )
                {
                    return result;
                }
                if( havebl[i] && (double)(x[i])<(double)(bl[i]) )
                {
                    x[i] = bl[i];
                }
                if( havebu[i] && (double)(x[i])>(double)(bu[i]) )
                {
                    x[i] = bu[i];
                }
            }
            for(i=0; i<=nslack-1; i++)
            {
                if( (double)(x[nmain+i])<(double)(0) )
                {
                    x[nmain+i] = 0;
                }
            }
            result = true;
            return result;
        }


        /*************************************************************************
        This function projects gradient into feasible area of boundary constrained
        optimization  problem.  X  can  be  infeasible  with  respect  to boundary
        constraints.  We  have  NMain+NSlack  dimensional  X,   with  first  NMain 
        components bounded by BL/BU, and next NSlack ones bounded by non-negativity
        constraints.

        INPUT PARAMETERS
            X       -   array[NMain+NSlack], point
            G       -   array[NMain+NSlack], gradient
            BL      -   lower bounds (may contain -INF, when bound is not present)
            HaveBL  -   if HaveBL[i] is False, then i-th bound is not present
            BU      -   upper bounds (may contain +INF, when bound is not present)
            HaveBU  -   if HaveBU[i] is False, then i-th bound is not present

        OUTPUT PARAMETERS
            G       -   projection of G. Components of G which satisfy one of the
                        following
                            (1) (X[I]<=BndL[I]) and (G[I]>0), OR
                            (2) (X[I]>=BndU[I]) and (G[I]<0)
                        are replaced by zeros.

        NOTE 1: this function assumes that constraints are feasible. It throws
        exception otherwise.

        NOTE 2: in fact, projection of ANTI-gradient is calculated,  because  this
        function trims components of -G which points outside of the feasible area.
        However, working with -G is considered confusing, because all optimization
        source work with G.

          -- ALGLIB --
             Copyright 10.01.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void projectgradientintobc(double[] x,
            ref double[] g,
            double[] bl,
            bool[] havebl,
            double[] bu,
            bool[] havebu,
            int nmain,
            int nslack)
        {
            int i = 0;

            for(i=0; i<=nmain-1; i++)
            {
                alglib.ap.assert((!havebl[i] || !havebu[i]) || (double)(bl[i])<=(double)(bu[i]), "ProjectGradientIntoBC: internal error (infeasible constraints)");
                if( (havebl[i] && (double)(x[i])<=(double)(bl[i])) && (double)(g[i])>(double)(0) )
                {
                    g[i] = 0;
                }
                if( (havebu[i] && (double)(x[i])>=(double)(bu[i])) && (double)(g[i])<(double)(0) )
                {
                    g[i] = 0;
                }
            }
            for(i=0; i<=nslack-1; i++)
            {
                if( (double)(x[nmain+i])<=(double)(0) && (double)(g[nmain+i])>(double)(0) )
                {
                    g[nmain+i] = 0;
                }
            }
        }


        /*************************************************************************
        Given
            a) initial point X0[NMain+NSlack]
               (feasible with respect to bound constraints)
            b) step vector alpha*D[NMain+NSlack]
            c) boundary constraints BndL[NMain], BndU[NMain]
            d) implicit non-negativity constraints for slack variables
        this  function  calculates  bound  on  the step length subject to boundary
        constraints.

        It returns:
            *  MaxStepLen - such step length that X0+MaxStepLen*alpha*D is exactly
               at the boundary given by constraints
            *  VariableToFreeze - index of the constraint to be activated,
               0 <= VariableToFreeze < NMain+NSlack
            *  ValueToFreeze - value of the corresponding constraint.

        Notes:
            * it is possible that several constraints can be activated by the step
              at once. In such cases only one constraint is returned. It is caller
              responsibility to check other constraints. This function makes  sure
              that we activate at least one constraint, and everything else is the
              responsibility of the caller.
            * steps smaller than MaxStepLen still can activate constraints due  to
              numerical errors. Thus purpose of this  function  is  not  to  guard 
              against accidental activation of the constraints - quite the reverse, 
              its purpose is to activate at least constraint upon performing  step
              which is too long.
            * in case there is no constraints to activate, we return negative
              VariableToFreeze and zero MaxStepLen and ValueToFreeze.
            * this function assumes that constraints are consistent; it throws
              exception otherwise.

        INPUT PARAMETERS
            X           -   array[NMain+NSlack], point. Must be feasible with respect 
                            to bound constraints (exception will be thrown otherwise)
            D           -   array[NMain+NSlack], step direction
            alpha       -   scalar multiplier before D, alpha<>0
            BndL        -   lower bounds, array[NMain]
                            (may contain -INF, when bound is not present)
            HaveBndL    -   array[NMain], if HaveBndL[i] is False,
                            then i-th bound is not present
            BndU        -   array[NMain], upper bounds
                            (may contain +INF, when bound is not present)
            HaveBndU    -   array[NMain], if HaveBndU[i] is False,
                            then i-th bound is not present
            NMain       -   number of main variables
            NSlack      -   number of slack variables
            
        OUTPUT PARAMETERS
            VariableToFreeze:
                            * negative value     = step is unbounded, ValueToFreeze=0,
                                                   MaxStepLen=0.
                            * non-negative value = at least one constraint, given by
                                                   this parameter, will  be  activated
                                                   upon performing maximum step.
            ValueToFreeze-  value of the variable which will be constrained
            MaxStepLen  -   maximum length of the step. Can be zero when step vector
                            looks outside of the feasible area.

          -- ALGLIB --
             Copyright 10.01.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void calculatestepbound(double[] x,
            double[] d,
            double alpha,
            double[] bndl,
            bool[] havebndl,
            double[] bndu,
            bool[] havebndu,
            int nmain,
            int nslack,
            ref int variabletofreeze,
            ref double valuetofreeze,
            ref double maxsteplen)
        {
            int i = 0;
            double prevmax = 0;
            double initval = 0;

            variabletofreeze = 0;
            valuetofreeze = 0;
            maxsteplen = 0;

            alglib.ap.assert((double)(alpha)!=(double)(0), "CalculateStepBound: zero alpha");
            variabletofreeze = -1;
            initval = math.maxrealnumber;
            maxsteplen = initval;
            for(i=0; i<=nmain-1; i++)
            {
                if( havebndl[i] && (double)(alpha*d[i])<(double)(0) )
                {
                    alglib.ap.assert((double)(x[i])>=(double)(bndl[i]), "CalculateStepBound: infeasible X");
                    prevmax = maxsteplen;
                    maxsteplen = apserv.safeminposrv(x[i]-bndl[i], -(alpha*d[i]), maxsteplen);
                    if( (double)(maxsteplen)<(double)(prevmax) )
                    {
                        variabletofreeze = i;
                        valuetofreeze = bndl[i];
                    }
                }
                if( havebndu[i] && (double)(alpha*d[i])>(double)(0) )
                {
                    alglib.ap.assert((double)(x[i])<=(double)(bndu[i]), "CalculateStepBound: infeasible X");
                    prevmax = maxsteplen;
                    maxsteplen = apserv.safeminposrv(bndu[i]-x[i], alpha*d[i], maxsteplen);
                    if( (double)(maxsteplen)<(double)(prevmax) )
                    {
                        variabletofreeze = i;
                        valuetofreeze = bndu[i];
                    }
                }
            }
            for(i=0; i<=nslack-1; i++)
            {
                if( (double)(alpha*d[nmain+i])<(double)(0) )
                {
                    alglib.ap.assert((double)(x[nmain+i])>=(double)(0), "CalculateStepBound: infeasible X");
                    prevmax = maxsteplen;
                    maxsteplen = apserv.safeminposrv(x[nmain+i], -(alpha*d[nmain+i]), maxsteplen);
                    if( (double)(maxsteplen)<(double)(prevmax) )
                    {
                        variabletofreeze = nmain+i;
                        valuetofreeze = 0;
                    }
                }
            }
            if( (double)(maxsteplen)==(double)(initval) )
            {
                valuetofreeze = 0;
                maxsteplen = 0;
            }
        }


        /*************************************************************************
        This function postprocesses bounded step by:
        * analysing step length (whether it is equal to MaxStepLen) and activating 
          constraint given by VariableToFreeze if needed
        * checking for additional bound constraints to activate

        This function uses final point of the step, quantities calculated  by  the
        CalculateStepBound()  function.  As  result,  it  returns  point  which is 
        exactly feasible with respect to boundary constraints.

        NOTE 1: this function does NOT handle and check linear equality constraints
        NOTE 2: when StepTaken=MaxStepLen we always activate at least one constraint

        INPUT PARAMETERS
            X           -   array[NMain+NSlack], final point to postprocess
            XPrev       -   array[NMain+NSlack], initial point
            BndL        -   lower bounds, array[NMain]
                            (may contain -INF, when bound is not present)
            HaveBndL    -   array[NMain], if HaveBndL[i] is False,
                            then i-th bound is not present
            BndU        -   array[NMain], upper bounds
                            (may contain +INF, when bound is not present)
            HaveBndU    -   array[NMain], if HaveBndU[i] is False,
                            then i-th bound is not present
            NMain       -   number of main variables
            NSlack      -   number of slack variables
            VariableToFreeze-result of CalculateStepBound()
            ValueToFreeze-  result of CalculateStepBound()
            StepTaken   -   actual step length (actual step is equal to the possibly 
                            non-unit step direction vector times this parameter).
                            StepTaken<=MaxStepLen.
            MaxStepLen  -   result of CalculateStepBound()
            
        OUTPUT PARAMETERS
            X           -   point bounded with respect to constraints.
                            components corresponding to active constraints are exactly
                            equal to the boundary values.
                            
        RESULT:
            number of constraints activated in addition to previously active ones.
            Constraints which were DEACTIVATED are ignored (do not influence
            function value).

          -- ALGLIB --
             Copyright 10.01.2012 by Bochkanov Sergey
        *************************************************************************/
        public static int postprocessboundedstep(ref double[] x,
            double[] xprev,
            double[] bndl,
            bool[] havebndl,
            double[] bndu,
            bool[] havebndu,
            int nmain,
            int nslack,
            int variabletofreeze,
            double valuetofreeze,
            double steptaken,
            double maxsteplen)
        {
            int result = 0;
            int i = 0;
            bool wasactivated = new bool();

            alglib.ap.assert(variabletofreeze<0 || (double)(steptaken)<=(double)(maxsteplen));
            
            //
            // Activate constraints
            //
            if( variabletofreeze>=0 && (double)(steptaken)==(double)(maxsteplen) )
            {
                x[variabletofreeze] = valuetofreeze;
            }
            for(i=0; i<=nmain-1; i++)
            {
                if( havebndl[i] && (double)(x[i])<(double)(bndl[i]) )
                {
                    x[i] = bndl[i];
                }
                if( havebndu[i] && (double)(x[i])>(double)(bndu[i]) )
                {
                    x[i] = bndu[i];
                }
            }
            for(i=0; i<=nslack-1; i++)
            {
                if( (double)(x[nmain+i])<=(double)(0) )
                {
                    x[nmain+i] = 0;
                }
            }
            
            //
            // Calculate number of constraints being activated
            //
            result = 0;
            for(i=0; i<=nmain-1; i++)
            {
                wasactivated = (double)(x[i])!=(double)(xprev[i]) && ((havebndl[i] && (double)(x[i])==(double)(bndl[i])) || (havebndu[i] && (double)(x[i])==(double)(bndu[i])));
                wasactivated = wasactivated || variabletofreeze==i;
                if( wasactivated )
                {
                    result = result+1;
                }
            }
            for(i=0; i<=nslack-1; i++)
            {
                wasactivated = (double)(x[nmain+i])!=(double)(xprev[nmain+i]) && (double)(x[nmain+i])==(double)(0.0);
                wasactivated = wasactivated || variabletofreeze==nmain+i;
                if( wasactivated )
                {
                    result = result+1;
                }
            }
            return result;
        }


        /*************************************************************************
        The  purpose  of  this  function is to prevent algorithm from "unsticking" 
        from  the  active  bound  constraints  because  of  numerical noise in the
        gradient or Hessian.

        It is done by zeroing some components of the search direction D.  D[i]  is
        zeroed when both (a) and (b) are true:
        a) corresponding X[i] is exactly at the boundary
        b) |D[i]*S[i]| <= DropTol*Sqrt(SUM(D[i]^2*S[I]^2))

        D  can  be  step  direction , antigradient, gradient, or anything similar. 
        Sign of D does not matter, nor matters step length.

        NOTE 1: boundary constraints are expected to be consistent, as well as X
                is expected to be feasible. Exception will be thrown otherwise.

        INPUT PARAMETERS
            D           -   array[NMain+NSlack], direction
            X           -   array[NMain+NSlack], current point
            BndL        -   lower bounds, array[NMain]
                            (may contain -INF, when bound is not present)
            HaveBndL    -   array[NMain], if HaveBndL[i] is False,
                            then i-th bound is not present
            BndU        -   array[NMain], upper bounds
                            (may contain +INF, when bound is not present)
            HaveBndU    -   array[NMain], if HaveBndU[i] is False,
                            then i-th bound is not present
            S           -   array[NMain+NSlack], scaling of the variables
            NMain       -   number of main variables
            NSlack      -   number of slack variables
            DropTol     -   drop tolerance, >=0
            
        OUTPUT PARAMETERS
            X           -   point bounded with respect to constraints.
                            components corresponding to active constraints are exactly
                            equal to the boundary values.

          -- ALGLIB --
             Copyright 10.01.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void filterdirection(ref double[] d,
            double[] x,
            double[] bndl,
            bool[] havebndl,
            double[] bndu,
            bool[] havebndu,
            double[] s,
            int nmain,
            int nslack,
            double droptol)
        {
            int i = 0;
            double scalednorm = 0;
            bool isactive = new bool();

            scalednorm = 0.0;
            for(i=0; i<=nmain+nslack-1; i++)
            {
                scalednorm = scalednorm+math.sqr(d[i]*s[i]);
            }
            scalednorm = Math.Sqrt(scalednorm);
            for(i=0; i<=nmain-1; i++)
            {
                alglib.ap.assert(!havebndl[i] || (double)(x[i])>=(double)(bndl[i]), "FilterDirection: infeasible point");
                alglib.ap.assert(!havebndu[i] || (double)(x[i])<=(double)(bndu[i]), "FilterDirection: infeasible point");
                isactive = (havebndl[i] && (double)(x[i])==(double)(bndl[i])) || (havebndu[i] && (double)(x[i])==(double)(bndu[i]));
                if( isactive && (double)(Math.Abs(d[i]*s[i]))<=(double)(droptol*scalednorm) )
                {
                    d[i] = 0.0;
                }
            }
            for(i=0; i<=nslack-1; i++)
            {
                alglib.ap.assert((double)(x[nmain+i])>=(double)(0), "FilterDirection: infeasible point");
                if( (double)(x[nmain+i])==(double)(0) && (double)(Math.Abs(d[nmain+i]*s[nmain+i]))<=(double)(droptol*scalednorm) )
                {
                    d[nmain+i] = 0.0;
                }
            }
        }


        /*************************************************************************
        This function returns number of bound constraints whose state was  changed
        (either activated or deactivated) when making step from XPrev to X.

        Constraints are considered:
        * active - when we are exactly at the boundary
        * inactive - when we are not at the boundary

        You should note that antigradient direction is NOT taken into account when
        we make decions on the constraint status.

        INPUT PARAMETERS
            X           -   array[NMain+NSlack], final point.
                            Must be feasible with respect to bound constraints.
            XPrev       -   array[NMain+NSlack], initial point.
                            Must be feasible with respect to bound constraints.
            BndL        -   lower bounds, array[NMain]
                            (may contain -INF, when bound is not present)
            HaveBndL    -   array[NMain], if HaveBndL[i] is False,
                            then i-th bound is not present
            BndU        -   array[NMain], upper bounds
                            (may contain +INF, when bound is not present)
            HaveBndU    -   array[NMain], if HaveBndU[i] is False,
                            then i-th bound is not present
            NMain       -   number of main variables
            NSlack      -   number of slack variables
            
        RESULT:
            number of constraints whose state was changed.

          -- ALGLIB --
             Copyright 10.01.2012 by Bochkanov Sergey
        *************************************************************************/
        public static int numberofchangedconstraints(double[] x,
            double[] xprev,
            double[] bndl,
            bool[] havebndl,
            double[] bndu,
            bool[] havebndu,
            int nmain,
            int nslack)
        {
            int result = 0;
            int i = 0;
            bool statuschanged = new bool();

            result = 0;
            for(i=0; i<=nmain-1; i++)
            {
                if( (double)(x[i])!=(double)(xprev[i]) )
                {
                    statuschanged = false;
                    if( havebndl[i] && ((double)(x[i])==(double)(bndl[i]) || (double)(xprev[i])==(double)(bndl[i])) )
                    {
                        statuschanged = true;
                    }
                    if( havebndu[i] && ((double)(x[i])==(double)(bndu[i]) || (double)(xprev[i])==(double)(bndu[i])) )
                    {
                        statuschanged = true;
                    }
                    if( statuschanged )
                    {
                        result = result+1;
                    }
                }
            }
            for(i=0; i<=nslack-1; i++)
            {
                if( (double)(x[nmain+i])!=(double)(xprev[nmain+i]) && ((double)(x[nmain+i])==(double)(0) || (double)(xprev[nmain+i])==(double)(0)) )
                {
                    result = result+1;
                }
            }
            return result;
        }


        /*************************************************************************
        This function finds feasible point of  (NMain+NSlack)-dimensional  problem
        subject to NMain explicit boundary constraints (some  constraints  can  be
        omitted), NSlack implicit non-negativity constraints,  K  linear  equality
        constraints.

        INPUT PARAMETERS
            X           -   array[NMain+NSlack], initial point.
            BndL        -   lower bounds, array[NMain]
                            (may contain -INF, when bound is not present)
            HaveBndL    -   array[NMain], if HaveBndL[i] is False,
                            then i-th bound is not present
            BndU        -   array[NMain], upper bounds
                            (may contain +INF, when bound is not present)
            HaveBndU    -   array[NMain], if HaveBndU[i] is False,
                            then i-th bound is not present
            NMain       -   number of main variables
            NSlack      -   number of slack variables
            CE          -   array[K,NMain+NSlack+1], equality  constraints CE*x=b.
                            Rows contain constraints, first  NMain+NSlack  columns
                            contain coefficients before X[], last  column  contain
                            right part.
            K           -   number of linear constraints
            EpsI        -   infeasibility (error in the right part) allowed in the
                            solution

        OUTPUT PARAMETERS:
            X           -   feasible point or best infeasible point found before
                            algorithm termination
            QPIts       -   number of QP iterations (for debug purposes)
            GPAIts      -   number of GPA iterations (for debug purposes)
            
        RESULT:
            True in case X is feasible, False - if it is infeasible.

          -- ALGLIB --
             Copyright 20.01.2012 by Bochkanov Sergey
        *************************************************************************/
        public static bool findfeasiblepoint(ref double[] x,
            double[] bndl,
            bool[] havebndl,
            double[] bndu,
            bool[] havebndu,
            int nmain,
            int nslack,
            double[,] ce,
            int k,
            double epsi,
            ref int qpits,
            ref int gpaits)
        {
            bool result = new bool();
            int i = 0;
            int j = 0;
            int idx0 = 0;
            int idx1 = 0;
            double[] permx = new double[0];
            double[] xn = new double[0];
            double[] xa = new double[0];
            double[] newtonstep = new double[0];
            double[] g = new double[0];
            double[] pg = new double[0];
            double[,] a = new double[0,0];
            double armijostep = 0;
            double armijobeststep = 0;
            double armijobestfeas = 0;
            double v = 0;
            double mx = 0;
            double feaserr = 0;
            double feasold = 0;
            double feasnew = 0;
            double pgnorm = 0;
            double vn = 0;
            double vd = 0;
            double stp = 0;
            int vartofreeze = 0;
            double valtofreeze = 0;
            double maxsteplen = 0;
            bool werechangesinconstraints = new bool();
            bool stage1isover = new bool();
            bool converged = new bool();
            double[] activeconstraints = new double[0];
            double[] tmpk = new double[0];
            double[] colnorms = new double[0];
            int nactive = 0;
            int nfree = 0;
            int nsvd = 0;
            int[] p1 = new int[0];
            int[] p2 = new int[0];
            apserv.apbuffers buf = new apserv.apbuffers();
            double[] w = new double[0];
            double[] s = new double[0];
            double[,] u = new double[0,0];
            double[,] vt = new double[0,0];
            int itscount = 0;
            int itswithintolerance = 0;
            int maxitswithintolerance = 0;
            int gparuns = 0;
            int maxgparuns = 0;
            int maxarmijoruns = 0;
            int i_ = 0;

            ce = (double[,])ce.Clone();
            qpits = 0;
            gpaits = 0;

            maxitswithintolerance = 3;
            maxgparuns = 3;
            maxarmijoruns = 5;
            qpits = 0;
            gpaits = 0;
            
            //
            // Initial enforcement of the feasibility with respect to boundary constraints
            // NOTE: after this block we assume that boundary constraints are consistent.
            //
            if( !enforceboundaryconstraints(ref x, bndl, havebndl, bndu, havebndu, nmain, nslack) )
            {
                result = false;
                return result;
            }
            if( k==0 )
            {
                
                //
                // No linear constraints, we can exit right now
                //
                result = true;
                return result;
            }
            
            //
            // Scale rows of CE in such way that max(CE[i,0..nmain+nslack-1])=1 for any i=0..k-1
            //
            for(i=0; i<=k-1; i++)
            {
                v = 0.0;
                for(j=0; j<=nmain+nslack-1; j++)
                {
                    v = Math.Max(v, Math.Abs(ce[i,j]));
                }
                if( (double)(v)!=(double)(0) )
                {
                    v = 1/v;
                    for(i_=0; i_<=nmain+nslack;i_++)
                    {
                        ce[i,i_] = v*ce[i,i_];
                    }
                }
            }
            
            //
            // Allocate temporaries
            //
            xn = new double[nmain+nslack];
            xa = new double[nmain+nslack];
            permx = new double[nmain+nslack];
            g = new double[nmain+nslack];
            pg = new double[nmain+nslack];
            tmpk = new double[k];
            a = new double[k, nmain+nslack];
            activeconstraints = new double[nmain+nslack];
            newtonstep = new double[nmain+nslack];
            s = new double[nmain+nslack];
            colnorms = new double[nmain+nslack];
            for(i=0; i<=nmain+nslack-1; i++)
            {
                s[i] = 1.0;
                colnorms[i] = 0.0;
                for(j=0; j<=k-1; j++)
                {
                    colnorms[i] = colnorms[i]+math.sqr(ce[j,i]);
                }
            }
            
            //
            // K>0, we have linear equality constraints combined with bound constraints.
            //
            // Try to find feasible point as minimizer of the quadratic function
            //     F(x) = 0.5*||CE*x-b||^2 = 0.5*x'*(CE'*CE)*x - (b'*CE)*x + 0.5*b'*b
            // subject to boundary constraints given by BL, BU and non-negativity of
            // the slack variables. BTW, we drop constant term because it does not
            // actually influences on the solution.
            //
            // Below we will assume that K>0.
            //
            itswithintolerance = 0;
            itscount = 0;
            while( true )
            {
                
                //
                // Stage 0: check for exact convergence
                //
                converged = true;
                feaserr = 0;
                for(i=0; i<=k-1; i++)
                {
                    
                    //
                    // Calculate:
                    // * V - error in the right part
                    // * MX - maximum term in the left part
                    //
                    // Terminate if error in the right part is not greater than 100*Eps*MX.
                    //
                    // IMPORTANT: we must perform check for non-strict inequality, i.e. to use <= instead of <.
                    //            it will allow us to easily handle situations with zero rows of CE.
                    //
                    mx = 0;
                    v = -ce[i,nmain+nslack];
                    for(j=0; j<=nmain+nslack-1; j++)
                    {
                        mx = Math.Max(mx, Math.Abs(ce[i,j]*x[j]));
                        v = v+ce[i,j]*x[j];
                    }
                    feaserr = feaserr+math.sqr(v);
                    converged = converged && (double)(Math.Abs(v))<=(double)(100*math.machineepsilon*mx);
                }
                feaserr = Math.Sqrt(feaserr);
                if( converged )
                {
                    result = (double)(feaserr)<=(double)(epsi);
                    return result;
                }
                
                //
                // Stage 1: equality constrained quadratic programming
                //
                // * treat active bound constraints as equality ones (constraint is considered 
                //   active when we are at the boundary, independently of the antigradient direction)
                // * calculate unrestricted Newton step to point XM (which may be infeasible)
                //   calculate MaxStepLen = largest step in direction of XM which retains feasibility.
                // * perform bounded step from X to XN:
                //   a) XN=XM                  (if XM is feasible)
                //   b) XN=X-MaxStepLen*(XM-X) (otherwise)
                // * X := XN
                // * if XM (Newton step subject to currently active constraints) was feasible, goto Stage 2
                // * repeat Stage 1
                //
                // NOTE 1: in order to solve constrained qudratic subproblem we will have to reorder
                //         variables in such way that ones corresponding to inactive constraints will
                //         be first, and active ones will be last in the list. CE and X are now
                //                                                       [ xi ]
                //         separated into two parts: CE = [CEi CEa], x = [    ], where CEi/Xi correspond
                //                                                       [ xa ]
                //         to INACTIVE constraints, and CEa/Xa correspond to the ACTIVE ones.
                //
                //         Now, instead of F=0.5*x'*(CE'*CE)*x - (b'*CE)*x + 0.5*b'*b, we have
                //         F(xi) = 0.5*(CEi*xi,CEi*xi) + (CEa*xa-b,CEi*xi) + (0.5*CEa*xa-b,CEa*xa).
                //         Here xa is considered constant, i.e. we optimize with respect to xi, leaving xa fixed.
                //
                //         We can solve it by performing SVD of CEi and calculating pseudoinverse of the
                //         Hessian matrix. Of course, we do NOT calculate pseudoinverse explicitly - we
                //         just use singular vectors to perform implicit multiplication by it.
                //
                //
                while( true )
                {
                    
                    //
                    // Calculate G - gradient subject to equality constraints,
                    // multiply it by inverse of the Hessian diagonal to obtain initial
                    // step vector.
                    //
                    // Bound step subject to constraints which can be activated,
                    // run Armijo search with increasing step size.
                    // Search is terminated when feasibility error stops to decrease.
                    //
                    // NOTE: it is important to test for "stops to decrease" instead
                    // of "starts to increase" in order to correctly handle cases with
                    // zero CE.
                    //
                    armijobeststep = 0.0;
                    armijobestfeas = 0.0;
                    for(i=0; i<=nmain+nslack-1; i++)
                    {
                        g[i] = 0;
                    }
                    for(i=0; i<=k-1; i++)
                    {
                        v = 0.0;
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            v += ce[i,i_]*x[i_];
                        }
                        v = v-ce[i,nmain+nslack];
                        armijobestfeas = armijobestfeas+math.sqr(v);
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            g[i_] = g[i_] + v*ce[i,i_];
                        }
                    }
                    armijobestfeas = Math.Sqrt(armijobestfeas);
                    for(i=0; i<=nmain-1; i++)
                    {
                        if( havebndl[i] && (double)(x[i])==(double)(bndl[i]) )
                        {
                            g[i] = 0.0;
                        }
                        if( havebndu[i] && (double)(x[i])==(double)(bndu[i]) )
                        {
                            g[i] = 0.0;
                        }
                    }
                    for(i=0; i<=nslack-1; i++)
                    {
                        if( (double)(x[nmain+i])==(double)(0.0) )
                        {
                            g[nmain+i] = 0.0;
                        }
                    }
                    v = 0.0;
                    for(i=0; i<=nmain+nslack-1; i++)
                    {
                        if( (double)(math.sqr(colnorms[i]))!=(double)(0) )
                        {
                            newtonstep[i] = -(g[i]/math.sqr(colnorms[i]));
                        }
                        else
                        {
                            newtonstep[i] = 0.0;
                        }
                        v = v+math.sqr(newtonstep[i]);
                    }
                    if( (double)(v)==(double)(0) )
                    {
                        
                        //
                        // Constrained gradient is zero, QP iterations are over
                        //
                        break;
                    }
                    calculatestepbound(x, newtonstep, 1.0, bndl, havebndl, bndu, havebndu, nmain, nslack, ref vartofreeze, ref valtofreeze, ref maxsteplen);
                    if( vartofreeze>=0 && (double)(maxsteplen)==(double)(0) )
                    {
                        
                        //
                        // Can not perform step, QP iterations are over
                        //
                        break;
                    }
                    if( vartofreeze>=0 )
                    {
                        armijostep = Math.Min(1.0, maxsteplen);
                    }
                    else
                    {
                        armijostep = 1;
                    }
                    while( true )
                    {
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            xa[i_] = x[i_];
                        }
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            xa[i_] = xa[i_] + armijostep*newtonstep[i_];
                        }
                        enforceboundaryconstraints(ref xa, bndl, havebndl, bndu, havebndu, nmain, nslack);
                        feaserr = 0.0;
                        for(i=0; i<=k-1; i++)
                        {
                            v = 0.0;
                            for(i_=0; i_<=nmain+nslack-1;i_++)
                            {
                                v += ce[i,i_]*xa[i_];
                            }
                            v = v-ce[i,nmain+nslack];
                            feaserr = feaserr+math.sqr(v);
                        }
                        feaserr = Math.Sqrt(feaserr);
                        if( (double)(feaserr)>=(double)(armijobestfeas) )
                        {
                            break;
                        }
                        armijobestfeas = feaserr;
                        armijobeststep = armijostep;
                        armijostep = 2.0*armijostep;
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        x[i_] = x[i_] + armijobeststep*newtonstep[i_];
                    }
                    enforceboundaryconstraints(ref x, bndl, havebndl, bndu, havebndu, nmain, nslack);
                    
                    //
                    // Determine number of active and free constraints
                    //
                    nactive = 0;
                    for(i=0; i<=nmain-1; i++)
                    {
                        activeconstraints[i] = 0;
                        if( havebndl[i] && (double)(x[i])==(double)(bndl[i]) )
                        {
                            activeconstraints[i] = 1;
                        }
                        if( havebndu[i] && (double)(x[i])==(double)(bndu[i]) )
                        {
                            activeconstraints[i] = 1;
                        }
                        if( (double)(activeconstraints[i])>(double)(0) )
                        {
                            nactive = nactive+1;
                        }
                    }
                    for(i=0; i<=nslack-1; i++)
                    {
                        activeconstraints[nmain+i] = 0;
                        if( (double)(x[nmain+i])==(double)(0.0) )
                        {
                            activeconstraints[nmain+i] = 1;
                        }
                        if( (double)(activeconstraints[nmain+i])>(double)(0) )
                        {
                            nactive = nactive+1;
                        }
                    }
                    nfree = nmain+nslack-nactive;
                    if( nfree==0 )
                    {
                        break;
                    }
                    qpits = qpits+1;
                    
                    //
                    // Reorder variables
                    //
                    tsort.tagsortbuf(ref activeconstraints, nmain+nslack, ref p1, ref p2, buf);
                    for(i=0; i<=k-1; i++)
                    {
                        for(j=0; j<=nmain+nslack-1; j++)
                        {
                            a[i,j] = ce[i,j];
                        }
                    }
                    for(j=0; j<=nmain+nslack-1; j++)
                    {
                        permx[j] = x[j];
                    }
                    for(j=0; j<=nmain+nslack-1; j++)
                    {
                        if( p2[j]!=j )
                        {
                            idx0 = p2[j];
                            idx1 = j;
                            for(i=0; i<=k-1; i++)
                            {
                                v = a[i,idx0];
                                a[i,idx0] = a[i,idx1];
                                a[i,idx1] = v;
                            }
                            v = permx[idx0];
                            permx[idx0] = permx[idx1];
                            permx[idx1] = v;
                        }
                    }
                    
                    //
                    // Calculate (unprojected) gradient:
                    // G(xi) = CEi'*(CEi*xi + CEa*xa - b)
                    //
                    for(i=0; i<=nfree-1; i++)
                    {
                        g[i] = 0;
                    }
                    for(i=0; i<=k-1; i++)
                    {
                        v = 0.0;
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            v += a[i,i_]*permx[i_];
                        }
                        tmpk[i] = v-ce[i,nmain+nslack];
                    }
                    for(i=0; i<=k-1; i++)
                    {
                        v = tmpk[i];
                        for(i_=0; i_<=nfree-1;i_++)
                        {
                            g[i_] = g[i_] + v*a[i,i_];
                        }
                    }
                    
                    //
                    // Calculate Newton step using SVD of CEi:
                    //     F(xi)  = 0.5*xi'*H*xi + g'*xi    (Taylor decomposition)
                    //     XN     = -H^(-1)*g               (new point, solution of the QP subproblem)
                    //     H      = CEi'*CEi                
                    //     CEi    = U*W*V'                  (SVD of CEi)
                    //     H      = V*W^2*V'                 
                    //     H^(-1) = V*W^(-2)*V'
                    //     step     = -V*W^(-2)*V'*g          (it is better to perform multiplication from right to left)
                    //
                    // NOTE 1: we do NOT need left singular vectors to perform Newton step.
                    //
                    nsvd = Math.Min(k, nfree);
                    if( !svd.rmatrixsvd(a, k, nfree, 0, 1, 2, ref w, ref u, ref vt) )
                    {
                        result = false;
                        return result;
                    }
                    for(i=0; i<=nsvd-1; i++)
                    {
                        v = 0.0;
                        for(i_=0; i_<=nfree-1;i_++)
                        {
                            v += vt[i,i_]*g[i_];
                        }
                        tmpk[i] = v;
                    }
                    for(i=0; i<=nsvd-1; i++)
                    {
                        
                        //
                        // It is important to have strict ">" in order to correctly 
                        // handle zero singular values.
                        //
                        if( (double)(math.sqr(w[i]))>(double)(math.sqr(w[0])*(nmain+nslack)*math.machineepsilon) )
                        {
                            tmpk[i] = tmpk[i]/math.sqr(w[i]);
                        }
                        else
                        {
                            tmpk[i] = 0;
                        }
                    }
                    for(i=0; i<=nmain+nslack-1; i++)
                    {
                        newtonstep[i] = 0;
                    }
                    for(i=0; i<=nsvd-1; i++)
                    {
                        v = tmpk[i];
                        for(i_=0; i_<=nfree-1;i_++)
                        {
                            newtonstep[i_] = newtonstep[i_] - v*vt[i,i_];
                        }
                    }
                    for(j=nmain+nslack-1; j>=0; j--)
                    {
                        if( p2[j]!=j )
                        {
                            idx0 = p2[j];
                            idx1 = j;
                            v = newtonstep[idx0];
                            newtonstep[idx0] = newtonstep[idx1];
                            newtonstep[idx1] = v;
                        }
                    }
                    
                    //
                    // NewtonStep contains Newton step subject to active bound constraints.
                    //
                    // Such step leads us to the minimizer of the equality constrained F,
                    // but such minimizer may be infeasible because some constraints which
                    // are inactive at the initial point can be violated at the solution.
                    //
                    // Thus, we perform optimization in two stages:
                    // a) perform bounded Newton step, i.e. step in the Newton direction
                    //    until activation of the first constraint
                    // b) in case (MaxStepLen>0)and(MaxStepLen<1), perform additional iteration
                    //    of the Armijo line search in the rest of the Newton direction.
                    //
                    calculatestepbound(x, newtonstep, 1.0, bndl, havebndl, bndu, havebndu, nmain, nslack, ref vartofreeze, ref valtofreeze, ref maxsteplen);
                    if( vartofreeze>=0 && (double)(maxsteplen)==(double)(0) )
                    {
                        
                        //
                        // Activation of the constraints prevent us from performing step,
                        // QP iterations are over
                        //
                        break;
                    }
                    if( vartofreeze>=0 )
                    {
                        v = Math.Min(1.0, maxsteplen);
                    }
                    else
                    {
                        v = 1.0;
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        xn[i_] = v*newtonstep[i_];
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        xn[i_] = xn[i_] + x[i_];
                    }
                    postprocessboundedstep(ref xn, x, bndl, havebndl, bndu, havebndu, nmain, nslack, vartofreeze, valtofreeze, v, maxsteplen);
                    if( (double)(maxsteplen)>(double)(0) && (double)(maxsteplen)<(double)(1) )
                    {
                        
                        //
                        // Newton step was restricted by activation of the constraints,
                        // perform Armijo iteration.
                        //
                        // Initial estimate for best step is zero step. We try different
                        // step sizes, from the 1-MaxStepLen (residual of the full Newton
                        // step) to progressively smaller and smaller steps.
                        //
                        armijobeststep = 0.0;
                        armijobestfeas = 0.0;
                        for(i=0; i<=k-1; i++)
                        {
                            v = 0.0;
                            for(i_=0; i_<=nmain+nslack-1;i_++)
                            {
                                v += ce[i,i_]*xn[i_];
                            }
                            v = v-ce[i,nmain+nslack];
                            armijobestfeas = armijobestfeas+math.sqr(v);
                        }
                        armijobestfeas = Math.Sqrt(armijobestfeas);
                        armijostep = 1-maxsteplen;
                        for(j=0; j<=maxarmijoruns-1; j++)
                        {
                            for(i_=0; i_<=nmain+nslack-1;i_++)
                            {
                                xa[i_] = xn[i_];
                            }
                            for(i_=0; i_<=nmain+nslack-1;i_++)
                            {
                                xa[i_] = xa[i_] + armijostep*newtonstep[i_];
                            }
                            enforceboundaryconstraints(ref xa, bndl, havebndl, bndu, havebndu, nmain, nslack);
                            feaserr = 0.0;
                            for(i=0; i<=k-1; i++)
                            {
                                v = 0.0;
                                for(i_=0; i_<=nmain+nslack-1;i_++)
                                {
                                    v += ce[i,i_]*xa[i_];
                                }
                                v = v-ce[i,nmain+nslack];
                                feaserr = feaserr+math.sqr(v);
                            }
                            feaserr = Math.Sqrt(feaserr);
                            if( (double)(feaserr)<(double)(armijobestfeas) )
                            {
                                armijobestfeas = feaserr;
                                armijobeststep = armijostep;
                            }
                            armijostep = 0.5*armijostep;
                        }
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            xa[i_] = xn[i_];
                        }
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            xa[i_] = xa[i_] + armijobeststep*newtonstep[i_];
                        }
                        enforceboundaryconstraints(ref xa, bndl, havebndl, bndu, havebndu, nmain, nslack);
                    }
                    else
                    {
                        
                        //
                        // Armijo iteration is not performed
                        //
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            xa[i_] = xn[i_];
                        }
                    }
                    stage1isover = (double)(maxsteplen)>=(double)(1) || (double)(maxsteplen)==(double)(0);
                    
                    //
                    // Calculate feasibility errors for old and new X.
                    // These quantinies are used for debugging purposes only.
                    // However, we can leave them in release code because performance impact is insignificant.
                    //
                    // Update X. Exit if needed.
                    //
                    feasold = 0;
                    feasnew = 0;
                    for(i=0; i<=k-1; i++)
                    {
                        v = 0.0;
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            v += ce[i,i_]*x[i_];
                        }
                        feasold = feasold+math.sqr(v-ce[i,nmain+nslack]);
                        v = 0.0;
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            v += ce[i,i_]*xa[i_];
                        }
                        feasnew = feasnew+math.sqr(v-ce[i,nmain+nslack]);
                    }
                    feasold = Math.Sqrt(feasold);
                    feasnew = Math.Sqrt(feasnew);
                    if( (double)(feasnew)>=(double)(feasold) )
                    {
                        break;
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        x[i_] = xa[i_];
                    }
                    if( stage1isover )
                    {
                        break;
                    }
                }
                
                //
                // Stage 2: gradient projection algorithm (GPA)
                //
                // * calculate feasibility error (with respect to linear equality constraints)
                // * calculate gradient G of F, project it into feasible area (G => PG)
                // * exit if norm(PG) is exactly zero or feasibility error is smaller than EpsC
                // * let XM be exact minimum of F along -PG (XM may be infeasible).
                //   calculate MaxStepLen = largest step in direction of -PG which retains feasibility.
                // * perform bounded step from X to XN:
                //   a) XN=XM              (if XM is feasible)
                //   b) XN=X-MaxStepLen*PG (otherwise)
                // * X := XN
                // * stop after specified number of iterations or when no new constraints was activated
                //
                // NOTES:
                // * grad(F) = (CE'*CE)*x - (b'*CE)^T
                // * CE[i] denotes I-th row of CE
                // * XM = X+stp*(-PG) where stp=(grad(F(X)),PG)/(CE*PG,CE*PG).
                //   Here PG is a projected gradient, but in fact it can be arbitrary non-zero 
                //   direction vector - formula for minimum of F along PG still will be correct.
                //
                werechangesinconstraints = false;
                for(gparuns=1; gparuns<=k; gparuns++)
                {
                    
                    //
                    // calculate feasibility error and G
                    //
                    feaserr = 0;
                    for(i=0; i<=nmain+nslack-1; i++)
                    {
                        g[i] = 0;
                    }
                    for(i=0; i<=k-1; i++)
                    {
                        
                        //
                        // G += CE[i]^T * (CE[i]*x-b[i])
                        //
                        v = 0.0;
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            v += ce[i,i_]*x[i_];
                        }
                        v = v-ce[i,nmain+nslack];
                        feaserr = feaserr+math.sqr(v);
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            g[i_] = g[i_] + v*ce[i,i_];
                        }
                    }
                    
                    //
                    // project G, filter it (strip numerical noise)
                    //
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        pg[i_] = g[i_];
                    }
                    projectgradientintobc(x, ref pg, bndl, havebndl, bndu, havebndu, nmain, nslack);
                    filterdirection(ref pg, x, bndl, havebndl, bndu, havebndu, s, nmain, nslack, 1.0E-9);
                    for(i=0; i<=nmain+nslack-1; i++)
                    {
                        if( (double)(math.sqr(colnorms[i]))!=(double)(0) )
                        {
                            pg[i] = pg[i]/math.sqr(colnorms[i]);
                        }
                        else
                        {
                            pg[i] = 0.0;
                        }
                    }
                    
                    //
                    // Check GNorm and feasibility.
                    // Exit when GNorm is exactly zero.
                    //
                    pgnorm = 0.0;
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        pgnorm += pg[i_]*pg[i_];
                    }
                    feaserr = Math.Sqrt(feaserr);
                    pgnorm = Math.Sqrt(pgnorm);
                    if( (double)(pgnorm)==(double)(0) )
                    {
                        result = (double)(feaserr)<=(double)(epsi);
                        return result;
                    }
                    
                    //
                    // calculate planned step length
                    //
                    vn = 0.0;
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        vn += g[i_]*pg[i_];
                    }
                    vd = 0;
                    for(i=0; i<=k-1; i++)
                    {
                        v = 0.0;
                        for(i_=0; i_<=nmain+nslack-1;i_++)
                        {
                            v += ce[i,i_]*pg[i_];
                        }
                        vd = vd+math.sqr(v);
                    }
                    stp = vn/vd;
                    
                    //
                    // Calculate step bound.
                    // Perform bounded step and post-process it
                    //
                    calculatestepbound(x, pg, -1.0, bndl, havebndl, bndu, havebndu, nmain, nslack, ref vartofreeze, ref valtofreeze, ref maxsteplen);
                    if( vartofreeze>=0 && (double)(maxsteplen)==(double)(0) )
                    {
                        result = false;
                        return result;
                    }
                    if( vartofreeze>=0 )
                    {
                        v = Math.Min(stp, maxsteplen);
                    }
                    else
                    {
                        v = stp;
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        xn[i_] = x[i_];
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        xn[i_] = xn[i_] - v*pg[i_];
                    }
                    postprocessboundedstep(ref xn, x, bndl, havebndl, bndu, havebndu, nmain, nslack, vartofreeze, valtofreeze, v, maxsteplen);
                    
                    //
                    // update X
                    // check stopping criteria
                    //
                    werechangesinconstraints = werechangesinconstraints || numberofchangedconstraints(xn, x, bndl, havebndl, bndu, havebndu, nmain, nslack)>0;
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        x[i_] = xn[i_];
                    }
                    gpaits = gpaits+1;
                    if( !werechangesinconstraints )
                    {
                        break;
                    }
                }
                
                //
                // Stage 3: decide to stop algorithm or not to stop
                //
                // 1. we can stop when last GPA run did NOT changed constraints status.
                //    It means that we've found final set of the active constraints even
                //    before GPA made its run. And it means that Newton step moved us to
                //    the minimum subject to the present constraints.
                //    Depending on feasibility error, True or False is returned.
                //
                feaserr = 0;
                for(i=0; i<=k-1; i++)
                {
                    v = 0.0;
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        v += ce[i,i_]*x[i_];
                    }
                    v = v-ce[i,nmain+nslack];
                    feaserr = feaserr+math.sqr(v);
                }
                feaserr = Math.Sqrt(feaserr);
                if( (double)(feaserr)<=(double)(epsi) )
                {
                    itswithintolerance = itswithintolerance+1;
                }
                else
                {
                    itswithintolerance = 0;
                }
                if( !werechangesinconstraints || itswithintolerance>=maxitswithintolerance )
                {
                    result = (double)(feaserr)<=(double)(epsi);
                    return result;
                }
                itscount = itscount+1;
            }
            return result;
        }


        /*************************************************************************
            This function check, that input derivatives are right. First it scale
        parameters DF0 and DF1 from segment [A;B] to [0;1]. Than it build Hermite
        spline and derivative of it in 0,5. Search scale as Max(DF0,DF1, |F0-F1|).
        Right derivative has to satisfy condition:
            |H-F|/S<=0,01, |H'-F'|/S<=0,01.
            
        INPUT PARAMETERS:
            F0  -   function's value in X-TestStep point;
            DF0 -   derivative's value in X-TestStep point;
            F1  -   function's value in X+TestStep point;
            DF1 -   derivative's value in X+TestStep point;
            F   -   testing function's value;
            DF  -   testing derivative's value;
           Width-   width of verification segment.

        RESULT:
            If input derivatives is right then function returns true, else 
            function returns false.
            
          -- ALGLIB --
             Copyright 29.05.2012 by Bochkanov Sergey
        *************************************************************************/
        public static bool derivativecheck(double f0,
            double df0,
            double f1,
            double df1,
            double f,
            double df,
            double width)
        {
            bool result = new bool();
            double s = 0;
            double h = 0;
            double dh = 0;

            df = width*df;
            df0 = width*df0;
            df1 = width*df1;
            s = Math.Max(Math.Max(Math.Abs(df0), Math.Abs(df1)), Math.Abs(f1-f0));
            h = 0.5*f0+0.125*df0+0.5*f1-0.125*df1;
            dh = -(1.5*f0)-0.25*df0+1.5*f1-0.25*df1;
            if( (double)(s)!=(double)(0) )
            {
                if( (double)(Math.Abs(h-f)/s)>(double)(0.001) || (double)(Math.Abs(dh-df)/s)>(double)(0.001) )
                {
                    result = false;
                    return result;
                }
            }
            else
            {
                if( (double)(h-f)!=(double)(0.0) || (double)(dh-df)!=(double)(0.0) )
                {
                    result = false;
                    return result;
                }
            }
            result = true;
            return result;
        }


    }
    public class mincg
    {
        /*************************************************************************
        This object stores state of the nonlinear CG optimizer.

        You should use ALGLIB functions to work with this object.
        *************************************************************************/
        public class mincgstate
        {
            public int n;
            public double epsg;
            public double epsf;
            public double epsx;
            public int maxits;
            public double stpmax;
            public double suggestedstep;
            public bool xrep;
            public bool drep;
            public int cgtype;
            public int prectype;
            public double[] diagh;
            public double[] diaghl2;
            public double[,] vcorr;
            public int vcnt;
            public double[] s;
            public double diffstep;
            public int nfev;
            public int mcstage;
            public int k;
            public double[] xk;
            public double[] dk;
            public double[] xn;
            public double[] dn;
            public double[] d;
            public double fold;
            public double stp;
            public double curstpmax;
            public double[] yk;
            public double lastgoodstep;
            public double lastscaledstep;
            public int mcinfo;
            public bool innerresetneeded;
            public bool terminationneeded;
            public double trimthreshold;
            public int rstimer;
            public double[] x;
            public double f;
            public double[] g;
            public bool needf;
            public bool needfg;
            public bool xupdated;
            public bool algpowerup;
            public bool lsstart;
            public bool lsend;
            public double teststep;
            public rcommstate rstate;
            public int repiterationscount;
            public int repnfev;
            public int repvaridx;
            public int repterminationtype;
            public int debugrestartscount;
            public linmin.linminstate lstate;
            public double fbase;
            public double fm2;
            public double fm1;
            public double fp1;
            public double fp2;
            public double betahs;
            public double betady;
            public double[] work0;
            public double[] work1;
            public mincgstate()
            {
                diagh = new double[0];
                diaghl2 = new double[0];
                vcorr = new double[0,0];
                s = new double[0];
                xk = new double[0];
                dk = new double[0];
                xn = new double[0];
                dn = new double[0];
                d = new double[0];
                yk = new double[0];
                x = new double[0];
                g = new double[0];
                rstate = new rcommstate();
                lstate = new linmin.linminstate();
                work0 = new double[0];
                work1 = new double[0];
            }
        };


        public class mincgreport
        {
            public int iterationscount;
            public int nfev;
            public int varidx;
            public int terminationtype;
        };




        public const int rscountdownlen = 10;
        public const double gtol = 0.3;


        /*************************************************************************
                NONLINEAR CONJUGATE GRADIENT METHOD

        DESCRIPTION:
        The subroutine minimizes function F(x) of N arguments by using one of  the
        nonlinear conjugate gradient methods.

        These CG methods are globally convergent (even on non-convex functions) as
        long as grad(f) is Lipschitz continuous in  a  some  neighborhood  of  the
        L = { x : f(x)<=f(x0) }.


        REQUIREMENTS:
        Algorithm will request following information during its operation:
        * function value F and its gradient G (simultaneously) at given point X


        USAGE:
        1. User initializes algorithm state with MinCGCreate() call
        2. User tunes solver parameters with MinCGSetCond(), MinCGSetStpMax() and
           other functions
        3. User calls MinCGOptimize() function which takes algorithm  state   and
           pointer (delegate, etc.) to callback function which calculates F/G.
        4. User calls MinCGResults() to get solution
        5. Optionally, user may call MinCGRestartFrom() to solve another  problem
           with same N but another starting point and/or another function.
           MinCGRestartFrom() allows to reuse already initialized structure.


        INPUT PARAMETERS:
            N       -   problem dimension, N>0:
                        * if given, only leading N elements of X are used
                        * if not given, automatically determined from size of X
            X       -   starting point, array[0..N-1].

        OUTPUT PARAMETERS:
            State   -   structure which stores algorithm state

          -- ALGLIB --
             Copyright 25.03.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgcreate(int n,
            double[] x,
            mincgstate state)
        {
            alglib.ap.assert(n>=1, "MinCGCreate: N too small!");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinCGCreate: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinCGCreate: X contains infinite or NaN values!");
            mincginitinternal(n, 0.0, state);
            mincgrestartfrom(state, x);
        }


        /*************************************************************************
        The subroutine is finite difference variant of MinCGCreate(). It uses
        finite differences in order to differentiate target function.

        Description below contains information which is specific to this function
        only. We recommend to read comments on MinCGCreate() in order to get more
        information about creation of CG optimizer.

        INPUT PARAMETERS:
            N       -   problem dimension, N>0:
                        * if given, only leading N elements of X are used
                        * if not given, automatically determined from size of X
            X       -   starting point, array[0..N-1].
            DiffStep-   differentiation step, >0

        OUTPUT PARAMETERS:
            State   -   structure which stores algorithm state

        NOTES:
        1. algorithm uses 4-point central formula for differentiation.
        2. differentiation step along I-th axis is equal to DiffStep*S[I] where
           S[] is scaling vector which can be set by MinCGSetScale() call.
        3. we recommend you to use moderate values of  differentiation  step.  Too
           large step will result in too large truncation  errors, while too small
           step will result in too large numerical  errors.  1.0E-6  can  be  good
           value to start with.
        4. Numerical  differentiation  is   very   inefficient  -   one   gradient
           calculation needs 4*N function evaluations. This function will work for
           any N - either small (1...10), moderate (10...100) or  large  (100...).
           However, performance penalty will be too severe for any N's except  for
           small ones.
           We should also say that code which relies on numerical  differentiation
           is  less  robust  and  precise.  L-BFGS  needs  exact  gradient values.
           Imprecise  gradient may slow down  convergence,  especially  on  highly
           nonlinear problems.
           Thus  we  recommend to use this function for fast prototyping on small-
           dimensional problems only, and to implement analytical gradient as soon
           as possible.

          -- ALGLIB --
             Copyright 16.05.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgcreatef(int n,
            double[] x,
            double diffstep,
            mincgstate state)
        {
            alglib.ap.assert(n>=1, "MinCGCreateF: N too small!");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinCGCreateF: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinCGCreateF: X contains infinite or NaN values!");
            alglib.ap.assert(math.isfinite(diffstep), "MinCGCreateF: DiffStep is infinite or NaN!");
            alglib.ap.assert((double)(diffstep)>(double)(0), "MinCGCreateF: DiffStep is non-positive!");
            mincginitinternal(n, diffstep, state);
            mincgrestartfrom(state, x);
        }


        /*************************************************************************
        This function sets stopping conditions for CG optimization algorithm.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            EpsG    -   >=0
                        The  subroutine  finishes  its  work   if   the  condition
                        |v|<EpsG is satisfied, where:
                        * |.| means Euclidian norm
                        * v - scaled gradient vector, v[i]=g[i]*s[i]
                        * g - gradient
                        * s - scaling coefficients set by MinCGSetScale()
            EpsF    -   >=0
                        The  subroutine  finishes  its work if on k+1-th iteration
                        the  condition  |F(k+1)-F(k)|<=EpsF*max{|F(k)|,|F(k+1)|,1}
                        is satisfied.
            EpsX    -   >=0
                        The subroutine finishes its work if  on  k+1-th  iteration
                        the condition |v|<=EpsX is fulfilled, where:
                        * |.| means Euclidian norm
                        * v - scaled step vector, v[i]=dx[i]/s[i]
                        * dx - ste pvector, dx=X(k+1)-X(k)
                        * s - scaling coefficients set by MinCGSetScale()
            MaxIts  -   maximum number of iterations. If MaxIts=0, the  number  of
                        iterations is unlimited.

        Passing EpsG=0, EpsF=0, EpsX=0 and MaxIts=0 (simultaneously) will lead to
        automatic stopping criterion selection (small EpsX).

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetcond(mincgstate state,
            double epsg,
            double epsf,
            double epsx,
            int maxits)
        {
            alglib.ap.assert(math.isfinite(epsg), "MinCGSetCond: EpsG is not finite number!");
            alglib.ap.assert((double)(epsg)>=(double)(0), "MinCGSetCond: negative EpsG!");
            alglib.ap.assert(math.isfinite(epsf), "MinCGSetCond: EpsF is not finite number!");
            alglib.ap.assert((double)(epsf)>=(double)(0), "MinCGSetCond: negative EpsF!");
            alglib.ap.assert(math.isfinite(epsx), "MinCGSetCond: EpsX is not finite number!");
            alglib.ap.assert((double)(epsx)>=(double)(0), "MinCGSetCond: negative EpsX!");
            alglib.ap.assert(maxits>=0, "MinCGSetCond: negative MaxIts!");
            if( (((double)(epsg)==(double)(0) && (double)(epsf)==(double)(0)) && (double)(epsx)==(double)(0)) && maxits==0 )
            {
                epsx = 1.0E-6;
            }
            state.epsg = epsg;
            state.epsf = epsf;
            state.epsx = epsx;
            state.maxits = maxits;
        }


        /*************************************************************************
        This function sets scaling coefficients for CG optimizer.

        ALGLIB optimizers use scaling matrices to test stopping  conditions  (step
        size and gradient are scaled before comparison with tolerances).  Scale of
        the I-th variable is a translation invariant measure of:
        a) "how large" the variable is
        b) how large the step should be to make significant changes in the function

        Scaling is also used by finite difference variant of CG optimizer  -  step
        along I-th axis is equal to DiffStep*S[I].

        In   most   optimizers  (and  in  the  CG  too)  scaling is NOT a form  of
        preconditioning. It just  affects  stopping  conditions.  You  should  set
        preconditioner by separate call to one of the MinCGSetPrec...() functions.

        There  is  special  preconditioning  mode, however,  which  uses   scaling
        coefficients to form diagonal preconditioning matrix. You  can  turn  this
        mode on, if you want.   But  you should understand that scaling is not the
        same thing as preconditioning - these are two different, although  related
        forms of tuning solver.

        INPUT PARAMETERS:
            State   -   structure stores algorithm state
            S       -   array[N], non-zero scaling coefficients
                        S[i] may be negative, sign doesn't matter.

          -- ALGLIB --
             Copyright 14.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetscale(mincgstate state,
            double[] s)
        {
            int i = 0;

            alglib.ap.assert(alglib.ap.len(s)>=state.n, "MinCGSetScale: Length(S)<N");
            for(i=0; i<=state.n-1; i++)
            {
                alglib.ap.assert(math.isfinite(s[i]), "MinCGSetScale: S contains infinite or NAN elements");
                alglib.ap.assert((double)(s[i])!=(double)(0), "MinCGSetScale: S contains zero elements");
                state.s[i] = Math.Abs(s[i]);
            }
        }


        /*************************************************************************
        This function turns on/off reporting.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            NeedXRep-   whether iteration reports are needed or not

        If NeedXRep is True, algorithm will call rep() callback function if  it is
        provided to MinCGOptimize().

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetxrep(mincgstate state,
            bool needxrep)
        {
            state.xrep = needxrep;
        }


        /*************************************************************************
        This function turns on/off line search reports.
        These reports are described in more details in developer-only  comments on
        MinCGState object.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            NeedDRep-   whether line search reports are needed or not

        This function is intended for private use only. Turning it on artificially
        may cause program failure.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetdrep(mincgstate state,
            bool needdrep)
        {
            state.drep = needdrep;
        }


        /*************************************************************************
        This function sets CG algorithm.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            CGType  -   algorithm type:
                        * -1    automatic selection of the best algorithm
                        * 0     DY (Dai and Yuan) algorithm
                        * 1     Hybrid DY-HS algorithm

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetcgtype(mincgstate state,
            int cgtype)
        {
            alglib.ap.assert(cgtype>=-1 && cgtype<=1, "MinCGSetCGType: incorrect CGType!");
            if( cgtype==-1 )
            {
                cgtype = 1;
            }
            state.cgtype = cgtype;
        }


        /*************************************************************************
        This function sets maximum step length

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            StpMax  -   maximum step length, >=0. Set StpMax to 0.0,  if you don't
                        want to limit step length.

        Use this subroutine when you optimize target function which contains exp()
        or  other  fast  growing  functions,  and optimization algorithm makes too
        large  steps  which  leads  to overflow. This function allows us to reject
        steps  that  are  too  large  (and  therefore  expose  us  to the possible
        overflow) without actually calculating function value at the x+stp*d.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetstpmax(mincgstate state,
            double stpmax)
        {
            alglib.ap.assert(math.isfinite(stpmax), "MinCGSetStpMax: StpMax is not finite!");
            alglib.ap.assert((double)(stpmax)>=(double)(0), "MinCGSetStpMax: StpMax<0!");
            state.stpmax = stpmax;
        }


        /*************************************************************************
        This function allows to suggest initial step length to the CG algorithm.

        Suggested  step  length  is used as starting point for the line search. It
        can be useful when you have  badly  scaled  problem,  i.e.  when  ||grad||
        (which is used as initial estimate for the first step) is many  orders  of
        magnitude different from the desired step.

        Line search  may  fail  on  such problems without good estimate of initial
        step length. Imagine, for example, problem with ||grad||=10^50 and desired
        step equal to 0.1 Line  search function will use 10^50  as  initial  step,
        then  it  will  decrease step length by 2 (up to 20 attempts) and will get
        10^44, which is still too large.

        This function allows us to tell than line search should  be  started  from
        some moderate step length, like 1.0, so algorithm will be able  to  detect
        desired step length in a several searches.

        Default behavior (when no step is suggested) is to use preconditioner,  if
        it is available, to generate initial estimate of step length.

        This function influences only first iteration of algorithm. It  should  be
        called between MinCGCreate/MinCGRestartFrom() call and MinCGOptimize call.
        Suggested step is ignored if you have preconditioner.

        INPUT PARAMETERS:
            State   -   structure used to store algorithm state.
            Stp     -   initial estimate of the step length.
                        Can be zero (no estimate).

          -- ALGLIB --
             Copyright 30.07.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsuggeststep(mincgstate state,
            double stp)
        {
            alglib.ap.assert(math.isfinite(stp), "MinCGSuggestStep: Stp is infinite or NAN");
            alglib.ap.assert((double)(stp)>=(double)(0), "MinCGSuggestStep: Stp<0");
            state.suggestedstep = stp;
        }


        /*************************************************************************
        Modification of the preconditioner: preconditioning is turned off.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state

        NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
        iterations.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetprecdefault(mincgstate state)
        {
            state.prectype = 0;
            state.innerresetneeded = true;
        }


        /*************************************************************************
        Modification  of  the  preconditioner:  diagonal of approximate Hessian is
        used.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            D       -   diagonal of the approximate Hessian, array[0..N-1],
                        (if larger, only leading N elements are used).

        NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
        iterations.

        NOTE 2: D[i] should be positive. Exception will be thrown otherwise.

        NOTE 3: you should pass diagonal of approximate Hessian - NOT ITS INVERSE.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetprecdiag(mincgstate state,
            double[] d)
        {
            int i = 0;

            alglib.ap.assert(alglib.ap.len(d)>=state.n, "MinCGSetPrecDiag: D is too short");
            for(i=0; i<=state.n-1; i++)
            {
                alglib.ap.assert(math.isfinite(d[i]), "MinCGSetPrecDiag: D contains infinite or NAN elements");
                alglib.ap.assert((double)(d[i])>(double)(0), "MinCGSetPrecDiag: D contains non-positive elements");
            }
            mincgsetprecdiagfast(state, d);
        }


        /*************************************************************************
        Modification of the preconditioner: scale-based diagonal preconditioning.

        This preconditioning mode can be useful when you  don't  have  approximate
        diagonal of Hessian, but you know that your  variables  are  badly  scaled
        (for  example,  one  variable is in [1,10], and another in [1000,100000]),
        and most part of the ill-conditioning comes from different scales of vars.

        In this case simple  scale-based  preconditioner,  with H[i] = 1/(s[i]^2),
        can greatly improve convergence.

        IMPRTANT: you should set scale of your variables with MinCGSetScale() call
        (before or after MinCGSetPrecScale() call). Without knowledge of the scale
        of your variables scale-based preconditioner will be just unit matrix.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state

        NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
        iterations.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetprecscale(mincgstate state)
        {
            state.prectype = 3;
            state.innerresetneeded = true;
        }


        /*************************************************************************
        NOTES:

        1. This function has two different implementations: one which  uses  exact
           (analytical) user-supplied  gradient, and one which uses function value
           only  and  numerically  differentiates  function  in  order  to  obtain
           gradient.
           
           Depending  on  the  specific  function  used to create optimizer object
           (either MinCGCreate()  for analytical gradient  or  MinCGCreateF()  for
           numerical differentiation) you should  choose  appropriate  variant  of
           MinCGOptimize() - one which accepts function AND gradient or one  which
           accepts function ONLY.

           Be careful to choose variant of MinCGOptimize()  which  corresponds  to
           your optimization scheme! Table below lists different  combinations  of
           callback (function/gradient) passed  to  MinCGOptimize()  and  specific
           function used to create optimizer.
           

                          |         USER PASSED TO MinCGOptimize()
           CREATED WITH   |  function only   |  function and gradient
           ------------------------------------------------------------
           MinCGCreateF() |     work                FAIL
           MinCGCreate()  |     FAIL                work

           Here "FAIL" denotes inappropriate combinations  of  optimizer  creation
           function and MinCGOptimize() version. Attemps to use  such  combination
           (for  example,  to create optimizer with  MinCGCreateF()  and  to  pass
           gradient information to MinCGOptimize()) will lead to  exception  being
           thrown. Either  you  did  not  pass  gradient when it WAS needed or you
           passed gradient when it was NOT needed.

          -- ALGLIB --
             Copyright 20.04.2009 by Bochkanov Sergey
        *************************************************************************/
        public static bool mincgiteration(mincgstate state)
        {
            bool result = new bool();
            int n = 0;
            int i = 0;
            double betak = 0;
            double v = 0;
            double vv = 0;
            int i_ = 0;

            
            //
            // Reverse communication preparations
            // I know it looks ugly, but it works the same way
            // anywhere from C++ to Python.
            //
            // This code initializes locals by:
            // * random values determined during code
            //   generation - on first subroutine call
            // * values from previous call - on subsequent calls
            //
            if( state.rstate.stage>=0 )
            {
                n = state.rstate.ia[0];
                i = state.rstate.ia[1];
                betak = state.rstate.ra[0];
                v = state.rstate.ra[1];
                vv = state.rstate.ra[2];
            }
            else
            {
                n = -983;
                i = -989;
                betak = -834;
                v = 900;
                vv = -287;
            }
            if( state.rstate.stage==0 )
            {
                goto lbl_0;
            }
            if( state.rstate.stage==1 )
            {
                goto lbl_1;
            }
            if( state.rstate.stage==2 )
            {
                goto lbl_2;
            }
            if( state.rstate.stage==3 )
            {
                goto lbl_3;
            }
            if( state.rstate.stage==4 )
            {
                goto lbl_4;
            }
            if( state.rstate.stage==5 )
            {
                goto lbl_5;
            }
            if( state.rstate.stage==6 )
            {
                goto lbl_6;
            }
            if( state.rstate.stage==7 )
            {
                goto lbl_7;
            }
            if( state.rstate.stage==8 )
            {
                goto lbl_8;
            }
            if( state.rstate.stage==9 )
            {
                goto lbl_9;
            }
            if( state.rstate.stage==10 )
            {
                goto lbl_10;
            }
            if( state.rstate.stage==11 )
            {
                goto lbl_11;
            }
            if( state.rstate.stage==12 )
            {
                goto lbl_12;
            }
            if( state.rstate.stage==13 )
            {
                goto lbl_13;
            }
            if( state.rstate.stage==14 )
            {
                goto lbl_14;
            }
            if( state.rstate.stage==15 )
            {
                goto lbl_15;
            }
            if( state.rstate.stage==16 )
            {
                goto lbl_16;
            }
            if( state.rstate.stage==17 )
            {
                goto lbl_17;
            }
            if( state.rstate.stage==18 )
            {
                goto lbl_18;
            }
            if( state.rstate.stage==19 )
            {
                goto lbl_19;
            }
            
            //
            // Routine body
            //
            
            //
            // Prepare
            //
            n = state.n;
            state.repterminationtype = 0;
            state.repiterationscount = 0;
            state.repvaridx = -1;
            state.repnfev = 0;
            state.debugrestartscount = 0;
            
            //
            //  Check, that transferred derivative value is right
            //
            clearrequestfields(state);
            if( !((double)(state.diffstep)==(double)(0) && (double)(state.teststep)>(double)(0)) )
            {
                goto lbl_20;
            }
            state.needfg = true;
            i = 0;
        lbl_22:
            if( i>n-1 )
            {
                goto lbl_24;
            }
            v = state.x[i];
            state.x[i] = v-state.teststep*state.s[i];
            state.rstate.stage = 0;
            goto lbl_rcomm;
        lbl_0:
            state.fm1 = state.f;
            state.fp1 = state.g[i];
            state.x[i] = v+state.teststep*state.s[i];
            state.rstate.stage = 1;
            goto lbl_rcomm;
        lbl_1:
            state.fm2 = state.f;
            state.fp2 = state.g[i];
            state.x[i] = v;
            state.rstate.stage = 2;
            goto lbl_rcomm;
        lbl_2:
            
            //
            // 2*State.TestStep   -   scale parameter
            // width of segment [Xi-TestStep;Xi+TestStep]
            //
            if( !optserv.derivativecheck(state.fm1, state.fp1, state.fm2, state.fp2, state.f, state.g[i], 2*state.teststep) )
            {
                state.repvaridx = i;
                state.repterminationtype = -7;
                result = false;
                return result;
            }
            i = i+1;
            goto lbl_22;
        lbl_24:
            state.needfg = false;
        lbl_20:
            
            //
            // Preparations continue:
            // * set XK
            // * calculate F/G
            // * set DK to -G
            // * powerup algo (it may change preconditioner)
            // * apply preconditioner to DK
            // * report update of X
            // * check stopping conditions for G
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.xk[i_] = state.x[i_];
            }
            state.terminationneeded = false;
            clearrequestfields(state);
            if( (double)(state.diffstep)!=(double)(0) )
            {
                goto lbl_25;
            }
            state.needfg = true;
            state.rstate.stage = 3;
            goto lbl_rcomm;
        lbl_3:
            state.needfg = false;
            goto lbl_26;
        lbl_25:
            state.needf = true;
            state.rstate.stage = 4;
            goto lbl_rcomm;
        lbl_4:
            state.fbase = state.f;
            i = 0;
        lbl_27:
            if( i>n-1 )
            {
                goto lbl_29;
            }
            v = state.x[i];
            state.x[i] = v-state.diffstep*state.s[i];
            state.rstate.stage = 5;
            goto lbl_rcomm;
        lbl_5:
            state.fm2 = state.f;
            state.x[i] = v-0.5*state.diffstep*state.s[i];
            state.rstate.stage = 6;
            goto lbl_rcomm;
        lbl_6:
            state.fm1 = state.f;
            state.x[i] = v+0.5*state.diffstep*state.s[i];
            state.rstate.stage = 7;
            goto lbl_rcomm;
        lbl_7:
            state.fp1 = state.f;
            state.x[i] = v+state.diffstep*state.s[i];
            state.rstate.stage = 8;
            goto lbl_rcomm;
        lbl_8:
            state.fp2 = state.f;
            state.x[i] = v;
            state.g[i] = (8*(state.fp1-state.fm1)-(state.fp2-state.fm2))/(6*state.diffstep*state.s[i]);
            i = i+1;
            goto lbl_27;
        lbl_29:
            state.f = state.fbase;
            state.needf = false;
        lbl_26:
            if( !state.drep )
            {
                goto lbl_30;
            }
            
            //
            // Report algorithm powerup (if needed)
            //
            clearrequestfields(state);
            state.algpowerup = true;
            state.rstate.stage = 9;
            goto lbl_rcomm;
        lbl_9:
            state.algpowerup = false;
        lbl_30:
            optserv.trimprepare(state.f, ref state.trimthreshold);
            for(i_=0; i_<=n-1;i_++)
            {
                state.dk[i_] = -state.g[i_];
            }
            preconditionedmultiply(state, ref state.dk, ref state.work0, ref state.work1);
            if( !state.xrep )
            {
                goto lbl_32;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 10;
            goto lbl_rcomm;
        lbl_10:
            state.xupdated = false;
        lbl_32:
            if( state.terminationneeded )
            {
                for(i_=0; i_<=n-1;i_++)
                {
                    state.xn[i_] = state.xk[i_];
                }
                state.repterminationtype = 8;
                result = false;
                return result;
            }
            v = 0;
            for(i=0; i<=n-1; i++)
            {
                v = v+math.sqr(state.g[i]*state.s[i]);
            }
            if( (double)(Math.Sqrt(v))<=(double)(state.epsg) )
            {
                for(i_=0; i_<=n-1;i_++)
                {
                    state.xn[i_] = state.xk[i_];
                }
                state.repterminationtype = 4;
                result = false;
                return result;
            }
            state.repnfev = 1;
            state.k = 0;
            state.fold = state.f;
            
            //
            // Choose initial step.
            // Apply preconditioner, if we have something other than default.
            //
            if( state.prectype==2 || state.prectype==3 )
            {
                
                //
                // because we use preconditioner, step length must be equal
                // to the norm of DK
                //
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.dk[i_]*state.dk[i_];
                }
                state.lastgoodstep = Math.Sqrt(v);
            }
            else
            {
                
                //
                // No preconditioner is used, we try to use suggested step
                //
                if( (double)(state.suggestedstep)>(double)(0) )
                {
                    state.lastgoodstep = state.suggestedstep;
                }
                else
                {
                    state.lastgoodstep = 1.0;
                }
            }
            
            //
            // Main cycle
            //
            state.rstimer = rscountdownlen;
        lbl_34:
            if( false )
            {
                goto lbl_35;
            }
            
            //
            // * clear reset flag
            // * clear termination flag
            // * store G[k] for later calculation of Y[k]
            // * prepare starting point and direction and step length for line search
            //
            state.innerresetneeded = false;
            state.terminationneeded = false;
            for(i_=0; i_<=n-1;i_++)
            {
                state.yk[i_] = -state.g[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.d[i_] = state.dk[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xk[i_];
            }
            state.mcstage = 0;
            state.stp = 1.0;
            linmin.linminnormalized(ref state.d, ref state.stp, n);
            if( (double)(state.lastgoodstep)!=(double)(0) )
            {
                state.stp = state.lastgoodstep;
            }
            state.curstpmax = state.stpmax;
            
            //
            // Report beginning of line search (if needed)
            // Terminate algorithm, if user request was detected
            //
            if( !state.drep )
            {
                goto lbl_36;
            }
            clearrequestfields(state);
            state.lsstart = true;
            state.rstate.stage = 11;
            goto lbl_rcomm;
        lbl_11:
            state.lsstart = false;
        lbl_36:
            if( state.terminationneeded )
            {
                for(i_=0; i_<=n-1;i_++)
                {
                    state.xn[i_] = state.x[i_];
                }
                state.repterminationtype = 8;
                result = false;
                return result;
            }
            
            //
            // Minimization along D
            //
            linmin.mcsrch(n, ref state.x, ref state.f, ref state.g, state.d, ref state.stp, state.curstpmax, gtol, ref state.mcinfo, ref state.nfev, ref state.work0, state.lstate, ref state.mcstage);
        lbl_38:
            if( state.mcstage==0 )
            {
                goto lbl_39;
            }
            
            //
            // Calculate function/gradient using either
            // analytical gradient supplied by user
            // or finite difference approximation.
            //
            // "Trim" function in order to handle near-singularity points.
            //
            clearrequestfields(state);
            if( (double)(state.diffstep)!=(double)(0) )
            {
                goto lbl_40;
            }
            state.needfg = true;
            state.rstate.stage = 12;
            goto lbl_rcomm;
        lbl_12:
            state.needfg = false;
            goto lbl_41;
        lbl_40:
            state.needf = true;
            state.rstate.stage = 13;
            goto lbl_rcomm;
        lbl_13:
            state.fbase = state.f;
            i = 0;
        lbl_42:
            if( i>n-1 )
            {
                goto lbl_44;
            }
            v = state.x[i];
            state.x[i] = v-state.diffstep*state.s[i];
            state.rstate.stage = 14;
            goto lbl_rcomm;
        lbl_14:
            state.fm2 = state.f;
            state.x[i] = v-0.5*state.diffstep*state.s[i];
            state.rstate.stage = 15;
            goto lbl_rcomm;
        lbl_15:
            state.fm1 = state.f;
            state.x[i] = v+0.5*state.diffstep*state.s[i];
            state.rstate.stage = 16;
            goto lbl_rcomm;
        lbl_16:
            state.fp1 = state.f;
            state.x[i] = v+state.diffstep*state.s[i];
            state.rstate.stage = 17;
            goto lbl_rcomm;
        lbl_17:
            state.fp2 = state.f;
            state.x[i] = v;
            state.g[i] = (8*(state.fp1-state.fm1)-(state.fp2-state.fm2))/(6*state.diffstep*state.s[i]);
            i = i+1;
            goto lbl_42;
        lbl_44:
            state.f = state.fbase;
            state.needf = false;
        lbl_41:
            optserv.trimfunction(ref state.f, ref state.g, n, state.trimthreshold);
            
            //
            // Call MCSRCH again
            //
            linmin.mcsrch(n, ref state.x, ref state.f, ref state.g, state.d, ref state.stp, state.curstpmax, gtol, ref state.mcinfo, ref state.nfev, ref state.work0, state.lstate, ref state.mcstage);
            goto lbl_38;
        lbl_39:
            
            //
            // * report end of line search
            // * store current point to XN
            // * report iteration
            // * terminate algorithm if user request was detected
            //
            if( !state.drep )
            {
                goto lbl_45;
            }
            
            //
            // Report end of line search (if needed)
            //
            clearrequestfields(state);
            state.lsend = true;
            state.rstate.stage = 18;
            goto lbl_rcomm;
        lbl_18:
            state.lsend = false;
        lbl_45:
            for(i_=0; i_<=n-1;i_++)
            {
                state.xn[i_] = state.x[i_];
            }
            if( !state.xrep )
            {
                goto lbl_47;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 19;
            goto lbl_rcomm;
        lbl_19:
            state.xupdated = false;
        lbl_47:
            if( state.terminationneeded )
            {
                for(i_=0; i_<=n-1;i_++)
                {
                    state.xn[i_] = state.x[i_];
                }
                state.repterminationtype = 8;
                result = false;
                return result;
            }
            
            //
            // Line search is finished.
            // * calculate BetaK
            // * calculate DN
            // * update timers
            // * calculate step length:
            //   * LastScaledStep is ALWAYS calculated because it is used in the stopping criteria
            //   * LastGoodStep is updated only when MCINFO is equal to 1 (Wolfe conditions hold).
            //     See below for more explanation.
            //
            if( state.mcinfo==1 && !state.innerresetneeded )
            {
                
                //
                // Standard Wolfe conditions hold
                // Calculate Y[K] and D[K]'*Y[K]
                //
                for(i_=0; i_<=n-1;i_++)
                {
                    state.yk[i_] = state.yk[i_] + state.g[i_];
                }
                vv = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    vv += state.yk[i_]*state.dk[i_];
                }
                
                //
                // Calculate BetaK according to DY formula
                //
                v = preconditionedmultiply2(state, ref state.g, ref state.g, ref state.work0, ref state.work1);
                state.betady = v/vv;
                
                //
                // Calculate BetaK according to HS formula
                //
                v = preconditionedmultiply2(state, ref state.g, ref state.yk, ref state.work0, ref state.work1);
                state.betahs = v/vv;
                
                //
                // Choose BetaK
                //
                if( state.cgtype==0 )
                {
                    betak = state.betady;
                }
                if( state.cgtype==1 )
                {
                    betak = Math.Max(0, Math.Min(state.betady, state.betahs));
                }
            }
            else
            {
                
                //
                // Something is wrong (may be function is too wild or too flat)
                // or we just have to restart algo.
                //
                // We'll set BetaK=0, which will restart CG algorithm.
                // We can stop later (during normal checks) if stopping conditions are met.
                //
                betak = 0;
                state.debugrestartscount = state.debugrestartscount+1;
            }
            if( state.repiterationscount>0 && state.repiterationscount%(3+n)==0 )
            {
                
                //
                // clear Beta every N iterations
                //
                betak = 0;
            }
            if( state.mcinfo==1 || state.mcinfo==5 )
            {
                state.rstimer = rscountdownlen;
            }
            else
            {
                state.rstimer = state.rstimer-1;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.dn[i_] = -state.g[i_];
            }
            preconditionedmultiply(state, ref state.dn, ref state.work0, ref state.work1);
            for(i_=0; i_<=n-1;i_++)
            {
                state.dn[i_] = state.dn[i_] + betak*state.dk[i_];
            }
            state.lastscaledstep = 0.0;
            for(i=0; i<=n-1; i++)
            {
                state.lastscaledstep = state.lastscaledstep+math.sqr(state.d[i]/state.s[i]);
            }
            state.lastscaledstep = state.stp*Math.Sqrt(state.lastscaledstep);
            if( state.mcinfo==1 )
            {
                
                //
                // Step is good (Wolfe conditions hold), update LastGoodStep.
                //
                // This check for MCINFO=1 is essential because sometimes in the
                // constrained optimization setting we may take very short steps
                // (like 1E-15) because we were very close to boundary of the
                // feasible area. Such short step does not mean that we've converged
                // to the solution - it was so short because we were close to the
                // boundary and there was a limit on step length.
                //
                // So having such short step is quite normal situation. However, we
                // should NOT start next iteration from step whose initial length is
                // estimated as 1E-15 because it may lead to the failure of the
                // linear minimizer (step is too short, function does not changes,
                // line search stagnates).
                //
                state.lastgoodstep = 0;
                for(i=0; i<=n-1; i++)
                {
                    state.lastgoodstep = state.lastgoodstep+math.sqr(state.d[i]);
                }
                state.lastgoodstep = state.stp*Math.Sqrt(state.lastgoodstep);
            }
            
            //
            // Update information.
            // Check stopping conditions.
            //
            state.repnfev = state.repnfev+state.nfev;
            state.repiterationscount = state.repiterationscount+1;
            if( state.repiterationscount>=state.maxits && state.maxits>0 )
            {
                
                //
                // Too many iterations
                //
                state.repterminationtype = 5;
                result = false;
                return result;
            }
            v = 0;
            for(i=0; i<=n-1; i++)
            {
                v = v+math.sqr(state.g[i]*state.s[i]);
            }
            if( (double)(Math.Sqrt(v))<=(double)(state.epsg) )
            {
                
                //
                // Gradient is small enough
                //
                state.repterminationtype = 4;
                result = false;
                return result;
            }
            if( !state.innerresetneeded )
            {
                
                //
                // These conditions are checked only when no inner reset was requested by user
                //
                if( (double)(state.fold-state.f)<=(double)(state.epsf*Math.Max(Math.Abs(state.fold), Math.Max(Math.Abs(state.f), 1.0))) )
                {
                    
                    //
                    // F(k+1)-F(k) is small enough
                    //
                    state.repterminationtype = 1;
                    result = false;
                    return result;
                }
                if( (double)(state.lastscaledstep)<=(double)(state.epsx) )
                {
                    
                    //
                    // X(k+1)-X(k) is small enough
                    //
                    state.repterminationtype = 2;
                    result = false;
                    return result;
                }
            }
            if( state.rstimer<=0 )
            {
                
                //
                // Too many subsequent restarts
                //
                state.repterminationtype = 7;
                result = false;
                return result;
            }
            
            //
            // Shift Xk/Dk, update other information
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.xk[i_] = state.xn[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.dk[i_] = state.dn[i_];
            }
            state.fold = state.f;
            state.k = state.k+1;
            goto lbl_34;
        lbl_35:
            result = false;
            return result;
            
            //
            // Saving state
            //
        lbl_rcomm:
            result = true;
            state.rstate.ia[0] = n;
            state.rstate.ia[1] = i;
            state.rstate.ra[0] = betak;
            state.rstate.ra[1] = v;
            state.rstate.ra[2] = vv;
            return result;
        }


        /*************************************************************************
        Conjugate gradient results

        INPUT PARAMETERS:
            State   -   algorithm state

        OUTPUT PARAMETERS:
            X       -   array[0..N-1], solution
            Rep     -   optimization report:
                        * Rep.TerminationType completetion code:
                            * -7    gradient verification failed.
                                    See MinCGSetGradientCheck() for more information.
                            *  1    relative function improvement is no more than
                                    EpsF.
                            *  2    relative step is no more than EpsX.
                            *  4    gradient norm is no more than EpsG
                            *  5    MaxIts steps was taken
                            *  7    stopping conditions are too stringent,
                                    further improvement is impossible,
                                    we return best X found so far
                            *  8    terminated by user
                        * Rep.IterationsCount contains iterations count
                        * NFEV countains number of function calculations

          -- ALGLIB --
             Copyright 20.04.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgresults(mincgstate state,
            ref double[] x,
            mincgreport rep)
        {
            x = new double[0];

            mincgresultsbuf(state, ref x, rep);
        }


        /*************************************************************************
        Conjugate gradient results

        Buffered implementation of MinCGResults(), which uses pre-allocated buffer
        to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
        intended to be used in the inner cycles of performance critical algorithms
        where array reallocation penalty is too large to be ignored.

          -- ALGLIB --
             Copyright 20.04.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgresultsbuf(mincgstate state,
            ref double[] x,
            mincgreport rep)
        {
            int i_ = 0;

            if( alglib.ap.len(x)<state.n )
            {
                x = new double[state.n];
            }
            for(i_=0; i_<=state.n-1;i_++)
            {
                x[i_] = state.xn[i_];
            }
            rep.iterationscount = state.repiterationscount;
            rep.nfev = state.repnfev;
            rep.varidx = state.repvaridx;
            rep.terminationtype = state.repterminationtype;
        }


        /*************************************************************************
        This  subroutine  restarts  CG  algorithm from new point. All optimization
        parameters are left unchanged.

        This  function  allows  to  solve multiple  optimization  problems  (which
        must have same number of dimensions) without object reallocation penalty.

        INPUT PARAMETERS:
            State   -   structure used to store algorithm state.
            X       -   new starting point.

          -- ALGLIB --
             Copyright 30.07.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgrestartfrom(mincgstate state,
            double[] x)
        {
            int i_ = 0;

            alglib.ap.assert(alglib.ap.len(x)>=state.n, "MinCGRestartFrom: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, state.n), "MinCGCreate: X contains infinite or NaN values!");
            for(i_=0; i_<=state.n-1;i_++)
            {
                state.x[i_] = x[i_];
            }
            mincgsuggeststep(state, 0.0);
            state.rstate.ia = new int[1+1];
            state.rstate.ra = new double[2+1];
            state.rstate.stage = -1;
            clearrequestfields(state);
        }


        /*************************************************************************
        Faster version of MinCGSetPrecDiag(), for time-critical parts of code,
        without safety checks.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetprecdiagfast(mincgstate state,
            double[] d)
        {
            int i = 0;

            apserv.rvectorsetlengthatleast(ref state.diagh, state.n);
            apserv.rvectorsetlengthatleast(ref state.diaghl2, state.n);
            state.prectype = 2;
            state.vcnt = 0;
            state.innerresetneeded = true;
            for(i=0; i<=state.n-1; i++)
            {
                state.diagh[i] = d[i];
                state.diaghl2[i] = 0.0;
            }
        }


        /*************************************************************************
        This function sets low-rank preconditioner for Hessian matrix  H=D+V'*C*V,
        where:
        * H is a Hessian matrix, which is approximated by D/V/C
        * D=D1+D2 is a diagonal matrix, which includes two positive definite terms:
          * constant term D1 (is not updated or infrequently updated)
          * variable term D2 (can be cheaply updated from iteration to iteration)
        * V is a low-rank correction
        * C is a diagonal factor of low-rank correction

        Preconditioner P is calculated using approximate Woodburry formula:
            P  = D^(-1) - D^(-1)*V'*(C^(-1)+V*D1^(-1)*V')^(-1)*V*D^(-1)
               = D^(-1) - D^(-1)*VC'*VC*D^(-1),
        where
            VC = sqrt(B)*V
            B  = (C^(-1)+V*D1^(-1)*V')^(-1)
            
        Note that B is calculated using constant term (D1) only,  which  allows us
        to update D2 without recalculation of B or   VC.  Such  preconditioner  is
        exact when D2 is zero. When D2 is non-zero, it is only approximation,  but
        very good and cheap one.

        This function accepts D1, V, C.
        D2 is set to zero by default.

        Cost of this update is O(N*VCnt*VCnt), but D2 can be updated in just O(N)
        by MinCGSetPrecVarPart.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetpreclowrankfast(mincgstate state,
            double[] d1,
            double[] c,
            double[,] v,
            int vcnt)
        {
            int i = 0;
            int j = 0;
            int k = 0;
            int n = 0;
            double t = 0;
            double[,] b = new double[0,0];
            int i_ = 0;

            if( vcnt==0 )
            {
                mincgsetprecdiagfast(state, d1);
                return;
            }
            n = state.n;
            b = new double[vcnt, vcnt];
            apserv.rvectorsetlengthatleast(ref state.diagh, n);
            apserv.rvectorsetlengthatleast(ref state.diaghl2, n);
            apserv.rmatrixsetlengthatleast(ref state.vcorr, vcnt, n);
            state.prectype = 2;
            state.vcnt = vcnt;
            state.innerresetneeded = true;
            for(i=0; i<=n-1; i++)
            {
                state.diagh[i] = d1[i];
                state.diaghl2[i] = 0.0;
            }
            for(i=0; i<=vcnt-1; i++)
            {
                for(j=i; j<=vcnt-1; j++)
                {
                    t = 0;
                    for(k=0; k<=n-1; k++)
                    {
                        t = t+v[i,k]*v[j,k]/d1[k];
                    }
                    b[i,j] = t;
                }
                b[i,i] = b[i,i]+1.0/c[i];
            }
            if( !trfac.spdmatrixcholeskyrec(ref b, 0, vcnt, true, ref state.work0) )
            {
                state.vcnt = 0;
                return;
            }
            for(i=0; i<=vcnt-1; i++)
            {
                for(i_=0; i_<=n-1;i_++)
                {
                    state.vcorr[i,i_] = v[i,i_];
                }
                for(j=0; j<=i-1; j++)
                {
                    t = b[j,i];
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.vcorr[i,i_] = state.vcorr[i,i_] - t*state.vcorr[j,i_];
                    }
                }
                t = 1/b[i,i];
                for(i_=0; i_<=n-1;i_++)
                {
                    state.vcorr[i,i_] = t*state.vcorr[i,i_];
                }
            }
        }


        /*************************************************************************
        This function updates variable part (diagonal matrix D2)
        of low-rank preconditioner.

        This update is very cheap and takes just O(N) time.

        It has no effect with default preconditioner.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetprecvarpart(mincgstate state,
            double[] d2)
        {
            int i = 0;
            int n = 0;

            n = state.n;
            for(i=0; i<=n-1; i++)
            {
                state.diaghl2[i] = d2[i];
            }
        }


        /*************************************************************************

        This  subroutine  turns  on  verification  of  the  user-supplied analytic
        gradient:
        * user calls this subroutine before optimization begins
        * MinCGOptimize() is called
        * prior to  actual  optimization, for each component  of  parameters being
          optimized X[i] algorithm performs following steps:
          * two trial steps are made to X[i]-TestStep*S[i] and X[i]+TestStep*S[i],
            where X[i] is i-th component of the initial point and S[i] is a  scale
            of i-th parameter
          * F(X) is evaluated at these trial points
          * we perform one more evaluation in the middle point of the interval
          * we  build  cubic  model using function values and derivatives at trial
            points and we compare its prediction with actual value in  the  middle
            point
          * in case difference between prediction and actual value is higher  than
            some predetermined threshold, algorithm stops with completion code -7;
            Rep.VarIdx is set to index of the parameter with incorrect derivative.
        * after verification is over, algorithm proceeds to the actual optimization.

        NOTE 1: verification  needs  N (parameters count) gradient evaluations. It
                is very costly and you should use  it  only  for  low  dimensional
                problems,  when  you  want  to  be  sure  that  you've   correctly
                calculated  analytic  derivatives.  You  should  not use it in the
                production code (unless you want to check derivatives provided  by
                some third party).

        NOTE 2: you  should  carefully  choose  TestStep. Value which is too large
                (so large that function behaviour is significantly non-cubic) will
                lead to false alarms. You may use  different  step  for  different
                parameters by means of setting scale with MinCGSetScale().

        NOTE 3: this function may lead to false positives. In case it reports that
                I-th  derivative was calculated incorrectly, you may decrease test
                step  and  try  one  more  time  - maybe your function changes too
                sharply  and  your  step  is  too  large for such rapidly chanding
                function.

        INPUT PARAMETERS:
            State       -   structure used to store algorithm state
            TestStep    -   verification step:
                            * TestStep=0 turns verification off
                            * TestStep>0 activates verification

          -- ALGLIB --
             Copyright 31.05.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void mincgsetgradientcheck(mincgstate state,
            double teststep)
        {
            alglib.ap.assert(math.isfinite(teststep), "MinCGSetGradientCheck: TestStep contains NaN or Infinite");
            alglib.ap.assert((double)(teststep)>=(double)(0), "MinCGSetGradientCheck: invalid argument TestStep(TestStep<0)");
            state.teststep = teststep;
        }


        /*************************************************************************
        Clears request fileds (to be sure that we don't forgot to clear something)
        *************************************************************************/
        private static void clearrequestfields(mincgstate state)
        {
            state.needf = false;
            state.needfg = false;
            state.xupdated = false;
            state.lsstart = false;
            state.lsend = false;
            state.algpowerup = false;
        }


        /*************************************************************************
        This function calculates preconditioned product H^(-1)*x and stores result
        back into X. Work0[] and Work1[] are used as temporaries (size must be at
        least N; this function doesn't allocate arrays).

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        private static void preconditionedmultiply(mincgstate state,
            ref double[] x,
            ref double[] work0,
            ref double[] work1)
        {
            int i = 0;
            int n = 0;
            int vcnt = 0;
            double v = 0;
            int i_ = 0;

            n = state.n;
            vcnt = state.vcnt;
            if( state.prectype==0 )
            {
                return;
            }
            if( state.prectype==3 )
            {
                for(i=0; i<=n-1; i++)
                {
                    x[i] = x[i]*state.s[i]*state.s[i];
                }
                return;
            }
            alglib.ap.assert(state.prectype==2, "MinCG: internal error (unexpected PrecType)");
            
            //
            // handle part common for VCnt=0 and VCnt<>0
            //
            for(i=0; i<=n-1; i++)
            {
                x[i] = x[i]/(state.diagh[i]+state.diaghl2[i]);
            }
            
            //
            // if VCnt>0
            //
            if( vcnt>0 )
            {
                for(i=0; i<=vcnt-1; i++)
                {
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += state.vcorr[i,i_]*x[i_];
                    }
                    work0[i] = v;
                }
                for(i=0; i<=n-1; i++)
                {
                    work1[i] = 0;
                }
                for(i=0; i<=vcnt-1; i++)
                {
                    v = work0[i];
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.work1[i_] = state.work1[i_] + v*state.vcorr[i,i_];
                    }
                }
                for(i=0; i<=n-1; i++)
                {
                    x[i] = x[i]-state.work1[i]/(state.diagh[i]+state.diaghl2[i]);
                }
            }
        }


        /*************************************************************************
        This function calculates preconditioned product x'*H^(-1)*y. Work0[] and
        Work1[] are used as temporaries (size must be at least N; this function
        doesn't allocate arrays).

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        private static double preconditionedmultiply2(mincgstate state,
            ref double[] x,
            ref double[] y,
            ref double[] work0,
            ref double[] work1)
        {
            double result = 0;
            int i = 0;
            int n = 0;
            int vcnt = 0;
            double v0 = 0;
            double v1 = 0;
            int i_ = 0;

            n = state.n;
            vcnt = state.vcnt;
            
            //
            // no preconditioning
            //
            if( state.prectype==0 )
            {
                v0 = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v0 += x[i_]*y[i_];
                }
                result = v0;
                return result;
            }
            if( state.prectype==3 )
            {
                result = 0;
                for(i=0; i<=n-1; i++)
                {
                    result = result+x[i]*state.s[i]*state.s[i]*y[i];
                }
                return result;
            }
            alglib.ap.assert(state.prectype==2, "MinCG: internal error (unexpected PrecType)");
            
            //
            // low rank preconditioning
            //
            result = 0.0;
            for(i=0; i<=n-1; i++)
            {
                result = result+x[i]*y[i]/(state.diagh[i]+state.diaghl2[i]);
            }
            if( vcnt>0 )
            {
                for(i=0; i<=n-1; i++)
                {
                    work0[i] = x[i]/(state.diagh[i]+state.diaghl2[i]);
                    work1[i] = y[i]/(state.diagh[i]+state.diaghl2[i]);
                }
                for(i=0; i<=vcnt-1; i++)
                {
                    v0 = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v0 += work0[i_]*state.vcorr[i,i_];
                    }
                    v1 = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v1 += work1[i_]*state.vcorr[i,i_];
                    }
                    result = result-v0*v1;
                }
            }
            return result;
        }


        /*************************************************************************
        Internal initialization subroutine

          -- ALGLIB --
             Copyright 16.05.2011 by Bochkanov Sergey
        *************************************************************************/
        private static void mincginitinternal(int n,
            double diffstep,
            mincgstate state)
        {
            int i = 0;

            
            //
            // Initialize
            //
            state.teststep = 0;
            state.n = n;
            state.diffstep = diffstep;
            mincgsetcond(state, 0, 0, 0, 0);
            mincgsetxrep(state, false);
            mincgsetdrep(state, false);
            mincgsetstpmax(state, 0);
            mincgsetcgtype(state, -1);
            mincgsetprecdefault(state);
            state.xk = new double[n];
            state.dk = new double[n];
            state.xn = new double[n];
            state.dn = new double[n];
            state.x = new double[n];
            state.d = new double[n];
            state.g = new double[n];
            state.work0 = new double[n];
            state.work1 = new double[n];
            state.yk = new double[n];
            state.s = new double[n];
            for(i=0; i<=n-1; i++)
            {
                state.s[i] = 1.0;
            }
        }


    }
    public class minbleic
    {
        /*************************************************************************
        This object stores nonlinear optimizer state.
        You should use functions provided by MinBLEIC subpackage to work with this
        object
        *************************************************************************/
        public class minbleicstate
        {
            public int nmain;
            public int nslack;
            public double innerepsg;
            public double innerepsf;
            public double innerepsx;
            public double outerepsx;
            public double outerepsi;
            public int maxits;
            public bool xrep;
            public double stpmax;
            public double diffstep;
            public int prectype;
            public double[] diaghoriginal;
            public double[] diagh;
            public double[] x;
            public double f;
            public double[] g;
            public bool needf;
            public bool needfg;
            public bool xupdated;
            public double teststep;
            public rcommstate rstate;
            public int repinneriterationscount;
            public int repouteriterationscount;
            public int repnfev;
            public int repvaridx;
            public int repterminationtype;
            public double repdebugeqerr;
            public double repdebugfs;
            public double repdebugff;
            public double repdebugdx;
            public int repdebugfeasqpits;
            public int repdebugfeasgpaits;
            public double[] xcur;
            public double[] xprev;
            public double[] xstart;
            public int itsleft;
            public double[] xend;
            public double[] lastg;
            public double trimthreshold;
            public double[,] ceoriginal;
            public double[,] ceeffective;
            public double[,] cecurrent;
            public int[] ct;
            public int cecnt;
            public int cedim;
            public double[] xe;
            public bool[] hasbndl;
            public bool[] hasbndu;
            public double[] bndloriginal;
            public double[] bnduoriginal;
            public double[] bndleffective;
            public double[] bndueffective;
            public bool[] activeconstraints;
            public double[] constrainedvalues;
            public double[] transforms;
            public double[] seffective;
            public double[] soriginal;
            public double[] w;
            public double[] tmp0;
            public double[] tmp1;
            public double[] tmp2;
            public double[] r;
            public double[,] lmmatrix;
            public double errfeas;
            public double gnorm;
            public double mpgnorm;
            public double mba;
            public int variabletofreeze;
            public double valuetofreeze;
            public double fbase;
            public double fm2;
            public double fm1;
            public double fp1;
            public double fp2;
            public double xm1;
            public double xp1;
            public mincg.mincgstate cgstate;
            public mincg.mincgreport cgrep;
            public int optdim;
            public minbleicstate()
            {
                diaghoriginal = new double[0];
                diagh = new double[0];
                x = new double[0];
                g = new double[0];
                rstate = new rcommstate();
                xcur = new double[0];
                xprev = new double[0];
                xstart = new double[0];
                xend = new double[0];
                lastg = new double[0];
                ceoriginal = new double[0,0];
                ceeffective = new double[0,0];
                cecurrent = new double[0,0];
                ct = new int[0];
                xe = new double[0];
                hasbndl = new bool[0];
                hasbndu = new bool[0];
                bndloriginal = new double[0];
                bnduoriginal = new double[0];
                bndleffective = new double[0];
                bndueffective = new double[0];
                activeconstraints = new bool[0];
                constrainedvalues = new double[0];
                transforms = new double[0];
                seffective = new double[0];
                soriginal = new double[0];
                w = new double[0];
                tmp0 = new double[0];
                tmp1 = new double[0];
                tmp2 = new double[0];
                r = new double[0];
                lmmatrix = new double[0,0];
                cgstate = new mincg.mincgstate();
                cgrep = new mincg.mincgreport();
            }
        };


        /*************************************************************************
        This structure stores optimization report:
        * InnerIterationsCount      number of inner iterations
        * OuterIterationsCount      number of outer iterations
        * NFEV                      number of gradient evaluations
        * TerminationType           termination type (see below)

        TERMINATION CODES

        TerminationType field contains completion code, which can be:
          -10   unsupported combination of algorithm settings:
                1) StpMax is set to non-zero value,
                AND 2) non-default preconditioner is used.
                You can't use both features at the same moment,
                so you have to choose one of them (and to turn
                off another one).
          -7    gradient verification failed.
                See MinBLEICSetGradientCheck() for more information.
          -3    inconsistent constraints. Feasible point is
                either nonexistent or too hard to find. Try to
                restart optimizer with better initial
                approximation
           4    conditions on constraints are fulfilled
                with error less than or equal to EpsC
           5    MaxIts steps was taken
           7    stopping conditions are too stringent,
                further improvement is impossible,
                X contains best point found so far.

        ADDITIONAL FIELDS

        There are additional fields which can be used for debugging:
        * DebugEqErr                error in the equality constraints (2-norm)
        * DebugFS                   f, calculated at projection of initial point
                                    to the feasible set
        * DebugFF                   f, calculated at the final point
        * DebugDX                   |X_start-X_final|
        *************************************************************************/
        public class minbleicreport
        {
            public int inneriterationscount;
            public int outeriterationscount;
            public int nfev;
            public int varidx;
            public int terminationtype;
            public double debugeqerr;
            public double debugfs;
            public double debugff;
            public double debugdx;
            public int debugfeasqpits;
            public int debugfeasgpaits;
        };




        public const double svdtol = 100;
        public const double maxouterits = 20;
        public const double maxnonmonotoniclen = 1.0E5;


        /*************************************************************************
                             BOUND CONSTRAINED OPTIMIZATION
               WITH ADDITIONAL LINEAR EQUALITY AND INEQUALITY CONSTRAINTS

        DESCRIPTION:
        The  subroutine  minimizes  function   F(x)  of N arguments subject to any
        combination of:
        * bound constraints
        * linear inequality constraints
        * linear equality constraints

        REQUIREMENTS:
        * user must provide function value and gradient
        * starting point X0 must be feasible or
          not too far away from the feasible set
        * grad(f) must be Lipschitz continuous on a level set:
          L = { x : f(x)<=f(x0) }
        * function must be defined everywhere on the feasible set F

        USAGE:

        Constrained optimization if far more complex than the unconstrained one.
        Here we give very brief outline of the BLEIC optimizer. We strongly recommend
        you to read examples in the ALGLIB Reference Manual and to read ALGLIB User Guide
        on optimization, which is available at http://www.alglib.net/optimization/

        1. User initializes algorithm state with MinBLEICCreate() call

        2. USer adds boundary and/or linear constraints by calling
           MinBLEICSetBC() and MinBLEICSetLC() functions.

        3. User sets stopping conditions for underlying unconstrained solver
           with MinBLEICSetInnerCond() call.
           This function controls accuracy of underlying optimization algorithm.

        4. User sets stopping conditions for outer iteration by calling
           MinBLEICSetOuterCond() function.
           This function controls handling of boundary and inequality constraints.

        5. Additionally, user may set limit on number of internal iterations
           by MinBLEICSetMaxIts() call.
           This function allows to prevent algorithm from looping forever.

        6. User calls MinBLEICOptimize() function which takes algorithm  state and
           pointer (delegate, etc.) to callback function which calculates F/G.

        7. User calls MinBLEICResults() to get solution

        8. Optionally user may call MinBLEICRestartFrom() to solve another problem
           with same N but another starting point.
           MinBLEICRestartFrom() allows to reuse already initialized structure.


        INPUT PARAMETERS:
            N       -   problem dimension, N>0:
                        * if given, only leading N elements of X are used
                        * if not given, automatically determined from size ofX
            X       -   starting point, array[N]:
                        * it is better to set X to a feasible point
                        * but X can be infeasible, in which case algorithm will try
                          to find feasible point first, using X as initial
                          approximation.

        OUTPUT PARAMETERS:
            State   -   structure stores algorithm state

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleiccreate(int n,
            double[] x,
            minbleicstate state)
        {
            double[,] c = new double[0,0];
            int[] ct = new int[0];

            alglib.ap.assert(n>=1, "MinBLEICCreate: N<1");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinBLEICCreate: Length(X)<N");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinBLEICCreate: X contains infinite or NaN values!");
            minbleicinitinternal(n, x, 0.0, state);
        }


        /*************************************************************************
        The subroutine is finite difference variant of MinBLEICCreate().  It  uses
        finite differences in order to differentiate target function.

        Description below contains information which is specific to  this function
        only. We recommend to read comments on MinBLEICCreate() in  order  to  get
        more information about creation of BLEIC optimizer.

        INPUT PARAMETERS:
            N       -   problem dimension, N>0:
                        * if given, only leading N elements of X are used
                        * if not given, automatically determined from size of X
            X       -   starting point, array[0..N-1].
            DiffStep-   differentiation step, >0

        OUTPUT PARAMETERS:
            State   -   structure which stores algorithm state

        NOTES:
        1. algorithm uses 4-point central formula for differentiation.
        2. differentiation step along I-th axis is equal to DiffStep*S[I] where
           S[] is scaling vector which can be set by MinBLEICSetScale() call.
        3. we recommend you to use moderate values of  differentiation  step.  Too
           large step will result in too large truncation  errors, while too small
           step will result in too large numerical  errors.  1.0E-6  can  be  good
           value to start with.
        4. Numerical  differentiation  is   very   inefficient  -   one   gradient
           calculation needs 4*N function evaluations. This function will work for
           any N - either small (1...10), moderate (10...100) or  large  (100...).
           However, performance penalty will be too severe for any N's except  for
           small ones.
           We should also say that code which relies on numerical  differentiation
           is  less  robust and precise. CG needs exact gradient values. Imprecise
           gradient may slow  down  convergence, especially  on  highly  nonlinear
           problems.
           Thus  we  recommend to use this function for fast prototyping on small-
           dimensional problems only, and to implement analytical gradient as soon
           as possible.

          -- ALGLIB --
             Copyright 16.05.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleiccreatef(int n,
            double[] x,
            double diffstep,
            minbleicstate state)
        {
            double[,] c = new double[0,0];
            int[] ct = new int[0];

            alglib.ap.assert(n>=1, "MinBLEICCreateF: N<1");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinBLEICCreateF: Length(X)<N");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinBLEICCreateF: X contains infinite or NaN values!");
            alglib.ap.assert(math.isfinite(diffstep), "MinBLEICCreateF: DiffStep is infinite or NaN!");
            alglib.ap.assert((double)(diffstep)>(double)(0), "MinBLEICCreateF: DiffStep is non-positive!");
            minbleicinitinternal(n, x, diffstep, state);
        }


        /*************************************************************************
        This function sets boundary constraints for BLEIC optimizer.

        Boundary constraints are inactive by default (after initial creation).
        They are preserved after algorithm restart with MinBLEICRestartFrom().

        INPUT PARAMETERS:
            State   -   structure stores algorithm state
            BndL    -   lower bounds, array[N].
                        If some (all) variables are unbounded, you may specify
                        very small number or -INF.
            BndU    -   upper bounds, array[N].
                        If some (all) variables are unbounded, you may specify
                        very large number or +INF.

        NOTE 1: it is possible to specify BndL[i]=BndU[i]. In this case I-th
        variable will be "frozen" at X[i]=BndL[i]=BndU[i].

        NOTE 2: this solver has following useful properties:
        * bound constraints are always satisfied exactly
        * function is evaluated only INSIDE area specified by  bound  constraints,
          even  when  numerical  differentiation is used (algorithm adjusts  nodes
          according to boundary constraints)

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetbc(minbleicstate state,
            double[] bndl,
            double[] bndu)
        {
            int i = 0;
            int n = 0;

            n = state.nmain;
            alglib.ap.assert(alglib.ap.len(bndl)>=n, "MinBLEICSetBC: Length(BndL)<N");
            alglib.ap.assert(alglib.ap.len(bndu)>=n, "MinBLEICSetBC: Length(BndU)<N");
            for(i=0; i<=n-1; i++)
            {
                alglib.ap.assert(math.isfinite(bndl[i]) || Double.IsNegativeInfinity(bndl[i]), "MinBLEICSetBC: BndL contains NAN or +INF");
                alglib.ap.assert(math.isfinite(bndu[i]) || Double.IsPositiveInfinity(bndu[i]), "MinBLEICSetBC: BndL contains NAN or -INF");
                state.bndloriginal[i] = bndl[i];
                state.hasbndl[i] = math.isfinite(bndl[i]);
                state.bnduoriginal[i] = bndu[i];
                state.hasbndu[i] = math.isfinite(bndu[i]);
            }
        }


        /*************************************************************************
        This function sets linear constraints for BLEIC optimizer.

        Linear constraints are inactive by default (after initial creation).
        They are preserved after algorithm restart with MinBLEICRestartFrom().

        INPUT PARAMETERS:
            State   -   structure previously allocated with MinBLEICCreate call.
            C       -   linear constraints, array[K,N+1].
                        Each row of C represents one constraint, either equality
                        or inequality (see below):
                        * first N elements correspond to coefficients,
                        * last element corresponds to the right part.
                        All elements of C (including right part) must be finite.
            CT      -   type of constraints, array[K]:
                        * if CT[i]>0, then I-th constraint is C[i,*]*x >= C[i,n+1]
                        * if CT[i]=0, then I-th constraint is C[i,*]*x  = C[i,n+1]
                        * if CT[i]<0, then I-th constraint is C[i,*]*x <= C[i,n+1]
            K       -   number of equality/inequality constraints, K>=0:
                        * if given, only leading K elements of C/CT are used
                        * if not given, automatically determined from sizes of C/CT

        NOTE 1: linear (non-bound) constraints are satisfied only approximately:
        * there always exists some minor violation (about Epsilon in magnitude)
          due to rounding errors
        * numerical differentiation, if used, may  lead  to  function  evaluations
          outside  of the feasible  area,   because   algorithm  does  NOT  change
          numerical differentiation formula according to linear constraints.
        If you want constraints to be  satisfied  exactly, try to reformulate your
        problem  in  such  manner  that  all constraints will become boundary ones
        (this kind of constraints is always satisfied exactly, both in  the  final
        solution and in all intermediate points).

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetlc(minbleicstate state,
            double[,] c,
            int[] ct,
            int k)
        {
            int nmain = 0;
            int i = 0;
            int i_ = 0;

            nmain = state.nmain;
            
            //
            // First, check for errors in the inputs
            //
            alglib.ap.assert(k>=0, "MinBLEICSetLC: K<0");
            alglib.ap.assert(alglib.ap.cols(c)>=nmain+1 || k==0, "MinBLEICSetLC: Cols(C)<N+1");
            alglib.ap.assert(alglib.ap.rows(c)>=k, "MinBLEICSetLC: Rows(C)<K");
            alglib.ap.assert(alglib.ap.len(ct)>=k, "MinBLEICSetLC: Length(CT)<K");
            alglib.ap.assert(apserv.apservisfinitematrix(c, k, nmain+1), "MinBLEICSetLC: C contains infinite or NaN values!");
            
            //
            // Determine number of constraints,
            // allocate space and copy
            //
            state.cecnt = k;
            apserv.rmatrixsetlengthatleast(ref state.ceoriginal, state.cecnt, nmain+1);
            apserv.ivectorsetlengthatleast(ref state.ct, state.cecnt);
            for(i=0; i<=k-1; i++)
            {
                state.ct[i] = ct[i];
                for(i_=0; i_<=nmain;i_++)
                {
                    state.ceoriginal[i,i_] = c[i,i_];
                }
            }
        }


        /*************************************************************************
        This function sets stopping conditions for the underlying nonlinear CG
        optimizer. It controls overall accuracy of solution. These conditions
        should be strict enough in order for algorithm to converge.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            EpsG    -   >=0
                        The  subroutine  finishes  its  work   if   the  condition
                        |v|<EpsG is satisfied, where:
                        * |.| means Euclidian norm
                        * v - scaled gradient vector, v[i]=g[i]*s[i]
                        * g - gradient
                        * s - scaling coefficients set by MinBLEICSetScale()
            EpsF    -   >=0
                        The  subroutine  finishes  its work if on k+1-th iteration
                        the  condition  |F(k+1)-F(k)|<=EpsF*max{|F(k)|,|F(k+1)|,1}
                        is satisfied.
            EpsX    -   >=0
                        The subroutine finishes its work if  on  k+1-th  iteration
                        the condition |v|<=EpsX is fulfilled, where:
                        * |.| means Euclidian norm
                        * v - scaled step vector, v[i]=dx[i]/s[i]
                        * dx - ste pvector, dx=X(k+1)-X(k)
                        * s - scaling coefficients set by MinBLEICSetScale()

        Passing EpsG=0, EpsF=0 and EpsX=0 (simultaneously) will lead to
        automatic stopping criterion selection.

        These conditions are used to terminate inner iterations. However, you
        need to tune termination conditions for outer iterations too.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetinnercond(minbleicstate state,
            double epsg,
            double epsf,
            double epsx)
        {
            alglib.ap.assert(math.isfinite(epsg), "MinBLEICSetInnerCond: EpsG is not finite number");
            alglib.ap.assert((double)(epsg)>=(double)(0), "MinBLEICSetInnerCond: negative EpsG");
            alglib.ap.assert(math.isfinite(epsf), "MinBLEICSetInnerCond: EpsF is not finite number");
            alglib.ap.assert((double)(epsf)>=(double)(0), "MinBLEICSetInnerCond: negative EpsF");
            alglib.ap.assert(math.isfinite(epsx), "MinBLEICSetInnerCond: EpsX is not finite number");
            alglib.ap.assert((double)(epsx)>=(double)(0), "MinBLEICSetInnerCond: negative EpsX");
            state.innerepsg = epsg;
            state.innerepsf = epsf;
            state.innerepsx = epsx;
        }


        /*************************************************************************
        This function sets stopping conditions for outer iteration of BLEIC algo.

        These conditions control accuracy of constraint handling and amount of
        infeasibility allowed in the solution.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            EpsX    -   >0, stopping condition on outer iteration step length
            EpsI    -   >0, stopping condition on infeasibility
            
        Both EpsX and EpsI must be non-zero.

        MEANING OF EpsX

        EpsX  is  a  stopping  condition for outer iterations. Algorithm will stop
        when  solution  of  the  current  modified  subproblem will be within EpsX
        (using 2-norm) of the previous solution.

        MEANING OF EpsI

        EpsI controls feasibility properties -  algorithm  won't  stop  until  all
        inequality constraints will be satisfied with error (distance from current
        point to the feasible area) at most EpsI.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetoutercond(minbleicstate state,
            double epsx,
            double epsi)
        {
            alglib.ap.assert(math.isfinite(epsx), "MinBLEICSetOuterCond: EpsX is not finite number");
            alglib.ap.assert((double)(epsx)>(double)(0), "MinBLEICSetOuterCond: non-positive EpsX");
            alglib.ap.assert(math.isfinite(epsi), "MinBLEICSetOuterCond: EpsI is not finite number");
            alglib.ap.assert((double)(epsi)>(double)(0), "MinBLEICSetOuterCond: non-positive EpsI");
            state.outerepsx = epsx;
            state.outerepsi = epsi;
        }


        /*************************************************************************
        This function sets scaling coefficients for BLEIC optimizer.

        ALGLIB optimizers use scaling matrices to test stopping  conditions  (step
        size and gradient are scaled before comparison with tolerances).  Scale of
        the I-th variable is a translation invariant measure of:
        a) "how large" the variable is
        b) how large the step should be to make significant changes in the function

        Scaling is also used by finite difference variant of the optimizer  - step
        along I-th axis is equal to DiffStep*S[I].

        In  most  optimizers  (and  in  the  BLEIC  too)  scaling is NOT a form of
        preconditioning. It just  affects  stopping  conditions.  You  should  set
        preconditioner  by  separate  call  to  one  of  the  MinBLEICSetPrec...()
        functions.

        There is a special  preconditioning  mode, however,  which  uses   scaling
        coefficients to form diagonal preconditioning matrix. You  can  turn  this
        mode on, if you want.   But  you should understand that scaling is not the
        same thing as preconditioning - these are two different, although  related
        forms of tuning solver.

        INPUT PARAMETERS:
            State   -   structure stores algorithm state
            S       -   array[N], non-zero scaling coefficients
                        S[i] may be negative, sign doesn't matter.

          -- ALGLIB --
             Copyright 14.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetscale(minbleicstate state,
            double[] s)
        {
            int i = 0;

            alglib.ap.assert(alglib.ap.len(s)>=state.nmain, "MinBLEICSetScale: Length(S)<N");
            for(i=0; i<=state.nmain-1; i++)
            {
                alglib.ap.assert(math.isfinite(s[i]), "MinBLEICSetScale: S contains infinite or NAN elements");
                alglib.ap.assert((double)(s[i])!=(double)(0), "MinBLEICSetScale: S contains zero elements");
                state.soriginal[i] = Math.Abs(s[i]);
            }
        }


        /*************************************************************************
        Modification of the preconditioner: preconditioning is turned off.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetprecdefault(minbleicstate state)
        {
            state.prectype = 0;
        }


        /*************************************************************************
        Modification  of  the  preconditioner:  diagonal of approximate Hessian is
        used.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            D       -   diagonal of the approximate Hessian, array[0..N-1],
                        (if larger, only leading N elements are used).

        NOTE 1: D[i] should be positive. Exception will be thrown otherwise.

        NOTE 2: you should pass diagonal of approximate Hessian - NOT ITS INVERSE.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetprecdiag(minbleicstate state,
            double[] d)
        {
            int i = 0;

            alglib.ap.assert(alglib.ap.len(d)>=state.nmain, "MinBLEICSetPrecDiag: D is too short");
            for(i=0; i<=state.nmain-1; i++)
            {
                alglib.ap.assert(math.isfinite(d[i]), "MinBLEICSetPrecDiag: D contains infinite or NAN elements");
                alglib.ap.assert((double)(d[i])>(double)(0), "MinBLEICSetPrecDiag: D contains non-positive elements");
            }
            apserv.rvectorsetlengthatleast(ref state.diaghoriginal, state.nmain);
            state.prectype = 2;
            for(i=0; i<=state.nmain-1; i++)
            {
                state.diaghoriginal[i] = d[i];
            }
        }


        /*************************************************************************
        Modification of the preconditioner: scale-based diagonal preconditioning.

        This preconditioning mode can be useful when you  don't  have  approximate
        diagonal of Hessian, but you know that your  variables  are  badly  scaled
        (for  example,  one  variable is in [1,10], and another in [1000,100000]),
        and most part of the ill-conditioning comes from different scales of vars.

        In this case simple  scale-based  preconditioner,  with H[i] = 1/(s[i]^2),
        can greatly improve convergence.

        IMPRTANT: you should set scale of your variables  with  MinBLEICSetScale()
        call  (before  or after MinBLEICSetPrecScale() call). Without knowledge of
        the scale of your variables scale-based preconditioner will be  just  unit
        matrix.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetprecscale(minbleicstate state)
        {
            state.prectype = 3;
        }


        /*************************************************************************
        This function allows to stop algorithm after specified number of inner
        iterations.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            MaxIts  -   maximum number of inner iterations.
                        If MaxIts=0, the number of iterations is unlimited.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetmaxits(minbleicstate state,
            int maxits)
        {
            alglib.ap.assert(maxits>=0, "MinBLEICSetMaxIts: negative MaxIts!");
            state.maxits = maxits;
        }


        /*************************************************************************
        This function turns on/off reporting.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            NeedXRep-   whether iteration reports are needed or not

        If NeedXRep is True, algorithm will call rep() callback function if  it is
        provided to MinBLEICOptimize().

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetxrep(minbleicstate state,
            bool needxrep)
        {
            state.xrep = needxrep;
        }


        /*************************************************************************
        This function sets maximum step length

        IMPORTANT: this feature is hard to combine with preconditioning. You can't
        set upper limit on step length, when you solve optimization  problem  with
        linear (non-boundary) constraints AND preconditioner turned on.

        When  non-boundary  constraints  are  present,  you  have to either a) use
        preconditioner, or b) use upper limit on step length.  YOU CAN'T USE BOTH!
        In this case algorithm will terminate with appropriate error code.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            StpMax  -   maximum step length, >=0. Set StpMax to 0.0,  if you don't
                        want to limit step length.

        Use this subroutine when you optimize target function which contains exp()
        or  other  fast  growing  functions,  and optimization algorithm makes too
        large  steps  which  lead   to overflow. This function allows us to reject
        steps  that  are  too  large  (and  therefore  expose  us  to the possible
        overflow) without actually calculating function value at the x+stp*d.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetstpmax(minbleicstate state,
            double stpmax)
        {
            alglib.ap.assert(math.isfinite(stpmax), "MinBLEICSetStpMax: StpMax is not finite!");
            alglib.ap.assert((double)(stpmax)>=(double)(0), "MinBLEICSetStpMax: StpMax<0!");
            state.stpmax = stpmax;
        }


        /*************************************************************************
        NOTES:

        1. This function has two different implementations: one which  uses  exact
           (analytical) user-supplied gradient,  and one which uses function value
           only  and  numerically  differentiates  function  in  order  to  obtain
           gradient.

           Depending  on  the  specific  function  used to create optimizer object
           (either  MinBLEICCreate() for analytical gradient or  MinBLEICCreateF()
           for numerical differentiation) you should choose appropriate variant of
           MinBLEICOptimize() - one  which  accepts  function  AND gradient or one
           which accepts function ONLY.

           Be careful to choose variant of MinBLEICOptimize() which corresponds to
           your optimization scheme! Table below lists different  combinations  of
           callback (function/gradient) passed to MinBLEICOptimize()  and specific
           function used to create optimizer.


                             |         USER PASSED TO MinBLEICOptimize()
           CREATED WITH      |  function only   |  function and gradient
           ------------------------------------------------------------
           MinBLEICCreateF() |     work                FAIL
           MinBLEICCreate()  |     FAIL                work

           Here "FAIL" denotes inappropriate combinations  of  optimizer  creation
           function  and  MinBLEICOptimize()  version.   Attemps   to   use   such
           combination (for  example,  to  create optimizer with MinBLEICCreateF()
           and  to  pass  gradient  information  to  MinCGOptimize()) will lead to
           exception being thrown. Either  you  did  not pass gradient when it WAS
           needed or you passed gradient when it was NOT needed.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static bool minbleiciteration(minbleicstate state)
        {
            bool result = new bool();
            int nmain = 0;
            int nslack = 0;
            int m = 0;
            int i = 0;
            int j = 0;
            double v = 0;
            double vv = 0;
            bool b = new bool();
            int i_ = 0;

            
            //
            // Reverse communication preparations
            // I know it looks ugly, but it works the same way
            // anywhere from C++ to Python.
            //
            // This code initializes locals by:
            // * random values determined during code
            //   generation - on first subroutine call
            // * values from previous call - on subsequent calls
            //
            if( state.rstate.stage>=0 )
            {
                nmain = state.rstate.ia[0];
                nslack = state.rstate.ia[1];
                m = state.rstate.ia[2];
                i = state.rstate.ia[3];
                j = state.rstate.ia[4];
                b = state.rstate.ba[0];
                v = state.rstate.ra[0];
                vv = state.rstate.ra[1];
            }
            else
            {
                nmain = -983;
                nslack = -989;
                m = -834;
                i = 900;
                j = -287;
                b = false;
                v = 214;
                vv = -338;
            }
            if( state.rstate.stage==0 )
            {
                goto lbl_0;
            }
            if( state.rstate.stage==1 )
            {
                goto lbl_1;
            }
            if( state.rstate.stage==2 )
            {
                goto lbl_2;
            }
            if( state.rstate.stage==3 )
            {
                goto lbl_3;
            }
            if( state.rstate.stage==4 )
            {
                goto lbl_4;
            }
            if( state.rstate.stage==5 )
            {
                goto lbl_5;
            }
            if( state.rstate.stage==6 )
            {
                goto lbl_6;
            }
            if( state.rstate.stage==7 )
            {
                goto lbl_7;
            }
            if( state.rstate.stage==8 )
            {
                goto lbl_8;
            }
            if( state.rstate.stage==9 )
            {
                goto lbl_9;
            }
            if( state.rstate.stage==10 )
            {
                goto lbl_10;
            }
            if( state.rstate.stage==11 )
            {
                goto lbl_11;
            }
            if( state.rstate.stage==12 )
            {
                goto lbl_12;
            }
            if( state.rstate.stage==13 )
            {
                goto lbl_13;
            }
            if( state.rstate.stage==14 )
            {
                goto lbl_14;
            }
            if( state.rstate.stage==15 )
            {
                goto lbl_15;
            }
            
            //
            // Routine body
            //
            
            //
            // Prepare:
            // * calculate number of slack variables
            // * initialize locals
            // * initialize debug fields
            // * make quick check
            //
            nmain = state.nmain;
            nslack = 0;
            for(i=0; i<=state.cecnt-1; i++)
            {
                if( state.ct[i]!=0 )
                {
                    nslack = nslack+1;
                }
            }
            state.nslack = nslack;
            state.repterminationtype = 0;
            state.repinneriterationscount = 0;
            state.repouteriterationscount = 0;
            state.repnfev = 0;
            state.repvaridx = -1;
            state.repdebugeqerr = 0.0;
            state.repdebugfs = Double.NaN;
            state.repdebugff = Double.NaN;
            state.repdebugdx = Double.NaN;
            if( (double)(state.stpmax)!=(double)(0) && state.prectype!=0 )
            {
                state.repterminationtype = -10;
                result = false;
                return result;
            }
            
            //
            // allocate
            //
            apserv.rvectorsetlengthatleast(ref state.r, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.diagh, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.tmp0, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.tmp1, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.tmp2, nmain+nslack);
            apserv.rmatrixsetlengthatleast(ref state.cecurrent, state.cecnt, nmain+nslack+1);
            apserv.bvectorsetlengthatleast(ref state.activeconstraints, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.constrainedvalues, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.lastg, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.xe, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.xcur, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.xprev, nmain+nslack);
            apserv.rvectorsetlengthatleast(ref state.xend, nmain);
            
            //
            // Create/restart optimizer.
            //
            // State.OptDim is used to determine current state of optimizer.
            //
            if( state.optdim!=nmain+nslack )
            {
                for(i=0; i<=nmain+nslack-1; i++)
                {
                    state.tmp1[i] = 0.0;
                }
                mincg.mincgcreate(nmain+nslack, state.tmp1, state.cgstate);
                state.optdim = nmain+nslack;
            }
            
            //
            // Prepare transformation.
            //
            // MinBLEIC's handling of preconditioner matrix is somewhat unusual -
            // instead of incorporating it into algorithm and making implicit
            // scaling (as most optimizers do) BLEIC optimizer uses explicit
            // scaling - it solves problem in the scaled parameters space S,
            // making transition between scaled (S) and unscaled (X) variables
            // every time we ask for function value.
            //
            // Following fields are calculated here:
            // * TransformS         X[i] = TransformS[i]*S[i], array[NMain]
            // * SEffective         "effective" scale of the variables after
            //                      transformation, array[NMain+NSlack]
            //
            apserv.rvectorsetlengthatleast(ref state.transforms, nmain);
            for(i=0; i<=nmain-1; i++)
            {
                if( state.prectype==2 )
                {
                    state.transforms[i] = 1/Math.Sqrt(state.diaghoriginal[i]);
                    continue;
                }
                if( state.prectype==3 )
                {
                    state.transforms[i] = state.soriginal[i];
                    continue;
                }
                state.transforms[i] = 1;
            }
            apserv.rvectorsetlengthatleast(ref state.seffective, nmain+nslack);
            for(i=0; i<=nmain-1; i++)
            {
                state.seffective[i] = state.soriginal[i]/state.transforms[i];
            }
            for(i=0; i<=nslack-1; i++)
            {
                state.seffective[nmain+i] = 1;
            }
            mincg.mincgsetscale(state.cgstate, state.seffective);
            
            //
            // Pre-process constraints
            // * check consistency of bound constraints
            // * add slack vars, convert problem to the bound/equality
            //   constrained one
            //
            // We calculate here:
            // * BndLEffective - lower bounds after transformation of variables (see above)
            // * BndUEffective - upper bounds after transformation of variables (see above)
            // * CEEffective - matrix of equality constraints for transformed variables
            //
            for(i=0; i<=nmain-1; i++)
            {
                if( state.hasbndl[i] )
                {
                    state.bndleffective[i] = state.bndloriginal[i]/state.transforms[i];
                }
                if( state.hasbndu[i] )
                {
                    state.bndueffective[i] = state.bnduoriginal[i]/state.transforms[i];
                }
            }
            for(i=0; i<=nmain-1; i++)
            {
                if( state.hasbndl[i] && state.hasbndu[i] )
                {
                    if( (double)(state.bndleffective[i])>(double)(state.bndueffective[i]) )
                    {
                        state.repterminationtype = -3;
                        result = false;
                        return result;
                    }
                }
            }
            apserv.rmatrixsetlengthatleast(ref state.ceeffective, state.cecnt, nmain+nslack+1);
            m = 0;
            for(i=0; i<=state.cecnt-1; i++)
            {
                
                //
                // NOTE: when we add slack variable, we use V = max(abs(CE[i,...])) as
                // coefficient before it in order to make linear equations better
                // conditioned.
                //
                v = 0;
                for(j=0; j<=nmain-1; j++)
                {
                    state.ceeffective[i,j] = state.ceoriginal[i,j]*state.transforms[j];
                    v = Math.Max(v, Math.Abs(state.ceeffective[i,j]));
                }
                if( (double)(v)==(double)(0) )
                {
                    v = 1;
                }
                for(j=0; j<=nslack-1; j++)
                {
                    state.ceeffective[i,nmain+j] = 0.0;
                }
                state.ceeffective[i,nmain+nslack] = state.ceoriginal[i,nmain];
                if( state.ct[i]<0 )
                {
                    state.ceeffective[i,nmain+m] = v;
                    m = m+1;
                }
                if( state.ct[i]>0 )
                {
                    state.ceeffective[i,nmain+m] = -v;
                    m = m+1;
                }
            }
            
            //
            // Find feasible point.
            //
            // 0. Convert from unscaled values (as stored in XStart) to scaled
            //    ones
            // 1. calculate values of slack variables such that starting
            //    point satisfies inequality constraints (after conversion to
            //    equality ones) as much as possible.
            // 2. use PrepareConstraintMatrix() function, which forces X
            //    to be strictly feasible.
            //
            for(i=0; i<=nmain-1; i++)
            {
                state.tmp0[i] = state.xstart[i]/state.transforms[i];
            }
            m = 0;
            for(i=0; i<=state.cecnt-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=nmain-1;i_++)
                {
                    v += state.ceeffective[i,i_]*state.tmp0[i_];
                }
                if( state.ct[i]<0 )
                {
                    state.tmp0[nmain+m] = state.ceeffective[i,nmain+nslack]-v;
                    m = m+1;
                }
                if( state.ct[i]>0 )
                {
                    state.tmp0[nmain+m] = v-state.ceeffective[i,nmain+nslack];
                    m = m+1;
                }
            }
            for(i=0; i<=nmain+nslack-1; i++)
            {
                state.tmp1[i] = 0;
            }
            for(i=0; i<=nmain+nslack-1; i++)
            {
                state.activeconstraints[i] = false;
            }
            b = optserv.findfeasiblepoint(ref state.tmp0, state.bndleffective, state.hasbndl, state.bndueffective, state.hasbndu, nmain, nslack, state.ceeffective, state.cecnt, state.outerepsi, ref state.repdebugfeasqpits, ref state.repdebugfeasgpaits);
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                state.xcur[i_] = state.tmp0[i_];
            }
            state.repdebugeqerr = 0.0;
            for(i=0; i<=state.cecnt-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    v += state.ceeffective[i,i_]*state.xcur[i_];
                }
                state.repdebugeqerr = state.repdebugeqerr+math.sqr(v-state.ceeffective[i,nmain+nslack]);
            }
            state.repdebugeqerr = Math.Sqrt(state.repdebugeqerr);
            if( !b )
            {
                state.repterminationtype = -3;
                result = false;
                return result;
            }
            
            //
            //  Check, that transferred derivative value is right
            //
            unscalepoint(state, state.xcur, ref state.x);
            clearrequestfields(state);
            if( !((double)(state.diffstep)==(double)(0) && (double)(state.teststep)>(double)(0)) )
            {
                goto lbl_16;
            }
            state.needfg = true;
            i = 0;
        lbl_18:
            if( i>nmain-1 )
            {
                goto lbl_20;
            }
            alglib.ap.assert((state.hasbndl[i] && (double)(state.bndloriginal[i])<=(double)(state.x[i])) || !state.hasbndl[i], "MinBLEICIteration: internal error(State.X is out of bounds)");
            alglib.ap.assert((state.hasbndu[i] && (double)(state.x[i])<=(double)(state.bnduoriginal[i])) || !state.hasbndu[i], "MinBLEICIteration: internal error(State.X is out of bounds)");
            v = state.x[i];
            state.x[i] = v-state.teststep*state.soriginal[i];
            if( state.hasbndl[i] )
            {
                state.x[i] = Math.Max(state.x[i], state.bndloriginal[i]);
            }
            state.xm1 = state.x[i];
            state.rstate.stage = 0;
            goto lbl_rcomm;
        lbl_0:
            state.fm1 = state.f;
            state.fp1 = state.g[i];
            state.x[i] = v+state.teststep*state.soriginal[i];
            if( state.hasbndu[i] )
            {
                state.x[i] = Math.Min(state.x[i], state.bnduoriginal[i]);
            }
            state.xp1 = state.x[i];
            state.rstate.stage = 1;
            goto lbl_rcomm;
        lbl_1:
            state.fm2 = state.f;
            state.fp2 = state.g[i];
            state.x[i] = (state.xm1+state.xp1)/2;
            if( state.hasbndl[i] )
            {
                state.x[i] = Math.Max(state.x[i], state.bndloriginal[i]);
            }
            if( state.hasbndu[i] )
            {
                state.x[i] = Math.Min(state.x[i], state.bnduoriginal[i]);
            }
            state.rstate.stage = 2;
            goto lbl_rcomm;
        lbl_2:
            state.x[i] = v;
            if( !optserv.derivativecheck(state.fm1, state.fp1, state.fm2, state.fp2, state.f, state.g[i], state.xp1-state.xm1) )
            {
                state.repvaridx = i;
                state.repterminationtype = -7;
                result = false;
                return result;
            }
            i = i+1;
            goto lbl_18;
        lbl_20:
            state.needfg = false;
        lbl_16:
            
            //
            // Initialize RepDebugFS with function value at initial point
            //
            unscalepoint(state, state.xcur, ref state.x);
            clearrequestfields(state);
            if( (double)(state.diffstep)!=(double)(0) )
            {
                goto lbl_21;
            }
            state.needfg = true;
            state.rstate.stage = 3;
            goto lbl_rcomm;
        lbl_3:
            state.needfg = false;
            goto lbl_22;
        lbl_21:
            state.needf = true;
            state.rstate.stage = 4;
            goto lbl_rcomm;
        lbl_4:
            state.needf = false;
        lbl_22:
            optserv.trimprepare(state.f, ref state.trimthreshold);
            state.repnfev = state.repnfev+1;
            state.repdebugfs = state.f;
            
            //
            // Outer cycle
            //
            state.itsleft = state.maxits;
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                state.xprev[i_] = state.xcur[i_];
            }
        lbl_23:
            if( false )
            {
                goto lbl_24;
            }
            alglib.ap.assert(state.prectype==0 || (double)(state.stpmax)==(double)(0), "MinBLEIC: internal error (-10)");
            
            //
            // Inner cycle: CG with projections and penalty functions
            //
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                state.tmp0[i_] = state.xcur[i_];
            }
            for(i=0; i<=nmain+nslack-1; i++)
            {
                state.tmp1[i] = 0;
                state.activeconstraints[i] = false;
            }
            if( !prepareconstraintmatrix(state, state.tmp0, state.tmp1, ref state.xcur, ref state.tmp2) )
            {
                state.repterminationtype = -3;
                result = false;
                return result;
            }
            for(i=0; i<=nmain+nslack-1; i++)
            {
                state.activeconstraints[i] = false;
            }
            rebuildcexe(state);
            mincg.mincgrestartfrom(state.cgstate, state.xcur);
            mincg.mincgsetcond(state.cgstate, state.innerepsg, state.innerepsf, state.innerepsx, state.itsleft);
            mincg.mincgsetxrep(state.cgstate, state.xrep);
            mincg.mincgsetdrep(state.cgstate, true);
            mincg.mincgsetstpmax(state.cgstate, state.stpmax);
        lbl_25:
            if( !mincg.mincgiteration(state.cgstate) )
            {
                goto lbl_26;
            }
            
            //
            // process different requests/reports of inner optimizer
            //
            if( state.cgstate.algpowerup )
            {
                for(i=0; i<=nmain+nslack-1; i++)
                {
                    state.activeconstraints[i] = false;
                }
                do
                {
                    rebuildcexe(state);
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        state.tmp1[i_] = state.cgstate.g[i_];
                    }
                    makegradientprojection(state, ref state.tmp1);
                    b = false;
                    for(i=0; i<=nmain-1; i++)
                    {
                        if( !state.activeconstraints[i] )
                        {
                            if( state.hasbndl[i] )
                            {
                                if( (double)(state.cgstate.x[i])==(double)(state.bndleffective[i]) && (double)(state.tmp1[i])>=(double)(0) )
                                {
                                    state.activeconstraints[i] = true;
                                    state.constrainedvalues[i] = state.bndleffective[i];
                                    b = true;
                                }
                            }
                            if( state.hasbndu[i] )
                            {
                                if( (double)(state.cgstate.x[i])==(double)(state.bndueffective[i]) && (double)(state.tmp1[i])<=(double)(0) )
                                {
                                    state.activeconstraints[i] = true;
                                    state.constrainedvalues[i] = state.bndueffective[i];
                                    b = true;
                                }
                            }
                        }
                    }
                    for(i=0; i<=nslack-1; i++)
                    {
                        if( !state.activeconstraints[nmain+i] )
                        {
                            if( (double)(state.cgstate.x[nmain+i])==(double)(0) && (double)(state.tmp1[nmain+i])>=(double)(0) )
                            {
                                state.activeconstraints[nmain+i] = true;
                                state.constrainedvalues[nmain+i] = 0;
                                b = true;
                            }
                        }
                    }
                }
                while( b );
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    state.cgstate.g[i_] = state.tmp1[i_];
                }
                goto lbl_25;
            }
            if( state.cgstate.lsstart )
            {
                
                //
                // Beginning of the line search: set upper limit on step size
                // to prevent algo from leaving feasible area.
                //
                if( (double)(state.cgstate.curstpmax)==(double)(0) )
                {
                    state.cgstate.curstpmax = 1.0E50;
                }
                state.variabletofreeze = -1;
                for(i=0; i<=nmain-1; i++)
                {
                    if( state.hasbndl[i] && (double)(state.cgstate.d[i])<(double)(0) )
                    {
                        v = state.cgstate.curstpmax;
                        vv = state.cgstate.x[i]-state.bndleffective[i];
                        if( (double)(vv)<(double)(0) )
                        {
                            vv = 0;
                        }
                        state.cgstate.curstpmax = apserv.safeminposrv(vv, -state.cgstate.d[i], state.cgstate.curstpmax);
                        if( (double)(state.cgstate.curstpmax)<(double)(v) )
                        {
                            state.variabletofreeze = i;
                            state.valuetofreeze = state.bndleffective[i];
                        }
                    }
                    if( state.hasbndu[i] && (double)(state.cgstate.d[i])>(double)(0) )
                    {
                        v = state.cgstate.curstpmax;
                        vv = state.bndueffective[i]-state.cgstate.x[i];
                        if( (double)(vv)<(double)(0) )
                        {
                            vv = 0;
                        }
                        state.cgstate.curstpmax = apserv.safeminposrv(vv, state.cgstate.d[i], state.cgstate.curstpmax);
                        if( (double)(state.cgstate.curstpmax)<(double)(v) )
                        {
                            state.variabletofreeze = i;
                            state.valuetofreeze = state.bndueffective[i];
                        }
                    }
                }
                for(i=0; i<=nslack-1; i++)
                {
                    if( (double)(state.cgstate.d[nmain+i])<(double)(0) )
                    {
                        v = state.cgstate.curstpmax;
                        vv = state.cgstate.x[nmain+i];
                        if( (double)(vv)<(double)(0) )
                        {
                            vv = 0;
                        }
                        state.cgstate.curstpmax = apserv.safeminposrv(vv, -state.cgstate.d[nmain+i], state.cgstate.curstpmax);
                        if( (double)(state.cgstate.curstpmax)<(double)(v) )
                        {
                            state.variabletofreeze = nmain+i;
                            state.valuetofreeze = 0;
                        }
                    }
                }
                if( (double)(state.cgstate.curstpmax)==(double)(0) )
                {
                    state.activeconstraints[state.variabletofreeze] = true;
                    state.constrainedvalues[state.variabletofreeze] = state.valuetofreeze;
                    state.cgstate.x[state.variabletofreeze] = state.valuetofreeze;
                    state.cgstate.terminationneeded = true;
                }
                goto lbl_25;
            }
            if( state.cgstate.lsend )
            {
                
                //
                // Line search just finished.
                // Maybe we should activate some constraints?
                //
                if( state.variabletofreeze>=0 )
                {
                    
                    //
                    // We have candidate constraint for activation, which can be
                    // activated in two cases:
                    //
                    // 1. monotonic step with Stp=StpMax is performed (MCINFO=5),
                    //    we just have to post-process this step to ensure that
                    //    constraint is activated
                    //
                    // 2. MCINFO<>5 and MCINFO<>1, and BOTH of the following holds:
                    //    2.a) ScaledNorm(State.CGState.G)>InnerEpsG (strict inequality to handle EpsG=0)
                    //    2.b) ScaledNorm(StpMax*State.CGState.D)<=MaxNonMonotonicLen*MachineEpsilon;
                    //
                    //    What do these conditions mean?
                    //    a) Having MCINFO which is not 5 or 1 means that it is
                    //       possible that our function is actually increasing in
                    //       the direction of D instead of decreasing.
                    //    b) clause (2.a) means that gradient value is large enough
                    //       to consider it non-zero (EpsG=0 means that any non-zero
                    //       gradient value will be large enough).
                    //    c) clause (2.b) means that step length is very small and
                    //       function non-monotonicity can be just a consequence of
                    //       numerical noise.
                    //
                    //    Given all of the listed above, it may be worth trying to
                    //    perform step which increases function value.
                    //
                    // NOTE: some non-monotonic algorithms can be caught in an eternal
                    //       loop. However, this algorithm is guaranteed from having
                    //       such eternal loop because non-monotonic step always activates
                    //       at least one constraint, and in order to have eternal loop
                    //       we need to deactivate this constraint, which can be done
                    //       only at the beginning of the outer iteration. And we have
                    //       upper limit on the number of outer iterations, so we can
                    //       be sure that algorithm will stop eventually.
                    //
                    if( state.cgstate.mcinfo!=5 && state.cgstate.mcinfo!=1 )
                    {
                        v = 0;
                        vv = 0;
                        for(i=0; i<=nmain+nslack-1; i++)
                        {
                            v = v+math.sqr(state.cgstate.g[i]*state.seffective[i]);
                            vv = vv+math.sqr(state.cgstate.curstpmax*state.cgstate.d[i]/state.seffective[i]);
                        }
                        v = Math.Sqrt(v);
                        vv = Math.Sqrt(vv);
                        b = (double)(v)>(double)(state.innerepsg) && (double)(vv)<=(double)(maxnonmonotoniclen*math.machineepsilon);
                    }
                    else
                    {
                        b = state.cgstate.mcinfo==5;
                    }
                    if( b )
                    {
                        state.activeconstraints[state.variabletofreeze] = true;
                        state.constrainedvalues[state.variabletofreeze] = state.valuetofreeze;
                    }
                }
                else
                {
                    b = false;
                }
                
                //
                // Additional activation of constraints
                //
                b = b || additionalcheckforconstraints(state, state.cgstate.x);
                
                //
                // If at least one constraint was activated we have to rebuild constraint matrices
                //
                if( b )
                {
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        state.tmp0[i_] = state.cgstate.x[i_];
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        state.tmp1[i_] = state.lastg[i_];
                    }
                    if( !prepareconstraintmatrix(state, state.tmp0, state.tmp1, ref state.cgstate.x, ref state.cgstate.g) )
                    {
                        state.repterminationtype = -3;
                        result = false;
                        return result;
                    }
                    state.cgstate.innerresetneeded = true;
                }
                goto lbl_25;
            }
            if( !state.cgstate.needfg )
            {
                goto lbl_27;
            }
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                state.tmp1[i_] = state.cgstate.x[i_];
            }
            projectpointandunscale(state, ref state.tmp1, ref state.x, ref state.r, ref vv);
            clearrequestfields(state);
            if( (double)(state.diffstep)!=(double)(0) )
            {
                goto lbl_29;
            }
            state.needfg = true;
            state.rstate.stage = 5;
            goto lbl_rcomm;
        lbl_5:
            state.needfg = false;
            goto lbl_30;
        lbl_29:
            state.needf = true;
            state.rstate.stage = 6;
            goto lbl_rcomm;
        lbl_6:
            state.fbase = state.f;
            i = 0;
        lbl_31:
            if( i>nmain-1 )
            {
                goto lbl_33;
            }
            v = state.x[i];
            b = false;
            if( state.hasbndl[i] )
            {
                b = b || (double)(v-state.diffstep*state.soriginal[i])<(double)(state.bndloriginal[i]);
            }
            if( state.hasbndu[i] )
            {
                b = b || (double)(v+state.diffstep*state.soriginal[i])>(double)(state.bnduoriginal[i]);
            }
            if( b )
            {
                goto lbl_34;
            }
            state.x[i] = v-state.diffstep*state.soriginal[i];
            state.rstate.stage = 7;
            goto lbl_rcomm;
        lbl_7:
            state.fm2 = state.f;
            state.x[i] = v-0.5*state.diffstep*state.soriginal[i];
            state.rstate.stage = 8;
            goto lbl_rcomm;
        lbl_8:
            state.fm1 = state.f;
            state.x[i] = v+0.5*state.diffstep*state.soriginal[i];
            state.rstate.stage = 9;
            goto lbl_rcomm;
        lbl_9:
            state.fp1 = state.f;
            state.x[i] = v+state.diffstep*state.soriginal[i];
            state.rstate.stage = 10;
            goto lbl_rcomm;
        lbl_10:
            state.fp2 = state.f;
            state.g[i] = (8*(state.fp1-state.fm1)-(state.fp2-state.fm2))/(6*state.diffstep*state.soriginal[i]);
            goto lbl_35;
        lbl_34:
            state.xm1 = Math.Max(v-state.diffstep*state.soriginal[i], state.bndloriginal[i]);
            state.x[i] = state.xm1;
            state.rstate.stage = 11;
            goto lbl_rcomm;
        lbl_11:
            state.fm1 = state.f;
            state.xp1 = Math.Min(v+state.diffstep*state.soriginal[i], state.bnduoriginal[i]);
            state.x[i] = state.xp1;
            state.rstate.stage = 12;
            goto lbl_rcomm;
        lbl_12:
            state.fp1 = state.f;
            state.g[i] = (state.fp1-state.fm1)/(state.xp1-state.xm1);
        lbl_35:
            state.x[i] = v;
            i = i+1;
            goto lbl_31;
        lbl_33:
            state.f = state.fbase;
            state.needf = false;
        lbl_30:
            if( (double)(state.f)<(double)(state.trimthreshold) )
            {
                
                //
                // normal processing
                //
                state.cgstate.f = state.f;
                scalegradientandexpand(state, state.g, ref state.cgstate.g);
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    state.lastg[i_] = state.cgstate.g[i_];
                }
                modifytargetfunction(state, state.tmp1, state.r, vv, ref state.cgstate.f, ref state.cgstate.g, ref state.gnorm, ref state.mpgnorm);
            }
            else
            {
                
                //
                // function value is too high, trim it
                //
                state.cgstate.f = state.trimthreshold;
                for(i=0; i<=nmain+nslack-1; i++)
                {
                    state.cgstate.g[i] = 0.0;
                }
            }
            goto lbl_25;
        lbl_27:
            if( !state.cgstate.xupdated )
            {
                goto lbl_36;
            }
            
            //
            // Report
            //
            unscalepoint(state, state.cgstate.x, ref state.x);
            state.f = state.cgstate.f;
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 13;
            goto lbl_rcomm;
        lbl_13:
            state.xupdated = false;
            goto lbl_25;
        lbl_36:
            goto lbl_25;
        lbl_26:
            mincg.mincgresults(state.cgstate, ref state.xcur, state.cgrep);
            unscalepoint(state, state.xcur, ref state.xend);
            state.repinneriterationscount = state.repinneriterationscount+state.cgrep.iterationscount;
            state.repouteriterationscount = state.repouteriterationscount+1;
            state.repnfev = state.repnfev+state.cgrep.nfev;
            
            //
            // Update RepDebugFF with function value at current point
            //
            unscalepoint(state, state.xcur, ref state.x);
            clearrequestfields(state);
            if( (double)(state.diffstep)!=(double)(0) )
            {
                goto lbl_38;
            }
            state.needfg = true;
            state.rstate.stage = 14;
            goto lbl_rcomm;
        lbl_14:
            state.needfg = false;
            goto lbl_39;
        lbl_38:
            state.needf = true;
            state.rstate.stage = 15;
            goto lbl_rcomm;
        lbl_15:
            state.needf = false;
        lbl_39:
            state.repnfev = state.repnfev+1;
            state.repdebugff = state.f;
            
            //
            // Check for stopping:
            // * "normal", outer step size is small enough, infeasibility is within bounds
            // * "inconsistent",  if Lagrange multipliers increased beyond threshold given by MaxLagrangeMul
            // * "too stringent", in other cases
            //
            v = 0;
            for(i=0; i<=nmain-1; i++)
            {
                v = v+math.sqr((state.xcur[i]-state.xprev[i])/state.seffective[i]);
            }
            v = Math.Sqrt(v);
            if( (double)(v)<=(double)(state.outerepsx) )
            {
                state.repterminationtype = 4;
                goto lbl_24;
            }
            if( state.maxits>0 )
            {
                state.itsleft = state.itsleft-state.cgrep.iterationscount;
                if( state.itsleft<=0 )
                {
                    state.repterminationtype = 5;
                    goto lbl_24;
                }
            }
            if( (double)(state.repouteriterationscount)>=(double)(maxouterits) )
            {
                
                //
                // Stopped because of reaching limit on the number of outer iterations.
                //
                // IMPORTANT: DO NOT REMOVE THIS STOPPING CRITERIA because it
                //            saves algorithm from eternal loop with non-monotonic
                //            steps being made (first step increases function value,
                //            next step moves us back). See comments on the code which
                //            handles State.CGState.LSEnd for more info on this subject.
                //
                state.repterminationtype = 5;
                goto lbl_24;
            }
            
            //
            // Next iteration
            //
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                state.xprev[i_] = state.xcur[i_];
            }
            goto lbl_23;
        lbl_24:
            
            //
            // We've stopped, fill debug information
            //
            state.repdebugeqerr = 0.0;
            for(i=0; i<=state.cecnt-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    v += state.ceeffective[i,i_]*state.xcur[i_];
                }
                state.repdebugeqerr = state.repdebugeqerr+math.sqr(v-state.ceeffective[i,nmain+nslack]);
            }
            state.repdebugeqerr = Math.Sqrt(state.repdebugeqerr);
            state.repdebugdx = 0;
            for(i=0; i<=nmain-1; i++)
            {
                state.repdebugdx = state.repdebugdx+math.sqr(state.xcur[i]-state.xstart[i]);
            }
            state.repdebugdx = Math.Sqrt(state.repdebugdx);
            result = false;
            return result;
            
            //
            // Saving state
            //
        lbl_rcomm:
            result = true;
            state.rstate.ia[0] = nmain;
            state.rstate.ia[1] = nslack;
            state.rstate.ia[2] = m;
            state.rstate.ia[3] = i;
            state.rstate.ia[4] = j;
            state.rstate.ba[0] = b;
            state.rstate.ra[0] = v;
            state.rstate.ra[1] = vv;
            return result;
        }


        /*************************************************************************
        BLEIC results

        INPUT PARAMETERS:
            State   -   algorithm state

        OUTPUT PARAMETERS:
            X       -   array[0..N-1], solution
            Rep     -   optimization report. You should check Rep.TerminationType
                        in  order  to  distinguish  successful  termination  from
                        unsuccessful one.
                        More information about fields of this  structure  can  be
                        found in the comments on MinBLEICReport datatype.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicresults(minbleicstate state,
            ref double[] x,
            minbleicreport rep)
        {
            x = new double[0];

            minbleicresultsbuf(state, ref x, rep);
        }


        /*************************************************************************
        BLEIC results

        Buffered implementation of MinBLEICResults() which uses pre-allocated buffer
        to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
        intended to be used in the inner cycles of performance critical algorithms
        where array reallocation penalty is too large to be ignored.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicresultsbuf(minbleicstate state,
            ref double[] x,
            minbleicreport rep)
        {
            int i = 0;
            int i_ = 0;

            if( alglib.ap.len(x)<state.nmain )
            {
                x = new double[state.nmain];
            }
            rep.inneriterationscount = state.repinneriterationscount;
            rep.outeriterationscount = state.repouteriterationscount;
            rep.nfev = state.repnfev;
            rep.varidx = state.repvaridx;
            rep.terminationtype = state.repterminationtype;
            if( state.repterminationtype>0 )
            {
                for(i_=0; i_<=state.nmain-1;i_++)
                {
                    x[i_] = state.xend[i_];
                }
            }
            else
            {
                for(i=0; i<=state.nmain-1; i++)
                {
                    x[i] = Double.NaN;
                }
            }
            rep.debugeqerr = state.repdebugeqerr;
            rep.debugfs = state.repdebugfs;
            rep.debugff = state.repdebugff;
            rep.debugdx = state.repdebugdx;
            rep.debugfeasqpits = state.repdebugfeasqpits;
            rep.debugfeasgpaits = state.repdebugfeasgpaits;
        }


        /*************************************************************************
        This subroutine restarts algorithm from new point.
        All optimization parameters (including constraints) are left unchanged.

        This  function  allows  to  solve multiple  optimization  problems  (which
        must have  same number of dimensions) without object reallocation penalty.

        INPUT PARAMETERS:
            State   -   structure previously allocated with MinBLEICCreate call.
            X       -   new starting point.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicrestartfrom(minbleicstate state,
            double[] x)
        {
            int n = 0;
            int i_ = 0;

            n = state.nmain;
            
            //
            // First, check for errors in the inputs
            //
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinBLEICRestartFrom: Length(X)<N");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinBLEICRestartFrom: X contains infinite or NaN values!");
            
            //
            // Set XC
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.xstart[i_] = x[i_];
            }
            
            //
            // prepare RComm facilities
            //
            state.rstate.ia = new int[4+1];
            state.rstate.ba = new bool[0+1];
            state.rstate.ra = new double[1+1];
            state.rstate.stage = -1;
            clearrequestfields(state);
        }


        /*************************************************************************
        This  subroutine  turns  on  verification  of  the  user-supplied analytic
        gradient:
        * user calls this subroutine before optimization begins
        * MinBLEICOptimize() is called
        * prior to  actual  optimization, for each component  of  parameters being
          optimized X[i] algorithm performs following steps:
          * two trial steps are made to X[i]-TestStep*S[i] and X[i]+TestStep*S[i],
            where X[i] is i-th component of the initial point and S[i] is a  scale
            of i-th parameter
          * if needed, steps are bounded with respect to constraints on X[]
          * F(X) is evaluated at these trial points
          * we perform one more evaluation in the middle point of the interval
          * we  build  cubic  model using function values and derivatives at trial
            points and we compare its prediction with actual value in  the  middle
            point
          * in case difference between prediction and actual value is higher  than
            some predetermined threshold, algorithm stops with completion code -7;
            Rep.VarIdx is set to index of the parameter with incorrect derivative.
        * after verification is over, algorithm proceeds to the actual optimization.

        NOTE 1: verification  needs  N (parameters count) gradient evaluations. It
                is very costly and you should use  it  only  for  low  dimensional
                problems,  when  you  want  to  be  sure  that  you've   correctly
                calculated  analytic  derivatives.  You  should  not use it in the
                production code (unless you want to check derivatives provided  by
                some third party).

        NOTE 2: you  should  carefully  choose  TestStep. Value which is too large
                (so large that function behaviour is significantly non-cubic) will
                lead to false alarms. You may use  different  step  for  different
                parameters by means of setting scale with MinBLEICSetScale().

        NOTE 3: this function may lead to false positives. In case it reports that
                I-th  derivative was calculated incorrectly, you may decrease test
                step  and  try  one  more  time  - maybe your function changes too
                sharply  and  your  step  is  too  large for such rapidly chanding
                function.

        INPUT PARAMETERS:
            State       -   structure used to store algorithm state
            TestStep    -   verification step:
                            * TestStep=0 turns verification off
                            * TestStep>0 activates verification

          -- ALGLIB --
             Copyright 15.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetgradientcheck(minbleicstate state,
            double teststep)
        {
            alglib.ap.assert(math.isfinite(teststep), "MinBLEICSetGradientCheck: TestStep contains NaN or Infinite");
            alglib.ap.assert((double)(teststep)>=(double)(0), "MinBLEICSetGradientCheck: invalid argument TestStep(TestStep<0)");
            state.teststep = teststep;
        }


        /*************************************************************************
        Clears request fileds (to be sure that we don't forget to clear something)
        *************************************************************************/
        private static void clearrequestfields(minbleicstate state)
        {
            state.needf = false;
            state.needfg = false;
            state.xupdated = false;
        }


        /*************************************************************************
        This functions "unscales" point, i.e. it makes transformation  from scaled
        variables to unscaled ones. Only leading NMain variables are copied from
        XUnscaled to XScaled.
        *************************************************************************/
        private static void unscalepoint(minbleicstate state,
            double[] xscaled,
            ref double[] xunscaled)
        {
            int i = 0;
            double v = 0;

            for(i=0; i<=state.nmain-1; i++)
            {
                v = xscaled[i]*state.transforms[i];
                if( state.hasbndl[i] )
                {
                    if( (double)(v)<(double)(state.bndloriginal[i]) )
                    {
                        v = state.bndloriginal[i];
                    }
                }
                if( state.hasbndu[i] )
                {
                    if( (double)(v)>(double)(state.bnduoriginal[i]) )
                    {
                        v = state.bnduoriginal[i];
                    }
                }
                xunscaled[i] = v;
            }
        }


        /*************************************************************************
        This function:
        1. makes projection of XScaled into equality constrained subspace
           (X is modified in-place)
        2. stores residual from the projection into R
        3. unscales projected XScaled and stores result into XUnscaled with
           additional enforcement
        It calculates set of additional values which are used later for
        modification of the target function F.

        INPUT PARAMETERS:
            State   -   optimizer state (we use its fields to get information
                        about constraints)
            X       -   vector being projected
            R       -   preallocated buffer, used to store residual from projection

        OUTPUT PARAMETERS:
            X       -   projection of input X
            R       -   residual
            RNorm   -   residual norm squared, used later to modify target function
        *************************************************************************/
        private static void projectpointandunscale(minbleicstate state,
            ref double[] xscaled,
            ref double[] xunscaled,
            ref double[] rscaled,
            ref double rnorm2)
        {
            double v = 0;
            int i = 0;
            int nmain = 0;
            int nslack = 0;
            int i_ = 0;

            rnorm2 = 0;

            nmain = state.nmain;
            nslack = state.nslack;
            
            //
            // * subtract XE from XScaled
            // * project XScaled
            // * calculate norm of deviation from null space, store it in RNorm2
            // * calculate residual from projection, store it in R
            // * add XE to XScaled
            // * unscale variables
            //
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                xscaled[i_] = xscaled[i_] - state.xe[i_];
            }
            rnorm2 = 0;
            for(i=0; i<=nmain+nslack-1; i++)
            {
                rscaled[i] = 0;
            }
            for(i=0; i<=nmain+nslack-1; i++)
            {
                if( state.activeconstraints[i] )
                {
                    v = xscaled[i];
                    xscaled[i] = 0;
                    rscaled[i] = rscaled[i]+v;
                    rnorm2 = rnorm2+math.sqr(v);
                }
            }
            for(i=0; i<=state.cecnt-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    v += xscaled[i_]*state.cecurrent[i,i_];
                }
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    xscaled[i_] = xscaled[i_] - v*state.cecurrent[i,i_];
                }
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    rscaled[i_] = rscaled[i_] + v*state.cecurrent[i,i_];
                }
                rnorm2 = rnorm2+math.sqr(v);
            }
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                xscaled[i_] = xscaled[i_] + state.xe[i_];
            }
            unscalepoint(state, xscaled, ref xunscaled);
        }


        /*************************************************************************
        This function scales and copies NMain elements of GUnscaled into GScaled.
        Other NSlack components of GScaled are set to zero.
        *************************************************************************/
        private static void scalegradientandexpand(minbleicstate state,
            double[] gunscaled,
            ref double[] gscaled)
        {
            int i = 0;

            for(i=0; i<=state.nmain-1; i++)
            {
                gscaled[i] = gunscaled[i]*state.transforms[i];
            }
            for(i=0; i<=state.nslack-1; i++)
            {
                gscaled[state.nmain+i] = 0;
            }
        }


        /*************************************************************************
        This subroutine applies modifications to the target function given by
        its value F and gradient G at the projected point X which lies in the
        equality constrained subspace.

        Following modifications are applied:
        * modified barrier functions to handle inequality constraints
          (both F and G are modified)
        * projection of gradient into equality constrained subspace
          (only G is modified)
        * quadratic penalty for deviations from equality constrained subspace
          (both F and G are modified)

        It also calculates gradient norm (three different norms for three
        different types of gradient), feasibility and complementary slackness
        errors.

        INPUT PARAMETERS:
            State   -   optimizer state (we use its fields to get information
                        about constraints)
            X       -   point (projected into equality constrained subspace)
            R       -   residual from projection
            RNorm2  -   residual norm squared
            F       -   function value at X
            G       -   function gradient at X

        OUTPUT PARAMETERS:
            F       -   modified function value at X
            G       -   modified function gradient at X
            GNorm   -   2-norm of unmodified G
            MPGNorm -   2-norm of modified G
            MBA     -   minimum argument of barrier functions.
                        If X is strictly feasible, it is greater than zero.
                        If X lies on a boundary, it is zero.
                        It is negative for infeasible X.
            FIErr   -   2-norm of feasibility error with respect to
                        inequality/bound constraints
            CSErr   -   2-norm of complementarity slackness error
        *************************************************************************/
        private static void modifytargetfunction(minbleicstate state,
            double[] x,
            double[] r,
            double rnorm2,
            ref double f,
            ref double[] g,
            ref double gnorm,
            ref double mpgnorm)
        {
            double v = 0;
            int i = 0;
            int nmain = 0;
            int nslack = 0;
            bool hasconstraints = new bool();
            int i_ = 0;

            gnorm = 0;
            mpgnorm = 0;

            nmain = state.nmain;
            nslack = state.nslack;
            hasconstraints = false;
            
            //
            // GNorm
            //
            v = 0.0;
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                v += g[i_]*g[i_];
            }
            gnorm = Math.Sqrt(v);
            
            //
            // Process equality constraints:
            // * modify F to handle penalty term for equality constraints
            // * project gradient on null space of equality constraints
            // * add penalty term for equality constraints to gradient
            //
            f = f+rnorm2;
            for(i=0; i<=nmain+nslack-1; i++)
            {
                if( state.activeconstraints[i] )
                {
                    g[i] = 0;
                }
            }
            for(i=0; i<=state.cecnt-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    v += g[i_]*state.cecurrent[i,i_];
                }
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    g[i_] = g[i_] - v*state.cecurrent[i,i_];
                }
            }
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                g[i_] = g[i_] + 2*r[i_];
            }
            
            //
            // MPGNorm
            //
            v = 0.0;
            for(i_=0; i_<=nmain+nslack-1;i_++)
            {
                v += g[i_]*g[i_];
            }
            mpgnorm = Math.Sqrt(v);
        }


        /*************************************************************************
        This function makes additional check for constraints which can be activated.

        We try activate constraints one by one, but it is possible that several
        constraints should be activated during one iteration. It this case only
        one of them (probably last) will be activated. This function will fix it -
        it will pass through constraints and activate those which are at the boundary
        or beyond it.

        It will return True, if at least one constraint was activated by this function.
        *************************************************************************/
        private static bool additionalcheckforconstraints(minbleicstate state,
            double[] x)
        {
            bool result = new bool();
            int i = 0;
            int nmain = 0;
            int nslack = 0;

            result = false;
            nmain = state.nmain;
            nslack = state.nslack;
            for(i=0; i<=nmain-1; i++)
            {
                if( !state.activeconstraints[i] )
                {
                    if( state.hasbndl[i] )
                    {
                        if( (double)(x[i])<=(double)(state.bndleffective[i]) )
                        {
                            state.activeconstraints[i] = true;
                            state.constrainedvalues[i] = state.bndleffective[i];
                            result = true;
                        }
                    }
                    if( state.hasbndu[i] )
                    {
                        if( (double)(x[i])>=(double)(state.bndueffective[i]) )
                        {
                            state.activeconstraints[i] = true;
                            state.constrainedvalues[i] = state.bndueffective[i];
                            result = true;
                        }
                    }
                }
            }
            for(i=0; i<=nslack-1; i++)
            {
                if( !state.activeconstraints[nmain+i] )
                {
                    if( (double)(x[nmain+i])<=(double)(0) )
                    {
                        state.activeconstraints[nmain+i] = true;
                        state.constrainedvalues[nmain+i] = 0;
                        result = true;
                    }
                }
            }
            return result;
        }


        /*************************************************************************
        This function rebuilds CECurrent and XE according to current set of
        active bound constraints.
        *************************************************************************/
        private static void rebuildcexe(minbleicstate state)
        {
            int i = 0;
            int j = 0;
            int k = 0;
            int nmain = 0;
            int nslack = 0;
            double v = 0;
            int i_ = 0;

            nmain = state.nmain;
            nslack = state.nslack;
            ablas.rmatrixcopy(state.cecnt, nmain+nslack+1, state.ceeffective, 0, 0, ref state.cecurrent, 0, 0);
            for(i=0; i<=state.cecnt-1; i++)
            {
                
                //
                // "Subtract" active bound constraints from I-th linear constraint
                //
                for(j=0; j<=nmain+nslack-1; j++)
                {
                    if( state.activeconstraints[j] )
                    {
                        state.cecurrent[i,nmain+nslack] = state.cecurrent[i,nmain+nslack]-state.cecurrent[i,j]*state.constrainedvalues[j];
                        state.cecurrent[i,j] = 0.0;
                    }
                }
                
                //
                // Reorthogonalize I-th constraint with respect to previous ones
                // NOTE: we also update right part, which is CECurrent[...,NMain+NSlack].
                //
                for(k=0; k<=i-1; k++)
                {
                    v = 0.0;
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        v += state.cecurrent[k,i_]*state.cecurrent[i,i_];
                    }
                    for(i_=0; i_<=nmain+nslack;i_++)
                    {
                        state.cecurrent[i,i_] = state.cecurrent[i,i_] - v*state.cecurrent[k,i_];
                    }
                }
                
                //
                // Calculate norm of I-th row of CECurrent. Fill by zeros, if it is
                // too small. Normalize otherwise.
                //
                // NOTE: we also scale last column of CECurrent (right part)
                //
                v = 0.0;
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    v += state.cecurrent[i,i_]*state.cecurrent[i,i_];
                }
                v = Math.Sqrt(v);
                if( (double)(v)>(double)(10000*math.machineepsilon) )
                {
                    v = 1/v;
                    for(i_=0; i_<=nmain+nslack;i_++)
                    {
                        state.cecurrent[i,i_] = v*state.cecurrent[i,i_];
                    }
                }
                else
                {
                    for(j=0; j<=nmain+nslack; j++)
                    {
                        state.cecurrent[i,j] = 0;
                    }
                }
            }
            for(j=0; j<=nmain+nslack-1; j++)
            {
                state.xe[j] = 0;
            }
            for(i=0; i<=nmain+nslack-1; i++)
            {
                if( state.activeconstraints[i] )
                {
                    state.xe[i] = state.xe[i]+state.constrainedvalues[i];
                }
            }
            for(i=0; i<=state.cecnt-1; i++)
            {
                v = state.cecurrent[i,nmain+nslack];
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    state.xe[i_] = state.xe[i_] + v*state.cecurrent[i,i_];
                }
            }
        }


        /*************************************************************************
        This function projects gradient onto equality constrained subspace
        *************************************************************************/
        private static void makegradientprojection(minbleicstate state,
            ref double[] pg)
        {
            int i = 0;
            int nmain = 0;
            int nslack = 0;
            double v = 0;
            int i_ = 0;

            nmain = state.nmain;
            nslack = state.nslack;
            for(i=0; i<=nmain+nslack-1; i++)
            {
                if( state.activeconstraints[i] )
                {
                    pg[i] = 0;
                }
            }
            for(i=0; i<=state.cecnt-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    v += pg[i_]*state.cecurrent[i,i_];
                }
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    pg[i_] = pg[i_] - v*state.cecurrent[i,i_];
                }
            }
        }


        /*************************************************************************
        This function prepares equality constrained subproblem:

        1. X is used to activate constraints (if there are ones which are still
           inactive, but should be activated).
        2. constraints matrix CEOrt is copied to CECurrent and modified  according
           to the list of active bound constraints (corresponding elements are
           filled by zeros and reorthogonalized).
        3. XE - least squares solution of equality constraints - is recalculated
        4. X is copied to PX and projected onto equality constrained subspace
        5. inactive constraints are checked against PX - if there is at least one
           which should be activated, we activate it and move back to (2)
        6. as result, PX is feasible with respect to bound constraints - step (5)
           guarantees it. But PX can be infeasible with respect to equality ones,
           because step (2) is done without checks for consistency. As the final
           step, we check that PX is feasible. If not, we return False. True is
           returned otherwise.

        If this algorithm returned True, then:
        * X is not changed
        * PX contains projection of X onto constrained subspace
        * G is not changed
        * PG contains projection of G onto constrained subspace
        * PX is feasible with respect to all constraints
        * all constraints which are active at PX, are activated
        *************************************************************************/
        private static bool prepareconstraintmatrix(minbleicstate state,
            double[] x,
            double[] g,
            ref double[] px,
            ref double[] pg)
        {
            bool result = new bool();
            int i = 0;
            int nmain = 0;
            int nslack = 0;
            double v = 0;
            double ferr = 0;
            int i_ = 0;

            nmain = state.nmain;
            nslack = state.nslack;
            result = true;
            
            //
            // Step 1
            //
            additionalcheckforconstraints(state, x);
            
            //
            // Steps 2-5
            //
            do
            {
                
                //
                // Steps 2-3
                //
                rebuildcexe(state);
                
                //
                // Step 4
                //
                // Calculate PX, PG
                //
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    px[i_] = x[i_];
                }
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    px[i_] = px[i_] - state.xe[i_];
                }
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    pg[i_] = g[i_];
                }
                for(i=0; i<=nmain+nslack-1; i++)
                {
                    if( state.activeconstraints[i] )
                    {
                        px[i] = 0;
                        pg[i] = 0;
                    }
                }
                for(i=0; i<=state.cecnt-1; i++)
                {
                    v = 0.0;
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        v += px[i_]*state.cecurrent[i,i_];
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        px[i_] = px[i_] - v*state.cecurrent[i,i_];
                    }
                    v = 0.0;
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        v += pg[i_]*state.cecurrent[i,i_];
                    }
                    for(i_=0; i_<=nmain+nslack-1;i_++)
                    {
                        pg[i_] = pg[i_] - v*state.cecurrent[i,i_];
                    }
                }
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    px[i_] = px[i_] + state.xe[i_];
                }
                
                //
                // Step 5 (loop condition below)
                //
            }
            while( additionalcheckforconstraints(state, px) );
            
            //
            // Step 6
            //
            ferr = 0;
            for(i=0; i<=state.cecnt-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=nmain+nslack-1;i_++)
                {
                    v += px[i_]*state.ceeffective[i,i_];
                }
                v = v-state.ceeffective[i,nmain+nslack];
                ferr = Math.Max(ferr, Math.Abs(v));
            }
            result = (double)(ferr)<=(double)(state.outerepsi);
            return result;
        }


        /*************************************************************************
        Internal initialization subroutine
        *************************************************************************/
        private static void minbleicinitinternal(int n,
            double[] x,
            double diffstep,
            minbleicstate state)
        {
            int i = 0;
            double[,] c = new double[0,0];
            int[] ct = new int[0];

            
            //
            // Initialize
            //
            state.teststep = 0;
            state.nmain = n;
            state.optdim = 0;
            state.diffstep = diffstep;
            state.bndloriginal = new double[n];
            state.bndleffective = new double[n];
            state.hasbndl = new bool[n];
            state.bnduoriginal = new double[n];
            state.bndueffective = new double[n];
            state.hasbndu = new bool[n];
            state.xstart = new double[n];
            state.soriginal = new double[n];
            state.x = new double[n];
            state.g = new double[n];
            for(i=0; i<=n-1; i++)
            {
                state.bndloriginal[i] = Double.NegativeInfinity;
                state.hasbndl[i] = false;
                state.bnduoriginal[i] = Double.PositiveInfinity;
                state.hasbndu[i] = false;
                state.soriginal[i] = 1.0;
            }
            minbleicsetlc(state, c, ct, 0);
            minbleicsetinnercond(state, 0.0, 0.0, 0.0);
            minbleicsetoutercond(state, 1.0E-6, 1.0E-6);
            minbleicsetmaxits(state, 0);
            minbleicsetxrep(state, false);
            minbleicsetstpmax(state, 0.0);
            minbleicsetprecdefault(state);
            minbleicrestartfrom(state, x);
        }


    }
    public class minlbfgs
    {
        public class minlbfgsstate
        {
            public int n;
            public int m;
            public double epsg;
            public double epsf;
            public double epsx;
            public int maxits;
            public bool xrep;
            public double stpmax;
            public double[] s;
            public double diffstep;
            public int nfev;
            public int mcstage;
            public int k;
            public int q;
            public int p;
            public double[] rho;
            public double[,] yk;
            public double[,] sk;
            public double[] theta;
            public double[] d;
            public double stp;
            public double[] work;
            public double fold;
            public double trimthreshold;
            public int prectype;
            public double gammak;
            public double[,] denseh;
            public double[] diagh;
            public double fbase;
            public double fm2;
            public double fm1;
            public double fp1;
            public double fp2;
            public double[] autobuf;
            public double[] x;
            public double f;
            public double[] g;
            public bool needf;
            public bool needfg;
            public bool xupdated;
            public double teststep;
            public rcommstate rstate;
            public int repiterationscount;
            public int repnfev;
            public int repvaridx;
            public int repterminationtype;
            public linmin.linminstate lstate;
            public minlbfgsstate()
            {
                s = new double[0];
                rho = new double[0];
                yk = new double[0,0];
                sk = new double[0,0];
                theta = new double[0];
                d = new double[0];
                work = new double[0];
                denseh = new double[0,0];
                diagh = new double[0];
                autobuf = new double[0];
                x = new double[0];
                g = new double[0];
                rstate = new rcommstate();
                lstate = new linmin.linminstate();
            }
        };


        public class minlbfgsreport
        {
            public int iterationscount;
            public int nfev;
            public int varidx;
            public int terminationtype;
        };




        public const double gtol = 0.4;


        /*************************************************************************
                LIMITED MEMORY BFGS METHOD FOR LARGE SCALE OPTIMIZATION

        DESCRIPTION:
        The subroutine minimizes function F(x) of N arguments by  using  a  quasi-
        Newton method (LBFGS scheme) which is optimized to use  a  minimum  amount
        of memory.
        The subroutine generates the approximation of an inverse Hessian matrix by
        using information about the last M steps of the algorithm  (instead of N).
        It lessens a required amount of memory from a value  of  order  N^2  to  a
        value of order 2*N*M.


        REQUIREMENTS:
        Algorithm will request following information during its operation:
        * function value F and its gradient G (simultaneously) at given point X


        USAGE:
        1. User initializes algorithm state with MinLBFGSCreate() call
        2. User tunes solver parameters with MinLBFGSSetCond() MinLBFGSSetStpMax()
           and other functions
        3. User calls MinLBFGSOptimize() function which takes algorithm  state and
           pointer (delegate, etc.) to callback function which calculates F/G.
        4. User calls MinLBFGSResults() to get solution
        5. Optionally user may call MinLBFGSRestartFrom() to solve another problem
           with same N/M but another starting point and/or another function.
           MinLBFGSRestartFrom() allows to reuse already initialized structure.


        INPUT PARAMETERS:
            N       -   problem dimension. N>0
            M       -   number of corrections in the BFGS scheme of Hessian
                        approximation update. Recommended value:  3<=M<=7. The smaller
                        value causes worse convergence, the bigger will  not  cause  a
                        considerably better convergence, but will cause a fall in  the
                        performance. M<=N.
            X       -   initial solution approximation, array[0..N-1].


        OUTPUT PARAMETERS:
            State   -   structure which stores algorithm state
            

        NOTES:
        1. you may tune stopping conditions with MinLBFGSSetCond() function
        2. if target function contains exp() or other fast growing functions,  and
           optimization algorithm makes too large steps which leads  to  overflow,
           use MinLBFGSSetStpMax() function to bound algorithm's  steps.  However,
           L-BFGS rarely needs such a tuning.


          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgscreate(int n,
            int m,
            double[] x,
            minlbfgsstate state)
        {
            alglib.ap.assert(n>=1, "MinLBFGSCreate: N<1!");
            alglib.ap.assert(m>=1, "MinLBFGSCreate: M<1");
            alglib.ap.assert(m<=n, "MinLBFGSCreate: M>N");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinLBFGSCreate: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinLBFGSCreate: X contains infinite or NaN values!");
            minlbfgscreatex(n, m, x, 0, 0.0, state);
        }


        /*************************************************************************
        The subroutine is finite difference variant of MinLBFGSCreate().  It  uses
        finite differences in order to differentiate target function.

        Description below contains information which is specific to  this function
        only. We recommend to read comments on MinLBFGSCreate() in  order  to  get
        more information about creation of LBFGS optimizer.

        INPUT PARAMETERS:
            N       -   problem dimension, N>0:
                        * if given, only leading N elements of X are used
                        * if not given, automatically determined from size of X
            M       -   number of corrections in the BFGS scheme of Hessian
                        approximation update. Recommended value:  3<=M<=7. The smaller
                        value causes worse convergence, the bigger will  not  cause  a
                        considerably better convergence, but will cause a fall in  the
                        performance. M<=N.
            X       -   starting point, array[0..N-1].
            DiffStep-   differentiation step, >0

        OUTPUT PARAMETERS:
            State   -   structure which stores algorithm state

        NOTES:
        1. algorithm uses 4-point central formula for differentiation.
        2. differentiation step along I-th axis is equal to DiffStep*S[I] where
           S[] is scaling vector which can be set by MinLBFGSSetScale() call.
        3. we recommend you to use moderate values of  differentiation  step.  Too
           large step will result in too large truncation  errors, while too small
           step will result in too large numerical  errors.  1.0E-6  can  be  good
           value to start with.
        4. Numerical  differentiation  is   very   inefficient  -   one   gradient
           calculation needs 4*N function evaluations. This function will work for
           any N - either small (1...10), moderate (10...100) or  large  (100...).
           However, performance penalty will be too severe for any N's except  for
           small ones.
           We should also say that code which relies on numerical  differentiation
           is   less  robust  and  precise.  LBFGS  needs  exact  gradient values.
           Imprecise gradient may slow  down  convergence,  especially  on  highly
           nonlinear problems.
           Thus  we  recommend to use this function for fast prototyping on small-
           dimensional problems only, and to implement analytical gradient as soon
           as possible.

          -- ALGLIB --
             Copyright 16.05.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgscreatef(int n,
            int m,
            double[] x,
            double diffstep,
            minlbfgsstate state)
        {
            alglib.ap.assert(n>=1, "MinLBFGSCreateF: N too small!");
            alglib.ap.assert(m>=1, "MinLBFGSCreateF: M<1");
            alglib.ap.assert(m<=n, "MinLBFGSCreateF: M>N");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinLBFGSCreateF: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinLBFGSCreateF: X contains infinite or NaN values!");
            alglib.ap.assert(math.isfinite(diffstep), "MinLBFGSCreateF: DiffStep is infinite or NaN!");
            alglib.ap.assert((double)(diffstep)>(double)(0), "MinLBFGSCreateF: DiffStep is non-positive!");
            minlbfgscreatex(n, m, x, 0, diffstep, state);
        }


        /*************************************************************************
        This function sets stopping conditions for L-BFGS optimization algorithm.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            EpsG    -   >=0
                        The  subroutine  finishes  its  work   if   the  condition
                        |v|<EpsG is satisfied, where:
                        * |.| means Euclidian norm
                        * v - scaled gradient vector, v[i]=g[i]*s[i]
                        * g - gradient
                        * s - scaling coefficients set by MinLBFGSSetScale()
            EpsF    -   >=0
                        The  subroutine  finishes  its work if on k+1-th iteration
                        the  condition  |F(k+1)-F(k)|<=EpsF*max{|F(k)|,|F(k+1)|,1}
                        is satisfied.
            EpsX    -   >=0
                        The subroutine finishes its work if  on  k+1-th  iteration
                        the condition |v|<=EpsX is fulfilled, where:
                        * |.| means Euclidian norm
                        * v - scaled step vector, v[i]=dx[i]/s[i]
                        * dx - ste pvector, dx=X(k+1)-X(k)
                        * s - scaling coefficients set by MinLBFGSSetScale()
            MaxIts  -   maximum number of iterations. If MaxIts=0, the  number  of
                        iterations is unlimited.

        Passing EpsG=0, EpsF=0, EpsX=0 and MaxIts=0 (simultaneously) will lead to
        automatic stopping criterion selection (small EpsX).

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetcond(minlbfgsstate state,
            double epsg,
            double epsf,
            double epsx,
            int maxits)
        {
            alglib.ap.assert(math.isfinite(epsg), "MinLBFGSSetCond: EpsG is not finite number!");
            alglib.ap.assert((double)(epsg)>=(double)(0), "MinLBFGSSetCond: negative EpsG!");
            alglib.ap.assert(math.isfinite(epsf), "MinLBFGSSetCond: EpsF is not finite number!");
            alglib.ap.assert((double)(epsf)>=(double)(0), "MinLBFGSSetCond: negative EpsF!");
            alglib.ap.assert(math.isfinite(epsx), "MinLBFGSSetCond: EpsX is not finite number!");
            alglib.ap.assert((double)(epsx)>=(double)(0), "MinLBFGSSetCond: negative EpsX!");
            alglib.ap.assert(maxits>=0, "MinLBFGSSetCond: negative MaxIts!");
            if( (((double)(epsg)==(double)(0) && (double)(epsf)==(double)(0)) && (double)(epsx)==(double)(0)) && maxits==0 )
            {
                epsx = 1.0E-6;
            }
            state.epsg = epsg;
            state.epsf = epsf;
            state.epsx = epsx;
            state.maxits = maxits;
        }


        /*************************************************************************
        This function turns on/off reporting.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            NeedXRep-   whether iteration reports are needed or not

        If NeedXRep is True, algorithm will call rep() callback function if  it is
        provided to MinLBFGSOptimize().


          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetxrep(minlbfgsstate state,
            bool needxrep)
        {
            state.xrep = needxrep;
        }


        /*************************************************************************
        This function sets maximum step length

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            StpMax  -   maximum step length, >=0. Set StpMax to 0.0 (default),  if
                        you don't want to limit step length.

        Use this subroutine when you optimize target function which contains exp()
        or  other  fast  growing  functions,  and optimization algorithm makes too
        large  steps  which  leads  to overflow. This function allows us to reject
        steps  that  are  too  large  (and  therefore  expose  us  to the possible
        overflow) without actually calculating function value at the x+stp*d.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetstpmax(minlbfgsstate state,
            double stpmax)
        {
            alglib.ap.assert(math.isfinite(stpmax), "MinLBFGSSetStpMax: StpMax is not finite!");
            alglib.ap.assert((double)(stpmax)>=(double)(0), "MinLBFGSSetStpMax: StpMax<0!");
            state.stpmax = stpmax;
        }


        /*************************************************************************
        This function sets scaling coefficients for LBFGS optimizer.

        ALGLIB optimizers use scaling matrices to test stopping  conditions  (step
        size and gradient are scaled before comparison with tolerances).  Scale of
        the I-th variable is a translation invariant measure of:
        a) "how large" the variable is
        b) how large the step should be to make significant changes in the function

        Scaling is also used by finite difference variant of the optimizer  - step
        along I-th axis is equal to DiffStep*S[I].

        In  most  optimizers  (and  in  the  LBFGS  too)  scaling is NOT a form of
        preconditioning. It just  affects  stopping  conditions.  You  should  set
        preconditioner  by  separate  call  to  one  of  the  MinLBFGSSetPrec...()
        functions.

        There  is  special  preconditioning  mode, however,  which  uses   scaling
        coefficients to form diagonal preconditioning matrix. You  can  turn  this
        mode on, if you want.   But  you should understand that scaling is not the
        same thing as preconditioning - these are two different, although  related
        forms of tuning solver.

        INPUT PARAMETERS:
            State   -   structure stores algorithm state
            S       -   array[N], non-zero scaling coefficients
                        S[i] may be negative, sign doesn't matter.

          -- ALGLIB --
             Copyright 14.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetscale(minlbfgsstate state,
            double[] s)
        {
            int i = 0;

            alglib.ap.assert(alglib.ap.len(s)>=state.n, "MinLBFGSSetScale: Length(S)<N");
            for(i=0; i<=state.n-1; i++)
            {
                alglib.ap.assert(math.isfinite(s[i]), "MinLBFGSSetScale: S contains infinite or NAN elements");
                alglib.ap.assert((double)(s[i])!=(double)(0), "MinLBFGSSetScale: S contains zero elements");
                state.s[i] = Math.Abs(s[i]);
            }
        }


        /*************************************************************************
        Extended subroutine for internal use only.

        Accepts additional parameters:

            Flags - additional settings:
                    * Flags = 0     means no additional settings
                    * Flags = 1     "do not allocate memory". used when solving
                                    a many subsequent tasks with  same N/M  values.
                                    First  call MUST  be without this flag bit set,
                                    subsequent  calls   of   MinLBFGS   with   same
                                    MinLBFGSState structure can set Flags to 1.
            DiffStep - numerical differentiation step

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgscreatex(int n,
            int m,
            double[] x,
            int flags,
            double diffstep,
            minlbfgsstate state)
        {
            bool allocatemem = new bool();
            int i = 0;

            alglib.ap.assert(n>=1, "MinLBFGS: N too small!");
            alglib.ap.assert(m>=1, "MinLBFGS: M too small!");
            alglib.ap.assert(m<=n, "MinLBFGS: M too large!");
            
            //
            // Initialize
            //
            state.teststep = 0;
            state.diffstep = diffstep;
            state.n = n;
            state.m = m;
            allocatemem = flags%2==0;
            flags = flags/2;
            if( allocatemem )
            {
                state.rho = new double[m];
                state.theta = new double[m];
                state.yk = new double[m, n];
                state.sk = new double[m, n];
                state.d = new double[n];
                state.x = new double[n];
                state.s = new double[n];
                state.g = new double[n];
                state.work = new double[n];
            }
            minlbfgssetcond(state, 0, 0, 0, 0);
            minlbfgssetxrep(state, false);
            minlbfgssetstpmax(state, 0);
            minlbfgsrestartfrom(state, x);
            for(i=0; i<=n-1; i++)
            {
                state.s[i] = 1.0;
            }
            state.prectype = 0;
        }


        /*************************************************************************
        Modification  of  the  preconditioner:  default  preconditioner    (simple
        scaling, same for all elements of X) is used.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state

        NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
        iterations.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetprecdefault(minlbfgsstate state)
        {
            state.prectype = 0;
        }


        /*************************************************************************
        Modification of the preconditioner: Cholesky factorization of  approximate
        Hessian is used.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            P       -   triangular preconditioner, Cholesky factorization of
                        the approximate Hessian. array[0..N-1,0..N-1],
                        (if larger, only leading N elements are used).
            IsUpper -   whether upper or lower triangle of P is given
                        (other triangle is not referenced)

        After call to this function preconditioner is changed to P  (P  is  copied
        into the internal buffer).

        NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
        iterations.

        NOTE 2:  P  should  be nonsingular. Exception will be thrown otherwise.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetpreccholesky(minlbfgsstate state,
            double[,] p,
            bool isupper)
        {
            int i = 0;
            double mx = 0;

            alglib.ap.assert(apserv.isfinitertrmatrix(p, state.n, isupper), "MinLBFGSSetPrecCholesky: P contains infinite or NAN values!");
            mx = 0;
            for(i=0; i<=state.n-1; i++)
            {
                mx = Math.Max(mx, Math.Abs(p[i,i]));
            }
            alglib.ap.assert((double)(mx)>(double)(0), "MinLBFGSSetPrecCholesky: P is strictly singular!");
            if( alglib.ap.rows(state.denseh)<state.n || alglib.ap.cols(state.denseh)<state.n )
            {
                state.denseh = new double[state.n, state.n];
            }
            state.prectype = 1;
            if( isupper )
            {
                ablas.rmatrixcopy(state.n, state.n, p, 0, 0, ref state.denseh, 0, 0);
            }
            else
            {
                ablas.rmatrixtranspose(state.n, state.n, p, 0, 0, ref state.denseh, 0, 0);
            }
        }


        /*************************************************************************
        Modification  of  the  preconditioner:  diagonal of approximate Hessian is
        used.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            D       -   diagonal of the approximate Hessian, array[0..N-1],
                        (if larger, only leading N elements are used).

        NOTE:  you  can  change  preconditioner  "on  the  fly",  during algorithm
        iterations.

        NOTE 2: D[i] should be positive. Exception will be thrown otherwise.

        NOTE 3: you should pass diagonal of approximate Hessian - NOT ITS INVERSE.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetprecdiag(minlbfgsstate state,
            double[] d)
        {
            int i = 0;

            alglib.ap.assert(alglib.ap.len(d)>=state.n, "MinLBFGSSetPrecDiag: D is too short");
            for(i=0; i<=state.n-1; i++)
            {
                alglib.ap.assert(math.isfinite(d[i]), "MinLBFGSSetPrecDiag: D contains infinite or NAN elements");
                alglib.ap.assert((double)(d[i])>(double)(0), "MinLBFGSSetPrecDiag: D contains non-positive elements");
            }
            apserv.rvectorsetlengthatleast(ref state.diagh, state.n);
            state.prectype = 2;
            for(i=0; i<=state.n-1; i++)
            {
                state.diagh[i] = d[i];
            }
        }


        /*************************************************************************
        Modification of the preconditioner: scale-based diagonal preconditioning.

        This preconditioning mode can be useful when you  don't  have  approximate
        diagonal of Hessian, but you know that your  variables  are  badly  scaled
        (for  example,  one  variable is in [1,10], and another in [1000,100000]),
        and most part of the ill-conditioning comes from different scales of vars.

        In this case simple  scale-based  preconditioner,  with H[i] = 1/(s[i]^2),
        can greatly improve convergence.

        IMPRTANT: you should set scale of your variables  with  MinLBFGSSetScale()
        call  (before  or after MinLBFGSSetPrecScale() call). Without knowledge of
        the scale of your variables scale-based preconditioner will be  just  unit
        matrix.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetprecscale(minlbfgsstate state)
        {
            state.prectype = 3;
        }


        /*************************************************************************
        NOTES:

        1. This function has two different implementations: one which  uses  exact
           (analytical) user-supplied gradient,  and one which uses function value
           only  and  numerically  differentiates  function  in  order  to  obtain
           gradient.

           Depending  on  the  specific  function  used to create optimizer object
           (either MinLBFGSCreate() for analytical gradient  or  MinLBFGSCreateF()
           for numerical differentiation) you should choose appropriate variant of
           MinLBFGSOptimize() - one  which  accepts  function  AND gradient or one
           which accepts function ONLY.

           Be careful to choose variant of MinLBFGSOptimize() which corresponds to
           your optimization scheme! Table below lists different  combinations  of
           callback (function/gradient) passed to MinLBFGSOptimize()  and specific
           function used to create optimizer.


                             |         USER PASSED TO MinLBFGSOptimize()
           CREATED WITH      |  function only   |  function and gradient
           ------------------------------------------------------------
           MinLBFGSCreateF() |     work                FAIL
           MinLBFGSCreate()  |     FAIL                work

           Here "FAIL" denotes inappropriate combinations  of  optimizer  creation
           function  and  MinLBFGSOptimize()  version.   Attemps   to   use   such
           combination (for example, to create optimizer with MinLBFGSCreateF() and
           to pass gradient information to MinCGOptimize()) will lead to exception
           being thrown. Either  you  did  not pass gradient when it WAS needed or
           you passed gradient when it was NOT needed.

          -- ALGLIB --
             Copyright 20.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static bool minlbfgsiteration(minlbfgsstate state)
        {
            bool result = new bool();
            int n = 0;
            int m = 0;
            int i = 0;
            int j = 0;
            int ic = 0;
            int mcinfo = 0;
            double v = 0;
            double vv = 0;
            int i_ = 0;

            
            //
            // Reverse communication preparations
            // I know it looks ugly, but it works the same way
            // anywhere from C++ to Python.
            //
            // This code initializes locals by:
            // * random values determined during code
            //   generation - on first subroutine call
            // * values from previous call - on subsequent calls
            //
            if( state.rstate.stage>=0 )
            {
                n = state.rstate.ia[0];
                m = state.rstate.ia[1];
                i = state.rstate.ia[2];
                j = state.rstate.ia[3];
                ic = state.rstate.ia[4];
                mcinfo = state.rstate.ia[5];
                v = state.rstate.ra[0];
                vv = state.rstate.ra[1];
            }
            else
            {
                n = -983;
                m = -989;
                i = -834;
                j = 900;
                ic = -287;
                mcinfo = 364;
                v = 214;
                vv = -338;
            }
            if( state.rstate.stage==0 )
            {
                goto lbl_0;
            }
            if( state.rstate.stage==1 )
            {
                goto lbl_1;
            }
            if( state.rstate.stage==2 )
            {
                goto lbl_2;
            }
            if( state.rstate.stage==3 )
            {
                goto lbl_3;
            }
            if( state.rstate.stage==4 )
            {
                goto lbl_4;
            }
            if( state.rstate.stage==5 )
            {
                goto lbl_5;
            }
            if( state.rstate.stage==6 )
            {
                goto lbl_6;
            }
            if( state.rstate.stage==7 )
            {
                goto lbl_7;
            }
            if( state.rstate.stage==8 )
            {
                goto lbl_8;
            }
            if( state.rstate.stage==9 )
            {
                goto lbl_9;
            }
            if( state.rstate.stage==10 )
            {
                goto lbl_10;
            }
            if( state.rstate.stage==11 )
            {
                goto lbl_11;
            }
            if( state.rstate.stage==12 )
            {
                goto lbl_12;
            }
            if( state.rstate.stage==13 )
            {
                goto lbl_13;
            }
            if( state.rstate.stage==14 )
            {
                goto lbl_14;
            }
            if( state.rstate.stage==15 )
            {
                goto lbl_15;
            }
            if( state.rstate.stage==16 )
            {
                goto lbl_16;
            }
            
            //
            // Routine body
            //
            
            //
            // Unload frequently used variables from State structure
            // (just for typing convinience)
            //
            n = state.n;
            m = state.m;
            state.repterminationtype = 0;
            state.repiterationscount = 0;
            state.repvaridx = -1;
            state.repnfev = 0;
            
            //
            //  Check, that transferred derivative value is right
            //
            clearrequestfields(state);
            if( !((double)(state.diffstep)==(double)(0) && (double)(state.teststep)>(double)(0)) )
            {
                goto lbl_17;
            }
            state.needfg = true;
            i = 0;
        lbl_19:
            if( i>n-1 )
            {
                goto lbl_21;
            }
            v = state.x[i];
            state.x[i] = v-state.teststep*state.s[i];
            state.rstate.stage = 0;
            goto lbl_rcomm;
        lbl_0:
            state.fm1 = state.f;
            state.fp1 = state.g[i];
            state.x[i] = v+state.teststep*state.s[i];
            state.rstate.stage = 1;
            goto lbl_rcomm;
        lbl_1:
            state.fm2 = state.f;
            state.fp2 = state.g[i];
            state.x[i] = v;
            state.rstate.stage = 2;
            goto lbl_rcomm;
        lbl_2:
            
            //
            // 2*State.TestStep   -   scale parameter
            // width of segment [Xi-TestStep;Xi+TestStep]
            //
            if( !optserv.derivativecheck(state.fm1, state.fp1, state.fm2, state.fp2, state.f, state.g[i], 2*state.teststep) )
            {
                state.repvaridx = i;
                state.repterminationtype = -7;
                result = false;
                return result;
            }
            i = i+1;
            goto lbl_19;
        lbl_21:
            state.needfg = false;
        lbl_17:
            
            //
            // Calculate F/G at the initial point
            //
            clearrequestfields(state);
            if( (double)(state.diffstep)!=(double)(0) )
            {
                goto lbl_22;
            }
            state.needfg = true;
            state.rstate.stage = 3;
            goto lbl_rcomm;
        lbl_3:
            state.needfg = false;
            goto lbl_23;
        lbl_22:
            state.needf = true;
            state.rstate.stage = 4;
            goto lbl_rcomm;
        lbl_4:
            state.fbase = state.f;
            i = 0;
        lbl_24:
            if( i>n-1 )
            {
                goto lbl_26;
            }
            v = state.x[i];
            state.x[i] = v-state.diffstep*state.s[i];
            state.rstate.stage = 5;
            goto lbl_rcomm;
        lbl_5:
            state.fm2 = state.f;
            state.x[i] = v-0.5*state.diffstep*state.s[i];
            state.rstate.stage = 6;
            goto lbl_rcomm;
        lbl_6:
            state.fm1 = state.f;
            state.x[i] = v+0.5*state.diffstep*state.s[i];
            state.rstate.stage = 7;
            goto lbl_rcomm;
        lbl_7:
            state.fp1 = state.f;
            state.x[i] = v+state.diffstep*state.s[i];
            state.rstate.stage = 8;
            goto lbl_rcomm;
        lbl_8:
            state.fp2 = state.f;
            state.x[i] = v;
            state.g[i] = (8*(state.fp1-state.fm1)-(state.fp2-state.fm2))/(6*state.diffstep*state.s[i]);
            i = i+1;
            goto lbl_24;
        lbl_26:
            state.f = state.fbase;
            state.needf = false;
        lbl_23:
            optserv.trimprepare(state.f, ref state.trimthreshold);
            if( !state.xrep )
            {
                goto lbl_27;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 9;
            goto lbl_rcomm;
        lbl_9:
            state.xupdated = false;
        lbl_27:
            state.repnfev = 1;
            state.fold = state.f;
            v = 0;
            for(i=0; i<=n-1; i++)
            {
                v = v+math.sqr(state.g[i]*state.s[i]);
            }
            if( (double)(Math.Sqrt(v))<=(double)(state.epsg) )
            {
                state.repterminationtype = 4;
                result = false;
                return result;
            }
            
            //
            // Choose initial step and direction.
            // Apply preconditioner, if we have something other than default.
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.d[i_] = -state.g[i_];
            }
            if( state.prectype==0 )
            {
                
                //
                // Default preconditioner is used, but we can't use it before iterations will start
                //
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.g[i_]*state.g[i_];
                }
                v = Math.Sqrt(v);
                if( (double)(state.stpmax)==(double)(0) )
                {
                    state.stp = Math.Min(1.0/v, 1);
                }
                else
                {
                    state.stp = Math.Min(1.0/v, state.stpmax);
                }
            }
            if( state.prectype==1 )
            {
                
                //
                // Cholesky preconditioner is used
                //
                fbls.fblscholeskysolve(state.denseh, 1.0, n, true, ref state.d, ref state.autobuf);
                state.stp = 1;
            }
            if( state.prectype==2 )
            {
                
                //
                // diagonal approximation is used
                //
                for(i=0; i<=n-1; i++)
                {
                    state.d[i] = state.d[i]/state.diagh[i];
                }
                state.stp = 1;
            }
            if( state.prectype==3 )
            {
                
                //
                // scale-based preconditioner is used
                //
                for(i=0; i<=n-1; i++)
                {
                    state.d[i] = state.d[i]*state.s[i]*state.s[i];
                }
                state.stp = 1;
            }
            
            //
            // Main cycle
            //
            state.k = 0;
        lbl_29:
            if( false )
            {
                goto lbl_30;
            }
            
            //
            // Main cycle: prepare to 1-D line search
            //
            state.p = state.k%m;
            state.q = Math.Min(state.k, m-1);
            
            //
            // Store X[k], G[k]
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.sk[state.p,i_] = -state.x[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.yk[state.p,i_] = -state.g[i_];
            }
            
            //
            // Minimize F(x+alpha*d)
            // Calculate S[k], Y[k]
            //
            state.mcstage = 0;
            if( state.k!=0 )
            {
                state.stp = 1.0;
            }
            linmin.linminnormalized(ref state.d, ref state.stp, n);
            linmin.mcsrch(n, ref state.x, ref state.f, ref state.g, state.d, ref state.stp, state.stpmax, gtol, ref mcinfo, ref state.nfev, ref state.work, state.lstate, ref state.mcstage);
        lbl_31:
            if( state.mcstage==0 )
            {
                goto lbl_32;
            }
            clearrequestfields(state);
            if( (double)(state.diffstep)!=(double)(0) )
            {
                goto lbl_33;
            }
            state.needfg = true;
            state.rstate.stage = 10;
            goto lbl_rcomm;
        lbl_10:
            state.needfg = false;
            goto lbl_34;
        lbl_33:
            state.needf = true;
            state.rstate.stage = 11;
            goto lbl_rcomm;
        lbl_11:
            state.fbase = state.f;
            i = 0;
        lbl_35:
            if( i>n-1 )
            {
                goto lbl_37;
            }
            v = state.x[i];
            state.x[i] = v-state.diffstep*state.s[i];
            state.rstate.stage = 12;
            goto lbl_rcomm;
        lbl_12:
            state.fm2 = state.f;
            state.x[i] = v-0.5*state.diffstep*state.s[i];
            state.rstate.stage = 13;
            goto lbl_rcomm;
        lbl_13:
            state.fm1 = state.f;
            state.x[i] = v+0.5*state.diffstep*state.s[i];
            state.rstate.stage = 14;
            goto lbl_rcomm;
        lbl_14:
            state.fp1 = state.f;
            state.x[i] = v+state.diffstep*state.s[i];
            state.rstate.stage = 15;
            goto lbl_rcomm;
        lbl_15:
            state.fp2 = state.f;
            state.x[i] = v;
            state.g[i] = (8*(state.fp1-state.fm1)-(state.fp2-state.fm2))/(6*state.diffstep*state.s[i]);
            i = i+1;
            goto lbl_35;
        lbl_37:
            state.f = state.fbase;
            state.needf = false;
        lbl_34:
            optserv.trimfunction(ref state.f, ref state.g, n, state.trimthreshold);
            linmin.mcsrch(n, ref state.x, ref state.f, ref state.g, state.d, ref state.stp, state.stpmax, gtol, ref mcinfo, ref state.nfev, ref state.work, state.lstate, ref state.mcstage);
            goto lbl_31;
        lbl_32:
            if( !state.xrep )
            {
                goto lbl_38;
            }
            
            //
            // report
            //
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 16;
            goto lbl_rcomm;
        lbl_16:
            state.xupdated = false;
        lbl_38:
            state.repnfev = state.repnfev+state.nfev;
            state.repiterationscount = state.repiterationscount+1;
            for(i_=0; i_<=n-1;i_++)
            {
                state.sk[state.p,i_] = state.sk[state.p,i_] + state.x[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.yk[state.p,i_] = state.yk[state.p,i_] + state.g[i_];
            }
            
            //
            // Stopping conditions
            //
            if( state.repiterationscount>=state.maxits && state.maxits>0 )
            {
                
                //
                // Too many iterations
                //
                state.repterminationtype = 5;
                result = false;
                return result;
            }
            v = 0;
            for(i=0; i<=n-1; i++)
            {
                v = v+math.sqr(state.g[i]*state.s[i]);
            }
            if( (double)(Math.Sqrt(v))<=(double)(state.epsg) )
            {
                
                //
                // Gradient is small enough
                //
                state.repterminationtype = 4;
                result = false;
                return result;
            }
            if( (double)(state.fold-state.f)<=(double)(state.epsf*Math.Max(Math.Abs(state.fold), Math.Max(Math.Abs(state.f), 1.0))) )
            {
                
                //
                // F(k+1)-F(k) is small enough
                //
                state.repterminationtype = 1;
                result = false;
                return result;
            }
            v = 0;
            for(i=0; i<=n-1; i++)
            {
                v = v+math.sqr(state.sk[state.p,i]/state.s[i]);
            }
            if( (double)(Math.Sqrt(v))<=(double)(state.epsx) )
            {
                
                //
                // X(k+1)-X(k) is small enough
                //
                state.repterminationtype = 2;
                result = false;
                return result;
            }
            
            //
            // If Wolfe conditions are satisfied, we can update
            // limited memory model.
            //
            // However, if conditions are not satisfied (NFEV limit is met,
            // function is too wild, ...), we'll skip L-BFGS update
            //
            if( mcinfo!=1 )
            {
                
                //
                // Skip update.
                //
                // In such cases we'll initialize search direction by
                // antigradient vector, because it  leads to more
                // transparent code with less number of special cases
                //
                state.fold = state.f;
                for(i_=0; i_<=n-1;i_++)
                {
                    state.d[i_] = -state.g[i_];
                }
            }
            else
            {
                
                //
                // Calculate Rho[k], GammaK
                //
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.yk[state.p,i_]*state.sk[state.p,i_];
                }
                vv = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    vv += state.yk[state.p,i_]*state.yk[state.p,i_];
                }
                if( (double)(v)==(double)(0) || (double)(vv)==(double)(0) )
                {
                    
                    //
                    // Rounding errors make further iterations impossible.
                    //
                    state.repterminationtype = -2;
                    result = false;
                    return result;
                }
                state.rho[state.p] = 1/v;
                state.gammak = v/vv;
                
                //
                //  Calculate d(k+1) = -H(k+1)*g(k+1)
                //
                //  for I:=K downto K-Q do
                //      V = s(i)^T * work(iteration:I)
                //      theta(i) = V
                //      work(iteration:I+1) = work(iteration:I) - V*Rho(i)*y(i)
                //  work(last iteration) = H0*work(last iteration) - preconditioner
                //  for I:=K-Q to K do
                //      V = y(i)^T*work(iteration:I)
                //      work(iteration:I+1) = work(iteration:I) +(-V+theta(i))*Rho(i)*s(i)
                //
                //  NOW WORK CONTAINS d(k+1)
                //
                for(i_=0; i_<=n-1;i_++)
                {
                    state.work[i_] = state.g[i_];
                }
                for(i=state.k; i>=state.k-state.q; i--)
                {
                    ic = i%m;
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += state.sk[ic,i_]*state.work[i_];
                    }
                    state.theta[ic] = v;
                    vv = v*state.rho[ic];
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.work[i_] = state.work[i_] - vv*state.yk[ic,i_];
                    }
                }
                if( state.prectype==0 )
                {
                    
                    //
                    // Simple preconditioner is used
                    //
                    v = state.gammak;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.work[i_] = v*state.work[i_];
                    }
                }
                if( state.prectype==1 )
                {
                    
                    //
                    // Cholesky preconditioner is used
                    //
                    fbls.fblscholeskysolve(state.denseh, 1, n, true, ref state.work, ref state.autobuf);
                }
                if( state.prectype==2 )
                {
                    
                    //
                    // diagonal approximation is used
                    //
                    for(i=0; i<=n-1; i++)
                    {
                        state.work[i] = state.work[i]/state.diagh[i];
                    }
                }
                if( state.prectype==3 )
                {
                    
                    //
                    // scale-based preconditioner is used
                    //
                    for(i=0; i<=n-1; i++)
                    {
                        state.work[i] = state.work[i]*state.s[i]*state.s[i];
                    }
                }
                for(i=state.k-state.q; i<=state.k; i++)
                {
                    ic = i%m;
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += state.yk[ic,i_]*state.work[i_];
                    }
                    vv = state.rho[ic]*(-v+state.theta[ic]);
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.work[i_] = state.work[i_] + vv*state.sk[ic,i_];
                    }
                }
                for(i_=0; i_<=n-1;i_++)
                {
                    state.d[i_] = -state.work[i_];
                }
                
                //
                // Next step
                //
                state.fold = state.f;
                state.k = state.k+1;
            }
            goto lbl_29;
        lbl_30:
            result = false;
            return result;
            
            //
            // Saving state
            //
        lbl_rcomm:
            result = true;
            state.rstate.ia[0] = n;
            state.rstate.ia[1] = m;
            state.rstate.ia[2] = i;
            state.rstate.ia[3] = j;
            state.rstate.ia[4] = ic;
            state.rstate.ia[5] = mcinfo;
            state.rstate.ra[0] = v;
            state.rstate.ra[1] = vv;
            return result;
        }


        /*************************************************************************
        L-BFGS algorithm results

        INPUT PARAMETERS:
            State   -   algorithm state

        OUTPUT PARAMETERS:
            X       -   array[0..N-1], solution
            Rep     -   optimization report:
                        * Rep.TerminationType completetion code:
                            * -7    gradient verification failed.
                                    See MinLBFGSSetGradientCheck() for more information.
                            * -2    rounding errors prevent further improvement.
                                    X contains best point found.
                            * -1    incorrect parameters were specified
                            *  1    relative function improvement is no more than
                                    EpsF.
                            *  2    relative step is no more than EpsX.
                            *  4    gradient norm is no more than EpsG
                            *  5    MaxIts steps was taken
                            *  7    stopping conditions are too stringent,
                                    further improvement is impossible
                        * Rep.IterationsCount contains iterations count
                        * NFEV countains number of function calculations

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgsresults(minlbfgsstate state,
            ref double[] x,
            minlbfgsreport rep)
        {
            x = new double[0];

            minlbfgsresultsbuf(state, ref x, rep);
        }


        /*************************************************************************
        L-BFGS algorithm results

        Buffered implementation of MinLBFGSResults which uses pre-allocated buffer
        to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
        intended to be used in the inner cycles of performance critical algorithms
        where array reallocation penalty is too large to be ignored.

          -- ALGLIB --
             Copyright 20.08.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgsresultsbuf(minlbfgsstate state,
            ref double[] x,
            minlbfgsreport rep)
        {
            int i_ = 0;

            if( alglib.ap.len(x)<state.n )
            {
                x = new double[state.n];
            }
            for(i_=0; i_<=state.n-1;i_++)
            {
                x[i_] = state.x[i_];
            }
            rep.iterationscount = state.repiterationscount;
            rep.nfev = state.repnfev;
            rep.varidx = state.repvaridx;
            rep.terminationtype = state.repterminationtype;
        }


        /*************************************************************************
        This  subroutine restarts LBFGS algorithm from new point. All optimization
        parameters are left unchanged.

        This  function  allows  to  solve multiple  optimization  problems  (which
        must have same number of dimensions) without object reallocation penalty.

        INPUT PARAMETERS:
            State   -   structure used to store algorithm state
            X       -   new starting point.

          -- ALGLIB --
             Copyright 30.07.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgsrestartfrom(minlbfgsstate state,
            double[] x)
        {
            int i_ = 0;

            alglib.ap.assert(alglib.ap.len(x)>=state.n, "MinLBFGSRestartFrom: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, state.n), "MinLBFGSRestartFrom: X contains infinite or NaN values!");
            for(i_=0; i_<=state.n-1;i_++)
            {
                state.x[i_] = x[i_];
            }
            state.rstate.ia = new int[5+1];
            state.rstate.ra = new double[1+1];
            state.rstate.stage = -1;
            clearrequestfields(state);
        }


        /*************************************************************************
        This  subroutine  turns  on  verification  of  the  user-supplied analytic
        gradient:
        * user calls this subroutine before optimization begins
        * MinLBFGSOptimize() is called
        * prior to  actual  optimization, for each component  of  parameters being
          optimized X[i] algorithm performs following steps:
          * two trial steps are made to X[i]-TestStep*S[i] and X[i]+TestStep*S[i],
            where X[i] is i-th component of the initial point and S[i] is a  scale
            of i-th parameter
          * if needed, steps are bounded with respect to constraints on X[]
          * F(X) is evaluated at these trial points
          * we perform one more evaluation in the middle point of the interval
          * we  build  cubic  model using function values and derivatives at trial
            points and we compare its prediction with actual value in  the  middle
            point
          * in case difference between prediction and actual value is higher  than
            some predetermined threshold, algorithm stops with completion code -7;
            Rep.VarIdx is set to index of the parameter with incorrect derivative.
        * after verification is over, algorithm proceeds to the actual optimization.

        NOTE 1: verification  needs  N (parameters count) gradient evaluations. It
                is very costly and you should use  it  only  for  low  dimensional
                problems,  when  you  want  to  be  sure  that  you've   correctly
                calculated  analytic  derivatives.  You  should  not use it in the
                production code (unless you want to check derivatives provided  by
                some third party).

        NOTE 2: you  should  carefully  choose  TestStep. Value which is too large
                (so large that function behaviour is significantly non-cubic) will
                lead to false alarms. You may use  different  step  for  different
                parameters by means of setting scale with MinLBFGSSetScale().

        NOTE 3: this function may lead to false positives. In case it reports that
                I-th  derivative was calculated incorrectly, you may decrease test
                step  and  try  one  more  time  - maybe your function changes too
                sharply  and  your  step  is  too  large for such rapidly chanding
                function.

        INPUT PARAMETERS:
            State       -   structure used to store algorithm state
            TestStep    -   verification step:
                            * TestStep=0 turns verification off
                            * TestStep>0 activates verification

          -- ALGLIB --
             Copyright 24.05.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetgradientcheck(minlbfgsstate state,
            double teststep)
        {
            alglib.ap.assert(math.isfinite(teststep), "MinLBFGSSetGradientCheck: TestStep contains NaN or Infinite");
            alglib.ap.assert((double)(teststep)>=(double)(0), "MinLBFGSSetGradientCheck: invalid argument TestStep(TestStep<0)");
            state.teststep = teststep;
        }


        /*************************************************************************
        Clears request fileds (to be sure that we don't forgot to clear something)
        *************************************************************************/
        private static void clearrequestfields(minlbfgsstate state)
        {
            state.needf = false;
            state.needfg = false;
            state.xupdated = false;
        }


    }
    public class cqmodels
    {
        /*************************************************************************
        This structure describes convex quadratic model of the form:
            f(x) = 0.5*(Alpha*x'*A*x + Tau*x'*D*x) + 0.5*Theta*(Q*x-r)'*(Q*x-r) + b'*x
        where:
            * Alpha>=0, Tau>=0, Theta>=0, Alpha+Tau>0.
            * A is NxN matrix, Q is NxK matrix (N>=1, K>=0), b is Nx1 vector,
              D is NxN diagonal matrix.
            * "main" quadratic term Alpha*A+Lambda*D is symmetric
              positive definite
        Structure may contain optional equality constraints of the form x[i]=x0[i],
        in this case functions provided by this unit calculate Newton step subject
        to these equality constraints.
        *************************************************************************/
        public class convexquadraticmodel
        {
            public int n;
            public int k;
            public double alpha;
            public double tau;
            public double theta;
            public double[,] a;
            public double[,] q;
            public double[] b;
            public double[] r;
            public double[] xc;
            public double[] d;
            public bool[] activeset;
            public double[,] tq2dense;
            public double[,] tk2;
            public double[] tq2diag;
            public double[] tq1;
            public double[] tk1;
            public double tq0;
            public double tk0;
            public double[] txc;
            public double[] tb;
            public int nfree;
            public int ecakind;
            public double[,] ecadense;
            public double[,] eq;
            public double[,] eccm;
            public double[] ecadiag;
            public double[] eb;
            public double ec;
            public double[] tmp0;
            public double[] tmp1;
            public double[] tmpg;
            public double[,] tmp2;
            public bool ismaintermchanged;
            public bool issecondarytermchanged;
            public bool islineartermchanged;
            public bool isactivesetchanged;
            public convexquadraticmodel()
            {
                a = new double[0,0];
                q = new double[0,0];
                b = new double[0];
                r = new double[0];
                xc = new double[0];
                d = new double[0];
                activeset = new bool[0];
                tq2dense = new double[0,0];
                tk2 = new double[0,0];
                tq2diag = new double[0];
                tq1 = new double[0];
                tk1 = new double[0];
                txc = new double[0];
                tb = new double[0];
                ecadense = new double[0,0];
                eq = new double[0,0];
                eccm = new double[0,0];
                ecadiag = new double[0];
                eb = new double[0];
                tmp0 = new double[0];
                tmp1 = new double[0];
                tmpg = new double[0];
                tmp2 = new double[0,0];
            }
        };




        public const int newtonrefinementits = 3;


        /*************************************************************************
        This subroutine is used to initialize CQM. By default, empty NxN model  is
        generated, with Alpha=Lambda=Theta=0.0 and zero b.

        Previously allocated buffer variables are reused as much as possible.

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqminit(int n,
            convexquadraticmodel s)
        {
            int i = 0;

            s.n = n;
            s.k = 0;
            s.nfree = n;
            s.ecakind = -1;
            s.alpha = 0.0;
            s.tau = 0.0;
            s.theta = 0.0;
            s.ismaintermchanged = true;
            s.issecondarytermchanged = true;
            s.islineartermchanged = true;
            s.isactivesetchanged = true;
            apserv.bvectorsetlengthatleast(ref s.activeset, n);
            apserv.rvectorsetlengthatleast(ref s.xc, n);
            apserv.rvectorsetlengthatleast(ref s.eb, n);
            apserv.rvectorsetlengthatleast(ref s.tq1, n);
            apserv.rvectorsetlengthatleast(ref s.txc, n);
            apserv.rvectorsetlengthatleast(ref s.tb, n);
            apserv.rvectorsetlengthatleast(ref s.b, s.n);
            apserv.rvectorsetlengthatleast(ref s.tk1, s.n);
            for(i=0; i<=n-1; i++)
            {
                s.activeset[i] = false;
                s.xc[i] = 0.0;
                s.b[i] = 0.0;
            }
        }


        /*************************************************************************
        This subroutine changes main quadratic term of the model.

        INPUT PARAMETERS:
            S       -   model
            A       -   NxN matrix, only upper or lower triangle is referenced
            IsUpper -   True, when matrix is stored in upper triangle
            Alpha   -   multiplier; when Alpha=0, A is not referenced at all

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmseta(convexquadraticmodel s,
            double[,] a,
            bool isupper,
            double alpha)
        {
            int i = 0;
            int j = 0;
            double v = 0;

            alglib.ap.assert(math.isfinite(alpha) && (double)(alpha)>=(double)(0), "CQMSetA: Alpha<0 or is not finite number");
            alglib.ap.assert((double)(alpha)==(double)(0) || apserv.isfinitertrmatrix(a, s.n, isupper), "CQMSetA: A is not finite NxN matrix");
            s.alpha = alpha;
            if( (double)(alpha)>(double)(0) )
            {
                apserv.rmatrixsetlengthatleast(ref s.a, s.n, s.n);
                apserv.rmatrixsetlengthatleast(ref s.ecadense, s.n, s.n);
                apserv.rmatrixsetlengthatleast(ref s.tq2dense, s.n, s.n);
                for(i=0; i<=s.n-1; i++)
                {
                    for(j=i; j<=s.n-1; j++)
                    {
                        if( isupper )
                        {
                            v = a[i,j];
                        }
                        else
                        {
                            v = a[j,i];
                        }
                        s.a[i,j] = v;
                        s.a[j,i] = v;
                    }
                }
            }
            s.ismaintermchanged = true;
        }


        /*************************************************************************
        This subroutine rewrites diagonal of the main quadratic term of the  model
        (dense  A)  by  vector  Z/Alpha (current value of the Alpha coefficient is
        used).

        IMPORTANT: in  case  model  has  no  dense  quadratic  term, this function
                   allocates N*N dense matrix of zeros, and fills its diagonal  by
                   non-zero values.

        INPUT PARAMETERS:
            S       -   model
            Z       -   new diagonal, array[N]

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmrewritedensediagonal(convexquadraticmodel s,
            double[] z)
        {
            int n = 0;
            int i = 0;
            int j = 0;

            n = s.n;
            if( (double)(s.alpha)==(double)(0) )
            {
                apserv.rmatrixsetlengthatleast(ref s.a, s.n, s.n);
                apserv.rmatrixsetlengthatleast(ref s.ecadense, s.n, s.n);
                apserv.rmatrixsetlengthatleast(ref s.tq2dense, s.n, s.n);
                for(i=0; i<=n-1; i++)
                {
                    for(j=0; j<=n-1; j++)
                    {
                        s.a[i,j] = 0.0;
                    }
                }
                s.alpha = 1.0;
            }
            for(i=0; i<=s.n-1; i++)
            {
                s.a[i,i] = z[i]/s.alpha;
            }
            s.ismaintermchanged = true;
        }


        /*************************************************************************
        This subroutine changes diagonal quadratic term of the model.

        INPUT PARAMETERS:
            S       -   model
            D       -   array[N], semidefinite diagonal matrix
            Tau     -   multiplier; when Tau=0, D is not referenced at all

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmsetd(convexquadraticmodel s,
            double[] d,
            double tau)
        {
            int i = 0;

            alglib.ap.assert(math.isfinite(tau) && (double)(tau)>=(double)(0), "CQMSetD: Tau<0 or is not finite number");
            alglib.ap.assert((double)(tau)==(double)(0) || apserv.isfinitevector(d, s.n), "CQMSetD: D is not finite Nx1 vector");
            s.tau = tau;
            if( (double)(tau)>(double)(0) )
            {
                apserv.rvectorsetlengthatleast(ref s.d, s.n);
                apserv.rvectorsetlengthatleast(ref s.ecadiag, s.n);
                apserv.rvectorsetlengthatleast(ref s.tq2diag, s.n);
                for(i=0; i<=s.n-1; i++)
                {
                    alglib.ap.assert((double)(d[i])>=(double)(0), "CQMSetD: D[i]<0");
                    s.d[i] = d[i];
                }
            }
            s.ismaintermchanged = true;
        }


        /*************************************************************************
        This subroutine drops main quadratic term A from the model. It is same  as
        call  to  CQMSetA()  with  zero  A,   but gives better performance because
        algorithm  knows  that  matrix  is  zero  and  can  optimize    subsequent
        calculations.

        INPUT PARAMETERS:
            S       -   model

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmdropa(convexquadraticmodel s)
        {
            s.alpha = 0.0;
            s.ismaintermchanged = true;
        }


        /*************************************************************************
        This subroutine changes linear term of the model

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmsetb(convexquadraticmodel s,
            double[] b)
        {
            int i = 0;

            alglib.ap.assert(apserv.isfinitevector(b, s.n), "CQMSetB: B is not finite vector");
            apserv.rvectorsetlengthatleast(ref s.b, s.n);
            for(i=0; i<=s.n-1; i++)
            {
                s.b[i] = b[i];
            }
            s.islineartermchanged = true;
        }


        /*************************************************************************
        This subroutine changes linear term of the model

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmsetq(convexquadraticmodel s,
            double[,] q,
            double[] r,
            int k,
            double theta)
        {
            int i = 0;
            int j = 0;

            alglib.ap.assert(k>=0, "CQMSetQ: K<0");
            alglib.ap.assert((k==0 || (double)(theta)==(double)(0)) || apserv.apservisfinitematrix(q, k, s.n), "CQMSetQ: Q is not finite matrix");
            alglib.ap.assert((k==0 || (double)(theta)==(double)(0)) || apserv.isfinitevector(r, k), "CQMSetQ: R is not finite vector");
            alglib.ap.assert(math.isfinite(theta) && (double)(theta)>=(double)(0), "CQMSetQ: Theta<0 or is not finite number");
            
            //
            // degenerate case: K=0 or Theta=0
            //
            if( k==0 || (double)(theta)==(double)(0) )
            {
                s.k = 0;
                s.theta = 0;
                s.issecondarytermchanged = true;
                return;
            }
            
            //
            // General case: both Theta>0 and K>0
            //
            s.k = k;
            s.theta = theta;
            apserv.rmatrixsetlengthatleast(ref s.q, s.k, s.n);
            apserv.rvectorsetlengthatleast(ref s.r, s.k);
            apserv.rmatrixsetlengthatleast(ref s.eq, s.k, s.n);
            apserv.rmatrixsetlengthatleast(ref s.eccm, s.k, s.k);
            apserv.rmatrixsetlengthatleast(ref s.tk2, s.k, s.n);
            for(i=0; i<=s.k-1; i++)
            {
                for(j=0; j<=s.n-1; j++)
                {
                    s.q[i,j] = q[i,j];
                }
                s.r[i] = r[i];
            }
            s.issecondarytermchanged = true;
        }


        /*************************************************************************
        This subroutine changes active set

        INPUT PARAMETERS
            S       -   model
            X       -   array[N], constraint values
            ActiveSet-  array[N], active set. If ActiveSet[I]=True, then I-th
                        variables is constrained to X[I].

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmsetactiveset(convexquadraticmodel s,
            double[] x,
            bool[] activeset)
        {
            int i = 0;

            alglib.ap.assert(alglib.ap.len(x)>=s.n, "CQMSetActiveSet: Length(X)<N");
            alglib.ap.assert(alglib.ap.len(activeset)>=s.n, "CQMSetActiveSet: Length(ActiveSet)<N");
            for(i=0; i<=s.n-1; i++)
            {
                s.isactivesetchanged = s.isactivesetchanged || (s.activeset[i] && !activeset[i]);
                s.isactivesetchanged = s.isactivesetchanged || (activeset[i] && !s.activeset[i]);
                s.activeset[i] = activeset[i];
                if( activeset[i] )
                {
                    alglib.ap.assert(math.isfinite(x[i]), "CQMSetActiveSet: X[] contains infinite constraints");
                    s.isactivesetchanged = s.isactivesetchanged || (double)(s.xc[i])!=(double)(x[i]);
                    s.xc[i] = x[i];
                }
            }
        }


        /*************************************************************************
        This subroutine evaluates model at X. Active constraints are ignored.

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static double cqmeval(convexquadraticmodel s,
            double[] x)
        {
            double result = 0;
            int n = 0;
            int i = 0;
            int j = 0;
            double v = 0;
            int i_ = 0;

            n = s.n;
            alglib.ap.assert(apserv.isfinitevector(x, n), "CQMEval: X is not finite vector");
            result = 0.0;
            
            //
            // main quadratic term
            //
            if( (double)(s.alpha)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    for(j=0; j<=n-1; j++)
                    {
                        result = result+s.alpha*0.5*x[i]*s.a[i,j]*x[j];
                    }
                }
            }
            if( (double)(s.tau)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    result = result+0.5*math.sqr(x[i])*s.tau*s.d[i];
                }
            }
            
            //
            // secondary quadratic term
            //
            if( (double)(s.theta)>(double)(0) )
            {
                for(i=0; i<=s.k-1; i++)
                {
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += s.q[i,i_]*x[i_];
                    }
                    result = result+0.5*s.theta*math.sqr(v-s.r[i]);
                }
            }
            
            //
            // linear term
            //
            for(i=0; i<=s.n-1; i++)
            {
                result = result+x[i]*s.b[i];
            }
            return result;
        }


        /*************************************************************************
        This subroutine evaluates model at X. Active constraints are ignored.
        It returns:
            R   -   model value
            Noise-  estimate of the numerical noise in data

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmevalx(convexquadraticmodel s,
            double[] x,
            ref double r,
            ref double noise)
        {
            int n = 0;
            int i = 0;
            int j = 0;
            double v = 0;
            double v2 = 0;
            double mxq = 0;
            double eps = 0;

            r = 0;
            noise = 0;

            n = s.n;
            alglib.ap.assert(apserv.isfinitevector(x, n), "CQMEval: X is not finite vector");
            r = 0.0;
            noise = 0.0;
            eps = 2*math.machineepsilon;
            mxq = 0.0;
            
            //
            // Main quadratic term.
            //
            // Noise from the main quadratic term is equal to the
            // maximum summand in the term.
            //
            if( (double)(s.alpha)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    for(j=0; j<=n-1; j++)
                    {
                        v = s.alpha*0.5*x[i]*s.a[i,j]*x[j];
                        r = r+v;
                        noise = Math.Max(noise, eps*Math.Abs(v));
                    }
                }
            }
            if( (double)(s.tau)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    v = 0.5*math.sqr(x[i])*s.tau*s.d[i];
                    r = r+v;
                    noise = Math.Max(noise, eps*Math.Abs(v));
                }
            }
            
            //
            // secondary quadratic term
            //
            // Noise from the secondary quadratic term is estimated as follows:
            // * noise in qi*x-r[i] is estimated as
            //   Eps*MXQ = Eps*max(|r[i]|, |q[i,j]*x[j]|)
            // * noise in (qi*x-r[i])^2 is estimated as
            //   NOISE = (|qi*x-r[i]|+Eps*MXQ)^2-(|qi*x-r[i]|)^2
            //         = Eps*MXQ*(2*|qi*x-r[i]|+Eps*MXQ)
            //
            if( (double)(s.theta)>(double)(0) )
            {
                for(i=0; i<=s.k-1; i++)
                {
                    v = 0.0;
                    mxq = Math.Abs(s.r[i]);
                    for(j=0; j<=n-1; j++)
                    {
                        v2 = s.q[i,j]*x[j];
                        v = v+v2;
                        mxq = Math.Max(mxq, Math.Abs(v2));
                    }
                    r = r+0.5*s.theta*math.sqr(v-s.r[i]);
                    noise = Math.Max(noise, eps*mxq*(2*Math.Abs(v-s.r[i])+eps*mxq));
                }
            }
            
            //
            // linear term
            //
            for(i=0; i<=s.n-1; i++)
            {
                r = r+x[i]*s.b[i];
                noise = Math.Max(noise, eps*Math.Abs(x[i]*s.b[i]));
            }
            
            //
            // Final update of the noise
            //
            noise = n*noise;
        }


        /*************************************************************************
        This  subroutine  evaluates  gradient of the model; active constraints are
        ignored.

        INPUT PARAMETERS:
            S       -   convex model
            X       -   point, array[N]
            G       -   possibly preallocated buffer; resized, if too small

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmgradunconstrained(convexquadraticmodel s,
            double[] x,
            ref double[] g)
        {
            int n = 0;
            int i = 0;
            int j = 0;
            double v = 0;
            int i_ = 0;

            n = s.n;
            alglib.ap.assert(apserv.isfinitevector(x, n), "CQMEvalGradUnconstrained: X is not finite vector");
            apserv.rvectorsetlengthatleast(ref g, n);
            for(i=0; i<=n-1; i++)
            {
                g[i] = 0;
            }
            
            //
            // main quadratic term
            //
            if( (double)(s.alpha)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    v = 0.0;
                    for(j=0; j<=n-1; j++)
                    {
                        v = v+s.alpha*s.a[i,j]*x[j];
                    }
                    g[i] = g[i]+v;
                }
            }
            if( (double)(s.tau)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    g[i] = g[i]+x[i]*s.tau*s.d[i];
                }
            }
            
            //
            // secondary quadratic term
            //
            if( (double)(s.theta)>(double)(0) )
            {
                for(i=0; i<=s.k-1; i++)
                {
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += s.q[i,i_]*x[i_];
                    }
                    v = s.theta*(v-s.r[i]);
                    for(i_=0; i_<=n-1;i_++)
                    {
                        g[i_] = g[i_] + v*s.q[i,i_];
                    }
                }
            }
            
            //
            // linear term
            //
            for(i=0; i<=n-1; i++)
            {
                g[i] = g[i]+s.b[i];
            }
        }


        /*************************************************************************
        This subroutine evaluates x'*(0.5*alpha*A+tau*D)*x

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static double cqmxtadx2(convexquadraticmodel s,
            double[] x)
        {
            double result = 0;
            int n = 0;
            int i = 0;
            int j = 0;

            n = s.n;
            alglib.ap.assert(apserv.isfinitevector(x, n), "CQMEval: X is not finite vector");
            result = 0.0;
            
            //
            // main quadratic term
            //
            if( (double)(s.alpha)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    for(j=0; j<=n-1; j++)
                    {
                        result = result+s.alpha*0.5*x[i]*s.a[i,j]*x[j];
                    }
                }
            }
            if( (double)(s.tau)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    result = result+0.5*math.sqr(x[i])*s.tau*s.d[i];
                }
            }
            return result;
        }


        /*************************************************************************
        This subroutine evaluates (0.5*alpha*A+tau*D)*x

        Y is automatically resized if needed

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmadx(convexquadraticmodel s,
            double[] x,
            ref double[] y)
        {
            int n = 0;
            int i = 0;
            double v = 0;
            int i_ = 0;

            n = s.n;
            alglib.ap.assert(apserv.isfinitevector(x, n), "CQMEval: X is not finite vector");
            apserv.rvectorsetlengthatleast(ref y, n);
            
            //
            // main quadratic term
            //
            for(i=0; i<=n-1; i++)
            {
                y[i] = 0;
            }
            if( (double)(s.alpha)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += s.a[i,i_]*x[i_];
                    }
                    y[i] = y[i]+s.alpha*v;
                }
            }
            if( (double)(s.tau)>(double)(0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    y[i] = y[i]+x[i]*s.tau*s.d[i];
                }
            }
        }


        /*************************************************************************
        This subroutine finds optimum of the model. It returns  False  on  failure
        (indefinite/semidefinite matrix).  Optimum  is  found  subject  to  active
        constraints.

        INPUT PARAMETERS
            S       -   model
            X       -   possibly preallocated buffer; automatically resized, if
                        too small enough.

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static bool cqmconstrainedoptimum(convexquadraticmodel s,
            ref double[] x)
        {
            bool result = new bool();
            int n = 0;
            int nfree = 0;
            int k = 0;
            int i = 0;
            double v = 0;
            int cidx0 = 0;
            int itidx = 0;
            int i_ = 0;

            
            //
            // Rebuild internal structures
            //
            if( !cqmrebuild(s) )
            {
                result = false;
                return result;
            }
            n = s.n;
            k = s.k;
            nfree = s.nfree;
            result = true;
            
            //
            // Calculate initial point for the iterative refinement:
            // * free components are set to zero
            // * constrained components are set to their constrained values
            //
            apserv.rvectorsetlengthatleast(ref x, n);
            for(i=0; i<=n-1; i++)
            {
                if( s.activeset[i] )
                {
                    x[i] = s.xc[i];
                }
                else
                {
                    x[i] = 0;
                }
            }
            
            //
            // Iterative refinement.
            //
            // In an ideal world without numerical errors it would be enough
            // to make just one Newton step from initial point:
            //   x_new = -H^(-1)*grad(x=0)
            // However, roundoff errors can significantly deteriorate quality
            // of the solution. So we have to recalculate gradient and to
            // perform Newton steps several times.
            //
            // Below we perform fixed number of Newton iterations.
            //
            for(itidx=0; itidx<=newtonrefinementits-1; itidx++)
            {
                
                //
                // Calculate gradient at the current point.
                // Move free components of the gradient in the beginning.
                //
                cqmgradunconstrained(s, x, ref s.tmpg);
                cidx0 = 0;
                for(i=0; i<=n-1; i++)
                {
                    if( !s.activeset[i] )
                    {
                        s.tmpg[cidx0] = s.tmpg[i];
                        cidx0 = cidx0+1;
                    }
                }
                
                //
                // Free components of the extrema are calculated in the first NFree elements of TXC.
                //
                // First, we have to calculate original Newton step, without rank-K perturbations
                //
                for(i_=0; i_<=nfree-1;i_++)
                {
                    s.txc[i_] = -s.tmpg[i_];
                }
                cqmsolveea(s, ref s.txc, ref s.tmp0);
                
                //
                // Then, we account for rank-K correction.
                // Woodbury matrix identity is used.
                //
                if( s.k>0 && (double)(s.theta)>(double)(0) )
                {
                    apserv.rvectorsetlengthatleast(ref s.tmp0, Math.Max(nfree, k));
                    apserv.rvectorsetlengthatleast(ref s.tmp1, Math.Max(nfree, k));
                    for(i_=0; i_<=nfree-1;i_++)
                    {
                        s.tmp1[i_] = -s.tmpg[i_];
                    }
                    cqmsolveea(s, ref s.tmp1, ref s.tmp0);
                    for(i=0; i<=k-1; i++)
                    {
                        v = 0.0;
                        for(i_=0; i_<=nfree-1;i_++)
                        {
                            v += s.eq[i,i_]*s.tmp1[i_];
                        }
                        s.tmp0[i] = v;
                    }
                    fbls.fblscholeskysolve(s.eccm, 1.0, k, true, ref s.tmp0, ref s.tmp1);
                    for(i=0; i<=nfree-1; i++)
                    {
                        s.tmp1[i] = 0.0;
                    }
                    for(i=0; i<=k-1; i++)
                    {
                        v = s.tmp0[i];
                        for(i_=0; i_<=nfree-1;i_++)
                        {
                            s.tmp1[i_] = s.tmp1[i_] + v*s.eq[i,i_];
                        }
                    }
                    cqmsolveea(s, ref s.tmp1, ref s.tmp0);
                    for(i_=0; i_<=nfree-1;i_++)
                    {
                        s.txc[i_] = s.txc[i_] - s.tmp1[i_];
                    }
                }
                
                //
                // Unpack components from TXC into X. We pass through all
                // free components of X and add our step.
                //
                cidx0 = 0;
                for(i=0; i<=n-1; i++)
                {
                    if( !s.activeset[i] )
                    {
                        x[i] = x[i]+s.txc[cidx0];
                        cidx0 = cidx0+1;
                    }
                }
            }
            return result;
        }


        /*************************************************************************
        This function scales vector  by  multiplying it by inverse of the diagonal
        of the Hessian matrix. It should be used to  accelerate  steepest  descent
        phase of the QP solver.

        Although  it  is  called  "scale-grad",  it  can be called for any vector,
        whether it is gradient, anti-gradient, or just some vector.

        This function does NOT takes into account current set of  constraints,  it
        just performs matrix-vector multiplication  without  taking  into  account
        constraints.

        INPUT PARAMETERS:
            S       -   model
            X       -   vector to scale

        OUTPUT PARAMETERS:
            X       -   scaled vector
            
        NOTE:
            when called for non-SPD matrices, it silently skips components of X
            which correspond to zero or negative diagonal elements.
            
        NOTE:
            this function uses diagonals of A and D; it ignores Q - rank-K term of
            the quadratic model.

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void cqmscalevector(convexquadraticmodel s,
            ref double[] x)
        {
            int n = 0;
            int i = 0;
            double v = 0;

            n = s.n;
            for(i=0; i<=n-1; i++)
            {
                v = 0.0;
                if( (double)(s.alpha)>(double)(0) )
                {
                    v = v+s.a[i,i];
                }
                if( (double)(s.tau)>(double)(0) )
                {
                    v = v+s.d[i];
                }
                if( (double)(v)>(double)(0) )
                {
                    x[i] = x[i]/v;
                }
            }
        }


        /*************************************************************************
        This subroutine calls CQMRebuild() and evaluates model at X subject to
        active constraints.

        It  is  intended  for  debug  purposes only, because it evaluates model by
        means of temporaries, which were calculated  by  CQMRebuild().  The   only
        purpose of this function  is  to  check  correctness  of  CQMRebuild()  by
        comparing results of this function with ones obtained by CQMEval(),  which
        is  used  as  reference  point. The  idea is that significant deviation in
        results  of  these  two  functions  is  evidence  of  some  error  in  the
        CQMRebuild().

        NOTE: suffix T denotes that temporaries marked by T-prefix are used. There
              is one more variant of this function, which uses  "effective"  model
              built by CQMRebuild().

        NOTE2: in case CQMRebuild() fails (due to model non-convexity), this
              function returns NAN.

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static double cqmdebugconstrainedevalt(convexquadraticmodel s,
            double[] x)
        {
            double result = 0;
            int n = 0;
            int nfree = 0;
            int i = 0;
            int j = 0;
            double v = 0;

            n = s.n;
            alglib.ap.assert(apserv.isfinitevector(x, n), "CQMDebugConstrainedEvalT: X is not finite vector");
            if( !cqmrebuild(s) )
            {
                result = Double.NaN;
                return result;
            }
            result = 0.0;
            nfree = s.nfree;
            
            //
            // Reorder variables
            //
            j = 0;
            for(i=0; i<=n-1; i++)
            {
                if( !s.activeset[i] )
                {
                    alglib.ap.assert(j<nfree, "CQMDebugConstrainedEvalT: internal error");
                    s.txc[j] = x[i];
                    j = j+1;
                }
            }
            
            //
            // TQ2, TQ1, TQ0
            //
            //
            if( (double)(s.alpha)>(double)(0) )
            {
                
                //
                // Dense TQ2
                //
                for(i=0; i<=nfree-1; i++)
                {
                    for(j=0; j<=nfree-1; j++)
                    {
                        result = result+0.5*s.txc[i]*s.tq2dense[i,j]*s.txc[j];
                    }
                }
            }
            else
            {
                
                //
                // Diagonal TQ2
                //
                for(i=0; i<=nfree-1; i++)
                {
                    result = result+0.5*s.tq2diag[i]*math.sqr(s.txc[i]);
                }
            }
            for(i=0; i<=nfree-1; i++)
            {
                result = result+s.tq1[i]*s.txc[i];
            }
            result = result+s.tq0;
            
            //
            // TK2, TK1, TK0
            //
            if( s.k>0 && (double)(s.theta)>(double)(0) )
            {
                for(i=0; i<=s.k-1; i++)
                {
                    v = 0;
                    for(j=0; j<=nfree-1; j++)
                    {
                        v = v+s.tk2[i,j]*s.txc[j];
                    }
                    result = result+0.5*math.sqr(v);
                }
                for(i=0; i<=nfree-1; i++)
                {
                    result = result+s.tk1[i]*s.txc[i];
                }
                result = result+s.tk0;
            }
            
            //
            // TB (Bf and Bc parts)
            //
            for(i=0; i<=n-1; i++)
            {
                result = result+s.tb[i]*s.txc[i];
            }
            return result;
        }


        /*************************************************************************
        This subroutine calls CQMRebuild() and evaluates model at X subject to
        active constraints.

        It  is  intended  for  debug  purposes only, because it evaluates model by
        means of "effective" matrices built by CQMRebuild(). The only  purpose  of
        this function is to check correctness of CQMRebuild() by comparing results
        of this function with  ones  obtained  by  CQMEval(),  which  is  used  as
        reference  point.  The  idea  is  that significant deviation in results of
        these two functions is evidence of some error in the CQMRebuild().

        NOTE: suffix E denotes that effective matrices. There is one more  variant
              of this function, which uses temporary matrices built by
              CQMRebuild().

        NOTE2: in case CQMRebuild() fails (due to model non-convexity), this
              function returns NAN.

          -- ALGLIB --
             Copyright 12.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static double cqmdebugconstrainedevale(convexquadraticmodel s,
            double[] x)
        {
            double result = 0;
            int n = 0;
            int nfree = 0;
            int i = 0;
            int j = 0;
            double v = 0;

            n = s.n;
            alglib.ap.assert(apserv.isfinitevector(x, n), "CQMDebugConstrainedEvalE: X is not finite vector");
            if( !cqmrebuild(s) )
            {
                result = Double.NaN;
                return result;
            }
            result = 0.0;
            nfree = s.nfree;
            
            //
            // Reorder variables
            //
            j = 0;
            for(i=0; i<=n-1; i++)
            {
                if( !s.activeset[i] )
                {
                    alglib.ap.assert(j<nfree, "CQMDebugConstrainedEvalE: internal error");
                    s.txc[j] = x[i];
                    j = j+1;
                }
            }
            
            //
            // ECA
            //
            alglib.ap.assert((s.ecakind==0 || s.ecakind==1) || (s.ecakind==-1 && nfree==0), "CQMDebugConstrainedEvalE: unexpected ECAKind");
            if( s.ecakind==0 )
            {
                
                //
                // Dense ECA
                //
                for(i=0; i<=nfree-1; i++)
                {
                    v = 0.0;
                    for(j=i; j<=nfree-1; j++)
                    {
                        v = v+s.ecadense[i,j]*s.txc[j];
                    }
                    result = result+0.5*math.sqr(v);
                }
            }
            if( s.ecakind==1 )
            {
                
                //
                // Diagonal ECA
                //
                for(i=0; i<=nfree-1; i++)
                {
                    result = result+0.5*math.sqr(s.ecadiag[i]*s.txc[i]);
                }
            }
            
            //
            // EQ
            //
            for(i=0; i<=s.k-1; i++)
            {
                v = 0.0;
                for(j=0; j<=nfree-1; j++)
                {
                    v = v+s.eq[i,j]*s.txc[j];
                }
                result = result+0.5*math.sqr(v);
            }
            
            //
            // EB
            //
            for(i=0; i<=nfree-1; i++)
            {
                result = result+s.eb[i]*s.txc[i];
            }
            
            //
            // EC
            //
            result = result+s.ec;
            return result;
        }


        /*************************************************************************
        Internal function, rebuilds "effective" model subject to constraints.
        Returns False on failure (non-SPD main quadratic term)

          -- ALGLIB --
             Copyright 10.05.2011 by Bochkanov Sergey
        *************************************************************************/
        private static bool cqmrebuild(convexquadraticmodel s)
        {
            bool result = new bool();
            int n = 0;
            int nfree = 0;
            int k = 0;
            int i = 0;
            int j = 0;
            int ridx0 = 0;
            int ridx1 = 0;
            int cidx0 = 0;
            int cidx1 = 0;
            double v = 0;
            int i_ = 0;

            if( (double)(s.alpha)==(double)(0) && (double)(s.tau)==(double)(0) )
            {
                
                //
                // Non-SPD model, quick exit
                //
                result = false;
                return result;
            }
            result = true;
            n = s.n;
            k = s.k;
            
            //
            // Determine number of free variables.
            // Fill TXC - array whose last N-NFree elements store constraints.
            //
            if( s.isactivesetchanged )
            {
                s.nfree = 0;
                for(i=0; i<=n-1; i++)
                {
                    if( !s.activeset[i] )
                    {
                        s.nfree = s.nfree+1;
                    }
                }
                j = s.nfree;
                for(i=0; i<=n-1; i++)
                {
                    if( s.activeset[i] )
                    {
                        s.txc[j] = s.xc[i];
                        j = j+1;
                    }
                }
            }
            nfree = s.nfree;
            
            //
            // Re-evaluate TQ2/TQ1/TQ0, if needed
            //
            if( s.isactivesetchanged || s.ismaintermchanged )
            {
                
                //
                // Handle cases Alpha>0 and Alpha=0 separately:
                // * in the first case we have dense matrix
                // * in the second one we have diagonal matrix, which can be
                //   handled more efficiently
                //
                if( (double)(s.alpha)>(double)(0) )
                {
                    
                    //
                    // Alpha>0, dense QP
                    //
                    // Split variables into two groups - free (F) and constrained (C). Reorder
                    // variables in such way that free vars come first, constrained are last:
                    // x = [xf, xc].
                    // 
                    // Main quadratic term x'*(alpha*A+tau*D)*x now splits into quadratic part,
                    // linear part and constant part:
                    //                   ( alpha*Aff+tau*Df  alpha*Afc        ) ( xf )              
                    //   0.5*( xf' xc' )*(                                    )*(    ) =
                    //                   ( alpha*Acf         alpha*Acc+tau*Dc ) ( xc )
                    //
                    //   = 0.5*xf'*(alpha*Aff+tau*Df)*xf + (alpha*Afc*xc)'*xf + 0.5*xc'(alpha*Acc+tau*Dc)*xc
                    //                    
                    // We store these parts into temporary variables:
                    // * alpha*Aff+tau*Df, alpha*Afc, alpha*Acc+tau*Dc are stored into upper
                    //   triangle of TQ2
                    // * alpha*Afc*xc is stored into TQ1
                    // * 0.5*xc'(alpha*Acc+tau*Dc)*xc is stored into TQ0
                    //
                    // Below comes first part of the work - generation of TQ2:
                    // * we pass through rows of A and copy I-th row into upper block (Aff/Afc) or
                    //   lower one (Acf/Acc) of TQ2, depending on presence of X[i] in the active set.
                    //   RIdx0 variable contains current position for insertion into upper block,
                    //   RIdx1 contains current position for insertion into lower one.
                    // * within each row, we copy J-th element into left half (Aff/Acf) or right
                    //   one (Afc/Acc), depending on presence of X[j] in the active set. CIdx0
                    //   contains current position for insertion into left block, CIdx1 contains
                    //   position for insertion into right one.
                    // * during copying, we multiply elements by alpha and add diagonal matrix D.
                    //
                    ridx0 = 0;
                    ridx1 = s.nfree;
                    for(i=0; i<=n-1; i++)
                    {
                        cidx0 = 0;
                        cidx1 = s.nfree;
                        for(j=0; j<=n-1; j++)
                        {
                            if( !s.activeset[i] && !s.activeset[j] )
                            {
                                
                                //
                                // Element belongs to Aff
                                //
                                v = s.alpha*s.a[i,j];
                                if( i==j && (double)(s.tau)>(double)(0) )
                                {
                                    v = v+s.tau*s.d[i];
                                }
                                s.tq2dense[ridx0,cidx0] = v;
                            }
                            if( !s.activeset[i] && s.activeset[j] )
                            {
                                
                                //
                                // Element belongs to Afc
                                //
                                s.tq2dense[ridx0,cidx1] = s.alpha*s.a[i,j];
                            }
                            if( s.activeset[i] && !s.activeset[j] )
                            {
                                
                                //
                                // Element belongs to Acf
                                //
                                s.tq2dense[ridx1,cidx0] = s.alpha*s.a[i,j];
                            }
                            if( s.activeset[i] && s.activeset[j] )
                            {
                                
                                //
                                // Element belongs to Acc
                                //
                                v = s.alpha*s.a[i,j];
                                if( i==j && (double)(s.tau)>(double)(0) )
                                {
                                    v = v+s.tau*s.d[i];
                                }
                                s.tq2dense[ridx1,cidx1] = v;
                            }
                            if( s.activeset[j] )
                            {
                                cidx1 = cidx1+1;
                            }
                            else
                            {
                                cidx0 = cidx0+1;
                            }
                        }
                        if( s.activeset[i] )
                        {
                            ridx1 = ridx1+1;
                        }
                        else
                        {
                            ridx0 = ridx0+1;
                        }
                    }
                    
                    //
                    // Now we have TQ2, and we can evaluate TQ1.
                    // In the special case when we have Alpha=0, NFree=0 or NFree=N,
                    // TQ1 is filled by zeros.
                    //
                    for(i=0; i<=n-1; i++)
                    {
                        s.tq1[i] = 0.0;
                    }
                    if( s.nfree>0 && s.nfree<n )
                    {
                        ablas.rmatrixmv(s.nfree, n-s.nfree, s.tq2dense, 0, s.nfree, 0, s.txc, s.nfree, ref s.tq1, 0);
                    }
                    
                    //
                    // And finally, we evaluate TQ0.
                    //
                    v = 0.0;
                    for(i=s.nfree; i<=n-1; i++)
                    {
                        for(j=s.nfree; j<=n-1; j++)
                        {
                            v = v+0.5*s.txc[i]*s.tq2dense[i,j]*s.txc[j];
                        }
                    }
                    s.tq0 = v;
                }
                else
                {
                    
                    //
                    // Alpha=0, diagonal QP
                    //
                    // Split variables into two groups - free (F) and constrained (C). Reorder
                    // variables in such way that free vars come first, constrained are last:
                    // x = [xf, xc].
                    // 
                    // Main quadratic term x'*(tau*D)*x now splits into quadratic and constant
                    // parts:
                    //                   ( tau*Df        ) ( xf )              
                    //   0.5*( xf' xc' )*(               )*(    ) =
                    //                   (        tau*Dc ) ( xc )
                    //
                    //   = 0.5*xf'*(tau*Df)*xf + 0.5*xc'(tau*Dc)*xc
                    //                    
                    // We store these parts into temporary variables:
                    // * tau*Df is stored in TQ2Diag
                    // * 0.5*xc'(tau*Dc)*xc is stored into TQ0
                    //
                    s.tq0 = 0.0;
                    ridx0 = 0;
                    for(i=0; i<=n-1; i++)
                    {
                        if( !s.activeset[i] )
                        {
                            s.tq2diag[ridx0] = s.tau*s.d[i];
                            ridx0 = ridx0+1;
                        }
                        else
                        {
                            s.tq0 = s.tq0+0.5*s.tau*s.d[i]*math.sqr(s.xc[i]);
                        }
                    }
                    for(i=0; i<=n-1; i++)
                    {
                        s.tq1[i] = 0.0;
                    }
                }
            }
            
            //
            // Re-evaluate TK2/TK1/TK0, if needed
            //
            if( s.isactivesetchanged || s.issecondarytermchanged )
            {
                
                //
                // Split variables into two groups - free (F) and constrained (C). Reorder
                // variables in such way that free vars come first, constrained are last:
                // x = [xf, xc].
                // 
                // Secondary term theta*(Q*x-r)'*(Q*x-r) now splits into quadratic part,
                // linear part and constant part:
                //             (          ( xf )     )'  (          ( xf )     )
                //   0.5*theta*( (Qf Qc)'*(    ) - r ) * ( (Qf Qc)'*(    ) - r ) =
                //             (          ( xc )     )   (          ( xc )     )
                //
                //   = 0.5*theta*xf'*(Qf'*Qf)*xf + theta*((Qc*xc-r)'*Qf)*xf + 
                //     + theta*(-r'*(Qc*xc-r)-0.5*r'*r+0.5*xc'*Qc'*Qc*xc)
                //                    
                // We store these parts into temporary variables:
                // * sqrt(theta)*Qf is stored into TK2
                // * theta*((Qc*xc-r)'*Qf) is stored into TK1
                // * theta*(-r'*(Qc*xc-r)-0.5*r'*r+0.5*xc'*Qc'*Qc*xc) is stored into TK0
                //
                // We use several other temporaries to store intermediate results:
                // * Tmp0 - to store Qc*xc-r
                // * Tmp1 - to store Qc*xc
                //
                // Generation of TK2/TK1/TK0 is performed as follows:
                // * we fill TK2/TK1/TK0 (to handle K=0 or Theta=0)
                // * other steps are performed only for K>0 and Theta>0
                // * we pass through columns of Q and copy I-th column into left block (Qf) or
                //   right one (Qc) of TK2, depending on presence of X[i] in the active set.
                //   CIdx0 variable contains current position for insertion into upper block,
                //   CIdx1 contains current position for insertion into lower one.
                // * we calculate Qc*xc-r and store it into Tmp0
                // * we calculate TK0 and TK1
                // * we multiply leading part of TK2 which stores Qf by sqrt(theta)
                //   it is important to perform this step AFTER calculation of TK0 and TK1,
                //   because we need original (non-modified) Qf to calculate TK0 and TK1.
                //
                for(j=0; j<=n-1; j++)
                {
                    for(i=0; i<=k-1; i++)
                    {
                        s.tk2[i,j] = 0.0;
                    }
                    s.tk1[j] = 0.0;
                }
                s.tk0 = 0.0;
                if( s.k>0 && (double)(s.theta)>(double)(0) )
                {
                    
                    //
                    // Split Q into Qf and Qc
                    // Calculate Qc*xc-r, store in Tmp0
                    //
                    apserv.rvectorsetlengthatleast(ref s.tmp0, k);
                    apserv.rvectorsetlengthatleast(ref s.tmp1, k);
                    cidx0 = 0;
                    cidx1 = nfree;
                    for(i=0; i<=k-1; i++)
                    {
                        s.tmp1[i] = 0.0;
                    }
                    for(j=0; j<=n-1; j++)
                    {
                        if( s.activeset[j] )
                        {
                            for(i=0; i<=k-1; i++)
                            {
                                s.tk2[i,cidx1] = s.q[i,j];
                                s.tmp1[i] = s.tmp1[i]+s.q[i,j]*s.txc[cidx1];
                            }
                            cidx1 = cidx1+1;
                        }
                        else
                        {
                            for(i=0; i<=k-1; i++)
                            {
                                s.tk2[i,cidx0] = s.q[i,j];
                            }
                            cidx0 = cidx0+1;
                        }
                    }
                    for(i=0; i<=k-1; i++)
                    {
                        s.tmp0[i] = s.tmp1[i]-s.r[i];
                    }
                    
                    //
                    // Calculate TK0
                    //
                    v = 0.0;
                    for(i=0; i<=k-1; i++)
                    {
                        v = v+s.theta*(0.5*math.sqr(s.tmp1[i])-s.r[i]*s.tmp0[i]-0.5*math.sqr(s.r[i]));
                    }
                    s.tk0 = v;
                    
                    //
                    // Calculate TK1
                    //
                    if( nfree>0 )
                    {
                        for(i=0; i<=k-1; i++)
                        {
                            v = s.theta*s.tmp0[i];
                            for(i_=0; i_<=nfree-1;i_++)
                            {
                                s.tk1[i_] = s.tk1[i_] + v*s.tk2[i,i_];
                            }
                        }
                    }
                    
                    //
                    // Calculate TK2
                    //
                    if( nfree>0 )
                    {
                        v = Math.Sqrt(s.theta);
                        for(i=0; i<=k-1; i++)
                        {
                            for(i_=0; i_<=nfree-1;i_++)
                            {
                                s.tk2[i,i_] = v*s.tk2[i,i_];
                            }
                        }
                    }
                }
            }
            
            //
            // Re-evaluate TB
            //
            if( s.isactivesetchanged || s.islineartermchanged )
            {
                ridx0 = 0;
                ridx1 = nfree;
                for(i=0; i<=n-1; i++)
                {
                    if( s.activeset[i] )
                    {
                        s.tb[ridx1] = s.b[i];
                        ridx1 = ridx1+1;
                    }
                    else
                    {
                        s.tb[ridx0] = s.b[i];
                        ridx0 = ridx0+1;
                    }
                }
            }
            
            //
            // Compose ECA: either dense ECA or diagonal ECA
            //
            if( (s.isactivesetchanged || s.ismaintermchanged) && nfree>0 )
            {
                if( (double)(s.alpha)>(double)(0) )
                {
                    
                    //
                    // Dense ECA
                    //
                    s.ecakind = 0;
                    for(i=0; i<=nfree-1; i++)
                    {
                        for(j=i; j<=nfree-1; j++)
                        {
                            s.ecadense[i,j] = s.tq2dense[i,j];
                        }
                    }
                    if( !trfac.spdmatrixcholeskyrec(ref s.ecadense, 0, nfree, true, ref s.tmp0) )
                    {
                        result = false;
                        return result;
                    }
                }
                else
                {
                    
                    //
                    // Diagonal ECA
                    //
                    s.ecakind = 1;
                    for(i=0; i<=nfree-1; i++)
                    {
                        if( (double)(s.tq2diag[i])<(double)(0) )
                        {
                            result = false;
                            return result;
                        }
                        s.ecadiag[i] = Math.Sqrt(s.tq2diag[i]);
                    }
                }
            }
            
            //
            // Compose EQ
            //
            if( s.isactivesetchanged || s.issecondarytermchanged )
            {
                for(i=0; i<=k-1; i++)
                {
                    for(j=0; j<=nfree-1; j++)
                    {
                        s.eq[i,j] = s.tk2[i,j];
                    }
                }
            }
            
            //
            // Calculate ECCM
            //
            if( ((((s.isactivesetchanged || s.ismaintermchanged) || s.issecondarytermchanged) && s.k>0) && (double)(s.theta)>(double)(0)) && nfree>0 )
            {
                
                //
                // Calculate ECCM - Cholesky factor of the "effective" capacitance
                // matrix CM = I + EQ*inv(EffectiveA)*EQ'.
                //
                // We calculate CM as follows:
                //   CM = I + EQ*inv(EffectiveA)*EQ'
                //      = I + EQ*ECA^(-1)*ECA^(-T)*EQ'
                //      = I + (EQ*ECA^(-1))*(EQ*ECA^(-1))'
                //
                // Then we perform Cholesky decomposition of CM.
                //
                apserv.rmatrixsetlengthatleast(ref s.tmp2, k, n);
                ablas.rmatrixcopy(k, nfree, s.eq, 0, 0, ref s.tmp2, 0, 0);
                alglib.ap.assert(s.ecakind==0 || s.ecakind==1, "CQMRebuild: unexpected ECAKind");
                if( s.ecakind==0 )
                {
                    ablas.rmatrixrighttrsm(k, nfree, s.ecadense, 0, 0, true, false, 0, ref s.tmp2, 0, 0);
                }
                if( s.ecakind==1 )
                {
                    for(i=0; i<=k-1; i++)
                    {
                        for(j=0; j<=nfree-1; j++)
                        {
                            s.tmp2[i,j] = s.tmp2[i,j]/s.ecadiag[j];
                        }
                    }
                }
                for(i=0; i<=k-1; i++)
                {
                    for(j=0; j<=k-1; j++)
                    {
                        s.eccm[i,j] = 0.0;
                    }
                    s.eccm[i,i] = 1.0;
                }
                ablas.rmatrixsyrk(k, nfree, 1.0, s.tmp2, 0, 0, 0, 1.0, ref s.eccm, 0, 0, true);
                if( !trfac.spdmatrixcholeskyrec(ref s.eccm, 0, k, true, ref s.tmp0) )
                {
                    result = false;
                    return result;
                }
            }
            
            //
            // Compose EB and EC
            //
            // NOTE: because these quantities are cheap to compute, we do not
            // use caching here.
            //
            for(i=0; i<=nfree-1; i++)
            {
                s.eb[i] = s.tq1[i]+s.tk1[i]+s.tb[i];
            }
            s.ec = s.tq0+s.tk0;
            for(i=nfree; i<=n-1; i++)
            {
                s.ec = s.ec+s.tb[i]*s.txc[i];
            }
            
            //
            // Change cache status - everything is cached 
            //
            s.ismaintermchanged = false;
            s.issecondarytermchanged = false;
            s.islineartermchanged = false;
            s.isactivesetchanged = false;
            return result;
        }


        /*************************************************************************
        Internal function, solves system Effective_A*x = b.
        It should be called after successful completion of CQMRebuild().

        INPUT PARAMETERS:
            S       -   quadratic model, after call to CQMRebuild()
            X       -   right part B, array[S.NFree]
            Tmp     -   temporary array, automatically reallocated if needed

        OUTPUT PARAMETERS:
            X       -   solution, array[S.NFree]
            
        NOTE: when called with zero S.NFree, returns silently
        NOTE: this function assumes that EA is non-degenerate

          -- ALGLIB --
             Copyright 10.05.2011 by Bochkanov Sergey
        *************************************************************************/
        private static void cqmsolveea(convexquadraticmodel s,
            ref double[] x,
            ref double[] tmp)
        {
            int i = 0;

            alglib.ap.assert((s.ecakind==0 || s.ecakind==1) || (s.ecakind==-1 && s.nfree==0), "CQMSolveEA: unexpected ECAKind");
            if( s.ecakind==0 )
            {
                
                //
                // Dense ECA, use FBLSCholeskySolve() dense solver.
                //
                fbls.fblscholeskysolve(s.ecadense, 1.0, s.nfree, true, ref x, ref tmp);
            }
            if( s.ecakind==1 )
            {
                
                //
                // Diagonal ECA
                //
                for(i=0; i<=s.nfree-1; i++)
                {
                    x[i] = x[i]/math.sqr(s.ecadiag[i]);
                }
            }
        }


    }
    public class minqp
    {
        /*************************************************************************
        This object stores nonlinear optimizer state.
        You should use functions provided by MinQP subpackage to work with this
        object
        *************************************************************************/
        public class minqpstate
        {
            public int n;
            public int algokind;
            public cqmodels.convexquadraticmodel a;
            public double anorm;
            public double[] b;
            public double[] bndl;
            public double[] bndu;
            public bool[] havebndl;
            public bool[] havebndu;
            public double[] xorigin;
            public double[] startx;
            public bool havex;
            public double[,] cmatrix;
            public double[] cr;
            public int[] ct;
            public int ccount;
            public double[,] cleic;
            public int nec;
            public int nic;
            public double[] xc;
            public double[] gc;
            public double[] xn;
            public double[] pg;
            public bool[] activelin;
            public bool[] activeb;
            public double[,] activecm;
            public double[] activecr;
            public int activelincnt;
            public double[] workbndl;
            public double[] workbndu;
            public double[,] workcleic;
            public int repinneriterationscount;
            public int repouteriterationscount;
            public int repncholesky;
            public int repnmv;
            public int repterminationtype;
            public double[] tmp0;
            public double[] tmp1;
            public double[] tmpfeas;
            public double[,] tmpm0;
            public double[,] lstransform;
            public double[,] lagrangesystem;
            public double[,] lagrangesystemtmp;
            public bool[] prevactiveb;
            public bool[] prevactivelin;
            public double[] lsrightpart;
            public double[] lsrightparttmp;
            public double[] lagrangecoeffs;
            public double[] lstmp0;
            public double[] lstmp1;
            public normestimator.normestimatorstate estimator;
            public minqpstate()
            {
                a = new cqmodels.convexquadraticmodel();
                b = new double[0];
                bndl = new double[0];
                bndu = new double[0];
                havebndl = new bool[0];
                havebndu = new bool[0];
                xorigin = new double[0];
                startx = new double[0];
                cmatrix = new double[0,0];
                cr = new double[0];
                ct = new int[0];
                cleic = new double[0,0];
                xc = new double[0];
                gc = new double[0];
                xn = new double[0];
                pg = new double[0];
                activelin = new bool[0];
                activeb = new bool[0];
                activecm = new double[0,0];
                activecr = new double[0];
                workbndl = new double[0];
                workbndu = new double[0];
                workcleic = new double[0,0];
                tmp0 = new double[0];
                tmp1 = new double[0];
                tmpfeas = new double[0];
                tmpm0 = new double[0,0];
                lstransform = new double[0,0];
                lagrangesystem = new double[0,0];
                lagrangesystemtmp = new double[0,0];
                prevactiveb = new bool[0];
                prevactivelin = new bool[0];
                lsrightpart = new double[0];
                lsrightparttmp = new double[0];
                lagrangecoeffs = new double[0];
                lstmp0 = new double[0];
                lstmp1 = new double[0];
                estimator = new normestimator.normestimatorstate();
            }
        };


        /*************************************************************************
        This structure stores optimization report:
        * InnerIterationsCount      number of inner iterations
        * OuterIterationsCount      number of outer iterations
        * NCholesky                 number of Cholesky decomposition
        * NMV                       number of matrix-vector products
                                    (only products calculated as part of iterative
                                    process are counted)
        * TerminationType           completion code (see below)

        Completion codes:
        * -5    inappropriate solver was used:
                * Cholesky solver for semidefinite or indefinite problems
                * Cholesky solver for problems with non-boundary constraints
        * -3    inconsistent constraints (or, maybe, feasible point is
                too hard to find). If you are sure that constraints are feasible,
                try to restart optimizer with better initial approximation.
        * -1    solver error
        *  4    successful completion
        *  5    MaxIts steps was taken
        *  7    stopping conditions are too stringent,
                further improvement is impossible,
                X contains best point found so far.
        *************************************************************************/
        public class minqpreport
        {
            public int inneriterationscount;
            public int outeriterationscount;
            public int nmv;
            public int ncholesky;
            public int terminationtype;
        };




        public const int badqpitsbeforetermination = 5;
        public const int maxlagrangeits = 10;


        /*************************************************************************
                            CONSTRAINED QUADRATIC PROGRAMMING

        The subroutine creates QP optimizer. After initial creation,  it  contains
        default optimization problem with zero quadratic and linear terms  and  no
        constraints. You should set quadratic/linear terms with calls to functions
        provided by MinQP subpackage.

        INPUT PARAMETERS:
            N       -   problem size
            
        OUTPUT PARAMETERS:
            State   -   optimizer with zero quadratic/linear terms
                        and no constraints

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpcreate(int n,
            minqpstate state)
        {
            int i = 0;

            alglib.ap.assert(n>=1, "MinQPCreate: N<1");
            
            //
            // initialize QP solver
            //
            state.n = n;
            state.ccount = 0;
            state.nec = 0;
            state.nic = 0;
            state.activelincnt = 0;
            state.repterminationtype = 0;
            state.anorm = 1;
            cqmodels.cqminit(n, state.a);
            state.b = new double[n];
            state.bndl = new double[n];
            state.bndu = new double[n];
            state.workbndl = new double[n];
            state.workbndu = new double[n];
            state.havebndl = new bool[n];
            state.havebndu = new bool[n];
            state.startx = new double[n];
            state.xorigin = new double[n];
            state.xc = new double[n];
            state.xn = new double[n];
            state.gc = new double[n];
            state.pg = new double[n];
            state.activeb = new bool[n];
            for(i=0; i<=n-1; i++)
            {
                state.bndl[i] = Double.NegativeInfinity;
                state.bndu[i] = Double.PositiveInfinity;
                state.havebndl[i] = false;
                state.havebndu[i] = false;
                state.b[i] = 0.0;
                state.startx[i] = 0.0;
                state.xorigin[i] = 0.0;
            }
            state.havex = false;
            minqpsetalgocholesky(state);
            normestimator.normestimatorcreate(n, n, 5, 5, state.estimator);
        }


        /*************************************************************************
        This function sets linear term for QP solver.

        By default, linear term is zero.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            B       -   linear term, array[N].

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetlinearterm(minqpstate state,
            double[] b)
        {
            int n = 0;

            n = state.n;
            alglib.ap.assert(alglib.ap.len(b)>=n, "MinQPSetLinearTerm: Length(B)<N");
            alglib.ap.assert(apserv.isfinitevector(b, n), "MinQPSetLinearTerm: B contains infinite or NaN elements");
            minqpsetlineartermfast(state, b);
        }


        /*************************************************************************
        This function sets quadratic term for QP solver.

        By default quadratic term is zero.

        IMPORTANT: this solver minimizes following  function:
            f(x) = 0.5*x'*A*x + b'*x.
        Note that quadratic term has 0.5 before it. So if  you  want  to  minimize
            f(x) = x^2 + x
        you should rewrite your problem as follows:
            f(x) = 0.5*(2*x^2) + x
        and your matrix A will be equal to [[2.0]], not to [[1.0]]

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            A       -   matrix, array[N,N]
            IsUpper -   (optional) storage type:
                        * if True, symmetric matrix  A  is  given  by  its  upper
                          triangle, and the lower triangle isn’t used
                        * if False, symmetric matrix  A  is  given  by  its lower
                          triangle, and the upper triangle isn’t used
                        * if not given, both lower and upper  triangles  must  be
                          filled.

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetquadraticterm(minqpstate state,
            double[,] a,
            bool isupper)
        {
            int n = 0;

            n = state.n;
            alglib.ap.assert(alglib.ap.rows(a)>=n, "MinQPSetQuadraticTerm: Rows(A)<N");
            alglib.ap.assert(alglib.ap.cols(a)>=n, "MinQPSetQuadraticTerm: Cols(A)<N");
            alglib.ap.assert(apserv.isfinitertrmatrix(a, n, isupper), "MinQPSetQuadraticTerm: A contains infinite or NaN elements");
            minqpsetquadratictermfast(state, a, isupper, 0.0);
        }


        /*************************************************************************
        This function sets starting point for QP solver. It is useful to have
        good initial approximation to the solution, because it will increase
        speed of convergence and identification of active constraints.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            X       -   starting point, array[N].

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetstartingpoint(minqpstate state,
            double[] x)
        {
            int n = 0;

            n = state.n;
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinQPSetStartingPoint: Length(B)<N");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinQPSetStartingPoint: X contains infinite or NaN elements");
            minqpsetstartingpointfast(state, x);
        }


        /*************************************************************************
        This  function sets origin for QP solver. By default, following QP program
        is solved:

            min(0.5*x'*A*x+b'*x)
            
        This function allows to solve different problem:

            min(0.5*(x-x_origin)'*A*(x-x_origin)+b'*(x-x_origin))
            
        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            XOrigin -   origin, array[N].

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetorigin(minqpstate state,
            double[] xorigin)
        {
            int n = 0;

            n = state.n;
            alglib.ap.assert(alglib.ap.len(xorigin)>=n, "MinQPSetOrigin: Length(B)<N");
            alglib.ap.assert(apserv.isfinitevector(xorigin, n), "MinQPSetOrigin: B contains infinite or NaN elements");
            minqpsetoriginfast(state, xorigin);
        }


        /*************************************************************************
        This function tells solver to use Cholesky-based algorithm.

        Cholesky-based algorithm can be used when:
        * problem is convex
        * there is no constraints or only boundary constraints are present

        This algorithm has O(N^3) complexity for unconstrained problem and  is  up
        to several times slower on bound constrained  problems  (these  additional
        iterations are needed to identify active constraints).

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetalgocholesky(minqpstate state)
        {
            state.algokind = 1;
        }


        /*************************************************************************
        This function sets boundary constraints for QP solver

        Boundary constraints are inactive by default (after initial creation).
        After  being  set,  they  are  preserved  until explicitly turned off with
        another SetBC() call.

        INPUT PARAMETERS:
            State   -   structure stores algorithm state
            BndL    -   lower bounds, array[N].
                        If some (all) variables are unbounded, you may specify
                        very small number or -INF (latter is recommended because
                        it will allow solver to use better algorithm).
            BndU    -   upper bounds, array[N].
                        If some (all) variables are unbounded, you may specify
                        very large number or +INF (latter is recommended because
                        it will allow solver to use better algorithm).
                        
        NOTE: it is possible to specify BndL[i]=BndU[i]. In this case I-th
        variable will be "frozen" at X[i]=BndL[i]=BndU[i].

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetbc(minqpstate state,
            double[] bndl,
            double[] bndu)
        {
            int i = 0;
            int n = 0;

            n = state.n;
            alglib.ap.assert(alglib.ap.len(bndl)>=n, "MinQPSetBC: Length(BndL)<N");
            alglib.ap.assert(alglib.ap.len(bndu)>=n, "MinQPSetBC: Length(BndU)<N");
            for(i=0; i<=n-1; i++)
            {
                alglib.ap.assert(math.isfinite(bndl[i]) || Double.IsNegativeInfinity(bndl[i]), "MinQPSetBC: BndL contains NAN or +INF");
                alglib.ap.assert(math.isfinite(bndu[i]) || Double.IsPositiveInfinity(bndu[i]), "MinQPSetBC: BndU contains NAN or -INF");
                state.bndl[i] = bndl[i];
                state.havebndl[i] = math.isfinite(bndl[i]);
                state.bndu[i] = bndu[i];
                state.havebndu[i] = math.isfinite(bndu[i]);
            }
        }


        /*************************************************************************
        This function sets linear constraints for QP optimizer.

        Linear constraints are inactive by default (after initial creation).

        INPUT PARAMETERS:
            State   -   structure previously allocated with MinQPCreate call.
            C       -   linear constraints, array[K,N+1].
                        Each row of C represents one constraint, either equality
                        or inequality (see below):
                        * first N elements correspond to coefficients,
                        * last element corresponds to the right part.
                        All elements of C (including right part) must be finite.
            CT      -   type of constraints, array[K]:
                        * if CT[i]>0, then I-th constraint is C[i,*]*x >= C[i,n+1]
                        * if CT[i]=0, then I-th constraint is C[i,*]*x  = C[i,n+1]
                        * if CT[i]<0, then I-th constraint is C[i,*]*x <= C[i,n+1]
            K       -   number of equality/inequality constraints, K>=0:
                        * if given, only leading K elements of C/CT are used
                        * if not given, automatically determined from sizes of C/CT

        NOTE 1: linear (non-bound) constraints are satisfied only approximately  -
                there always exists some minor violation (about 10^10...10^13) due
                to rounding errors.

          -- ALGLIB --
             Copyright 19.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetlc(minqpstate state,
            double[,] c,
            int[] ct,
            int k)
        {
            int n = 0;
            int i = 0;
            int j = 0;
            double v = 0;
            int i_ = 0;

            n = state.n;
            
            //
            // First, check for errors in the inputs
            //
            alglib.ap.assert(k>=0, "MinQPSetLC: K<0");
            alglib.ap.assert(alglib.ap.cols(c)>=n+1 || k==0, "MinQPSetLC: Cols(C)<N+1");
            alglib.ap.assert(alglib.ap.rows(c)>=k, "MinQPSetLC: Rows(C)<K");
            alglib.ap.assert(alglib.ap.len(ct)>=k, "MinQPSetLC: Length(CT)<K");
            alglib.ap.assert(apserv.apservisfinitematrix(c, k, n+1), "MinQPSetLC: C contains infinite or NaN values!");
            
            //
            // Handle zero K
            //
            if( k==0 )
            {
                state.nec = 0;
                state.nic = 0;
                return;
            }
            
            //
            // Equality constraints are stored first, in the upper
            // NEC rows of State.CLEIC matrix. Inequality constraints
            // are stored in the next NIC rows.
            //
            // NOTE: we convert inequality constraints to the form
            // A*x<=b before copying them.
            //
            apserv.rmatrixsetlengthatleast(ref state.cleic, k, n+1);
            state.nec = 0;
            state.nic = 0;
            for(i=0; i<=k-1; i++)
            {
                if( ct[i]==0 )
                {
                    for(i_=0; i_<=n;i_++)
                    {
                        state.cleic[state.nec,i_] = c[i,i_];
                    }
                    state.nec = state.nec+1;
                }
            }
            for(i=0; i<=k-1; i++)
            {
                if( ct[i]!=0 )
                {
                    if( ct[i]>0 )
                    {
                        for(i_=0; i_<=n;i_++)
                        {
                            state.cleic[state.nec+state.nic,i_] = -c[i,i_];
                        }
                    }
                    else
                    {
                        for(i_=0; i_<=n;i_++)
                        {
                            state.cleic[state.nec+state.nic,i_] = c[i,i_];
                        }
                    }
                    state.nic = state.nic+1;
                }
            }
            
            //
            // Normalize rows of State.CLEIC: each row must have unit norm.
            // Norm is calculated using first N elements (i.e. right part is
            // not counted when we calculate norm).
            //
            for(i=0; i<=k-1; i++)
            {
                v = 0;
                for(j=0; j<=n-1; j++)
                {
                    v = v+math.sqr(state.cleic[i,j]);
                }
                if( (double)(v)==(double)(0) )
                {
                    continue;
                }
                v = 1/Math.Sqrt(v);
                for(i_=0; i_<=n;i_++)
                {
                    state.cleic[i,i_] = v*state.cleic[i,i_];
                }
            }
            
            //
            // Determine number of constraints,
            // allocate space and copy
            //
            state.ccount = k;
            apserv.rmatrixsetlengthatleast(ref state.cmatrix, k, n+1);
            apserv.ivectorsetlengthatleast(ref state.ct, k);
            apserv.rvectorsetlengthatleast(ref state.cr, k);
            for(i=0; i<=k-1; i++)
            {
                state.ct[i] = ct[i];
                state.cr[i] = c[i,n];
                for(i_=0; i_<=n;i_++)
                {
                    state.cmatrix[i,i_] = c[i,i_];
                }
            }
        }


        /*************************************************************************
        This function solves quadratic programming problem.
        You should call it after setting solver options with MinQPSet...() calls.

        INPUT PARAMETERS:
            State   -   algorithm state

        You should use MinQPResults() function to access results after calls
        to this function.

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpoptimize(minqpstate state)
        {
            int n = 0;
            int i = 0;
            int j = 0;
            int nbc = 0;
            double v0 = 0;
            double v1 = 0;
            double vn0 = 0;
            double v = 0;
            double noisetolerance = 0;
            double fprev = 0;
            double fcur = 0;
            int baditscounter = 0;
            int i_ = 0;

            noisetolerance = 10;
            n = state.n;
            state.repterminationtype = -5;
            state.repinneriterationscount = 0;
            state.repouteriterationscount = 0;
            state.repncholesky = 0;
            state.repnmv = 0;
            
            //
            // check correctness of constraints
            //
            for(i=0; i<=n-1; i++)
            {
                if( state.havebndl[i] && state.havebndu[i] )
                {
                    if( (double)(state.bndl[i])>(double)(state.bndu[i]) )
                    {
                        state.repterminationtype = -3;
                        return;
                    }
                }
            }
            
            //
            // count number of bound and linear constraints
            //
            nbc = 0;
            for(i=0; i<=n-1; i++)
            {
                if( state.havebndl[i] )
                {
                    nbc = nbc+1;
                }
                if( state.havebndu[i] )
                {
                    nbc = nbc+1;
                }
            }
            
            //
            // Our formulation of quadratic problem includes origin point,
            // i.e. we have F(x-x_origin) which is minimized subject to
            // constraints on x, instead of having simply F(x).
            //
            // Here we make transition from non-zero origin to zero one.
            // In order to make such transition we have to:
            // 1. subtract x_origin from x_start
            // 2. modify constraints
            // 3. solve problem
            // 4. add x_origin to solution
            //
            // There is alternate solution - to modify quadratic function
            // by expansion of multipliers containing (x-x_origin), but
            // we prefer to modify constraints, because it is a) more precise
            // and b) easier to to.
            //
            // Parts (1)-(2) are done here. After this block is over,
            // we have:
            // * XC, which stores shifted XStart (if we don't have XStart,
            //   value of XC will be ignored later)
            // * WorkBndL, WorkBndU, which store modified boundary constraints.
            //
            for(i=0; i<=n-1; i++)
            {
                if( state.havebndl[i] )
                {
                    state.workbndl[i] = state.bndl[i]-state.xorigin[i];
                }
                else
                {
                    state.workbndl[i] = Double.NegativeInfinity;
                }
                if( state.havebndu[i] )
                {
                    state.workbndu[i] = state.bndu[i]-state.xorigin[i];
                }
                else
                {
                    state.workbndu[i] = Double.PositiveInfinity;
                }
            }
            apserv.rmatrixsetlengthatleast(ref state.workcleic, state.nec+state.nic, n+1);
            for(i=0; i<=state.nec+state.nic-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.cleic[i,i_]*state.xorigin[i_];
                }
                for(i_=0; i_<=n-1;i_++)
                {
                    state.workcleic[i,i_] = state.cleic[i,i_];
                }
                state.workcleic[i,n] = state.cleic[i,n]-v;
            }
            
            //
            // modify starting point XC according to boundary constraints
            //
            if( state.havex )
            {
                
                //
                // We have starting point in StartX, so we just have to shift and bound it
                //
                for(i=0; i<=n-1; i++)
                {
                    state.xc[i] = state.startx[i]-state.xorigin[i];
                    if( state.havebndl[i] )
                    {
                        if( (double)(state.xc[i])<(double)(state.workbndl[i]) )
                        {
                            state.xc[i] = state.workbndl[i];
                        }
                    }
                    if( state.havebndu[i] )
                    {
                        if( (double)(state.xc[i])>(double)(state.workbndu[i]) )
                        {
                            state.xc[i] = state.workbndu[i];
                        }
                    }
                }
            }
            else
            {
                
                //
                // We don't have starting point, so we deduce it from
                // constraints (if they are present).
                //
                // NOTE: XC contains some meaningless values from previous block
                // which are ignored by code below.
                //
                for(i=0; i<=n-1; i++)
                {
                    if( state.havebndl[i] && state.havebndu[i] )
                    {
                        state.xc[i] = 0.5*(state.workbndl[i]+state.workbndu[i]);
                        if( (double)(state.xc[i])<(double)(state.workbndl[i]) )
                        {
                            state.xc[i] = state.workbndl[i];
                        }
                        if( (double)(state.xc[i])>(double)(state.workbndu[i]) )
                        {
                            state.xc[i] = state.workbndu[i];
                        }
                        continue;
                    }
                    if( state.havebndl[i] )
                    {
                        state.xc[i] = state.workbndl[i];
                        continue;
                    }
                    if( state.havebndu[i] )
                    {
                        state.xc[i] = state.workbndu[i];
                        continue;
                    }
                    state.xc[i] = 0;
                }
            }
            
            //
            // Select algo
            //
            if( state.algokind==1 )
            {
                
                //
                // Cholesky-based algorithm for dense bound constrained problems.
                //
                // This algorithm exists in two variants:
                // * unconstrained one, which can solve problem using only one NxN
                //   double matrix
                // * bound constrained one, which needs two NxN matrices
                //
                // We will try to solve problem using unconstrained algorithm,
                // and will use bound constrained version only when constraints
                // are actually present
                //
                if( nbc==0 && state.ccount==0 )
                {
                    
                    //
                    // "Simple" unconstrained version
                    //
                    for(i=0; i<=n-1; i++)
                    {
                        state.activeb[i] = false;
                    }
                    state.repncholesky = state.repncholesky+1;
                    cqmodels.cqmsetb(state.a, state.b);
                    cqmodels.cqmsetactiveset(state.a, state.xc, state.activeb);
                    if( !cqmodels.cqmconstrainedoptimum(state.a, ref state.xn) )
                    {
                        state.repterminationtype = -5;
                        return;
                    }
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.xc[i_] = state.xn[i_];
                    }
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.xc[i_] = state.xc[i_] + state.xorigin[i_];
                    }
                    state.repinneriterationscount = 1;
                    state.repouteriterationscount = 1;
                    state.repterminationtype = 4;
                    return;
                }
                
                //
                // Determine initial set of active boundary constraints
                //
                if( state.nec+state.nic>0 )
                {
                    
                    //
                    // General linear constraints are present; general code is used.
                    //
                    apserv.rvectorsetlengthatleast(ref state.tmp0, n);
                    apserv.rvectorsetlengthatleast(ref state.tmpfeas, n+state.nic);
                    apserv.rmatrixsetlengthatleast(ref state.tmpm0, state.nec+state.nic, n+state.nic+1);
                    for(i=0; i<=state.nec+state.nic-1; i++)
                    {
                        for(i_=0; i_<=n-1;i_++)
                        {
                            state.tmpm0[i,i_] = state.workcleic[i,i_];
                        }
                        for(j=n; j<=n+state.nic-1; j++)
                        {
                            state.tmpm0[i,j] = 0;
                        }
                        if( i>=state.nec )
                        {
                            state.tmpm0[i,n+i-state.nec] = 1.0;
                        }
                        state.tmpm0[i,n+state.nic] = state.workcleic[i,n];
                    }
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.tmpfeas[i_] = state.xc[i_];
                    }
                    for(i=0; i<=state.nic-1; i++)
                    {
                        v = 0.0;
                        for(i_=0; i_<=n-1;i_++)
                        {
                            v += state.workcleic[i+state.nec,i_]*state.xc[i_];
                        }
                        state.tmpfeas[i+n] = Math.Max(state.workcleic[i+state.nec,n]-v, 0.0);
                    }
                    if( !optserv.findfeasiblepoint(ref state.tmpfeas, state.workbndl, state.havebndl, state.workbndu, state.havebndu, n, state.nic, state.tmpm0, state.nec+state.nic, 1.0E-6, ref i, ref j) )
                    {
                        state.repterminationtype = -3;
                        return;
                    }
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.xc[i_] = state.tmpfeas[i_];
                    }
                    for(i=0; i<=n-1; i++)
                    {
                        state.activeb[i] = (state.havebndl[i] && (double)(state.xc[i])==(double)(state.workbndl[i])) || (state.havebndu[i] && (double)(state.xc[i])==(double)(state.workbndu[i]));
                    }
                    apserv.bvectorsetlengthatleast(ref state.activelin, state.nec+state.nic);
                    for(i=0; i<=state.nec+state.nic-1; i++)
                    {
                        state.activelin[i] = i<state.nec || (double)(state.tmpfeas[n+i-state.nec])==(double)(0);
                    }
                }
                else
                {
                    
                    //
                    // Only bound constraints are present, quick code can be used
                    //
                    for(i=0; i<=n-1; i++)
                    {
                        state.activeb[i] = false;
                        if( state.havebndl[i] && (double)(state.xc[i])<=(double)(state.workbndl[i]) )
                        {
                            state.xc[i] = state.workbndl[i];
                            state.activeb[i] = true;
                        }
                        if( state.havebndu[i] && (double)(state.xc[i])>=(double)(state.workbndu[i]) )
                        {
                            state.xc[i] = state.workbndu[i];
                            state.activeb[i] = true;
                        }
                    }
                }
                orthogonalizeactiveconstraints(state.xc, state.activeb, n, state.workcleic, state.activelin, state.nec+state.nic, ref state.activecm, ref state.activecr, ref state.activelincnt);
                
                //
                // Main cycle of BLEIC-QP algorithm
                //
                baditscounter = 0;
                state.repterminationtype = 4;
                while( true )
                {
                    
                    //
                    // Phase 1.
                    // 
                    // Active constraints can be added, but can not be removed.
                    // This phase allows us to reduce function value without
                    // leaving current face.
                    //
                    // Phase is over when we have no constraints to add. After
                    // this phase we have optimal and strictly feasible XC
                    // (or hopelessly infeasible - when constraints are inconsistent).
                    //
                    // Iterations which increase model value instead of decreasing
                    // it are called "bad". When number of "bad" iterations exceeds
                    // BadQPItsBeforeTermination limit, algorithm is terminated.
                    //
                    fprev = minqpmodelvalue(state.a, state.b, state.xc, n, ref state.tmp0);
                    while( true )
                    {
                        
                        //
                        // Calculate optimum subject to presently active constraints
                        //
                        state.repncholesky = state.repncholesky+1;
                        if( !minqpconstrainedoptimum(state.a, state.anorm, state.b, state.activeb, state.xc, n, state.activecm, state.activecr, state.activelincnt, ref state.xn, ref state.tmp0, ref state.tmp1) )
                        {
                            state.repterminationtype = -5;
                            return;
                        }
                        
                        //
                        // Add constraints.
                        // If no constraints were added, accept candidate point XN and move to next phase.
                        //
                        if( minqpboundedstepandactivation(state, ref state.xc, state.xn, ref state.tmp0)==0 )
                        {
                            break;
                        }
                    }
                    fcur = minqpmodelvalue(state.a, state.b, state.xc, n, ref state.tmp0);
                    if( (double)(fcur)>=(double)(fprev) )
                    {
                        baditscounter = baditscounter+1;
                    }
                    if( baditscounter>=badqpitsbeforetermination )
                    {
                        state.repterminationtype = 7;
                        break;
                    }
                    
                    //
                    // Phase 2 - steepest descent.
                    // The only phase where we can terminate algorithm.
                    //
                    // 1. we calculate projected gradient PG and scale it using
                    //    inverse diagonal of Hessian matrix
                    // 2. we calculate Cauchy point XN
                    // 3. in case difference between f(XN) and f(XC) is dominated by
                    //    numerical noise, we terminate algorithm.
                    // 4. otherwise, we perform bounded step to XN
                    //
                    // This is the only phase were we can terminate algorithm
                    //
                    // NOTE: Cauchy point is calculated as minimizer of
                    //       g(t) = f(xc-t*pg) = t^2*(0.5*pg'*A*pg) + t*(-pg*g) + const
                    //       where pg is a projected gradient, t is a step length, g is
                    //       a non-projected gradient.
                    //
                    // NOTE 2: here we assume that our problem is convex, hence pg'*A*pg>=0,
                    //         and pg'*A*pg=0 if and only if pg=0.
                    //
                    minqpdropconstraints(state, state.xc, ref state.tmp0);
                    minqpgradientandconstraineddescent(state, state.xc, ref state.gc, ref state.pg);
                    cqmodels.cqmevalx(state.a, state.xc, ref v, ref vn0);
                    v0 = cqmodels.cqmxtadx2(state.a, state.pg);
                    if( (double)(v0)>(double)(0) )
                    {
                        
                        //
                        // Function curvature is positive.
                        // Calculate predicted function decrease, which is equal to (pg'*g)^2/(4*pg'*A*pg).
                        // In case step is dominated by noise, we exit.
                        //
                        v1 = 0.0;
                        for(i_=0; i_<=n-1;i_++)
                        {
                            v1 += state.pg[i_]*state.gc[i_];
                        }
                        v1 = -v1;
                        if( (double)(math.sqr(v1)/(4*v0))<(double)(noisetolerance*vn0) )
                        {
                            break;
                        }
                        v = v1/(2*v0);
                        for(i_=0; i_<=n-1;i_++)
                        {
                            state.xn[i_] = state.xc[i_];
                        }
                        for(i_=0; i_<=n-1;i_++)
                        {
                            state.xn[i_] = state.xn[i_] + v*state.pg[i_];
                        }
                        minqpboundedstepandactivation(state, ref state.xc, state.xn, ref state.tmp0);
                    }
                    else
                    {
                        
                        //
                        // non-positive curvature detected.
                        // because it is assumed that function is positive definite,
                        // it means that projected gradient is zero (termination is needed).
                        //
                        break;
                    }
                    
                    //
                    // Update iterations count
                    //
                    state.repinneriterationscount = state.repinneriterationscount+1;
                }
                state.repouteriterationscount = 1;
                
                //
                // Post-process: add XOrigin to XC
                //
                for(i=0; i<=n-1; i++)
                {
                    if( state.havebndl[i] && (double)(state.xc[i])==(double)(state.workbndl[i]) )
                    {
                        state.xc[i] = state.bndl[i];
                        continue;
                    }
                    if( state.havebndu[i] && (double)(state.xc[i])==(double)(state.workbndu[i]) )
                    {
                        state.xc[i] = state.bndu[i];
                        continue;
                    }
                    state.xc[i] = apserv.boundval(state.xc[i]+state.xorigin[i], state.bndl[i], state.bndu[i]);
                }
                return;
            }
        }


        /*************************************************************************
        QP solver results

        INPUT PARAMETERS:
            State   -   algorithm state

        OUTPUT PARAMETERS:
            X       -   array[0..N-1], solution
            Rep     -   optimization report. You should check Rep.TerminationType,
                        which contains completion code, and you may check  another
                        fields which contain another information  about  algorithm
                        functioning.

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpresults(minqpstate state,
            ref double[] x,
            minqpreport rep)
        {
            x = new double[0];

            minqpresultsbuf(state, ref x, rep);
        }


        /*************************************************************************
        QP results

        Buffered implementation of MinQPResults() which uses pre-allocated  buffer
        to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
        intended to be used in the inner cycles of performance critical algorithms
        where array reallocation penalty is too large to be ignored.

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpresultsbuf(minqpstate state,
            ref double[] x,
            minqpreport rep)
        {
            int i_ = 0;

            if( alglib.ap.len(x)<state.n )
            {
                x = new double[state.n];
            }
            for(i_=0; i_<=state.n-1;i_++)
            {
                x[i_] = state.xc[i_];
            }
            rep.inneriterationscount = state.repinneriterationscount;
            rep.outeriterationscount = state.repouteriterationscount;
            rep.nmv = state.repnmv;
            rep.ncholesky = state.repncholesky;
            rep.terminationtype = state.repterminationtype;
        }


        /*************************************************************************
        Fast version of MinQPSetLinearTerm(), which doesn't check its arguments.
        For internal use only.

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetlineartermfast(minqpstate state,
            double[] b)
        {
            int i_ = 0;

            for(i_=0; i_<=state.n-1;i_++)
            {
                state.b[i_] = b[i_];
            }
        }


        /*************************************************************************
        Fast version of MinQPSetQuadraticTerm(), which doesn't check its arguments.

        It accepts additional parameter - shift S, which allows to "shift"  matrix
        A by adding s*I to A. S must be positive (although it is not checked).

        For internal use only.

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetquadratictermfast(minqpstate state,
            double[,] a,
            bool isupper,
            double s)
        {
            int i = 0;
            int j = 0;
            int n = 0;

            n = state.n;
            cqmodels.cqmseta(state.a, a, isupper, 1.0);
            if( (double)(s)>(double)(0) )
            {
                apserv.rvectorsetlengthatleast(ref state.tmp0, n);
                for(i=0; i<=n-1; i++)
                {
                    state.tmp0[i] = a[i,i]+s;
                }
                cqmodels.cqmrewritedensediagonal(state.a, state.tmp0);
            }
            
            //
            // Estimate norm of A
            // (it will be used later in the quadratic penalty function)
            //
            state.anorm = 0;
            for(i=0; i<=n-1; i++)
            {
                if( isupper )
                {
                    for(j=i; j<=n-1; j++)
                    {
                        state.anorm = Math.Max(state.anorm, Math.Abs(a[i,j]));
                    }
                }
                else
                {
                    for(j=0; j<=i; j++)
                    {
                        state.anorm = Math.Max(state.anorm, Math.Abs(a[i,j]));
                    }
                }
            }
            state.anorm = state.anorm*n;
        }


        /*************************************************************************
        Internal function which allows to rewrite diagonal of quadratic term.
        For internal use only.

        This function can be used only when you have dense A and already made
        MinQPSetQuadraticTerm(Fast) call.

          -- ALGLIB --
             Copyright 16.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqprewritediagonal(minqpstate state,
            double[] s)
        {
            cqmodels.cqmrewritedensediagonal(state.a, s);
        }


        /*************************************************************************
        Fast version of MinQPSetStartingPoint(), which doesn't check its arguments.
        For internal use only.

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetstartingpointfast(minqpstate state,
            double[] x)
        {
            int n = 0;
            int i_ = 0;

            n = state.n;
            for(i_=0; i_<=n-1;i_++)
            {
                state.startx[i_] = x[i_];
            }
            state.havex = true;
        }


        /*************************************************************************
        Fast version of MinQPSetOrigin(), which doesn't check its arguments.
        For internal use only.

          -- ALGLIB --
             Copyright 11.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minqpsetoriginfast(minqpstate state,
            double[] xorigin)
        {
            int n = 0;
            int i_ = 0;

            n = state.n;
            for(i_=0; i_<=n-1;i_++)
            {
                state.xorigin[i_] = xorigin[i_];
            }
        }


        /*************************************************************************
        Having current point XC, this function scans active constraints,   detects
        ones  which  should  be deactivated by step in the antigradient direction,
        deactivates them, and returns constraints count.

        For example, if we have:
          XC=1.0,
          antigradient is equal to [-2]
          x>=0, x<=1
        then this function will detect that constraint "x<=1" will be  deactivated
        by  attempt  to  make  step  in the antigradient direction. Only presently
        active constraints are counted and deactivated.

        INPUT PARAMETERS:
            State   -   MinQP state.
            XC      -   current point, must be feasible with respect to all
                        constrants
            Buf     -   temporary buffer, automatically resized if needed

        OUTPUT PARAMETERS:
            State   -   this function changes following fields of State:
                        * State.ActiveB     
                        * State.ActiveLin   
                        * State.ActiveLinCnt
                        * State.ActiveCM
                        * State.ActiveCR
            
        RESULT:

        Number of constraints being deactivated.

        NOTES:
            this function uses fields of State which are marked as
            "DropConstraints temporaries". These fields are changed during algorithm
            operation.

          -- ALGLIB --
             Copyright 29.02.2012 by Bochkanov Sergey
        *************************************************************************/
        private static int minqpdropconstraints(minqpstate state,
            double[] xc,
            ref double[] buf)
        {
            int result = 0;
            int n = 0;
            int ncols = 0;
            int i = 0;
            int j = 0;
            int cidx = 0;
            double v = 0;
            bool reactivationflag = new bool();
            double norm0 = 0;
            double norm1 = 0;
            int idx = 0;
            int nactivebnd = 0;
            int orthogonalbasissize = 0;
            int i_ = 0;
            int i1_ = 0;

            n = state.n;
            
            //
            // Handle important special case - no active linear constraints,
            // only boundary constraints are present
            //
            if( state.activelincnt==0 )
            {
                
                //
                // Calculate gradient vector
                //
                apserv.rvectorsetlengthatleast(ref buf, n);
                cqmodels.cqmadx(state.a, xc, ref buf);
                for(i_=0; i_<=n-1;i_++)
                {
                    buf[i_] = buf[i_] + state.b[i_];
                }
                result = 0;
                for(i=0; i<=n-1; i++)
                {
                    if( state.activeb[i] )
                    {
                        if( (state.havebndl[i] && state.havebndu[i]) && (double)(state.workbndl[i])==(double)(state.workbndu[i]) )
                        {
                            continue;
                        }
                        if( (state.havebndl[i] && (double)(xc[i])==(double)(state.workbndl[i])) && (double)(buf[i])<(double)(0) )
                        {
                            result = result+1;
                            state.activeb[i] = false;
                            continue;
                        }
                        if( (state.havebndu[i] && (double)(xc[i])==(double)(state.workbndu[i])) && (double)(buf[i])>(double)(0) )
                        {
                            result = result+1;
                            state.activeb[i] = false;
                            continue;
                        }
                    }
                }
                return result;
            }
            
            //
            // Now we have following problem: given ActiveLinCnt>0 active linear
            // constraints and NActiveBnd>=0 active boundary constraints, we want
            // to determine which constraints should be deactivated.
            //
            // We use method of Lagrange multipliers to do so:
            // * active constraints are written as C*x=r, where c[i] is I-th row
            //   of C, and C contains all active constraints - boundary and 
            //   general linear ones.
            // * gradient at x=XC is denoted as g
            // * at x=xc we have following linear system: SUM(lc[i]*C[i])=-g,
            //   where lc[i] is I-th Lagrange multiplier, C[i] is an I-th row of C.
            // * solution of this system is a set of Lagrange coefficients. Inequality
            //   constraints with non-positive Lagrange coefficients are deactivated.
            //   Equality constraints (either boundary constraints of the form a<=x[i]<=a
            //   or general linear ones a'*x=b) are left unchanged. 
            // * after initial deactivation of constraints we perform re-check, because
            //   sometimes algorithm deactivates too many constraints.
            //   We repeatedly calculate descent vector and check its feasibility. In
            //   case step in descent direction activates some constraints, these
            //   constraints are activated and we perform check one more time, until we 
            //   finally get descent direction which does not activate anything.
            //
            // Lagrange system we want to solve has N*(NActiveBnd+ActiveLinCnt) size;
            // it can be pretty large when we have many boundary constraints activated.
            // Straightforward attemp to solve such system gives us O(N^3) worst-case
            // performance.
            //
            // However, we can exploit structure of the matrix to solve this system
            // efficiently. Consider an example problem with N=6, 3 active boundary
            // constraints and 2 active linear constraints. After permutation of rows
            // (ones which correspond to active boundary constraints are moved to the
            // beginning) matrix of the Lagrange system will have following structure:
            //
            //   ( lc0 )T   ( d0                      )      ( -g0 )T
            //   ( lc1 )    (     d1                  )      ( -g1 )
            //   ( lc2 )  * (         d2              )      ( -g2 )
            //   ( lc3 )    ( s00 s01 s02 s03 s04 s05 ) *  = ( -g3 )
            //   ( lc4 )    ( s10 s11 s12 s13 s14 s15 )      ( -g4 )
            //                                               ( -g5 )
            //
            // Furthermore, diagonal elements di are either -1 (in case lower bound
            // is active) or +1 (in case upper bound is active). Such system can be
            // easility orthogonalized by Gram–Schmidt process applied to rows:
            //
            //     lc*C = -g
            //     lc*inv(T)*T*C = -g, where T is transformation matrix
            //     (lc*inv(T))*Q = -g, where Q is orthogonal
            //     lc = -g*Q'*T
            //
            // Special structure of the system matrix C allows us to easily
            // orthogonalize it in O(N*ActiveLinCnt^2) operations. It is important
            // to note that (NActiveBnd+ActiveLinCnt)*(NActiveBnd+ActiveLinCnt) transform
            // matrix T has special structure too:
            //
            //   ( d0                      )
            //   (     d1                  )
            //   (         d2              )
            //   ( t00 t01 t02 t03 t04 t05 )
            //   ( t10 t11 t12 t13 t14 t15 )
            //
            // It allow us to store it using N*ActiveLinCnt storage.
            //
            // Thus, our Lagrangian solver has O(N*ActiveLinCnt^2) time complexity and
            // O(N*ActiveLinCnt) memory complexity, which is significant improvement over
            // naive O(N^3)/O(N^2) approach.
            //
            nactivebnd = 0;
            for(j=0; j<=n-1; j++)
            {
                if( state.activeb[j] )
                {
                    nactivebnd = nactivebnd+1;
                }
            }
            ncols = state.activelincnt+nactivebnd;
            
            //
            // Generate temporary system and right part - ones
            // before permutation of rows is applied:
            // * LagrangeSystemTmp - ActiveLinCnt*N block of the system matrix (right part)
            // * LSRightPartTmp - right part, antigradient
            //
            apserv.rvectorsetlengthatleast(ref state.lsrightparttmp, n);
            apserv.rmatrixsetlengthatleast(ref state.lagrangesystemtmp, state.activelincnt, n);
            cqmodels.cqmadx(state.a, xc, ref state.lsrightparttmp);
            for(i_=0; i_<=n-1;i_++)
            {
                state.lsrightparttmp[i_] = state.lsrightparttmp[i_] + state.b[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.lsrightparttmp[i_] = -1.0*state.lsrightparttmp[i_];
            }
            cidx = 0;
            for(j=0; j<=state.nec+state.nic-1; j++)
            {
                if( state.activelin[j] )
                {
                    for(i_=0; i_<=n-1;i_++)
                    {
                        state.lagrangesystemtmp[cidx,i_] = state.workcleic[j,i_];
                    }
                    cidx = cidx+1;
                }
            }
            cidx = 0;
            for(j=0; j<=n-1; j++)
            {
                if( state.activeb[j] )
                {
                    alglib.ap.assert((state.havebndl[j] && (double)(xc[j])==(double)(state.workbndl[j])) || (state.havebndu[j] && (double)(xc[j])==(double)(state.workbndu[j])), "MinQPDropConstraints: internal error (1)");
                    if( (double)(xc[j])==(double)(state.workbndl[j]) )
                    {
                        
                        //
                        // We normalize our system to have all d[i]=1.0
                        // by multiplying columns corresponding to negative d[i] by -1.
                        //
                        for(i_=0; i_<=state.activelincnt-1;i_++)
                        {
                            state.lagrangesystemtmp[i_,j] = -1*state.lagrangesystemtmp[i_,j];
                        }
                        state.lsrightparttmp[j] = -state.lsrightparttmp[j];
                    }
                    cidx = cidx+1;
                }
            }
            
            //
            // Initialize right part of the transform matrix (ActiveLinCnt*NCols block)
            // by corresponding part of the identity matrix:
            //
            // LSTransform :=  (          1   )
            //                 (            1 )
            //
            // Initial matrix T is NCols*NCols identity matrix, but top NActiveBnd
            // columns are not changed by orthogonalization process, so we store
            // only its bottom block, which is actually changed during orthogonalization.
            //
            apserv.rmatrixsetlengthatleast(ref state.lstransform, state.activelincnt, ncols);
            for(i=0; i<=ncols-1; i++)
            {
                for(j=0; j<=state.activelincnt-1; j++)
                {
                    state.lstransform[j,i] = 0;
                }
            }
            for(i=0; i<=state.activelincnt-1; i++)
            {
                state.lstransform[i,nactivebnd+i] = 1.0;
            }
            
            //
            // Reoder columns of the temporary system; columns corresponding to active
            // boundary constraints are moved to the beginning
            //
            apserv.rmatrixsetlengthatleast(ref state.lagrangesystem, state.activelincnt, n);
            apserv.rvectorsetlengthatleast(ref state.lsrightpart, n);
            idx = 0;
            for(i=0; i<=n-1; i++)
            {
                if( state.activeb[i] )
                {
                    for(i_=0; i_<=state.activelincnt-1;i_++)
                    {
                        state.lagrangesystem[i_,idx] = state.lagrangesystemtmp[i_,i];
                    }
                    state.lsrightpart[idx] = state.lsrightparttmp[i];
                    idx = idx+1;
                }
            }
            for(i=0; i<=n-1; i++)
            {
                if( !state.activeb[i] )
                {
                    for(i_=0; i_<=state.activelincnt-1;i_++)
                    {
                        state.lagrangesystem[i_,idx] = state.lagrangesystemtmp[i_,i];
                    }
                    state.lsrightpart[idx] = state.lsrightparttmp[i];
                    idx = idx+1;
                }
            }
            
            //
            // Perform orthogonalization of rows of C:
            // * first NActiveBnd rows are already orthogonalized, we just
            //   have to orthogonalize next ActiveLinCnt with respect to each other.
            // * during orthogonalization we monitor size of the current basis
            //   (stored in OrthogonalBasisSize variable). When basis size will become
            //   equal to the dimensionality of the space, we will stop adding new
            //   vectors to basis.
            //
            orthogonalbasissize = nactivebnd;
            for(i=0; i<=state.activelincnt-1; i++)
            {
                
                //
                // Calculate norm of I-th rows. If it is zero, skip rows.
                //
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.lagrangesystem[i,i_]*state.lagrangesystem[i,i_];
                }
                norm0 = Math.Sqrt(v);
                if( (double)(norm0)==(double)(0) )
                {
                    continue;
                }
                
                //
                // Orthogonalize with respect to first NActiveBnd rows (correspond
                // to active boundary constraints).
                //
                // Transformation which is applied to LagrangeSystem is also applied
                // to LSTransform, which stores transformation matrix.
                //
                for(j=0; j<=nactivebnd-1; j++)
                {
                    state.lstransform[i,j] = -state.lagrangesystem[i,j];
                    state.lagrangesystem[i,j] = 0.0;
                }
                
                //
                // Orthogonalize with respect to active linear constraints
                //
                // NOTE: first NActiveBnd elements of I-th row are already
                //       zero; we perform orthogonalization only when NActiveBnd<N.
                //
                if( nactivebnd<n )
                {
                    for(j=0; j<=i-1; j++)
                    {
                        v = 0.0;
                        for(i_=nactivebnd; i_<=n-1;i_++)
                        {
                            v += state.lagrangesystem[j,i_]*state.lagrangesystem[i,i_];
                        }
                        for(i_=nactivebnd; i_<=n-1;i_++)
                        {
                            state.lagrangesystem[i,i_] = state.lagrangesystem[i,i_] - v*state.lagrangesystem[j,i_];
                        }
                        for(i_=0; i_<=j+nactivebnd;i_++)
                        {
                            state.lstransform[i,i_] = state.lstransform[i,i_] - v*state.lstransform[j,i_];
                        }
                    }
                }
                
                //
                // Determine row norm and decay factor. In case row is almost zero
                // when compared with its norm before orthogonalization, it is considered
                // zero.
                //
                // We perform normalization of the row and apply similar transformation
                // to LSTransform.
                //
                // NOTE: row can be considered zero in one more case - size of the
                //       current basis is already N (dimension of space).
                //
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.lagrangesystem[i,i_]*state.lagrangesystem[i,i_];
                }
                norm1 = Math.Sqrt(v);
                if( (double)(norm1)>=(double)(10*n*math.machineepsilon*norm0) && orthogonalbasissize<n )
                {
                    v = 1/norm1;
                    orthogonalbasissize = orthogonalbasissize+1;
                }
                else
                {
                    v = 0.0;
                }
                for(i_=0; i_<=n-1;i_++)
                {
                    state.lagrangesystem[i,i_] = v*state.lagrangesystem[i,i_];
                }
                for(i_=0; i_<=ncols-1;i_++)
                {
                    state.lstransform[i,i_] = v*state.lstransform[i,i_];
                }
            }
            
            //
            // After factorization of the Lagrange system, we have to solve it:
            // * first, we perform multiplication by Q
            // * then, we multiply by T
            //
            apserv.rvectorsetlengthatleast(ref buf, ncols);
            apserv.rvectorsetlengthatleast(ref state.lagrangecoeffs, ncols);
            for(i=0; i<=nactivebnd-1; i++)
            {
                buf[i] = state.lsrightpart[i];
            }
            for(i=0; i<=state.activelincnt-1; i++)
            {
                if( nactivebnd<n )
                {
                    v = 0.0;
                    for(i_=nactivebnd; i_<=n-1;i_++)
                    {
                        v += state.lagrangesystem[i,i_]*state.lsrightpart[i_];
                    }
                }
                else
                {
                    v = 0;
                }
                buf[nactivebnd+i] = v;
            }
            for(i=0; i<=nactivebnd-1; i++)
            {
                i1_ = (nactivebnd)-(0);
                v = 0.0;
                for(i_=0; i_<=state.activelincnt-1;i_++)
                {
                    v += state.lstransform[i_,i]*buf[i_+i1_];
                }
                state.lagrangecoeffs[i] = buf[i]+v;
            }
            for(i=0; i<=state.activelincnt-1; i++)
            {
                i1_ = (nactivebnd+i)-(i);
                v = 0.0;
                for(i_=i; i_<=state.activelincnt-1;i_++)
                {
                    v += state.lstransform[i_,nactivebnd+i]*buf[i_+i1_];
                }
                state.lagrangecoeffs[nactivebnd+i] = v;
            }
            
            //
            // We've solved Lagrange system.
            // Store previous values of ActiveB/ActiveLin
            // Deactivate bound and inequality constraints corresponding to negative Lagrange multipliers.
            // Active equality constraints are left unchanged independently of the multiplier sign.
            //
            apserv.bvectorsetlengthatleast(ref state.prevactiveb, n);
            apserv.bvectorsetlengthatleast(ref state.prevactivelin, state.nec+state.nic);
            for(i=0; i<=n-1; i++)
            {
                state.prevactiveb[i] = state.activeb[i];
            }
            for(i=0; i<=state.nec+state.nic-1; i++)
            {
                state.prevactivelin[i] = state.activelin[i];
            }
            cidx = 0;
            for(i=0; i<=n-1; i++)
            {
                if( state.activeb[i] )
                {
                    if( (state.havebndl[i] && state.havebndu[i]) && (double)(state.workbndl[i])==(double)(state.workbndu[i]) )
                    {
                        
                        //
                        // Equality constraint a[i]=x[i]=b[i], skip it
                        //
                        cidx = cidx+1;
                        continue;
                    }
                    if( (double)(state.lagrangecoeffs[cidx])<(double)(0) )
                    {
                        state.activeb[i] = false;
                    }
                    cidx = cidx+1;
                }
            }
            for(i=0; i<=state.nec+state.nic-1; i++)
            {
                if( state.activelin[i] )
                {
                    if( i<state.nec )
                    {
                        
                        //
                        // Equality constraint, skip it
                        //
                        cidx = cidx+1;
                        continue;
                    }
                    if( (double)(state.lagrangecoeffs[cidx])<(double)(0) )
                    {
                        state.activelin[i] = false;
                    }
                    cidx = cidx+1;
                }
            }
            
            //
            // Post-process constraints, maybe some of them need reactivation.
            // Each iteration of the cycle refreshes ActiveCM and other related structures.
            //
            while( true )
            {
                orthogonalizeactiveconstraints(xc, state.activeb, n, state.workcleic, state.activelin, state.nec+state.nic, ref state.activecm, ref state.activecr, ref state.activelincnt);
                minqpgradientandconstraineddescent(state, xc, ref buf, ref state.lstmp0);
                reactivationflag = false;
                for(i=0; i<=n-1; i++)
                {
                    if( !state.activeb[i] && state.prevactiveb[i] )
                    {
                        if( ((state.havebndl[i] && (double)(xc[i])==(double)(state.workbndl[i])) && (double)(state.lstmp0[i])<(double)(0)) || ((state.havebndu[i] && (double)(xc[i])==(double)(state.workbndu[i])) && (double)(state.lstmp0[i])>(double)(0)) )
                        {
                            reactivationflag = true;
                            state.activeb[i] = true;
                        }
                    }
                }
                for(i=state.nec; i<=state.nec+state.nic-1; i++)
                {
                    if( !state.activelin[i] && state.prevactivelin[i] )
                    {
                        v = 0.0;
                        for(i_=0; i_<=n-1;i_++)
                        {
                            v += state.workcleic[i,i_]*state.lstmp0[i_];
                        }
                        if( (double)(v)>(double)(0) )
                        {
                            reactivationflag = true;
                            state.activelin[i] = true;
                        }
                    }
                }
                if( !reactivationflag )
                {
                    break;
                }
            }
            
            //
            // Number of deactivated constraints
            //
            result = 0;
            for(i=0; i<=n-1; i++)
            {
                if( state.prevactiveb[i] && !state.activeb[i] )
                {
                    result = result+1;
                }
            }
            for(i=0; i<=state.nec+state.nic-1; i++)
            {
                if( state.prevactivelin[i] && !state.activelin[i] )
                {
                    result = result+1;
                }
            }
            return result;
        }


        /*************************************************************************
        Having feasible current point XC and possibly infeasible candidate   point
        XN,  this  function  performs  longest  step  from  XC to XN which retains
        feasibility. In case XN is found to be infeasible, at least one constraint
        is activated.

        For example, if we have:
          XC=0.5
          XN=1.2
          x>=0, x<=1
        then this function will move us to X=1.0 and activate constraint "x<=1".

        INPUT PARAMETERS:
            State   -   MinQP state.
            XC      -   current point, must be feasible with respect to
                        all constraints
            XN      -   candidate point, can be infeasible with respect to some
                        constraints
            Buf     -   temporary buffer, automatically resized if needed

        OUTPUT PARAMETERS:
            State   -   this function changes following fields of State:
                        * State.ActiveB     -   active boundary constraints
                        * State.ActiveC     -   active linear constraints
            XC      -   new position

        RESULT:
            number of constraints being activated by step.  Constraints which were
            DEACTIVATED are ignored (do not influence function value).

          -- ALGLIB --
             Copyright 29.02.2012 by Bochkanov Sergey
        *************************************************************************/
        private static int minqpboundedstepandactivation(minqpstate state,
            ref double[] xc,
            double[] xn,
            ref double[] buf)
        {
            int result = 0;
            int n = 0;
            int i = 0;
            int varidx = 0;
            int linidx = 0;
            double varval = 0;
            double stplen = 0;
            double prevmax = 0;
            double vc = 0;
            double vn = 0;
            int i_ = 0;

            n = state.n;
            apserv.rvectorsetlengthatleast(ref buf, n);
            for(i_=0; i_<=n-1;i_++)
            {
                buf[i_] = xn[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                buf[i_] = buf[i_] - xc[i_];
            }
            varidx = -1;
            varval = 0.0;
            linidx = -1;
            stplen = math.maxrealnumber;
            for(i=0; i<=n-1; i++)
            {
                if( !state.activeb[i] )
                {
                    alglib.ap.assert(!state.havebndl[i] || (double)(xc[i])>=(double)(state.workbndl[i]), "MinQPBoundedStepAndActivation: internal error - infeasible XC");
                    alglib.ap.assert(!state.havebndu[i] || (double)(xc[i])<=(double)(state.workbndu[i]), "MinQPBoundedStepAndActivation: internal error - infeasible XC");
                    if( state.havebndl[i] && (double)(xn[i])<=(double)(state.workbndl[i]) )
                    {
                        prevmax = stplen;
                        stplen = apserv.safeminposrv(xc[i]-state.workbndl[i], xc[i]-xn[i], stplen);
                        if( (double)(stplen)<(double)(prevmax) )
                        {
                            varidx = i;
                            linidx = -1;
                            varval = state.workbndl[i];
                        }
                    }
                    if( state.havebndu[i] && (double)(xn[i])>=(double)(state.workbndu[i]) )
                    {
                        prevmax = stplen;
                        stplen = apserv.safeminposrv(state.workbndu[i]-xc[i], xn[i]-xc[i], stplen);
                        if( (double)(stplen)<(double)(prevmax) )
                        {
                            varidx = i;
                            linidx = -1;
                            varval = state.workbndu[i];
                        }
                    }
                }
            }
            for(i=state.nec; i<=state.nec+state.nic-1; i++)
            {
                if( !state.activelin[i] )
                {
                    vc = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        vc += state.workcleic[i,i_]*xc[i_];
                    }
                    vc = vc-state.workcleic[i,n];
                    vn = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        vn += state.workcleic[i,i_]*xn[i_];
                    }
                    vn = vn-state.workcleic[i,n];
                    if( (double)(vn)>=(double)(0) )
                    {
                        
                        //
                        // XN is infeasible. Depending on feasibility of XC,
                        // we can perform either non-zero or zero step.
                        //
                        if( (double)(vc)<(double)(0) )
                        {
                            
                            //
                            // XC is strictly feasible with respect to I-th constraint,
                            // we can perform non-zero step because there is non-zero distance
                            // between XC and bound.
                            //
                            prevmax = stplen;
                            stplen = apserv.safeminposrv(-vc, vn-vc, stplen);
                            if( (double)(stplen)<(double)(prevmax) )
                            {
                                linidx = i;
                                varidx = -1;
                            }
                        }
                        else
                        {
                            
                            //
                            // XC is at the boundary (or slightly beyond it), and XN is
                            // strictly infeasible.
                            // The only thing we can do is to perform zero step and activate
                            // I-th constraint.
                            //
                            stplen = 0;
                            linidx = i;
                            varidx = -1;
                        }
                    }
                }
            }
            stplen = Math.Min(stplen, 1.0);
            
            //
            // Perform step
            //
            for(i_=0; i_<=n-1;i_++)
            {
                buf[i_] = xn[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                buf[i_] = buf[i_] - xc[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                buf[i_] = stplen*buf[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                buf[i_] = buf[i_] + xc[i_];
            }
            alglib.ap.assert(varidx<0 || linidx<0, "MinQPBoundedStepAndActivation: internal error (2)");
            if( (double)(stplen)<(double)(1.0) )
            {
                if( varidx>=0 )
                {
                    state.activeb[varidx] = true;
                }
                if( linidx>=0 )
                {
                    state.activelin[linidx] = true;
                }
            }
            result = optserv.postprocessboundedstep(ref buf, xc, state.workbndl, state.havebndl, state.workbndu, state.havebndu, n, 0, varidx, varval, stplen, stplen);
            if( linidx>=0 )
            {
                result = result+1;
            }
            if( result>0 )
            {
                orthogonalizeactiveconstraints(buf, state.activeb, n, state.workcleic, state.activelin, state.nec+state.nic, ref state.activecm, ref state.activecr, ref state.activelincnt);
            }
            for(i_=0; i_<=n-1;i_++)
            {
                xc[i_] = buf[i_];
            }
            return result;
        }


        /*************************************************************************
        Model value: f = 0.5*x'*A*x + b'*x

        INPUT PARAMETERS:
            A       -   convex quadratic model; only main quadratic term is used,
                        other parts of the model (D/Q/linear term) are ignored.
                        This function does not modify model state.
            B       -   right part
            XC      -   evaluation point
            Tmp     -   temporary buffer, automatically resized if needed

          -- ALGLIB --
             Copyright 20.06.2012 by Bochkanov Sergey
        *************************************************************************/
        private static double minqpmodelvalue(cqmodels.convexquadraticmodel a,
            double[] b,
            double[] xc,
            int n,
            ref double[] tmp)
        {
            double result = 0;
            double v0 = 0;
            double v1 = 0;
            int i_ = 0;

            apserv.rvectorsetlengthatleast(ref tmp, n);
            cqmodels.cqmadx(a, xc, ref tmp);
            v0 = 0.0;
            for(i_=0; i_<=n-1;i_++)
            {
                v0 += xc[i_]*tmp[i_];
            }
            v1 = 0.0;
            for(i_=0; i_<=n-1;i_++)
            {
                v1 += xc[i_]*b[i_];
            }
            result = 0.5*v0+v1;
            return result;
        }


        /*************************************************************************
        Constrained descent direction from the feasible point:
        * without constraints, equal to the antigradient
        * in the presence of active constraints it is equal to the projection of
          antigradient into feasible set

        INPUT PARAMETERS:
            State   -   MinQP state.
            XC      -   current point, must be feasible with respect to
                        all constraints
            GC      -   possibly preallocated buffer
            D       -   possibly preallocated buffer

        OUTPUT PARAMETERS:
            GC      -   gradient of the original (unconstrained model without
                        rank-K term) at the current point
            D       -   descent direction chosen with respect to constraints

        RESULT:
            length of the direction vector

          -- ALGLIB --
             Copyright 20.06.2012 by Bochkanov Sergey
        *************************************************************************/
        private static double minqpgradientandconstraineddescent(minqpstate state,
            double[] xc,
            ref double[] gc,
            ref double[] d)
        {
            double result = 0;
            int i = 0;
            int n = 0;
            double v = 0;
            double norm0 = 0;
            double norm1 = 0;
            int i_ = 0;

            n = state.n;
            apserv.rvectorsetlengthatleast(ref gc, n);
            apserv.rvectorsetlengthatleast(ref d, n);
            
            //
            // Calculate gradient of the original model
            //
            cqmodels.cqmadx(state.a, xc, ref gc);
            for(i_=0; i_<=n-1;i_++)
            {
                gc[i_] = gc[i_] + state.b[i_];
            }
            
            //
            // Calculate descent direction
            //
            norm0 = 0;
            for(i=0; i<=n-1; i++)
            {
                if( state.activeb[i] )
                {
                    d[i] = 0;
                }
                else
                {
                    d[i] = -gc[i];
                    norm0 = norm0+math.sqr(d[i]);
                }
            }
            norm0 = Math.Sqrt(norm0);
            for(i=0; i<=state.activelincnt-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.activecm[i,i_]*d[i_];
                }
                for(i_=0; i_<=n-1;i_++)
                {
                    d[i_] = d[i_] - v*state.activecm[i,i_];
                }
            }
            norm1 = 0;
            for(i=0; i<=n-1; i++)
            {
                norm1 = norm1+math.sqr(d[i]);
            }
            norm1 = Math.Sqrt(norm1);
            if( (double)(norm1)<=(double)(1.0E3*math.machineepsilon*norm0) )
            {
                for(i=0; i<=n-1; i++)
                {
                    d[i] = 0;
                }
            }
            result = 0.0;
            for(i=0; i<=n-1; i++)
            {
                result = result+math.sqr(d[i]);
            }
            result = Math.Sqrt(result);
            return result;
        }


        /*************************************************************************
        Optimum of A subject to:
        a) active boundary constraints (given by ActiveB[] and corresponding
           elements of XC)
        b) active linear constraints (given by C, R, LagrangeC)

        INPUT PARAMETERS:
            A       -   main quadratic term of the model;
                        although structure may  store  linear  and  rank-K  terms,
                        these terms are ignored and rewritten  by  this  function.
            ANorm   -   estimate of ||A|| (2-norm is used)
            B       -   array[N], linear term of the model
            ActiveB -   array[N], active boundary constraints
            XC      -   array[N], constrained values
            C       -   equality constraints, array[K,N]
            R       -   array[K], right part of the equality constraints
            K       -   number of equality constraints
            XN      -   possibly preallocated buffer
            Tmp     -   temporary buffer (automatically resized)
            Tmp1    -   temporary buffer (automatically resized)

        OUTPUT PARAMETERS:
            A       -   modified quadratic model (this function changes rank-K
                        term and linear term of the model)
            LagrangeC-  current estimate of the Lagrange coefficients
            XN      -   solution

        RESULT:
            True on success, False on failure (non-SPD model)

          -- ALGLIB --
             Copyright 20.06.2012 by Bochkanov Sergey
        *************************************************************************/
        private static bool minqpconstrainedoptimum(cqmodels.convexquadraticmodel a,
            double anorm,
            double[] b,
            bool[] activeb,
            double[] xc,
            int n,
            double[,] c,
            double[] r,
            int k,
            ref double[] xn,
            ref double[] tmp,
            ref double[] lagrangec)
        {
            bool result = new bool();
            int itidx = 0;
            int i = 0;
            double v = 0;
            double feaserrold = 0;
            double feaserrnew = 0;
            double theta = 0;
            int i_ = 0;

            
            //
            // Prepare model
            //
            theta = 100.0*anorm;
            apserv.rvectorsetlengthatleast(ref tmp, n);
            apserv.rvectorsetlengthatleast(ref lagrangec, k);
            cqmodels.cqmsetactiveset(a, xc, activeb);
            cqmodels.cqmsetq(a, c, r, k, theta);
            
            //
            // Iterate until optimal values of Lagrange multipliers are found
            //
            for(i=0; i<=k-1; i++)
            {
                lagrangec[i] = 0;
            }
            feaserrnew = math.maxrealnumber;
            result = true;
            for(itidx=1; itidx<=maxlagrangeits; itidx++)
            {
                
                //
                // Generate right part B using linear term and current
                // estimate of the Lagrange multipliers.
                //
                for(i_=0; i_<=n-1;i_++)
                {
                    tmp[i_] = b[i_];
                }
                for(i=0; i<=k-1; i++)
                {
                    v = lagrangec[i];
                    for(i_=0; i_<=n-1;i_++)
                    {
                        tmp[i_] = tmp[i_] - v*c[i,i_];
                    }
                }
                cqmodels.cqmsetb(a, tmp);
                
                //
                // Solve
                //
                result = cqmodels.cqmconstrainedoptimum(a, ref xn);
                if( !result )
                {
                    return result;
                }
                
                //
                // Compare feasibility errors.
                // Terminate if error decreased too slowly.
                //
                feaserrold = feaserrnew;
                feaserrnew = 0;
                for(i=0; i<=k-1; i++)
                {
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += c[i,i_]*xn[i_];
                    }
                    feaserrnew = feaserrnew+math.sqr(v-r[i]);
                }
                feaserrnew = Math.Sqrt(feaserrnew);
                if( (double)(feaserrnew)>=(double)(0.2*feaserrold) )
                {
                    break;
                }
                
                //
                // Update Lagrange multipliers
                //
                for(i=0; i<=k-1; i++)
                {
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += c[i,i_]*xn[i_];
                    }
                    lagrangec[i] = lagrangec[i]-theta*(v-r[i]);
                }
            }
            return result;
        }


        /*************************************************************************
        This function builds orthogonal constraint matrix using:
        * active set given by ActiveB/ActiveLin
        * XC, which stores current point (and - values of active boundary constraints)
        * original matrix of constraints CLEIC

        As result, it fills:
        * ActiveCM - by orthogonalized constraints
        * ActiveCR - by right parts

        INPUT PARAMETERS:
            XC      -   current point, array[N]; used to determine values of active
                        boundary constraints.
            ActiveB -   active boundary constraints, array[N]
            N       -   size
            CLEIC   -   matrix of ALL linear constraints, array[CTotal,N+1].
                        first N elements of each row store constraint, last
                        element contains right part.
            ActiveLin-  active linear constraints
            CTotal  -   number of linear constraints (equality + inequality)
            ActiveCM-   possibly preallocated buffer
            ActiveCR-   possibly preallocated buffer

        OUTPUT PARAMETERS:
            ActiveCM-   matrix of active linear constraints, array[...,N].
                        Rows of this matrix are:
                        * orthogonal to the active boundary constraints (i.e.
                          have zeros at corresponding positions)
                        * orthogonal to each other
                        Right parts are stored in separate array.
            ActiveCR-   matrix of active right parts
            NActive -   number of active linear constraints

          -- ALGLIB --
             Copyright 20.06.2012 by Bochkanov Sergey
        *************************************************************************/
        private static void orthogonalizeactiveconstraints(double[] xc,
            bool[] activeb,
            int n,
            double[,] cleic,
            bool[] activelin,
            int ctotal,
            ref double[,] activecm,
            ref double[] activecr,
            ref int nactive)
        {
            int i = 0;
            int j = 0;
            int k = 0;
            int ridx = 0;
            double v = 0;
            double norm0 = 0;
            double norm1 = 0;
            int i_ = 0;

            
            //
            // Determine number of active constraints
            //
            nactive = 0;
            for(i=0; i<=ctotal-1; i++)
            {
                if( activelin[i] )
                {
                    nactive = nactive+1;
                }
            }
            if( nactive==0 )
            {
                return;
            }
            
            //
            // Orthogonalize active linear constraints:
            // * with respect to active bound constraints
            // * with respect to each other
            //
            apserv.rmatrixsetlengthatleast(ref activecm, nactive, n);
            apserv.rvectorsetlengthatleast(ref activecr, nactive);
            ridx = 0;
            for(i=0; i<=ctotal-1; i++)
            {
                if( activelin[i] )
                {
                    for(i_=0; i_<=n-1;i_++)
                    {
                        activecm[ridx,i_] = cleic[i,i_];
                    }
                    activecr[ridx] = cleic[i,n];
                    ridx = ridx+1;
                }
            }
            for(i=0; i<=nactive-1; i++)
            {
                
                //
                // Calculate row norm before transformation.
                // Zero rows are skipped
                //
                norm0 = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    norm0 += activecm[i,i_]*activecm[i,i_];
                }
                norm0 = Math.Sqrt(norm0);
                if( (double)(norm0)==(double)(0) )
                {
                    continue;
                }
                
                //
                // Orthogonalize I-th row with respect to boundary constraints
                //
                for(j=0; j<=n-1; j++)
                {
                    if( activeb[j] )
                    {
                        activecr[i] = activecr[i]-activecm[i,j]*xc[j];
                        activecm[i,j] = 0.0;
                    }
                }
                
                //
                // Orthogonalize I-th row with respect to previous linear constraints
                //
                for(k=0; k<=i-1; k++)
                {
                    v = 0.0;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        v += activecm[k,i_]*activecm[i,i_];
                    }
                    for(i_=0; i_<=n-1;i_++)
                    {
                        activecm[i,i_] = activecm[i,i_] - v*activecm[k,i_];
                    }
                    activecr[i] = activecr[i]-v*activecr[k];
                }
                
                //
                // Calculate row norm after transformation.
                // Rows which become almost zero (when compared with their
                // magnitude before orthogonalization) are artificially
                // zeroed.
                //
                norm1 = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    norm1 += activecm[i,i_]*activecm[i,i_];
                }
                norm1 = Math.Sqrt(norm1);
                if( (double)(norm1)>(double)(1.0E4*math.machineepsilon*norm0) )
                {
                    v = 1/norm1;
                    for(i_=0; i_<=n-1;i_++)
                    {
                        activecm[i,i_] = v*activecm[i,i_];
                    }
                    activecr[i] = activecr[i]*v;
                }
                else
                {
                    for(j=0; j<=n-1; j++)
                    {
                        activecm[i,j] = 0;
                    }
                    activecr[i] = 0;
                }
            }
        }


    }
    public class minlm
    {
        /*************************************************************************
        Levenberg-Marquardt optimizer.

        This structure should be created using one of the MinLMCreate???()
        functions. You should not access its fields directly; use ALGLIB functions
        to work with it.
        *************************************************************************/
        public class minlmstate
        {
            public int n;
            public int m;
            public double diffstep;
            public double epsg;
            public double epsf;
            public double epsx;
            public int maxits;
            public bool xrep;
            public double stpmax;
            public int maxmodelage;
            public bool makeadditers;
            public double[] x;
            public double f;
            public double[] fi;
            public double[,] j;
            public double[,] h;
            public double[] g;
            public bool needf;
            public bool needfg;
            public bool needfgh;
            public bool needfij;
            public bool needfi;
            public bool xupdated;
            public int algomode;
            public bool hasf;
            public bool hasfi;
            public bool hasg;
            public double[] xbase;
            public double fbase;
            public double[] fibase;
            public double[] gbase;
            public double[,] quadraticmodel;
            public double[] bndl;
            public double[] bndu;
            public bool[] havebndl;
            public bool[] havebndu;
            public double[] s;
            public double lambdav;
            public double nu;
            public int modelage;
            public double[] xdir;
            public double[] deltax;
            public double[] deltaf;
            public bool deltaxready;
            public bool deltafready;
            public double teststep;
            public int repiterationscount;
            public int repterminationtype;
            public int repfuncidx;
            public int repvaridx;
            public int repnfunc;
            public int repnjac;
            public int repngrad;
            public int repnhess;
            public int repncholesky;
            public rcommstate rstate;
            public double[] choleskybuf;
            public double[] tmp0;
            public double actualdecrease;
            public double predicteddecrease;
            public double xm1;
            public double xp1;
            public double[] fm1;
            public double[] fp1;
            public double[] fc1;
            public double[] gm1;
            public double[] gp1;
            public double[] gc1;
            public minlbfgs.minlbfgsstate internalstate;
            public minlbfgs.minlbfgsreport internalrep;
            public minqp.minqpstate qpstate;
            public minqp.minqpreport qprep;
            public minlmstate()
            {
                x = new double[0];
                fi = new double[0];
                j = new double[0,0];
                h = new double[0,0];
                g = new double[0];
                xbase = new double[0];
                fibase = new double[0];
                gbase = new double[0];
                quadraticmodel = new double[0,0];
                bndl = new double[0];
                bndu = new double[0];
                havebndl = new bool[0];
                havebndu = new bool[0];
                s = new double[0];
                xdir = new double[0];
                deltax = new double[0];
                deltaf = new double[0];
                rstate = new rcommstate();
                choleskybuf = new double[0];
                tmp0 = new double[0];
                fm1 = new double[0];
                fp1 = new double[0];
                fc1 = new double[0];
                gm1 = new double[0];
                gp1 = new double[0];
                gc1 = new double[0];
                internalstate = new minlbfgs.minlbfgsstate();
                internalrep = new minlbfgs.minlbfgsreport();
                qpstate = new minqp.minqpstate();
                qprep = new minqp.minqpreport();
            }
        };


        /*************************************************************************
        Optimization report, filled by MinLMResults() function

        FIELDS:
        * TerminationType, completetion code:
            * -7    derivative correctness check failed;
                    see Rep.WrongNum, Rep.WrongI, Rep.WrongJ for
                    more information.
            *  1    relative function improvement is no more than
                    EpsF.
            *  2    relative step is no more than EpsX.
            *  4    gradient is no more than EpsG.
            *  5    MaxIts steps was taken
            *  7    stopping conditions are too stringent,
                    further improvement is impossible
        * IterationsCount, contains iterations count
        * NFunc, number of function calculations
        * NJac, number of Jacobi matrix calculations
        * NGrad, number of gradient calculations
        * NHess, number of Hessian calculations
        * NCholesky, number of Cholesky decomposition calculations
        *************************************************************************/
        public class minlmreport
        {
            public int iterationscount;
            public int terminationtype;
            public int funcidx;
            public int varidx;
            public int nfunc;
            public int njac;
            public int ngrad;
            public int nhess;
            public int ncholesky;
        };




        public const int lmmodefj = 0;
        public const int lmmodefgj = 1;
        public const int lmmodefgh = 2;
        public const int lmflagnoprelbfgs = 1;
        public const int lmflagnointlbfgs = 2;
        public const int lmprelbfgsm = 5;
        public const int lmintlbfgsits = 5;
        public const int lbfgsnorealloc = 1;
        public const double lambdaup = 2.0;
        public const double lambdadown = 0.33;
        public const double suspiciousnu = 16;
        public const int smallmodelage = 3;
        public const int additers = 5;


        /*************************************************************************
                        IMPROVED LEVENBERG-MARQUARDT METHOD FOR
                         NON-LINEAR LEAST SQUARES OPTIMIZATION

        DESCRIPTION:
        This function is used to find minimum of function which is represented  as
        sum of squares:
            F(x) = f[0]^2(x[0],...,x[n-1]) + ... + f[m-1]^2(x[0],...,x[n-1])
        using value of function vector f[] and Jacobian of f[].


        REQUIREMENTS:
        This algorithm will request following information during its operation:

        * function vector f[] at given point X
        * function vector f[] and Jacobian of f[] (simultaneously) at given point

        There are several overloaded versions of  MinLMOptimize()  function  which
        correspond  to  different LM-like optimization algorithms provided by this
        unit. You should choose version which accepts fvec()  and jac() callbacks.
        First  one  is used to calculate f[] at given point, second one calculates
        f[] and Jacobian df[i]/dx[j].

        You can try to initialize MinLMState structure with VJ  function and  then
        use incorrect version  of  MinLMOptimize()  (for  example,  version  which
        works  with  general  form function and does not provide Jacobian), but it
        will  lead  to  exception  being  thrown  after first attempt to calculate
        Jacobian.


        USAGE:
        1. User initializes algorithm state with MinLMCreateVJ() call
        2. User tunes solver parameters with MinLMSetCond(),  MinLMSetStpMax() and
           other functions
        3. User calls MinLMOptimize() function which  takes algorithm  state   and
           callback functions.
        4. User calls MinLMResults() to get solution
        5. Optionally, user may call MinLMRestartFrom() to solve  another  problem
           with same N/M but another starting point and/or another function.
           MinLMRestartFrom() allows to reuse already initialized structure.


        INPUT PARAMETERS:
            N       -   dimension, N>1
                        * if given, only leading N elements of X are used
                        * if not given, automatically determined from size of X
            M       -   number of functions f[i]
            X       -   initial solution, array[0..N-1]

        OUTPUT PARAMETERS:
            State   -   structure which stores algorithm state

        NOTES:
        1. you may tune stopping conditions with MinLMSetCond() function
        2. if target function contains exp() or other fast growing functions,  and
           optimization algorithm makes too large steps which leads  to  overflow,
           use MinLMSetStpMax() function to bound algorithm's steps.

          -- ALGLIB --
             Copyright 30.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmcreatevj(int n,
            int m,
            double[] x,
            minlmstate state)
        {
            alglib.ap.assert(n>=1, "MinLMCreateVJ: N<1!");
            alglib.ap.assert(m>=1, "MinLMCreateVJ: M<1!");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinLMCreateVJ: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinLMCreateVJ: X contains infinite or NaN values!");
            
            //
            // initialize, check parameters
            //
            state.teststep = 0;
            state.n = n;
            state.m = m;
            state.algomode = 1;
            state.hasf = false;
            state.hasfi = true;
            state.hasg = false;
            
            //
            // second stage of initialization
            //
            lmprepare(n, m, false, state);
            minlmsetacctype(state, 0);
            minlmsetcond(state, 0, 0, 0, 0);
            minlmsetxrep(state, false);
            minlmsetstpmax(state, 0);
            minlmrestartfrom(state, x);
        }


        /*************************************************************************
                        IMPROVED LEVENBERG-MARQUARDT METHOD FOR
                         NON-LINEAR LEAST SQUARES OPTIMIZATION

        DESCRIPTION:
        This function is used to find minimum of function which is represented  as
        sum of squares:
            F(x) = f[0]^2(x[0],...,x[n-1]) + ... + f[m-1]^2(x[0],...,x[n-1])
        using value of function vector f[] only. Finite differences  are  used  to
        calculate Jacobian.


        REQUIREMENTS:
        This algorithm will request following information during its operation:
        * function vector f[] at given point X

        There are several overloaded versions of  MinLMOptimize()  function  which
        correspond  to  different LM-like optimization algorithms provided by this
        unit. You should choose version which accepts fvec() callback.

        You can try to initialize MinLMState structure with VJ  function and  then
        use incorrect version  of  MinLMOptimize()  (for  example,  version  which
        works with general form function and does not accept function vector), but
        it will  lead  to  exception being thrown after first attempt to calculate
        Jacobian.


        USAGE:
        1. User initializes algorithm state with MinLMCreateV() call
        2. User tunes solver parameters with MinLMSetCond(),  MinLMSetStpMax() and
           other functions
        3. User calls MinLMOptimize() function which  takes algorithm  state   and
           callback functions.
        4. User calls MinLMResults() to get solution
        5. Optionally, user may call MinLMRestartFrom() to solve  another  problem
           with same N/M but another starting point and/or another function.
           MinLMRestartFrom() allows to reuse already initialized structure.


        INPUT PARAMETERS:
            N       -   dimension, N>1
                        * if given, only leading N elements of X are used
                        * if not given, automatically determined from size of X
            M       -   number of functions f[i]
            X       -   initial solution, array[0..N-1]
            DiffStep-   differentiation step, >0

        OUTPUT PARAMETERS:
            State   -   structure which stores algorithm state

        See also MinLMIteration, MinLMResults.

        NOTES:
        1. you may tune stopping conditions with MinLMSetCond() function
        2. if target function contains exp() or other fast growing functions,  and
           optimization algorithm makes too large steps which leads  to  overflow,
           use MinLMSetStpMax() function to bound algorithm's steps.

          -- ALGLIB --
             Copyright 30.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmcreatev(int n,
            int m,
            double[] x,
            double diffstep,
            minlmstate state)
        {
            alglib.ap.assert(math.isfinite(diffstep), "MinLMCreateV: DiffStep is not finite!");
            alglib.ap.assert((double)(diffstep)>(double)(0), "MinLMCreateV: DiffStep<=0!");
            alglib.ap.assert(n>=1, "MinLMCreateV: N<1!");
            alglib.ap.assert(m>=1, "MinLMCreateV: M<1!");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinLMCreateV: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinLMCreateV: X contains infinite or NaN values!");
            
            //
            // Initialize
            //
            state.teststep = 0;
            state.n = n;
            state.m = m;
            state.algomode = 0;
            state.hasf = false;
            state.hasfi = true;
            state.hasg = false;
            state.diffstep = diffstep;
            
            //
            // Second stage of initialization
            //
            lmprepare(n, m, false, state);
            minlmsetacctype(state, 1);
            minlmsetcond(state, 0, 0, 0, 0);
            minlmsetxrep(state, false);
            minlmsetstpmax(state, 0);
            minlmrestartfrom(state, x);
        }


        /*************************************************************************
            LEVENBERG-MARQUARDT-LIKE METHOD FOR NON-LINEAR OPTIMIZATION

        DESCRIPTION:
        This  function  is  used  to  find  minimum  of general form (not "sum-of-
        -squares") function
            F = F(x[0], ..., x[n-1])
        using  its  gradient  and  Hessian.  Levenberg-Marquardt modification with
        L-BFGS pre-optimization and internal pre-conditioned  L-BFGS  optimization
        after each Levenberg-Marquardt step is used.


        REQUIREMENTS:
        This algorithm will request following information during its operation:

        * function value F at given point X
        * F and gradient G (simultaneously) at given point X
        * F, G and Hessian H (simultaneously) at given point X

        There are several overloaded versions of  MinLMOptimize()  function  which
        correspond  to  different LM-like optimization algorithms provided by this
        unit. You should choose version which accepts func(),  grad()  and  hess()
        function pointers. First pointer is used to calculate F  at  given  point,
        second  one  calculates  F(x)  and  grad F(x),  third one calculates F(x),
        grad F(x), hess F(x).

        You can try to initialize MinLMState structure with FGH-function and  then
        use incorrect version of MinLMOptimize() (for example, version which  does
        not provide Hessian matrix), but it will lead to  exception  being  thrown
        after first attempt to calculate Hessian.


        USAGE:
        1. User initializes algorithm state with MinLMCreateFGH() call
        2. User tunes solver parameters with MinLMSetCond(),  MinLMSetStpMax() and
           other functions
        3. User calls MinLMOptimize() function which  takes algorithm  state   and
           pointers (delegates, etc.) to callback functions.
        4. User calls MinLMResults() to get solution
        5. Optionally, user may call MinLMRestartFrom() to solve  another  problem
           with same N but another starting point and/or another function.
           MinLMRestartFrom() allows to reuse already initialized structure.


        INPUT PARAMETERS:
            N       -   dimension, N>1
                        * if given, only leading N elements of X are used
                        * if not given, automatically determined from size of X
            X       -   initial solution, array[0..N-1]

        OUTPUT PARAMETERS:
            State   -   structure which stores algorithm state

        NOTES:
        1. you may tune stopping conditions with MinLMSetCond() function
        2. if target function contains exp() or other fast growing functions,  and
           optimization algorithm makes too large steps which leads  to  overflow,
           use MinLMSetStpMax() function to bound algorithm's steps.

          -- ALGLIB --
             Copyright 30.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmcreatefgh(int n,
            double[] x,
            minlmstate state)
        {
            alglib.ap.assert(n>=1, "MinLMCreateFGH: N<1!");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinLMCreateFGH: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinLMCreateFGH: X contains infinite or NaN values!");
            
            //
            // initialize
            //
            state.teststep = 0;
            state.n = n;
            state.m = 0;
            state.algomode = 2;
            state.hasf = true;
            state.hasfi = false;
            state.hasg = true;
            
            //
            // init2
            //
            lmprepare(n, 0, true, state);
            minlmsetacctype(state, 2);
            minlmsetcond(state, 0, 0, 0, 0);
            minlmsetxrep(state, false);
            minlmsetstpmax(state, 0);
            minlmrestartfrom(state, x);
        }


        /*************************************************************************
        This function sets stopping conditions for Levenberg-Marquardt optimization
        algorithm.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            EpsG    -   >=0
                        The  subroutine  finishes  its  work   if   the  condition
                        |v|<EpsG is satisfied, where:
                        * |.| means Euclidian norm
                        * v - scaled gradient vector, v[i]=g[i]*s[i]
                        * g - gradient
                        * s - scaling coefficients set by MinLMSetScale()
            EpsF    -   >=0
                        The  subroutine  finishes  its work if on k+1-th iteration
                        the  condition  |F(k+1)-F(k)|<=EpsF*max{|F(k)|,|F(k+1)|,1}
                        is satisfied.
            EpsX    -   >=0
                        The subroutine finishes its work if  on  k+1-th  iteration
                        the condition |v|<=EpsX is fulfilled, where:
                        * |.| means Euclidian norm
                        * v - scaled step vector, v[i]=dx[i]/s[i]
                        * dx - ste pvector, dx=X(k+1)-X(k)
                        * s - scaling coefficients set by MinLMSetScale()
            MaxIts  -   maximum number of iterations. If MaxIts=0, the  number  of
                        iterations   is    unlimited.   Only   Levenberg-Marquardt
                        iterations  are  counted  (L-BFGS/CG  iterations  are  NOT
                        counted because their cost is very low compared to that of
                        LM).

        Passing EpsG=0, EpsF=0, EpsX=0 and MaxIts=0 (simultaneously) will lead to
        automatic stopping criterion selection (small EpsX).

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmsetcond(minlmstate state,
            double epsg,
            double epsf,
            double epsx,
            int maxits)
        {
            alglib.ap.assert(math.isfinite(epsg), "MinLMSetCond: EpsG is not finite number!");
            alglib.ap.assert((double)(epsg)>=(double)(0), "MinLMSetCond: negative EpsG!");
            alglib.ap.assert(math.isfinite(epsf), "MinLMSetCond: EpsF is not finite number!");
            alglib.ap.assert((double)(epsf)>=(double)(0), "MinLMSetCond: negative EpsF!");
            alglib.ap.assert(math.isfinite(epsx), "MinLMSetCond: EpsX is not finite number!");
            alglib.ap.assert((double)(epsx)>=(double)(0), "MinLMSetCond: negative EpsX!");
            alglib.ap.assert(maxits>=0, "MinLMSetCond: negative MaxIts!");
            if( (((double)(epsg)==(double)(0) && (double)(epsf)==(double)(0)) && (double)(epsx)==(double)(0)) && maxits==0 )
            {
                epsx = 1.0E-6;
            }
            state.epsg = epsg;
            state.epsf = epsf;
            state.epsx = epsx;
            state.maxits = maxits;
        }


        /*************************************************************************
        This function turns on/off reporting.

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            NeedXRep-   whether iteration reports are needed or not

        If NeedXRep is True, algorithm will call rep() callback function if  it is
        provided to MinLMOptimize(). Both Levenberg-Marquardt and internal  L-BFGS
        iterations are reported.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmsetxrep(minlmstate state,
            bool needxrep)
        {
            state.xrep = needxrep;
        }


        /*************************************************************************
        This function sets maximum step length

        INPUT PARAMETERS:
            State   -   structure which stores algorithm state
            StpMax  -   maximum step length, >=0. Set StpMax to 0.0,  if you don't
                        want to limit step length.

        Use this subroutine when you optimize target function which contains exp()
        or  other  fast  growing  functions,  and optimization algorithm makes too
        large  steps  which  leads  to overflow. This function allows us to reject
        steps  that  are  too  large  (and  therefore  expose  us  to the possible
        overflow) without actually calculating function value at the x+stp*d.

        NOTE: non-zero StpMax leads to moderate  performance  degradation  because
        intermediate  step  of  preconditioned L-BFGS optimization is incompatible
        with limits on step size.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmsetstpmax(minlmstate state,
            double stpmax)
        {
            alglib.ap.assert(math.isfinite(stpmax), "MinLMSetStpMax: StpMax is not finite!");
            alglib.ap.assert((double)(stpmax)>=(double)(0), "MinLMSetStpMax: StpMax<0!");
            state.stpmax = stpmax;
        }


        /*************************************************************************
        This function sets scaling coefficients for LM optimizer.

        ALGLIB optimizers use scaling matrices to test stopping  conditions  (step
        size and gradient are scaled before comparison with tolerances).  Scale of
        the I-th variable is a translation invariant measure of:
        a) "how large" the variable is
        b) how large the step should be to make significant changes in the function

        Generally, scale is NOT considered to be a form of preconditioner.  But LM
        optimizer is unique in that it uses scaling matrix both  in  the  stopping
        condition tests and as Marquardt damping factor.

        Proper scaling is very important for the algorithm performance. It is less
        important for the quality of results, but still has some influence (it  is
        easier  to  converge  when  variables  are  properly  scaled, so premature
        stopping is possible when very badly scalled variables are  combined  with
        relaxed stopping conditions).

        INPUT PARAMETERS:
            State   -   structure stores algorithm state
            S       -   array[N], non-zero scaling coefficients
                        S[i] may be negative, sign doesn't matter.

          -- ALGLIB --
             Copyright 14.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmsetscale(minlmstate state,
            double[] s)
        {
            int i = 0;

            alglib.ap.assert(alglib.ap.len(s)>=state.n, "MinLMSetScale: Length(S)<N");
            for(i=0; i<=state.n-1; i++)
            {
                alglib.ap.assert(math.isfinite(s[i]), "MinLMSetScale: S contains infinite or NAN elements");
                alglib.ap.assert((double)(s[i])!=(double)(0), "MinLMSetScale: S contains zero elements");
                state.s[i] = Math.Abs(s[i]);
            }
        }


        /*************************************************************************
        This function sets boundary constraints for LM optimizer

        Boundary constraints are inactive by default (after initial creation).
        They are preserved until explicitly turned off with another SetBC() call.

        INPUT PARAMETERS:
            State   -   structure stores algorithm state
            BndL    -   lower bounds, array[N].
                        If some (all) variables are unbounded, you may specify
                        very small number or -INF (latter is recommended because
                        it will allow solver to use better algorithm).
            BndU    -   upper bounds, array[N].
                        If some (all) variables are unbounded, you may specify
                        very large number or +INF (latter is recommended because
                        it will allow solver to use better algorithm).

        NOTE 1: it is possible to specify BndL[i]=BndU[i]. In this case I-th
        variable will be "frozen" at X[i]=BndL[i]=BndU[i].

        NOTE 2: this solver has following useful properties:
        * bound constraints are always satisfied exactly
        * function is evaluated only INSIDE area specified by bound constraints
          or at its boundary

          -- ALGLIB --
             Copyright 14.01.2011 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmsetbc(minlmstate state,
            double[] bndl,
            double[] bndu)
        {
            int i = 0;
            int n = 0;

            n = state.n;
            alglib.ap.assert(alglib.ap.len(bndl)>=n, "MinLMSetBC: Length(BndL)<N");
            alglib.ap.assert(alglib.ap.len(bndu)>=n, "MinLMSetBC: Length(BndU)<N");
            for(i=0; i<=n-1; i++)
            {
                alglib.ap.assert(math.isfinite(bndl[i]) || Double.IsNegativeInfinity(bndl[i]), "MinLMSetBC: BndL contains NAN or +INF");
                alglib.ap.assert(math.isfinite(bndu[i]) || Double.IsPositiveInfinity(bndu[i]), "MinLMSetBC: BndU contains NAN or -INF");
                state.bndl[i] = bndl[i];
                state.havebndl[i] = math.isfinite(bndl[i]);
                state.bndu[i] = bndu[i];
                state.havebndu[i] = math.isfinite(bndu[i]);
            }
        }


        /*************************************************************************
        This function is used to change acceleration settings

        You can choose between three acceleration strategies:
        * AccType=0, no acceleration.
        * AccType=1, secant updates are used to update quadratic model after  each
          iteration. After fixed number of iterations (or after  model  breakdown)
          we  recalculate  quadratic  model  using  analytic  Jacobian  or  finite
          differences. Number of secant-based iterations depends  on  optimization
          settings: about 3 iterations - when we have analytic Jacobian, up to 2*N
          iterations - when we use finite differences to calculate Jacobian.

        AccType=1 is recommended when Jacobian  calculation  cost  is  prohibitive
        high (several Mx1 function vector calculations  followed  by  several  NxN
        Cholesky factorizations are faster than calculation of one M*N  Jacobian).
        It should also be used when we have no Jacobian, because finite difference
        approximation takes too much time to compute.

        Table below list  optimization  protocols  (XYZ  protocol  corresponds  to
        MinLMCreateXYZ) and acceleration types they support (and use by  default).

        ACCELERATION TYPES SUPPORTED BY OPTIMIZATION PROTOCOLS:

        protocol    0   1   comment
        V           +   +
        VJ          +   +
        FGH         +

        DAFAULT VALUES:

        protocol    0   1   comment
        V               x   without acceleration it is so slooooooooow
        VJ          x
        FGH         x

        NOTE: this  function should be called before optimization. Attempt to call
        it during algorithm iterations may result in unexpected behavior.

        NOTE: attempt to call this function with unsupported protocol/acceleration
        combination will result in exception being thrown.

          -- ALGLIB --
             Copyright 14.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmsetacctype(minlmstate state,
            int acctype)
        {
            alglib.ap.assert((acctype==0 || acctype==1) || acctype==2, "MinLMSetAccType: incorrect AccType!");
            if( acctype==2 )
            {
                acctype = 0;
            }
            if( acctype==0 )
            {
                state.maxmodelage = 0;
                state.makeadditers = false;
                return;
            }
            if( acctype==1 )
            {
                alglib.ap.assert(state.hasfi, "MinLMSetAccType: AccType=1 is incompatible with current protocol!");
                if( state.algomode==0 )
                {
                    state.maxmodelage = 2*state.n;
                }
                else
                {
                    state.maxmodelage = smallmodelage;
                }
                state.makeadditers = false;
                return;
            }
        }


        /*************************************************************************
        NOTES:

        1. Depending on function used to create state  structure,  this  algorithm
           may accept Jacobian and/or Hessian and/or gradient.  According  to  the
           said above, there ase several versions of this function,  which  accept
           different sets of callbacks.

           This flexibility opens way to subtle errors - you may create state with
           MinLMCreateFGH() (optimization using Hessian), but call function  which
           does not accept Hessian. So when algorithm will request Hessian,  there
           will be no callback to call. In this case exception will be thrown.

           Be careful to avoid such errors because there is no way to find them at
           compile time - you can see them at runtime only.

          -- ALGLIB --
             Copyright 10.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static bool minlmiteration(minlmstate state)
        {
            bool result = new bool();
            int n = 0;
            int m = 0;
            bool bflag = new bool();
            int iflag = 0;
            double v = 0;
            double s = 0;
            double t = 0;
            int i = 0;
            int k = 0;
            int i_ = 0;

            
            //
            // Reverse communication preparations
            // I know it looks ugly, but it works the same way
            // anywhere from C++ to Python.
            //
            // This code initializes locals by:
            // * random values determined during code
            //   generation - on first subroutine call
            // * values from previous call - on subsequent calls
            //
            if( state.rstate.stage>=0 )
            {
                n = state.rstate.ia[0];
                m = state.rstate.ia[1];
                iflag = state.rstate.ia[2];
                i = state.rstate.ia[3];
                k = state.rstate.ia[4];
                bflag = state.rstate.ba[0];
                v = state.rstate.ra[0];
                s = state.rstate.ra[1];
                t = state.rstate.ra[2];
            }
            else
            {
                n = -983;
                m = -989;
                iflag = -834;
                i = 900;
                k = -287;
                bflag = false;
                v = 214;
                s = -338;
                t = -686;
            }
            if( state.rstate.stage==0 )
            {
                goto lbl_0;
            }
            if( state.rstate.stage==1 )
            {
                goto lbl_1;
            }
            if( state.rstate.stage==2 )
            {
                goto lbl_2;
            }
            if( state.rstate.stage==3 )
            {
                goto lbl_3;
            }
            if( state.rstate.stage==4 )
            {
                goto lbl_4;
            }
            if( state.rstate.stage==5 )
            {
                goto lbl_5;
            }
            if( state.rstate.stage==6 )
            {
                goto lbl_6;
            }
            if( state.rstate.stage==7 )
            {
                goto lbl_7;
            }
            if( state.rstate.stage==8 )
            {
                goto lbl_8;
            }
            if( state.rstate.stage==9 )
            {
                goto lbl_9;
            }
            if( state.rstate.stage==10 )
            {
                goto lbl_10;
            }
            if( state.rstate.stage==11 )
            {
                goto lbl_11;
            }
            if( state.rstate.stage==12 )
            {
                goto lbl_12;
            }
            if( state.rstate.stage==13 )
            {
                goto lbl_13;
            }
            if( state.rstate.stage==14 )
            {
                goto lbl_14;
            }
            if( state.rstate.stage==15 )
            {
                goto lbl_15;
            }
            if( state.rstate.stage==16 )
            {
                goto lbl_16;
            }
            if( state.rstate.stage==17 )
            {
                goto lbl_17;
            }
            if( state.rstate.stage==18 )
            {
                goto lbl_18;
            }
            
            //
            // Routine body
            //
            
            //
            // prepare
            //
            n = state.n;
            m = state.m;
            state.repiterationscount = 0;
            state.repterminationtype = 0;
            state.repfuncidx = -1;
            state.repvaridx = -1;
            state.repnfunc = 0;
            state.repnjac = 0;
            state.repngrad = 0;
            state.repnhess = 0;
            state.repncholesky = 0;
            
            //
            // check consistency of constraints,
            // enforce feasibility of the solution
            // set constraints
            //
            if( !optserv.enforceboundaryconstraints(ref state.xbase, state.bndl, state.havebndl, state.bndu, state.havebndu, n, 0) )
            {
                state.repterminationtype = -3;
                result = false;
                return result;
            }
            minqp.minqpsetbc(state.qpstate, state.bndl, state.bndu);
            
            //
            //  Check, that transferred derivative value is right
            //
            clearrequestfields(state);
            if( !(state.algomode==1 && (double)(state.teststep)>(double)(0)) )
            {
                goto lbl_19;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            state.needfij = true;
            i = 0;
        lbl_21:
            if( i>n-1 )
            {
                goto lbl_23;
            }
            alglib.ap.assert((state.havebndl[i] && (double)(state.bndl[i])<=(double)(state.x[i])) || !state.havebndl[i], "MinLM: internal error(State.X is out of bounds)");
            alglib.ap.assert((state.havebndu[i] && (double)(state.x[i])<=(double)(state.bndu[i])) || !state.havebndu[i], "MinLMIteration: internal error(State.X is out of bounds)");
            v = state.x[i];
            state.x[i] = v-state.teststep*state.s[i];
            if( state.havebndl[i] )
            {
                state.x[i] = Math.Max(state.x[i], state.bndl[i]);
            }
            state.xm1 = state.x[i];
            state.rstate.stage = 0;
            goto lbl_rcomm;
        lbl_0:
            for(i_=0; i_<=m-1;i_++)
            {
                state.fm1[i_] = state.fi[i_];
            }
            for(i_=0; i_<=m-1;i_++)
            {
                state.gm1[i_] = state.j[i_,i];
            }
            state.x[i] = v+state.teststep*state.s[i];
            if( state.havebndu[i] )
            {
                state.x[i] = Math.Min(state.x[i], state.bndu[i]);
            }
            state.xp1 = state.x[i];
            state.rstate.stage = 1;
            goto lbl_rcomm;
        lbl_1:
            for(i_=0; i_<=m-1;i_++)
            {
                state.fp1[i_] = state.fi[i_];
            }
            for(i_=0; i_<=m-1;i_++)
            {
                state.gp1[i_] = state.j[i_,i];
            }
            state.x[i] = (state.xm1+state.xp1)/2;
            if( state.havebndl[i] )
            {
                state.x[i] = Math.Max(state.x[i], state.bndl[i]);
            }
            if( state.havebndu[i] )
            {
                state.x[i] = Math.Min(state.x[i], state.bndu[i]);
            }
            state.rstate.stage = 2;
            goto lbl_rcomm;
        lbl_2:
            for(i_=0; i_<=m-1;i_++)
            {
                state.fc1[i_] = state.fi[i_];
            }
            for(i_=0; i_<=m-1;i_++)
            {
                state.gc1[i_] = state.j[i_,i];
            }
            state.x[i] = v;
            for(k=0; k<=m-1; k++)
            {
                if( !optserv.derivativecheck(state.fm1[k], state.gm1[k], state.fp1[k], state.gp1[k], state.fc1[k], state.gc1[k], state.xp1-state.xm1) )
                {
                    state.repfuncidx = k;
                    state.repvaridx = i;
                    state.repterminationtype = -7;
                    result = false;
                    return result;
                }
            }
            i = i+1;
            goto lbl_21;
        lbl_23:
            state.needfij = false;
        lbl_19:
            
            //
            // Initial report of current point
            //
            // Note 1: we rewrite State.X twice because
            // user may accidentally change it after first call.
            //
            // Note 2: we set NeedF or NeedFI depending on what
            // information about function we have.
            //
            if( !state.xrep )
            {
                goto lbl_24;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            clearrequestfields(state);
            if( !state.hasf )
            {
                goto lbl_26;
            }
            state.needf = true;
            state.rstate.stage = 3;
            goto lbl_rcomm;
        lbl_3:
            state.needf = false;
            goto lbl_27;
        lbl_26:
            alglib.ap.assert(state.hasfi, "MinLM: internal error 2!");
            state.needfi = true;
            state.rstate.stage = 4;
            goto lbl_rcomm;
        lbl_4:
            state.needfi = false;
            v = 0.0;
            for(i_=0; i_<=m-1;i_++)
            {
                v += state.fi[i_]*state.fi[i_];
            }
            state.f = v;
        lbl_27:
            state.repnfunc = state.repnfunc+1;
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 5;
            goto lbl_rcomm;
        lbl_5:
            state.xupdated = false;
        lbl_24:
            
            //
            // Prepare control variables
            //
            state.nu = 1;
            state.lambdav = -math.maxrealnumber;
            state.modelage = state.maxmodelage+1;
            state.deltaxready = false;
            state.deltafready = false;
            
            //
            // Main cycle.
            //
            // We move through it until either:
            // * one of the stopping conditions is met
            // * we decide that stopping conditions are too stringent
            //   and break from cycle
            //
            //
        lbl_28:
            if( false )
            {
                goto lbl_29;
            }
            
            //
            // First, we have to prepare quadratic model for our function.
            // We use BFlag to ensure that model is prepared;
            // if it is false at the end of this block, something went wrong.
            //
            // We may either calculate brand new model or update old one.
            //
            // Before this block we have:
            // * State.XBase            - current position.
            // * State.DeltaX           - if DeltaXReady is True
            // * State.DeltaF           - if DeltaFReady is True
            //
            // After this block is over, we will have:
            // * State.XBase            - base point (unchanged)
            // * State.FBase            - F(XBase)
            // * State.GBase            - linear term
            // * State.QuadraticModel   - quadratic term
            // * State.LambdaV          - current estimate for lambda
            //
            // We also clear DeltaXReady/DeltaFReady flags
            // after initialization is done.
            //
            bflag = false;
            if( !(state.algomode==0 || state.algomode==1) )
            {
                goto lbl_30;
            }
            
            //
            // Calculate f[] and Jacobian
            //
            if( !(state.modelage>state.maxmodelage || !(state.deltaxready && state.deltafready)) )
            {
                goto lbl_32;
            }
            
            //
            // Refresh model (using either finite differences or analytic Jacobian)
            //
            if( state.algomode!=0 )
            {
                goto lbl_34;
            }
            
            //
            // Optimization using F values only.
            // Use finite differences to estimate Jacobian.
            //
            alglib.ap.assert(state.hasfi, "MinLMIteration: internal error when estimating Jacobian (no f[])");
            k = 0;
        lbl_36:
            if( k>n-1 )
            {
                goto lbl_38;
            }
            
            //
            // We guard X[k] from leaving [BndL,BndU].
            // In case BndL=BndU, we assume that derivative in this direction is zero.
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            state.x[k] = state.x[k]-state.s[k]*state.diffstep;
            if( state.havebndl[k] )
            {
                state.x[k] = Math.Max(state.x[k], state.bndl[k]);
            }
            if( state.havebndu[k] )
            {
                state.x[k] = Math.Min(state.x[k], state.bndu[k]);
            }
            state.xm1 = state.x[k];
            clearrequestfields(state);
            state.needfi = true;
            state.rstate.stage = 6;
            goto lbl_rcomm;
        lbl_6:
            state.repnfunc = state.repnfunc+1;
            for(i_=0; i_<=m-1;i_++)
            {
                state.fm1[i_] = state.fi[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            state.x[k] = state.x[k]+state.s[k]*state.diffstep;
            if( state.havebndl[k] )
            {
                state.x[k] = Math.Max(state.x[k], state.bndl[k]);
            }
            if( state.havebndu[k] )
            {
                state.x[k] = Math.Min(state.x[k], state.bndu[k]);
            }
            state.xp1 = state.x[k];
            clearrequestfields(state);
            state.needfi = true;
            state.rstate.stage = 7;
            goto lbl_rcomm;
        lbl_7:
            state.repnfunc = state.repnfunc+1;
            for(i_=0; i_<=m-1;i_++)
            {
                state.fp1[i_] = state.fi[i_];
            }
            v = state.xp1-state.xm1;
            if( (double)(v)!=(double)(0) )
            {
                v = 1/v;
                for(i_=0; i_<=m-1;i_++)
                {
                    state.j[i_,k] = v*state.fp1[i_];
                }
                for(i_=0; i_<=m-1;i_++)
                {
                    state.j[i_,k] = state.j[i_,k] - v*state.fm1[i_];
                }
            }
            else
            {
                for(i=0; i<=m-1; i++)
                {
                    state.j[i,k] = 0;
                }
            }
            k = k+1;
            goto lbl_36;
        lbl_38:
            
            //
            // Calculate F(XBase)
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            clearrequestfields(state);
            state.needfi = true;
            state.rstate.stage = 8;
            goto lbl_rcomm;
        lbl_8:
            state.needfi = false;
            state.repnfunc = state.repnfunc+1;
            state.repnjac = state.repnjac+1;
            
            //
            // New model
            //
            state.modelage = 0;
            goto lbl_35;
        lbl_34:
            
            //
            // Obtain f[] and Jacobian
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            clearrequestfields(state);
            state.needfij = true;
            state.rstate.stage = 9;
            goto lbl_rcomm;
        lbl_9:
            state.needfij = false;
            state.repnfunc = state.repnfunc+1;
            state.repnjac = state.repnjac+1;
            
            //
            // New model
            //
            state.modelage = 0;
        lbl_35:
            goto lbl_33;
        lbl_32:
            
            //
            // State.J contains Jacobian or its current approximation;
            // refresh it using secant updates:
            //
            // f(x0+dx) = f(x0) + J*dx,
            // J_new = J_old + u*h'
            // h = x_new-x_old
            // u = (f_new - f_old - J_old*h)/(h'h)
            //
            // We can explicitly generate h and u, but it is
            // preferential to do in-place calculations. Only
            // I-th row of J_old is needed to calculate u[I],
            // so we can update J row by row in one pass.
            //
            // NOTE: we expect that State.XBase contains new point,
            // State.FBase contains old point, State.DeltaX and
            // State.DeltaY contain updates from last step.
            //
            alglib.ap.assert(state.deltaxready && state.deltafready, "MinLMIteration: uninitialized DeltaX/DeltaF");
            t = 0.0;
            for(i_=0; i_<=n-1;i_++)
            {
                t += state.deltax[i_]*state.deltax[i_];
            }
            alglib.ap.assert((double)(t)!=(double)(0), "MinLM: internal error (T=0)");
            for(i=0; i<=m-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.j[i,i_]*state.deltax[i_];
                }
                v = (state.deltaf[i]-v)/t;
                for(i_=0; i_<=n-1;i_++)
                {
                    state.j[i,i_] = state.j[i,i_] + v*state.deltax[i_];
                }
            }
            for(i_=0; i_<=m-1;i_++)
            {
                state.fi[i_] = state.fibase[i_];
            }
            for(i_=0; i_<=m-1;i_++)
            {
                state.fi[i_] = state.fi[i_] + state.deltaf[i_];
            }
            
            //
            // Increase model age
            //
            state.modelage = state.modelage+1;
        lbl_33:
            
            //
            // Generate quadratic model:
            //     f(xbase+dx) =
            //       = (f0 + J*dx)'(f0 + J*dx)
            //       = f0^2 + dx'J'f0 + f0*J*dx + dx'J'J*dx
            //       = f0^2 + 2*f0*J*dx + dx'J'J*dx
            //
            // Note that we calculate 2*(J'J) instead of J'J because
            // our quadratic model is based on Tailor decomposition,
            // i.e. it has 0.5 before quadratic term.
            //
            ablas.rmatrixgemm(n, n, m, 2.0, state.j, 0, 0, 1, state.j, 0, 0, 0, 0.0, ref state.quadraticmodel, 0, 0);
            ablas.rmatrixmv(n, m, state.j, 0, 0, 1, state.fi, 0, ref state.gbase, 0);
            for(i_=0; i_<=n-1;i_++)
            {
                state.gbase[i_] = 2*state.gbase[i_];
            }
            v = 0.0;
            for(i_=0; i_<=m-1;i_++)
            {
                v += state.fi[i_]*state.fi[i_];
            }
            state.fbase = v;
            for(i_=0; i_<=m-1;i_++)
            {
                state.fibase[i_] = state.fi[i_];
            }
            
            //
            // set control variables
            //
            bflag = true;
        lbl_30:
            if( state.algomode!=2 )
            {
                goto lbl_39;
            }
            alglib.ap.assert(!state.hasfi, "MinLMIteration: internal error (HasFI is True in Hessian-based mode)");
            
            //
            // Obtain F, G, H
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            clearrequestfields(state);
            state.needfgh = true;
            state.rstate.stage = 10;
            goto lbl_rcomm;
        lbl_10:
            state.needfgh = false;
            state.repnfunc = state.repnfunc+1;
            state.repngrad = state.repngrad+1;
            state.repnhess = state.repnhess+1;
            ablas.rmatrixcopy(n, n, state.h, 0, 0, ref state.quadraticmodel, 0, 0);
            for(i_=0; i_<=n-1;i_++)
            {
                state.gbase[i_] = state.g[i_];
            }
            state.fbase = state.f;
            
            //
            // set control variables
            //
            bflag = true;
            state.modelage = 0;
        lbl_39:
            alglib.ap.assert(bflag, "MinLM: internal integrity check failed!");
            state.deltaxready = false;
            state.deltafready = false;
            
            //
            // If Lambda is not initialized, initialize it using quadratic model
            //
            if( (double)(state.lambdav)<(double)(0) )
            {
                state.lambdav = 0;
                for(i=0; i<=n-1; i++)
                {
                    state.lambdav = Math.Max(state.lambdav, Math.Abs(state.quadraticmodel[i,i])*math.sqr(state.s[i]));
                }
                state.lambdav = 0.001*state.lambdav;
                if( (double)(state.lambdav)==(double)(0) )
                {
                    state.lambdav = 1;
                }
            }
            
            //
            // Test stopping conditions for function gradient
            //
            if( (double)(boundedscaledantigradnorm(state, state.xbase, state.gbase))>(double)(state.epsg) )
            {
                goto lbl_41;
            }
            if( state.modelage!=0 )
            {
                goto lbl_43;
            }
            
            //
            // Model is fresh, we can rely on it and terminate algorithm
            //
            state.repterminationtype = 4;
            if( !state.xrep )
            {
                goto lbl_45;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            state.f = state.fbase;
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 11;
            goto lbl_rcomm;
        lbl_11:
            state.xupdated = false;
        lbl_45:
            result = false;
            return result;
            goto lbl_44;
        lbl_43:
            
            //
            // Model is not fresh, we should refresh it and test
            // conditions once more
            //
            state.modelage = state.maxmodelage+1;
            goto lbl_28;
        lbl_44:
        lbl_41:
            
            //
            // Find value of Levenberg-Marquardt damping parameter which:
            // * leads to positive definite damped model
            // * within bounds specified by StpMax
            // * generates step which decreases function value
            //
            // After this block IFlag is set to:
            // * -3, if constraints are infeasible
            // * -2, if model update is needed (either Lambda growth is too large
            //       or step is too short, but we can't rely on model and stop iterations)
            // * -1, if model is fresh, Lambda have grown too large, termination is needed
            // *  0, if everything is OK, continue iterations
            //
            // State.Nu can have any value on enter, but after exit it is set to 1.0
            //
            iflag = -99;
        lbl_47:
            if( false )
            {
                goto lbl_48;
            }
            
            //
            // Do we need model update?
            //
            if( state.modelage>0 && (double)(state.nu)>=(double)(suspiciousnu) )
            {
                iflag = -2;
                goto lbl_48;
            }
            
            //
            // Setup quadratic solver and solve quadratic programming problem.
            // After problem is solved we'll try to bound step by StpMax
            // (Lambda will be increased if step size is too large).
            //
            // We use BFlag variable to indicate that we have to increase Lambda.
            // If it is False, we will try to increase Lambda and move to new iteration.
            //
            bflag = true;
            minqp.minqpsetstartingpointfast(state.qpstate, state.xbase);
            minqp.minqpsetoriginfast(state.qpstate, state.xbase);
            minqp.minqpsetlineartermfast(state.qpstate, state.gbase);
            minqp.minqpsetquadratictermfast(state.qpstate, state.quadraticmodel, true, 0.0);
            for(i=0; i<=n-1; i++)
            {
                state.tmp0[i] = state.quadraticmodel[i,i]+state.lambdav/math.sqr(state.s[i]);
            }
            minqp.minqprewritediagonal(state.qpstate, state.tmp0);
            minqp.minqpoptimize(state.qpstate);
            minqp.minqpresultsbuf(state.qpstate, ref state.xdir, state.qprep);
            if( state.qprep.terminationtype>0 )
            {
                
                //
                // successful solution of QP problem
                //
                for(i_=0; i_<=n-1;i_++)
                {
                    state.xdir[i_] = state.xdir[i_] - state.xbase[i_];
                }
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.xdir[i_]*state.xdir[i_];
                }
                if( math.isfinite(v) )
                {
                    v = Math.Sqrt(v);
                    if( (double)(state.stpmax)>(double)(0) && (double)(v)>(double)(state.stpmax) )
                    {
                        bflag = false;
                    }
                }
                else
                {
                    bflag = false;
                }
            }
            else
            {
                
                //
                // Either problem is non-convex (increase LambdaV) or constraints are inconsistent
                //
                alglib.ap.assert(state.qprep.terminationtype==-3 || state.qprep.terminationtype==-5, "MinLM: unexpected completion code from QP solver");
                if( state.qprep.terminationtype==-3 )
                {
                    iflag = -3;
                    goto lbl_48;
                }
                bflag = false;
            }
            if( !bflag )
            {
                
                //
                // Solution failed:
                // try to increase lambda to make matrix positive definite and continue.
                //
                if( !increaselambda(ref state.lambdav, ref state.nu) )
                {
                    iflag = -1;
                    goto lbl_48;
                }
                goto lbl_47;
            }
            
            //
            // Step in State.XDir and it is bounded by StpMax.
            //
            // We should check stopping conditions on step size here.
            // DeltaX, which is used for secant updates, is initialized here.
            //
            // This code is a bit tricky because sometimes XDir<>0, but
            // it is so small that XDir+XBase==XBase (in finite precision
            // arithmetics). So we set DeltaX to XBase, then
            // add XDir, and then subtract XBase to get exact value of
            // DeltaX.
            //
            // Step length is estimated using DeltaX.
            //
            // NOTE: stopping conditions are tested
            // for fresh models only (ModelAge=0)
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.deltax[i_] = state.xbase[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.deltax[i_] = state.deltax[i_] + state.xdir[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.deltax[i_] = state.deltax[i_] - state.xbase[i_];
            }
            state.deltaxready = true;
            v = 0.0;
            for(i=0; i<=n-1; i++)
            {
                v = v+math.sqr(state.deltax[i]/state.s[i]);
            }
            v = Math.Sqrt(v);
            if( (double)(v)>(double)(state.epsx) )
            {
                goto lbl_49;
            }
            if( state.modelage!=0 )
            {
                goto lbl_51;
            }
            
            //
            // Step is too short, model is fresh and we can rely on it.
            // Terminating.
            //
            state.repterminationtype = 2;
            if( !state.xrep )
            {
                goto lbl_53;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            state.f = state.fbase;
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 12;
            goto lbl_rcomm;
        lbl_12:
            state.xupdated = false;
        lbl_53:
            result = false;
            return result;
            goto lbl_52;
        lbl_51:
            
            //
            // Step is suspiciously short, but model is not fresh
            // and we can't rely on it.
            //
            iflag = -2;
            goto lbl_48;
        lbl_52:
        lbl_49:
            
            //
            // Let's evaluate new step:
            // a) if we have Fi vector, we evaluate it using rcomm, and
            //    then we manually calculate State.F as sum of squares of Fi[]
            // b) if we have F value, we just evaluate it through rcomm interface
            //
            // We prefer (a) because we may need Fi vector for additional
            // iterations
            //
            alglib.ap.assert(state.hasfi || state.hasf, "MinLM: internal error 2!");
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.x[i_] + state.xdir[i_];
            }
            clearrequestfields(state);
            if( !state.hasfi )
            {
                goto lbl_55;
            }
            state.needfi = true;
            state.rstate.stage = 13;
            goto lbl_rcomm;
        lbl_13:
            state.needfi = false;
            v = 0.0;
            for(i_=0; i_<=m-1;i_++)
            {
                v += state.fi[i_]*state.fi[i_];
            }
            state.f = v;
            for(i_=0; i_<=m-1;i_++)
            {
                state.deltaf[i_] = state.fi[i_];
            }
            for(i_=0; i_<=m-1;i_++)
            {
                state.deltaf[i_] = state.deltaf[i_] - state.fibase[i_];
            }
            state.deltafready = true;
            goto lbl_56;
        lbl_55:
            state.needf = true;
            state.rstate.stage = 14;
            goto lbl_rcomm;
        lbl_14:
            state.needf = false;
        lbl_56:
            state.repnfunc = state.repnfunc+1;
            if( (double)(state.f)>=(double)(state.fbase) )
            {
                
                //
                // Increase lambda and continue
                //
                if( !increaselambda(ref state.lambdav, ref state.nu) )
                {
                    iflag = -1;
                    goto lbl_48;
                }
                goto lbl_47;
            }
            
            //
            // We've found our step!
            //
            iflag = 0;
            goto lbl_48;
            goto lbl_47;
        lbl_48:
            state.nu = 1;
            alglib.ap.assert(iflag>=-3 && iflag<=0, "MinLM: internal integrity check failed!");
            if( iflag==-3 )
            {
                state.repterminationtype = -3;
                result = false;
                return result;
            }
            if( iflag==-2 )
            {
                state.modelage = state.maxmodelage+1;
                goto lbl_28;
            }
            if( iflag==-1 )
            {
                goto lbl_29;
            }
            
            //
            // Levenberg-Marquardt step is ready.
            // Compare predicted vs. actual decrease and decide what to do with lambda.
            //
            // NOTE: we expect that State.DeltaX contains direction of step,
            // State.F contains function value at new point.
            //
            alglib.ap.assert(state.deltaxready, "MinLM: deltaX is not ready");
            t = 0;
            for(i=0; i<=n-1; i++)
            {
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.quadraticmodel[i,i_]*state.deltax[i_];
                }
                t = t+state.deltax[i]*state.gbase[i]+0.5*state.deltax[i]*v;
            }
            state.predicteddecrease = -t;
            state.actualdecrease = -(state.f-state.fbase);
            if( (double)(state.predicteddecrease)<=(double)(0) )
            {
                goto lbl_29;
            }
            v = state.actualdecrease/state.predicteddecrease;
            if( (double)(v)>=(double)(0.1) )
            {
                goto lbl_57;
            }
            if( increaselambda(ref state.lambdav, ref state.nu) )
            {
                goto lbl_59;
            }
            
            //
            // Lambda is too large, we have to break iterations.
            //
            state.repterminationtype = 7;
            if( !state.xrep )
            {
                goto lbl_61;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            state.f = state.fbase;
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 15;
            goto lbl_rcomm;
        lbl_15:
            state.xupdated = false;
        lbl_61:
            result = false;
            return result;
        lbl_59:
        lbl_57:
            if( (double)(v)>(double)(0.5) )
            {
                decreaselambda(ref state.lambdav, ref state.nu);
            }
            
            //
            // Accept step, report it and
            // test stopping conditions on iterations count and function decrease.
            //
            // NOTE: we expect that State.DeltaX contains direction of step,
            // State.F contains function value at new point.
            //
            // NOTE2: we should update XBase ONLY. In the beginning of the next
            // iteration we expect that State.FIBase is NOT updated and
            // contains old value of a function vector.
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.xbase[i_] = state.xbase[i_] + state.deltax[i_];
            }
            if( !state.xrep )
            {
                goto lbl_63;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 16;
            goto lbl_rcomm;
        lbl_16:
            state.xupdated = false;
        lbl_63:
            state.repiterationscount = state.repiterationscount+1;
            if( state.repiterationscount>=state.maxits && state.maxits>0 )
            {
                state.repterminationtype = 5;
            }
            if( state.modelage==0 )
            {
                if( (double)(Math.Abs(state.f-state.fbase))<=(double)(state.epsf*Math.Max(1, Math.Max(Math.Abs(state.f), Math.Abs(state.fbase)))) )
                {
                    state.repterminationtype = 1;
                }
            }
            if( state.repterminationtype<=0 )
            {
                goto lbl_65;
            }
            if( !state.xrep )
            {
                goto lbl_67;
            }
            
            //
            // Report: XBase contains new point, F contains function value at new point
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 17;
            goto lbl_rcomm;
        lbl_17:
            state.xupdated = false;
        lbl_67:
            result = false;
            return result;
        lbl_65:
            state.modelage = state.modelage+1;
            goto lbl_28;
        lbl_29:
            
            //
            // Lambda is too large, we have to break iterations.
            //
            state.repterminationtype = 7;
            if( !state.xrep )
            {
                goto lbl_69;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xbase[i_];
            }
            state.f = state.fbase;
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 18;
            goto lbl_rcomm;
        lbl_18:
            state.xupdated = false;
        lbl_69:
            result = false;
            return result;
            
            //
            // Saving state
            //
        lbl_rcomm:
            result = true;
            state.rstate.ia[0] = n;
            state.rstate.ia[1] = m;
            state.rstate.ia[2] = iflag;
            state.rstate.ia[3] = i;
            state.rstate.ia[4] = k;
            state.rstate.ba[0] = bflag;
            state.rstate.ra[0] = v;
            state.rstate.ra[1] = s;
            state.rstate.ra[2] = t;
            return result;
        }


        /*************************************************************************
        Levenberg-Marquardt algorithm results

        INPUT PARAMETERS:
            State   -   algorithm state

        OUTPUT PARAMETERS:
            X       -   array[0..N-1], solution
            Rep     -   optimization report;
                        see comments for this structure for more info.

          -- ALGLIB --
             Copyright 10.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmresults(minlmstate state,
            ref double[] x,
            minlmreport rep)
        {
            x = new double[0];

            minlmresultsbuf(state, ref x, rep);
        }


        /*************************************************************************
        Levenberg-Marquardt algorithm results

        Buffered implementation of MinLMResults(), which uses pre-allocated buffer
        to store X[]. If buffer size is  too  small,  it  resizes  buffer.  It  is
        intended to be used in the inner cycles of performance critical algorithms
        where array reallocation penalty is too large to be ignored.

          -- ALGLIB --
             Copyright 10.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmresultsbuf(minlmstate state,
            ref double[] x,
            minlmreport rep)
        {
            int i_ = 0;

            if( alglib.ap.len(x)<state.n )
            {
                x = new double[state.n];
            }
            for(i_=0; i_<=state.n-1;i_++)
            {
                x[i_] = state.x[i_];
            }
            rep.iterationscount = state.repiterationscount;
            rep.terminationtype = state.repterminationtype;
            rep.funcidx = state.repfuncidx;
            rep.varidx = state.repvaridx;
            rep.nfunc = state.repnfunc;
            rep.njac = state.repnjac;
            rep.ngrad = state.repngrad;
            rep.nhess = state.repnhess;
            rep.ncholesky = state.repncholesky;
        }


        /*************************************************************************
        This  subroutine  restarts  LM  algorithm from new point. All optimization
        parameters are left unchanged.

        This  function  allows  to  solve multiple  optimization  problems  (which
        must have same number of dimensions) without object reallocation penalty.

        INPUT PARAMETERS:
            State   -   structure used for reverse communication previously
                        allocated with MinLMCreateXXX call.
            X       -   new starting point.

          -- ALGLIB --
             Copyright 30.07.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmrestartfrom(minlmstate state,
            double[] x)
        {
            int i_ = 0;

            alglib.ap.assert(alglib.ap.len(x)>=state.n, "MinLMRestartFrom: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, state.n), "MinLMRestartFrom: X contains infinite or NaN values!");
            for(i_=0; i_<=state.n-1;i_++)
            {
                state.xbase[i_] = x[i_];
            }
            state.rstate.ia = new int[4+1];
            state.rstate.ba = new bool[0+1];
            state.rstate.ra = new double[2+1];
            state.rstate.stage = -1;
            clearrequestfields(state);
        }


        /*************************************************************************
        This is obsolete function.

        Since ALGLIB 3.3 it is equivalent to MinLMCreateVJ().

          -- ALGLIB --
             Copyright 30.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmcreatevgj(int n,
            int m,
            double[] x,
            minlmstate state)
        {
            minlmcreatevj(n, m, x, state);
        }


        /*************************************************************************
        This is obsolete function.

        Since ALGLIB 3.3 it is equivalent to MinLMCreateFJ().

          -- ALGLIB --
             Copyright 30.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmcreatefgj(int n,
            int m,
            double[] x,
            minlmstate state)
        {
            minlmcreatefj(n, m, x, state);
        }


        /*************************************************************************
        This function is considered obsolete since ALGLIB 3.1.0 and is present for
        backward  compatibility  only.  We  recommend  to use MinLMCreateVJ, which
        provides similar, but more consistent and feature-rich interface.

          -- ALGLIB --
             Copyright 30.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmcreatefj(int n,
            int m,
            double[] x,
            minlmstate state)
        {
            alglib.ap.assert(n>=1, "MinLMCreateFJ: N<1!");
            alglib.ap.assert(m>=1, "MinLMCreateFJ: M<1!");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinLMCreateFJ: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinLMCreateFJ: X contains infinite or NaN values!");
            
            //
            // initialize
            //
            state.teststep = 0;
            state.n = n;
            state.m = m;
            state.algomode = 1;
            state.hasf = true;
            state.hasfi = false;
            state.hasg = false;
            
            //
            // init 2
            //
            lmprepare(n, m, true, state);
            minlmsetacctype(state, 0);
            minlmsetcond(state, 0, 0, 0, 0);
            minlmsetxrep(state, false);
            minlmsetstpmax(state, 0);
            minlmrestartfrom(state, x);
        }


        /*************************************************************************
        This  subroutine  turns  on  verification  of  the  user-supplied analytic
        gradient:
        * user calls this subroutine before optimization begins
        * MinLMOptimize() is called
        * prior to actual optimization, for  each  function Fi and each  component
          of parameters  being  optimized X[j] algorithm performs following steps:
          * two trial steps are made to X[j]-TestStep*S[j] and X[j]+TestStep*S[j],
            where X[j] is j-th parameter and S[j] is a scale of j-th parameter
          * if needed, steps are bounded with respect to constraints on X[]
          * Fi(X) is evaluated at these trial points
          * we perform one more evaluation in the middle point of the interval
          * we  build  cubic  model using function values and derivatives at trial
            points and we compare its prediction with actual value in  the  middle
            point
          * in case difference between prediction and actual value is higher  than
            some predetermined threshold, algorithm stops with completion code -7;
            Rep.VarIdx is set to index of the parameter with incorrect derivative,
            Rep.FuncIdx is set to index of the function.
        * after verification is over, algorithm proceeds to the actual optimization.

        NOTE 1: verification  needs  N (parameters count) Jacobian evaluations. It
                is  very  costly  and  you  should use it only for low dimensional
                problems,  when  you  want  to  be  sure  that  you've   correctly
                calculated  analytic  derivatives.  You should not  use  it in the
                production code  (unless  you  want  to check derivatives provided
                by some third party).

        NOTE 2: you  should  carefully  choose  TestStep. Value which is too large
                (so large that function behaviour is significantly non-cubic) will
                lead to false alarms. You may use  different  step  for  different
                parameters by means of setting scale with MinLMSetScale().

        NOTE 3: this function may lead to false positives. In case it reports that
                I-th  derivative was calculated incorrectly, you may decrease test
                step  and  try  one  more  time  - maybe your function changes too
                sharply  and  your  step  is  too  large for such rapidly chanding
                function.

        INPUT PARAMETERS:
            State       -   structure used to store algorithm state
            TestStep    -   verification step:
                            * TestStep=0 turns verification off
                            * TestStep>0 activates verification

          -- ALGLIB --
             Copyright 15.06.2012 by Bochkanov Sergey
        *************************************************************************/
        public static void minlmsetgradientcheck(minlmstate state,
            double teststep)
        {
            alglib.ap.assert(math.isfinite(teststep), "MinLMSetGradientCheck: TestStep contains NaN or Infinite");
            alglib.ap.assert((double)(teststep)>=(double)(0), "MinLMSetGradientCheck: invalid argument TestStep(TestStep<0)");
            state.teststep = teststep;
        }


        /*************************************************************************
        Prepare internal structures (except for RComm).

        Note: M must be zero for FGH mode, non-zero for V/VJ/FJ/FGJ mode.
        *************************************************************************/
        private static void lmprepare(int n,
            int m,
            bool havegrad,
            minlmstate state)
        {
            int i = 0;

            if( n<=0 || m<0 )
            {
                return;
            }
            if( havegrad )
            {
                state.g = new double[n];
            }
            if( m!=0 )
            {
                state.j = new double[m, n];
                state.fi = new double[m];
                state.fibase = new double[m];
                state.deltaf = new double[m];
                state.fm1 = new double[m];
                state.fp1 = new double[m];
                state.fc1 = new double[m];
                state.gm1 = new double[m];
                state.gp1 = new double[m];
                state.gc1 = new double[m];
            }
            else
            {
                state.h = new double[n, n];
            }
            state.x = new double[n];
            state.deltax = new double[n];
            state.quadraticmodel = new double[n, n];
            state.xbase = new double[n];
            state.gbase = new double[n];
            state.xdir = new double[n];
            state.tmp0 = new double[n];
            
            //
            // prepare internal L-BFGS
            //
            for(i=0; i<=n-1; i++)
            {
                state.x[i] = 0;
            }
            minlbfgs.minlbfgscreate(n, Math.Min(additers, n), state.x, state.internalstate);
            minlbfgs.minlbfgssetcond(state.internalstate, 0.0, 0.0, 0.0, Math.Min(additers, n));
            
            //
            // Prepare internal QP solver
            //
            minqp.minqpcreate(n, state.qpstate);
            minqp.minqpsetalgocholesky(state.qpstate);
            
            //
            // Prepare boundary constraints
            //
            state.bndl = new double[n];
            state.bndu = new double[n];
            state.havebndl = new bool[n];
            state.havebndu = new bool[n];
            for(i=0; i<=n-1; i++)
            {
                state.bndl[i] = Double.NegativeInfinity;
                state.havebndl[i] = false;
                state.bndu[i] = Double.PositiveInfinity;
                state.havebndu[i] = false;
            }
            
            //
            // Prepare scaling matrix
            //
            state.s = new double[n];
            for(i=0; i<=n-1; i++)
            {
                state.s[i] = 1.0;
            }
        }


        /*************************************************************************
        Clears request fileds (to be sure that we don't forgot to clear something)
        *************************************************************************/
        private static void clearrequestfields(minlmstate state)
        {
            state.needf = false;
            state.needfg = false;
            state.needfgh = false;
            state.needfij = false;
            state.needfi = false;
            state.xupdated = false;
        }


        /*************************************************************************
        Increases lambda, returns False when there is a danger of overflow
        *************************************************************************/
        private static bool increaselambda(ref double lambdav,
            ref double nu)
        {
            bool result = new bool();
            double lnlambda = 0;
            double lnnu = 0;
            double lnlambdaup = 0;
            double lnmax = 0;

            result = false;
            lnlambda = Math.Log(lambdav);
            lnlambdaup = Math.Log(lambdaup);
            lnnu = Math.Log(nu);
            lnmax = Math.Log(math.maxrealnumber);
            if( (double)(lnlambda+lnlambdaup+lnnu)>(double)(0.25*lnmax) )
            {
                return result;
            }
            if( (double)(lnnu+Math.Log(2))>(double)(lnmax) )
            {
                return result;
            }
            lambdav = lambdav*lambdaup*nu;
            nu = nu*2;
            result = true;
            return result;
        }


        /*************************************************************************
        Decreases lambda, but leaves it unchanged when there is danger of underflow.
        *************************************************************************/
        private static void decreaselambda(ref double lambdav,
            ref double nu)
        {
            nu = 1;
            if( (double)(Math.Log(lambdav)+Math.Log(lambdadown))<(double)(Math.Log(math.minrealnumber)) )
            {
                lambdav = math.minrealnumber;
            }
            else
            {
                lambdav = lambdav*lambdadown;
            }
        }


        /*************************************************************************
        Returns norm of bounded scaled anti-gradient.

        Bounded antigradient is a vector obtained from  anti-gradient  by  zeroing
        components which point outwards:
            result = norm(v)
            v[i]=0     if ((-g[i]<0)and(x[i]=bndl[i])) or
                          ((-g[i]>0)and(x[i]=bndu[i]))
            v[i]=-g[i]*s[i] otherwise, where s[i] is a scale for I-th variable

        This function may be used to check a stopping criterion.

          -- ALGLIB --
             Copyright 14.01.2011 by Bochkanov Sergey
        *************************************************************************/
        private static double boundedscaledantigradnorm(minlmstate state,
            double[] x,
            double[] g)
        {
            double result = 0;
            int n = 0;
            int i = 0;
            double v = 0;

            result = 0;
            n = state.n;
            for(i=0; i<=n-1; i++)
            {
                v = -(g[i]*state.s[i]);
                if( state.havebndl[i] )
                {
                    if( (double)(x[i])<=(double)(state.bndl[i]) && (double)(-g[i])<(double)(0) )
                    {
                        v = 0;
                    }
                }
                if( state.havebndu[i] )
                {
                    if( (double)(x[i])>=(double)(state.bndu[i]) && (double)(-g[i])>(double)(0) )
                    {
                        v = 0;
                    }
                }
                result = result+math.sqr(v);
            }
            result = Math.Sqrt(result);
            return result;
        }


    }
    public class mincomp
    {
        public class minasastate
        {
            public int n;
            public double epsg;
            public double epsf;
            public double epsx;
            public int maxits;
            public bool xrep;
            public double stpmax;
            public int cgtype;
            public int k;
            public int nfev;
            public int mcstage;
            public double[] bndl;
            public double[] bndu;
            public int curalgo;
            public int acount;
            public double mu;
            public double finit;
            public double dginit;
            public double[] ak;
            public double[] xk;
            public double[] dk;
            public double[] an;
            public double[] xn;
            public double[] dn;
            public double[] d;
            public double fold;
            public double stp;
            public double[] work;
            public double[] yk;
            public double[] gc;
            public double laststep;
            public double[] x;
            public double f;
            public double[] g;
            public bool needfg;
            public bool xupdated;
            public rcommstate rstate;
            public int repiterationscount;
            public int repnfev;
            public int repterminationtype;
            public int debugrestartscount;
            public linmin.linminstate lstate;
            public double betahs;
            public double betady;
            public minasastate()
            {
                bndl = new double[0];
                bndu = new double[0];
                ak = new double[0];
                xk = new double[0];
                dk = new double[0];
                an = new double[0];
                xn = new double[0];
                dn = new double[0];
                d = new double[0];
                work = new double[0];
                yk = new double[0];
                gc = new double[0];
                x = new double[0];
                g = new double[0];
                rstate = new rcommstate();
                lstate = new linmin.linminstate();
            }
        };


        public class minasareport
        {
            public int iterationscount;
            public int nfev;
            public int terminationtype;
            public int activeconstraints;
        };




        public const int n1 = 2;
        public const int n2 = 2;
        public const double stpmin = 1.0E-300;
        public const double gtol = 0.3;
        public const double gpaftol = 0.0001;
        public const double gpadecay = 0.5;
        public const double asarho = 0.5;


        /*************************************************************************
        Obsolete function, use MinLBFGSSetPrecDefault() instead.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetdefaultpreconditioner(minlbfgs.minlbfgsstate state)
        {
            minlbfgs.minlbfgssetprecdefault(state);
        }


        /*************************************************************************
        Obsolete function, use MinLBFGSSetCholeskyPreconditioner() instead.

          -- ALGLIB --
             Copyright 13.10.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minlbfgssetcholeskypreconditioner(minlbfgs.minlbfgsstate state,
            double[,] p,
            bool isupper)
        {
            minlbfgs.minlbfgssetpreccholesky(state, p, isupper);
        }


        /*************************************************************************
        This is obsolete function which was used by previous version of the  BLEIC
        optimizer. It does nothing in the current version of BLEIC.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetbarrierwidth(minbleic.minbleicstate state,
            double mu)
        {
        }


        /*************************************************************************
        This is obsolete function which was used by previous version of the  BLEIC
        optimizer. It does nothing in the current version of BLEIC.

          -- ALGLIB --
             Copyright 28.11.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minbleicsetbarrierdecay(minbleic.minbleicstate state,
            double mudecay)
        {
        }


        /*************************************************************************
        Obsolete optimization algorithm.
        Was replaced by MinBLEIC subpackage.

          -- ALGLIB --
             Copyright 25.03.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minasacreate(int n,
            double[] x,
            double[] bndl,
            double[] bndu,
            minasastate state)
        {
            int i = 0;

            alglib.ap.assert(n>=1, "MinASA: N too small!");
            alglib.ap.assert(alglib.ap.len(x)>=n, "MinCGCreate: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, n), "MinCGCreate: X contains infinite or NaN values!");
            alglib.ap.assert(alglib.ap.len(bndl)>=n, "MinCGCreate: Length(BndL)<N!");
            alglib.ap.assert(apserv.isfinitevector(bndl, n), "MinCGCreate: BndL contains infinite or NaN values!");
            alglib.ap.assert(alglib.ap.len(bndu)>=n, "MinCGCreate: Length(BndU)<N!");
            alglib.ap.assert(apserv.isfinitevector(bndu, n), "MinCGCreate: BndU contains infinite or NaN values!");
            for(i=0; i<=n-1; i++)
            {
                alglib.ap.assert((double)(bndl[i])<=(double)(bndu[i]), "MinASA: inconsistent bounds!");
                alglib.ap.assert((double)(bndl[i])<=(double)(x[i]), "MinASA: infeasible X!");
                alglib.ap.assert((double)(x[i])<=(double)(bndu[i]), "MinASA: infeasible X!");
            }
            
            //
            // Initialize
            //
            state.n = n;
            minasasetcond(state, 0, 0, 0, 0);
            minasasetxrep(state, false);
            minasasetstpmax(state, 0);
            minasasetalgorithm(state, -1);
            state.bndl = new double[n];
            state.bndu = new double[n];
            state.ak = new double[n];
            state.xk = new double[n];
            state.dk = new double[n];
            state.an = new double[n];
            state.xn = new double[n];
            state.dn = new double[n];
            state.x = new double[n];
            state.d = new double[n];
            state.g = new double[n];
            state.gc = new double[n];
            state.work = new double[n];
            state.yk = new double[n];
            minasarestartfrom(state, x, bndl, bndu);
        }


        /*************************************************************************
        Obsolete optimization algorithm.
        Was replaced by MinBLEIC subpackage.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minasasetcond(minasastate state,
            double epsg,
            double epsf,
            double epsx,
            int maxits)
        {
            alglib.ap.assert(math.isfinite(epsg), "MinASASetCond: EpsG is not finite number!");
            alglib.ap.assert((double)(epsg)>=(double)(0), "MinASASetCond: negative EpsG!");
            alglib.ap.assert(math.isfinite(epsf), "MinASASetCond: EpsF is not finite number!");
            alglib.ap.assert((double)(epsf)>=(double)(0), "MinASASetCond: negative EpsF!");
            alglib.ap.assert(math.isfinite(epsx), "MinASASetCond: EpsX is not finite number!");
            alglib.ap.assert((double)(epsx)>=(double)(0), "MinASASetCond: negative EpsX!");
            alglib.ap.assert(maxits>=0, "MinASASetCond: negative MaxIts!");
            if( (((double)(epsg)==(double)(0) && (double)(epsf)==(double)(0)) && (double)(epsx)==(double)(0)) && maxits==0 )
            {
                epsx = 1.0E-6;
            }
            state.epsg = epsg;
            state.epsf = epsf;
            state.epsx = epsx;
            state.maxits = maxits;
        }


        /*************************************************************************
        Obsolete optimization algorithm.
        Was replaced by MinBLEIC subpackage.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minasasetxrep(minasastate state,
            bool needxrep)
        {
            state.xrep = needxrep;
        }


        /*************************************************************************
        Obsolete optimization algorithm.
        Was replaced by MinBLEIC subpackage.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minasasetalgorithm(minasastate state,
            int algotype)
        {
            alglib.ap.assert(algotype>=-1 && algotype<=1, "MinASASetAlgorithm: incorrect AlgoType!");
            if( algotype==-1 )
            {
                algotype = 1;
            }
            state.cgtype = algotype;
        }


        /*************************************************************************
        Obsolete optimization algorithm.
        Was replaced by MinBLEIC subpackage.

          -- ALGLIB --
             Copyright 02.04.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minasasetstpmax(minasastate state,
            double stpmax)
        {
            alglib.ap.assert(math.isfinite(stpmax), "MinASASetStpMax: StpMax is not finite!");
            alglib.ap.assert((double)(stpmax)>=(double)(0), "MinASASetStpMax: StpMax<0!");
            state.stpmax = stpmax;
        }


        /*************************************************************************

          -- ALGLIB --
             Copyright 20.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static bool minasaiteration(minasastate state)
        {
            bool result = new bool();
            int n = 0;
            int i = 0;
            double betak = 0;
            double v = 0;
            double vv = 0;
            int mcinfo = 0;
            bool b = new bool();
            bool stepfound = new bool();
            int diffcnt = 0;
            int i_ = 0;

            
            //
            // Reverse communication preparations
            // I know it looks ugly, but it works the same way
            // anywhere from C++ to Python.
            //
            // This code initializes locals by:
            // * random values determined during code
            //   generation - on first subroutine call
            // * values from previous call - on subsequent calls
            //
            if( state.rstate.stage>=0 )
            {
                n = state.rstate.ia[0];
                i = state.rstate.ia[1];
                mcinfo = state.rstate.ia[2];
                diffcnt = state.rstate.ia[3];
                b = state.rstate.ba[0];
                stepfound = state.rstate.ba[1];
                betak = state.rstate.ra[0];
                v = state.rstate.ra[1];
                vv = state.rstate.ra[2];
            }
            else
            {
                n = -983;
                i = -989;
                mcinfo = -834;
                diffcnt = 900;
                b = true;
                stepfound = false;
                betak = 214;
                v = -338;
                vv = -686;
            }
            if( state.rstate.stage==0 )
            {
                goto lbl_0;
            }
            if( state.rstate.stage==1 )
            {
                goto lbl_1;
            }
            if( state.rstate.stage==2 )
            {
                goto lbl_2;
            }
            if( state.rstate.stage==3 )
            {
                goto lbl_3;
            }
            if( state.rstate.stage==4 )
            {
                goto lbl_4;
            }
            if( state.rstate.stage==5 )
            {
                goto lbl_5;
            }
            if( state.rstate.stage==6 )
            {
                goto lbl_6;
            }
            if( state.rstate.stage==7 )
            {
                goto lbl_7;
            }
            if( state.rstate.stage==8 )
            {
                goto lbl_8;
            }
            if( state.rstate.stage==9 )
            {
                goto lbl_9;
            }
            if( state.rstate.stage==10 )
            {
                goto lbl_10;
            }
            if( state.rstate.stage==11 )
            {
                goto lbl_11;
            }
            if( state.rstate.stage==12 )
            {
                goto lbl_12;
            }
            if( state.rstate.stage==13 )
            {
                goto lbl_13;
            }
            if( state.rstate.stage==14 )
            {
                goto lbl_14;
            }
            
            //
            // Routine body
            //
            
            //
            // Prepare
            //
            n = state.n;
            state.repterminationtype = 0;
            state.repiterationscount = 0;
            state.repnfev = 0;
            state.debugrestartscount = 0;
            state.cgtype = 1;
            for(i_=0; i_<=n-1;i_++)
            {
                state.xk[i_] = state.x[i_];
            }
            for(i=0; i<=n-1; i++)
            {
                if( (double)(state.xk[i])==(double)(state.bndl[i]) || (double)(state.xk[i])==(double)(state.bndu[i]) )
                {
                    state.ak[i] = 0;
                }
                else
                {
                    state.ak[i] = 1;
                }
            }
            state.mu = 0.1;
            state.curalgo = 0;
            
            //
            // Calculate F/G, initialize algorithm
            //
            clearrequestfields(state);
            state.needfg = true;
            state.rstate.stage = 0;
            goto lbl_rcomm;
        lbl_0:
            state.needfg = false;
            if( !state.xrep )
            {
                goto lbl_15;
            }
            
            //
            // progress report
            //
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 1;
            goto lbl_rcomm;
        lbl_1:
            state.xupdated = false;
        lbl_15:
            if( (double)(asaboundedantigradnorm(state))<=(double)(state.epsg) )
            {
                state.repterminationtype = 4;
                result = false;
                return result;
            }
            state.repnfev = state.repnfev+1;
            
            //
            // Main cycle
            //
            // At the beginning of new iteration:
            // * CurAlgo stores current algorithm selector
            // * State.XK, State.F and State.G store current X/F/G
            // * State.AK stores current set of active constraints
            //
        lbl_17:
            if( false )
            {
                goto lbl_18;
            }
            
            //
            // GPA algorithm
            //
            if( state.curalgo!=0 )
            {
                goto lbl_19;
            }
            state.k = 0;
            state.acount = 0;
        lbl_21:
            if( false )
            {
                goto lbl_22;
            }
            
            //
            // Determine Dk = proj(xk - gk)-xk
            //
            for(i=0; i<=n-1; i++)
            {
                state.d[i] = apserv.boundval(state.xk[i]-state.g[i], state.bndl[i], state.bndu[i])-state.xk[i];
            }
            
            //
            // Armijo line search.
            // * exact search with alpha=1 is tried first,
            //   'exact' means that we evaluate f() EXACTLY at
            //   bound(x-g,bndl,bndu), without intermediate floating
            //   point operations.
            // * alpha<1 are tried if explicit search wasn't successful
            // Result is placed into XN.
            //
            // Two types of search are needed because we can't
            // just use second type with alpha=1 because in finite
            // precision arithmetics (x1-x0)+x0 may differ from x1.
            // So while x1 is correctly bounded (it lie EXACTLY on
            // boundary, if it is active), (x1-x0)+x0 may be
            // not bounded.
            //
            v = 0.0;
            for(i_=0; i_<=n-1;i_++)
            {
                v += state.d[i_]*state.g[i_];
            }
            state.dginit = v;
            state.finit = state.f;
            if( !((double)(asad1norm(state))<=(double)(state.stpmax) || (double)(state.stpmax)==(double)(0)) )
            {
                goto lbl_23;
            }
            
            //
            // Try alpha=1 step first
            //
            for(i=0; i<=n-1; i++)
            {
                state.x[i] = apserv.boundval(state.xk[i]-state.g[i], state.bndl[i], state.bndu[i]);
            }
            clearrequestfields(state);
            state.needfg = true;
            state.rstate.stage = 2;
            goto lbl_rcomm;
        lbl_2:
            state.needfg = false;
            state.repnfev = state.repnfev+1;
            stepfound = (double)(state.f)<=(double)(state.finit+gpaftol*state.dginit);
            goto lbl_24;
        lbl_23:
            stepfound = false;
        lbl_24:
            if( !stepfound )
            {
                goto lbl_25;
            }
            
            //
            // we are at the boundary(ies)
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.xn[i_] = state.x[i_];
            }
            state.stp = 1;
            goto lbl_26;
        lbl_25:
            
            //
            // alpha=1 is too large, try smaller values
            //
            state.stp = 1;
            linmin.linminnormalized(ref state.d, ref state.stp, n);
            state.dginit = state.dginit/state.stp;
            state.stp = gpadecay*state.stp;
            if( (double)(state.stpmax)>(double)(0) )
            {
                state.stp = Math.Min(state.stp, state.stpmax);
            }
        lbl_27:
            if( false )
            {
                goto lbl_28;
            }
            v = state.stp;
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.xk[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.x[i_] = state.x[i_] + v*state.d[i_];
            }
            clearrequestfields(state);
            state.needfg = true;
            state.rstate.stage = 3;
            goto lbl_rcomm;
        lbl_3:
            state.needfg = false;
            state.repnfev = state.repnfev+1;
            if( (double)(state.stp)<=(double)(stpmin) )
            {
                goto lbl_28;
            }
            if( (double)(state.f)<=(double)(state.finit+state.stp*gpaftol*state.dginit) )
            {
                goto lbl_28;
            }
            state.stp = state.stp*gpadecay;
            goto lbl_27;
        lbl_28:
            for(i_=0; i_<=n-1;i_++)
            {
                state.xn[i_] = state.x[i_];
            }
        lbl_26:
            state.repiterationscount = state.repiterationscount+1;
            if( !state.xrep )
            {
                goto lbl_29;
            }
            
            //
            // progress report
            //
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 4;
            goto lbl_rcomm;
        lbl_4:
            state.xupdated = false;
        lbl_29:
            
            //
            // Calculate new set of active constraints.
            // Reset counter if active set was changed.
            // Prepare for the new iteration
            //
            for(i=0; i<=n-1; i++)
            {
                if( (double)(state.xn[i])==(double)(state.bndl[i]) || (double)(state.xn[i])==(double)(state.bndu[i]) )
                {
                    state.an[i] = 0;
                }
                else
                {
                    state.an[i] = 1;
                }
            }
            for(i=0; i<=n-1; i++)
            {
                if( (double)(state.ak[i])!=(double)(state.an[i]) )
                {
                    state.acount = -1;
                    break;
                }
            }
            state.acount = state.acount+1;
            for(i_=0; i_<=n-1;i_++)
            {
                state.xk[i_] = state.xn[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.ak[i_] = state.an[i_];
            }
            
            //
            // Stopping conditions
            //
            if( !(state.repiterationscount>=state.maxits && state.maxits>0) )
            {
                goto lbl_31;
            }
            
            //
            // Too many iterations
            //
            state.repterminationtype = 5;
            if( !state.xrep )
            {
                goto lbl_33;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 5;
            goto lbl_rcomm;
        lbl_5:
            state.xupdated = false;
        lbl_33:
            result = false;
            return result;
        lbl_31:
            if( (double)(asaboundedantigradnorm(state))>(double)(state.epsg) )
            {
                goto lbl_35;
            }
            
            //
            // Gradient is small enough
            //
            state.repterminationtype = 4;
            if( !state.xrep )
            {
                goto lbl_37;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 6;
            goto lbl_rcomm;
        lbl_6:
            state.xupdated = false;
        lbl_37:
            result = false;
            return result;
        lbl_35:
            v = 0.0;
            for(i_=0; i_<=n-1;i_++)
            {
                v += state.d[i_]*state.d[i_];
            }
            if( (double)(Math.Sqrt(v)*state.stp)>(double)(state.epsx) )
            {
                goto lbl_39;
            }
            
            //
            // Step size is too small, no further improvement is
            // possible
            //
            state.repterminationtype = 2;
            if( !state.xrep )
            {
                goto lbl_41;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 7;
            goto lbl_rcomm;
        lbl_7:
            state.xupdated = false;
        lbl_41:
            result = false;
            return result;
        lbl_39:
            if( (double)(state.finit-state.f)>(double)(state.epsf*Math.Max(Math.Abs(state.finit), Math.Max(Math.Abs(state.f), 1.0))) )
            {
                goto lbl_43;
            }
            
            //
            // F(k+1)-F(k) is small enough
            //
            state.repterminationtype = 1;
            if( !state.xrep )
            {
                goto lbl_45;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 8;
            goto lbl_rcomm;
        lbl_8:
            state.xupdated = false;
        lbl_45:
            result = false;
            return result;
        lbl_43:
            
            //
            // Decide - should we switch algorithm or not
            //
            if( asauisempty(state) )
            {
                if( (double)(asaginorm(state))>=(double)(state.mu*asad1norm(state)) )
                {
                    state.curalgo = 1;
                    goto lbl_22;
                }
                else
                {
                    state.mu = state.mu*asarho;
                }
            }
            else
            {
                if( state.acount==n1 )
                {
                    if( (double)(asaginorm(state))>=(double)(state.mu*asad1norm(state)) )
                    {
                        state.curalgo = 1;
                        goto lbl_22;
                    }
                }
            }
            
            //
            // Next iteration
            //
            state.k = state.k+1;
            goto lbl_21;
        lbl_22:
        lbl_19:
            
            //
            // CG algorithm
            //
            if( state.curalgo!=1 )
            {
                goto lbl_47;
            }
            
            //
            // first, check that there are non-active constraints.
            // move to GPA algorithm, if all constraints are active
            //
            b = true;
            for(i=0; i<=n-1; i++)
            {
                if( (double)(state.ak[i])!=(double)(0) )
                {
                    b = false;
                    break;
                }
            }
            if( b )
            {
                state.curalgo = 0;
                goto lbl_17;
            }
            
            //
            // CG iterations
            //
            state.fold = state.f;
            for(i_=0; i_<=n-1;i_++)
            {
                state.xk[i_] = state.x[i_];
            }
            for(i=0; i<=n-1; i++)
            {
                state.dk[i] = -(state.g[i]*state.ak[i]);
                state.gc[i] = state.g[i]*state.ak[i];
            }
        lbl_49:
            if( false )
            {
                goto lbl_50;
            }
            
            //
            // Store G[k] for later calculation of Y[k]
            //
            for(i=0; i<=n-1; i++)
            {
                state.yk[i] = -state.gc[i];
            }
            
            //
            // Make a CG step in direction given by DK[]:
            // * calculate step. Step projection into feasible set
            //   is used. It has several benefits: a) step may be
            //   found with usual line search, b) multiple constraints
            //   may be activated with one step, c) activated constraints
            //   are detected in a natural way - just compare x[i] with
            //   bounds
            // * update active set, set B to True, if there
            //   were changes in the set.
            //
            for(i_=0; i_<=n-1;i_++)
            {
                state.d[i_] = state.dk[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.xn[i_] = state.xk[i_];
            }
            state.mcstage = 0;
            state.stp = 1;
            linmin.linminnormalized(ref state.d, ref state.stp, n);
            if( (double)(state.laststep)!=(double)(0) )
            {
                state.stp = state.laststep;
            }
            linmin.mcsrch(n, ref state.xn, ref state.f, ref state.gc, state.d, ref state.stp, state.stpmax, gtol, ref mcinfo, ref state.nfev, ref state.work, state.lstate, ref state.mcstage);
        lbl_51:
            if( state.mcstage==0 )
            {
                goto lbl_52;
            }
            
            //
            // preprocess data: bound State.XN so it belongs to the
            // feasible set and store it in the State.X
            //
            for(i=0; i<=n-1; i++)
            {
                state.x[i] = apserv.boundval(state.xn[i], state.bndl[i], state.bndu[i]);
            }
            
            //
            // RComm
            //
            clearrequestfields(state);
            state.needfg = true;
            state.rstate.stage = 9;
            goto lbl_rcomm;
        lbl_9:
            state.needfg = false;
            
            //
            // postprocess data: zero components of G corresponding to
            // the active constraints
            //
            for(i=0; i<=n-1; i++)
            {
                if( (double)(state.x[i])==(double)(state.bndl[i]) || (double)(state.x[i])==(double)(state.bndu[i]) )
                {
                    state.gc[i] = 0;
                }
                else
                {
                    state.gc[i] = state.g[i];
                }
            }
            linmin.mcsrch(n, ref state.xn, ref state.f, ref state.gc, state.d, ref state.stp, state.stpmax, gtol, ref mcinfo, ref state.nfev, ref state.work, state.lstate, ref state.mcstage);
            goto lbl_51;
        lbl_52:
            diffcnt = 0;
            for(i=0; i<=n-1; i++)
            {
                
                //
                // XN contains unprojected result, project it,
                // save copy to X (will be used for progress reporting)
                //
                state.xn[i] = apserv.boundval(state.xn[i], state.bndl[i], state.bndu[i]);
                
                //
                // update active set
                //
                if( (double)(state.xn[i])==(double)(state.bndl[i]) || (double)(state.xn[i])==(double)(state.bndu[i]) )
                {
                    state.an[i] = 0;
                }
                else
                {
                    state.an[i] = 1;
                }
                if( (double)(state.an[i])!=(double)(state.ak[i]) )
                {
                    diffcnt = diffcnt+1;
                }
                state.ak[i] = state.an[i];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.xk[i_] = state.xn[i_];
            }
            state.repnfev = state.repnfev+state.nfev;
            state.repiterationscount = state.repiterationscount+1;
            if( !state.xrep )
            {
                goto lbl_53;
            }
            
            //
            // progress report
            //
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 10;
            goto lbl_rcomm;
        lbl_10:
            state.xupdated = false;
        lbl_53:
            
            //
            // Update info about step length
            //
            v = 0.0;
            for(i_=0; i_<=n-1;i_++)
            {
                v += state.d[i_]*state.d[i_];
            }
            state.laststep = Math.Sqrt(v)*state.stp;
            
            //
            // Check stopping conditions.
            //
            if( (double)(asaboundedantigradnorm(state))>(double)(state.epsg) )
            {
                goto lbl_55;
            }
            
            //
            // Gradient is small enough
            //
            state.repterminationtype = 4;
            if( !state.xrep )
            {
                goto lbl_57;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 11;
            goto lbl_rcomm;
        lbl_11:
            state.xupdated = false;
        lbl_57:
            result = false;
            return result;
        lbl_55:
            if( !(state.repiterationscount>=state.maxits && state.maxits>0) )
            {
                goto lbl_59;
            }
            
            //
            // Too many iterations
            //
            state.repterminationtype = 5;
            if( !state.xrep )
            {
                goto lbl_61;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 12;
            goto lbl_rcomm;
        lbl_12:
            state.xupdated = false;
        lbl_61:
            result = false;
            return result;
        lbl_59:
            if( !((double)(asaginorm(state))>=(double)(state.mu*asad1norm(state)) && diffcnt==0) )
            {
                goto lbl_63;
            }
            
            //
            // These conditions (EpsF/EpsX) are explicitly or implicitly
            // related to the current step size and influenced
            // by changes in the active constraints.
            //
            // For these reasons they are checked only when we don't
            // want to 'unstick' at the end of the iteration and there
            // were no changes in the active set.
            //
            // NOTE: consition |G|>=Mu*|D1| must be exactly opposite
            // to the condition used to switch back to GPA. At least
            // one inequality must be strict, otherwise infinite cycle
            // may occur when |G|=Mu*|D1| (we DON'T test stopping
            // conditions and we DON'T switch to GPA, so we cycle
            // indefinitely).
            //
            if( (double)(state.fold-state.f)>(double)(state.epsf*Math.Max(Math.Abs(state.fold), Math.Max(Math.Abs(state.f), 1.0))) )
            {
                goto lbl_65;
            }
            
            //
            // F(k+1)-F(k) is small enough
            //
            state.repterminationtype = 1;
            if( !state.xrep )
            {
                goto lbl_67;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 13;
            goto lbl_rcomm;
        lbl_13:
            state.xupdated = false;
        lbl_67:
            result = false;
            return result;
        lbl_65:
            if( (double)(state.laststep)>(double)(state.epsx) )
            {
                goto lbl_69;
            }
            
            //
            // X(k+1)-X(k) is small enough
            //
            state.repterminationtype = 2;
            if( !state.xrep )
            {
                goto lbl_71;
            }
            clearrequestfields(state);
            state.xupdated = true;
            state.rstate.stage = 14;
            goto lbl_rcomm;
        lbl_14:
            state.xupdated = false;
        lbl_71:
            result = false;
            return result;
        lbl_69:
        lbl_63:
            
            //
            // Check conditions for switching
            //
            if( (double)(asaginorm(state))<(double)(state.mu*asad1norm(state)) )
            {
                state.curalgo = 0;
                goto lbl_50;
            }
            if( diffcnt>0 )
            {
                if( asauisempty(state) || diffcnt>=n2 )
                {
                    state.curalgo = 1;
                }
                else
                {
                    state.curalgo = 0;
                }
                goto lbl_50;
            }
            
            //
            // Calculate D(k+1)
            //
            // Line search may result in:
            // * maximum feasible step being taken (already processed)
            // * point satisfying Wolfe conditions
            // * some kind of error (CG is restarted by assigning 0.0 to Beta)
            //
            if( mcinfo==1 )
            {
                
                //
                // Standard Wolfe conditions are satisfied:
                // * calculate Y[K] and BetaK
                //
                for(i_=0; i_<=n-1;i_++)
                {
                    state.yk[i_] = state.yk[i_] + state.gc[i_];
                }
                vv = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    vv += state.yk[i_]*state.dk[i_];
                }
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.gc[i_]*state.gc[i_];
                }
                state.betady = v/vv;
                v = 0.0;
                for(i_=0; i_<=n-1;i_++)
                {
                    v += state.gc[i_]*state.yk[i_];
                }
                state.betahs = v/vv;
                if( state.cgtype==0 )
                {
                    betak = state.betady;
                }
                if( state.cgtype==1 )
                {
                    betak = Math.Max(0, Math.Min(state.betady, state.betahs));
                }
            }
            else
            {
                
                //
                // Something is wrong (may be function is too wild or too flat).
                //
                // We'll set BetaK=0, which will restart CG algorithm.
                // We can stop later (during normal checks) if stopping conditions are met.
                //
                betak = 0;
                state.debugrestartscount = state.debugrestartscount+1;
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.dn[i_] = -state.gc[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.dn[i_] = state.dn[i_] + betak*state.dk[i_];
            }
            for(i_=0; i_<=n-1;i_++)
            {
                state.dk[i_] = state.dn[i_];
            }
            
            //
            // update other information
            //
            state.fold = state.f;
            state.k = state.k+1;
            goto lbl_49;
        lbl_50:
        lbl_47:
            goto lbl_17;
        lbl_18:
            result = false;
            return result;
            
            //
            // Saving state
            //
        lbl_rcomm:
            result = true;
            state.rstate.ia[0] = n;
            state.rstate.ia[1] = i;
            state.rstate.ia[2] = mcinfo;
            state.rstate.ia[3] = diffcnt;
            state.rstate.ba[0] = b;
            state.rstate.ba[1] = stepfound;
            state.rstate.ra[0] = betak;
            state.rstate.ra[1] = v;
            state.rstate.ra[2] = vv;
            return result;
        }


        /*************************************************************************
        Obsolete optimization algorithm.
        Was replaced by MinBLEIC subpackage.

          -- ALGLIB --
             Copyright 20.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minasaresults(minasastate state,
            ref double[] x,
            minasareport rep)
        {
            x = new double[0];

            minasaresultsbuf(state, ref x, rep);
        }


        /*************************************************************************
        Obsolete optimization algorithm.
        Was replaced by MinBLEIC subpackage.

          -- ALGLIB --
             Copyright 20.03.2009 by Bochkanov Sergey
        *************************************************************************/
        public static void minasaresultsbuf(minasastate state,
            ref double[] x,
            minasareport rep)
        {
            int i = 0;
            int i_ = 0;

            if( alglib.ap.len(x)<state.n )
            {
                x = new double[state.n];
            }
            for(i_=0; i_<=state.n-1;i_++)
            {
                x[i_] = state.x[i_];
            }
            rep.iterationscount = state.repiterationscount;
            rep.nfev = state.repnfev;
            rep.terminationtype = state.repterminationtype;
            rep.activeconstraints = 0;
            for(i=0; i<=state.n-1; i++)
            {
                if( (double)(state.ak[i])==(double)(0) )
                {
                    rep.activeconstraints = rep.activeconstraints+1;
                }
            }
        }


        /*************************************************************************
        Obsolete optimization algorithm.
        Was replaced by MinBLEIC subpackage.

          -- ALGLIB --
             Copyright 30.07.2010 by Bochkanov Sergey
        *************************************************************************/
        public static void minasarestartfrom(minasastate state,
            double[] x,
            double[] bndl,
            double[] bndu)
        {
            int i_ = 0;

            alglib.ap.assert(alglib.ap.len(x)>=state.n, "MinASARestartFrom: Length(X)<N!");
            alglib.ap.assert(apserv.isfinitevector(x, state.n), "MinASARestartFrom: X contains infinite or NaN values!");
            alglib.ap.assert(alglib.ap.len(bndl)>=state.n, "MinASARestartFrom: Length(BndL)<N!");
            alglib.ap.assert(apserv.isfinitevector(bndl, state.n), "MinASARestartFrom: BndL contains infinite or NaN values!");
            alglib.ap.assert(alglib.ap.len(bndu)>=state.n, "MinASARestartFrom: Length(BndU)<N!");
            alglib.ap.assert(apserv.isfinitevector(bndu, state.n), "MinASARestartFrom: BndU contains infinite or NaN values!");
            for(i_=0; i_<=state.n-1;i_++)
            {
                state.x[i_] = x[i_];
            }
            for(i_=0; i_<=state.n-1;i_++)
            {
                state.bndl[i_] = bndl[i_];
            }
            for(i_=0; i_<=state.n-1;i_++)
            {
                state.bndu[i_] = bndu[i_];
            }
            state.laststep = 0;
            state.rstate.ia = new int[3+1];
            state.rstate.ba = new bool[1+1];
            state.rstate.ra = new double[2+1];
            state.rstate.stage = -1;
            clearrequestfields(state);
        }


        /*************************************************************************
        Returns norm of bounded anti-gradient.

        Bounded antigradient is a vector obtained from  anti-gradient  by  zeroing
        components which point outwards:
            result = norm(v)
            v[i]=0     if ((-g[i]<0)and(x[i]=bndl[i])) or
                          ((-g[i]>0)and(x[i]=bndu[i]))
            v[i]=-g[i] otherwise

        This function may be used to check a stopping criterion.

          -- ALGLIB --
             Copyright 20.03.2009 by Bochkanov Sergey
        *************************************************************************/
        private static double asaboundedantigradnorm(minasastate state)
        {
            double result = 0;
            int i = 0;
            double v = 0;

            result = 0;
            for(i=0; i<=state.n-1; i++)
            {
                v = -state.g[i];
                if( (double)(state.x[i])==(double)(state.bndl[i]) && (double)(-state.g[i])<(double)(0) )
                {
                    v = 0;
                }
                if( (double)(state.x[i])==(double)(state.bndu[i]) && (double)(-state.g[i])>(double)(0) )
                {
                    v = 0;
                }
                result = result+math.sqr(v);
            }
            result = Math.Sqrt(result);
            return result;
        }


        /*************************************************************************
        Returns norm of GI(x).

        GI(x) is  a  gradient  vector  whose  components  associated  with  active
        constraints are zeroed. It  differs  from  bounded  anti-gradient  because
        components  of   GI(x)   are   zeroed  independently  of  sign(g[i]),  and
        anti-gradient's components are zeroed with respect to both constraint  and
        sign.

          -- ALGLIB --
             Copyright 20.03.2009 by Bochkanov Sergey
        *************************************************************************/
        private static double asaginorm(minasastate state)
        {
            double result = 0;
            int i = 0;

            result = 0;
            for(i=0; i<=state.n-1; i++)
            {
                if( (double)(state.x[i])!=(double)(state.bndl[i]) && (double)(state.x[i])!=(double)(state.bndu[i]) )
                {
                    result = result+math.sqr(state.g[i]);
                }
            }
            result = Math.Sqrt(result);
            return result;
        }


        /*************************************************************************
        Returns norm(D1(State.X))

        For a meaning of D1 see 'NEW ACTIVE SET ALGORITHM FOR BOX CONSTRAINED
        OPTIMIZATION' by WILLIAM W. HAGER AND HONGCHAO ZHANG.

          -- ALGLIB --
             Copyright 20.03.2009 by Bochkanov Sergey
        *************************************************************************/
        private static double asad1norm(minasastate state)
        {
            double result = 0;
            int i = 0;

            result = 0;
            for(i=0; i<=state.n-1; i++)
            {
                result = result+math.sqr(apserv.boundval(state.x[i]-state.g[i], state.bndl[i], state.bndu[i])-state.x[i]);
            }
            result = Math.Sqrt(result);
            return result;
        }


        /*************************************************************************
        Returns True, if U set is empty.

        * State.X is used as point,
        * State.G - as gradient,
        * D is calculated within function (because State.D may have different
          meaning depending on current optimization algorithm)

        For a meaning of U see 'NEW ACTIVE SET ALGORITHM FOR BOX CONSTRAINED
        OPTIMIZATION' by WILLIAM W. HAGER AND HONGCHAO ZHANG.

          -- ALGLIB --
             Copyright 20.03.2009 by Bochkanov Sergey
        *************************************************************************/
        private static bool asauisempty(minasastate state)
        {
            bool result = new bool();
            int i = 0;
            double d = 0;
            double d2 = 0;
            double d32 = 0;

            d = asad1norm(state);
            d2 = Math.Sqrt(d);
            d32 = d*d2;
            result = true;
            for(i=0; i<=state.n-1; i++)
            {
                if( (double)(Math.Abs(state.g[i]))>=(double)(d2) && (double)(Math.Min(state.x[i]-state.bndl[i], state.bndu[i]-state.x[i]))>=(double)(d32) )
                {
                    result = false;
                    return result;
                }
            }
            return result;
        }


        /*************************************************************************
        Clears request fileds (to be sure that we don't forgot to clear something)
        *************************************************************************/
        private static void clearrequestfields(minasastate state)
        {
            state.needfg = false;
            state.xupdated = false;
        }


    }
}

