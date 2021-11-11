#ifndef _DIAUMPIRE_BSPLINE_HPP_
#define _DIAUMPIRE_BSPLINE_HPP_

#include <vector>
#include <fstream>
#include <boost/math/distributions/chi_squared.hpp>
#include <boost/algorithm/clamp.hpp>
#include <boost/range/numeric.hpp>
#include "ScanData.hpp"

namespace {
    //implements relative method - do not use for comparing with zero
    //use this most of the time, tolerance needs to be meaningful in your context
    template<typename TReal>
    static bool isApproximatelyEqual(TReal a, TReal b, TReal tolerance = std::numeric_limits<TReal>::epsilon())
    {
        TReal diff = std::fabs(a - b);
        if (diff <= tolerance)
            return true;

        if (diff < std::fmax(std::fabs(a), std::fabs(b)) * tolerance)
            return true;

        return false;
    }

    //supply tolerance that is meaningful in your context
    //for example, default tolerance may not work if you are comparing double with float
    template<typename TReal>
    static bool isApproximatelyZero(TReal a, TReal tolerance = std::numeric_limits<TReal>::epsilon())
    {
        if (std::fabs(a) <= tolerance)
            return true;
        return false;
    }


    //use this when you want to be on safe side
    //for example, don't start rover unless signal is above 1
    template<typename TReal>
    static bool isDefinitelyLessThan(TReal a, TReal b, TReal tolerance = std::numeric_limits<TReal>::epsilon(), bool orEqualTo = false)
    {
        TReal diff = a - b;
        if (diff < tolerance)
            return orEqualTo;

        if (diff < std::fmax(std::fabs(a), std::fabs(b)) * tolerance)
            return true;

        return false;
    }
    template<typename TReal>
    static bool isDefinitelyGreaterThan(TReal a, TReal b, TReal tolerance = std::numeric_limits<TReal>::epsilon(), bool orEqualTo = false)
    {
        TReal diff = b - a;
        if (diff < tolerance)
            return orEqualTo;

        if (diff < std::fmax(std::fabs(a), std::fabs(b)) * tolerance)
            return true;

        return false;
    }

    //implements ULP method
    //use this when you are only concerned about floating point precision issue
    //for example, if you want to see if a is 1.0 by checking if its within
    //10 closest representable floating point numbers around 1.0.
    template<typename TReal>
    static bool isWithinPrecisionInterval(TReal a, TReal b, unsigned int interval_size = 1)
    {
        TReal min_a = a - (a - std::nextafter(a, std::numeric_limits<TReal>::lowest())) * interval_size;
        TReal max_a = a + (std::nextafter(a, std::numeric_limits<TReal>::max()) - a) * interval_size;

        return min_a <= b && max_a >= b;
    }
}

namespace DiaUmpire {

#ifdef DIAUMPIRE_DEBUG
    ofstream bsplineLog("bspline-log.txt");
    ofstream bsplineOutputLog("bspline-output-log.txt");
#endif

/**
 * B-spline smoothing
 * @author Chih-Chiang Tsou <chihchiang.tsou@gmail.com>
 */
class BSpline
{
    std::vector<double> bspline_T_;

    public:

    XYPointCollection Run(const XYPointCollection& data, int PtNum, int smoothDegree, int logId)
    {
        XYPointCollection bsplineCollection;
        int p = smoothDegree;
        int n = data.Data.size() - 1;
        int m = data.Data.size() + p;
        bspline_T_.resize(m + p);

        if (data.Data.size() <= (size_t) p) {
            return data;
        }

        for (int i = 0; i <= n; i++) {
            bspline_T_[i] = 0;
            bspline_T_[m - i] = 1;
        }
        double intv = 1.0 / (m - 2 * p);
        for (int i = 1; i <= (m - 1); i++) {
            bspline_T_[p + i] = bspline_T_[p + i - 1] + intv;
        }

        double t;
        for (int i = 0; i <= PtNum; i++) {
            t = ((double)i / PtNum);
            XYData pt = getbspline(data, t, n, p);
            bsplineCollection.AddPoint(pt);
        }
        if (isDefinitelyLessThan(bsplineCollection.Data.back().getX(), data.Data.back().getX(), 1e-8f)) {
            bsplineCollection.AddPoint(data.Data[data.PointCount() - 1]);
        }
        if (isDefinitelyGreaterThan(bsplineCollection.Data[0].getX(), data.Data[0].getX(), 1e-8f)) {
            bsplineCollection.AddPoint(data.Data[0]);
        }

#ifdef DIAUMPIRE_DEBUG
        if (logId > 0)
        {
            bsplineLog << setprecision(10)
                << (bsplineCollection.Data.back().getX() == data.Data.back().getX())
                << " " << isDefinitelyLessThan(bsplineCollection.Data.back().getX(), data.Data.back().getX(), 1e-8f)
                << " " << (bsplineCollection.Data.back().getX() < data.Data.back().getX())
                << " " << bsplineCollection.Data.back().getX()
                << " " << data.Data.back().getX()
                << "\n";
            boost::format pkFormat(" %.3f");
            bsplineOutputLog << logId << " " << data.size() << " " << bsplineCollection.size();
            for (auto& pk : bsplineCollection.Data)
                bsplineOutputLog << (pkFormat % pk.x).str();
            bsplineOutputLog << "\n";
        }
#endif

        return bsplineCollection;
    }

    XYData getbspline(const XYPointCollection& data, double t, int n, int p)
    {
        XYData pt(0, 0);

        int itp = 0;
        for (int i = 0; i <= n; i++) {
            pt.x = (pt.getX() + data.Data[itp].getX() * bspline_base(i, p, t));
            pt.y = (pt.getY() + data.Data[itp].getY() * bspline_base(i, p, t));
            itp++;
        }
        return pt;
    }

    double bspline_base(int i, int p, double t)
    {
        double n, c1, c2;
        double tn1 = 0;
        double tn2 = 0;
        if (p == 0) {
            if (bspline_T_[i] <= t && t < bspline_T_[i + 1] && bspline_T_[i] < bspline_T_[i + 1]) {
                n = 1;
            }
            else {
                n = 0;
            }
        }
        else {
            if ((bspline_T_[i + p] - bspline_T_[i]) == 0) {
                c1 = 0;
            }
            else {
                tn1 = bspline_base(i, (p - 1), t);
                c1 = (t - bspline_T_[i]) / (bspline_T_[i + p] - bspline_T_[i]);
            }
            if ((bspline_T_[i + p + 1] - bspline_T_[i + 1]) == 0) {
                c2 = 0;
            }
            else {
                tn2 = bspline_base((i + 1), (p - 1), t);
                c2 = (bspline_T_[i + p + 1] - t) / (bspline_T_[i + p + 1] - bspline_T_[i + 1]);
            }
            n = (c1 * tn1) + (c2 * tn2);
        }
        return n;
    }
};

class LinearInterpolation
{
    public:

    XYPointCollection Run(const XYPointCollection& data, int PtNum)
    {
        std::vector<XYData> Smoothdata(PtNum);
        float intv = (data.Data[data.PointCount() - 1].getX() - data.Data[0].getX()) / (float)PtNum;
        float rt = data.Data[0].getX();
        for (int i = 0; i < PtNum; i++) {
            Smoothdata[i] = XYData{ intv * i + rt, -1 };
        }
        int index = 0;
        for (const XYData& point : data.Data)
        {
            //XYData closet = Smoothdata[index];
            bool found = false;
            for (int i = index; i < PtNum - 1; i++) {
                if (Smoothdata[i].getX() <= point.getX() && Smoothdata[i + 1].getX() > point.getX()) {
                    Smoothdata[i].y = (point.getY());
                    index = i;
                    found = true;
                    break;
                }
            }
            if (!found) {
                Smoothdata[PtNum - 1].y = (point.getY());
                index = PtNum - 1;
            }
        }

        bool gapfound = false;
        int startidx = 0;
        int endidx = 0;
        float startintensity = Smoothdata[0].getY();
        float endintensity = Smoothdata[0].getY();

        for (int i = 1; i < PtNum; i++)
        {
            if (gapfound && Smoothdata[i].getY() != -1) {
                endidx = i;
                endintensity = Smoothdata[i].getY();
                Smoothdata[(startidx + endidx) / 2].y = ((startintensity + endintensity) / 2);
                i = startidx;
                gapfound = false;
            }
            if (!gapfound && Smoothdata[i].getY() == -1) {
                startidx = i - 1;
                startintensity = Smoothdata[i - 1].getY();
                gapfound = true;
            }
        }
        XYPointCollection returndata;
        swap(returndata.Data, Smoothdata);
        return returndata;
    }
};


/*
 *This class implements the Continuous Wavelet Transform (CWT), Mexican Hat,
 * over raw datapoints of a certain spectrum. After get the spectrum in the
 * wavelet's time domain, we use the local maxima to detect possible peaks in
 * the original raw datapoints.
 * Described in Tautenhahn, R., Bottcher, C. & Neumann, S. 
 * Highly sensitive feature detection for high resolution LC/MS. 
 * BMC Bioinformatics 9, 504 (2008).
 */
class WaveletMassDetector
{
    /**
     * Parameters of the wavelet, NPOINTS is the number of wavelet values to use
     * The WAVELET_ESL & WAVELET_ESL indicates the Effective Support boundaries
     */
    double NPOINTS;
    int WAVELET_ESL = -5;
    int WAVELET_ESR = 5;
    #define waveletDebug false
    const InstrumentParameter& parameter;
    std::vector<XYData>& DataPoint;
    double waveletWindow = (double) 0.3;
    std::vector<float> MEXHAT;
    double NPOINTS_half;

    public:

    WaveletMassDetector(const InstrumentParameter& parameter, std::vector<XYData>& DataPoint, int NoPoints) : parameter(parameter), DataPoint(DataPoint)
    {
        NPOINTS = NoPoints;

        double wstep = ((WAVELET_ESR - WAVELET_ESL) / NPOINTS);
        MEXHAT.resize(NPOINTS);

        double waveletIndex = WAVELET_ESL;
        for (int j = 0; j < NPOINTS; j++)
        {
            // Pre calculate the values of the wavelet
            MEXHAT[j] = cwtMEXHATreal(waveletIndex, waveletWindow, 0.0);
            waveletIndex += wstep;
        }

        NPOINTS_half = NPOINTS / 2;
        d = (int) NPOINTS / (WAVELET_ESR - WAVELET_ESL);
    }
    int d;
    //ArrayList<XYData>[] waveletCWT;
    
    //List of peak ridge (local maxima)
    std::vector<std::unique_ptr<std::vector<XYData>>> PeakRidge;

    void Run()
    {
        //"Intensities less than this value are interpreted as noise",                
        //"Scale level",
        //"Number of wavelet'scale (coeficients) to use in m/z peak detection"
        //"Wavelet window size (%)",
        //"Size in % of wavelet window to apply in m/z peak detection");        
        int maxscale = (int) (std::max(std::min((DataPoint[DataPoint.size() - 1].getX() - DataPoint[0].getX()), parameter.MaxCurveRTRange), 0.5f) * parameter.NoPeakPerMin / (WAVELET_ESR + WAVELET_ESR));

        //waveletCWT = new ArrayList[15];
        PeakRidge.resize(maxscale);
        //XYData maxint = new XYData(0f, 0f);
        for (int scaleLevel = 0; scaleLevel < maxscale; scaleLevel++)
        {
            std::vector<XYData> wavelet = performCWT(scaleLevel * 2 + 5);
            PeakRidge[scaleLevel] = std::make_unique<std::vector<XYData>>();
            //waveletCWT[scaleLevel] = wavelet;
            XYData lastpt = wavelet[0];
            XYData localmax { 0, 0 };
            XYData startpt = wavelet[0];

            bool increasing = false;
            bool decreasing = false;
            XYData localmaxint { 0, 0 };

            for (size_t cwtidx = 1; cwtidx < wavelet.size(); cwtidx++)
            {
                XYData& CurrentPoint = wavelet[cwtidx];
                if (CurrentPoint.getY() > lastpt.getY()) {//the peak is increasing
                    if (decreasing) {//first increasing point, last point was a possible local minimum
                        //check if the peak was symetric
                        if (localmax.y > 0 && (lastpt.getY() <= startpt.getY() || abs(lastpt.getY() - startpt.getY()) / localmax.getY() < parameter.SymThreshold)) {
                            PeakRidge[scaleLevel]->push_back(localmax);
                            localmax = CurrentPoint;
                            startpt = lastpt;
                        }
                    }
                    increasing = true;
                    decreasing = false;
                } else if (CurrentPoint.getY() < lastpt.getY()) {//peak decreasing
                    if (increasing) {//first point decreasing, last point was a possible local maximum
                        if (localmax.getY() < lastpt.getY()) {
                            localmax = lastpt;
                        }
                    }
                    decreasing = true;
                    increasing = false;
                }
                lastpt = CurrentPoint;
                if (CurrentPoint.getY() > localmaxint.getY()) {
                    localmaxint = CurrentPoint;
                }
                if (cwtidx == wavelet.size() - 1 && decreasing) {
                    if (localmax.y > 0 && (CurrentPoint.getY() <= startpt.getY() || abs(CurrentPoint.getY() - startpt.getY()) / localmax.getY() < parameter.SymThreshold)) {
                        PeakRidge[scaleLevel]->push_back(localmax);
                    }
                }
            }

            if (!waveletDebug) {
                wavelet.clear();
                //wavelet = null;
            }
        }
    }

    private:

    /**
     * Perform the CWT over raw data points in the selected scale level
     *
     *
     */
    std::vector<XYData> performCWT(int scaleLevel)
    {
        int length = DataPoint.size();
        std::vector<XYData> cwtDataPoints(length);

        int a_esl = scaleLevel * WAVELET_ESL;
        int a_esr = scaleLevel * WAVELET_ESR;
        int NPOINTS_half = (int) this->NPOINTS_half;
        int NPOINTS = (int) this->NPOINTS;
        double sqrtScaleLevel = sqrt(scaleLevel);
        /*std::vector<float> intensities(length + a_esr);
        std::vector<float> yValues(DataPoint.size());
        for(size_t i=0 ;i < yValues.size(); ++i)
            yValues[i] = DataPoint[i].y;*/

        for (int dx = 0; dx < length; dx++)
        {
            /*
             * Compute wavelet boundaries
             */
            int t1 = a_esl + dx;
            if (t1 < 0) {
                t1 = 0;
            }
            int t2 = a_esr + dx;
            if (t2 >= length) {
                t2 = (length - 1);
            }

            /*
             * Perform convolution
             */
            float intensity = 0;
            int ind;
            //float* intensityPtr = &intensities[0];
            //float* yValuesPtr = &yValues[t1];
            //float* MEXHATPtr = &MEXHAT[0];
            for (int i = t1; i <= t2; ++i/*, ++yValuesPtr*/) {
                ind = NPOINTS_half + (d * (i - dx) / scaleLevel);
                ind = boost::algorithm::clamp(ind, 0, NPOINTS - 1);
                intensity += DataPoint[i].y * MEXHAT[ind];
                //intensity += *yValuesPtr * MEXHATPtr[ind];
                //intensityPtr[i] = yValuesPtr[i] * MEXHATPtr[ind];
            }
            //intensity = std::accumulate(intensities.begin() + t1, intensities.begin() + t2 + 1, 0.f);
            intensity /= sqrtScaleLevel;
            // Eliminate the negative part of the wavelet map
            if (intensity < 0) {
                intensity = 0;
            }
            cwtDataPoints[dx].x = DataPoint[dx].getX();
            cwtDataPoints[dx].y = intensity;
        }
        return cwtDataPoints;
    }

    /**
     * This function calculates the wavelets's coefficients in Time domain
     *
     * @param double x Step of the wavelet
     * @param double a Window Width of the wavelet
     * @param double b Offset from the center of the peak
     */
    double cwtMEXHATreal(double x, double window, double b)
    {
        /*
         * c = 2 / ( sqrt(3) * pi^(1/4) )
         */
        double c = 0.8673250705840776;
        double TINY = 1E-200;
        double x2;

        if (window == 0.0) {
            window = TINY;
        }
        //x-b=t
        //window=delta
        x = (x - b) / window;
        x2 = x * x;
        return c * (1.0 - x2) * std::exp(-x2 / 2);
    }
    /**
     * This function searches for maximums from wavelet data points
     */
};


struct MassDefect
{
    bool InMassDefectRange(float mass, float d)
    {
        //upper = 0.00052738*x + 0.066015 +0.1 
        //lower = 0.00042565*x + 0.00038210 -0.1

        double u = GetMassDefect(0.00052738*mass + 0.066015 + d);
        double l = GetMassDefect(0.00042565*mass + 0.00038210 - d);

        double defect = GetMassDefect(mass);
        if (u > l) {
            return (defect >= l && defect <= u);
        }
        return (defect >= l || defect <= u);
    }

    double GetMassDefect(double mass)
    {
        return mass - std::floor(mass);
    }
};


struct Regression
{
    /// <summary>
    /// Equation class: y=mx+b
    /// </summary>
    struct Equation {

        float Bvalue;
        float Mvalue;
        float SDvalue;
        float R2value;
        int NoPoints;
        float CorrelationCoffe;

        string GetEquationText() {
            return "Y=(" + lexical_cast<string>(std::round(Mvalue * 1000) / 1000) + ")X+" + lexical_cast<string>(std::round(Bvalue * 1000) / 1000);
        }
    };

    Equation equation;
    int MinPoint = 3;

    Regression(const XYPointCollection& pointset) : pointset(pointset)
    {
        FindEquation();
    }

    bool valid() {
        return pointset.PointCount() >= MinPoint;
    }

    float GetX(float y) {
        return (y - equation.Bvalue) / equation.Mvalue;
    }

    float GetY(float x) {
        return equation.Mvalue * x + equation.Bvalue;
    }

    float GetR2() {
        ComputeR2();
        return equation.R2value;
    }

    private:

    const XYPointCollection& pointset;
    float SigXY = 0;
    float SigX = 0;
    float SigY = 0;
    float SigX2 = 0;
    float SigY2 = 0;
    float SST;
    float SSR;
    float SXX;
    float SYY;
    float SXY;
    float MeanY;
    float MeanX;
    float max_x = 0;
    float min_x = std::numeric_limits<float>::max();
    float max_y = 0;
    float min_y = std::numeric_limits<float>::max();

    void FindEquation()
    {
        for (int i = 0; i < pointset.PointCount(); i++) {
            const XYData& point = pointset.Data[i];
            SigXY += point.getX() * point.getY();
            SigX += point.getX();
            SigY += point.getY();
            SigX2 += point.getX() * point.getX();
            SigY2 += point.getY() * point.getY();
            if (point.getX() > max_x) {
                max_x = point.getX();
            }
            if (point.getX() < min_x) {
                min_x = point.getX();
            }
            if (point.getY() > max_y) {
                max_y = point.getY();
            }
            if (point.getY() < min_y) {
                min_y = point.getY();
            }
        }
        equation.Mvalue = ((pointset.PointCount() * SigXY) - (SigX * SigY)) / ((pointset.PointCount() * SigX2) - (SigX * SigX));
        equation.Bvalue = (SigY - (equation.Mvalue * SigX)) / pointset.PointCount();
        equation.NoPoints = pointset.PointCount();
        MeanY = SigY / pointset.PointCount();
        MeanX = SigX / pointset.PointCount();
        //ComputeSD();
        //ComputeCorrelationCoff();
    }

    void ComputeCorrelationCoff()
    {
        ComputeSXY();
        ComputeSXX();
        ComputeSYY();
        equation.CorrelationCoffe = (float)(SXY / std::pow((double)SXX * SYY, 0.5));
    }

    void ComputeSXY()
    {
        SXY = 0;
        for (int i = 0; i < pointset.PointCount(); i++) {
            SXY += (pointset.Data[i].getX() - MeanX) * (pointset.Data[i].getY() - MeanY);
        }
    }

    void ComputeSXX()
    {
        SXX = 0;
        for (int i = 0; i < pointset.PointCount(); i++) {
            SXX += (pointset.Data[i].getX() - MeanX) * (pointset.Data[i].getX() - MeanX);
        }
    }

    void ComputeSYY()
    {
        SYY = 0;
        for (int i = 0; i < pointset.PointCount(); i++) {
            SYY += (pointset.Data[i].getY() - MeanY) * (pointset.Data[i].getY() - MeanY);
        }
    }

    void ComputeSD()
    {
        equation.SDvalue = (float)std::sqrt((double)((((pointset.PointCount() * SigY2) - (SigY * SigY)) - equation.Mvalue * ((pointset.PointCount() * SigXY) - (SigX * SigY))) / pointset.PointCount()));
    }

    void ComputeR2()
    {
        ComputeSST();
        ComputeSSR();
        equation.R2value = (SST - SSR) / SST;
    }

    void ComputeSST()
    {
        SST = 0;
        for (int i = 0; i < pointset.PointCount(); i++) {
            SST += (pointset.Data[i].getY() - MeanY) * (pointset.Data[i].getY() - MeanY);
        }
    }

    void ComputeSSR()
    {
        SSR = 0;
        for (int i = 0; i < pointset.PointCount(); i++) {
            SSR += (pointset.Data[i].getY() - (GetY(pointset.Data[i].getX()))) * (pointset.Data[i].getY() - (GetY(pointset.Data[i].getX())));
        }
    }
};


struct PearsonCorr
{
    static float CalcCorrNeighborBin(const XYPointCollection& CollectionA, const XYPointCollection& CollectionB)
    {
        int num = (int)((std::min(CollectionA.Data.at(CollectionA.PointCount() - 1).getX(), CollectionB.Data.at(CollectionB.PointCount() - 1).getX()) - std::max(CollectionA.Data.at(0).getX(), CollectionB.Data.at(0).getX())) * 100);
        float timeinterval = 1 / (float) 100.0;

        vector<float> arrayA(num);
        vector<float> arrayB(num);

        float start = std::max(CollectionA.Data.at(0).getX(), CollectionB.Data.at(0).getX());

        for (int i = 0; i < num - 1; i++) {
            float low = start + i * timeinterval;
            float up = start + (i + 1) * timeinterval;

            for (int j = 0; j < CollectionA.PointCount(); j++) {
                if (CollectionA.Data[j].getX() >= low && CollectionA.Data[j].getX() < up) {
                    float intenlow = CollectionA.Data[j].getY() * (1 - (CollectionA.Data[j].getX() - low) / timeinterval);
                    float intenup = CollectionA.Data[j].getY() * (1 - (up - CollectionA.Data[j].getX()) / timeinterval);
                    if (intenlow > arrayA[i]) {
                        arrayA[i] = intenlow;
                    }
                    if (intenup > arrayA[i + 1]) {
                        arrayA[i + 1] = intenup;
                    }
                }
                else if (CollectionA.Data[j].getX() > up) {
                    break;
                }
            }

            for (int j = 0; j < CollectionB.PointCount(); j++) {
                if (CollectionB.Data[j].getX() >= low && CollectionB.Data[j].getX() < up) {
                    float intenlow = CollectionB.Data[j].getY() * (1 - (CollectionB.Data[j].getX() - low) / timeinterval);
                    float intenup = CollectionB.Data[j].getY() * (1 - (up - CollectionB.Data[j].getX()) / timeinterval);
                    if (intenlow > arrayB[i]) {
                        arrayB[i] = intenlow;
                    }
                    if (intenup > arrayB[i + 1]) {
                        arrayB[i + 1] = intenup;
                    }
                }
                else if (CollectionB.Data[j].getX() > up) {
                    break;
                }
            }
        }

        XYPointCollection pointset;
        for (int i = 0; i < num; i++)
        {
            if (arrayA[i] > 0 && arrayB[i] > 0) {
                pointset.AddPoint(arrayA[i], arrayB[i]);
            }
        }

        float R2 = 0;

        if (pointset.PointCount() > 5) {
            Regression regression(pointset);
            if (regression.equation.Mvalue > 0) {
                R2 = regression.GetR2();
            }
        }
        return R2;
    }

    static float CalcCorr(const XYPointCollection& CollectionA, const XYPointCollection& CollectionB, int NoPointPerInterval)
    {
        int num = std::max(CollectionA.PointCount(), CollectionB.PointCount()) / 2;
        float timeinterval = 2 / (float)NoPointPerInterval;
        if (num < 6) {
            return 0;
        }

        vector<float> arrayA(num);
        vector<float> arrayB(num);
        int size = 0;

        float start = std::max(CollectionA.Data.at(0).getX(), CollectionB.Data.at(0).getX());

        int i = 0;
        float low = start;
        float up = start + timeinterval;

        for (int j = 0; j < CollectionA.PointCount(); j++) {
            while (CollectionA.Data[j].getX() > up) {
                i++;
                low = up;
                up = low + timeinterval;
            }
            if (i >= num) {
                break;
            }
            if (CollectionA.Data[j].getX() >= low && CollectionA.Data[j].getX() < up) {
                if (CollectionA.Data[j].getY() > arrayA[i]) {
                    arrayA[i] = CollectionA.Data[j].getY();
                }
            }
        }
        i = 0;
        low = start;
        up = start + timeinterval;
        for (int j = 0; j < CollectionB.PointCount(); j++) {
            while (CollectionB.Data[j].getX() > up) {
                i++;
                low = up;
                up = low + timeinterval;
            }
            if (i >= num) {
                break;
            }
            if (CollectionB.Data[j].getX() >= low && CollectionB.Data[j].getX() < up) {
                if (CollectionB.Data[j].getY() > arrayB[i]) {
                    arrayB[i] = CollectionB.Data[j].getY();
                    if (arrayA[i] > 0 && arrayB[i] > 0)
                        ++size;
                }
            }
        }

        for (int idx = 1; idx < num - 1; idx++) {
            if (arrayA[idx] == 0) {
                arrayA[idx] = (arrayA[idx - 1] + arrayA[idx + 1]) / 2;
            }
            if (arrayB[idx] == 0) {
                arrayB[idx] = (arrayB[idx - 1] + arrayB[idx + 1]) / 2;
            }
        }

        XYPointCollection pointset;
        pointset.Data.reserve(size);
        for (int idx = 0; idx < num; idx++) {
            if (arrayA[idx] > 0 && arrayB[idx] > 0) {
                pointset.AddPoint(arrayA[idx], arrayB[idx]);
            }
        }

        float R2 = 0;
        if (pointset.PointCount() > 5) {
            Regression regression(pointset);
            if (regression.equation.Mvalue > 0) {
                R2 = regression.GetR2();
            }
        }
        return R2;
    }
};


class ChiSquareGOF
{
    using chi_squared = boost::math::chi_squared;

    std::vector<chi_squared> chimodels;

    public:

    ChiSquareGOF(int maxpeak)
    {
        for (int i = 1; i < maxpeak; i++)
            chimodels.emplace_back(chi_squared(i));
    }

    float GetGoodNessOfFitProb(const vector<float>& expected, const vector<float>& observed) const
    {
        float gof = 0;
        int nopeaks = 0;
        for (size_t i = 0; i < std::min(observed.size(), expected.size()); i++)
        {
            if (observed[i] > 0)
            {
                float error = expected[i] - observed[i];
                gof += (error * error) / (expected[i] * expected[i]);
                nopeaks++;
            }
        }

        if (std::isnan(gof) || nopeaks < 2)
            return 0;

        //if (chimodels[nopeaks - 2] == null)
        //    std::cout << std::endl;

        float prob = 1 - (float) boost::math::cdf(chimodels[nopeaks - 2], gof);
        return prob;
    }
};


} // namespace DiaUmpire

#endif // !_DIAUMPIRE_BSPLINE_HPP_
