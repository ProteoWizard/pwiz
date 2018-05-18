// $Id: sqlite3pp.cpp 352 2011-12-28 16:59:20Z chambm $
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

#include "sqlite3pp.h"

namespace sqlite3pp
{

    null_type ignore;

    namespace
    {
        int busy_handler_impl(void* p, int cnt)
        {
            database::busy_handler* h = static_cast<database::busy_handler*>(p);
            return (*h)(cnt);
        }

        int commit_hook_impl(void* p)
        {
            database::commit_handler* h = static_cast<database::commit_handler*>(p);
            return (*h)();
        }

        void rollback_hook_impl(void* p)
        {
            database::rollback_handler* h = static_cast<database::rollback_handler*>(p);
            (*h)();
        }

        void update_hook_impl(void* p, int opcode, char const* dbname, char const* tablename, sqlite3_int64 rowid)
        {
            database::update_handler* h = static_cast<database::update_handler*>(p);
            (*h)(opcode, dbname, tablename, rowid);
        }

        int authorizer_impl(void* p, int evcode, char const* p1, char const* p2, char const* dbname, char const* tvname)
        {
            database::authorize_handler* h = static_cast<database::authorize_handler*>(p);
            return (*h)(evcode, p1, p2, dbname, tvname);
        }

        /*
        ** This function is used to load the contents of a database file on disk 
        ** into the "main" database of open database connection pInMemory, or
        ** to save the current contents of the database opened by pInMemory into
        ** a database file on disk. pInMemory is probably an in-memory database, 
        ** but this function will also work fine if it is not.
        **
        ** Parameter zFilename points to a nul-terminated string containing the
        ** name of the database file on disk to load from or save to. If parameter
        ** isSave is non-zero, then the contents of the file zFilename are 
        ** overwritten with the contents of the database opened by pInMemory. If
        ** parameter isSave is zero, then the contents of the database opened by
        ** pInMemory are replaced by data loaded from the file zFilename.
        **
        ** If the operation is successful, SQLITE_OK is returned. Otherwise, if
        ** an error occurs, an SQLite error code is returned.
        */
        int loadOrSaveDb(sqlite3 *pInMemory, const char *zFilename, int isSave)
        {
            int rc;                   /* Function return code */
            sqlite3 *pFile;           /* Database connection opened on zFilename */
            sqlite3_backup *pBackup;  /* Backup object used to copy data */
            sqlite3 *pTo;             /* Database to copy to (pFile or pInMemory) */
            sqlite3 *pFrom;           /* Database to copy from (pFile or pInMemory) */

            /* Open the database file identified by zFilename. Exit early if this fails
            ** for any reason. */
            rc = sqlite3_open(zFilename, &pFile);

            if( rc==SQLITE_OK )
            {
                /* Disable journalling and synchronous writes. */
                sqlite3_exec(pFile, "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF", 0, 0, 0);

                /* If this is a 'load' operation (isSave==0), then data is copied
                ** from the database file just opened to database pInMemory. 
                ** Otherwise, if this is a 'save' operation (isSave==1), then data
                ** is copied from pInMemory to pFile.  Set the variables pFrom and
                ** pTo accordingly. */
                pFrom = (isSave ? pInMemory : pFile);
                pTo   = (isSave ? pFile     : pInMemory);

                /* Set up the backup procedure to copy from the "main" database of 
                ** connection pFile to the main database of connection pInMemory.
                ** If something goes wrong, pBackup will be set to NULL and an error
                ** code and  message left in connection pTo.
                **
                ** If the backup object is successfully created, call backup_step()
                ** to copy data from pFile to pInMemory. Then call backup_finish()
                ** to release resources associated with the pBackup object.  If an
                ** error occurred, then  an error code and message will be left in
                ** connection pTo. If no error occurred, then the error code belonging
                ** to pTo is set to SQLITE_OK.
                */
                pBackup = sqlite3_backup_init(pTo, "main", pFrom, "main");
                if( pBackup )
                {
                    (void)sqlite3_backup_step(pBackup, -1);
                    (void)sqlite3_backup_finish(pBackup);
                }
                rc = sqlite3_errcode(pTo);
            }

            /* Close the database connection opened on database file zFilename
            ** and return the result of this function. */
            (void)sqlite3_close(pFile);
            return rc;
        }

    } // namespace

    int enable_shared_cache(bool fenable)
    {
        return sqlite3_enable_shared_cache(fenable);
    }

    database::database(const std::string& dbname, share_flags share, open_flags open) : db_(0), closeOnDisconnect_(true)
    {
        if (!dbname.empty()) {
            int rc = connect(dbname, share, open);
            if (rc != SQLITE_OK)
                throw database_error("can't connect database");
        }
    }

    database::database(sqlite3* db, bool closeOnDisconnect) 
       : db_(db), closeOnDisconnect_(closeOnDisconnect)
    {}

    database::~database()
    {
        disconnect();
    }

    int database::connect(const std::string& dbname, share_flags share, open_flags open)
    {
        disconnect();

        return sqlite3_open_v2(dbname.c_str(), &db_, share | open, NULL);
    }

    int database::disconnect()
    {
        int rc = SQLITE_OK;
        if (db_) {
            if (closeOnDisconnect_)
                rc = sqlite3_close(db_);
            db_ = 0;
        }

        return rc;
    }

    int database::attach(const std::string& dbname, const std::string& name)
    {
        return executef("ATTACH '%s' AS '%s'", dbname.c_str(), name.c_str());
    }

    int database::detach(const std::string& name)
    {
        return executef("DETACH '%s'", name.c_str());
    }

    void database::load_extension(const std::string& name)
    {
        sqlite3_enable_load_extension(db_, 1);
        char* errorBuf = NULL;
        int rc = sqlite3_load_extension(db_, name.c_str(), NULL, &errorBuf);
        if (rc != SQLITE_OK)
        {
            std::string error;
            if (errorBuf)
            {
                error = errorBuf;
                sqlite3_free(errorBuf);
            }
            throw database_error("loading extension \"" + name + "\": " + error);
        }
        sqlite3_enable_load_extension(db_, 0);
    }

    int database::load_from_file(const std::string& dbname)
    {
        return loadOrSaveDb(db_, dbname.c_str(), 0);
    }

    int database::save_to_file(const std::string& dbname)
    {
        return loadOrSaveDb(db_, dbname.c_str(), 1);
    }

    void database::set_busy_handler(busy_handler h)
    {
        bh_ = h;
        sqlite3_busy_handler(db_, bh_ ? busy_handler_impl : 0, &bh_);
    }

    void database::set_commit_handler(commit_handler h)
    {
        ch_ = h;
        sqlite3_commit_hook(db_, ch_ ? commit_hook_impl : 0, &ch_);
    }

    void database::set_rollback_handler(rollback_handler h)
    {
        rh_ = h;
        sqlite3_rollback_hook(db_, rh_ ? rollback_hook_impl : 0, &rh_);
    }

    void database::set_update_handler(update_handler h)
    {
        uh_ = h;
        sqlite3_update_hook(db_, uh_ ? update_hook_impl : 0, &uh_);
    }

    void database::set_authorize_handler(authorize_handler h)
    {
        ah_ = h;
        sqlite3_set_authorizer(db_, ah_ ? authorizer_impl : 0, &ah_);
    }

    sqlite3_int64 database::last_insert_rowid() const
    {
        return sqlite3_last_insert_rowid(db_);
    }

    bool database::has_table(const std::string& table)
    {
        return has_table(table.c_str());
    }

    bool database::has_table(const char* table)
    {
        query q(*this, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = ?");
        q.bind(1, table);
        return q.begin()->get<sqlite3_int64>(0) > 0;
    }

    int database::error_code() const
    {
        return sqlite3_errcode(db_);
    }

    char const* database::error_msg() const
    {
        return sqlite3_errmsg(db_);
    }

    int database::execute(const std::string& sql)
    {
        char* errorBuf = NULL;
        int rc = sqlite3_exec(db_, sql.c_str(), 0, 0, &errorBuf);
        if (rc != SQLITE_OK)
        {
            std::string error;
            if (errorBuf)
            {
                error = errorBuf;
                sqlite3_free(errorBuf);
            }
            throw database_error("executing \"" + sql + "\": " + error);
        }
        return rc;
    }

    int database::executef(const std::string& sql, ...)
    {
        const char* csql = sql.c_str();
        va_list ap;
        va_start(ap, csql);
        char* msql = sqlite3_vmprintf(csql, ap);
        va_end(ap);

        return execute(msql);
    }

    int database::set_busy_timeout(int ms)
    {
        return sqlite3_busy_timeout(db_, ms);
    }


    statement::statement(database& db, const std::string& stmt) : db_(db), stmt_(0), tail_(0)
    {
        if (!stmt.empty()) {
            int rc = prepare(stmt);
            if (rc != SQLITE_OK)
                throw database_error(db_);
        }
    }

    statement::~statement()
    {
        int rc = finish();
        if (rc != SQLITE_OK)
            throw database_error(db_);
    }

    int statement::prepare(const std::string& stmt)
    {
        int rc = finish();
        if (rc != SQLITE_OK)
            return rc;

        return prepare_impl(stmt.c_str());
    }

    int statement::prepare_impl(char const* stmt)
    {
        return sqlite3_prepare(db_.db_, stmt, strlen(stmt), &stmt_, &tail_);
    }

    int statement::finish()
    {
        int rc = SQLITE_OK;
        if (stmt_) {
            rc = finish_impl(stmt_);
            stmt_ = 0;
        }
        tail_ = 0;

        return rc;
    }

    int statement::finish_impl(sqlite3_stmt* stmt)
    {
        return sqlite3_finalize(stmt);
    }

    int statement::step()
    {
        return sqlite3_step(stmt_);
    }

    int statement::reset()
    {
        return sqlite3_reset(stmt_);
    }

    int statement::bind(int idx, char value)
    {
        return sqlite3_bind_text(stmt_, idx, &value, 1, SQLITE_TRANSIENT);
    }

    int statement::bind(int idx, int value)
    {
        return sqlite3_bind_int(stmt_, idx, value);
    }

    int statement::bind(int idx, double value)
    {
        return sqlite3_bind_double(stmt_, idx, value);
    }

    int statement::bind(int idx, sqlite3_int64 value)
    {
        return sqlite3_bind_int64(stmt_, idx, value);
    }

    int statement::bind(int idx, const std::string& value)
    {
        return sqlite3_bind_text(stmt_, idx, value.c_str(), value.length(), SQLITE_TRANSIENT);
    }

    int statement::bind(int idx, char const* value, bool fstatic)
    {
        return sqlite3_bind_text(stmt_, idx, value, strlen(value), fstatic ? SQLITE_STATIC : SQLITE_TRANSIENT);
    }

    int statement::bind(int idx, void const* value, int n, bool fstatic)
    {
        return sqlite3_bind_blob(stmt_, idx, value, n, fstatic ? SQLITE_STATIC : SQLITE_TRANSIENT);
    }

    int statement::bind(int idx, const blob& value)
    {
        return bind(idx, value.bytes_, value.n_, value.fstatic_);
    }

    int statement::bind(int idx)
    {
        return sqlite3_bind_null(stmt_, idx);
    }

    int statement::bind(int idx, null_type)
    {
        return bind(idx);
    }

    int statement::bind(char const* name, char value)
    {
        int idx = sqlite3_bind_parameter_index(stmt_, name);
        return bind(idx, value);
    }

    int statement::bind(char const* name, int value)
    {
        int idx = sqlite3_bind_parameter_index(stmt_, name);
        return bind(idx, value);
    }

    int statement::bind(char const* name, double value)
    {
        int idx = sqlite3_bind_parameter_index(stmt_, name);
        return bind(idx, value);
    }

    int statement::bind(char const* name, sqlite3_int64 value)
    {
        int idx = sqlite3_bind_parameter_index(stmt_, name);
        return bind(idx, value);
    }

    int statement::bind(char const* name, const std::string& value)
    {
        int idx = sqlite3_bind_parameter_index(stmt_, name);
        return bind(idx, value);
    }

    int statement::bind(char const* name, char const* value, bool fstatic)
    {
        int idx = sqlite3_bind_parameter_index(stmt_, name);
        return bind(idx, value, fstatic);
    }

    int statement::bind(char const* name, void const* value, int n, bool fstatic)
    {
        int idx = sqlite3_bind_parameter_index(stmt_, name);
        return bind(idx, value, n, fstatic);
    }

    int statement::bind(char const* name)
    {
        int idx = sqlite3_bind_parameter_index(stmt_, name);
        return bind(idx);
    }

    int statement::bind(char const* name, null_type)
    {
        return bind(name);
    }


    statement::bindstream::bindstream(statement& stmt, int idx) : stmt_(stmt), idx_(idx)
    {
    }

    statement::bindstream statement::binder(int idx)
    {
        return bindstream(*this, idx);
    }

    command::command(database& db, char const* stmt) : statement(db, stmt)
    {
    }

    int command::execute()
    {
        int rc = step();
        if (rc == SQLITE_DONE) rc = SQLITE_OK;

        return rc;
    }

    int command::execute_all()
    {
        int rc = execute();
        if (rc != SQLITE_OK) return rc;

        char const* sql = tail_;

        while (strlen(sql) > 0) { // sqlite3_complete() is broken.
            sqlite3_stmt* old_stmt = stmt_;

            if ((rc = prepare_impl(sql)) != SQLITE_OK) return rc;

            if ((rc = sqlite3_transfer_bindings(old_stmt, stmt_)) != SQLITE_OK) return rc;

            finish_impl(old_stmt);

            if ((rc = execute()) != SQLITE_OK) return rc;

            sql = tail_;
        }

        return rc;
    }


    query::rows::getstream::getstream(rows* rws, int idx) : rws_(rws), idx_(idx)
    {
    }

    query::rows::rows(sqlite3_stmt* stmt) : stmt_(stmt)
    {
    }

    int query::rows::data_count() const
    {
        return sqlite3_data_count(stmt_);
    }

    int query::rows::column_type(int idx) const
    {
        return sqlite3_column_type(stmt_, idx);
    }

    int query::rows::column_bytes(int idx) const
    {
        return sqlite3_column_bytes(stmt_, idx);
    }

    int query::rows::get(int idx, int) const
    {
        return sqlite3_column_int(stmt_, idx);
    }

    double query::rows::get(int idx, double) const
    {
        return sqlite3_column_double(stmt_, idx);
    }

    sqlite3_int64 query::rows::get(int idx, sqlite3_int64) const
    {
        return sqlite3_column_int64(stmt_, idx);
    }

    char const* query::rows::get(int idx, char const*) const
    {
        return reinterpret_cast<char const*>(sqlite3_column_text(stmt_, idx));
    }

    std::string query::rows::get(int idx, std::string) const
    {
        const char* str = get(idx, (char const*)0);
        return str == NULL ? std::string() : std::string(str);
    }

    void const* query::rows::get(int idx, void const*) const
    {
        return sqlite3_column_blob(stmt_, idx);
    }

    null_type query::rows::get(int idx, null_type) const
    {
        return ignore;
    }
    query::rows::getstream query::rows::getter(int idx)
    {
        return getstream(this, idx);
    }


    query::query_iterator::query_iterator() : cmd_(0)
    {
        rc_ = SQLITE_DONE;
    }

    query::query_iterator::query_iterator(query* cmd) : cmd_(cmd) {
        rc_ = cmd_->step();
        if (rc_ != SQLITE_ROW && rc_ != SQLITE_DONE)
            throw database_error(cmd_->db_);
    }

    void query::query_iterator::increment()
    {
        rc_ = cmd_->step();
        if (rc_ != SQLITE_ROW && rc_ != SQLITE_DONE)
            throw database_error(cmd_->db_);
    }

    bool query::query_iterator::equal(query_iterator const& other) const
    {
        return rc_ == other.rc_;
    }

    query::rows query::query_iterator::dereference() const
    {
        return rows(cmd_->stmt_);
    }

    query::query(database& db, char const* stmt) : statement(db, stmt)
    {
    }

    int query::column_count() const
    {
        return sqlite3_column_count(stmt_);
    }

    char const* query::column_name(int idx) const
    {
        return sqlite3_column_name(stmt_, idx);
    }

    char const* query::column_decltype(int idx) const
    {
        return sqlite3_column_decltype(stmt_, idx);
    }


    query::iterator query::begin()
    {
        return query_iterator(this);
    }

    query::iterator query::end()
    {
        return query_iterator();
    }


    transaction::transaction(database& db, bool fcommit, bool freserve) : db_(&db), fcommit_(fcommit)
    {
        db_->execute(freserve ? "BEGIN IMMEDIATE" : "BEGIN");
    }

    transaction::~transaction()
    {
        if (db_) {
            int rc = db_->execute(fcommit_ ? "COMMIT" : "ROLLBACK");
            if (rc != SQLITE_OK)
                throw database_error(*db_);
        }
    }

    int transaction::commit()
    {
        database* db = db_;
        db_ = 0;
        int rc = db->execute("COMMIT");
        return rc;
    }

    int transaction::rollback()
    {
        database* db = db_;
        db_ = 0;
        int rc = db->execute("ROLLBACK");
        return rc;
    }


    database_error::database_error(char const* msg) : std::runtime_error(msg)
    {
    }

    database_error::database_error(const std::string& msg) : std::runtime_error(msg)
    {
    }

    database_error::database_error(database& db) : std::runtime_error(sqlite3_errmsg(db.db_))
    {
    }


}
