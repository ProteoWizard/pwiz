// $Id: sqlite3pp.h 288 2011-08-09 22:41:43Z chambm $
//
// The MIT License
//
// Copyright (c) 2009 Wongoo Lee (iwongu at gmail dot com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#ifndef SQLITE3PP_H
#define SQLITE3PP_H

#include <string>
#include <stdexcept>
#include <sqlite3.h>
#include <boost/cstdint.hpp>
#include <boost/utility.hpp>
#include <boost/tuple/tuple.hpp>
#include <boost/iterator/iterator_facade.hpp>
#include <boost/function.hpp>
#include <boost/optional.hpp>

namespace sqlite3pp
{
    namespace ext
    {
        class function;
        class aggregate;
    }

    class null_type {};
    extern null_type ignore;

    int enable_shared_cache(bool fenable);

    enum share_flags
    {
        no_mutex = SQLITE_OPEN_NOMUTEX,
        full_mutex = SQLITE_OPEN_FULLMUTEX,
        shared_cache = SQLITE_OPEN_SHAREDCACHE,
        private_cache = SQLITE_OPEN_PRIVATECACHE
    };

    enum open_flags
    {
        read_only = SQLITE_OPEN_READONLY,
        read_write = SQLITE_OPEN_READWRITE,
        create = SQLITE_OPEN_CREATE
    };

    class database : boost::noncopyable
    {
        friend class statement;
        friend class database_error;
        friend class ext::function;
        friend class ext::aggregate;

    public:
        typedef boost::function<int (int)> busy_handler;
        typedef boost::function<int ()> commit_handler;
        typedef boost::function<void ()> rollback_handler;
        typedef boost::function<void (int, char const*, char const*, sqlite3_int64)> update_handler;
        typedef boost::function<int (int, char const*, char const*, char const*, char const*)> authorize_handler;

        explicit database(const std::string& dbname = std::string(),
                          share_flags share = full_mutex,
                          open_flags open = open_flags(create | read_write));
        explicit database(sqlite3* db, bool closeOnDisconnect = true);
        ~database();

        int connect(const std::string& dbname,
                    share_flags share = full_mutex,
                    open_flags open = open_flags(create | read_write));

        int disconnect();

        int attach(const std::string& dbname, const std::string& name);
        int detach(const std::string& name);

        sqlite3* connected() {return db_;}

        /// The sqlite3_load_extension() interface attempts to load an SQLite extension library contained in the file zFile.
        /// If the file cannot be loaded directly, attempts are made to load with various operating - system specific extensions added.
        /// So for example, if "samplelib" cannot be loaded, then names like "samplelib.so" or "samplelib.dylib" or "samplelib.dll" might be tried also.
        void load_extension(const std::string& name);

        int load_from_file(const std::string& dbname);
        int save_to_file(const std::string& dbname);

        sqlite3_int64 last_insert_rowid() const;

        bool has_table(const std::string& table);
        bool has_table(const char* table);

        int error_code() const;
        char const* error_msg() const;

        int execute(const std::string& sql);
        int executef(const std::string&, ...);

        int set_busy_timeout(int ms);

        void set_busy_handler(busy_handler h);
        void set_commit_handler(commit_handler h);
        void set_rollback_handler(rollback_handler h);
        void set_update_handler(update_handler h);
        void set_authorize_handler(authorize_handler h);

    private:
        sqlite3* db_;
        bool closeOnDisconnect_;

        busy_handler bh_;
        commit_handler ch_;
        rollback_handler rh_;
        update_handler uh_;
        authorize_handler ah_;
    };

    class database_error : public std::runtime_error
    {
    public:
        explicit database_error(char const* msg);
        explicit database_error(const std::string& msg);
        explicit database_error(database& db);
    };

    class statement : boost::noncopyable
    {
    public:

        struct blob
        {
            blob(void const* bytes, int size, bool fstatic = true)
                : bytes_(bytes), n_(size), fstatic_(fstatic)
            {}

            private:
            void const* bytes_;
            int n_;
            bool fstatic_;
            friend class statement;
        };

        int prepare(const std::string& stmt);
        int finish();

        int bind(int idx, char value);
        int bind(int idx, int value);
        int bind(int idx, double value);
        int bind(int idx, sqlite3_int64 value);
        int bind(int idx, const std::string& value);
        int bind(int idx, char const* value, bool fstatic = true);
        int bind(int idx, void const* value, int n, bool fstatic = true);
        int bind(int idx, const blob& value);
        int bind(int idx);
        int bind(int idx, null_type);

        int bind(char const* name, char value);
        int bind(char const* name, int value);
        int bind(char const* name, double value);
        int bind(char const* name, sqlite3_int64 value);
        int bind(char const* name, const std::string& value);
        int bind(char const* name, char const* value, bool fstatic = true);
        int bind(char const* name, void const* value, int n, bool fstatic = true);
        int bind(char const* name);
        int bind(char const* name, null_type);

        int step();
        int reset();

      struct bindstream
      {
        bindstream(statement& stmt, int idx);

        template <class T>
        bindstream& operator << (T value)
        {
            int rc = stmt_.bind(idx_, value);
            if (rc != SQLITE_OK)
              throw database_error(stmt_.db_);

            ++idx_;
            return *this;
        }

        private:
        statement& stmt_;
        int idx_;
      };

      bindstream binder(int idx = 1);

    protected:
        explicit statement(database& db, const std::string& stmt = std::string());
        ~statement();

        int prepare_impl(char const* stmt);
        int finish_impl(sqlite3_stmt* stmt);

    protected:
        database& db_;
        sqlite3_stmt* stmt_;
        char const* tail_;
    };

    class command : public statement
    {
    public:

      explicit command(database& db, char const* stmt = 0);


        int execute();
        int execute_all();
    };

    class query : public statement
    {
    public:
        class rows
        {
        public:
            class getstream
            {
            public:
                getstream(rows* rws, int idx);

                template <class T>
                getstream& operator >> (T& value) {
                    value = rws_->get(idx_, T());
                    ++idx_;
                    return *this;
                }

            private:
                rows* rws_;
                int idx_;
            };

            explicit rows(sqlite3_stmt* stmt);

            int data_count() const;
            int column_type(int idx) const;

            int column_bytes(int idx) const;

            template <class T> T get(int idx) const {
                return get(idx, T());
            }

            template <class T1>
            boost::tuple<T1> get_columns(int idx1) const {
                return boost::make_tuple(get(idx1, T1()));
            }

            template <class T1, class T2>
            boost::tuple<T1, T2> get_columns(int idx1, int idx2) const {
                return boost::make_tuple(get(idx1, T1()), get(idx2, T2()));
            }

            template <class T1, class T2, class T3>
            boost::tuple<T1, T2, T3> get_columns(int idx1, int idx2, int idx3) const {
                return boost::make_tuple(get(idx1, T1()), get(idx2, T2()), get(idx3, T3()));
            }

            template <class T1, class T2, class T3, class T4>
            boost::tuple<T1, T2, T3, T4> get_columns(int idx1, int idx2, int idx3, int idx4) const {
                return boost::make_tuple(get(idx1, T1()), get(idx2, T2()), get(idx3, T3()), get(idx4, T4()));
            }

            template <class T1, class T2, class T3, class T4, class T5>
            boost::tuple<T1, T2, T3, T4, T5> get_columns(int idx1, int idx2, int idx3, int idx4, int idx5) const {
                return boost::make_tuple(get(idx1, T1()), get(idx2, T2()), get(idx3, T3()), get(idx4, T4()), get(idx5, T5()));
            }

            template <class T>
            boost::optional<T> get_optional_column(int idx) const {
                return column_type(idx) == SQLITE_NULL ? boost::optional<T>() : get(idx, T());
            }

            getstream getter(int idx = 0);

        private:
            int get(int idx, int) const;
            double get(int idx, double) const;
            sqlite3_int64 get(int idx, sqlite3_int64) const;
            char const* get(int idx, char const*) const;
            std::string get(int idx, std::string) const;
            void const* get(int idx, void const*) const;
            null_type get(int idx, null_type) const;

            template <class T>
            boost::optional<T> get(int idx, boost::optional<T>) const {
                return get_optional_column<T>(idx);
            }

        private:
            sqlite3_stmt* stmt_;
        };

        class query_iterator
            : public boost::iterator_facade<query_iterator, rows, boost::single_pass_traversal_tag, rows>
        {
        public:
            query_iterator();
            explicit query_iterator(query* cmd);

        private:
            friend class boost::iterator_core_access;

            void increment();
            bool equal(query_iterator const& other) const;

            rows dereference() const;

            query* cmd_;
            int rc_;
        };

        explicit query(database& db, char const* stmt = 0);

        int column_count() const;

        char const* column_name(int idx) const;
        char const* column_decltype(int idx) const;

        typedef query_iterator iterator;
        typedef query_iterator const_iterator;

        iterator begin();
        iterator end();
    };

    class transaction : boost::noncopyable
    {
    public:
        explicit transaction(database& db, bool fcommit = false, bool freserve = false);
        ~transaction();

        int commit();
        int rollback();

    private:
        database* db_;
        bool fcommit_;
    };

} // namespace sqlite3pp

#endif
