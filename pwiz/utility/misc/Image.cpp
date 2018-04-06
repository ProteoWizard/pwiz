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


#define PWIZ_SOURCE

#include "Image.hpp"
#include "gd.h"
#include "gdfontt.h"
#include "gdfonts.h"
#include "gdfontmb.h"
#include "gdfontl.h"
#include "gdfontg.h"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace util {


namespace {

class ScopedData
{
    public:
    ScopedData() : size_(0), data_(0) {}
    int size() const {return size_;}
    void* data() {return data_;}
    const void* data() const {return data_;}
    ~ScopedData() {gdFree(data_);}

    protected:
    int size_;
    void* data_;
};


class ScopedDataPng : public ScopedData
{
    public:
    ScopedDataPng(gdImagePtr im)
    {
        data_ = gdImagePngPtr(im, &size_);
    }
};


bool writeScopedData(const char* filename, const ScopedData& sd)
{
    ofstream os(filename, ios::binary);

    if (!os)
    {
        cerr << "[writeScopedData()] Error opening file " << filename << endl;
        return false;
    }

    if (!sd.data() || !sd.size())
    {
        cerr << "[writeScopedData()] No data to write.\n";
        return false;
    }

    //cout << "Writing " << sd.size() << " bytes to file " << filename << endl;
    os.write((const char*)sd.data(), sd.size());
    return true;
}

} // namespace


class ImageImpl : public Image
{
    private:
    inline int SCALEX(int x) 
    {
        return scaled_?(int)(scalex_*x):x;
    }

    inline int SCALEY(int y) 
    {
        return scaled_?(int)(scaley_*y):y;
    }

    inline int SCALER(int r) 
    {
        return scaled_?(int)(scaler_*r):r;
    }

    public:
    ImageImpl(int logical_width, int logical_height, int output_width, int output_height)
    :   logical_width_(logical_width), logical_height_(logical_height),
        output_width_((output_width>0)?output_width:logical_width),
        output_height_((output_height>0)?output_height:logical_height)
    {
        scalex_ = (double)output_width_ / (double)logical_width_;
        scaley_ = (double)output_height_ / (double)logical_height_;
        scaler_ = min(scalex_,scaley_); // for scaling circles
        scaled_ = (scalex_!=1.0)||(scaley_!=1.0);
        im_ = gdImageCreateTrueColor(output_width_, output_height_);
    }

    void pixel(const Point& point, const Color& color)
    {
        if (scaled_) // might actually need more than one pixel
        {
            // gd rectangle is inclusive, so find scaled pixel just outside and back off
            Point p1(SCALEX(point.x),SCALEY(point.y));
            Point p2(SCALEX(point.x+1),SCALEY(point.y+1)); // far edge of rectangle of 1x1 logical pixels
            if (p2.x > p1.x)
                p2.x--;
            if (p2.y > p1.y)
                p2.y--;
            gdImageFilledRectangle(im_,p1.x,p1.y,p2.x,p2.y,color2gd(color));
        }
        else
            gdImageSetPixel(im_, point.x, point.y, color2gd(color));
    }

    void string(const std::string& text, const Point& point, const Color& color, Size size, int align)
    {
        // copy text into gd-friendly (unsigned char) buffer

        vector<unsigned char> buffer;
        copy(text.begin(), text.end(), back_inserter(buffer));
        buffer.push_back('\0');

        // choose font 

        gdFontPtr font;
        switch (size)
        {
            case Tiny: font = gdFontGetTiny(); break;
            case Small: font = gdFontGetSmall(); break;
            case MediumBold: font = gdFontGetMediumBold(); break;
            case Large: font = gdFontGetLarge(); break;
            case Giant: font = gdFontGetGiant(); break;
            default: throw runtime_error("[ImageImpl::string()] This isn't happening.");
        }

        // calculate position

        Point position(SCALEX(point.x),SCALEY(point.y));
        int length = (int)text.size() * font->w;
        int height = font->h;
        
        if (align & CenterX) position.x -= length/2;
        else if (align & Right) position.x -= length;
            
        if (align & CenterY) position.y -= height/2;
        else if (align & Bottom) position.y -= height;

        // draw the string
            
        gdImageString(im_, font, position.x, position.y, &buffer[0], color2gd(color)); 
    }

    void stringUp(const std::string& text, const Point& point, const Color& color, Size size, int align)
    {
        // copy text into gd-friendly (unsigned char) buffer

        vector<unsigned char> buffer;
        copy(text.begin(), text.end(), back_inserter(buffer));
        buffer.push_back('\0');

        // choose font 

        gdFontPtr font;
        switch (size)
        {
            case Tiny: font = gdFontGetTiny(); break;
            case Small: font = gdFontGetSmall(); break;
            case MediumBold: font = gdFontGetMediumBold(); break;
            case Large: font = gdFontGetLarge(); break;
            case Giant: font = gdFontGetGiant(); break;
            default: throw runtime_error("[ImageImpl::string()] This isn't happening.");
        }

        // calculate position

        Point position(SCALEX(point.x),SCALEY(point.y));
        int length = (int)text.size() * font->w;
        int height = font->h;
        
        if (align & CenterX) position.x -= height/2;
        else if (align & Right) position.x -= height;
            
        if (align & CenterY) position.y -= length/2;
        else if (align & Bottom) position.y -= length;

        // draw the string vertically
            
        gdImageStringUp(im_, font, position.x, position.y, &buffer[0], color2gd(color)); 
    }

    void rectangle(const Point& point1, const Point& point2, const Color& color, bool filled)
    {
        if (filled)
            gdImageFilledRectangle(im_, SCALEX(point1.x), SCALEY(point1.y), SCALEX(point2.x), SCALEY(point2.y), color2gd(color));
        else    
            gdImageRectangle(im_, SCALEX(point1.x), SCALEY(point1.y), SCALEX(point2.x), SCALEY(point2.y), color2gd(color));
    }

    void circle(const Point& center, int radius, const Color& color, bool filled)
    {
        if (filled)
            gdImageFilledEllipse(im_, SCALEX(center.x), SCALEY(center.y), SCALER(radius*2), SCALER(radius*2), color2gd(color)); 
        else    
            gdImageArc(im_, SCALEX(center.x), SCALEY(center.y), SCALER(radius*2), SCALER(radius*2), 0, 360, color2gd(color)); 
    }

    void line(const Point& point1, const Point& point2, const Color& color)
    {
        gdImageLine(im_, SCALEX(point1.x), SCALEY(point1.y), SCALEX(point2.x), SCALEY(point2.y), color2gd(color));
    }

    void clip(const Point& point1, const Point& point2)
    {
        gdImageSetClip(im_, SCALEX(point1.x), SCALEY(point1.y), SCALEX(point2.x), SCALEY(point2.y));
    }

    virtual bool writePng(const char* filename) const
    {
        ScopedDataPng imageData(im_);
        return writeScopedData(filename, imageData);
    }

    ~ImageImpl()
    {
        gdImageDestroy(im_);
    }

    private:
    int logical_width_;
    int logical_height_;
    int output_width_;  // output bitmap size can differ from logical size
    int output_height_;
    double scalex_; 
    double scaley_; 
    double scaler_; // for scaling circles, min(scalex, scaley)
    bool scaled_;   // true iff scale_x!=1.0 or scale_y!=1.0
    gdImagePtr im_;

    int color2gd(const Color& color) {return gdTrueColor(color.red, color.green, color.blue);} 
};


PWIZ_API_DECL auto_ptr<Image> Image::create(int logical_width, int logical_height, int output_width, int output_height)
{
    return auto_ptr<Image>(new ImageImpl(logical_width, logical_height, output_width, output_height));
}


} // namespace util
} // namespace pwiz
