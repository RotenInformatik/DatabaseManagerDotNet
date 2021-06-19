﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;

using RI.Abstractions.Logging;
using RI.DatabaseManager.Backup;
using RI.DatabaseManager.Batches;
using RI.DatabaseManager.Builder;
using RI.DatabaseManager.Cleanup;
using RI.DatabaseManager.Creation;
using RI.DatabaseManager.Upgrading;
using RI.DatabaseManager.Versioning;




namespace RI.DatabaseManager.Manager
{
    /// <summary>
    ///     Implements a database manager for SQLite databases.
    /// </summary>
    /// <threadsafety static="false" instance="false" />
    public sealed class SQLiteDbManager : DbManagerBase<SQLiteConnection, SQLiteTransaction, DbType,
        SQLiteParameterCollection, SQLiteParameter, SQLiteDbManagerOptions>
    {
        #region Instance Constructor/Destructor

        /// <summary>
        ///     Creates a new instance of <see cref="SQLiteDbManager" />.
        /// </summary>
        /// <param name="options"> The used SQLite database manager options. </param>
        /// <param name="logger"> The used logger. </param>
        /// <param name="batchLocator"> The used batch locator. </param>
        /// <param name="versionDetector"> The used version detector. </param>
        /// <param name="backupCreator"> The used backup creator, if any. </param>
        /// <param name="cleanupProcessor"> The used cleanup processor, if any. </param>
        /// <param name="versionUpgrader"> The used version upgrader, if any. </param>
        /// <param name="creator"> The used database creator, if any. </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="options" />, <paramref name="logger" />,
        ///     <paramref name="batchLocator" />, or <paramref name="versionDetector" /> is null.
        /// </exception>
        public SQLiteDbManager (SQLiteDbManagerOptions options, ILogger logger,
                                IDbBatchLocator<SQLiteConnection, SQLiteTransaction, DbType> batchLocator,
                                IDbVersionDetector<SQLiteConnection, SQLiteTransaction, DbType> versionDetector,
                                IDbBackupCreator<SQLiteConnection, SQLiteTransaction, DbType> backupCreator,
                                IDbCleanupProcessor<SQLiteConnection, SQLiteTransaction, DbType> cleanupProcessor,
                                IDbVersionUpgrader<SQLiteConnection, SQLiteTransaction, DbType> versionUpgrader,
                                IDbCreator<SQLiteConnection, SQLiteTransaction, DbType> creator) :
            base(logger, batchLocator, versionDetector, backupCreator, cleanupProcessor, versionUpgrader, creator,
                 options) { }

        #endregion




        #region Instance Methods

        internal SQLiteConnection CreateInternalConnection (string connectionStringOverride, bool readOnly)
        {
            SQLiteConnectionStringBuilder connectionString =
                new SQLiteConnectionStringBuilder(connectionStringOverride ??
                                                  this.Options.ConnectionString.ConnectionString);

            connectionString.ReadOnly = readOnly;

            SQLiteConnection connection = new SQLiteConnection(connectionString.ConnectionString);
            connection.Open();

            foreach (SQLiteFunction customFunction in this.Options.CustomFunctions)
            {
                SQLiteFunctionAttribute attribute = customFunction.GetType()
                                                                  .GetCustomAttributes(typeof(SQLiteFunctionAttribute),
                                                                      true)
                                                                  .OfType<SQLiteFunctionAttribute>()
                                                                  .FirstOrDefault();

                if (attribute == null)
                {
                    throw new
                        SQLiteException($"The specified SQLite function does not have {nameof(SQLiteFunctionAttribute)} applied: {customFunction.GetType().Name}");
                }

                connection.BindFunction(attribute, customFunction);
            }

            foreach (SQLiteFunction customCollation in this.Options.CustomCollations)
            {
                SQLiteFunctionAttribute attribute = customCollation.GetType()
                                                                   .GetCustomAttributes(typeof(SQLiteFunctionAttribute),
                                                                       true)
                                                                   .OfType<SQLiteFunctionAttribute>()
                                                                   .FirstOrDefault();

                if (attribute == null)
                {
                    throw new
                        SQLiteException($"The specified SQLite collation does not have {nameof(SQLiteFunctionAttribute)} applied: {customCollation.GetType().Name}");
                }

                connection.BindFunction(attribute, customCollation);
            }

            return connection;
        }

        internal SQLiteTransaction CreateInternalTransaction (string connectionStringOverride, bool readOnly,
                                                              IsolationLevel isolationLevel)
        {
            SQLiteConnection connection = this.CreateInternalConnection(connectionStringOverride, readOnly);
            return connection.BeginTransaction(isolationLevel);
        }

        #endregion




        #region Overrides

        /// <inheritdoc />
        protected override bool SupportsBackupImpl => true;

        /// <inheritdoc />
        protected override bool SupportsCleanupImpl => true;

        /// <inheritdoc />
        protected override bool SupportsCreateImpl => true;

        /// <inheritdoc />
        protected override bool SupportsReadOnlyConnectionsImpl => true;

        /// <inheritdoc />
        protected override bool SupportsRestoreImpl => false;

        /// <inheritdoc />
        protected override bool SupportsUpgradeImpl => true;

        /// <inheritdoc />
        protected override SQLiteConnection CreateConnectionImpl (bool readOnly) =>
            this.CreateInternalConnection(null, readOnly);

        /// <inheritdoc />
        protected override SQLiteTransaction CreateTransactionImpl (bool readOnly, IsolationLevel isolationLevel) =>
            this.CreateInternalTransaction(null, readOnly, isolationLevel);

        /// <inheritdoc />
        protected override bool DetectStateAndVersionImpl (out DbState? state, out int version)
        {
            FileInfo databaseFileInfo = new FileInfo(this.Options.ConnectionString.DataSource);

            if (!databaseFileInfo.Exists)
            {
                state = null;
                version = 0;
                return true;
            }

            if (databaseFileInfo.Length == 0)
            {
                state = null;
                version = 0;
                return true;
            }

            return base.DetectStateAndVersionImpl(out state, out version);
        }

        /// <inheritdoc />
        protected override List<object> ExecuteCommandScriptImpl (SQLiteConnection connection,
                                                                  SQLiteTransaction transaction,
                                                                  DbBatchExecutionType executionType,
                                                                  string script,
                                                                  IDbBatchCommandParameterCollection<DbType> parameters,
                                                                  out string error, out Exception exception)
        {
            error = null;
            exception = null;

            List<object> results = new List<object>();

            this.Log(LogLevel.Debug, "Executing SQLite database processing command:{0}{1}", Environment.NewLine,
                     script);

            using (SQLiteCommand command = transaction == null ? new SQLiteCommand(script, connection)
                                               : new SQLiteCommand(script, connection, transaction))
            {
                foreach (IDbBatchCommandParameter<DbType> parameter in parameters)
                {
                    command.Parameters.Add(parameter.Name, parameter.Type)
                           .Value = parameter.Value;
                }

                switch (executionType)
                {
                    case DbBatchExecutionType.Reader:
                        SQLiteDataReader reader = command.ExecuteReader(CommandBehavior.Default);

                        while (reader.Read())
                        {
                            for (int i1 = 0; i1 < reader.FieldCount; i1++)
                            {
                                object readerResult = reader[i1];
                                results.Add(readerResult);
                            }
                        }

                        break;

                    case DbBatchExecutionType.Scalar:
                        object resultScalar = command.ExecuteScalar();
                        results.Add(resultScalar);
                        break;

                    case DbBatchExecutionType.NonQuery:
                        int resultNonQuery = command.ExecuteNonQuery();
                        results.Add(resultNonQuery);
                        break;
                }

                return results;
            }
        }

        /// <inheritdoc />
        protected override IsolationLevel GetDefaultIsolationLevel () => IsolationLevel.ReadCommitted;

        #endregion
    }
}
