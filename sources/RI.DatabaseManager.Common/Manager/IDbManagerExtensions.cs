﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

using RI.DatabaseManager.Batches;
using RI.DatabaseManager.Creation;
using RI.DatabaseManager.Upgrading;




namespace RI.DatabaseManager.Manager
{
    /// <summary>
    ///     Provides utility/extension methods for the <see cref="IDbManager" /> and <see cref="IDbManager{TConnection,TTransaction,TParameterTypes}" /> type.
    /// </summary>
    /// <threadsafety static="false" instance="false" />
    public static class IDbManagerExtensions
    {
        #region Static Methods

        /// <summary>
        ///     Gets whether the database is in a state where it can be upgraded to a newer version.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <returns>
        ///     true if the database supports upgrading, is in a ready state, and the current version is less than the maximum supported version, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> is null </exception>
        public static bool CanUpgrade (this IDbManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.SupportsUpgrade && manager.IsReady() && (manager.Version > 0) && (manager.Version < manager.MaxVersion);
        }

        /// <summary>
        ///     Gets whether the database is in a state where it can be created to its initial version.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <returns>
        ///     true if the database supports creation, is in the new state, and the current version is zero, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> is null </exception>
        public static bool CanCreate(this IDbManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.SupportsCreate && (manager.State  == DbState.New) && (manager.Version == 0);
        }

        /// <summary>
        ///     Gets whether the database is in a state where it can be cleaned up.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <returns>
        ///     true if the database supports upgrading and is in a ready state.
        /// </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> is null </exception>
        public static bool CanCleanup(this IDbManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.SupportsUpgrade && manager.IsReady();
        }

        /// <summary>
        ///     Creates a new connection which can be used to work with the database.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <returns>
        ///     The newly created and already opened connection or null if the connection could not be created.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The connection is not read-only.
        ///     </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> is null </exception>
        /// <exception cref="InvalidOperationException"> The database is not in a ready state. </exception>
        public static DbConnection CreateConnection (this IDbManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.CreateConnection(false);
        }

        /// <inheritdoc cref="CreateConnection(IDbManager)" />
        public static TConnection CreateConnection <TConnection, TTransaction, TParameterTypes> (this IDbManager<TConnection, TTransaction, TParameterTypes> manager)
            where TConnection : DbConnection
            where TTransaction : DbTransaction
            where TParameterTypes : Enum
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.CreateConnection(false);
        }

        /// <summary>
        ///     Creates a new transaction which can be used to work with the database.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <returns>
        ///     The newly created transaction with its underlying connection already opened or null if the transaction or connection could not be created.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The underlying connection of the transaction is not read-only and <see cref="IsolationLevel.ReadCommitted"/> is used as transaction level.
        ///     </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> is null </exception>
        /// <exception cref="InvalidOperationException"> The database is not in a ready state. </exception>
        public static DbTransaction CreateTransaction (this IDbManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.CreateTransaction(false, IsolationLevel.ReadCommitted);
        }

        /// <inheritdoc cref="CreateTransaction(IDbManager)" />
        public static TTransaction CreateTransaction <TConnection, TTransaction, TParameterTypes> (this IDbManager<TConnection, TTransaction, TParameterTypes> manager)
            where TConnection : DbConnection
            where TTransaction : DbTransaction
            where TParameterTypes : Enum
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.CreateTransaction(false, IsolationLevel.ReadCommitted);
        }

        /// <summary>
        ///     Executes a batch.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <param name="batch"> The batch to execute. </param>
        /// <returns>
        ///     true if the batch was executed successfully, false otherwise.
        ///     Details about failures should be written to logs and/or into properties of the executed batch.
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The used connection to execute the batch is not read-only.
        ///     </para>
        ///     <para>
        ///         The database version and state are not re-detected after the batch has been executed.
        ///     </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> or <paramref name="batch" /> is null </exception>
        /// <exception cref="InvalidOperationException"> The database is not in a ready state. </exception>
        public static bool ExecuteBatch (this IDbManager manager, IDbBatch batch)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.ExecuteBatch(batch, false, false);
        }

        /// <inheritdoc cref="ExecuteBatch(IDbManager,IDbBatch)" />
        public static bool ExecuteBatch<TConnection, TTransaction, TParameterTypes>(this IDbManager<TConnection, TTransaction, TParameterTypes> manager, IDbBatch<TConnection, TTransaction, TParameterTypes> batch)
            where TConnection : DbConnection
            where TTransaction : DbTransaction
            where TParameterTypes : Enum
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.ExecuteBatch(batch, false, false);
        }

        /// <summary>
        ///     Gets a batch (for later execution) of a specified name using the configured <see cref="IDbBatchLocator" />.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <param name="name"> The name of the batch. </param>
        /// <returns>
        ///     The batch or null if the batch of the specified name could not be found.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The batch locators default command separator is used and the batch is preprocessed if applicable.
        ///     </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> or <paramref name="name" /> is null. </exception>
        /// <exception cref="ArgumentException"> <paramref name="name" /> is an empty string. </exception>
        public static IDbBatch GetBatch (this IDbManager manager, string name)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.GetBatch(name, null);
        }

        /// <inheritdoc cref="GetBatch(IDbManager,string)" />
        public static IDbBatch<TConnection, TTransaction, TParameterTypes> GetBatch<TConnection, TTransaction, TParameterTypes>(this IDbManager<TConnection, TTransaction, TParameterTypes> manager, string name)
            where TConnection : DbConnection
            where TTransaction : DbTransaction
            where TParameterTypes : Enum
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.GetBatch(name, null);
        }

        /// <summary>
        ///     Gets all available batches using the configured <see cref="IDbBatchLocator" />.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <param name="commandSeparator"> The string which is used as the separator to separate commands within the batch or null if the batch locators default separators are to be used. </param>
        /// <returns> </returns>
        /// <exception cref="ArgumentException"> <paramref name="commandSeparator" /> is an empty string. </exception>
        public static IDictionary<string, IDbBatch> GetBatches (this IDbManager manager, string commandSeparator = null)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (commandSeparator != null)
            {
                if (string.IsNullOrWhiteSpace(commandSeparator))
                {
                    throw new ArgumentException("The string argument is empty.", nameof(commandSeparator));
                }
            }

            ISet<string> names = manager.GetBatchNames();
            Dictionary<string, IDbBatch> batches = new Dictionary<string, IDbBatch>();

            foreach (string name in names)
            {
                IDbBatch batch = manager.GetBatch(name, commandSeparator);

                if (batch != null)
                {
                    batches.Add(name, batch);
                }
            }

            return batches;
        }

        /// <inheritdoc cref="GetBatches(IDbManager,string)" />
        public static IDictionary<string, IDbBatch<TConnection, TTransaction, TParameterTypes>> GetBatches<TConnection, TTransaction, TParameterTypes>(this IDbManager<TConnection, TTransaction, TParameterTypes> manager, string commandSeparator = null)
            where TConnection : DbConnection
            where TTransaction : DbTransaction
            where TParameterTypes : Enum
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (commandSeparator != null)
            {
                if (string.IsNullOrWhiteSpace(commandSeparator))
                {
                    throw new ArgumentException("The string argument is empty.", nameof(commandSeparator));
                }
            }

            ISet<string> names = manager.GetBatchNames();
            Dictionary<string, IDbBatch<TConnection, TTransaction, TParameterTypes>> batches = new Dictionary<string, IDbBatch<TConnection, TTransaction, TParameterTypes>>();

            foreach (string name in names)
            {
                IDbBatch<TConnection, TTransaction, TParameterTypes> batch = manager.GetBatch(name, commandSeparator);

                if (batch != null)
                {
                    batches.Add(name, batch);
                }
            }

            return batches;
        }

        /// <summary>
        ///     Gets whether the database is ready for use and connections and transactions can be created.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <returns>
        ///     true if the database is in <see cref="DbState.ReadyUnknown" />, <see cref="DbState.ReadyNew" />, or <see cref="DbState.ReadyOld" /> state, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> is null </exception>
        public static bool IsReady (this IDbManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return (manager.State == DbState.ReadyNew) || (manager.State == DbState.ReadyOld) || (manager.State == DbState.ReadyUnknown);
        }

        /// <summary>
        ///     Performs an upgrade to highest supported version using the configured <see cref="IDbVersionUpgrader" />.
        /// </summary>
        /// <returns>
        ///     true if the upgrade was successful, false otherwise.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <param name="manager"> The used database manager. </param>
        /// <remarks>
        ///     <note type="implement">
        ///         <see cref="IDbManager.State" /> and <see cref="IDbManager.Version" /> are updated to reflect the state and version of the database after upgrade.
        ///     </note>
        ///     <note type="implement">
        ///         If <see cref="IDbManager.MaxVersion" /> is the same as <see cref="Version" />, nothing should be done.
        ///     </note>
        ///     <note type="implement">
        ///         Upgrading is to be performed incrementally, upgrading from n to n+1 until the desired target version, as specified by <see cref="IDbManager.MaxVersion" />, is reached.
        ///     </note>
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> is null </exception>
        /// <exception cref="InvalidOperationException"> The database is not in a ready state. </exception>
        /// <exception cref="NotSupportedException"> Upgrading is not supported by the database manager or no <see cref="IDbVersionUpgrader" /> is configured. </exception>
        public static bool Upgrade (this IDbManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            return manager.Upgrade(manager.MaxVersion);
        }

        /// <summary>
        ///     Creates the database and performs an upgrade to highest supported version using the configured <see cref="IDbCreator"/> and <see cref="IDbVersionUpgrader" />.
        /// </summary>
        /// <returns>
        ///     true if the creation and upgrade was successful, false otherwise.
        ///     Details about failures should be written to logs.
        /// </returns>
        /// <param name="manager"> The used database manager. </param>
        /// <remarks>
        ///     <note type="implement">
        ///         <see cref="IDbManager.State" /> and <see cref="IDbManager.Version" /> are updated to reflect the state and version of the database after upgrade.
        ///     </note>
        ///     <note type="implement">
        ///         If the database already exists, only the upgrade shall be performed (if necessary).
        ///     </note>
        ///     <note type="implement">
        ///         If <see cref="IDbManager.MaxVersion" /> is the same as <see cref="Version" />, no upgrade should be done.
        ///     </note>
        ///     <note type="implement">
        ///         Upgrading is to be performed incrementally, upgrading from n to n+1 until the desired target version, as specified by <see cref="IDbManager.MaxVersion" />, is reached.
        ///     </note>
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="manager" /> is null </exception>
        /// <exception cref="InvalidOperationException"> The database is not in a ready or the new state. </exception>
        /// <exception cref="NotSupportedException"> Creating or upgrading is not supported by the database manager or no <see cref="IDbCreator"/> or <see cref="IDbVersionUpgrader" /> is configured. </exception>
        public static bool CreateAndUpgrade(this IDbManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (!manager.Create())
            {
                return false;
            }

            return manager.Upgrade();
        }

        #endregion
    }
}
