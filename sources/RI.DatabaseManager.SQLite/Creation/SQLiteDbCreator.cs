﻿using System;
using System.Data;
using System.Data.SQLite;

using RI.Abstractions.Logging;
using RI.DatabaseManager.Builder;




namespace RI.DatabaseManager.Creation
{
    /// <summary>
    ///     Implements a database creator for SQLite databases.
    /// </summary>
    /// <threadsafety static="false" instance="false" />
    public sealed class SQLiteDbCreator : DbCreatorBase<SQLiteConnection, SQLiteTransaction, DbType>
    {
        #region Instance Constructor/Destructor

        /// <summary>
        ///     Creates a new instance of <see cref="SQLiteDbCreator" />.
        /// </summary>
        /// <param name="options"> The used SQLite database manager options. </param>
        /// <param name="logger"> The used logger. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="options" /> or <paramref name="logger" /> is null. </exception>
        public SQLiteDbCreator (SQLiteDbManagerOptions options, ILogger logger) : base(options, logger) { }

        #endregion
    }
}