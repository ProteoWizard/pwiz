//
// Image.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _IMAGE_HPP_
#define _IMAGE_HPP_


#include <memory>
#include <string>


namespace pwiz {
namespace util {


/// wrapper class for using 'gd' graphics library
class Image
{
    public:

    /// struct for holding rgb values (in [0,255])
    struct Color
    {
        int red;
        int green;
        int blue;

        Color(int r=0, int g=0, int b=0) : red(r), green(g), blue(b) {}
    };

    static Color white() {return Color(255, 255, 255);}
    static Color black() {return Color(0, 0, 0);}

    /// struct for holding pixel coordinates
    struct Point
    {
        int x;
        int y;

        Point(int _x=0, int _y=0) :  x(_x), y(_y) {}
    };

    enum Align {Left=0x01, CenterX=0x02, Right=0x04, Top=0x08, CenterY=0x10, Bottom=0x20};
    enum Size {Tiny, Small, MediumBold, Large, Giant};

    /// create an instance
    static std::auto_ptr<Image> create(int width, int height);

    /// draw pixel
    virtual void pixel(const Point& point, const Color& color) = 0;

    /// draw string 
    virtual void string(const std::string& text, const Point& point, const Color& color,
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

    /// write jpeg file 
    virtual bool writeJpg(const char* filename) const = 0;

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
