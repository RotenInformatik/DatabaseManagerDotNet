﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;

using RI.DatabaseManager.Backup;
using RI.DatabaseManager.Builder;
using RI.DatabaseManager.Cleanup;
using RI.DatabaseManager.Scripts;
using RI.DatabaseManager.Upgrading;
using RI.DatabaseManager.Versioning;




namespace RI.DatabaseManager.Manager
{
    /// <summary>
    ///     Boilerplate implementation of <see cref="IDatabaseManager"/> and <see cref="IDatabaseManager{TConnection,TTransaction,TManager}"/>.
    /// </summary>
    /// <typeparam name="TConnection"> The database connection type. </typeparam>
    /// <typeparam name="TTransaction"> The database transaction type. </typeparam>
    /// <typeparam name="TManager"> The type of the database manager. </typeparam>
    /// <remarks>
    ///     <note type="implement">
    ///         It is recommended that database manager implementations use this base class as it already implements most of the database-independent logic defined by <see cref="IDatabaseManager"/> and <see cref="IDatabaseManager{TConnection,TTransaction,TManager}"/>.
    ///     </note>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public abstract class DatabaseManager <TConnection, TTransaction, TManager> : IDatabaseManager<TConnection, TTransaction, TManager>
        where TConnection : DbConnection
        where TTransaction : DbTransaction
        where TManager : class, IDatabaseManager<TConnection, TTransaction, TManager>
    {
        private IDatabaseVersionDetector<TConnection, TTransaction, TManager> VersionDetector { get; }

        private IDatabaseBackupCreator<TConnection, TTransaction, TManager> BackupCreator { get; }

        private IDatabaseCleanupProcessor<TConnection, TTransaction, TManager> CleanupProcessor { get; }

        private IDatabaseVersionUpgrader<TConnection, TTransaction, TManager> VersionUpgrader { get; }

        private IDatabaseScriptLocator ScriptLocator { get; }




        #region Instance Constructor/Destructor

        /// <summary>
        ///     Creates a new instance of <see cref="DatabaseManager{TConnection,TTransaction,TManager}" />.
        /// </summary>
        protected DatabaseManager (IDatabaseVersionDetector<TConnection, TTransaction, TManager> versionDetector, IDatabaseBackupCreator<TConnection, TTransaction, TManager> backupCreator, IDatabaseCleanupProcessor<TConnection, TTransaction, TManager> cleanupProcessor, IDatabaseVersionUpgrader<TConnection, TTransaction, TManager> versionUpgrader, IDatabaseScriptLocator scriptLocator)
        {
            if (versionDetector == null)
            {
                throw new ArgumentNullException(nameof(versionDetector));
            }

            this.VersionDetector = versionDetector;

            this.BackupCreator = backupCreator == null ? null : (backupCreator is DbManagerBuilder.NullInstance<TConnection, TTransaction, TManager> ? null : backupCreator);
            this.CleanupProcessor = cleanupProcessor == null ? null : (cleanupProcessor is DbManagerBuilder.NullInstance<TConnection, TTransaction, TManager> ? null : cleanupProcessor);
            this.VersionUpgrader = versionUpgrader == null ? null : (versionUpgrader is DbManagerBuilder.NullInstance<TConnection, TTransaction, TManager> ? null : versionUpgrader);
            this.ScriptLocator = scriptLocator == null ? null : (scriptLocator is DbManagerBuilder.NullInstance<TConnection, TTransaction, TManager> ? null : scriptLocator);

            this.InitialState = DatabaseState.Uninitialized;
            this.InitialVersion = -1;

            this.State = DatabaseState.Uninitialized;
            this.Version = -1;

            this.SetStateAndVersion(DatabaseState.Uninitialized, -1);
        }

        /// <summary>
        ///     Finalizes this instance of <see cref="DatabaseManager{TConnection,TTransaction,TManager}" />.
        /// </summary>
        ~DatabaseManager ()
        {
            this.Dispose(false);
        }

        #endregion




        #region Instance Methods

        /// <summary>
        ///     Performs a database state and version detection and updates <see cref="State" />, <see cref="Version" />, <see cref="IsReady"/>, <see cref="CanUpgrade"/>.
        /// </summary>
        protected void DetectStateAndVersion ()
        {
            bool valid = this.DetectStateAndVersionImpl(out DatabaseState? state, out int version);

            if ((!valid) || (version < 0) || (state.GetValueOrDefault(DatabaseState.Uninitialized) == DatabaseState.DamagedOrInvalid))
            {
                state = DatabaseState.DamagedOrInvalid;
                version = -1;
            }
            else if (!state.HasValue)
            {
                if (this.SupportsUpgrade)
                {
                    if (version == 0)
                    {
                        state = DatabaseState.New;
                    }
                    else if (version < this.MinVersion)
                    {
                        state = DatabaseState.TooOld;
                    }
                    else if ((version >= this.MinVersion) && (version < this.MaxVersion))
                    {
                        state = DatabaseState.ReadyOld;
                    }
                    else if (version == this.MaxVersion)
                    {
                        state = DatabaseState.ReadyNew;
                    }
                    else if (version > this.MaxVersion)
                    {
                        state = DatabaseState.TooNew;
                    }
                    else
                    {
                        state = DatabaseState.ReadyUnknown;
                    }
                }
                else
                {
                    state = version == 0 ? DatabaseState.Unavailable : DatabaseState.ReadyUnknown;
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
            //TODO: Log: this.Log(LogLevel.Debug, "Closing database: {0}", this.DebugDetails);

            this.DisposeImpl(disposing);

            this.SetStateAndVersion(DatabaseState.Uninitialized, -1);

            this.InitialState = DatabaseState.Uninitialized;
            this.InitialVersion = -1;
        }

        private void SetStateAndVersion (DatabaseState state, int version)
        {
            DatabaseState oldState = this.State;
            int oldVersion = this.Version;

            this.State = state;
            this.Version = version;

            if (oldState != state)
            {
                //TODO: Log: this.Log(LogLevel.Debug, "Database state changed: {0} -> {1}: {2}", oldState, state, this.DebugDetails);
                this.OnStateChanged(oldState, state);
            }

            if (oldVersion != version)
            {
                //TODO: Log: this.Log(LogLevel.Debug, "Database version changed: {0} -> {1}: {2}", oldVersion, version, this.DebugDetails);
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
        ///     Gets whether this database manager implementation supports cleanup.
        /// </summary>
        /// <value>
        ///     true if cleanup is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsCleanupImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports read-only connections.
        /// </summary>
        /// <value>
        ///     true if read-only connections are supported, false otherwise.
        /// </value>
        protected abstract bool SupportsReadOnlyImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports restore.
        /// </summary>
        /// <value>
        ///     true if restore is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsRestoreImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports script retrieval.
        /// </summary>
        /// <value>
        ///     true if script retrieval is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsScriptsImpl { get; }

        /// <summary>
        ///     Gets whether this database manager implementation supports upgrading.
        /// </summary>
        /// <value>
        ///     true if upgrading is supported, false otherwise.
        /// </value>
        protected abstract bool SupportsUpgradeImpl { get; }

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
        ///     Creates a new database processing step.
        /// </summary>
        /// <returns>
        ///     The newly created database processing step.
        /// </returns>
        protected abstract IDatabaseProcessingStep<TConnection, TTransaction, TManager> CreateProcessingStepImpl ();

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
        ///         The default implementation calls <see cref="IDatabaseBackupCreator.Backup" />.
        ///     </note>
        /// </remarks>
        protected virtual bool BackupImpl (object backupTarget)
        {
            //TODO: Log: this.Log(LogLevel.Debug, "Performing database backup: [{0}]: {1}", backupTarget, this.DebugDetails);
            return this.BackupCreator.Backup(this, backupTarget);
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
        ///         The default implementation calls <see cref="IDatabaseCleanupProcessor.Cleanup" />.
        ///     </note>
        /// </remarks>
        protected virtual bool CleanupImpl ()
        {
            //TODO: Log: this.Log(LogLevel.Debug, "Performing database cleanup: {0}", this.DebugDetails);
            return this.CleanupProcessor.Cleanup(this);
        }

        /// <summary>
        ///     Performs the actual state and version detection as required by this database manager implementation.
        /// </summary>
        /// <param name="state"> Returns the state of the database. Can be null to perform state detection based on <paramref name="version" /> as implemented in <see cref="DatabaseManager{TConnection,TTransaction,TManager}" />. </param>
        /// <param name="version"> Returns the version of the database. </param>
        /// <returns>
        ///     true if the state and version could be successfully determined, false if the database is damaged or in an invalid state.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation calls <see cref="IDatabaseVersionDetector.Detect" />.
        ///     </note>
        /// </remarks>
        protected virtual bool DetectStateAndVersionImpl (out DatabaseState? state, out int version)
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
        protected virtual void DisposeImpl (bool disposing)
        {
        }

        /// <summary>
        ///     Retrieves a script batch.
        /// </summary>
        /// <param name="name"> The name of the script. </param>
        /// <param name="preprocess"> Specifies whether the script is to be preprocessed, if applicable. </param>
        /// <returns>
        ///     The batch in the script (list of independently executed commands).
        ///     If the script is empty or does not contain any commands respectively, an empty list is returned.
        ///     If the script could not be found, null is returned.
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The default implementation calls <see cref="IDatabaseScriptLocator.GetScriptBatch" />.
        ///     </para>
        /// </remarks>
        protected virtual List<string> GetScriptBatchImpl (string name, bool preprocess)
        {
            return this.ScriptLocator.GetScriptBatch(this, name, preprocess);
        }

        /// <summary>
        ///     Performs initialization specific to this database manager implementation.
        /// </summary>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void InitializeImpl ()
        {
        }

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
        protected virtual void OnConnectionCreated (TConnection connection, bool readOnly)
        {
        }

        /// <summary>
        ///     Called when a script has been retrieved
        /// </summary>
        /// <param name="name"> The name of the retrieved script. </param>
        /// <param name="preprocess"> Specifies whether the script is preprocessed. </param>
        /// <param name="scriptBatches"> The list of retrieved individual batches. </param>
        /// <remarks>
        ///     <note type="implement">
        ///         The default implementation does nothing.
        ///     </note>
        /// </remarks>
        protected virtual void OnScriptRetrieved (string name, bool preprocess, List<string> scriptBatches)
        {
        }

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
        protected virtual void OnStateChanged (DatabaseState oldState, DatabaseState newState)
        {
        }

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
        protected virtual void OnVersionChanged (int oldVersion, int newVersion)
        {
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
        ///         The default implementation calls <see cref="IDatabaseBackupCreator.Restore" />.
        ///     </note>
        /// </remarks>
        protected virtual bool RestoreImpl (object backupSource)
        {
            //TODO: Log: this.Log(LogLevel.Debug, "Performing database restore: [{0}]: {1}", backupSource, this.DebugDetails);
            return this.BackupCreator.Restore(this, backupSource);
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
        ///         The default implementation calls <see cref="IDatabaseVersionUpgrader.Upgrade" />.
        ///     </note>
        ///     <note type="implement">
        ///         <see cref="UpgradeImpl" /> might be called multiple times for a single upgrade operation as the upgrading is performed incrementally, calling <see cref="UpgradeImpl" /> once for each version increment.
        ///     </note>
        /// </remarks>
        protected virtual bool UpgradeImpl (int sourceVersion)
        {
            //TODO: Log: this.Log(LogLevel.Debug, "Performing database upgrade: {0} -> {1}: {2}", sourceVersion, sourceVersion + 1, this.DebugDetails);
            return this.VersionUpgrader.Upgrade(this, sourceVersion);
        }

        #endregion




        #region Overrides

        /// <inheritdoc />
        public sealed override string ToString () => string.Format(CultureInfo.InvariantCulture, "{0}; State={1}; Version={2}", this.GetType().Name, this.State, this.Version);

        #endregion




        #region Interface: IDatabaseManager<TConnection,TTransaction,TConnectionStringBuilder,TManager,TConfiguration>

        /// <inheritdoc />
        public bool CanUpgrade => this.SupportsUpgrade && (this.IsReady || (this.State == DatabaseState.New)) && (this.Version >= 0) && (this.Version < this.MaxVersion);

        /// <inheritdoc />
        public DatabaseState InitialState { get; private set; }

        /// <inheritdoc />
        public int InitialVersion { get; private set; }

        /// <inheritdoc />
        public bool IsReady => (this.State == DatabaseState.ReadyNew) || (this.State == DatabaseState.ReadyOld) || (this.State == DatabaseState.ReadyUnknown);
        
        /// <inheritdoc />
        public int MaxVersion => this.SupportsUpgrade ? this.VersionUpgrader.GetMaxVersion(this) : -1;

        /// <inheritdoc />
        public int MinVersion => this.SupportsUpgrade ? this.VersionUpgrader.GetMinVersion(this) : -1;

        /// <inheritdoc />
        public DatabaseState State { get; private set; }

        /// <inheritdoc />
        public bool SupportsBackup => this.SupportsBackupImpl && (this.BackupCreator != null);

        /// <inheritdoc />
        public bool SupportsCleanup => this.SupportsCleanupImpl && (this.CleanupProcessor != null);

        /// <inheritdoc />
        public bool SupportsReadOnly => this.SupportsReadOnlyImpl;

        /// <inheritdoc />
        public bool SupportsRestore => this.SupportsRestoreImpl && (this.BackupCreator?.SupportsRestore ?? false);

        /// <inheritdoc />
        public bool SupportsScripts => this.SupportsScriptsImpl && (this.ScriptLocator != null);

        /// <inheritdoc />
        public bool SupportsUpgrade => this.SupportsUpgradeImpl && (this.VersionUpgrader != null);

        /// <inheritdoc />
        public int Version { get; private set; }

        /// <inheritdoc />
        public bool Backup (object backupTarget)
        {
            if (backupTarget == null)
            {
                throw new ArgumentNullException(nameof(backupTarget));
            }

            if (this.State == DatabaseState.Uninitialized)
            {
                throw new InvalidOperationException(this.GetType().Name + " must be initialized to perform a backup; current state is " + this.State + ".");
            }

            if (!this.SupportsBackup)
            {
                throw new NotSupportedException(this.GetType().Name + " does not support backups.");
            }

            bool result = this.BackupImpl(backupTarget);

            this.DetectStateAndVersion();

            return result;
        }

        /// <inheritdoc />
        public bool Cleanup ()
        {
            if ((!this.IsReady) && (this.State != DatabaseState.New))
            {
                throw new InvalidOperationException(this.GetType().Name + " must be in a ready state or the new state to perform a cleanup; current state is " + this.State + ".");
            }

            if (!this.SupportsCleanup)
            {
                throw new NotSupportedException(this.GetType().Name + " does not support cleanups.");
            }

            bool result = this.CleanupImpl();

            this.DetectStateAndVersion();

            return result;
        }

        /// <inheritdoc />
        public void Close ()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        DbConnection IDatabaseManager.CreateConnection (bool readOnly) => this.CreateConnection(readOnly);

        /// <inheritdoc />
        public TConnection CreateConnection (bool readOnly)
        {
            if (!this.IsReady)
            {
                throw new InvalidOperationException(this.GetType().Name + " must be in a ready state to create a connection; current state is " + this.State + ".");
            }

            if ((!this.SupportsReadOnly) && readOnly)
            {
                throw new NotSupportedException(this.GetType().Name + " does not support read-only connections.");
            }

            TConnection connection = this.CreateConnectionImpl(readOnly);

            if (connection != null)
            {

                this.OnConnectionCreated(connection, readOnly);
            }

            return connection;
        }

        /// <inheritdoc />
        public IDatabaseProcessingStep<TConnection, TTransaction, TManager> CreateProcessingStep ()
        {
            return this.CreateProcessingStepImpl();
        }

        /// <inheritdoc />
        IDatabaseProcessingStep IDatabaseManager.CreateProcessingStep () => this.CreateProcessingStep();

        /// <inheritdoc />
        void IDisposable.Dispose () => this.Close();

        /// <inheritdoc />
        public List<string> GetScriptBatch (string name, bool preprocess)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Argument is an empty string.", nameof(name));
            }

            if (!this.SupportsScripts)
            {
                throw new NotSupportedException(this.GetType().Name + " does not support script retrieval.");
            }

            List<string> result = this.GetScriptBatchImpl(name, preprocess);

            if (result != null)
            {
                this.OnScriptRetrieved(name, preprocess, result);
            }

            return result;
        }

        /// <inheritdoc />
        public void Initialize ()
        {
            //TODO: Log: this.Log(LogLevel.Debug, "Initializing database: {0}", this.DebugDetails);

            if (this.State != DatabaseState.Uninitialized)
            {
                this.Close();
            }

            GC.ReRegisterForFinalize(this);

            this.InitializeImpl();

            this.DetectStateAndVersion();

            this.InitialState = this.State;
            this.InitialVersion = this.Version;
        }

        /// <inheritdoc />
        public bool Restore (object backupSource)
        {
            if (backupSource == null)
            {
                throw new ArgumentNullException(nameof(backupSource));
            }

            if (this.State == DatabaseState.Uninitialized)
            {
                throw new InvalidOperationException(this.GetType().Name + " must be initialized to perform a restore; current state is " + this.State + ".");
            }

            if (!this.SupportsRestore)
            {
                throw new NotSupportedException(this.GetType().Name + " does not support restores.");
            }

            bool result = this.RestoreImpl(backupSource);

            this.DetectStateAndVersion();

            return result;
        }

        /// <inheritdoc />
        public bool Upgrade (int version)
        {
            if ((!this.IsReady) && (this.State != DatabaseState.New))
            {
                throw new InvalidOperationException(this.GetType().Name + " must be in a ready state or the new state to perform an upgrade; current state is " + this.State + ".");
            }

            if (!this.SupportsUpgrade)
            {
                throw new NotSupportedException(this.GetType().Name + " does not support upgrades.");
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

                if ((!this.IsReady) || (!result) || (this.Version <= currentVersion))
                {
                    return false;
                }

                currentVersion = this.Version;
            }

            return true;
        }

        #endregion
    }
}
