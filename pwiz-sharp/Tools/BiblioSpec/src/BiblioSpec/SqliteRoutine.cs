using System.Data;
using System.Data.SQLite;
using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// C# port of cpp <c>BiblioSpec::SqliteRoutine</c> (SqliteRoutine.cpp / SqliteRoutine.h).
/// Thin convenience layer over <see cref="System.Data.SQLite"/> providing the same
/// validate / table-exists / table-rows / exec / escape-apostrophes utilities BlibMaker /
/// PsmFile / BlibFilter / BuildParser call into, plus a small set of idiomatic helpers
/// (Open / Scalar / Query / transaction control / Vacuum) that the managed callers will
/// want once BlibMaker is ported.
///
/// <para>The cpp original wraps the C SQLite API directly (sqlite3_open, sqlite3_exec,
/// sqlite3_get_table). We forward those onto the managed <c>SQLiteConnection</c> /
/// <c>SQLiteCommand</c> / <c>SQLiteDataReader</c> surface — the parity contract is the
/// resulting database content, not the wire shape of the SQL string, so parameterized
/// commands replace cpp's sprintf-into-buffer pattern.</para>
/// </summary>
public static class SqliteRoutine
{
    /// <summary>
    /// Opens (or, with <paramref name="readOnly"/> false, opens-or-creates) a SQLite
    /// database file. Mirrors cpp <c>sqlite3_open</c> usage across BlibMaker / PsmFile.
    /// </summary>
    /// <param name="path">Filesystem path to the SQLite database. Must be non-empty.</param>
    /// <param name="readOnly">When true, opens with <c>ReadOnly=true</c> so concurrent
    /// readers don't take a write lock. Match what cpp does for <c>LibReader</c>-style
    /// callers (read-only) versus <c>BlibBuilder</c> (read-write).</param>
    /// <returns>An open <see cref="SQLiteConnection"/>. Caller owns the lifetime —
    /// wrap in a <c>using</c>.</returns>
    public static SQLiteConnection Open(string path, bool readOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var csb = new SQLiteConnectionStringBuilder
        {
            DataSource = path,
            ReadOnly = readOnly,
            // Pooling=false matches UimfData.cs: pooled handles can outlive a `using`
            // block and surprise callers that delete the file afterward.
            Pooling = false,
        };
        var conn = new SQLiteConnection(csb.ConnectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// cpp <c>VALIDATE_DATABASE(const char*)</c> at SqliteRoutine.cpp:33-48. Returns
    /// <c>true</c> when the path opens cleanly as a SQLite database. The cpp version
    /// prints to stdout on failure; we just swallow the exception and return false —
    /// callers that want diagnostics should call <see cref="Open"/> directly.
    /// </summary>
    public static bool ValidateDatabase(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var conn = Open(path, readOnly: true);
            return true;
        }
        catch (SQLiteException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// cpp <c>TABLE_EXISTS(const char*, sqlite3*)</c> at SqliteRoutine.cpp:50-73.
    /// Queries <c>sqlite_master</c> for a row with <c>name=tableName</c> and
    /// <c>type='table'</c>.
    /// </summary>
    public static bool TableExists(SQLiteConnection conn, string tableName)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE name=@name AND type='table'";
        cmd.Parameters.AddWithValue("@name", tableName);
        var result = cmd.ExecuteScalar();
        return ToInt64(result) == 1;
    }

    /// <summary>
    /// cpp <c>TABLE_ROWS(const char*, sqlite3*)</c> at SqliteRoutine.cpp:75-95. Returns the
    /// row count of <paramref name="tableName"/>. The cpp version interpolates the table
    /// name into the SQL via <c>sprintf("select count(*) from '%s'", ...)</c>; we do the
    /// same shape but quote-escape so a malicious / weirdly-named table doesn't break the
    /// statement. (SQLite doesn't allow binding identifiers, so table names have to be
    /// inlined either way.)
    /// </summary>
    public static int TableRows(SQLiteConnection conn, string tableName)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM " + QuoteIdentifier(tableName);
        var result = cmd.ExecuteScalar();
        return checked((int)ToInt64(result));
    }

    /// <summary>
    /// cpp <c>SQL_STMT(const char*, sqlite3*)</c> at SqliteRoutine.cpp:97-108. Runs a
    /// non-query statement (DDL, INSERT, BEGIN/COMMIT, PRAGMA, etc.) and returns the
    /// rows-affected count. cpp swallows errors and prints them — we throw, because the
    /// managed callers will want to surface failures up to <c>BlibException</c>.
    /// </summary>
    public static int Execute(SQLiteConnection conn, string sql)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Parameterized variant of <see cref="Execute(SQLiteConnection, string)"/>. cpp
    /// callers build SQL with sprintf and then run it through <c>SQL_STMT</c>; on the
    /// managed side, prefer this overload so the values are bound rather than
    /// concatenated.
    /// </summary>
    /// <param name="conn">Open connection.</param>
    /// <param name="sql">SQL with <c>@name</c>-style parameters.</param>
    /// <param name="parameters">Name → value pairs to bind. Use a <c>byte[]</c> value to
    /// bind a BLOB; null values become <c>DBNull</c>.</param>
    public static int Execute(SQLiteConnection conn, string sql,
        IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var kv in parameters)
            cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Single-value query helper. Wraps <c>SQLiteCommand.ExecuteScalar</c> with conversion
    /// to <typeparamref name="T"/>. Used in cpp via <c>sqlite3_get_table</c> followed by
    /// <c>atoi(result[1])</c>; this is the typed managed equivalent.
    /// </summary>
    /// <typeparam name="T">Target type. <c>int</c>, <c>long</c>, <c>double</c>,
    /// <c>string</c>, and <c>bool</c> are converted via <c>IConvertible</c>.</typeparam>
    /// <returns>Default(T) when the query returns no rows or <c>NULL</c>.</returns>
    public static T? Scalar<T>(SQLiteConnection conn, string sql)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return ConvertScalar<T>(result);
    }

    /// <summary>
    /// Parameterized variant of <see cref="Scalar{T}(SQLiteConnection, string)"/>.
    /// </summary>
    public static T? Scalar<T>(SQLiteConnection conn, string sql,
        IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var kv in parameters)
            cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
        var result = cmd.ExecuteScalar();
        return ConvertScalar<T>(result);
    }

    /// <summary>
    /// Streams rows from a query. Mirrors cpp's <c>sqlite3_get_table</c> + callback
    /// pattern, but in an idiomatic managed shape: the caller iterates and the reader is
    /// disposed automatically when the enumeration ends.
    /// </summary>
    /// <param name="conn">Open connection.</param>
    /// <param name="sql">SQL query.</param>
    /// <returns>An enumerable that yields the same <see cref="SQLiteDataReader"/>
    /// instance per row (positioned on that row). Materialize per-row state inside the
    /// loop body — the reader is not safe to retain past the iteration.</returns>
    public static IEnumerable<SQLiteDataReader> Query(SQLiteConnection conn, string sql)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            yield return reader;
    }

    /// <summary>
    /// Parameterized variant of <see cref="Query(SQLiteConnection, string)"/>.
    /// </summary>
    public static IEnumerable<SQLiteDataReader> Query(SQLiteConnection conn, string sql,
        IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var kv in parameters)
            cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            yield return reader;
    }

    /// <summary>
    /// Begins a transaction. Returned object should be used in a <c>using</c> block;
    /// call <c>Commit</c> or <c>Rollback</c> before disposal. Matches cpp <c>BEGIN</c> /
    /// <c>COMMIT</c> via <c>SQL_STMT</c> at PsmFile.cpp:59 / 257.
    /// </summary>
    public static SQLiteTransaction BeginTransaction(SQLiteConnection conn)
    {
        ArgumentNullException.ThrowIfNull(conn);
        return conn.BeginTransaction();
    }

    /// <summary>
    /// Runs <c>VACUUM</c> on the database. Equivalent to <c>SQL_STMT("VACUUM", db)</c>
    /// in cpp BlibFilter.
    /// </summary>
    public static void Vacuum(SQLiteConnection conn)
    {
        ArgumentNullException.ThrowIfNull(conn);
        Execute(conn, "VACUUM");
    }

    /// <summary>
    /// cpp <c>ESCAPE_APOSTROPHES(const string&amp;)</c> at SqliteRoutine.cpp:110-122.
    /// Doubles every single-quote in <paramref name="sql"/> for safe inlining inside a
    /// <c>'...'</c> SQL literal. Prefer parameterized SQL where possible; this is only
    /// for cases where the cpp callers concatenate the value into a longer statement
    /// (e.g. BlibMaker INSERT-or-IGNORE builders, BlibFilter <c>ATTACH DATABASE</c>).
    /// </summary>
    public static string EscapeApostrophes(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        // cpp loop: append char, then on `'` append again. Replace covers it 1:1.
        return sql.Replace("'", "''", StringComparison.Ordinal);
    }

    // ---- internals ---------------------------------------------------------

    /// <summary>
    /// Quotes a SQLite identifier (table / column name) by wrapping it in double quotes
    /// and doubling embedded double-quotes. SQLite parameter binding cannot bind
    /// identifiers, so when a name has to be inlined this is the safe form.
    /// </summary>
    private static string QuoteIdentifier(string name)
    {
        return "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static long ToInt64(object? scalar)
    {
        if (scalar is null || scalar is DBNull) return 0;
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    private static T? ConvertScalar<T>(object? scalar)
    {
        if (scalar is null || scalar is DBNull) return default;
        if (scalar is T already) return already;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(scalar, target, CultureInfo.InvariantCulture);
    }
}
