﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;

using RI.Abstractions.Logging;
using RI.DatabaseManager.Backup;
using RI.DatabaseManager.Batches;
using RI.DatabaseManager.Batches.Commands;
using RI.DatabaseManager.Builder;
using RI.DatabaseManager.Builder.Options;
using RI.DatabaseManager.Cleanup;
using RI.DatabaseManager.Upgrading;
using RI.DatabaseManager.Versioning;
using RI.DatabaseManager.Creation;




namespace RI.DatabaseManager.Manager
{
    /// <summary>
    ///     Boilerplate implementation of <see cref="IDbManager" /> and <see cref="IDbManager{TConnection,TTransaction,TParameterTypes}" />.
    /// </summary>
    /// <typeparam name="TConnection"> The database connection type. </typeparam>
    /// <typeparam name="TTransaction"> The database transaction type. </typeparam>
    /// <typeparam name="TParameterTypes"> The database command parameter type. </typeparam>
    /// <typeparam name="TParameters"> The database command parameter collection type. </typeparam>
    /// <typeparam name="TParameter"> The database command parameter type. </typeparam>
    /// <remarks>
    ///     <note type="implement">
    ///         It is recommended that database manager implementations use this base class as it already implements most of the database-independent logic defined by <see cref="IDbManager" /> and <see cref="IDbManager{TConnection,TTransaction,TParameterTypes}" />.
    ///     </note>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public abstract class DbManagerBase <TConnection, TTransaction, TParameterTypes, TParameters, TParameter> : IDbManager<TConnection, TTransaction, TParameterTypes>
        where TConnection : DbConnection
        where TTransaction : DbTransaction
        where TParameterTypes : Enum
        where TParameters : DbParameterCollection
        where TParameter : DbParameter, new()
    {
        #region Instance Constructor/Destructor

        /// <summary>
        ///     Creates a new instance of <see cref="DbManagerBase{TConnection,TTransaction,TParameterTypes,TParameters,TParameter}" />.
        /// </summary>
        /// <param name="logger"> The used logger. </param>
        /// <param name="batchLocator"> The used batch locator. </param>
        /// <param name="versionDetector"> The used version detector. </param>
        /// <param name="backupCreator"> The used backup creator, if any. </param>
        /// <param name="cleanupProcessor"> The used cleanup processor, if any. </param>
        /// <param name="versionUpgrader"> The used version upgrader, if any. </param>
        /// <param name="options"> The used database manager options.</param>
        /// <param name="creator"> The used database creator, if any. </param>
        /// <remarks>
        ///     <note type="important">
        ///         <paramref name="backupCreator" />, <paramref name="cleanupProcessor" />, <paramref name="versionUpgrader" />, <paramref name="options" /> can be null or <see cref="DbManagerBuilder.NullInstance{TConnection,TTransaction,TParameterTypes}" />, depending on how the database manager was build and which (if any) dependency injection library is used.
        ///         Some dependency injection libraries are not properly able to provide null as a constructor-injected dependency for optional dependencies. In such cases, <see cref="DbManagerBuilder.NullInstance{TConnection,TTransaction,TParameterTypes}" /> is passed as parameter value.
        ///     </note>
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="logger" />, <paramref name="batchLocator" />, or <paramref name="versionDetector" /> is null. </exception>
        protected DbManagerBase (ILogger logger, IDbBatchLocator<TConnection, TTransaction, TParameterTypes> batchLocator, IDbVersionDetector<TConnection, TTransaction, TParameterTypes> versionDetector, IDbBackupCreator<TConnection, TTransaction, TParameterTypes> backupCreator, IDbCleanupProcessor<TConnection, TTransaction, TParameterTypes> cleanupProcessor, IDbVersionUpgrader<TConnection, TTransaction, TParameterTypes> versionUpgrader, IDbCreator<TConnection, TTransaction, TParameterTypes> creator, IDbManagerOptions options)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (batchLocator == null)
            {
                throw new ArgumentNullException(nameof(batchLocator));
            }

            if (versionDetector == null)
            {
                throw new ArgumentNullException(nameof(versionDetector));
            }

            this.Logger = logger;
            this.BatchLocator = batchLocator;
            this.VersionDetector = versionDetector;

            this.BackupCreator = backupCreator == null ? null : backupCreator is DbManagerBuilder.NullInstance<TConnection, TTransaction, TParameterTypes> ? null : backupCreator;
            this.CleanupProcessor = cleanupProcessor == null ? null : cleanupProcessor is DbManagerBuilder.NullInstance<TConnection, TTransaction, TParameterTypes> ? null : cleanupProcessor;
            this.VersionUpgrader = versionUpgrader == null ? null : versionUpgrader is DbManagerBuilder.NullInstance<TConnection, TTransaction, TParameterTypes> ? null : versionUpgrader;
            this.Creator = creator == null ? null : creator is DbManagerBuilder.NullInstance<TConnection, TTransaction, TParameterTypes> ? null : creator;
            this.Options = options == null ? null : options is DbManagerBuilder.NullInstance<TConnection, TTransaction, TParameterTypes> ? null : options;

            this.InitialState = DbState.Uninitialized;
            this.InitialVersion = -1;

            this.State = DbState.Uninitialized;
            this.Version = -1;

            this.SetStateAndVersion(DbState.Uninitialized, -1);
        }

        /// <summary>
        ///     Finalizes this instance of <see cref="DbManagerBase{TConnection,TTransaction,TParameterTypes,TParameters,TParameter}" />.
        /// </summary>
        ~DbManagerBase ()
        {
            this.Dispose(false);
        }

        #endregion




        #region Instance Properties/Indexer

        /// <summary>
        ///     Gets the used logger.
        /// </summary>
        /// <value>
        ///     The used logger.
        /// </value>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the used options.
        /// </summary>
        /// <value>
        /// The used options or null if no options are used.
        /// </value>
        protected IDbManagerOptions Options { get; }

        private IDbCreator<TConnection, TTransaction, TParameterTypes> Creator { get; }

        private IDbBackupCreator<TConnection, TTransaction, TParameterTypes> BackupCreator { get; }

        private IDbBatchLocator<TConnection, TTransaction, TParameterTypes> BatchLocator { get; }

        private IDbCleanupProcessor<TConnection, TTransaction, TParameterTypes> CleanupProcessor { get; }

        private IDbVersionDetector<TConnection, TTransaction, TParameterTypes> VersionDetector { get; }

        private IDbVersionUpgrader<TConnection, TTransaction, TParameterTypes> VersionUpgrader { get; }

        #endregion




        #region Instance Methods

        /// <summary>
        ///     Performs a database state and version detection and updates <see cref="State" /> and <see cref="Version" />.
        /// </summary>
        protected void DetectStateAndVersion ()
        {
            bool valid = this.DetectStateAndVersionImpl(out DbState? state, out int version);

            if (!valid || (state.GetValueOrDefault(DbState.Uninitialized) == DbState.DamagedOrInvalid))
            {
                state = DbState.DamagedOrInvalid;
                version = -1;
            }
            else if ((version < 0) || (state.GetValueOrDefault(DbState.Uninitialized) == DbState.Unavailable))
            {
                state = DbState.Unavailable;
                version = -1;
            }
            else if ((version == 0) || (state.GetValueOrDefault(DbState.Uninitialized) == DbState.New))
            {
                state = DbState.New;
                version = 0;
            }
            else if (!state.HasValue)
            {
                if (this.SupportsUpgrade)
                {
                    if (version < this.MinVersion)
                    {
                        state = DbState.TooOld;
                    }
                    else if ((version >= this.MinVersion) && (version < this.MaxVersion))
                    {
                        state = DbState.ReadyOld;
                    }
                    else if (version == this.MaxVersion)
                    {
                        state = DbState.ReadyNew;
                    }
                    else if (version > this.MaxVersion)
                    {
                        state = DbState.TooNew;
                    }
                    else
                    {
                        state = DbState.ReadyUnknown;
                    }
                }
                else
                {
                    state = DbState.ReadyUnknown;
                }
            }

            this.SetStateAndVersion(state.Value, version);
        }

        /// <summary>
        ///     Disposes this database manager and frees all resources.
        /// </summary>
        /// <param name="disposing"> true if called from <see cref="IDisposable.Dispose" /> or <see cref="Close" />, false if called from the destructor. </param>
        protected void Dispose (bool disposing)
        {
            this.Log(LogLevel.Debug, "Closing database manager");

            this.SetStateAndVersion(DbState.Uninitialized, -1);

            this.DisposeImpl(disposing);

            this.InitialState = DbState.Uninitialized;
            this.InitialVersion = -1;
        }

        /// <summary>
        ///     Writes a log message for this database manager.
        /// </summary>
        /// <param name="level"> The log level of the log message. </param>
        /// <param name="format"> Log message (with optional string expansion arguments such as <c> {0} </c>, <c> {1} </c>, etc. which are expanded by <paramref name="args" />). </param>
        /// <param name="args"> Optional message arguments expanded into <paramref name="format" />. </param>
        protected void Log (LogLevel level, string format, params object[] args)
        {
            this.Logger.Log(level, this.GetType()
                                       .Name, null, format, args);
        }

        /// <summary>
        ///     Writes a log message for this database manager.
        /// </summary>
        /// <param name="level"> The log level of the log message. </param>
        /// <param name="exception"> Exception associated with the log message. </param>
        /// <param name="format"> Optional log message (with optional string expansion arguments such as <c> {0} </c>, <c> {1} </c>, etc. which are expanded by <paramref name="args" />). </param>
        /// <param name="args"> Optional message arguments expanded into <paramref name="format" />. </param>
        protected void Log (LogLevel level, Exception exception, string format, params object[] args)
        {
            this.Logger.Log(level, this.GetType()
                                       .Name, exception, format, args);
        }

        private void SetStateAndVersion (DbState state, int version)
        {
            DbState oldState = this.State;
            int oldVersion = this.Version;

            this.State = state;
            this.Version = version;

            if (oldState != state)
            {
                this.Log(LogLevel.Information, "Database state changed: {0} -> {1}", oldState, state);
                this.OnStateChanged(oldState, state);
            }

            if (oldVersion != version)
            {
                this.Log(LogLevel.Information, "Database version changed: {0} -> {1}", oldVersion, version);
                this.OnVersionChanged(oldVersion, version);
            }
        }

        #endregion




        #region Abstracts

        /// <summary>
        ///     Gets whether this database manager implementation supports backup.
        /// </summary>
        /// <value>
        ///     true if backup is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsBackupImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports restore.
        /// </summary>
        /// <value>
        ///     true if restore is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsRestoreImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports cleanup.
        /// </summary>
        /// <value>
        ///     true if cleanup is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsCleanupImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports database creation.
        /// </summary>
        /// <value>
        ///     true if creation is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsCreateImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports upgrading.
        /// </summary>
        /// <value>
        ///     true if upgrading is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsUpgradeImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports read-only connections.
        /// </summary>
        /// <value>
        ///     true if read-only connections are supported, false otherwise.
        /// </value>
        protected abstract bool SupportsReadOnlyConnectionsImpl { get; }

        /// <summary>
        ///     Creates a new database connection.
        /// </summary>
        /// <param name="readOnly"> Specifies whether the connection should be read-only. </param>
        /// <returns>
        ///     The newly created and already opened connection or null if the connection could not be created.
        ///     Details about failures should be written to logs.
        /// </returns>
        protected abstract TConnection CreateConnectionImpl (bool readOnly);

        /// <summary>
        ///     Creates a new database transaction.
        /// </summary>
        /// <param name="readOnly"> Specifies whether the underlying connection should be read-only. </param>
        /// <param name="isolationLevel"> Specifies the used isolation level for the transaction.</param>
        /// <returns>
        ///     The newly created transaction with its underlying connection already opened or null if the transaction or connection could not be created.
        ///     Details about failures should be written to logs.
        /// </returns>
        protected abstract TTransaction CreateTransactionImpl (bool readOnly, IsolationLevel isolationLevel);

        /// <summary>
        /// Gets the default isolation level used by the database if none is provided.
        /// </summary>
        /// <returns>
        /// The default isolation level used by the database.
        /// </returns>
        protected abstract IsolationLevel GetDefaultIsolationLevel ();

        /// <summary>
        ///     Executes database script code of a single batch command.
        /// </summary>
        /// <param name="connection"> The used database connection. </param>
        /// <param name="transaction"> The used database transaction or null if no transaction is used. </param>
        /// <param name="script"> The database script to execute. </param>
        /// <param name="parameters"> The parameters used in the command. </param>
        /// <param name="error"> The database specific error which occurred during execution (or null if none is available). </param>
        /// <param name="exception"> The database specific exception which occurred during execution (or null if none is available). </param>
        /// <returns>
        ///     The result of the code callback.
        /// </returns>
        protected abstract object ExecuteCommandScriptImpl (TConnection connection, TTransaction transaction, string script, IDbBatchCommandParameterCollection<TParameterTypes> parameters, out string error, out Exception exception);

        #endregion




        #region Virtuals

        /// <summary>
        ///     Performs a backup.
        /// </summary>
        /// <param name="backupTarget"> The backup creator specific object which abstracts the backup target. </param>
        /// <returns>
        ///     true if the backup was successful, false otherwise.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDbBackupCreator.Backup" />.
        ///     </note>
        /// </remarks>
        protected virtual bool BackupImpl (object backupTarget)
        {
            this.Log(LogLevel.Information, "Performing database backup; Target=[{0}][{1}]", backupTarget.GetType()
                                                                                                  .Name, backupTarget);

            return this.BackupCreator.Backup(this, backupTarget);
        }

        /// <summary>
        ///     Performs a restore.
        /// </summary>
        /// <param name="backupSource"> The backup creator specific object which abstracts the backup source. </param>
        /// <returns>
        ///     true if the restore was successful, false otherwise.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDbBackupCreator.Restore" />.
        ///     </note>
        /// </remarks>
        protected virtual bool RestoreImpl (object backupSource)
        {
            this.Log(LogLevel.Information, "Performing database restore; Source=[{0}][{1}]", backupSource.GetType()
                                                                                                         .Name, backupSource);

            return this.BackupCreator.Restore(this, backupSource);
        }

        /// <summary>
        ///     Performs a database cleanup.
        /// </summary>
        /// <returns>
        ///     true if the cleanup was successful, false otherwise.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDbCleanupProcessor.Cleanup" />.
        ///     </note>
        /// </remarks>
        protected virtual bool CleanupImpl ()
        {
            this.Log(LogLevel.Information, "Performing database cleanup");
            return this.CleanupProcessor.Cleanup(this);
        }

        /// <summary>
        ///     Performs a database creation.
        /// </summary>
        /// <returns>
        ///     true if the creation was successful, false otherwise.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDbCreator.Create" />.
        ///     </note>
        /// </remarks>
        protected virtual bool CreateImpl ()
        {
            this.Log(LogLevel.Information, "Performing database creation");
            return this.Creator.Create(this);
        }

        /// <summary>
        ///     Performs an upgrade from <paramref name="sourceVersion" /> to <paramref name="sourceVersion" /> + 1.
        /// </summary>
        /// <param name="sourceVersion"> The current version to upgrade from. </param>
        /// <returns>
        ///     true if the upgrade was successful, false otherwise.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDbVersionUpgrader.Upgrade" />.
        ///     </note>
        ///     <note type="implement">
        ///         <see cref="UpgradeImpl" /> might be called multiple times for a single upgrade operation as the upgrading is performed incrementally, calling <see cref="UpgradeImpl" /> once for each version increment.
        ///     </note>
        /// </remarks>
        protected virtual bool UpgradeImpl (int sourceVersion)
        {
            this.Log(LogLevel.Information, "Performing database upgrade: {0} -> {1}", sourceVersion, sourceVersion + 1);
            return this.VersionUpgrader.Upgrade(this, sourceVersion);
        }

        /// <summary>
        ///     Creates a new empty batch.
        /// </summary>
        /// <returns>
        ///     The newly created empty batch.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation creates and returns a new instance of <see cref="DbBatch{TConnection,TTransaction,TParameterTypes}" />.
        ///     </note>
        /// </remarks>
        protected virtual IDbBatch<TConnection, TTransaction, TParameterTypes> CreateBatchImpl ()
        {
            return new DbBatch<TConnection, TTransaction, TParameterTypes>();
        }

        /// <summary>
        ///     Gets the names of all available batches.
        /// </summary>
        /// <returns>
        ///     The set with the names of all available batches.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDbBatchLocator.GetNames" />.
        ///     </note>
        /// </remarks>
        protected virtual ISet<string> GetBatchNamesImpl ()
        {
            return this.BatchLocator.GetNames();
        }

        /// <summary>
        ///     Gets a batch of a specified name.
        /// </summary>
        /// <param name="name"> The name of the batch. </param>
        /// <param name="commandSeparator"> The string which is used as the separator to separate commands within the batch or null if the batch locators default separators are to be used. </param>
        /// <param name="batchCreator"> The batch creator provided by the used database manager which is used to create proper batch instances (implementations of <see cref="IDbBatch"/>). </param>
        /// <returns>
        ///     The batch or null if the batch of the specified name could not be found.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDbBatchLocator.GetBatch" />.
        ///     </note>
        /// </remarks>
        protected virtual IDbBatch<TConnection, TTransaction, TParameterTypes> GetBatchImpl(string name, string commandSeparator, Func<IDbBatch<TConnection, TTransaction, TParameterTypes>> batchCreator)
        {
            return this.BatchLocator.GetBatch(name, commandSeparator, batchCreator);
        }

        /// <summary>
        ///     Executes a batch according to this database manager implementation.
        /// </summary>
        /// <param name="batch"> The batch to execute. </param>
        /// <param name="readOnly"> Specifies whether the connection, used to execute the batch, should be read-only. </param>
        /// <returns>
        ///     true if the batch was executed successfully, false otherwise.
        ///     Details about failures should be written to logs and/or into properties of the executed batch.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation first resets all commands and then executes <see cref="ExecuteCommandScriptImpl" /> or <see cref="ExecuteCommandCodeImpl" /> for each command of the batch.
        ///     </note>
        /// </remarks>
        protected virtual bool ExecuteBatchImpl(IDbBatch<TConnection, TTransaction, TParameterTypes> batch, bool readOnly)
        {
            TConnection connection;
            TTransaction transaction;

            if (batch.RequiresTransaction() || (!batch.DisallowsTransaction()))
            {
                transaction = this.CreateTransaction(readOnly, batch.GetRequiredIsolationLevel().GetValueOrDefault(this.GetDefaultIsolationLevel()));

                if (transaction == null)
                {
                    return false;
                }

                connection = (TConnection)transaction.Connection;
            }
            else
            {
                connection = this.CreateConnection(readOnly);

                if (connection == null)
                {
                    return false;
                }

                transaction = null;
            }

            batch.Reset();

            foreach (IDbBatchCommand<TConnection, TTransaction, TParameterTypes> command in batch.Commands)
            {
                if (command == null)
                {
                    continue;
                }

                object result;
                string error;
                Exception exception;

                DbBatchCommandParameterCollection<TParameterTypes> parameters = command.MergeParameters(batch);

                if ((command.Script != null) && (command.Code == null))
                {
                    try
                    {
                        command.WasExecuted = true;
                        result = this.ExecuteCommandScriptImpl(connection, transaction, command.Script, parameters, out error, out exception);
                    }
                    catch (Exception ex)
                    {
                        command.Exception = ex;
                        this.Log(LogLevel.Error, ex, "Execution of database batch script failed.");
                        return false;
                    }
                }
                else if ((command.Script == null) && (command.Code != null))
                {
                    try
                    {
                        command.WasExecuted = true;
                        result = this.ExecuteCommandCodeImpl(connection, transaction, command.Code, parameters, out error, out exception);
                    }
                    catch (Exception ex)
                    {
                        command.Exception = ex;
                        this.Log(LogLevel.Error, ex, "Execution of database batch code failed.");
                        return false;
                    }
                }
                else if ((command.Script != null) && (command.Code != null))
                {
                    throw new NotSupportedException($"The provided batch command is invalid (contains both script and code): {command.GetType().Name}.");
                }
                else
                {
                    throw new NotSupportedException($"The provided batch command is invalid (contains neither script nor code): {command.GetType().Name}.");
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    command.Error = error;
                    this.Log(LogLevel.Error, "Execution of database batch failed.{0}{1}", Environment.NewLine, error);
                    return false;
                }

                if (exception != null)
                {
                    command.Exception = exception;
                    this.Log(LogLevel.Error, exception, "Execution of database batch failed.");
                    return false;
                }

                command.Result = result;
            }

            return true;
        }

        /// <summary>
        ///     Executes a code callback of a single batch command.
        /// </summary>
        /// <param name="connection"> The used database connection. </param>
        /// <param name="transaction"> The used database transaction or null if no transaction is used. </param>
        /// <param name="code"> The callback to execute. </param>
        /// <param name="parameters"> The parameters used in the command. </param>
        /// <param name="error"> The database specific error which occurred during execution (or null if none is available). </param>
        /// <param name="exception"> The database specific exception which occurred during execution (or null if none is available). </param>
        /// <returns>
        ///     The result of the code callback.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <paramref name="code" /> with <paramref name="connection" />, <paramref name="transaction" />, and <paramref name="parameters"/> as parameters.
        ///     </note>
        /// </remarks>
        protected virtual object ExecuteCommandCodeImpl(TConnection connection, TTransaction transaction, CallbackBatchCommandDelegate<TConnection, TTransaction, TParameterTypes> code, IDbBatchCommandParameterCollection<TParameterTypes> parameters, out string error, out Exception exception)
        {
            error = null;
            exception = null;

            return code(connection, transaction, parameters, out error, out exception);
        }

        /// <summary>
        ///     Performs the actual state and version detection as required by this database manager implementation.
        /// </summary>
        /// <param name="state"> Returns the state of the database. Can be null to perform state detection based on <paramref name="version" /> as implemented in <see cref="DbManagerBase{TConnection,TTransaction,TParameterTypes, TParameters,TParameter}" />. </param>
        /// <param name="version"> Returns the version of the database. </param>
        /// <returns>
        ///     true if the state and version could be successfully determined, false if the database is damaged or in an invalid state.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDbVersionDetector.Detect" />.
        ///     </note>
        /// </remarks>
        protected virtual bool DetectStateAndVersionImpl (out DbState? state, out int version)
        {
            return this.VersionDetector.Detect(this, out state, out version);
        }

        /// <summary>
        ///     Performs disposing specific to this database manager implementation.
        /// </summary>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void DisposeImpl (bool disposing) { }

        /// <summary>
        ///     Performs initialization specific to this database manager implementation.
        /// </summary>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void InitializeImpl () { }

        /// <summary>
        ///     Called when a batch has been created.
        /// </summary>
        /// <param name="batch"> The created batch. </param>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void OnBatchCreated (IDbBatch<TConnection, TTransaction, TParameterTypes> batch) { }

        /// <summary>
        ///     Called when a batch has been retrieved.
        /// </summary>
        /// <param name="batch"> The retrieved batch. </param>
        /// <param name="name"> The name of the retrieved batch. </param>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void OnBatchRetrieved (IDbBatch<TConnection, TTransaction, TParameterTypes> batch, string name) { }

        /// <summary>
        ///     Called when a connection has been created.
        /// </summary>
        /// <param name="connection"> The connection. </param>
        /// <param name="readOnly"> Indicates whether the connection is read-only. </param>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void OnConnectionCreated (TConnection connection, bool readOnly) { }

        /// <summary>
        ///     Called when the current database state has changed.
        /// </summary>
        /// <param name="oldState"> The previous state. </param>
        /// <param name="newState"> The new current state. </param>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void OnStateChanged (DbState oldState, DbState newState) { }

        /// <summary>
        ///     Called when a transaction has been created.
        /// </summary>
        /// <param name="transaction"> The transaction. </param>
        /// <param name="readOnly"> Indicates whether the underlying connection is read-only. </param>
        /// <param name="isolationLevel"> Specifies the used isolation level for the transaction.</param>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void OnTransactionCreated (TTransaction transaction, bool readOnly, IsolationLevel isolationLevel) { }

        /// <summary>
        ///     Called when the current database version has changed.
        /// </summary>
        /// <param name="oldVersion"> The previous version. </param>
        /// <param name="newVersion"> The new current version. </param>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void OnVersionChanged (int oldVersion, int newVersion) { }

        #endregion




        #region Overrides

        /// <inheritdoc />
        public override string ToString ()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}; State={1}; Version={2}", this.GetType()
                                                                                                  .Name, this.State, this.Version);
        }

        #endregion




        #region Interface: IDbManager<TConnection,TTransaction>

        /// <inheritdoc />
        public IDbManagerOptions GetOptions () => this.Options;

        /// <inheritdoc />
        public DbState InitialState { get; private set; }

        /// <inheritdoc />
        public int InitialVersion { get; private set; }

        /// <inheritdoc />
        public int MaxVersion => this.SupportsUpgrade ? this.VersionUpgrader.GetMaxVersion(this) : -1;

        /// <inheritdoc />
        public int MinVersion => this.SupportsUpgrade ? this.VersionUpgrader.GetMinVersion(this) : -1;

        /// <inheritdoc />
        public DbState State { get; private set; }

        /// <inheritdoc />
        public int Version { get; private set; }

        /// <inheritdoc />
        public bool SupportsBackup => this.SupportsBackupImpl && (this.BackupCreator?.SupportsBackup ?? false);

        /// <inheritdoc />
        public bool SupportsRestore => this.SupportsRestoreImpl && (this.BackupCreator?.SupportsRestore ?? false);

        /// <inheritdoc />
        public bool SupportsCleanup => this.SupportsCleanupImpl && (this.CleanupProcessor != null);

        /// <inheritdoc />
        public bool SupportsCreate => this.SupportsCreateImpl && (this.Creator != null);

        /// <inheritdoc />
        public bool SupportsUpgrade => this.SupportsUpgradeImpl && (this.VersionUpgrader != null);

        /// <inheritdoc />
        public bool SupportsReadOnlyConnections => this.SupportsReadOnlyConnectionsImpl;

        /// <inheritdoc />
        public bool Backup (object backupTarget)
        {
            if (backupTarget == null)
            {
                throw new ArgumentNullException(nameof(backupTarget));
            }

            if (this.State == DbState.Uninitialized)
            {
                throw new InvalidOperationException(this.GetType()
                                                        .Name + " must be initialized to perform a backup; current state is " + this.State + ".");
            }

            if (!this.SupportsBackup)
            {
                throw new NotSupportedException(this.GetType()
                                                    .Name + " does not support backups.");
            }

            bool result = this.BackupImpl(backupTarget);

            this.DetectStateAndVersion();

            return result;
        }

        /// <inheritdoc />
        public bool Restore (object backupSource)
        {
            if (backupSource == null)
            {
                throw new ArgumentNullException(nameof(backupSource));
            }

            if (this.State == DbState.Uninitialized)
            {
                throw new InvalidOperationException(this.GetType()
                                                        .Name + " must be initialized to perform a restore; current state is " + this.State + ".");
            }

            if (!this.SupportsRestore)
            {
                throw new NotSupportedException(this.GetType()
                                                    .Name + " does not support restores.");
            }

            bool result = this.RestoreImpl(backupSource);

            this.DetectStateAndVersion();

            return result;
        }

        /// <inheritdoc />
        public bool Cleanup ()
        {
            if (!this.IsReady())
            {
                throw new InvalidOperationException(this.GetType()
                                                        .Name + " must be in a ready state to perform a cleanup; current state is " + this.State + ".");
            }

            if (!this.SupportsCleanup)
            {
                throw new NotSupportedException(this.GetType()
                                                    .Name + " does not support cleanups.");
            }

            bool result = this.CleanupImpl();

            this.DetectStateAndVersion();

            return result;
        }

        /// <inheritdoc />
        public bool Create ()
        {
            if (this.State != DbState.New)
            {
                throw new InvalidOperationException(this.GetType()
                                                        .Name + " must be in the new state to perform creation; current state is " + this.State + ".");
            }

            if (!this.SupportsCreate)
            {
                throw new NotSupportedException(this.GetType()
                                                    .Name + " does not support creation.");
            }

            bool result = this.CreateImpl();

            this.DetectStateAndVersion();

            return result;
        }

        /// <inheritdoc />
        public bool Upgrade (int version)
        {
            if (!this.IsReady())
            {
                throw new InvalidOperationException(this.GetType()
                                                        .Name + " must be in a ready state to perform an upgrade; current state is " + this.State + ".");
            }

            if (!this.SupportsUpgrade)
            {
                throw new NotSupportedException(this.GetType()
                                                    .Name + " does not support upgrades.");
            }

            if ((version < this.MinVersion) || (version > this.MaxVersion))
            {
                throw new ArgumentOutOfRangeException(nameof(version), "The specified version " + version + " is not within the supported version range (" + this.MinVersion + "..." + this.MaxVersion + ").");
            }

            if (version < this.Version)
            {
                throw new ArgumentOutOfRangeException(nameof(version), "The specified version " + version + " is lower than the current version (" + this.Version + ").");
            }

            if (version == this.Version)
            {
                return true;
            }

            int currentVersion = this.Version;

            while (currentVersion < version)
            {
                bool result = this.UpgradeImpl(currentVersion);

                this.DetectStateAndVersion();

                if (!this.IsReady() || !result || (this.Version <= currentVersion))
                {
                    return false;
                }

                currentVersion = this.Version;
            }

            return true;
        }

        /// <inheritdoc />
        DbConnection IDbManager.CreateConnection (bool readOnly) => this.CreateConnection(readOnly);

        /// <inheritdoc />
        public TConnection CreateConnection (bool readOnly)
        {
            if (this.State == DbState.Uninitialized)
            {
                throw new InvalidOperationException(this.GetType()
                                                        .Name + " must be initialized to create a connection; current state is " + this.State + ".");
            }

            if (!this.SupportsReadOnlyConnections && readOnly)
            {
                throw new NotSupportedException(this.GetType()
                                                    .Name + " does not support read-only connections.");
            }

            TConnection connection;

            try
            {
                connection = this.CreateConnectionImpl(readOnly);
            }
            catch (Exception exception)
            {
                this.Log(LogLevel.Error, exception, "Creation of database connection failed.");
                return null;
            }

            if (connection != null)
            {
                this.OnConnectionCreated(connection, readOnly);
            }

            return connection;
        }

        /// <inheritdoc />
        DbTransaction IDbManager.CreateTransaction (bool readOnly, IsolationLevel isolationLevel) => this.CreateTransaction(readOnly, isolationLevel);

        /// <inheritdoc />
        public TTransaction CreateTransaction (bool readOnly, IsolationLevel isolationLevel)
        {
            if (this.State == DbState.Uninitialized)
            {
                throw new InvalidOperationException(this.GetType()
                                                        .Name + " must be initialized to create a transaction; current state is " + this.State + ".");
            }

            if (!this.SupportsReadOnlyConnections && readOnly)
            {
                throw new NotSupportedException(this.GetType()
                                                    .Name + " does not support read-only connections.");
            }

            TTransaction transaction;

            try
            {
                transaction = this.CreateTransactionImpl(readOnly, isolationLevel);
            }
            catch (Exception exception)
            {
                this.Log(LogLevel.Error, exception, "Creation of database transaction failed.");
                return null;
            }

            if (transaction != null)
            {
                this.OnTransactionCreated(transaction, readOnly, isolationLevel);
            }

            return transaction;
        }

        /// <inheritdoc />
        IDbBatch IDbManager.CreateBatch () => this.CreateBatch();

        /// <inheritdoc />
        public IDbBatch<TConnection, TTransaction, TParameterTypes> CreateBatch ()
        {
            IDbBatch<TConnection, TTransaction, TParameterTypes> batch = this.CreateBatchImpl();

            this.OnBatchCreated(batch);

            return batch;
        }

        /// <inheritdoc />
        public ISet<string> GetBatchNames ()
        {
            return this.GetBatchNamesImpl();
        }

        /// <inheritdoc />
        IDbBatch IDbManager.GetBatch (string name, string commandSeparator)
        {
            return this.GetBatch(name, commandSeparator);
        }

        /// <inheritdoc />
        public IDbBatch<TConnection, TTransaction, TParameterTypes> GetBatch (string name, string commandSeparator)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The string argument is empty.", nameof(name));
            }

            if (commandSeparator != null)
            {
                if (string.IsNullOrWhiteSpace(commandSeparator))
                {
                    throw new ArgumentException("The string argument is empty.", nameof(commandSeparator));
                }
            }

            IDbBatch<TConnection, TTransaction, TParameterTypes> batch = this.GetBatchImpl(name, commandSeparator, this.CreateBatch);

            if (batch != null)
            {
                this.OnBatchRetrieved(batch, name);
            }

            return batch;
        }

        /// <inheritdoc />
        bool IDbManager.ExecuteBatch (IDbBatch batch, bool readOnly, bool detectVersionAndStateAfterExecution)
        {
            return this.ExecuteBatch((IDbBatch<TConnection, TTransaction, TParameterTypes>)batch, readOnly, detectVersionAndStateAfterExecution);
        }

        /// <inheritdoc />
        public bool ExecuteBatch (IDbBatch<TConnection, TTransaction, TParameterTypes> batch, bool readOnly, bool detectVersionAndStateAfterExecution)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (this.State == DbState.Uninitialized)
            {
                throw new InvalidOperationException(this.GetType()
                                                        .Name + " must be initialized to execute a batch; current state is " + this.State + ".");
            }

            if (!this.SupportsReadOnlyConnections && readOnly)
            {
                throw new NotSupportedException(this.GetType()
                                                    .Name + " does not support read-only connections.");
            }

            batch.Reset();

            bool result = this.ExecuteBatchImpl(batch, readOnly);

            if (detectVersionAndStateAfterExecution)
            {
                this.DetectStateAndVersion();
            }

            return result;
        }

        /// <inheritdoc />
        void IDisposable.Dispose ()
        {
            this.Close();
        }

        /// <inheritdoc />
        public void Initialize ()
        {
            this.Log(LogLevel.Debug, "Initializing database manager");

            if (this.State != DbState.Uninitialized)
            {
                throw new InvalidOperationException("The database manager is already initialized.");
            }

            GC.ReRegisterForFinalize(this);

            this.InitializeImpl();

            this.DetectStateAndVersion();

            this.InitialState = this.State;
            this.InitialVersion = this.Version;
        }

        /// <inheritdoc />
        public void Close ()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <inheritdoc cref="DbManagerBase{TConnection, TTransaction, TParameterTypes, TParameters, TParameter}" />
    /// <typeparam name="TConnection"> The database connection type. </typeparam>
    /// <typeparam name="TTransaction"> The database transaction type. </typeparam>
    /// <typeparam name="TParameterTypes"> The database command parameter type. </typeparam>
    /// <typeparam name="TParameters"> The database command parameter collection type. </typeparam>
    /// <typeparam name="TParameter"> The database command parameter type. </typeparam>
    /// <typeparam name="TOptions"> The database option type. </typeparam>
    public abstract class DbManagerBase <TConnection, TTransaction, TParameterTypes, TParameters, TParameter, TOptions> : DbManagerBase<TConnection, TTransaction, TParameterTypes, TParameters, TParameter>
        where TConnection : DbConnection
        where TTransaction : DbTransaction
        where TParameterTypes : Enum
        where TParameters : DbParameterCollection
        where TParameter : DbParameter, new()
        where TOptions : IDbManagerOptions
    {
        /// <inheritdoc />
        protected DbManagerBase (ILogger logger, IDbBatchLocator<TConnection, TTransaction, TParameterTypes> batchLocator, IDbVersionDetector<TConnection, TTransaction, TParameterTypes> versionDetector, IDbBackupCreator<TConnection, TTransaction, TParameterTypes> backupCreator, IDbCleanupProcessor<TConnection, TTransaction, TParameterTypes> cleanupProcessor, IDbVersionUpgrader<TConnection, TTransaction, TParameterTypes> versionUpgrader, IDbCreator<TConnection, TTransaction, TParameterTypes> creator, TOptions options) : base(logger, batchLocator, versionDetector, backupCreator, cleanupProcessor, versionUpgrader, creator, (TOptions)options.Clone()) { }

        /// <summary>
        /// Gets the used options.
        /// </summary>
        /// <value>
        /// The used options or null if no options are used.
        /// </value>
        protected new TOptions Options => (TOptions)base.Options;

        /// <inheritdoc cref="IDbManager.GetOptions"/>
        public new TOptions GetOptions() => this.Options;
    }
}
