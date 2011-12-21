/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GImage.h"
#include "GBitTable.h"
#include <math.h>
#include "GError.h"
#include <stdlib.h>
//#include "GBezier.h"
#include "GBits.h"
#include "GFile.h"
#include "GRect.h"
#include "GFourier.h"
#include "GOptimizer.h"
#include "GHillClimber.h"
#include "GMath.h"
#include <vector>
#include <algorithm>
#include <string.h>
#include <sstream>
#include <cmath>
#include "zlib.h"
#include "png.h"

namespace GClasses {
using std::vector;
using std::string;


#ifdef WINDOWS
#	include <windows.h>
#else
typedef unsigned int DWORD;
typedef long LONG;
typedef short WORD;


#pragma pack(1)
typedef struct tagBITMAPINFOHEADER {
    DWORD  biSize;
    LONG   biWidth;
    LONG   biHeight;
    WORD   biPlanes;
    WORD   biBitCount;
    DWORD  biCompression;
    DWORD  biSizeImage;
    LONG   biXPelsPerMeter;
    LONG   biYPelsPerMeter;
    DWORD  biClrUsed;
    DWORD  biClrImportant;
} BITMAPINFOHEADER;

typedef struct tagBITMAPFILEHEADER { 
  WORD    bfType; 
  DWORD   bfSize; 
  WORD    bfReserved1; 
  WORD    bfReserved2; 
  DWORD   bfOffBits; 
} BITMAPFILEHEADER, *PBITMAPFILEHEADER; 
#pragma pack()

#endif

const char* rgbToHex(char* pOutHex, unsigned int c)
{
	GBits::byteToHexBigEndian(gRed(c), pOutHex);
	GBits::byteToHexBigEndian(gGreen(c), pOutHex + 2);
	GBits::byteToHexBigEndian(gBlue(c), pOutHex + 4);
	pOutHex[6] = '\0';
	return pOutHex;
}

unsigned int hexToRgb(const char* szHex)
{
	if( szHex[0] == '\0' ||
		szHex[1] == '\0' ||
		szHex[2] == '\0' ||
		szHex[3] == '\0' ||
		szHex[4] == '\0' ||
		szHex[5] == '\0')
			ThrowError("A color value should consist of six hexadecimal digits");
	unsigned char r = GBits::hexToByte(szHex[1], szHex[0]);
	unsigned char g = GBits::hexToByte(szHex[3], szHex[2]);
	unsigned char b = GBits::hexToByte(szHex[5], szHex[4]);
	return gRGB(r, g, b);
}

void rgbToHsv(unsigned int c, float* pHue, float* pSaturation, float* pValue)
{
	int r = gRed(c);
	int g = gGreen(c);
	int b = gBlue(c);
	int min;
	if(b >= g && b >= r)
	{
		// Blue
		min = std::min(r, g);
		if(b != min)
		{
			*pHue = (float)(r - g) / ((b - min) * 6) + (float)2 / 3;
			*pSaturation = (float)1 - (float)min / (float)b;
		}
		else
		{
			*pHue = 0;
			*pSaturation = 0;
		}
		*pValue = (float)b / 255;
	}
	else if(g >= r)
	{
		// Green
		min = std::min(b, r);
		if(g != min)
		{
			*pHue = (float)(b - r) / ((g - min) * 6) + (float)1 / 3;
			*pSaturation = (float)1 - (float)min / (float)g;
		}
		else
		{
			*pHue = 0;
			*pSaturation = 0;
		}
		*pValue = (float)g / 255;
	}
	else
	{
		// Red
		min = std::min(g, b);
		if(r != min)
		{
			*pHue = (float)(g - b) / ((r - min) * 6);
			if(*pHue < 0)
				(*pHue) += (float)1;
			*pSaturation = (float)1 - (float)min / (float)r;
		}
		else
		{
			*pHue = 0;
			*pSaturation = 0;
		}
		*pValue = (float)r / 255;
	}
}

unsigned int gAHSV(int alpha, float hue, float saturation, float value)
{
	GAssert(hue >= 0 && hue <= 1 && saturation >= 0 && saturation <= 1 && value >= 0 && value <= 1); // out of range
	hue -= floor(hue);
	hue *= 6;
	int h = (int)hue;
	float f = hue - h;
	float p = value * (0.99999f - saturation);
	float q = value * (0.99999f - ((h & 1) == 0 ? 1.0f - f : f) * saturation);
	float v = value - 0.00001f;
	if(h < 3)
	{
		if(h == 0) return gARGB(alpha, (int)(v * 256), (int)(q * 256), (int)(p * 256));
		else if(h == 1) return gARGB(alpha, (int)(q * 256), (int)(v * 256), (int)(p * 256));
		else return gARGB(alpha, (int)(p * 256), (int)(v * 256), (int)(q * 256));
	}
	else
	{
		if(h == 3) return gARGB(alpha, (int)(p * 256), (int)(q * 256), (int)(v * 256));
		else if(h == 4) return gARGB(alpha, (int)(q * 256), (int)(p * 256), (int)(v * 256));
		else return gARGB(alpha, (int)(v * 256), (int)(p * 256), (int)(q * 256));
	}
}

GImage::GImage()
{
	m_pPixels = NULL;
	m_width = 0;
	m_height = 0;
}

GImage::~GImage()
{
	delete[] m_pPixels;
}

void GImage::setSize(unsigned int nWidth, unsigned int nHeight)
{
	if(nWidth == m_width && nHeight == m_height)
		return;
	delete[] m_pPixels;
	if(nWidth == 0 || nHeight == 0)
		m_pPixels = NULL;
	else
	{
		unsigned int nSize = nWidth * nHeight;
		m_pPixels = new unsigned int[nSize];
		memset(m_pPixels, '\0', nSize * sizeof(unsigned int));
	}
	m_width = nWidth;
	m_height = nHeight;
}

void GImage::copy(GImage* pSourceImage)
{
	setSize(pSourceImage->width(), pSourceImage->height());
	memcpy(m_pPixels, pSourceImage->pixels(), m_width * m_height * sizeof(unsigned int));
}

void GImage::copyRect(GImage* pSourceImage, int nLeft, int nTop, int nRight, int nBottom)
{
	int nWidth = nRight - nLeft + 1;
	int nHeight = nBottom - nTop + 1;
	setSize(nWidth, nHeight);
	int x, y;
	unsigned int c;
	for(y = 0; y < nHeight; y++)
	{
		for(x = 0; x < nWidth; x++)
		{
			c = pSourceImage->pixel(x + nLeft, y + nTop);
			setPixel(x, y, c);
		}
	}
}

void GImage::clear(unsigned int color)
{
	unsigned int nSize = m_width * m_height;
	unsigned int nPos;
	for(nPos = 0; nPos < nSize; nPos++)
		m_pPixels[nPos] = color;
}

void GImage::setPixelTranslucent(int nX, int nY, unsigned int color, double dOpacity)
{
	unsigned int cOld = pixel(nX, nY);
	unsigned int c = gRGB(
			(int)((1 - dOpacity) * gRed(cOld) + dOpacity * gRed(color)),
			(int)((1 - dOpacity) * gGreen(cOld) + dOpacity * gGreen(color)),
			(int)((1 - dOpacity) * gBlue(cOld) + dOpacity * gBlue(color))
		);
	setPixel(nX, nY, c);
}

void GImage::setPixelIfInRange(int nX, int nY, unsigned int color)
{
	if((unsigned int)nX < m_width && (unsigned int)nY < m_height)
		setPixel(nX, nY, color);
}

unsigned int GImage::pixelNearest(int nX, int nY) const
{
	return pixel(std::max(0, std::min((int)m_width - 1, nX)), std::max(0, std::min((int)m_height - 1, nY)));
}

unsigned int GImage::interpolatePixel(float dX, float dY)
{
	int nX = (int)dX;
	int nY = (int)dY;
	float dXDif = dX - nX;
	float dYDif = dY - nY;
	unsigned int c1;
	unsigned int c2;
	c1 = pixelNearest(nX, nY);
	c2 = pixelNearest(nX + 1, nY);
	float dA1 = dXDif * (float)gAlpha(c2) + (1 - dXDif) * (float)gAlpha(c1);
	float dR1 = dXDif * (float)gRed(c2) + (1 - dXDif) * (float)gRed(c1);
	float dG1 = dXDif * (float)gGreen(c2) + (1 - dXDif) * (float)gGreen(c1);
	float dB1 = dXDif * (float)gBlue(c2) + (1 - dXDif) * (float)gBlue(c1);
	c1 = pixelNearest(nX, nY + 1);
	c2 = pixelNearest(nX + 1, nY + 1);
	float dA2 = dXDif * (float)gAlpha(c2) + (1 - dXDif) * (float)gAlpha(c1);
	float dR2 = dXDif * (float)gRed(c2) + (1 - dXDif) * (float)gRed(c1);
	float dG2 = dXDif * (float)gGreen(c2) + (1 - dXDif) * (float)gGreen(c1);
	float dB2 = dXDif * (float)gBlue(c2) + (1 - dXDif) * (float)gBlue(c1);
	return gARGB((int)(dYDif * dA2 + (1 - dYDif) * dA1),
				(int)(dYDif * dR2 + (1 - dYDif) * dR1),
				(int)(dYDif * dG2 + (1 - dYDif) * dG1),
				(int)(dYDif * dB2 + (1 - dYDif) * dB1));
}

void GImage::loadByExtension(const char* szFilename)
{
	PathData pd;
	GFile::parsePath(szFilename, &pd);
	const char* szExt = &szFilename[pd.extStart];
	if(_stricmp(szExt, ".png") == 0)
		loadPng(szFilename);
	else if(_stricmp(szExt, ".bmp") == 0)
		loadBmp(szFilename);
	else if(_stricmp(szExt, ".ppm") == 0)
		loadPpm(szFilename);
	else if(_stricmp(szExt, ".pgm") == 0)
		loadPgm(szFilename);
	else
		ThrowError("Unrecognized extension");
}

void GImage::saveByExtension(const char* szFilename)
{
	PathData pd;
	GFile::parsePath(szFilename, &pd);
	const char* szExt = &szFilename[pd.extStart];
	if(_stricmp(szExt, ".png") == 0)
		savePng(szFilename);
	else if(_stricmp(szExt, ".bmp") == 0)
		saveBmp(szFilename);
	else if(_stricmp(szExt, ".ppm") == 0)
		savePpm(szFilename);
	else if(_stricmp(szExt, ".pgm") == 0)
		savePgm(szFilename);
	else
		ThrowError("Unrecognized extension");
}

void GImage::loadPpm(const char* szFilename)
{
	FILE* pFile = fopen(szFilename, "rb");
	if(!pFile)
		ThrowError("Failed to open file: ", szFilename);
	FileHolder hFile(pFile);
	char pBuff[2];
	if(fread(pBuff, 2, 1, pFile) != 1)
		ThrowError("Error reading from file: ", szFilename);
	if(pBuff[0] != 'P' && pBuff[0] != 'p')
		ThrowError("Unrecognized file format");
	if(pBuff[1] == '3')
		loadPixMap(pFile, true, false);
	else if(pBuff[1] == '6')
		loadPixMap(pFile, false, false);
	else
		ThrowError("Unrecognized format");
}

int ReadNextPixMapInteger(FILE* pFile)
{
	// Read past any white space and skip comments
	int n;
	while(true)
	{
		n = fgetc(pFile);
		if(n == '#')
		{
			while(true)
			{
				n = fgetc(pFile);
				if(n == EOF)
					return false;
				if(n == '\n')
					break;
			}
		}
		if(n == EOF)
			return false;
		if(n > 32)
			break;
	}

	// Read the integer
	char pBuff[16];
	pBuff[0] = n;
	int nPos = 1;
	while(true)
	{
		n = fgetc(pFile);
		if(n == EOF)
			return -1;
		if(n <= 32)
			break;
		pBuff[nPos] = n;
		nPos++;
		if(nPos >= 16)
			return -1;
	}
	pBuff[nPos] = '\0';
	return atoi(pBuff);
}

void GImage::loadPixMap(FILE* pFile, bool bTextData, bool bGrayScale)
{
	if(!pFile)
		ThrowError("expected a valid stream");
	int nWidth = ReadNextPixMapInteger(pFile);
	if(nWidth < 1)
		ThrowError("invalid width");
	int nHeight = ReadNextPixMapInteger(pFile);
	if(nHeight < 1)
		ThrowError("invalid height");
	int nRange = ReadNextPixMapInteger(pFile) + 1;
	if(nRange < 2)
		ThrowError("invalid range");

	// Read the data
	setSize(nWidth, nHeight);
	int x, y, r, g, b;
	if(bTextData)
	{
		for(y = 0; y < nHeight; y++)
		{
			for(x = 0; x < nWidth; x++)
			{
				if(bGrayScale)
				{
					g = ReadNextPixMapInteger(pFile);
					if(g < 0)
						ThrowError("invalid value");
					setPixel(x, y, gRGB(g, g, g));
				}
				else
				{
					r = ReadNextPixMapInteger(pFile);
					if(r < 0)
						ThrowError("invalid value");
					g = ReadNextPixMapInteger(pFile);
					if(g < 0)
						ThrowError("invalid value");
					b = ReadNextPixMapInteger(pFile);
					if(b < 0)
						ThrowError("invalid value");
					setPixel(x, y, gRGB(r, g, b));
				}
			}
		}
	}
	else
	{
		char pBuff[16];
		unsigned int nRed;
		unsigned int nGreen;
		unsigned int nBlue;
		for(y = 0; y < nHeight; y++)
		{
			for(x = 0; x < nWidth; x++)
			{
				if(bGrayScale)
				{
					if(fread(pBuff, 1, 1, pFile) != 1)
						ThrowError("error reading from file");
					nRed = (pBuff[0] << 8) / nRange;
					setPixel(x, y, gRGB(nRed, nRed, nRed));
				}
				else
				{
					if(fread(pBuff, 3, 1, pFile) != 1)
						ThrowError("error reading from file");
					nRed = (pBuff[0] << 8) / nRange;
					nGreen = (pBuff[1] << 8) / nRange;
					nBlue = (pBuff[2] << 8) / nRange;
					setPixel(x, y, gRGB(nRed, nGreen, nBlue));
				}
			}
		}
	}
}

void GImage::loadPgm(const char* szFilename)
{
	FILE* pFile = fopen(szFilename, "rb");
	if(!pFile)
		ThrowError("Error opening file: ", szFilename);
	FileHolder hFile(pFile);
	char pBuff[2];
	if(fread(pBuff, 2, 1, pFile) != 1)
		ThrowError("Error reading from file: ", szFilename);
	if(pBuff[0] != 'P' && pBuff[0] != 'p')
		ThrowError("Unrecognized file format: ", szFilename);
	if(pBuff[1] == '2')
		loadPixMap(pFile, true, true);
	else if(pBuff[1] == '5')
		loadPixMap(pFile, false, true);
	else
		ThrowError("Unexpected format");
}

void GImage::savePixMap(FILE* pFile, bool bTextData, bool bGrayScale)
{
	// Write header junk
	std::ostringstream os;
	os << m_width << " " << m_height;
	string tmp = os.str();
	fputs(tmp.c_str(), pFile);
	fputs("\n255\n", pFile);

	// Write pixel data
	unsigned int col;
	for(unsigned int y = 0; y < m_height; y++)
	{
		for(unsigned int x = 0; x < m_width; x++)
		{
			if(bGrayScale)
			{
				col = pixel(x, y);
				unsigned char nGray = gGray(col) >> 8;
				if(fwrite(&nGray, 1, 1, pFile) != 1)
					ThrowError("Error writing to file");
			}
			else
			{
				col = pixel(x, y);
				int n;
				n = gRed(col);
				if(fwrite(&n, 1, 1, pFile) != 1)
					ThrowError("Error writing to file");
				n = gGreen(col);
				if(fwrite(&n, 1, 1, pFile) != 1)
					ThrowError("Error writing to file");
				n = gBlue(col);
				if(fwrite(&n, 1, 1, pFile) != 1)
					ThrowError("Error writing to file");
			}
		}
	}
}

void GImage::savePpm(const char* szFilename)
{
	FILE* pFile = fopen(szFilename, "wb");
	if(!pFile)
		ThrowError("Error creating file: ", szFilename);
	FileHolder hFile(pFile);
	if(fputs("P6\n", pFile) == EOF)
		ThrowError("Error writing to file: ", szFilename);
	savePixMap(pFile, false, false);
}

void GImage::savePgm(const char* szFilename)
{
	FILE* pFile = fopen(szFilename, "wb");
	if(!pFile)
		ThrowError("Error creating file: ", szFilename);
	FileHolder hFile(pFile);
	if(fputs("P5\n", pFile) == EOF)
		ThrowError("Error writing to file: ", szFilename);
	savePixMap(pFile, false, true);
}

inline unsigned int ColorToGrayScale(unsigned int c)
{
	int nGray = gGray(c) >> 8;
	return gRGB(nGray, nGray, nGray);
}

void GImage::convertToGrayScale()
{
	unsigned int nSize = m_width * m_height;
	unsigned int nPos;
	int nGray;
	for(nPos = 0; nPos < nSize; nPos++)
	{
		nGray = gGray(m_pPixels[nPos]) >> 8;
		m_pPixels[nPos] = gARGB(gAlpha(m_pPixels[nPos]), nGray, nGray, nGray);
	}
}

void GImage::equalizeColorSpread()
{
	// Create the histogram data
	unsigned int pnHistData[257];
	memset(pnHistData, '\0', sizeof(int) * 257);
	unsigned int nSize = m_width * m_height;
	unsigned int nPos;
	unsigned int nGray;
	unsigned int nMaxValue = 0;
	for(nPos = 0; nPos < nSize; nPos++)
	{
		nGray = gGray(m_pPixels[nPos]) >> 8;
		pnHistData[nGray]++;
		if(pnHistData[nGray] > nMaxValue)
			nMaxValue = pnHistData[nGray];
	}
	pnHistData[255] += pnHistData[256];
	if(pnHistData[255] > nMaxValue)
		nMaxValue = pnHistData[255];

	// Turn it into cumulative histogram data
	int n;
	for(n = 1; n < 256; n++)
		pnHistData[n] += pnHistData[n - 1];
	int nFactor = pnHistData[255] >> 8;

	// turn it into a picture
	unsigned int col;
	float fFactor;
	for(unsigned int y = 0; y < m_height; y++)
	{
		for(unsigned int x = 0; x < m_width; x++)
		{
			col = pixel(x, y);
			nGray = gGray(col) >> 8;
			fFactor = ((float)pnHistData[nGray] / (float)nFactor) / nGray;
			
			setPixel(x, y, 
				gRGB(
				(char)std::min((int)((float)gRed(col) * fFactor), 255),
				(char)std::min((int)((float)gGreen(col) * fFactor), 255),
				(char)std::min((int)((float)gBlue(col) * fFactor), 255)
					));
		}
	}
}

void GImage::locallyEqualizeColorSpread(int nLocalSize, float fExtent)
{
	if(nLocalSize % 2 != 0)
		nLocalSize++;
	GAssert(nLocalSize > 2); // Local size must be more than 2

	// Create histograms for all the local regions
	int nHalfRegionSize = nLocalSize >> 1;
	int nHorizRegions = (m_width + nHalfRegionSize - 1) / nHalfRegionSize + 1;
	int nVertRegions = (m_height + nHalfRegionSize - 1) / nHalfRegionSize + 1;
	int* pArrHistograms = new int[256 * nHorizRegions * nVertRegions];
	memset(pArrHistograms, '\0', 256 * nHorizRegions * nVertRegions * sizeof(int));
	int* pHist;
	int nHoriz, nVert, nDX, nDY, nX, nY, nGrayscale, n;
	unsigned int col;
	for(nVert = 0; nVert < nVertRegions; nVert++)
	{
		for(nHoriz = 0; nHoriz < nHorizRegions; nHoriz++)
		{
			// Make a histogram for the local region
			pHist = pArrHistograms + 256 * (nHorizRegions * nVert + nHoriz);
			for(nDY = 0; nDY < nLocalSize; nDY++)
			{
				nY = (nVert - 1) * nHalfRegionSize + nDY;
				for(nDX = 0; nDX < nLocalSize; nDX++)
				{
					nX = (nHoriz - 1) * nHalfRegionSize + nDX;
					if((unsigned int)nX < m_width && (unsigned int)nY < m_height)
					{
						col = pixel(nX, nY);
						nGrayscale = gGray(col) >> 8;
						pHist[nGrayscale]++;
					}
				}
			}
			
			// Turn the histogram into cumulative histogram data
			for(n = 1; n < 256; n++)
				pHist[n] += pHist[n - 1];
		}
	}
	
	// Equalize the colors
	float fFactor1, fFactor2, fFactor3, fFactor4, fFactorTop, fFactorBottom, fFactorInterpolated;
	float fInterp;
	for(nY = 0; nY < (int)m_height; nY++)
	{
		nVert = nY / nHalfRegionSize;
		GAssert(nVert < nVertRegions); // Region out of range
		nDY = nY % nHalfRegionSize;
		for(nX = 0; nX < (int)m_width; nX++)
		{
			// Get the Pixel
			nHoriz = nX / nHalfRegionSize;
			GAssert(nHoriz < nHorizRegions); // Region out of range
			nDX = nX % nHalfRegionSize;
			col = pixel(nX, nY);
			nGrayscale = gGray(col) >> 8;
			
			// Calculate equalization factor for quadrant 1
			pHist = pArrHistograms + 256 * (nHorizRegions * nVert + nHoriz);
			fFactor1 = ((float)pHist[nGrayscale] / (float)pHist[255]) * 255 / nGrayscale;
			
			// Calculate equalization factor for quadrant 2
			pHist = pArrHistograms + 256 * (nHorizRegions * nVert + (nHoriz + 1));
			fFactor2 = ((float)pHist[nGrayscale] / (float)pHist[255]) * 255 / nGrayscale;
			
			// Calculate equalization factor for quadrant 3
			pHist = pArrHistograms + 256 * (nHorizRegions * (nVert + 1) + nHoriz);
			fFactor3 = ((float)pHist[nGrayscale] / (float)pHist[255]) * 255 / nGrayscale;
			
			// Calculate equalization factor for quadrant 4
			pHist = pArrHistograms + 256 * (nHorizRegions * (nVert + 1) + (nHoriz + 1));
			fFactor4 = ((float)pHist[nGrayscale] / (float)pHist[255]) * 255 / nGrayscale;
			
			// Interpolate a factor from all 4 quadrants
			fInterp = (float)nDX / (float)(nHalfRegionSize - 1);
			fFactorTop = fInterp * fFactor2 + (1 - fInterp) * fFactor1;
			fFactorBottom = fInterp * fFactor4 + (1 - fInterp) * fFactor3;
			fInterp = (float)nDY / (float)(nHalfRegionSize - 1);
			fFactorInterpolated = (fInterp * fFactorBottom + (1 - fInterp) * fFactorTop) * fExtent + 1 - fExtent;

			// Set the Pixel
			setPixel(nX, nY,
				gRGB(
					ClipChan((int)((float)gRed(col) * fFactorInterpolated)),
					ClipChan((int)((float)gGreen(col) * fFactorInterpolated)),
					ClipChan((int)((float)gBlue(col) * fFactorInterpolated))
					));
		}
	}
}

bool GImage::clipLineEndPoints(int& nX1, int& nY1, int& nX2, int& nY2)
{
	// Check nX1
	if(nX1 < 0)
	{
		if(nX2 < 0)
			return false;
		nY1 += (0 - nX1) * (nY2 - nY1) / (nX2 - nX1);
		nX1 = 0;
	}
	if(nX1 >= (int)m_width)
	{
		if(nX2 >= (int)m_width)
			return false;
		nY1 -= (nX1 - (m_width - 1)) * (nY2 - nY1) / (nX2 - nX1);
		nX1 = (int)m_width - 1;
	}

	// Check nY1
	if(nY1 < 0)
	{
		if(nY2 < 0)
			return false;
		nX1 = std::max(0, std::min((int)m_width - 1, nX1 + (0 - nY1) * (nX2 - nX1) / (nY2 - nY1)));
		nY1 = 0;
	}
	if(nY1 >= (int)m_height)
	{
		if(nY2 >= (int)m_height)
			return false;
		nX1 = std::max(0, std::min((int)m_width - 1, nX1 - (nY1 - ((int)m_height - 1)) * (nX2 - nX1) / (nY2 - nY1)));
		nY1 = (int)m_height - 1;
	}

	// Check nX2
	if(nX2 < 0)
	{
		if(nX1 < 0)
			return false;
		nY2 += (0 - nX2) * (nY2 - nY1) / (nX2 - nX1);
		nX2 = 0;
	}
	if(nX2 >= (int)m_width)
	{
		if(nX1 >= (int)m_width)
			return false;
		nY2 -= (nX2 - ((int)m_width - 1)) * (nY2 - nY1) / (nX2 - nX1);
		nX2 = m_width - 1;
	}

	// Check nY2
	if(nY2 < 0)
	{
		if(nY1 < 0)
			return false;
		nX2 = std::max(0, std::min((int)m_width - 1, nX2 + (0 - nY2) * (nX2 - nX1) / (nY2 - nY1)));
		nY2 = 0;
	}
	if(nY2 >= (int)m_height)
	{
		if(nY1 >= (int)m_height)
			return false;
		nX2 = std::max(0, std::min((int)m_width - 1, nX2 - (nY2 - ((int)m_height - 1)) * (nX2 - nX1) / (nY2 - nY1)));
		nY2 = m_height - 1;
	}
	return true;
}

void GImage::line(int nX1, int nY1, int nX2, int nY2, unsigned int color)
{
	if(!clipLineEndPoints(nX1, nY1, nX2, nY2))
		return;
	lineNoChecks(nX1, nY1, nX2, nY2, color);
}

// Note: This uses the Bresenham line drawing algorithm
void GImage::lineNoChecks(int nX1, int nY1, int nX2, int nY2, unsigned int color)
{
	int n;
	int nXDif = std::abs(nX2 - nX1);
	int nYDif = std::abs(nY2 - nY1);
	int nOverflow;
	int m;
	if(nXDif > nYDif)
	{
		if(nX2 < nX1)
		{
			n = nX2;
			nX2 = nX1;
			nX1 = n;
			n = nY2;
			nY2 = nY1;
			nY1 = n;
		}
		nOverflow = nXDif >> 1;
		m = nY1;
		if(nY1 < nY2)
		{
			for(n = nX1; n <= nX2; n++)
			{
				setPixel(n, m, color);
				nOverflow += nYDif;
				if(nOverflow >= nXDif)
				{
					nOverflow -= nXDif;
					m++;
				}
			}
		}
		else
		{
			for(n = nX1; n <= nX2; n++)
			{
				setPixel(n, m, color);
				nOverflow += nYDif;
				if(nOverflow >= nXDif)
				{
					nOverflow -= nXDif;
					m--;
				}
			}
		}
	}
	else
	{
		if(nY2 < nY1)
		{
			n = nX2;
			nX2 = nX1;
			nX1 = n;
			n = nY2;
			nY2 = nY1;
			nY1 = n;
		}
		nOverflow = nYDif >> 1;
		m = nX1;
		if(nX1 < nX2)
		{
			for(n = nY1; n <= nY2; n++)
			{
				setPixel(m, n, color);
				nOverflow += nXDif;
				if(nOverflow >= nYDif)
				{
					nOverflow -= nYDif;
					m++;
				}
			}
		}
		else
		{
			for(n = nY1; n <= nY2; n++)
			{
				setPixel(m, n, color);
				nOverflow += nXDif;
				if(nOverflow >= nYDif)
				{
					nOverflow -= nYDif;
					m--;
				}
			}
		}
	}
}

void GImage::lineAntiAlias(int nX1, int nY1, int nX2, int nY2, unsigned int color)
{
	if(!clipLineEndPoints(nX1, nY1, nX2, nY2))
		return;
	int n;
	int m;
	int nXDif = std::abs(nX2 - nX1);
	int nYDif = std::abs(nY2 - nY1);
	int nOverflow;
	unsigned int col;
	double d;
	if(nXDif > nYDif)
	{
		if(nX2 < nX1)
		{
			n = nX2;
			nX2 = nX1;
			nX1 = n;
			n = nY2;
			nY2 = nY1;
			nY1 = n;
		}
		nOverflow = 0;
		m = nY1;
		if(nY1 < nY2)
		{
			for(n = nX1; n <= nX2; n++)
			{
				d = (double)nOverflow / nXDif;
				col = pixel(n, m);
				setPixel(n, m, 
					gRGB((unsigned char)(d * gRed(col) + (1 - d) * gRed(color)), (unsigned char)(d * gGreen(col) + (1 - d) * gGreen(color)), (unsigned char)(d * gBlue(col) + (1 - d) * gBlue(color))));
				col = pixel(n, m + 1);
				setPixel(n, m + 1, 
					gRGB((unsigned char)((1 - d) * gRed(col) + d * gRed(color)), (unsigned char)((1 - d) * gGreen(col) + d * gGreen(color)), (unsigned char)((1 - d) * gBlue(col) + d * gBlue(color))));
				nOverflow += nYDif;
				if(nOverflow >= nXDif)
				{
					nOverflow -= nXDif;
					m++;
				}
			}
		}
		else
		{
			for(n = nX1; n <= nX2; n++)
			{
				d = (double)nOverflow / nXDif;
				col = pixel(n, m);
				setPixel(n, m, 
					gRGB((unsigned char)(d * gRed(col) + (1 - d) * gRed(color)), (unsigned char)(d * gGreen(col) + (1 - d) * gGreen(color)), (unsigned char)(d * gBlue(col) + (1 - d) * gBlue(color))));
				col = pixel(n, m - 1);
				setPixel(n, m - 1, 
					gRGB((unsigned char)((1 - d) * gRed(col) + d * gRed(color)), (unsigned char)((1 - d) * gGreen(col) + d * gGreen(color)), (unsigned char)((1 - d) * gBlue(col) + d * gBlue(color))));
				nOverflow += nYDif;
				if(nOverflow >= nXDif)
				{
					nOverflow -= nXDif;
					m--;
				}
			}
		}
	}
	else
	{
		if(nY2 < nY1)
		{
			n = nX2;
			nX2 = nX1;
			nX1 = n;
			n = nY2;
			nY2 = nY1;
			nY1 = n;
		}
		nOverflow = 0;
		m = nX1;
		if(nX1 < nX2)
		{
			for(n = nY1; n <= nY2; n++)
			{
				d = (double)nOverflow / nYDif;
				col = pixel(m, n);
				setPixel(m, n, 
					gRGB((unsigned char)(d * gRed(col) + (1 - d) * gRed(color)), (unsigned char)(d * gGreen(col) + (1 - d) * gGreen(color)), (unsigned char)(d * gBlue(col) + (1 - d) * gBlue(color))));
				col = pixel(m + 1, n);
				setPixel(m + 1, n, 
					gRGB((unsigned char)((1 - d) * gRed(col) + d * gRed(color)), (unsigned char)((1 - d) * gGreen(col) + d * gGreen(color)), (unsigned char)((1 - d) * gBlue(col) + d * gBlue(color))));
				nOverflow += nXDif;
				if(nOverflow >= nYDif)
				{
					nOverflow -= nYDif;
					m++;
				}
			}
		}
		else
		{
			for(n = nY1; n <= nY2; n++)
			{
				d = (double)nOverflow / nYDif;
				col = pixel(m, n);
				setPixel(m, n, 
					gRGB((unsigned char)(d * gRed(col) + (1 - d) * gRed(color)), (unsigned char)(d * gGreen(col) + (1 - d) * gGreen(color)), (unsigned char)(d * gBlue(col) + (1 - d) * gBlue(color))));
				col = pixel(m - 1, n);
				setPixel(m - 1, n, 
					gRGB((unsigned char)((1 - d) * gRed(col) + d * gRed(color)), (unsigned char)((1 - d) * gGreen(col) + d * gGreen(color)), (unsigned char)((1 - d) * gBlue(col) + d * gBlue(color))));
				nOverflow += nXDif;
				if(nOverflow >= nYDif)
				{
					nOverflow -= nYDif;
					m--;
				}
			}
		}
	}
}

void GImage::floodFillRecurser(int nX, int nY, unsigned char nSrcR, unsigned char nSrcG, unsigned char nSrcB, unsigned char nDstR, unsigned char nDstG, unsigned char nDstB, int nTolerance)
{
	unsigned int col;
	int nDif;
	while(nX > 0)
	{
		col = pixel(nX - 1, nY);
		nDif = std::abs((int)gRed(col) - nSrcR) + std::abs((int)gGreen(col) - nSrcG) + std::abs((int)gBlue(col) - nSrcB);
		if(nDif > nTolerance)
			break;
		nX--;
	}
	GBitTable btUp(m_width);
	GBitTable btDn(m_width);
	while(true)
	{
		setPixel(nX, nY, gRGB(nDstR, nDstG, nDstB));

		if(nY > 0)
		{
			col = pixel(nX, nY - 1);
			if(gRed(col) != nDstR || gGreen(col) != nDstG || gBlue(col) != nDstB)
			{
				nDif = std::abs((int)gRed(col) - nSrcR) + std::abs((int)gGreen(col) - nSrcG) + std::abs((int)gBlue(col) - nSrcB);
				if(nDif <= nTolerance)
					btUp.set(nX);
			}
		}
		
		if(nY < (int)m_height - 1)
		{
			col = pixel(nX, nY + 1);
			if(gRed(col) != nDstR || gGreen(col) != nDstG || gBlue(col) != nDstB)
			{
				nDif = std::abs((int)gRed(col) - nSrcR) + std::abs((int)gGreen(col) - nSrcG) + std::abs((int)gBlue(col) - nSrcB);
				if(nDif <= nTolerance)
					btDn.set(nX);
			}
		}

		nX++;
		if(nX >= (int)m_width)
			break;
		col = pixel(nX, nY);
		nDif = std::abs((int)gRed(col) - nSrcR) + std::abs((int)gGreen(col) - nSrcG) + std::abs((int)gBlue(col) - nSrcB);
		if(nDif > nTolerance)
			break;
	}
	bool bPrev = false;
	for(nX = 0; nX < (int)m_width; nX++)
	{
		if(btUp.bit(nX))
		{
			if(!bPrev)
			{
				col = pixel(nX, nY - 1);
				if(gRed(col) != nDstR || gGreen(col) != nDstG || gBlue(col) != nDstB)
					floodFillRecurser(nX, nY - 1, nSrcR, nSrcG, nSrcB, nDstR, nDstG, nDstB, nTolerance);
			}
			bPrev = true;
		}
		else
			bPrev = false;
	}
	bPrev = false;
	for(nX = 0; nX < (int)m_width; nX++)
	{
		if(btDn.bit(nX))
		{
			if(!bPrev)
			{
				col = pixel(nX, nY + 1);
				if(gRed(col) != nDstR || gGreen(col) != nDstG || gBlue(col) != nDstB)
					floodFillRecurser(nX, nY + 1, nSrcR, nSrcG, nSrcB, nDstR, nDstG, nDstB, nTolerance);
			}
			bPrev = true;
		}
		else
			bPrev = false;
	}
}

void GImage::floodFill(int nX, int nY, unsigned int color, int nTolerance)
{
	unsigned int col = pixel(nX, nY);
	floodFillRecurser(nX, nY, gRed(col), gGreen(col), gBlue(col), gRed(color), gGreen(color), gBlue(color), nTolerance);
}

void GImage::boundaryFill(int nX, int nY, unsigned char nBoundaryR, unsigned char nBoundaryG, unsigned char nBoundaryB, unsigned char nFillR, unsigned char nFillG, unsigned char nFillB, int nTolerance)
{
	int nDif;
	unsigned int col = pixel(nX, nY);
	nDif = std::abs((int)gRed(col) - nBoundaryR) + std::abs((int)gGreen(col) - nBoundaryG) + std::abs((int)gBlue(col) - nBoundaryB);
	if(nDif <= nTolerance)
		return;
	while(true)
	{
		col = pixel(nX - 1, nY);
		nDif = std::abs((int)gRed(col) - nBoundaryR) + std::abs((int)gGreen(col) - nBoundaryG) + std::abs((int)gBlue(col) - nBoundaryB);
		if(nDif <= nTolerance || nX < 1)
			break;
		nX--;
	}
	GBitTable btUp(m_width);
	GBitTable btDn(m_width);
	while(true)
	{
		setPixel(nX, nY, gRGB(nFillR, nFillG, nFillB));

		if(nY > 0)
		{
			col = pixel(nX, nY - 1);
			if(gRed(col) != nFillR || gGreen(col) != nFillG || gBlue(col) != nFillB)
			{
				nDif = std::abs((int)gRed(col) - nBoundaryR) + std::abs((int)gGreen(col) - nBoundaryG) + std::abs((int)gBlue(col) - nBoundaryB);
				if(nDif > nTolerance)
					btUp.set(nX);
			}
		}
		
		if(nY < (int)m_height - 1)
		{
			col = pixel(nX, nY + 1);
			if(gRed(col) != nFillR || gGreen(col) != nFillG || gBlue(col) != nFillB)
			{
				nDif = std::abs((int)gRed(col) - nBoundaryR) + std::abs((int)gGreen(col) - nBoundaryG) + std::abs((int)gBlue(col) - nBoundaryB);
				if(nDif > nTolerance)
					btDn.set(nX);
			}
		}

		nX++;
		if(nX >= (int)m_width)
			break;
		col = pixel(nX, nY);
		nDif = std::abs((int)gRed(col) - nBoundaryR) + std::abs((int)gGreen(col) - nBoundaryG) + std::abs((int)gBlue(col) - nBoundaryB);
		if(nDif <= nTolerance)
			break;
	}
	bool bPrev = false;
	for(nX = 0; nX < (int)m_width; nX++)
	{
		if(btUp.bit(nX))
		{
			if(!bPrev)
			{
				col = pixel(nX, nY - 1);
				if(gRed(col) != nFillR || gGreen(col) != nFillG || gBlue(col) != nFillB)
					boundaryFill(nX, nY - 1, nBoundaryR, nBoundaryG, nBoundaryB, nFillR, nFillG, nFillB, nTolerance);
			}
			bPrev = true;
		}
		else
			bPrev = false;
	}
	bPrev = false;
	for(nX = 0; nX < (int)m_width; nX++)
	{
		if(btDn.bit(nX))
		{
			if(!bPrev)
			{
				col = pixel(nX, nY + 1);
				if(gRed(col) != nFillR || gGreen(col) != nFillG || gBlue(col) != nFillB)
					boundaryFill(nX, nY + 1, nBoundaryR, nBoundaryG, nBoundaryB, nFillR, nFillG, nFillB, nTolerance);
			}
			bPrev = true;
		}
		else
			bPrev = false;
	}
}

void GImage::circle(int nX, int nY, float fRadius, unsigned int color)
{
	int radiusSquared = (int)(fRadius * fRadius + (float).5);
	int x = 0;
	int y = (int)fRadius;
	while(y >= x)
	{
		// Draw the eight symmetric pixels
		setPixelIfInRange(nX + x, nY - y, color);
		setPixelIfInRange(nX - x, nY - y, color);
		setPixelIfInRange(nX + x, nY + y, color);
		setPixelIfInRange(nX - x, nY + y, color);
		setPixelIfInRange(nX + y, nY - x, color);
		setPixelIfInRange(nX - y, nY - x, color);
		setPixelIfInRange(nX + y, nY + x, color);
		setPixelIfInRange(nX - y, nY + x, color);

		// Compute the next position
		if((x * x) + (y * y) > radiusSquared)
			y--;
		x++;
	}
}

void GImage::circleFill(int nX, int nY, float fRadius, unsigned int color)
{
	int radiusSquared = (int)(fRadius * fRadius + (float).5);
	int x = 0;
	int y = (int)fRadius;
	while(y >= x)
	{
		// Draw the eight symmetric pixels
		line(nX + x, nY - y, nX + x, nY + y, color);
		line(nX - x, nY - y, nX - x, nY + y, color);
		line(nX + y, nY - x, nX - y, nY - x, color);
		line(nX + y, nY + x, nX - y, nY + x, color);

		// Compute the next position
		if((x * x) + (y * y) > radiusSquared)
			y--;
		x++;
	}
}

void GImage::ellipse(int nX, int nY, double dRadius, double dHeightToWidthRatio, unsigned int color)
{
	double dAngle;
	double dStep = .9 / dRadius;
	if(dHeightToWidthRatio > 1)
		dStep /= dHeightToWidthRatio;
	if(dStep == 0)
		return;
	double dSin;
	double dCos;
	for(dAngle = 0; dAngle < 0.79; dAngle += dStep)
	{
		dSin = sin(dAngle);
		dCos = cos(dAngle);
		setPixelIfInRange((int)(nX + dCos * dRadius), (int)(nY + dHeightToWidthRatio * dSin * dRadius), color);
		setPixelIfInRange((int)(nX - dCos * dRadius), (int)(nY + dHeightToWidthRatio * dSin * dRadius), color);
		setPixelIfInRange((int)(nX + dCos * dRadius), (int)(nY - dHeightToWidthRatio * dSin * dRadius), color);
		setPixelIfInRange((int)(nX - dCos * dRadius), (int)(nY - dHeightToWidthRatio * dSin * dRadius), color);
		setPixelIfInRange((int)(nX + dSin * dRadius), (int)(nY + dHeightToWidthRatio * dCos * dRadius), color);
		setPixelIfInRange((int)(nX - dSin * dRadius), (int)(nY + dHeightToWidthRatio * dCos * dRadius), color);
		setPixelIfInRange((int)(nX + dSin * dRadius), (int)(nY - dHeightToWidthRatio * dCos * dRadius), color);
		setPixelIfInRange((int)(nX - dSin * dRadius), (int)(nY - dHeightToWidthRatio * dCos * dRadius), color);
	}
}

void GImage::rotate(GImage* pSourceImage, int nX, int nY, double dAngle)
{
	setSize(pSourceImage->width(), pSourceImage->height());
	int x;
	int y;
	float dCos = (float)cos(dAngle);
	float dSin = (float)sin(dAngle);
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			float dX = (x - nX) * dCos - (y - nY) * dSin + nX;
			float dY = (x - nX) * dSin + (y - nY) * dCos + nY;
			unsigned int col = pSourceImage->interpolatePixel(dX, dY);
			setPixel(x, y, col);
		}
	}
}

class GPNGReader
{
public:
	png_structp m_pReadStruct;
	png_infop m_pInfoStruct;
	png_infop m_pEndInfoStruct;
	const unsigned char* m_pData;
	int m_nPos;

	GPNGReader(const unsigned char* pData)
	{
		m_pData = pData;
		m_nPos = 0;
		m_pReadStruct = png_create_read_struct(PNG_LIBPNG_VER_STRING, NULL, NULL, NULL);
		if(!m_pReadStruct)
		{
			m_pReadStruct = NULL;
			GAssert(false); // Failed to create the read struct
			return;	
		}
		m_pInfoStruct = png_create_info_struct(m_pReadStruct);
		m_pEndInfoStruct = png_create_info_struct(m_pReadStruct);
	}

	~GPNGReader()
	{
		if(m_pReadStruct)
			png_destroy_read_struct(&m_pReadStruct, &m_pInfoStruct, &m_pEndInfoStruct);
	}

	void ReadBytes(unsigned char* pBuf, int nBytes)
	{
		memcpy(pBuf, m_pData + m_nPos, nBytes);
		m_nPos += nBytes;
	}
};

void readFunc(png_struct* pReadStruct, png_bytep pBuf, png_size_t nSize)
{
	GPNGReader* pReader = (GPNGReader*)png_get_io_ptr(pReadStruct);
	pReader->ReadBytes((unsigned char*)pBuf, (int)nSize);
}

void LoadPng(GImage* pImage, const unsigned char* pData, size_t nDataSize)
{
	// Check for the PNG signature
	if(nDataSize < 8 || png_sig_cmp((png_bytep)pData, 0, 8) != 0)
		ThrowError("not a png file");

	// Read all PNG data up until the image data chunk.
	GPNGReader reader(pData);
	png_set_read_fn(reader.m_pReadStruct, (png_voidp)&reader, (png_rw_ptr)readFunc);
	png_read_info(reader.m_pReadStruct, reader.m_pInfoStruct);

	// Get the image data
	int depth, color;
	png_uint_32 width, height;
	png_get_IHDR(reader.m_pReadStruct, reader.m_pInfoStruct, &width, &height, &depth, &color, NULL, NULL, NULL);
	GAssert(depth == 8); // unexpected depth
	pImage->setSize(width, height);

	// Set gamma correction
	double dGamma;
	if (png_get_gAMA(reader.m_pReadStruct, reader.m_pInfoStruct, &dGamma))
		png_set_gamma(reader.m_pReadStruct, 2.2, dGamma);
	else
		png_set_gamma(reader.m_pReadStruct, 2.2, 1.0 / 2.2); // 1.0 = viewing gamma, 2.2 = screen gamma

	// Update the 'info' struct with the gamma information
	png_read_update_info(reader.m_pReadStruct, reader.m_pInfoStruct);

	// Tell it to expand palettes to full channels
	png_set_expand(reader.m_pReadStruct);
	png_set_gray_to_rgb(reader.m_pReadStruct);

	// Allocate the row pointers
	unsigned long rowbytes = png_get_rowbytes(reader.m_pReadStruct, reader.m_pInfoStruct);
	unsigned long channels = rowbytes / width;
	ArrayHolder<unsigned char> hData(new unsigned char[rowbytes * height]);
	png_bytep pRawData = (png_bytep)hData.get();
	unsigned int i;
	{
		ArrayHolder<unsigned char> hRows(new unsigned char[sizeof(png_bytep) * height]);
		png_bytep* pRows = (png_bytep*)hRows.get();
		for(i = 0; i < height; i++)
			pRows[i] = pRawData + i * rowbytes;
		png_read_image(reader.m_pReadStruct, pRows);
	}

	// Copy to the GImage
	unsigned long nPixels = width * height;
	unsigned int* pRGBQuads = pImage->pixels();
	unsigned char *pBytes = pRawData;
	if(channels > 3)
	{
		GAssert(channels == 4); // unexpected number of channels
		for(i = 0; i < nPixels; i++)
		{
			*pRGBQuads = gARGB(pBytes[3], pBytes[0], pBytes[1], pBytes[2]);
			pBytes += channels;
			pRGBQuads++;
		}
	}
	else if(channels == 3)
	{
		for(i = 0; i < nPixels; i++)
		{
			*pRGBQuads = gARGB(0xff, pBytes[0], pBytes[1], pBytes[2]);
			pBytes += channels;
			pRGBQuads++;
		}
	}
	else
	{
		ThrowError("Sorry, loading one-channel pngs not implemented yet");
/*		GAssert(channels == 1); // unexpected number of channels
		for(i = 0; i < nPixels; i++)
		{
			*pRGBQuads = gARGB(0xff, pBytes[0], pBytes[0], pBytes[0]);
			pBytes += channels;
			pRGBQuads++;
		}*/
	}

	// Check for additional tags
	png_read_end(reader.m_pReadStruct, reader.m_pEndInfoStruct);
}

// -----------------------------------------------------------------------

class GPNGWriter
{
public:
	png_structp m_pWriteStruct;
	png_infop m_pInfoStruct;

	GPNGWriter()
	{
		m_pWriteStruct = png_create_write_struct(PNG_LIBPNG_VER_STRING, NULL, error_handler, NULL);
		if(!m_pWriteStruct)
			ThrowError("Failed to create write struct. Out of mem?");
		m_pInfoStruct = png_create_info_struct(m_pWriteStruct);
		if(!m_pInfoStruct)
			ThrowError("Failed to create info struct. Out of mem?");
	}

	~GPNGWriter()
	{
		png_destroy_write_struct(&m_pWriteStruct, &m_pInfoStruct);
	}

	static void error_handler(png_structp png_ptr, png_const_charp msg)
	{
		ThrowError("Error writing PNG file: ", msg);
	}
};


void SavePng(GImage* pImage, FILE* pFile, bool bIncludeAlphaChannel)
{
	// Set the jump value (This has something to do with enabling the error handler)
	GPNGWriter writer;
	if(setjmp(png_jmpbuf(writer.m_pWriteStruct)))
		ThrowError("Failed to set the jump value");

	// Init the IO
	png_init_io(writer.m_pWriteStruct, pFile);
	png_set_compression_level(writer.m_pWriteStruct, Z_BEST_COMPRESSION);

	// Write image stats and settings
	unsigned long width = pImage->width();
	unsigned long height = pImage->height();
	png_set_IHDR(writer.m_pWriteStruct, writer.m_pInfoStruct,
		width, height, 8,
		bIncludeAlphaChannel ? PNG_COLOR_TYPE_RGB_ALPHA : PNG_COLOR_TYPE_RGB,
		PNG_INTERLACE_NONE,	PNG_COMPRESSION_TYPE_DEFAULT, PNG_FILTER_TYPE_DEFAULT);
	png_write_info(writer.m_pWriteStruct, writer.m_pInfoStruct);
	png_set_packing(writer.m_pWriteStruct);

	// Write the image data
	unsigned long channels = bIncludeAlphaChannel ? 4 : 3;
	unsigned long rowbytes = width * channels;
	unsigned char* pRow = new unsigned char[rowbytes];
	ArrayHolder<unsigned char> hRow(pRow);
	unsigned int* pPix = pImage->pixels();
	if(channels == 4)
	{
		for(unsigned int i = 0; i < height; i++)
		{
			unsigned char* pBytes = pRow;
			for(unsigned int j = 0; j < width; j++)
			{
				*(pBytes++) = gRed(*pPix);
				*(pBytes++) = gGreen(*pPix);
				*(pBytes++) = gBlue(*pPix);
				*(pBytes++) = gAlpha(*pPix);
				pPix++;
			}
			png_write_row(writer.m_pWriteStruct, pRow);
		}
	}
	else if(channels == 3)
	{
		for(unsigned int i = 0; i < height; i++)
		{
			unsigned char* pBytes = pRow;
			for(unsigned int j = 0; j < width; j++)
			{
				*(pBytes++) = gRed(*pPix);
				*(pBytes++) = gGreen(*pPix);
				*(pBytes++) = gBlue(*pPix);
			}
			png_write_row(writer.m_pWriteStruct, pRow);
		}
	}
	else
		ThrowError("Unsupported number of channels");
	png_write_end(writer.m_pWriteStruct, writer.m_pInfoStruct);
}

void GImage::loadPng(const unsigned char* pRawData, size_t nBytes)
{
	LoadPng(this, pRawData, nBytes);
}

void GImage::loadPng(const char* szFilename)
{
	size_t nSize;
	char* pRawData = GFile::loadFile(szFilename, &nSize);
	ArrayHolder<char> hRawData(pRawData);
	LoadPng(this, (const unsigned char*)pRawData, nSize);
}

void GImage::loadPngFromHex(const char* szHex)
{
	size_t len = strlen(szHex);
	unsigned char* pBuf = new unsigned char[len / 2];
	ArrayHolder<unsigned char> hBuf(pBuf);
	GBits::hexToBuffer(szHex, len, pBuf);
	LoadPng(this, pBuf, len / 2);
}

void GImage::savePng(FILE* pFile)
{
	SavePng(this, pFile, true);
}

void GImage::savePng(const char* szFilename)
{
	FILE* pFile = fopen(szFilename, "wb");
	if(!pFile)
		ThrowError("Failed to create file: ", szFilename);
	FileHolder hFile(pFile);
	savePng(pFile);
}

void GImage::saveBmp(const char* szFilename)
{
	unsigned int nSize = m_width * m_height;

	BITMAPFILEHEADER h1;
	h1.bfType = GBits::n16ToLittleEndian((unsigned short)19778); // "BM"
	h1.bfSize = GBits::n32ToLittleEndian((unsigned int)(sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER) + 3 * nSize));
	h1.bfReserved1 = 0;
	h1.bfReserved2 = 0;
	h1.bfOffBits = GBits::n32ToLittleEndian((unsigned int)(sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER)));

	BITMAPINFOHEADER h2;
	h2.biSize = GBits::n32ToLittleEndian((unsigned int)sizeof(BITMAPINFOHEADER));
	h2.biWidth = GBits::n32ToLittleEndian((unsigned int)m_width);
	h2.biHeight = GBits::n32ToLittleEndian((unsigned int)m_height);
	h2.biPlanes = GBits::n16ToLittleEndian((unsigned short)1);
	h2.biBitCount = GBits::n16ToLittleEndian((unsigned short)24);
	h2.biCompression = 0;
	h2.biSizeImage = GBits::n32ToLittleEndian(3 * nSize);
	h2.biXPelsPerMeter = GBits::n32ToLittleEndian((unsigned int)3780);
	h2.biYPelsPerMeter = GBits::n32ToLittleEndian((unsigned int)3780);
	h2.biClrUsed = 0;
	h2.biClrImportant = 0;

	FILE* pFile = fopen(szFilename, "wb");
	if(!pFile)
		ThrowError("Error creating file: ", szFilename);
	FileHolder hFile(pFile);

	if(fwrite(&h1, sizeof(BITMAPFILEHEADER), 1, pFile) != 1)
		ThrowError("Error writing to file: ", szFilename);
	if(fwrite(&h2, sizeof(BITMAPINFOHEADER), 1, pFile) != 1)
		ThrowError("Error writing to file: ", szFilename);
	int y;
	int x;
	unsigned int col;
	for(y = (int)m_height - 1; y >= 0; y--)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			col = pixel(x, y);
			int nR = gRed(col);
			int nG = gGreen(col);
			int nB = gBlue(col);
			if(
				fwrite(&nB, 1, 1, pFile) != 1 ||
				fwrite(&nG, 1, 1, pFile) != 1 ||
				fwrite(&nR, 1, 1, pFile) != 1
				)
				ThrowError("Error writing to file: ", szFilename);
		}
		int n = (x * 3) % 4;
		if(n > 0)
		{
			int nR = 0;
			while(n < 4) // Allign on word boundaries
			{
				if(fwrite(&nR, 1, 1, pFile) != 1)
					ThrowError("error writing to file: ", szFilename);
				n++;
			}
		}
	}
}

void GImage::loadBmp(const char* szFilename)
{
	FILE* pFile = fopen(szFilename, "rb");
	FileHolder hFile(pFile);
	if(!pFile)
		ThrowError("Error opening file: ", szFilename);
	loadBmp(pFile);
}

void GImage::loadBmp(FILE* pFile)
{
	BITMAPFILEHEADER h1;
	BITMAPINFOHEADER h2;
	if(fread(&h1, sizeof(BITMAPFILEHEADER), 1, pFile) != 1)
		ThrowError("Unrecognized bitmap file format");
	if(fread(&h2, sizeof(BITMAPINFOHEADER), 1, pFile) != 1)
		ThrowError("Unrecognized bitmap file format");
	if(h2.biBitCount != 24)
		ThrowError("Sorry, only 24-bit bitmaps are currently supported");
	if(h2.biWidth < 1 || h2.biHeight < 1)
		ThrowError("Sorry, only bottom-up bitmaps are currently supported");
	setSize(h2.biWidth, h2.biHeight);

	int y;
	int x;
	unsigned char nR;
	unsigned char nG;
	unsigned char nB;
	for(y = (int)m_height - 1; y >= 0; y--)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			if(
				fread(&nB, 1, 1, pFile) != 1 ||
				fread(&nG, 1, 1, pFile) != 1 ||
				fread(&nR, 1, 1, pFile) != 1
				)
				ThrowError("Error reading from file");
			setPixel(x, y, gRGB(nR, nG, nB));
		}
		int n = (x * 3) % 4;
		if(n > 0)
		{
			nR = 0;
			while(n < 4) // Align on word boundaries
			{
				if(fread(&nR, 1, 1, pFile) != 1)
					ThrowError("Error reading from file");
				n++;
			}
		}
	}
}

void GImage::loadBmp(const unsigned char* pRawData, int nLen)
{
	if(nLen < (int)(sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER)))
		ThrowError("Unrecognized bitmap format");
	BITMAPFILEHEADER* h1 = (BITMAPFILEHEADER*)pRawData;
	BITMAPINFOHEADER* h2 = (BITMAPINFOHEADER*)((char*)h1 + sizeof(BITMAPFILEHEADER));
	const unsigned char* pData = (const unsigned char*)((char*)h2 + sizeof(BITMAPINFOHEADER));
	if(h2->biBitCount != 24)
		ThrowError("Sorry, only 24-bit bitmaps are currently supported");
	if(h2->biWidth < 1 || h2->biHeight < 1)
		ThrowError("Sorry, only bottom-up bitmaps are currently supported");
	setSize(h2->biWidth, h2->biHeight);
	// todo: make sure nLen is big enough to hold all the data
	int y;
	int x;
	unsigned char nR;
	unsigned char nG;
	unsigned char nB;
	for(y = (int)m_height - 1; y >= 0; y--)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			nB = *(pData++);
			nG = *(pData++);
			nR = *(pData++);
			setPixel(x, y, gRGB(nR, nG, nB));
		}
		int n = (x * 3) % 4;
		if(n > 0)
		{
			while(n < 4) // Align on word boundaries
			{
				pData++;
				n++;
			}
		}
	}
}

void GImage::crop(int left, int top, int width, int height)
{
	if(width < 1 || height < 1)
	{
		setSize(width, height);
		return;
	}
	GImage tmp;
	tmp.setSize(width, height);

	int l = std::max(0, left);
	int t = std::max(0, top);
	int ll = std::max(-left, 0);
	int tt = std::max(-top, 0);
	int w = std::min((int)width - ll, std::min((int)m_width - l, (int)width));
	int h = std::min((int)height - tt, std::min((int)m_height - t, (int)height));
	GRect r(l, t, w, h);
	tmp.blit(ll, tt, this, &r);
	swapData(&tmp);
}

void GImage::box(int nX1, int nY1, int nX2, int nY2, unsigned int color)
{
	int tmp;
	if(nX1 > nX2)
	{
		tmp = nX2;
		nX2 = nX1;
		nX1 = tmp;
	}
	if(nY1 > nY2)
	{
		tmp = nY2;
		nY2 = nY1;
		nY1 = tmp;
	}
	int n;
	for(n = nX1; n <= nX2; n++)
	{
		setPixel(n, nY1, color);
		setPixel(n, nY2, color);
	}
	for(n = nY1 + 1; n < nY2; n++)
	{
		setPixel(nX1, n, color);
		setPixel(nX2, n, color);
	}
}

void GImage::boxFill(int x, int y, int w, int h, unsigned int c)
{
	x = std::max(0, x);
	y = std::max(0, y);
	w = std::min((int)m_width - x, w);
	h = std::min((int)m_height - y, h);
	unsigned int* pPixels;
	int xx, yy;
	for(yy = 0; yy < h; yy++)
	{
		pPixels = pixelRef(x, y + yy);
		for(xx = 0; xx < w; xx++)
			*(pPixels++) = c;
	}
}

void GImage::scale(unsigned int nNewWidth, unsigned int nNewHeight)
{
	unsigned int nOldWidth = m_width;
	unsigned int nOldHeight = m_height;
	GImage tmpImage;
	tmpImage.swapData(this);
	setSize(nNewWidth, nNewHeight);
	unsigned int col;
	int x, y;
	for(y = 0; y < (int)nNewHeight; y++)
	{
		for(x = 0; x < (int)nNewWidth; x++)
		{
			col = tmpImage.interpolatePixel((float)(x * nOldWidth) / nNewWidth, (float)(y * nOldHeight) / nNewHeight);
			setPixel(x, y, col);
		}
	}
}

void GImage::flipHorizontally()
{
	unsigned int c1;
	unsigned int c2;
	int x, y;
	int nHalfWidth = (int)m_width >> 1;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < nHalfWidth; x++)
		{
			c1 = pixel(x, y);
			c2 = pixel((int)m_width - 1 - x, y);
			setPixel(x, y, c2);
			setPixel((int)m_width - 1 - x, y, c1);
		}
	}
}

void GImage::flipVertically()
{
	unsigned int c1;
	unsigned int c2;
	int x, y;
	int nHalfHeight = (int)m_height >> 1;
	for(x = 0; x < (int)m_width; x++)
	{
		for(y = 0; y < nHalfHeight; y++)
		{
			c1 = pixel(x, y);
			c2 = pixel(x, m_height - 1 - y);
			setPixel(x, y, c2);
			setPixel(x, m_height - 1 - y, c1);
		}
	}
}

void GImage::swapData(GImage* pSwapImage)
{
	unsigned int* pTmpRGBQuads = m_pPixels;
	unsigned int nTmpWidth = m_width;
	unsigned int nTmpHeight = m_height;
	m_pPixels = pSwapImage->m_pPixels;
	m_width = pSwapImage->m_width;
	m_height = pSwapImage->m_height;
	pSwapImage->m_pPixels = pTmpRGBQuads;
	pSwapImage->m_width = nTmpWidth;
	pSwapImage->m_height = nTmpHeight;
}

void GImage::convolve(GImage* pKernel)
{
	GImage NewImage;
	NewImage.setSize(m_width, m_height);
	unsigned int c1;
	unsigned int c2;
	int nHalfKWidth = (int)pKernel->m_width >> 1;
	int nHalfKHeight = (int)pKernel->m_height >> 1;
	int nRSum, nGSum, nBSum;
	int nRTot, nGTot, nBTot;
	nRTot = 0;
	nGTot = 0;
	nBTot = 0;
	int x, y, kx, ky;
	for(ky = 0; ky < (int)pKernel->m_height; ky++)
	{
		for(kx = 0; kx < (int)pKernel->m_width; kx++)
		{
			c1 = pKernel->pixel(kx, ky);
			nRTot += gRed(c1);
			nGTot += gGreen(c1);
			nBTot += gBlue(c1);
		}
	}
	nRTot = std::max(1, nRTot);
	nGTot = std::max(1, nGTot);
	nBTot = std::max(1, nBTot);
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			nRSum = 0;
			nGSum = 0;
			nBSum = 0;
			for(ky = 0; ky < (int)pKernel->m_height; ky++)
			{
				for(kx = 0; kx < (int)pKernel->m_width; kx++)
				{
					c1 = pKernel->pixel(pKernel->m_width - kx - 1, pKernel->m_height - ky - 1);
					c2 = pixelNearest(x + kx - nHalfKWidth, y + ky - nHalfKHeight);
					nRSum += gRed(c1) * gRed(c2);
					nGSum += gGreen(c1) * gGreen(c2);
					nBSum += gBlue(c1) * gBlue(c2);
				}
			}
			NewImage.setPixel(x, y, gRGB(ClipChan(nRSum / nRTot), ClipChan(nGSum / nGTot), ClipChan(nBSum / nBTot)));
		}
	}
	swapData(&NewImage);
}

void GImage::convolveKernel(GImage* pKernel)
{
	GImage NewImage;
	NewImage.setSize(m_width, m_height);
	unsigned int c1;
	unsigned int c2;
	int nHalfKWidth = (int)pKernel->m_width >> 1;
	int nHalfKHeight = (int)pKernel->m_height >> 1;
	int nRSum, nGSum, nBSum;
	int x, y;
	int kx, ky;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			nRSum = 0;
			nGSum = 0;
			nBSum = 0;
			for(ky = 0; ky < (int)pKernel->m_height; ky++)
			{
				for(kx = 0; kx < (int)pKernel->m_width; kx++)
				{
					c1 = pKernel->pixel(pKernel->m_width - kx - 1, pKernel->m_height - ky - 1);
					c2 = pixelNearest(x + kx - nHalfKWidth, y + ky - nHalfKHeight);
					nRSum += gRed(c1) * gRed(c2);
					nGSum += gGreen(c1) * gGreen(c2);
					nBSum += gBlue(c1) * gBlue(c2);
				}
			}
			nRSum = ClipChan(nRSum);
			nGSum = ClipChan(nGSum);
			nBSum = ClipChan(nBSum);
			NewImage.setPixel(x, y, gRGB(nRSum, nGSum, nBSum));
		}
	}
	swapData(&NewImage);
}

void GImage::blur(double dRadius)
{
	// Calculate how big of a kernel we need
	int nFactor = (int)dRadius;
	int nWidth = nFactor * 2 + 1;
	GImage imgKernel;
	imgKernel.setSize(nWidth, nWidth);

	// Produce the blurring kernel
	double dTmp = dRadius / 8;
	double d;
	int n;
	int x, y;
	for(y = 0; y < nWidth; y++)
	{
		for(x = 0; x < nWidth; x++)
		{
			d = pow((double)2, -(sqrt((double)((nFactor - x) * (nFactor - x) + (nFactor - y) * (nFactor - y))) / dTmp));
			n = ClipChan((int)(d * 256));
			imgKernel.setPixel(x, y, gRGB(n, n, n));
		}
	}

	// Convolve the kernel with the image
	convolve(&imgKernel);
}

void GImage::blurQuick(int iters, int nRadius)
{
	GImage tmp;
	tmp.setSize(width(), height());
	unsigned int c;
	for(int iter = 0; iter < iters; iter++)
	{
		for(int y = 0; y < (int)m_height; y++)
		{
			for(int x = 0; x < (int)m_width; x++)
			{
				int r = 0;
				int g = 0;
				int b = 0;
				int count = 0;
				int head = std::max(0, x - nRadius);
				int tail = std::min(x + nRadius + 1, (int)m_width);
				for(int i = head; i < tail; i++)
				{
					c = pixel(i, y);
					r += gRed(c);
					g += gGreen(c);
					b += gBlue(c);
					count++;
				}
				head = std::max(0, y - nRadius);
				tail = std::min(y + nRadius + 1, (int)m_height);
				for(int i = head; i < tail; i++)
				{
					if(i != y)
					{
						c = tmp.pixel(x, i);
						r += gRed(c);
						g += gGreen(c);
						b += gBlue(c);
						count++;
					}
				}
				tmp.setPixel(x, y, gRGB(r / count, g / count, b / count));
			}
		}
		this->swapData(&tmp);
	}
}

void GImage::sharpen(double dFactor)
{
	GImage imgBlurred;
	imgBlurred.copy(this);
	imgBlurred.blur(dFactor);
	GImage imgTmp;
	imgTmp.setSize(m_width, m_height);
	unsigned int col;
	int nRed, nGreen, nBlue;
	int x, y;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			col = pixel(x, y);
			nRed = 2 * gRed(col);
			nGreen = 2 * gGreen(col);
			nBlue = 2 * gBlue(col);
			col = imgBlurred.pixel(x, y);
			nRed = ClipChan((int)(nRed - gRed(col)));
			nGreen = ClipChan((int)(nGreen - gGreen(col)));
			nBlue = ClipChan((int)(nBlue - gBlue(col)));
			imgTmp.setPixel(x, y, gRGB(nRed, nGreen, nBlue));
		}
	}
	swapData(&imgTmp);
}

void GImage::invert()
{
	int x, y;
	unsigned int col;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			col = pixel(x, y);
			setPixel(x, y, gRGB(255 - gRed(col), 255 - gGreen(col), 255 - gBlue(col)));
		}
	}
}

void GImage::invertRect(GRect* pRect)
{
	int x, y;
	unsigned int col;
	for(y = pRect->y; y < pRect->y + pRect->h; y++)
	{
		for(x = pRect->x; x < pRect->x + pRect->w; x++)
		{
			col = pixel(x, y);
			setPixel(x, y, gRGB(255 - gRed(col), 255 - gGreen(col), 255 - gBlue(col)));
		}
	}
}

void GImage::addBorder(const GImage* pSourceImage, unsigned int cBackground, unsigned int cBorder)
{
	setSize(pSourceImage->width(), pSourceImage->height());
	int x, y;
	unsigned int c;
	for(y = (int)m_height - 2; y > 0; y--)
	{
		for(x = (int)m_width - 2; x > 0; x--)
		{
			c = pSourceImage->pixel(x, y);
			if(c == cBackground)
			{
				if(
						pSourceImage->pixel(x - 1, y) != cBackground ||
						pSourceImage->pixel(x + 1, y) != cBackground ||
						pSourceImage->pixel(x, y - 1) != cBackground ||
						pSourceImage->pixel(x, y + 1) != cBackground
					)
					setPixel(x, y, cBorder);
				else
					setPixel(x, y, c);
			}
			else
				setPixel(x, y, c);
		}
	}
	for(y = m_height - 1; y >= 0; y--)
	{
		setPixel(0, y, pSourceImage->pixel(0, y));
		setPixel(m_width - 1, y, pSourceImage->pixel(m_width - 1, y));
	}
	for(x = m_width - 2; x > 0; x--)
	{
		setPixel(x, 0, pSourceImage->pixel(x, 0));
		setPixel(x, m_height - 1, pSourceImage->pixel(x, m_height - 1));
	}
}

void GImage::makeEdgesGlow(float fThresh, int nThickness, int nOpacity, unsigned int color)
{
	// Make initial mask
	GImage tmp;
	tmp.setSize(m_width, m_height);
	unsigned int col, colLeft, colRight, colTop, colBottom;
	int x, y, dif;
	for(y = (int)m_height - 2; y > 0; y--)
	{
		for(x = (int)m_width - 2; x > 0; x--)
		{
			col = pixel(x, y);
			colLeft = pixel(x - 1, y);
			colRight = pixel(x + 1, y);
			colTop = pixel(x, y - 1);
			colBottom = pixel(x, y + 1);

			dif = std::abs((int)gRed(col) - (int)gRed(colLeft)) + std::abs((int)gGreen(col) - (int)gGreen(colLeft)) + std::abs((int)gBlue(col) - (int)gBlue(colLeft)) +
				std::abs((int)gRed(col) - (int)gRed(colRight)) + std::abs((int)gGreen(col) - (int)gGreen(colRight)) + std::abs((int)gBlue(col) - (int)gBlue(colRight)) +
				std::abs((int)gRed(col) - (int)gRed(colTop)) + std::abs((int)gGreen(col) - (int)gGreen(colTop)) + std::abs((int)gBlue(col) - (int)gBlue(colTop)) +
				std::abs((int)gRed(col) - (int)gRed(colBottom)) + std::abs((int)gGreen(col) - (int)gGreen(colBottom)) + std::abs((int)gBlue(col) - (int)gBlue(colBottom));
			if((float)dif * gAlpha(col) > fThresh * 49152)
				tmp.setPixel(x, y, nThickness + 1);

/*
			dif = std::abs((int)gAlpha(col) - (int)gAlpha(colLeft)) +
				std::abs((int)gAlpha(col) - (int)gAlpha(colRight)) +
				std::abs((int)gAlpha(col) - (int)gAlpha(colTop)) +
				std::abs((int)gAlpha(col) - (int)gAlpha(colBottom));
			if((float)dif / 1024 > fThresh)
				tmp.setPixel(x, y, nThickness + 1);
*/
		}
	}

	// Make the glowing
	int n;
	for(n = nThickness; n >= 0; n--)
	{
		for(y = (int)m_height - 2; y > 0; y--)
		{
			for(x = (int)m_width - 2; x > 0; x--)
			{
				col = tmp.pixel(x, y);
				if(col > (unsigned int)n)
				{
					tmp.setPixel(x - 1, y, tmp.pixel(x - 1, y) | n);
					tmp.setPixel(x + 1, y, tmp.pixel(x + 1, y) | n);
					tmp.setPixel(x, y - 1, tmp.pixel(x, y - 1) | n);
					tmp.setPixel(x, y + 1, tmp.pixel(x, y + 1) | n);
					col = MixColors(color, pixel(x, y), nOpacity);
					setPixel(x, y, col);
				}
			}
		}
	}
}

void GImage::horizDifferenceize()
{
	if(m_width < 2)
		return;
	unsigned int c1, c2;
	int x, y;
	for(y = 0; y < (int)m_height; y++)
	{
		c1 = pixel(0, y);
		for(x = 1; x < (int)m_width; x++)
		{
			c2 = pixel(x, y);
			setPixel(x, y, gRGB(
				(256 + gRed(c2) - gRed(c1)) % 256,
				(256 + gGreen(c2) - gGreen(c1)) % 256,
				(256 + gBlue(c2) - gBlue(c1)) % 256));
			c1 = c2;
		}
	}
}

void GImage::horizSummize()
{
	if(m_width < 2)
		return;
	unsigned int c1, c2;
	int x, y;
	for(y = 0; y < (int)m_height; y++)
	{
		c1 = pixel(0, y);
		for(x = 1; x < (int)m_width; x++)
		{
			c2 = pixel(x, y);
			setPixel(x, y, gRGB(
				(256 + gRed(c2) + gRed(c1)) % 256,
				(256 + gGreen(c2) + gGreen(c1)) % 256,
				(256 + gBlue(c2) + gBlue(c1)) % 256));
			c1 = c2;
		}
	}
}

void GImage::dot(float x, float y, float radius, unsigned int fore, unsigned int back)
{
	int a = (int)floor(x + 0.5);
	if((unsigned int)a == 0x80000000)
		return;
	int b = (int)floor(y + 0.5);
	if((unsigned int)b == 0x80000000)
		return;
	float r = radius * radius;

	// From center of dot to top
	for(int t = b; t >= 0; t--)
	{
		float d = (t - y) * (t - y);
		if(d >= r)
			break;

		// From center to left
		for(int s = a; s >= 0; s--)
		{
			float c = (s - x) * (s - x);
			if(d + c >= r)
				break;
			setPixelIfInRange(s, t, MixColors(back, fore, (int)((d + c) * 256 / r)));
		}

		// From center to right
		for(int s = a + 1; s < (int)m_width; s++)
		{
			float c = (s - x) * (s - x);
			if(d + c >= r)
				break;
			setPixelIfInRange(s, t, MixColors(back, fore, (int)((d + c) * 256 / r)));
		}
	}

	// From center of dot to bottom
	for(int t = b + 1; t < (int)m_height; t++)
	{
		float d = (t - y) * (t - y);
		if(d >= r)
			break;

		// From center to left
		for(int s = a; s >= 0; s--)
		{
			float c = (s - x) * (s - x);
			if(d + c >= r)
				break;
			setPixelIfInRange(s, t, MixColors(back, fore, (int)((d + c) * 256 / r)));
		}

		// From center to right
		for(int s = a + 1; s < (int)m_width; s++)
		{
			float c = (s - x) * (s - x);
			if(d + c >= r)
				break;
			setPixelIfInRange(s, t, MixColors(back, fore, (int)((d + c) * 256 / r)));
		}
	}
}


static const unsigned short g_fontPixelMap[] =
{
0,0,0,
0,382,0,
0,7,0,7,0,
0,72,510,72,510,72,0,
0,268,274,1023,290,194,0,
0,12,18,274,204,32,16,204,290,288,192,
0,230,281,273,273,169,70,176,256,
0,0,7,0,
0,240,780,1026,2049,
0,2049,1026,780,240,0,
0,20,8,62,8,20,0,
0,32,32,32,508,32,32,32,
0,2304,1792,
0,32,32,32,32,32,0,
0,384,0,
0,1024,896,96,28,2,
252,258,258,258,258,252,0,
0,260,260,510,256,256,
0,388,322,290,290,274,268,
0,132,258,274,274,274,236,
0,96,80,72,68,510,64,
0,158,274,274,274,274,226,
0,248,276,274,274,274,224,
0,2,258,194,34,26,6,
0,236,274,274,274,274,236,
0,28,290,290,290,162,124,0,
0,0,408,0,
0,0,1152,920,0,0,
0,32,80,136,260,514,0,
0,136,136,136,136,136,136,0,
0,514,260,136,80,32,0,0,0,
0,4,834,34,18,12,0,
240,780,516,1266,1290,1290,1162,1530,260,268,240,
0,384,96,88,70,88,96,384,
0,510,274,274,274,274,236,
0,120,132,258,258,258,258,132,
0,510,258,258,258,258,132,120,
0,510,274,274,274,274,258,
0,510,18,18,18,18,2,
0,120,132,258,258,290,290,484,
0,510,16,16,16,16,16,510,
0,258,510,258,
0,256,258,258,254,
0,510,16,40,68,130,256,
0,510,256,256,256,256,
0,510,2,12,48,192,48,12,2,510,
0,510,6,8,16,96,384,510,
0,120,132,258,258,258,258,132,120,
0,510,34,34,34,34,28,
0,120,132,258,258,770,1282,1156,1144,
0,510,18,18,50,82,140,256,
0,140,274,274,274,274,274,228,
0,2,2,2,510,2,2,2,
0,126,128,256,256,256,128,126,
0,6,24,96,384,96,24,6,
0,14,240,256,192,48,12,48,192,256,240,14,
0,258,132,72,48,72,132,258,
0,2,12,16,480,16,12,2,
0,386,322,290,274,266,262,
0,2047,1025,1025,
0,3,12,112,384,1536,
0,1025,1025,2047,0,
0,16,8,4,2,4,8,16,
0,1024,1024,1024,1024,1024,1024,0,0,
0,0,3,2,0,
0,192,296,296,296,168,496,
0,511,272,264,264,264,240,
0,240,264,264,264,264,
0,240,264,264,264,136,511,
0,240,296,296,296,296,176,
0,8,510,9,9,
0,240,2312,2312,2312,2184,2040,
0,511,16,8,8,8,496,
0,506,0,
2048,2056,2042,
0,511,32,80,136,260,0,
0,511,0,
0,504,16,8,8,496,16,8,8,496,
0,504,16,8,8,8,496,
0,240,264,264,264,264,240,
0,4088,272,264,264,264,240,
0,240,264,264,264,136,4088,
0,504,16,8,8,
0,144,296,296,328,208,
0,8,254,264,264,
0,248,256,256,128,504,
0,24,96,384,96,24,
0,24,96,384,96,24,96,384,96,24,
0,264,144,96,144,264,
0,24,2144,1920,96,24,
0,392,328,296,280,264,
0,32,32,990,1025,1025,0,
0,0,2047,0,
0,0,1025,1025,990,32,32,0,
0,32,16,16,32,64,64,32,0,
0,504,504,504,504,
};

static const unsigned char g_fontConnectMap[] =
{
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,4,0,0,5,0,0,5,0,0,5,0,0,5,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,4,0,4,0,0,5,0,5,0,0,1,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,4,0,4,0,0,0,0,5,0,5,0,0,0,2,15,10,15,8,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,2,15,10,15,8,0,0,0,5,0,5,0,0,0,0,1,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,32,68,0,0,0,0,32,82,143,10,8,0,0,20,128,5,0,0,0,0,33,64,5,0,0,0,0,16,130,13,0,0,0,0,0,0,7,40,64,0,0,0,0,5,16,132,0,0,0,0,5,32,65,0,0,2,10,47,88,128,0,0,0,0,17,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,32,66,40,64,0,0,32,64,0,0,0,20,128,16,132,0,0,20,128,0,0,0,33,64,32,65,0,32,65,0,0,0,0,16,130,24,128,32,80,128,0,0,0,0,0,0,0,32,80,128,32,66,40,64,0,0,0,0,20,128,0,20,128,16,132,0,0,0,32,65,0,0,33,64,32,65,0,0,0,16,128,0,0,16,130,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,32,66,10,10,40,64,0,0,0,20,128,0,0,16,132,0,0,0,33,64,0,0,32,65,0,0,0,16,132,0,32,80,128,0,0,0,32,67,10,56,192,0,4,0,0,20,128,0,16,160,96,65,0,0,5,0,0,0,48,240,192,0,0,33,64,0,32,80,144,160,64,0,16,130,10,24,128,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,4,0,0,0,5,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,32,64,0,0,32,80,128,0,0,20,128,0,0,32,65,0,0,0,20,128,0,0,0,5,0,0,0,0,5,0,0,0,0,33,64,0,0,0,16,132,0,0,0,0,33,64,0,0,0,16,160,64,0,0,0,16,128,
0,32,64,0,0,0,0,16,160,64,0,0,0,0,16,132,0,0,0,0,0,33,64,0,0,0,0,16,132,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,32,65,0,0,0,0,20,128,0,0,0,32,65,0,0,0,32,80,128,0,0,0,16,128,0,0,0,
0,0,0,0,0,0,0,0,0,0,4,0,0,0,0,32,64,5,32,64,0,0,48,194,15,56,192,0,0,16,128,5,16,128,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,2,10,10,15,10,10,8,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,12,0,0,5,0,32,65,0,16,128,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,1,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,32,64,0,0,0,0,20,128,0,0,0,0,5,0,0,0,0,32,65,0,0,0,0,20,128,0,0,0,32,65,0,0,0,0,20,128,0,0,0,0,5,0,0,0,0,32,65,0,0,0,0,16,128,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,32,66,10,10,40,64,0,20,128,0,0,16,132,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,33,64,0,0,32,65,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,4,0,0,0,2,10,13,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,2,10,11,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,16,128,0,0,16,132,0,0,0,0,0,32,65,0,0,0,0,32,80,128,0,0,32,66,24,128,0,0,32,80,128,0,0,0,0,20,128,0,0,0,0,0,3,10,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,16,128,0,0,16,132,0,0,0,0,0,32,65,0,0,0,2,10,56,192,0,0,0,0,0,16,132,0,0,0,0,0,0,5,0,32,64,0,0,32,65,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,32,68,0,0,0,0,32,82,141,0,0,0,32,80,128,5,0,0,32,80,128,0,5,0,0,20,128,0,0,5,0,0,3,10,10,10,15,8,0,0,0,0,0,5,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,6,10,10,10,10,8,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,3,10,10,10,40,64,0,0,0,0,0,16,132,0,0,0,0,0,0,5,0,32,64,0,0,32,65,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,32,66,10,8,0,0,32,80,128,0,0,0,0,20,128,0,0,0,0,0,7,10,10,10,40,64,0,5,0,0,0,16,132,0,5,0,0,0,0,5,0,33,64,0,0,32,65,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,2,10,10,10,10,12,0,0,0,0,0,32,65,0,0,0,0,0,20,128,0,0,0,0,32,65,0,0,0,0,32,80,128,0,0,0,0,20,128,0,0,0,0,32,65,0,0,0,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,20,128,0,0,16,132,0,33,64,0,0,32,65,0,48,194,10,10,56,192,0,20,128,0,0,16,132,0,5,0,0,0,0,5,0,33,64,0,0,32,65,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,0,20,128,0,0,16,132,0,0,5,0,0,0,0,5,0,0,33,64,0,0,0,5,0,0,16,130,10,10,10,13,0,0,0,0,0,0,32,65,0,0,0,0,0,32,80,128,0,0,0,2,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,12,0,0,0,0,0,5,0,0,0,0,32,65,0,0,0,0,16,128,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,32,64,0,0,0,0,32,80,128,0,0,0,32,80,128,0,0,0,32,80,128,0,0,0,0,48,192,0,0,0,0,0,16,160,64,0,0,0,0,0,16,160,64,0,0,0,0,0,16,160,64,0,0,0,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,10,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,10,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,32,64,0,0,0,0,0,0,0,16,160,64,0,0,0,0,0,0,0,16,160,64,0,0,0,0,0,0,0,16,160,64,0,0,0,0,0,0,0,48,192,0,0,0,0,0,0,32,80,128,0,0,0,0,0,32,80,128,0,0,0,0,0,32,80,128,0,0,0,0,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,32,66,10,40,64,0,0,16,128,0,16,132,0,0,0,0,0,32,65,0,0,0,0,32,80,128,0,0,0,32,80,128,0,0,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,32,66,10,10,10,40,64,0,0,0,38,88,128,0,0,0,48,226,76,0,32,81,128,32,66,10,10,28,144,161,64,20,128,0,20,128,0,0,5,0,16,132,5,0,0,5,0,0,0,5,0,0,5,5,0,0,5,0,0,0,5,0,0,5,33,64,0,33,64,32,98,77,0,32,65,16,164,64,16,130,24,144,131,10,24,128,0,19,168,64,0,0,0,0,0,0,0,0,0,16,130,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,0,32,97,64,0,0,0,0,0,20,144,132,0,0,0,0,32,65,0,33,64,0,0,0,20,128,0,16,132,0,0,32,67,10,10,10,41,64,0,20,128,0,0,0,16,132,0,1,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,6,10,10,10,40,64,0,5,0,0,0,16,132,0,5,0,0,0,32,65,0,7,10,10,10,56,192,0,5,0,0,0,16,132,0,5,0,0,0,0,5,0,5,0,0,0,32,65,0,3,10,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,32,80,128,0,0,16,128,0,20,128,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,33,64,0,0,0,0,0,0,16,160,64,0,0,32,64,0,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,6,10,10,10,40,64,0,0,5,0,0,0,16,160,64,0,5,0,0,0,0,16,132,0,5,0,0,0,0,0,5,0,5,0,0,0,0,0,5,0,5,0,0,0,0,32,65,0,5,0,0,0,32,80,128,0,3,10,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,6,10,10,10,10,8,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,7,10,10,10,8,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,3,10,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,6,10,10,10,10,8,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,7,10,10,10,8,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,32,80,128,0,0,16,128,0,20,128,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,2,10,12,0,33,64,0,0,0,0,5,0,16,160,64,0,0,0,5,0,0,16,130,10,10,10,9,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,4,0,5,0,0,0,0,0,5,0,5,0,0,0,0,0,5,0,7,10,10,10,10,10,13,0,5,0,0,0,0,0,5,0,5,0,0,0,0,0,5,0,5,0,0,0,0,0,5,0,1,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,2,14,8,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,2,11,8,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,2,10,12,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,32,65,0,2,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,4,0,0,32,64,0,0,5,0,32,80,128,0,0,5,32,80,128,0,0,0,7,56,192,0,0,0,0,5,16,160,64,0,0,0,5,0,16,160,64,0,0,5,0,0,16,160,64,0,1,0,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,4,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,3,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,6,40,64,0,0,0,32,66,12,0,5,16,132,0,0,0,20,128,5,0,5,0,33,64,0,32,65,0,5,0,5,0,16,132,0,20,128,0,5,0,5,0,0,33,96,65,0,0,5,0,5,0,0,16,148,128,0,0,5,0,5,0,0,0,1,0,0,0,5,0,1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,38,76,0,0,0,0,4,0,23,169,64,0,0,0,5,0,5,16,160,64,0,0,5,0,5,0,16,160,64,0,5,0,5,0,0,16,132,0,5,0,5,0,0,0,33,64,5,0,5,0,0,0,16,166,77,0,1,0,0,0,0,19,137,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,0,32,80,128,0,0,16,160,64,0,20,128,0,0,0,0,16,132,0,5,0,0,0,0,0,0,5,0,5,0,0,0,0,0,0,5,0,33,64,0,0,0,0,32,65,0,16,160,64,0,0,32,80,128,0,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,6,10,10,10,40,64,0,5,0,0,0,16,132,0,5,0,0,0,0,5,0,5,0,0,0,32,65,0,7,10,10,10,24,128,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,0,32,80,128,0,0,16,160,64,0,20,128,0,0,0,0,16,132,0,5,0,0,0,0,0,0,5,0,5,0,0,0,0,0,0,5,0,33,64,0,0,0,0,32,65,0,16,160,64,0,0,32,80,128,0,0,16,130,10,46,88,128,0,0,0,0,0,0,49,192,0,0,0,0,0,0,0,16,130,10,8,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,6,10,10,10,40,64,0,0,5,0,0,0,16,132,0,0,5,0,0,0,32,65,0,0,7,10,10,46,88,128,0,0,5,0,0,49,192,0,0,0,5,0,0,16,160,64,0,0,5,0,0,0,16,160,64,0,1,0,0,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,32,66,10,10,10,40,64,0,20,128,0,0,0,16,128,0,33,64,0,0,0,0,0,0,16,130,10,10,10,40,64,0,0,0,0,0,0,16,132,0,0,0,0,0,0,0,5,0,32,64,0,0,0,32,65,0,16,130,10,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,2,10,10,14,10,10,8,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,4,0,5,0,0,0,0,0,5,0,5,0,0,0,0,0,5,0,5,0,0,0,0,0,5,0,5,0,0,0,0,0,5,0,33,64,0,0,0,32,65,0,16,160,64,0,32,80,128,0,0,16,130,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,4,0,33,64,0,0,0,32,65,0,16,132,0,0,0,20,128,0,0,33,64,0,32,65,0,0,0,16,132,0,20,128,0,0,0,0,33,96,65,0,0,0,0,0,16,148,128,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,0,0,0,0,4,0,5,0,0,0,0,4,0,0,0,0,5,0,33,64,0,0,32,97,64,0,0,32,65,0,16,132,0,0,20,144,132,0,0,20,128,0,0,5,0,32,65,0,33,64,0,5,0,0,0,5,0,20,128,0,16,132,0,5,0,0,0,33,96,65,0,0,0,33,96,65,0,0,0,16,144,128,0,0,0,16,144,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,32,64,0,0,0,32,64,0,16,160,64,0,32,80,128,0,0,16,160,96,80,128,0,0,0,0,16,148,128,0,0,0,0,0,32,97,64,0,0,0,0,32,80,144,160,64,0,0,32,80,128,0,16,160,64,0,16,128,0,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,32,64,0,0,0,32,64,0,16,132,0,0,0,20,128,0,0,33,64,0,32,65,0,0,0,16,160,96,80,128,0,0,0,0,16,148,128,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,2,10,10,10,10,12,0,0,0,0,0,32,65,0,0,0,0,32,80,128,0,0,0,32,80,128,0,0,0,32,80,128,0,0,0,32,80,128,0,0,0,0,20,128,0,0,0,0,0,3,10,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,6,10,8,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,3,10,8,0,0,0,0,
0,4,0,0,0,0,0,33,64,0,0,0,0,16,132,0,0,0,0,0,33,64,0,0,0,0,16,132,0,0,0,0,0,5,0,0,0,0,0,33,64,0,0,0,0,16,132,0,0,0,0,0,33,64,0,0,0,0,16,132,0,0,0,0,0,1,0,0,0,0,0,0,
0,2,10,12,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,2,10,9,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,32,96,64,0,0,0,0,32,80,144,160,64,0,0,32,80,128,0,16,160,64,0,16,128,0,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,10,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,
0,0,36,64,0,0,0,19,136,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,10,10,40,64,0,0,0,0,0,16,132,0,32,66,10,10,10,13,0,20,128,0,0,0,5,0,33,64,0,32,98,77,0,16,130,10,24,144,129,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,4,0,0,0,0,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,5,32,66,10,40,64,0,7,24,128,0,16,132,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,32,65,0,3,10,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,32,66,10,10,8,0,20,128,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,0,33,64,0,0,0,0,16,130,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,4,0,0,0,0,0,0,5,0,0,0,0,0,0,5,0,32,66,10,10,10,13,0,20,128,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,33,64,0,32,98,77,0,16,130,10,24,144,129,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,20,128,0,0,16,132,0,7,10,10,10,10,9,0,5,0,0,0,0,0,0,33,64,0,0,32,64,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,32,66,8,0,0,20,128,0,0,0,5,0,0,0,2,15,10,8,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,32,66,10,10,10,12,0,20,128,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,33,64,0,32,66,13,0,16,130,10,24,128,5,0,0,0,0,0,0,5,0,0,0,0,0,32,65,0,0,2,10,10,24,128,
0,4,0,0,0,0,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,5,32,66,10,40,64,0,7,24,128,0,16,132,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,1,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,4,0,0,5,0,0,5,0,0,5,0,0,5,0,0,1,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,2,12,0,0,5,0,0,5,0,0,5,0,0,5,0,0,5,0,0,5,0,32,65,2,24,128,
0,4,0,0,0,0,0,0,5,0,0,0,0,0,0,5,0,0,32,64,0,0,5,0,32,80,128,0,0,5,32,80,128,0,0,0,7,56,192,0,0,0,0,5,16,160,64,0,0,0,5,0,16,160,64,0,0,1,0,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,4,0,0,5,0,0,5,0,0,5,0,0,5,0,0,5,0,0,5,0,0,5,0,0,1,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,36,96,66,40,64,32,66,40,64,0,23,152,128,16,134,24,128,16,132,0,5,0,0,0,5,0,0,0,5,0,5,0,0,0,5,0,0,0,5,0,5,0,0,0,5,0,0,0,5,0,1,0,0,0,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,36,96,66,10,40,64,0,23,152,128,0,16,132,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,1,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,32,66,10,10,40,64,0,20,128,0,0,16,132,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,33,64,0,0,32,65,0,16,130,10,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,36,96,66,10,40,64,0,23,152,128,0,16,132,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,32,65,0,7,10,10,10,24,128,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,1,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,32,66,10,10,10,12,0,20,128,0,0,0,5,0,5,0,0,0,0,5,0,5,0,0,0,0,5,0,33,64,0,32,66,13,0,16,130,10,24,128,5,0,0,0,0,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,1,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,36,96,66,8,0,23,152,128,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,32,66,10,40,64,0,48,192,0,16,128,0,16,130,40,64,0,0,0,0,16,162,76,0,32,64,0,48,193,0,16,130,10,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,4,0,0,0,0,5,0,0,0,2,15,10,8,0,0,5,0,0,0,0,5,0,0,0,0,5,0,0,0,0,33,64,0,0,0,16,130,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,4,0,5,0,0,0,5,0,5,0,0,0,5,0,5,0,0,0,5,0,33,64,32,98,77,0,16,130,24,144,129,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,4,0,33,64,0,32,65,0,16,132,0,20,128,0,0,33,96,65,0,0,0,16,148,128,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,4,0,0,0,4,0,33,64,0,32,97,64,0,32,65,0,16,132,0,20,144,132,0,20,128,0,0,33,96,65,0,33,96,65,0,0,0,16,148,128,0,16,148,128,0,0,0,0,1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,32,64,0,32,64,0,16,160,96,80,128,0,0,16,148,128,0,0,0,32,97,64,0,0,32,80,144,160,64,0,16,128,0,16,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,4,0,33,64,0,32,65,0,16,132,0,20,128,0,0,33,96,65,0,0,0,16,148,128,0,0,0,0,5,0,0,0,0,0,5,0,0,0,0,32,65,0,0,0,0,16,128,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,10,10,46,72,0,0,0,32,81,128,0,0,32,80,128,0,0,32,80,128,0,0,0,20,128,0,0,0,0,3,10,10,10,8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,32,66,8,0,0,0,0,20,128,0,0,0,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,32,65,0,0,0,0,2,56,192,0,0,0,0,0,16,132,0,0,0,0,0,0,5,0,0,0,0,0,0,5,0,0,0,0,0,0,33,64,0,0,0,0,0,16,130,8,0,0,0,0,0,0,0,0,
0,0,4,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,5,0,0,0,1,0,0,0,0,0,
0,0,2,40,64,0,0,0,0,0,0,16,132,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,33,64,0,0,0,0,0,0,48,194,8,0,0,0,0,0,20,128,0,0,0,0,0,0,5,0,0,0,0,0,0,0,5,0,0,0,0,0,0,32,65,0,0,0,0,0,2,24,128,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,32,66,40,64,0,0,0,0,0,16,128,16,160,64,32,64,0,0,0,0,0,16,130,24,128,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,38,78,14,76,0,23,143,31,141,0,7,15,15,13,0,7,15,15,13,0,39,79,47,77,0,19,139,27,137,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
};

static const unsigned short g_fontCharStart[97] =
{
0,
3,	// space
6,
11,
18,
25,
36,
45,
49,
54,
60,
67,
75,
78,
85,
88,
94, // 0
101,
107,
114,
121,
128,
135,
142,
149,
156,
164, // :
168,
174,
181,
189,
198,
205,
216, // A
224,
231,
239,
247,
254,
261,
269,
277,
281,
286,
293,
299, // M
309,
317,
326,
333,
342,
350,
358,
366,
374,
382,
394,
402,
410, // Z
417,
421,
427,
432,
440,
449,
454, // a
461,
468,
474,
481,
488,
493,
500,
507,
510,
513,
520,
523, // m
533,
540,
547,
554,
561,
566,
572,
577,
583,
589,
599,
605,
611, // z
617,
624,
628,
636,
645,
650
};

#define IDEAL_MARGIN 0.1464466094067262378
#define B_N 0x1
#define B_E 0x2
#define B_S 0x4
#define B_W 0x8
#define B_NE 0x10
#define B_SE 0x20
#define B_SW 0x40
#define B_NW 0x80

void GImage::textCharSmall(char ch, int x, int y, int wid, int hgt, float size, unsigned int col)
{
	unsigned char charIndex = std::min((unsigned char)95, (unsigned char)(ch - 32));
	int ofs = g_fontCharStart[charIndex];
	int w = g_fontCharStart[charIndex + 1] - ofs;
	float delta = 1.0f / size;
	float t;
	float bot = (float)std::min(12, (int)(((float)(m_height - y)) * delta));
	float top = std::max(0.0f, (float)-y * delta);
	if(y < 0)
	{
		hgt += y;
		y = 0;
	}
	if(x >= (int)m_width)
		return;
	for(float yy = top; yy < bot; yy += delta)
	{
		if(hgt-- <= 0)
			break;
		int ww = x - std::max(0, x) + wid;
		unsigned int* pPix = pixelRef(std::max(0, std::min((int)m_width - 1, x)), y++);
		t = floor(yy);
		int yn = (int)t;
		float right = std::min((float)w, (m_width - std::max(0, x)) * delta);
		for(float xx = std::max(0.0f, (float)-x * delta); xx < right; xx += delta)
		{
			if(ww-- <= 0)
				break;
			t = floor(xx);
			int xn = (int)t;
			unsigned short colPixels = g_fontPixelMap[ofs + xn];
			if(colPixels & (1 << yn))
				*pPix = col;
			pPix++;
		}
	}
}

void GImage::textChar(char ch, int x, int y, int wid, int hgt, float size, unsigned int col)
{
	if(size < 1.5f)
	{
		textCharSmall(ch, x, y, wid, hgt, size, col);
		return;
	}
	unsigned char charIndex = std::min((unsigned char)95, (unsigned char)((unsigned char)ch - (unsigned char)32));
	int ofs = g_fontCharStart[charIndex];
	int w = g_fontCharStart[charIndex + 1] - ofs;
	const unsigned char* map = g_fontConnectMap + ofs * 12;
	float delta = 1.0f / size;
	float t, xo, yo, xt, yt;
	int d, e, f, u, v;
	float bot = (float)std::min(12, (int)(((float)(m_height - y)) * delta));
	float top = std::max(0.0f, (float)-y * delta);
	if(y < 0)
	{
		hgt += y;
		y = 0;
	}
	if(x >= (int)m_width)
		return;
	for(float yy = top; yy < bot; yy += delta)
	{
		if(hgt-- <= 0)
			break;
		int ww = x - std::max(0, x) + wid;
		unsigned int* pPix = pixelRef(std::max(0, x), y++);
		t = floor(yy);
		yo = yy - t;
		int yn = (int)t;
		const unsigned char* pMapRow = map + yn * w;
		float right = std::min((float)w, (m_width - std::max(0, x)) * delta);
		for(float xx = std::max(0.0f, (float)-x * delta); xx < right; xx += delta)
		{
			if(ww-- <= 0)
				break;
			t = floor(xx);
			xo = xx - t;
			int xn = (int)t;
			unsigned char m = pMapRow[xn];
			unsigned short colPixels = g_fontPixelMap[ofs + xn];
			if(colPixels & (1 << yn))
			{
				if(xo < 0.5f)
				{
					xt = xo;
					if(yo < 0.5f)
					{
						yt = yo;
						u = m & B_W;
						v = m & B_N;
						d = m & B_NW;
						e = m & B_NE;
						f = m & B_SW;
					}
					else
					{
						yt = 1.0f - yo;
						u = m & B_W;
						v = m & B_S;
						d = m & B_SW;
						e = m & B_SE;
						f = m & B_NW;
					}
				}
				else
				{
					xt = 1.0f - xo;
					if(yo < 0.5f)
					{
						yt = yo;
						u = m & B_E;
						v = m & B_N;
						d = m & B_NE;
						e = m & B_NW;
						f = m & B_SE;
					}
					else
					{
						yt = 1.0f - yo;
						u = m & B_E;
						v = m & B_S;
						d = m & B_SE;
						e = m & B_SW;
						f = m & B_NE;
					}
				}

				if(v && xt >= IDEAL_MARGIN)
					*pPix = col;
				else if(u && yt >= IDEAL_MARGIN)
					*pPix = col;
				else if(d)
					*pPix = col;
				else if(xt + yt >= 0.5f && xt >= IDEAL_MARGIN && yt >= IDEAL_MARGIN)
					*pPix = col;
				else if(e && xt + yt > 0.5f && xt > IDEAL_MARGIN && 0.5f - yt > IDEAL_MARGIN)
					*pPix = col;
				else if(f && xt + yt > 0.5f && 0.5f - xt > IDEAL_MARGIN && yt > IDEAL_MARGIN)
					*pPix = col;
			}
			else
			{
				if(xo < 0.5f)
				{
					xt = xo;
					if(yo < 0.5f)
					{
						yt = yo;
						d = m & B_NW;
					}
					else
					{
						yt = 1.0f - yo;
						d = m & B_SW;
					}
				}
				else
				{
					xt = 1.0f - xo;
					if(yo < 0.5f)
					{
						yt = yo;
						d = m & B_NE;
					}
					else
					{
						yt = 1.0f - yo;
						d = m & B_SE;
					}
				}
				if(d && xt + yt < 0.5f)
					*pPix = col;
			}
			pPix++;
		}
	}
}

void GImage::text(const char* text, int x, int y, float size, unsigned int col, int wid, int hgt)
{
	while(*text != '\0')
	{
		unsigned char charIndex = std::min((unsigned char)95, (unsigned char)(*text - 32));
		int ofs = g_fontCharStart[charIndex];
		int w = g_fontCharStart[charIndex + 1] - ofs;
		textChar(*text, x, y, wid, hgt, size, col);
		int charwid = std::max(1, (int)((float)w * size));
		x += charwid;
		wid -= charwid;
		text++;
	}
}

// static
int GImage::measureCharWidth(char c, float size)
{
	unsigned char charIndex = std::min((unsigned char)95, (unsigned char)(c - 32));
	int ofs = g_fontCharStart[charIndex];
	int w = g_fontCharStart[charIndex + 1] - ofs;
	return std::max(1, (int)((float)w * size));
}

// static
int GImage::measureTextWidth(const char* text, float size)
{
	int wid = 0;
	while(*text != '\0')
	{
		wid += measureCharWidth(*text, size);
		text++;
	}
	return wid;
}

int GImage::countTextChars(int horizArea, const char* text, float size)
{
	int charCount = 0;
	while(*text != '\0')
	{
		unsigned char charIndex = std::min((unsigned char)95, (unsigned char)(*text - 32));
		int ofs = g_fontCharStart[charIndex];
		int w = g_fontCharStart[charIndex + 1] - ofs;
		horizArea -= std::max(1, (int)((float)w * size));
		if(horizArea < 0)
			break;
		charCount++;
		text++;
	}
	return charCount;
}

void GImage::blit(int x, int y, GImage* pSource, GRect* pSourceRect)
{
	int sx, sy, sw, sh;
	if(pSourceRect)
	{
		sx = pSourceRect->x;
		sy = pSourceRect->y;
		sw = pSourceRect->w;
		sh = pSourceRect->h;
	}
	else
	{
		sx = 0;
		sy = 0;
		sw = pSource->width();
		sh = pSource->height();
	}
	if(x < 0)
	{
		sx -= x;
		sw += x;
		x = 0;
	}
	if(x + sw > (int)m_width)
		sw = (int)m_width - x;
	if(y < 0)
	{
		sy -= y;
		sh += y;
		y = 0;
	}
	if(y + sh > (int)m_height)
		sh = (int)m_height - y;
	if(sw <= 0)
		return;
	int dst = y * m_width + x;
	int src = sy * pSource->m_width + sx;
	sw *= sizeof(unsigned int);
	for( ; sh > 0; sh--)
	{
		memcpy(&m_pPixels[dst], &pSource->m_pPixels[src], sw);
		dst += m_width;
		src += pSource->m_width;
	}
}

void GImage::blitAlpha(int x, int y, GImage* pSource, GRect* pSourceRect)
{
	int sx, sy, sw, sh;
	if(pSourceRect)
	{
		sx = pSourceRect->x;
		sy = pSourceRect->y;
		sw = pSourceRect->w;
		sh = pSourceRect->h;
	}
	else
	{
		sx = 0;
		sy = 0;
		sw = pSource->width();
		sh = pSource->height();
	}
	if(x < 0)
	{
		sx -= x;
		sw += x;
		x = 0;
	}
	if(x + sw > (int)m_width)
		sw = (int)m_width - x;
	if(y < 0)
	{
		sy -= y;
		sh += y;
		y = 0;
	}
	if(y + sh > (int)m_height)
		sh = (int)m_height - y;
	int dst = y * m_width + x;
	int src = sy * pSource->m_width + sx;
	int xx, a;
	unsigned int pix, pixOld;
	for( ; sh > 0; sh--)
	{
		for(xx = 0; xx < sw; xx++)
		{
			pix = pSource->m_pPixels[src + xx];
			a = gAlpha(pix);
			pixOld = m_pPixels[dst + xx];
			m_pPixels[dst + xx] = gARGB(std::max(a, (int)gAlpha(pixOld)),
							(a * gRed(pix) + (256 - a) * gRed(pixOld)) >> 8,
							(a * gGreen(pix) + (256 - a) * gGreen(pixOld)) >> 8,
							(a * gBlue(pix) + (256 - a) * gBlue(pixOld)) >> 8);
		}
		dst += m_width;
		src += pSource->m_width;
	}
}

void GImage::blitAlphaStretch(GRect* pDestRect, GImage* pSource, GRect* pSourceRect)
{
	float fSourceX, fSourceY, fSourceDX, fSourceDY;
	if(pSourceRect)
	{
		fSourceDX =  (float)(pSourceRect->w - 1) / (float)(pDestRect->w - 1);
		fSourceDY = (float)(pSourceRect->h - 1) / (float)(pDestRect->h - 1);
		fSourceX = (float)pSourceRect->x;
		fSourceY = (float)pSourceRect->y;
	}
	else
	{
		fSourceDX =  (float)(pSource->width() - 1) / (float)(pDestRect->w - 1);
		fSourceDY = (float)(pSource->height() - 1) / (float)(pDestRect->h - 1);
		fSourceX = 0.0f;
		fSourceY = 0.0f;
	}
	int xStart = pDestRect->x;
	int xEnd = pDestRect->x + pDestRect->w;
	int yStart = pDestRect->y;
	int yEnd = pDestRect->y + pDestRect->h;

	// Clip
	if(xStart < 0)
	{
		fSourceX = (-xStart) * fSourceDX;
		xStart = 0;
	}
	if(yStart < 0)
	{
		fSourceY = (-yStart) * fSourceDY;
		yStart = 0;
	}
	if(xEnd > (int)m_width)
		xEnd = (int)m_width;
	if(yEnd > (int)m_height)
		yEnd = (int)m_height;

	// Blit
	unsigned int colIn, colOld;
	int x, y, a;
	float fSX;
	unsigned int* pPix;
	for(y = yStart; y < yEnd; y++)
	{
		fSX = fSourceX;
		pPix = pixels() + (y * m_width) + xStart;
		for(x = xStart; x < xEnd; x++)
		{
			colIn = pSource->pixel((int)fSX, (int)fSourceY);
			a = gAlpha(colIn);
			colOld = *pPix;
			*pPix = gARGB(std::max(a, (int)gAlpha(colOld)),
						(a * gRed(colIn) + (256 - a) * gRed(colOld)) >> 8,
						(a * gGreen(colIn) + (256 - a) * gGreen(colOld)) >> 8,
						(a * gBlue(colIn) + (256 - a) * gBlue(colOld)) >> 8);
			pPix++;
			fSX += fSourceDX;
		}
		fSourceY += fSourceDY;
	}
}

void GImage::blitStretchInterpolate(GDoubleRect* pDestRect, GImage* pSource, GDoubleRect* pSourceRect)
{
	double sx, sy, sw, sh;
	if(pSourceRect)
	{
		sx = pSourceRect->x;
		sy = pSourceRect->y;
		sw = pSourceRect->w;
		sh = pSourceRect->h;
	}
	else
	{
		sx = 0;
		sy = 0;
		sw = pSource->width();
		sh = pSource->height();
	}
	int t = std::max(0, (int)floor(pDestRect->y));
	int b = std::min((int)height() - 1, (int)ceil(pDestRect->y + pDestRect->h));
	int l = std::max(0, (int)floor(pDestRect->x));
	int r = std::min((int)width() - 1, (int)ceil(pDestRect->x + pDestRect->w));
	for(int y = t; y <= b; y++)
	{
		double yy = ((double)y - pDestRect->y) * sh / pDestRect->h + sy;
		for(int x = l; x <= r; x++)
		{
			double xx = ((double)x - pDestRect->x) * sw / pDestRect->w + sx;
			setPixel(x, y, pSource->interpolatePixel((float)xx, (float)yy));
		}
	}
}

void GImage::moveLight(double dRadians, float fAmount)
{
	float dx = (float)cos(dRadians);
	float dy = (float)sin(dRadians);
	int nDelta;
	unsigned int c1, c2;
	int x, y;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			c1 = pixel(x, y);
			c2 = interpolatePixel(x + dx, y + dy);
			nDelta = (int)gGray(c1) - gGray(c2);
			setPixel(x, y, gARGB(
				gAlpha(c1),
				ClipChan(gRed(c1) + (int)(nDelta * fAmount)),
				ClipChan(gGreen(c1) + (int)(nDelta * fAmount)),
				ClipChan(gBlue(c1) + (int)(nDelta * fAmount))));
		}
	}
}

GImage* GImage::munge(int nStyle, float fExtent)
{
	GImage* pMunged = new GImage();
	pMunged->setSize(m_width, m_height);
	int x, y;
	unsigned int col;
	double d;
	GRand prng(0);
	switch(nStyle)
	{
		case 0: // particle-blur (pick a random pixel in the proximity)
			for(y = 0; y < (int)m_height; y++)
			{
				for(x = 0; x < (int)m_width; x++)
				{
					col = pixelNearest(
						(int)(x + fExtent * m_width * (prng.uniform() - .5)),
						(int)(y + fExtent * m_height * (prng.uniform() - .5))
						);
					pMunged->setPixel(x, y, col);
				}
			}
			break;

		case 1: // shadow threshold (throw out all pixels below a certain percent of the total brighness)
			{
				fExtent = 1 - fExtent;
				fExtent *= fExtent;
				fExtent *= fExtent;
				fExtent = 1 - fExtent;

				// Create the histogram data
				unsigned int pnHistData[257];
				memset(pnHistData, '\0', sizeof(int) * 257);
				unsigned int nSize = m_width * m_height;
				unsigned int nPos;
				unsigned int nGray;
				for(nPos = 0; nPos < nSize; nPos++)
					pnHistData[gGray(m_pPixels[nPos]) >> 8]++;

				// Turn it into cumulative histogram data
				int n;
				for(n = 1; n < 256; n++)
					pnHistData[n] += pnHistData[n - 1];

				// Find the cut-off
				unsigned int nCutOff = (unsigned int)(fExtent * pnHistData[255]);
				for(n = 0; n < 256 && pnHistData[n] < nCutOff; n++)
				{
				}

				// Copy all the data above the threshold
				for(y = 0; y < (int)m_height; y++)
				{
					for(x = 0; x < (int)m_width; x++)
					{
						col = pixel(x, y);
						nGray = gGray(col) >> 8;
						if(nGray > (unsigned int)n)
							pMunged->setPixel(x, y, col);
					}
				}
			}
			break;

		case 2: // waves
			for(y = 0; y < (int)m_height; y++)
			{
				for(x = 0; x < (int)m_width; x++)
				{
					col = pixelNearest(
										(int)(x + fExtent * (m_width / 2) * cos((double)x * 16 / m_width)),
										(int)(y + fExtent * (m_height / 2) * sin((double)y * 16 / m_height))
									);
					pMunged->setPixel(x, y, col);
				}
			}
			break;

		case 3: // waved in or out of the middle
				fExtent = 1 - fExtent;
				fExtent *= fExtent;
				fExtent = 1 - fExtent;
			for(y = 0; y < (int)m_height; y++)
			{
				for(x = 0; x < (int)m_width; x++)
				{
					d = atan2((double)(y - m_height / 2), (double)(x - m_width / 2));
					d = fExtent * cos(d * 5);
					col = pixelNearest(
										(int)((1 - d) * x + d * (m_width / 2)),
										(int)((1 - d) * y + d * (m_height / 2))
									);
					pMunged->setPixel(x, y, col);
				}
			}
			break;
	}
	return pMunged;
}

void GImage::triangleFill(float x1, float y1, float x2, float y2, float x3, float y3, unsigned int c)
{
	// Get y1 on top, y2 in middle, and y3 on bottom
	if(y2 < y1)
	{
		std::swap(y2, y1);
		std::swap(x2, x1);
	}
	if(y3 < y1)
	{
		std::swap(y3, y1);
		std::swap(x3, x1);
	}
	if(y3 < y2)
	{
		std::swap(y3, y2);
		std::swap(x3, x2);
	}

	// Compute step sizes
	float fx1 = x1 + 0.5f;
	float fx2 = x1 + 0.5f;
	float dx1, dx2;
	if(y1 == y2)
	{
		fx1 = x2 + 0.5f;
		dx1 = 0.0f;
	}
	else
		dx1 = (x2 - x1) / std::max(1.0f, (y2 - y1));
	if(y1 == y3)
	{
		fx2 = x3 + 0.5f;
		dx2 = 0.0f;
	}
	else
		dx2 = (x3 - x1) / std::max(1.0f, (y3 - y1));

	// Draw the first half
	int x, xMax, y;
	int top = (int)floor(y1 + 0.5f);
	if(top < 0)
	{
		top = 0;
		fx1 += (-y1) * dx1;
		fx2 += (-y1) * dx2;
	}
	int bot = std::min((int)m_height - 1, (int)floor(y2 + 0.5f));
	if(dx1 < dx2)
	{
		for(y = top; y <= bot; y++)
		{
			x = std::max(0, (int)fx1);
			xMax = std::min((int)m_width - 1, (int)fx2);
			if(x <= xMax)
			{
				for(unsigned int* pPix = pixelRef(x, y); x <= xMax; x++)
					*(pPix++) = c;
			}
			fx1 += dx1;
			fx2 += dx2;
		}
	}
	else
	{
		for(y = top; y <= bot; y++)
		{
			x = std::max(0, (int)fx2);
			xMax = std::min((int)m_width - 1, (int)fx1);
			if(x <= xMax)
			{
				for(unsigned int* pPix = pixelRef(x, y); x <= xMax; x++)
					*(pPix++) = c;
			}
			fx1 += dx1;
			fx2 += dx2;
		}
	}

	// Draw the second half
	fx1 = x2 + 0.5f;
	if(y2 == y3)
		dx1 = 0.0f;
	else
		dx1 = (x3 - x2) / std::max(1.0f, (y3 - y2));
	fx1 += dx1;
	if(y2 < 0)
		fx1 += (-y2) * dx1;
	bot = std::min((int)m_height - 1, (int)floor(y3 + 0.5f));
	if(fx1 < fx2)
	{
		for( ; y <= bot; y++)
		{
			x = std::max(0, (int)fx1);
			xMax = std::min((int)m_width - 1, (int)fx2);
			if(x <= xMax)
			{
				for(unsigned int* pPix = pixelRef(x, y); x <= xMax; x++)
					*(pPix++) = c;
			}
			fx1 += dx1;
			fx2 += dx2;
		}
	}
	else
	{
		for( ; y <= bot; y++)
		{
			x = std::max(0, (int)fx2);
			xMax = std::min((int)m_width - 1, (int)fx1);
			if(x <= xMax)
			{
				for(unsigned int* pPix = pixelRef(x, y); x <= xMax; x++)
					*(pPix++) = c;
			}
			fx1 += dx1;
			fx2 += dx2;
		}
	}
}
/*
void GImage::fatLine(float x1, float y1, float x2, float y2, float fThickness, unsigned int color)
{
	double dAngle = atan2((double)(y2 - y1), (double)(x2 - x1));
	float c = (fThickness * (float)cos(dAngle) / 2.0f + 0.5f);
	float s = (fThickness * (float)sin(dAngle) / 2.0f + 0.5f);
	triangleFill(x1 - s, y1 + c, x1 + s, y1 - c, x2 + s, y2 - c, color);
	triangleFill(x2 + s, y2 - c, x2 - s, y2 + c, x1 - s, y1 + c, color);
}
*/
void GImage::fatLine(float x1, float y1, float x2, float y2, float thickness, unsigned int color)
{
	float dy = y2 - y1;
	float dx = x2 - x1;
	if(std::abs(dx) < std::abs(dy))
	{
		if(dy <= 0.0f)
		{
			if(dy == 0.0f)
				return;
			std::swap(x1, x2);
			std::swap(y1, y2);
		}
		double hw = 0.5 * sqrt(thickness * thickness * ((dx * dx) / (dy * dy) + 1.0));
		int beg = std::max(0, (int)floor(y1 + 0.5f));
		int end = std::min((int)m_height - 1, (int)floor(y2 + 0.5f));
		float df = dx / dy;
		float f = x1 + ((float)beg - y1) * df;
		for(; beg <= end; beg++)
		{
			int bb = std::max(0, (int)floor(f - hw + 0.5f));
			int ee = std::min((int)m_width - 1, (int)floor(f + hw + 0.5f));
			for(; bb <= ee; bb++)
				setPixel(bb, beg, color);
			f += df;
		}
	}
	else
	{
		if(dx < 0.0f)
		{
			std::swap(x1, x2);
			std::swap(y1, y2);
		}
		double hw = 0.5 * sqrt(thickness * thickness * ((dy * dy) / (dx * dx) + 1.0));
		int beg = std::max(0, (int)floor(x1 + 0.5f));
		int end = std::min((int)m_width - 1, (int)floor(x2 + 0.5f));
		float df = dy / dx;
		float f = y1 + ((float)beg - x1) * df;
		for(; beg <= end; beg++)
		{
			int bb = std::max(0, (int)floor(f - hw + 0.5f));
			int ee = std::min((int)m_height - 1, (int)floor(f + hw + 0.5f));
			for(; bb <= ee; bb++)
				setPixel(beg, bb, color);
			f += df;
		}
	}
}

void GImage::stretch(int nXStart, int nYStart, int nXEnd, int nYEnd)
{
	GImage tmp;
	tmp.setSize(width(), height());
	unsigned int col;
	float d;
	double dSize = sqrt((double)((nXEnd - nXStart) * (nXEnd - nXStart) + (nYEnd - nYStart) * (nYEnd - nYStart)));
	if(dSize == 0)
		dSize = .01;
	int x, y;
	for(y = 0; y < (int)tmp.height(); y++)
	{
		for(x = 0; x < (int)tmp.width(); x++)
		{
			d = (float)(exp(-(((x - nXEnd) * (x - nXEnd) + (y - nYEnd) * (y - nYEnd)) / (dSize * dSize))));
			col = interpolatePixel((float)x - d * (nXEnd - nXStart), (float)y - d * (nYEnd - nYStart));
			tmp.setPixel(x, y, col);
		}
	}
	swapData(&tmp);
}

void GImage::captcha(const char* szText, GRand* pRand)
{
	int nWidth = measureTextWidth(szText, 4.0f);
	setSize(nWidth + 32, 64);
	clear(0xffeeeecc);
	GRect r(16, 16, nWidth + 16, 32);
	text(szText, 16, 16, 4.0f, 0xff444400, nWidth, 64);
	GImage* pTmp = munge(0, 0.01f);
	swapData(pTmp);
	delete(pTmp);
	int i;
	for(i = 0; i < 3; i++)
		lineNoChecks((int)pRand->next(nWidth + 32), (int)pRand->next(64), (int)pRand->next(nWidth + 32), (int)pRand->next(64), 0xff444400);
	pTmp = munge(3, 0.02f);
	swapData(pTmp);
	delete(pTmp);
	for(i = 0; i < 3; i++)
	{
		int x = (int)pRand->next(nWidth + 32);
		int y = (int)pRand->next(64);
		int dx = (int)pRand->next(40) - 20;
		int dy = (int)pRand->next(40) - 20;
		if(std::abs(dx) < 10 && std::abs(dy) < 10)
		{
			dx = 15;
			dy = -12;
		}
		stretch(x, y, x + dx, y + dy);
	}
	for(i = 0; i < 4; i++)
		lineNoChecks((int)pRand->next(nWidth + 32), (int)pRand->next(64), (int)pRand->next(nWidth + 32), (int)pRand->next(64), 0xff444400);
	pTmp = munge(0, 0.01f);
	swapData(pTmp);
	delete(pTmp);
}

void GImage::gaussianKernel(int nWidth, float fDepth)
{
	GAssert(nWidth >= 1); // out of range
	GAssert(fDepth >= 1 && fDepth <= 255); // out of range
	double dRadius = sqrt(std::max(0.0, -2.0 * log(1.0 / (double)fDepth)));
	setSize(nWidth, nWidth);
	double dCenter = (double)(nWidth - 1) / 2.0;
	int x, y, v;
	double r;
	float val;
	for(y = 0; y < nWidth; y++)
	{
		for(x = 0; x < nWidth; x++)
		{
			r = sqrt((dCenter - x) * (dCenter - x) + (dCenter - y) * (dCenter - y));
			val = fDepth * (float)GMath::gaussian(r * dRadius / dCenter);
			v = ClipChan((int)(val + (float).5));
			setPixel(x, y, gARGB(0xff, v, v, v));
		}
	}
}

void GImage::highPassFilter(double dExtent)
{
	dExtent *= (2.0 * std::max(width(), height()));

	// Convert to the Fourier domain
	int nChannelWidth, nChannelHeight;
	struct ComplexNumber* pArray = GFourier::imageToFftArray(this, &nChannelWidth, &nChannelHeight);
	ArrayHolder<struct ComplexNumber> hArray(pArray);
	int nChannelSize = nChannelWidth * nChannelHeight;

	// Filter out the low frequency data
	double d, fac;
	int radius = (int)dExtent;
	int x, y, i, nStart;
	for(y = 0; y < radius && y < nChannelHeight; y++)
	{
		for(x = 0; x < radius && x < nChannelWidth; x++)
		{
			d = sqrt((double)(x * x + y * y));
			if((int)d >= radius)
				break;
			fac = GMath::softStep(d / dExtent, 2.0);
			nStart = 0;
			for(i = 0; i < 3; i++)
			{
				pArray[nStart + y * nChannelWidth + x].real *= fac;
				pArray[nStart + y * nChannelWidth + x].imag *= fac;
				pArray[nStart + y * nChannelWidth + nChannelWidth - 1 - x].real *= fac;
				pArray[nStart + y * nChannelWidth + nChannelWidth - 1 - x].imag *= fac;
				pArray[nStart + (nChannelHeight - 1 - y) * nChannelWidth + x].real *= fac;
				pArray[nStart + (nChannelHeight - 1 - y) * nChannelWidth + x].imag *= fac;
				pArray[nStart + (nChannelHeight - 1 - y) * nChannelWidth + nChannelWidth - 1 - x].real *= fac;
				pArray[nStart + (nChannelHeight - 1 - y) * nChannelWidth + nChannelWidth - 1 - x].imag *= fac;
				nStart += nChannelSize;
			}
		}
	}

	// Convert back to the spatial domain
	GFourier::fftArrayToImage(pArray, nChannelWidth, nChannelHeight, this, true);
}

void GImage::lowPassFilter(double dExtent)
{
	dExtent *= (2.0 * std::max(width(), height()));

	// Convert to the Fourier domain
	int nChannelWidth, nChannelHeight;
	struct ComplexNumber* pArray = GFourier::imageToFftArray(this, &nChannelWidth, &nChannelHeight);
	ArrayHolder<struct ComplexNumber> hArray(pArray);
	int nChannelSize = nChannelWidth * nChannelHeight;

	// Filter out the low frequency data
	double d, fac;
	int radius = (int)dExtent;
	int x, y, i, nStart;
	for(y = 0; y < radius && y < nChannelHeight; y++)
	{
		for(x = 0; x < radius && x < nChannelWidth; x++)
		{
			d = sqrt((double)(x * x + y * y));
			if((int)d >= radius)
				break;
			fac = 1.0 - GMath::softStep(d / dExtent, 2.0);
			nStart = 0;
			for(i = 0; i < 3; i++)
			{
				pArray[nStart + y * nChannelWidth + x].real *= fac;
				pArray[nStart + y * nChannelWidth + x].imag *= fac;
				pArray[nStart + y * nChannelWidth + nChannelWidth - 1 - x].real *= fac;
				pArray[nStart + y * nChannelWidth + nChannelWidth - 1 - x].imag *= fac;
				pArray[nStart + (nChannelHeight - 1 - y) * nChannelWidth + x].real *= fac;
				pArray[nStart + (nChannelHeight - 1 - y) * nChannelWidth + x].imag *= fac;
				pArray[nStart + (nChannelHeight - 1 - y) * nChannelWidth + nChannelWidth - 1 - x].real *= fac;
				pArray[nStart + (nChannelHeight - 1 - y) * nChannelWidth + nChannelWidth - 1 - x].imag *= fac;
				nStart += nChannelSize;
			}
		}
	}

	// Convert back to the spatial domain
	GFourier::fftArrayToImage(pArray, nChannelWidth, nChannelHeight, this, true);
}

void GImage::threshold(int nGrayscaleValue)
{
	int x, y, g;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			g = gGray(pixel(x, y));
			if(g >= nGrayscaleValue)
				setPixel(x, y, 0xffffffff);
			else
				setPixel(x, y, 0xff000000);
		}
	}
}

void GImage::medianFilter(float fRadius)
{
	int x, y, i, j, d2, median, size;
	int r = (int)ceil(fRadius);
	vector<int> arr(r * r);
	float fRadiusSquared = fRadius * fRadius;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			arr.clear();
			for(j = y - r; j <= y + r; j++)
			{
				if(j < 0)
					continue;
				if(j >= (int)m_height)
					break;
				for(i = x - r; i <= x + r; i++)
				{
					if(i < 0)
						continue;
					if(i >= (int)m_width)
						break;
					d2 = (x - i) * (x - i) + (y - j) * (y - j);
					if(d2 > (int)fRadiusSquared)
						continue;
					arr.push_back(gGray(pixel(i, j)));
				}
			}
			size = (int)arr.size();
			GAssert(size > 0); // no data
			std::sort(arr.begin(), arr.end());
			median = (arr[(size - 1) / 2] + arr[size / 2]) / 2;
			median = median >> 8;
			GAssert(median >= 0 && median <= 255); // out of range
			setPixel(x, y, gARGB(0xff, median, median, median));
		}
	}
}

void GImage::dialate(GImage* pStructuringElement)
{
	GImage target;
	target.setSize(width(), height());
	int x, y, i, j, r, g, b, u, v;
	unsigned int c1, c2;
	int dx = pStructuringElement->width() / 2;
	int dy = pStructuringElement->height() / 2;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			r = 0;
			g = 0;
			b = 0;
			for(j = 0; j < (int)pStructuringElement->height(); j++)
			{
				v = y + j - dy;
				if(v < 0)
					continue;
				if(v >= (int)m_height)
					break;
				for(i = 0; i < (int)pStructuringElement->m_width; i++)
				{
					u = x + i - dx;
					if(u < 0)
						continue;
					if(u >= (int)m_width)
						break;
					c1 = pixel(u, v);
					c2 = pStructuringElement->pixel(i, j);
					r = std::max(r, (int)gRed(c1) + (int)gRed(c2));
					g = std::max(g, (int)gGreen(c1) + (int)gGreen(c2));
					b = std::max(b, (int)gBlue(c1) + (int)gBlue(c2));
				}
			}
			target.setPixel(x, y, gARGB(0xff, ClipChan(r), ClipChan(g), ClipChan(b)));
		}
	}
	swapData(&target);
}

void GImage::erode(GImage* pStructuringElement)
{
	GImage target;
	target.setSize(width(), height());
	int x, y, i, j, r, g, b, u, v;
	unsigned int c1, c2;
	int dx = pStructuringElement->width() / 2;
	int dy = pStructuringElement->height() / 2;
	for(y = 0; y < (int)height(); y++)
	{
		for(x = 0; x < (int)width(); x++)
		{
			r = 255 * 256;
			g = 255 * 256;
			b = 255 * 256;
			for(j = 0; j < (int)pStructuringElement->height(); j++)
			{
				v = y + j - dy;
				if(v < 0)
					continue;
				if(v >= (int)height())
					break;
				for(i = 0; i < (int)pStructuringElement->width(); i++)
				{
					u = x + i - dx;
					if(u < 0)
						continue;
					if(u >= (int)width())
						break;
					c1 = pixel(u, v);
					c2 = pStructuringElement->pixel(i, j);
					r = std::min(r, (int)gRed(c1) - (int)gRed(c2));
					g = std::min(g, (int)gGreen(c1) - (int)gGreen(c2));
					b = std::min(b, (int)gBlue(c1) - (int)gBlue(c2));
				}
			}
			target.setPixel(x, y, gARGB(0xff, ClipChan(r), ClipChan(g), ClipChan(b)));
		}
	}
	swapData(&target);
}

void GImage::open(int nPixels)
{
	GImage structuringElement;
	structuringElement.gaussianKernel((int)(std::abs(nPixels) * 2 + 1), (float)1);
	if(nPixels > 0)
	{
		erode(&structuringElement);
		dialate(&structuringElement);
	}
	else
	{
		dialate(&structuringElement);
		erode(&structuringElement);
	}
}

void GImage::arrow(int x1, int y1, int x2, int y2, unsigned int col, int headSize)
{
	line(x1, y1, x2, y2, col);
	double theta = atan2((double)(y2 - y1), (double)(x2 - x1));
	line(x2, y2, x2 + (int)(headSize * cos(theta + M_PI + .3)), y2 + (int)(headSize * sin(theta + M_PI + .314)), col);
	line(x2, y2, x2 + (int)(headSize * cos(theta + M_PI - .3)), y2 + (int)(headSize * sin(theta + M_PI - .314)), col);
}

double GImage::moment(double centerX, double centerY, double i, double j)
{
	int x, y, g;
	double dNom = 0;
	double dDenom = 0;
	double d;
	for(y = 0; y < (int)height(); y++)
	{
		for(x = 0; x < (int)width(); x++)
		{
			d = pow((double)x - centerX, i);
			d *= pow((double)y - centerY, j);
			g = gGray(pixel(x, y));
			dNom += (d * g);
			dDenom += g;
		}
	}
	return dNom / dDenom;
}

void GImage::meanAndOrientation(double* pMeanX, double* pMeanY, double* pRadians)
{
	*pMeanX = moment(0, 0, 1, 0);
	*pMeanY = moment(0, 0, 0, 1);
	double dNom = 2.0 * moment(*pMeanX, *pMeanY, 1, 1);
	double dX = moment(*pMeanX, *pMeanY, 2, 0);
	double dY = moment(*pMeanX, *pMeanY, 0, 2);
	double dDenom = dX - dY;
	*pRadians = atan2(dNom, dDenom) / 2.0;
}

void GImage::gradientMagnitudeImage(const GImage* pIn)
{
	setSize(pIn->width(), pIn->height());
	unsigned int c1, c2, c3, c4, c5, c6, c7, c8;
	int x, y, r1, g1, b1, r2, g2, b2;
	for(y = 0; y < (int)height(); y++)
	{
		for(x = 0; x < (int)width(); x++)
		{
			c1 = pIn->pixelNearest(x - 1, y - 1);
			c2 = pIn->pixelNearest(x, y - 1);
			c3 = pIn->pixelNearest(x + 1, y - 1);
			c4 = pIn->pixelNearest(x - 1, y);
			c5 = pIn->pixelNearest(x + 1, y);
			c6 = pIn->pixelNearest(x - 1, y + 1);
			c7 = pIn->pixelNearest(x, y + 1);
			c8 = pIn->pixelNearest(x + 1, y + 1);
			r1 = gRed(c1) + 2 * gRed(c2) + gRed(c3) - (gRed(c6) + 2 * gRed(c7) + gRed(c8));
			g1 = gGreen(c1) + 2 * gGreen(c2) + gGreen(c3) - (gGreen(c6) + 2 * gGreen(c7) + gGreen(c8));
			b1 = gBlue(c1) + 2 * gBlue(c2) + gBlue(c3) - (gBlue(c6) + 2 * gBlue(c7) + gBlue(c8));
			r2 = gRed(c1) + 2 * gRed(c4) + gRed(c6) - (gRed(c3) + 2 * gRed(c5) + gRed(c8));
			g2 = gGreen(c1) + 2 * gGreen(c4) + gGreen(c6) - (gGreen(c3) + 2 * gGreen(c5) + gGreen(c8));
			b2 = gBlue(c1) + 2 * gBlue(c4) + gBlue(c6) - (gBlue(c3) + 2 * gBlue(c5) + gBlue(c8));
			//r1 = (int)(sqrt((double)(r1 * r1 + r2 * r2)) / 5.65685424949238); // 5.65854 = 4 * sqrt(2)
			//g1 = (int)(sqrt((double)(g1 * g1 + g2 * g2)) / 5.65685424949238);
			//b1 = (int)(sqrt((double)(b1 * b1 + b2 * b2)) / 5.65685424949238);
			//GAssert(r1 >= 0 && r1 < 257 && g1 >= 0 && g1 < 257 && b1 >= 0 && b1 < 257); // bad clippage
			//setPixel(x, y, gARGB(0xff, ClipChan(r1), ClipChan(g1), ClipChan(b1)));
			setPixel(x, y, std::max(r1 * r1 + r2 * r2, std::max(g1 * g1 + g2 * g2, b1 * b1 + b2 * b2)));
		}
	}
}

void GImage::setGlobalAlpha(int alpha)
{
	unsigned int c;
	int x, y;
	for(y = 0; y < (int)height(); y++)
	{
		for(x = 0; x < (int)width(); x++)
		{
			c = pixel(x, y);
			setPixel(x, y, gARGB(alpha, gRed(c), gGreen(c), gBlue(c)));
		}
	}
}

// static
bool GImage::isPointInsideTriangle(float x, float y,
				float x0, float y0,
				float x1, float y1,
				float x2, float y2)
{
	int s1 = GBits::sign((x1 - x0) * (y0 - y) - (x0 - x) * (y1 - y0));
	if(s1 == 0)
		return true;
	int s2 = GBits::sign((x2 - x1) * (y1 - y) - (x1 - x) * (y2 - y1));
	if(s2 == 0)
		return true;
	if(s1 != s2)
		return false;
	s2 = GBits::sign((x0 - x2) * (y2 - y) - (x2 - x) * (y0 - y2));
	if(s2 == 0)
		return true;
	if(s1 != s2)
		return false;
	return true;
}

// static
void GImage::triangleWeights(float* pW0, float* pW1, float* pW2,
				float x, float y,
				float x0, float y0,
				float x1, float y1,
				float x2, float y2)
{
	float t = (float)1.0 / ((x0 * y1 + x1 * y2 + x2 * y0) -
				(y0 * x1 + y1 * x2 + y2 * x0));
	*pW0 = t * ((x1 * y2 - x2 * y1) + (y1 - y2) * x + (x2 - x1) * y);
	*pW1 = t * ((x2 * y0 - x0 * y2) + (y2 - y0) * x + (x0 - x2) * y);
	*pW2 = t * ((x0 * y1 - x1 * y0) + (y0 - y1) * x + (x1 - x0) * y);
}

unsigned int GImage::interpolateWithinTriangle(float w0, float w1, float w2,
				float x0, float y0,
				float x1, float y1,
				float x2, float y2)
{
	return interpolatePixel(w0 * x0 + w1 * x1 + w2 * x2, w0 * y0 + w1 * y1 + w2 * y2);
}

void GImage::rotateCounterClockwise90(GImage* pSourceImage)
{
	setSize((int)pSourceImage->m_height, (int)pSourceImage->m_width);
	int x, y, z;
	for(y = 0; y < (int)m_height; y++)
	{
		z = m_height - 1 - y;
		for(x = 0; x < (int)m_width; x++)
			setPixel(x, y, pSourceImage->pixel(z, x));
	}
}

void GImage::rotateClockwise90(GImage* pSourceImage)
{
	setSize(pSourceImage->m_height, pSourceImage->m_width);
	int x, y;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
			setPixel(x, y, pSourceImage->pixel(y, m_width - 1 - x));
	}
}

void GImage::contrastAndBrightness(float fContrastScale, int nBrightnessDelta)
{
	int x, y;
	float fCenter = (float)128 - fContrastScale * 128;
	unsigned int* pPix;
	unsigned int c;
	for(y = 0; y < (int)m_height; y++)
	{
		pPix = pixelRef(0, y);
		for(x = 0; x < (int)m_width; x++)
		{
			c = *pPix;
			*pPix = gARGB(gAlpha(c),
				ClipChan((int)(fContrastScale * gRed(c) + fCenter) + nBrightnessDelta),
				ClipChan((int)(fContrastScale * gGreen(c) + fCenter) + nBrightnessDelta),
				ClipChan((int)(fContrastScale * gBlue(c) + fCenter) + nBrightnessDelta)
				);
			pPix++;
		}
	}
}

void GImage::hueSaturationAndValue(GImage* pSource, float hue, float saturation, float value)
{
	setSize(pSource->m_width, pSource->m_height);
	int x, y;
	float h, s, v;
	unsigned int c;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			c = pSource->pixel(x, y);
			rgbToHsv(c, &h, &s, &v);
			h = h + hue + 10000; // 10000 = just some big number to ensure we've got a positive value
			h -= ((int)h); // drop any digits that come before the decimal point
			s = std::max((float)0, std::min((float)1, s + saturation));
			v = std::max((float)0, std::min((float)1, v + value));
			setPixel(x, y, gAHSV(gAlpha(c), h, s, v));
		}
	}
}

void GImage::shade(float fStart, float fEnd, float fSteepness)
{
	GAssert(fStart >= fEnd); // fStart should be >= fEnd
	float fBrightness, f;
	int x, y;
	unsigned int c;
	for(y = 0; y < (int)m_height; y++)
	{
		for(x = 0; x < (int)m_width; x++)
		{
			c = pixel(x, y);
			fBrightness = (float)gGray(c) / 65280;
			if(fBrightness < fStart)
			{
				if(fBrightness <= fEnd)
				{
					setPixel(x, y, gARGB(gAlpha(c), 0, 0, 0));
				}
				else
				{
					f = (float)GMath::softStep((fBrightness - fEnd) / (fStart - fEnd), fSteepness);
					setPixel(x, y, gARGB(gAlpha(c),
						ClipChan((int)(f * gRed(c))),
						ClipChan((int)(f * gGreen(c))),
						ClipChan((int)(f * gBlue(c)))
						));
				}
			}
		}
	}
}

void GImage::convertAbgrToArgb()
{
	unsigned int count = m_width * m_height;
	for(unsigned i = 0; i < count; i++)
		m_pPixels[i] = abgrToArgb((unsigned int)m_pPixels[i]);
}

void GImage::replaceColor(unsigned int before, unsigned int after)
{
	unsigned int* pEnd = pixels() + (m_width * m_height);
	for(unsigned int* pPix = pixels(); pPix != pEnd; pPix++)
	{
		if(*pPix == before)
			*pPix = after;
	}
}

} // namespace GClasses
