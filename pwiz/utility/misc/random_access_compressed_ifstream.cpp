// $Id$
//
// This is just an istream which chooses its streambuf implementation
// based on whether the file is gzipped or not.  Could be extended
// to work with other compression formats, too.
//
// What makes it interesting compared to the classic gzstream implementation
// is the ability to perform seeks in a reasonably efficient manner.  In the
// event that a seek is requested (other than a rewind, or tellg()) the file 
// is decompressed once and snapshots of the compression state are made 
// every 1MB or so.  Further seeks are then quite efficient since they don't
// have to begin at the head of the file.
//
// It also features threaded readahead with adaptive buffering - it will read 
// increasingly larger chunks of the raw file as it perceives a sequential read 
// in progress, and will launch a thread to grab the next probable chunk of the 
// file while the previous chunk is being parsed.  This is especially helpful for
// files being read across a slow network connection.
// If lots of seeking is going on, and the buffer size proves excessive, the read 
// buffer size decreases.  This is also true for the non-compressed case, so this 
// is generally useful for file read speed optimization.
//
// draws heavily on example code from the zlib distro, so
// for conditions of distribution and use, see copyright notice in zlib.h
//
// Copyright (C) Insilicos LLC 2008,2011 ALl Rights Reserved.
// For conditions of distribution and use, see copyright notice in zlib.h
//
// based on:
/* gzio.c -- IO on .gz files
* Copyright (C) 1995-2005 Jean-loup Gailly.
* For conditions of distribution and use, see copyright notice in zlib.h
*/

// efficient random access stuff based on
/* zran.c -- example of zlib/gzip stream indexing and random access
* Copyright (C) 2005 Mark Adler
* For conditions of distribution and use, see copyright notice in zlib.h
Version 1.0  29 May 2005  Mark Adler */


#define PWIZ_SOURCE


#include <cstdio>

#include "zlib.h"

#include "random_access_compressed_ifstream.hpp"

#if defined(_MSC_VER) || defined(__MINGW32__)  // MSVC or MinGW
#include <winsock2.h>
#include <sys/types.h>
#include <fcntl.h>
#include <io.h>
#define lseek64 _lseeki64
#else
#include <stdint.h>
#include <netinet/in.h>
#endif

#include <sys/stat.h>
#include <vector>
#include <cassert>
#include <cerrno>
#include <iostream>
#include <cstring>
#include "pwiz/utility/misc/Std.hpp"
#include <boost/thread.hpp>  

#ifndef O_LARGEFILE
#define O_LARGEFILE 0
#endif

namespace pwiz {
namespace util {

#ifndef Z_BUFSIZE
#  ifdef MAXSEG_64K
#    define Z_BUFSIZE 4096 /* minimize memory usage for 16-bit DOS */
#  else
#    define Z_BUFSIZE 16384
#  endif
#endif

#define DECOMPRESS_BUFSIZE 16384
#define DEFAULT_CHUNKY_BUFSIZE 32768
#define MIN_CHUNKY_BUFSIZE 16384
#define MAX_CHUNKY_BUFSIZE DEFAULT_CHUNKY_BUFSIZE*1024

#define ALLOC(size) malloc(size)
#define TRYFREE(p) {if (p) free(p); p=NULL;}

static int const gz_magic[2] = {0x1f, 0x8b}; /* gzip magic header */

/* gzip flag byte */
#define ASCII_FLAG   0x01 /* bit 0 set: file probably ascii text */
#define HEAD_CRC     0x02 /* bit 1 set: header CRC present */
#define EXTRA_FIELD  0x04 /* bit 2 set: extra field present */
#define ORIG_NAME    0x08 /* bit 3 set: original file name present */
#define COMMENT      0x10 /* bit 4 set: file comment present */
#define RESERVED     0xE0 /* bits 5..7: reserved */


#define SPAN 1048576L       /* desired distance between access points */
#define WINSIZE 32768U      /* sliding window size */
#define CHUNK 16384         /* file input buffer size */
/* access point entry */
class synchpoint {
public:
	random_access_compressed_ifstream_off_t out;          /* corresponding offset in uncompressed data */
	random_access_compressed_ifstream_off_t in;           /* offset in input file of first full byte */
	z_stream *state; // stream state
};
//
// here's where the real customization of the stream happens
//
class chunky_streambuf; // forward ref
class random_access_compressed_streambuf : public std::streambuf {
   friend class random_access_compressed_ifstream; // so we can modify some behaviors
public:
	random_access_compressed_streambuf(chunky_streambuf *rdbuf); // ctor
	virtual ~random_access_compressed_streambuf();
	bool is_open() const;
	chunky_streambuf *close(); // close file and hand back readbuf
protected:
    virtual pos_type seekoff(off_type off,
		std::ios_base::seekdir way,
        std::ios_base::openmode which = std::ios_base::in); // we don't do out
    virtual pos_type seekpos(pos_type pos,
		std::ios_base::openmode which = std::ios_base::in); // we don't do out
	virtual int_type underflow(); // repopulate the input buffer
private:
	std::streampos my_seekg(std::streampos offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode); // streampos not the same as streamoff, esp in win32
	z_stream stream;
	int      z_err;   /* error code for last stream operation */
	int      z_eof;   /* set if end of input file */
    std::istream *infile;   /* raw .gz file we're reading */
	Byte     *inbuf;  /* input buffer */
	Byte     *outbuf; /* output buffer */
	uLong    crc;     /* crc32 of uncompressed data */
	random_access_compressed_ifstream_off_t  start;   /* start of compressed data in file (header skipped) */
	random_access_compressed_ifstream_off_t  uncompressedLength; /* total length of uncompressed file */
	std::streampos  last_seek_pos; /* last requested seek position (uncompressed) */
	std::streampos  outbuf_headpos; /* filepos for head of outbuf */
	std::streamoff	outbuf_len; /* length of outbuf last time we populated it */
	std::vector<synchpoint *> index; // index for random access
	/* Add an entry to the access point list. */
   synchpoint *addIndexEntry(random_access_compressed_ifstream_off_t in, random_access_compressed_ifstream_off_t out);

	// gzip stuff
	int do_flush(int flush);
	int get_byte();
	int  get_buf(int len);
	void  check_header();
	int    destroy();
	uLong  getLong();
	int build_index();
	void update_istream_ptrs(std::streampos new_headpos,int new_buflen,int new_posoffset=0) {
		outbuf_headpos = new_headpos; // note the decompressed filepos corresponding to buf head
		outbuf_len = new_buflen; // how many bytes in the buffer are consumable?
		// declare first, next, last for istream use
		setg((char *)outbuf,(char *)outbuf+new_posoffset,(char *)outbuf + new_buflen);
	}
	std::streampos get_next_read_pos() {
		return outbuf_headpos+boost::iostreams::offset_to_position(gptr()-(char *)outbuf);
	}
	bool pos_is_in_outbuf(std::streampos pos) {
		return ((outbuf_headpos <= pos) && 
			(pos < (outbuf_headpos + outbuf_len)));
	}
};

class chunky_streambuf : public std::streambuf {
public:
	chunky_streambuf();
	virtual ~chunky_streambuf();
    bool open(const char *path);
	bool is_open() const;
	void close();

    virtual pos_type seekoff(off_type off,
		std::ios_base::seekdir way,
        std::ios_base::openmode which = std::ios_base::in); // we don't do out
    virtual pos_type seekpos(pos_type pos,
		std::ios_base::openmode which = std::ios_base::in); // we don't do out
	virtual int_type underflow(); // repopulate the input buffer

	std::streampos my_seekg(boost::iostreams::stream_offset offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode); // streampos not the same as streamoff, esp in win32
	int		 handle;   /* handle of file we're reading */
	char     *path;   /* path name for debugging only */
    size_t   desired_readbuf_len; /* dynamic sizing of disk reads */
	boost::iostreams::stream_offset last_seek_pos; /* last requested seek position */
#define N_INBUFS 3 // lookback, current, readahead
    struct {
	    Byte     *readbuf; /* file read buffer */
        size_t   maxbufsize; /* size of read buffer */
	boost::iostreams::stream_offset readbuf_head_filepos; /* filepos for head of readbuf */
	size_t	readbuf_len; /* length of readbuf last time we populated it */
    size_t  chars_used; /* number of chars actually read out of buffer */
    } inbuf[N_INBUFS]; // read out of one while the other populates in another thread
    int current_inbuf;
    int readerThread_inbuf;
    boost::thread *readerThread;
    bool threadComplete; // for tuning readahead size
    boost::iostreams::stream_offset flen; /* unknown until first SEEK_END */

    inline boost::iostreams::stream_offset &readbuf_head_filepos() {
        return inbuf[current_inbuf].readbuf_head_filepos;
    }
    inline boost::iostreams::stream_offset &readbuf_head_filepos(int n) {
        return inbuf[n].readbuf_head_filepos;
    }
    inline Byte* &readbuf() { /* file read buffer */
	    return inbuf[current_inbuf].readbuf; /* file read buffer */
    }
    inline Byte* &readbuf(int n) { /* file read buffer */
	    return inbuf[n].readbuf; /* file read buffer */
    }
    inline size_t &maxbufsize() { /* size of read buffer */
        return inbuf[current_inbuf].maxbufsize; /* size of read buffer */
    }
	inline size_t &readbuf_len() { /* length of readbuf last time we populated it */
	    return inbuf[current_inbuf].readbuf_len; /* length of readbuf last time we populated it */
    }
	inline size_t &readbuf_len(int n) { /* length of readbuf last time we populated it */
	    return inbuf[n].readbuf_len; /* length of readbuf last time we populated it */
    }
    inline size_t &chars_used() { /* number of chars actually read out of buffer */
        return inbuf[current_inbuf].chars_used; /* number of chars actually read out of buffer */
    }

    void set_inbuf(int bufnum,boost::iostreams::stream_offset headpos,
           size_t readlen, 
           boost::iostreams::stream_offset readpos) {
        current_inbuf = bufnum;
        update_istream_ptrs(headpos,readlen,readpos-headpos);
    }

	void update_istream_ptrs(boost::iostreams::stream_offset new_headpos,int new_buflen,int new_posoffset=0) {
		readbuf_head_filepos() = new_headpos; // note the filepos corresponding to buf head
		readbuf_len() = new_buflen; // how many bytes in the buffer?
        if (new_buflen) { // not just a reset to provoke underflow
            chars_used() = this->gptr()-this->eback(); // note usage of old buffer
        }
		// declare first, next, last for istream use
		setg((char *)readbuf(),(char *)readbuf()+new_posoffset,(char *)readbuf() + new_buflen);
	}
	boost::iostreams::stream_offset get_next_read_pos() {
		return readbuf_head_filepos()+(gptr()-(char *)readbuf());
	}
	bool pos_is_in_readbuf(boost::iostreams::stream_offset pos) {
		return ((readbuf_head_filepos() <= pos) && 
			(pos < (readbuf_head_filepos() + readbuf_len())));
	}
	int find_readbuf_for_pos(boost::iostreams::stream_offset pos) {
        for (int n=N_INBUFS;n--;) {
		  if (((!readerThread) || (n!=readerThread_inbuf)) && // don't mess with an active read
              (readbuf_head_filepos(n) <= pos) && 
              (pos < (readbuf_head_filepos(n) + readbuf_len(n)))) {
              return n;
          }
        }
        // didn't find it - maybe we're loading it?
        if (readerThread) {
            int n = readerThread_inbuf;
            this->readerThread->join(); // wait for completion
            delete this->readerThread;
            this->readerThread = NULL;
            if ((readbuf_head_filepos(n) <= pos) && 
                (pos < (readbuf_head_filepos(n) + readbuf_len(n)))) {
                return n;
            }
        }
        return -1;
	}

};




#define gzio_raw_readerror(s) (s->infile->bad())

// default ctor
PWIZ_API_DECL
random_access_compressed_ifstream::random_access_compressed_ifstream() :
std::istream(new chunky_streambuf()) {
	compressionType = NONE;
}

// constructor
PWIZ_API_DECL
random_access_compressed_ifstream::random_access_compressed_ifstream(const char *path) :
std::istream(new chunky_streambuf()) 
{
	open(path);
}

PWIZ_API_DECL
void random_access_compressed_ifstream::open(const char *path) {
	chunky_streambuf *fb = (chunky_streambuf *)rdbuf();
	fb->open(path);
	bool gzipped = false;
    compressionType = NONE;
	if (fb->is_open()) {
	   // check for gzip magic header
		gzipped = ((fb->sbumpc() == gz_magic[0]) && (fb->sbumpc() == gz_magic[1])); 
		fb->pubseekpos(0); // rewind
		if (gzipped) { // replace streambuf with gzip handler (handing it current rdbuf)
			rdbuf( new random_access_compressed_streambuf( (chunky_streambuf *)rdbuf() ));
		compressionType = GZIP;
	}
	} else {
		this->setstate(std::ios::failbit); // could not open, set the fail flag
	}
}

PWIZ_API_DECL
bool random_access_compressed_ifstream::is_open() const { // for ease of use as ifstream replacement
	if (NONE == compressionType) {
		return ((chunky_streambuf *)rdbuf())->is_open();
	} else {
		return ((random_access_compressed_streambuf *)rdbuf())->is_open();
	}
}

PWIZ_API_DECL
void random_access_compressed_ifstream::close() {
	if (rdbuf()) {
	    if (NONE != compressionType) {
            // retrieve rdbuf from gzip handler
		    rdbuf(((random_access_compressed_streambuf *)rdbuf())->close());
        }
        ((chunky_streambuf *)rdbuf())->close();
			// in case object gets reused
			compressionType = NONE;
		}
}

PWIZ_API_DECL
random_access_compressed_ifstream::~random_access_compressed_ifstream()
{
    close();
    delete rdbuf(NULL);
}

random_access_compressed_streambuf::random_access_compressed_streambuf(chunky_streambuf *rawbuf) {
    int err;

	this->stream.zalloc = (alloc_func)0;
	this->stream.zfree = (free_func)0;
	this->stream.opaque = (voidpf)0;
	this->stream.next_in = this->inbuf = Z_NULL;
	this->stream.next_out = this->outbuf = Z_NULL;
	this->stream.avail_in = this->stream.avail_out = 0;
	this->last_seek_pos = -1;
	this->infile = new istream(rawbuf); // dynamic disk buffer size
	this->z_err = Z_OK;
	this->z_eof = 0;
	this->crc = crc32(0L, Z_NULL, 0);

	if ((this->infile->fail())) {
		this->destroy();
		return;
	}

	this->stream.next_in  = this->inbuf = (Byte*)ALLOC(Z_BUFSIZE);
	this->outbuf = (Byte*)ALLOC(DECOMPRESS_BUFSIZE);

	err = inflateInit2(&(this->stream), -MAX_WBITS);
	/* windowBits is passed < 0 to tell that there is no zlib header.
	* Note that in this case inflate *requires* an extra "dummy" byte
	* after the compressed stream in order to complete decompression and
	* return Z_STREAM_END. Here the gzip CRC32 ensures that 4 bytes are
	* present after the compressed stream.
	*/
	if (err != Z_OK || this->inbuf == Z_NULL) {
		this->destroy();
		return;
	}
	this->stream.avail_out = DECOMPRESS_BUFSIZE;

	errno = 0;

	this->check_header(); /* skip the .gz header */
   this->infile->clear(); // clear eof flag for short files
	this->start = this->infile->tellg() - (std::streamoff)(this->stream.avail_in);
	this->update_istream_ptrs(0,0); // we're pointed at head of file, but no read yet
}


bool random_access_compressed_streambuf::is_open() const { // for ifstream-ish-ness
	return true; // only ever exist when file is open
}

chunky_streambuf * random_access_compressed_streambuf::close() { // for ifstream-ish-ness
    chunky_streambuf *rawbuf = (chunky_streambuf *) this->infile->rdbuf(); // preserve
    this->infile->rdbuf(NULL);
	this->destroy();
    return rawbuf; // hand it back to parent
}

random_access_compressed_streambuf::~random_access_compressed_streambuf() {
	this->destroy();
}



/* ===========================================================================
Read a byte from a random_access_gzstream; update next_in and avail_in. Return EOF
for end of file.
IN assertion: the stream s has been sucessfully opened for reading.
*/
int random_access_compressed_streambuf::get_byte()
{
	if (this->z_eof) {
		return EOF;
	}
	if (this->stream.avail_in == 0) {
		errno = 0;
		this->infile->read((char *)this->inbuf, Z_BUFSIZE);
		this->stream.avail_in = this->infile->gcount(); // how many did we read?
		if (this->stream.avail_in <= 0) {
			this->z_eof = 1;
			if (gzio_raw_readerror(this)) {
				this->z_err = Z_ERRNO;
			} else if (this->infile->eof()) {
				this->infile->clear(); // clear eof flag
			}
			return EOF;
		}
		this->stream.next_in = this->inbuf;
	}
	this->stream.avail_in--;
	return *(this->stream.next_in)++;
}

/* ===========================================================================
Check the gzip header of a random_access_gzstream opened for reading. set this->err
to Z_DATA_ERROR if the magic header is not present, or present but the rest of the header
is incorrect.
IN assertion: the stream s has already been created sucessfully;
this->stream.avail_in is zero for the first time, but may be non-zero
for concatenated .gz files.
*/
void random_access_compressed_streambuf::check_header()
{
	int method; /* method byte */
	int flags;  /* flags byte */
	uInt len;
	int c;

	/* Assure two bytes in the buffer so we can peek ahead -- handle case
	where first byte of header is at the end of the buffer after the last
	gzip segment */
	len = this->stream.avail_in;
	if (len < 2) {
		if (len) {
			this->inbuf[0] = this->stream.next_in[0];
		}
		errno = 0;
		this->infile->read((char *)(this->inbuf + len), Z_BUFSIZE >> len);
		len = this->infile->gcount();
		if (len <= 0 && gzio_raw_readerror(this)) {
			this->z_err = Z_ERRNO;
		}
		this->stream.avail_in += len;
		this->stream.next_in = this->inbuf;
		if (this->stream.avail_in < 2) {
			if (this->stream.avail_in) {
				this->z_err = Z_DATA_ERROR;
			}
			return;
		}
	}

	/* Peek ahead to check the gzip magic header */
	if (this->stream.next_in[0] != gz_magic[0] ||
		this->stream.next_in[1] != gz_magic[1]) {
			this->z_err = Z_DATA_ERROR;
			return;
	}
	this->stream.avail_in -= 2;
	this->stream.next_in += 2;

	/* Check the rest of the gzip header */
	method = this->get_byte();
	flags = this->get_byte();
	if (method != Z_DEFLATED || (flags & RESERVED) != 0) {
		this->z_err = Z_DATA_ERROR;
		return;
	}

	/* Discard time, xflags and OS code: */
	for (len = 0; len < 6; len++) {
		this->get_byte();
	}

	if ((flags & EXTRA_FIELD) != 0) { /* skip the extra field */
		len  =  (uInt)this->get_byte();
		len += ((uInt)this->get_byte())<<8;
		/* len is garbage if EOF but the loop below will quit anyway */
		while (len-- != 0 && this->get_byte() != EOF) ;
	}
	if ((flags & ORIG_NAME) != 0) { /* skip the original file name */
		while ((c = this->get_byte()) != 0 && c != EOF) ;
	}
	if ((flags & COMMENT) != 0) {   /* skip the .gz file comment */
		while ((c = this->get_byte()) != 0 && c != EOF) ;
	}
	if ((flags & HEAD_CRC) != 0) {  /* skip the header crc */
		for (len = 0; len < 2; len++) {
			this->get_byte();
		}
	}
	this->z_err = this->z_eof ? Z_DATA_ERROR : Z_OK;
}

/* ===========================================================================
* Cleanup then free the given random_access_gzstream. Return a zlib error code.
Try freeing in the reverse order of allocations.
*/
int random_access_compressed_streambuf::destroy ()
{
	int err = Z_OK;

	bool bClosedOK=true;
	if (this->stream.state != NULL) {
		err = inflateEnd(&(this->stream));
		if (this->infile) {
			delete this->infile;
		}
		this->infile = NULL;
	}
	// clean up the seek index list if any
	for (int i=(int)this->index.size();i--;) {
		inflateEnd(this->index[i]->state);
		delete this->index[i]->state;
		delete this->index[i];
	}
	this->index.clear(); // set length 0
	if (!bClosedOK) {
#ifdef ESPIPE
		if (errno != ESPIPE) /* fclose is broken for pipes in HP/UX */
#endif
			err = Z_ERRNO;
	}
	if (this->z_err < 0) {
		err = this->z_err;
	}

	TRYFREE(this->inbuf);
	TRYFREE(this->outbuf);
	return err;
}

//
// this gets called each time ifstream uses up its input buffer
//
random_access_compressed_streambuf::int_type random_access_compressed_streambuf::underflow() {
	int nread = 0;
	int len = DECOMPRESS_BUFSIZE; // we'll try to read the next full chunk
	if (this->last_seek_pos >= 0) { // we had a seek request

		/* here's where we tear out the inefficient default seek behavior and replace
		with the stuff from zran.c - bpratt */

		/* Use the index to read len bytes from offset into buf, return bytes read or
		negative for error (Z_DATA_ERROR or Z_MEM_ERROR).  If data is requested past
		the end of the uncompressed data, then extract() will return a value less
		than len, indicating how much as actually read into buf.  This function
		should not return a data error unless the file was modified since the index
		was generated.  extract() may also return Z_ERRNO if there is an error on
		reading or seeking the input file. */

		int skip, ret;
		unsigned char *discard = new unsigned char[WINSIZE];
		random_access_compressed_ifstream_off_t offset = 
			boost::iostreams::position_to_offset(this->last_seek_pos); // last requested absolute uncompress filepos
		std::streampos  next_read_pos = this->last_seek_pos; // will be new outbuf head pos
		this->last_seek_pos = -1; // satisfied it

		// first locate the index entry which will get us at or just before target
		size_t ind = this->index.size();
		while (--ind && this->index[ind]->out > offset);
		// and prepare to decompress
		synchpoint *synch = this->index[ind];
		inflateEnd(&this->stream); // tidy up old ptrs
		inflateCopy(&this->stream,synch->state);
		z_stream &strm = this->stream;
      assert(strm.total_in == synch->in-this->start);
		this->infile->clear(); // clear eof flag if any
		this->infile->seekg(boost::iostreams::offset_to_position(synch->in));

		/* skip uncompressed bytes until offset reached */
		offset -= synch->out;  // now offset is the number of uncompressed bytes we need to skip
		strm.avail_in = 0; // inflateCopy doesn't retain the input buffer
		skip = 1;                               /* while skipping to offset */
		do {
			/* define where to put uncompressed data, and how much */
			if (offset == 0 && skip) {          /* at offset now */
				strm.avail_out = len;
				strm.next_out = (Bytef *)this->outbuf;
				skip = 0;                       /* only do this once */
			}
			if (offset > WINSIZE) {             /* skip WINSIZE bytes */
				strm.avail_out = WINSIZE;
				strm.next_out = discard;
				offset -= WINSIZE;
			}
			else if (offset != 0) {             /* last skip */
				strm.avail_out = (unsigned)offset;
				strm.next_out = discard;
				offset = 0;
			}

			/* uncompress until avail_out filled, or end of stream */
			do {
				if (strm.avail_in == 0) {
					this->infile->read((char *)this->inbuf, CHUNK);
					strm.avail_in = this->infile->gcount();
					if (gzio_raw_readerror(this)) {
						ret = Z_ERRNO;
						goto perform_seek_ret;
					} else if (this->infile->eof()) {
						this->infile->clear(); // clear eof flag
					}
					if (strm.avail_in == 0) {
						ret = Z_DATA_ERROR;
						goto perform_seek_ret;
					}
					strm.next_in = this->inbuf;
				}
				ret = inflate(&strm, Z_NO_FLUSH);       /* normal inflate */
				if (ret == Z_NEED_DICT)
					ret = Z_DATA_ERROR;
				if (ret == Z_MEM_ERROR || ret == Z_DATA_ERROR)
					goto perform_seek_ret;
				if (ret == Z_STREAM_END) {
					break;
				}
			} while (strm.avail_out != 0);

			/* if reach end of stream, then don't keep trying to get more */
			if (ret == Z_STREAM_END) {
				// reached EOF, clear fail bit
				this->infile->clear();
				break;
			}

			/* do until offset reached and requested data read, or stream ends */
		} while (skip);
		nread = skip ? 0 : len - strm.avail_out;
		update_istream_ptrs(next_read_pos,nread);
		/* return error if any */
perform_seek_ret:
		delete []discard;

	} else {
      std::streampos buftailpos = get_next_read_pos(); // buf tail will be buf head unless we seek

		Bytef *start = (Bytef*)this->outbuf; /* starting point for crc computation */
		Byte  *next_out; /* == stream.next_out but not forced far (for MSDOS) */

		next_out = (Byte*)start;
		this->stream.next_out = next_out;
		this->stream.avail_out = len;

		while (this->stream.avail_out != 0) {

			if (this->stream.avail_in == 0 && !this->z_eof) {

				errno = 0;
				this->infile->read((char *)this->inbuf, Z_BUFSIZE);
				this->stream.avail_in = this->infile->gcount();
				if (this->stream.avail_in <= 0) {
					this->z_eof = 1;
					if (gzio_raw_readerror(this)) {
						this->z_err = Z_ERRNO;
						break;
					} else if (this->infile->eof()) {
						this->infile->clear(); // clear eof flag
					}
				}
				this->stream.next_in = this->inbuf;
			}
			this->z_err = inflate(&(this->stream), Z_NO_FLUSH);

			if (this->z_err != Z_OK || this->z_eof) {
				break;
			}
		}
		this->crc = crc32(this->crc, start, (uInt)(this->stream.next_out - start));

		if (len == (int)this->stream.avail_out &&
			(this->z_err == Z_DATA_ERROR || this->z_err == Z_ERRNO)) {
				return traits_type::eof();;
		}
		nread = (int)(len - this->stream.avail_out); // how many bytes actually read?
		update_istream_ptrs(buftailpos,nread); // update outbuf head position, length
	}
	return (nread>0)?outbuf[0]:traits_type::eof();
}

std::streampos random_access_compressed_streambuf::seekpos(std::streampos pos,std::ios_base::openmode Mode) {
	return my_seekg(pos,std::ios_base::beg,Mode);
}

std::streampos random_access_compressed_streambuf::seekoff(std::streamoff offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode) {
	return my_seekg(boost::iostreams::offset_to_position(offset),whence,Mode);
}

std::streampos random_access_compressed_streambuf::my_seekg(std::streampos offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode) {
	if (this->z_err == Z_ERRNO || this->z_err == Z_DATA_ERROR) {
			return -1;
	 }
	// watch out for a simple rewind or tellg
	if (0==offset) {
		if (whence == std::ios_base::cur) {
			return get_next_read_pos(); // nothing to do
		}
		if ((whence == std::ios_base::beg) && // just a rewind
			!this->index.size()) { // no index yet
			// do we already have this decompressed?
			if (pos_is_in_outbuf(offset) || // already in buffer
				(offset==outbuf_headpos)) // or buffer is not yet loaded, let underflow() do it
			{
				update_istream_ptrs(outbuf_headpos,outbuf_len);
			} else  { // rewind without provoking index build
				this->stream.avail_in = 0;
				this->stream.avail_out = 0;
				this->stream.total_in = 0;
				this->stream.total_out = 0;
				this->stream.next_in = this->inbuf;
				this->crc = crc32(0L, Z_NULL, 0);
				(void)inflateReset(&this->stream);
				this->last_seek_pos = -1; // no need to seek
				this->infile->seekg(boost::iostreams::offset_to_position(this->start));
				update_istream_ptrs(0,0); // blow the cache
			}
			return 0; 
      } // end just a rewind and no index yet
   } // end if offset==0

	this->z_err = Z_OK;
	this->z_eof = 0;
	if (!this->index.size()) { // first seek - build index
		this->build_index();
      update_istream_ptrs(0,0); // blow the cache
	}
	/* find where in stream to start */
	/* compute absolute position */
	std::streampos pos = offset;
	if (whence == std::ios_base::cur) {
		pos += this->get_next_read_pos();
	} else if (whence == std::ios_base::end) {
		pos += boost::iostreams::offset_to_position(this->uncompressedLength);
	}

	// do we already have this decompressed?
	if (pos_is_in_outbuf(pos)) {
		update_istream_ptrs(outbuf_headpos,outbuf_len,pos-outbuf_headpos);
      this->last_seek_pos = -1; // no need to actually seek
	} else {
  	    // just note the request for now, actually seek at read time
		this->last_seek_pos = pos;
      update_istream_ptrs(outbuf_headpos,0); // blow the cache
	}
   return pos;
}

/* ===========================================================================
Reads a long in LSB order from the given random_access_gzstream. Sets z_err in case
of error.
*/
uLong random_access_compressed_streambuf::getLong ()
{
	uLong x = (uLong)this->get_byte();
	int c;

	x += ((uLong)this->get_byte())<<8;
	x += ((uLong)this->get_byte())<<16;
	c = this->get_byte();
	if (c == EOF) {
		this->z_err = Z_DATA_ERROR;
	}
	x += ((uLong)c)<<24;
	return x;
}


//
// from here we're stealing from zran.c
//

/* Add an entry to the access point list. */
synchpoint *random_access_compressed_streambuf::addIndexEntry(random_access_compressed_ifstream_off_t in, random_access_compressed_ifstream_off_t out)
{
	/* fill in entry and increment how many we have */
	synchpoint *next = new synchpoint();
	if (next) {
		next->in = in;
		next->out = out;
		next->state = new z_stream;
		inflateCopy(next->state,&this->stream);
		this->index.push_back(next);
	}
	return next;
}

/* Make one entire pass through the compressed stream and build an index, with
access points about every span bytes of uncompressed output -- span is
chosen to balance the speed of random access against the memory requirements
of the list, about 32K bytes per access point.  Note that data after the end
of the first zlib or gzip stream in the file is ignored.  build_index()
returns 0 on success, Z_MEM_ERROR for out of memory, Z_DATA_ERROR for an error in 
the input file, or Z_ERRNO for a file read error.  
On success, *built points to the resulting index. */
int random_access_compressed_streambuf::build_index()
{
	int ret;
	random_access_compressed_ifstream_off_t span=SPAN;
	random_access_compressed_ifstream_off_t totin, totout;        /* our own total counters to avoid 4GB limit */
	random_access_compressed_ifstream_off_t last;                 /* totout value of last access point */
	unsigned char *input = new unsigned char[CHUNK];
	unsigned char *window = new unsigned char[WINSIZE];
	z_stream &strm = this->stream;

	/* initialize inflate */
   this->stream.avail_in = 0;
   this->stream.avail_out = 0;
   this->stream.total_in = 0;
   this->stream.total_out = 0;
   this->stream.next_in = this->inbuf;
   this->crc = crc32(0L, Z_NULL, 0);
   ret = inflateReset(&strm); 
   this->infile->clear(); // clear stale eof bit if any
   this->infile->seekg((std::streamoff)this->start); // rewind
	if (ret != Z_OK) {
		goto build_index_error;
	}

	/* inflate the input, maintain a sliding window, and build an index -- this
	also validates the integrity of the compressed data using the check
	information at the end of the gzip or zlib stream */
   totout = last = 0;
   totin = this->start;
   this->addIndexEntry(totin,totout); // note head of file

	do {
		/* get some compressed data from input file */
		this->infile->read((char *)input, CHUNK);
		strm.avail_in = this->infile->gcount();
		if (gzio_raw_readerror(this)) {
			ret = Z_ERRNO;
			goto build_index_error;
		}
		if (strm.avail_in == 0) {
			ret = Z_DATA_ERROR;
			goto build_index_error;
		}
		strm.next_in = input;

		/* process all of that, or until end of stream */
		do {
			/* reset sliding window if necessary */
			if (strm.avail_out == 0) {
				strm.avail_out = WINSIZE;
				strm.next_out = window;
			}

			/* inflate until out of input, output, or at end of block --
			update the total input and output counters */
			totin += strm.avail_in; // note input filepos at start of inflate
			totout += strm.avail_out; // note uncompressed filepos prior to inflate
			ret = inflate(&strm, Z_BLOCK);      /* return at end of block */
			totin -= strm.avail_in; // if we use this as a synchpoint we'll have to repopulate the input buffer
			totout -= strm.avail_out;
			if (ret == Z_NEED_DICT)
				ret = Z_DATA_ERROR;
			if (ret == Z_MEM_ERROR || ret == Z_DATA_ERROR)
				goto build_index_error;
			if (ret == Z_STREAM_END) {
				// reached end successfully
				this->infile->clear(); // clear the fail bit
				break;
			}

         /* add an index entry every 'span' bytes   */
         if (( totout - last) > span) {
            if (!this->addIndexEntry(totin,totout)) {
							ret = Z_MEM_ERROR;
							goto build_index_error;
					}
					last = totout;
			}
		} while (strm.avail_in != 0);
	} while (ret != Z_STREAM_END);

	/* return index (release unused entries in list) */
	this->uncompressedLength = totout;

	/* return error */
build_index_error:
	delete[] window;
	delete[] input;
	return ret;
}

//
// stuff for an ifstream with a really big buffer and smarter seek
//
//
chunky_streambuf::chunky_streambuf() {
    this->handle = -1;
    this->flen = 0;
	this->path = NULL;
    this->readerThread = NULL;
    this->desired_readbuf_len = DEFAULT_CHUNKY_BUFSIZE;
    this->last_seek_pos = -1;
    for (this->current_inbuf=N_INBUFS;this->current_inbuf--;) {
        this->chars_used() = 0;
        this->readbuf() = NULL;
    }
    this->current_inbuf = 0;
}

bool chunky_streambuf::open(const char *path) {
	if (!path) {
		return false;
	}

    this->handle = ::open(path,O_RDONLY|O_BINARY|O_LARGEFILE);
    this->flen = 0;
    // dynamic read buffer sizing - start small
    this->desired_readbuf_len = DEFAULT_CHUNKY_BUFSIZE;
    for (this->current_inbuf=N_INBUFS;this->current_inbuf--;) {
        this->chars_used() = 0;
        this->readbuf_len() = 0;
        this->readbuf_head_filepos() = -1;

    // allocate a big read buffer (we'll treat it as smaller if that makes
    // sense for how the file is being read)
        for (this->maxbufsize() = MAX_CHUNKY_BUFSIZE; 
             this->maxbufsize() > DEFAULT_CHUNKY_BUFSIZE;
             this->maxbufsize() /= 2) {
	        this->readbuf() = (Byte*)ALLOC(this->maxbufsize());
            if (this->readbuf()) {
            break;
        }
    }
    }
    this->current_inbuf = 0;
	this->path = strdup(path);
	if ((this->path == NULL) || (-1 == this->handle)) {
		return false;
	}

	if ((this->readbuf(0) == NULL)||(this->readbuf(1) == NULL)) {
		return false;
	}

	this->update_istream_ptrs(0,0); // we're pointed at head of file, but no read yet
    return true;
}


bool chunky_streambuf::is_open() const { // for ifstream-ish-ness
	return (-1 != this->handle);
}

void chunky_streambuf::close() { // for ifstream-ish-ness
    if (this->handle != -1) {
        if (this->readerThread) { // were we working on a readahead?
            this->readerThread->join(); // wait for completion
            delete this->readerThread;
            this->readerThread = NULL;
        }
        ::close(this->handle); 
	    this->handle = -1;
    }
}

chunky_streambuf::~chunky_streambuf() {
	int err = Z_OK;
    this->close();
    for (this->current_inbuf=N_INBUFS;this->current_inbuf--;) {
	    TRYFREE(this->readbuf());
    }
	TRYFREE(this->path);
}

// code for thread that reads the next chunk while we parse the current one
void readAhead(chunky_streambuf *csb, int inbuf, size_t readlen) {
    csb->readbuf_len(inbuf) = ::read(csb->handle,(char *)csb->readbuf(inbuf), readlen);
    csb->threadComplete = true;
}

//
// this gets called each time ifstream uses up its input buffer
//
chunky_streambuf::int_type chunky_streambuf::underflow() {
	size_t nread = 0;
	boost::iostreams::stream_offset next_read_pos;
	if (this->last_seek_pos >= 0) { // we had a seek request
        int bufnum = find_readbuf_for_pos(this->last_seek_pos); // will wait for readahead thread
        if (bufnum>=0) { // found it already loaded
            set_inbuf(bufnum,readbuf_head_filepos(bufnum),readbuf_len(bufnum),last_seek_pos);
            size_t offset = last_seek_pos-readbuf_head_filepos();
            last_seek_pos = -1; // satisfied it
            return readbuf()[offset];
        } else { // actually need to read
        // did we read a lot more than we needed to?
            if (chars_used() && this->desired_readbuf_len > 2*chars_used()) {
                size_t newsize = MIN_CHUNKY_BUFSIZE*(1+(chars_used()/MIN_CHUNKY_BUFSIZE)); // ugh - why isn't min() portable
            this->desired_readbuf_len = (newsize < MIN_CHUNKY_BUFSIZE) ? MIN_CHUNKY_BUFSIZE : newsize;
                if (this->desired_readbuf_len > this->maxbufsize()) {
                    this->desired_readbuf_len = this->maxbufsize();
                }
        }            
		next_read_pos = this->last_seek_pos; // will be new outbuf head pos
		this->last_seek_pos = -1; // satisfied it
		lseek64(this->handle,next_read_pos,SEEK_SET);
            int next_inbuf = (current_inbuf+1)%N_INBUFS;
            nread = ::read(this->handle,(char *)this->readbuf(next_inbuf), this->desired_readbuf_len);
            set_inbuf(next_inbuf,next_read_pos,nread,next_read_pos); // rotate buffers
            // cout << "s " << next_read_pos << " " << nread << " " << next_read_pos << "\n";
        }
    } else {
        // we hit end of buffer - perhaps we should have read more?
        next_read_pos = get_next_read_pos(); 
        int last_inbuf = current_inbuf;
        bool threadActive = this->readerThread != NULL;
        bool readWait= threadActive && !this->threadComplete; // readahead done yet?
        int bufnum = find_readbuf_for_pos(next_read_pos); // will wait for readahead thread
        bool readahead_success = false;
        if (bufnum >= 0) {
            set_inbuf(bufnum,readbuf_head_filepos(bufnum),readbuf_len(bufnum),next_read_pos);
            readahead_success = true;
            //if (threadActive && (bufnum == readerThread_inbuf)) cout << "t " << readbuf_head_filepos() << " " << readbuf_len() << " " << next_read_pos << "\n";
        }

        if (readahead_success && 
            (readbuf_head_filepos()==
            readbuf_head_filepos(last_inbuf)+readbuf_len(last_inbuf))&&
            (find_readbuf_for_pos(readbuf_head_filepos(last_inbuf)-1)>-1)) {
            // we read all 3 sequentially - go big
            this->desired_readbuf_len = this->maxbufsize();
        } else if (readWait) { // we'd like the read and parse threads about the same duration
            size_t newsize = this->desired_readbuf_len - this->desired_readbuf_len/8; 
            this->desired_readbuf_len = (newsize < MIN_CHUNKY_BUFSIZE) ? MIN_CHUNKY_BUFSIZE : newsize;
        } else { // see if we can read a bigger chunk
            size_t newsize = this->desired_readbuf_len *2;   
            this->desired_readbuf_len = (newsize > this->maxbufsize()) ? this->maxbufsize() : newsize;
        }

        if (!readahead_success) { // need an immediate blocking read
            int next_inbuf = (current_inbuf+1)%N_INBUFS; // rotate buffers
            nread = ::read(this->handle,(char *)this->readbuf(next_inbuf), this->desired_readbuf_len);
            set_inbuf(next_inbuf,next_read_pos,nread,next_read_pos); 
            // cout << "i " << readbuf_head_filepos() << " " << readbuf_len() << " " << next_read_pos << "\n";
        }
        nread = readbuf_len();
        if (nread && !readerThread) { // at eof? then don't try to read any farther
            // read the next chunk asynchronously in hopes we'll want that next
            readerThread_inbuf=(current_inbuf+1)%N_INBUFS;
            readbuf_head_filepos(readerThread_inbuf) = readbuf_head_filepos()+nread;
            threadComplete = false;
            readerThread = new boost::thread(readAhead, this,readerThread_inbuf,this->desired_readbuf_len); 
        }
    }
    if (!nread) {
       update_istream_ptrs(next_read_pos,0);
       return traits_type::eof();
    } else {
	   return readbuf()[0];
    }
}

std::streampos chunky_streambuf::seekpos(std::streampos pos,std::ios_base::openmode Mode) {
	return my_seekg(boost::iostreams::position_to_offset(pos),std::ios_base::beg,Mode);
}

std::streampos chunky_streambuf::seekoff(std::streamoff offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode) {
	return my_seekg(offset,whence,Mode);
}

std::streampos chunky_streambuf::my_seekg(boost::iostreams::stream_offset offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode) {
	if (-1 == this->handle) {
			return -1;
	 }
	// watch out for a simple rewind or tellg
	if (0==offset) {
		if (whence == std::ios_base::cur) {
			return get_next_read_pos(); // nothing to do
		}
		if (whence == std::ios_base::beg)  { // just a rewind
			// do we already have this loaded?
            int n = find_readbuf_for_pos(0);
            if (n >= 0) {
                set_inbuf(n,0,readbuf_len(n),0); 
			} else  { // rewind 
                lseek64(this->handle,0,SEEK_SET);
				update_istream_ptrs(0,0); // blow the cache
			}
			last_seek_pos = -1; // no need to seek
			return 0; 
      } // end just a rewind
   } // end if offset==0

	/* find where in stream to start */
	/* compute absolute position */
	boost::iostreams::stream_offset pos = offset;
	if (whence == std::ios_base::cur) {
		pos += this->get_next_read_pos();
	} else if (whence == std::ios_base::end) {
        if (!this->flen) { // length is unknown
            this->flen = lseek64(this->handle,0,SEEK_END);
        }
        pos = this->flen+pos;
	}

	// do we already have this loaded?
    int n = find_readbuf_for_pos(pos);
	if (n>=0) {
		set_inbuf(n,readbuf_head_filepos(n),readbuf_len(n),pos);
        this->last_seek_pos = -1; // no need to actually seek
	} else {
		// just note the request for now, actually seek at read time
		this->last_seek_pos = pos;
		update_istream_ptrs(0,0); // blow the cache
	}
   return pos;
}

} // namespace util
} // namespace pwiz 

