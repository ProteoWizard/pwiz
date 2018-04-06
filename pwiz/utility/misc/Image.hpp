//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _IMAGE_HPP_
#define _IMAGE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <memory>
#include <string>


namespace pwiz {
namespace util {


/// wrapper class for using 'gd' graphics library
class PWIZ_API_DECL Image
{
    public:

    /// struct for holding rgb values (in [0,255])
    struct PWIZ_API_DECL Color
    {
        int red;
        int green;
        int blue;

        Color(int r=0, int g=0, int b=0) : red(r), green(g), blue(b) {}
    };

    static Color white() {return Color(255, 255, 255);}
    static Color black() {return Color(0, 0, 0);}

    /// struct for holding pixel coordinates
    struct PWIZ_API_DECL Point
    {
        int x;
        int y;

        Point(int _x=0, int _y=0) :  x(_x), y(_y) {}
    };

    enum PWIZ_API_DECL Align {Left=0x01, CenterX=0x02, Right=0x04, Top=0x08, CenterY=0x10, Bottom=0x20};
    enum PWIZ_API_DECL Size {Tiny, Small, MediumBold, Large, Giant};

    /// create an instance
    /// optional output_width and output_height allows easy scaling to a desired output 
    /// image size without complicating the drawing code (default is to use logical width and height)
    static std::auto_ptr<Image> create(int logical_width, int logical_height, 
                                       int output_width=-1, int output_height=-1); // -1 means use logical

    /// draw pixel
    virtual void pixel(const Point& point, const Color& color) = 0;

    /// draw string 
    virtual void string(const std::string& text, const Point& point, const Color& color,
                        Size size=Large, int align=Left|Top) = 0;

    /// draw string 
    virtual void stringUp(const std::string& text, const Point& point, const Color& color,
                        Size size=Large, int align=Left|Top) = 0;

    /// draw rectangle
    virtual void rectangle(const Point& point1, const Point& point2, const Color& color,
                           bool filled=true) = 0;

    /// draw circle
    virtual void circle(const Point& center, int radius, const Color& color, 
                        bool filled=true) = 0;

    /// draw line 
    virtual void line(const Point& point1, const Point& point2, const Color& color) = 0;

    /// set clipping rectangle 
    virtual void clip(const Point& point1, const Point& point2) = 0;

    /// write png file
    virtual bool writePng(const char* filename) const = 0;

    virtual ~Image(){}
};


inline Image::Point operator+(const Image::Point& a, const Image::Point& b) 
{
    return Image::Point(a.x+b.x, a.y+b.y);
}


inline Image::Point operator-(const Image::Point& a, const Image::Point& b) 
{
    return Image::Point(a.x-b.x, a.y-b.y);
}


} // namespace util
} // namespace pwiz


#endif // _IMAGE_HPP_
