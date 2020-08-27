﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

using RI.DatabaseManager.Manager;




namespace RI.DatabaseManager.Upgrading
{
    /// <summary>
    ///     Implements a database version upgrader for SQLite databases.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="SQLiteDatabaseVersionUpgrader" /> uses upgrade steps associated to specific source versions to perform the upgrade.
    ///     </para>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public sealed class SQLiteDatabaseVersionUpgrader : DatabaseVersionUpgrader<SQLiteConnection, SQLiteTransaction, SQLiteConnectionStringBuilder, SQLiteDatabaseManager, SQLiteDatabaseManagerConfiguration>
    {
        #region Instance Constructor/Destructor

        /// <summary>
        ///     Creates a new instance of <see cref="SQLiteDatabaseVersionUpgrader" />.
        /// </summary>
        /// <param name="upgradeSteps"> The sequence of upgrade steps supported by this version upgrader. </param>
        /// <remarks>
        ///     <para>
        ///         <paramref name="upgradeSteps" /> is enumerated only once.
        ///     </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="upgradeSteps" /> is null. </exception>
        /// <exception cref="ArgumentException"> <paramref name="upgradeSteps" /> is an empty sequence or contains the same source version multiple times. </exception>
        public SQLiteDatabaseVersionUpgrader (IEnumerable<SQLiteDbVersionUpgradeStep> upgradeSteps)
        {
            if (upgradeSteps == null)
            {
                throw new ArgumentNullException(nameof(upgradeSteps));
            }

            this.UpgradeSteps = new List<SQLiteDbVersionUpgradeStep>(upgradeSteps);

            if (this.UpgradeSteps.Count == 0)
            {
                throw new ArgumentException("No upgrade steps.", nameof(upgradeSteps));
            }

            foreach (int sourceVersion in this.UpgradeSteps.Select(x => x.SourceVersion))
            {
                int count = this.UpgradeSteps.Count(x => x.SourceVersion == sourceVersion);
                if (count != 1)
                {
                    throw new ArgumentException("Source version " + sourceVersion + " specified multiple times.", nameof(upgradeSteps));
                }
            }
        }

        /// <summary>
        ///     Creates a new instance of <see cref="SQLiteDatabaseVersionUpgrader" />.
        /// </summary>
        /// <param name="upgradeSteps"> The array of upgrade steps supported by this version upgrader. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="upgradeSteps" /> is null. </exception>
        /// <exception cref="ArgumentException"> <paramref name="upgradeSteps" /> is an empty array or contains the same source version multiple times. </exception>
        public SQLiteDatabaseVersionUpgrader (params SQLiteDbVersionUpgradeStep[] upgradeSteps)
            : this((IEnumerable<SQLiteDbVersionUpgradeStep>)upgradeSteps)
        {
        }

        #endregion




        #region Instance Properties/Indexer

        private List<SQLiteDbVersionUpgradeStep> UpgradeSteps { get; }

        #endregion




        #region Instance Methods

        /// <summary>
        ///     Gets the list of available upgrade steps.
        /// </summary>
        /// <returns>
        ///     The list of available upgrade steps.
        ///     The list is never empty.
        /// </returns>
        public List<SQLiteDbVersionUpgradeStep> GetUpgradeSteps () => new List<SQLiteDbVersionUpgradeStep>(this.UpgradeSteps);

        #endregion




        #region Overrides

        /// <inheritdoc />
        public override bool RequiresScriptLocator => this.UpgradeSteps.Any(x => x.RequiresScriptLocator);

        /// <inheritdoc />
        public override int GetMaxVersion (SQLiteDatabaseManager manager) => this.UpgradeSteps.Select(x => x.SourceVersion).Max() + 1;

        /// <inheritdoc />
        public override int GetMinVersion (SQLiteDatabaseManager manager) => this.UpgradeSteps.Select(x => x.SourceVersion).Min();

        /// <inheritdoc />
        public override bool Upgrade (SQLiteDatabaseManager manager, int sourceVersion)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (sourceVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceVersion));
            }

            try
            {
                //TODO: Log: this.Log(LogLevel.Debug, "Beginning SQLite database upgrade step: SourceVersion=[{0}]; Connection=[{1}]", sourceVersion, manager.Configuration.ConnectionString);

                SQLiteDbVersionUpgradeStep upgradeStep = this.UpgradeSteps.FirstOrDefault(x => x.SourceVersion == sourceVersion);
                if (upgradeStep == null)
                {
                    throw new Exception("No upgrade step found for source version: " + sourceVersion);
                }

                using (SQLiteConnection connection = manager.CreateInternalConnection(null, false))
                {
                    using (SQLiteTransaction transaction = upgradeStep.RequiresTransaction ? connection.BeginTransaction(IsolationLevel.Serializable) : null)
                    {
                        upgradeStep.Execute(manager, connection, transaction);
                        transaction?.Commit();
                    }
                }

                //TODO: Log: this.Log(LogLevel.Debug, "Finished SQLite database upgrade step: SourceVersion=[{0}]; Connection=[{1}]", sourceVersion, manager.Configuration.ConnectionString);

                return true;
            }
            catch (Exception exception)
            {
                //TODO: Log: this.Log(LogLevel.Error, "SQLite database upgrade step failed:{0}{1}", Environment.NewLine, exception.ToDetailedString());
                return false;
            }
        }

        #endregion
    }
}
