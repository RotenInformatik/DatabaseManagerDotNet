﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

using RI.Abstractions.Logging;
using RI.DatabaseManager.Batches;
using RI.DatabaseManager.Builder.Options;
using RI.DatabaseManager.Cleanup;
using RI.DatabaseManager.Manager;




namespace RI.DatabaseManager.Creation
{
    /// <summary>
    ///     Boilerplate implementation of <see cref="IDbCreator" /> and <see cref="IDbCreator{TConnection,TTransaction,TParameterTypes}" />.
    /// </summary>
    /// <typeparam name="TConnection"> The database connection type. </typeparam>
    /// <typeparam name="TTransaction"> The database transaction type. </typeparam>
    /// <typeparam name="TParameterTypes"> The database command parameter type. </typeparam>
    /// <remarks>
    ///     <note type="implement">
    ///         It is recommended that database creator implementations use this base class as it already implements most of the database-independent logic defined by <see cref="IDbCreator" /> and <see cref="IDbCreator{TConnection,TTransaction,TParameterTypes}" />.
    ///     </note>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public abstract class DbCreatorBase <TConnection, TTransaction, TParameterTypes> : IDbCreator<TConnection, TTransaction, TParameterTypes>
        where TConnection : DbConnection
        where TTransaction : DbTransaction
        where TParameterTypes : Enum
    {
        #region Instance Constructor/Destructor

        /// <summary>
        ///     Creates a new instance of <see cref="DbCreatorBase{TConnection,TTransaction,TParameterTypes}" />.
        /// </summary>
        /// <param name="options"> The used database manager options. </param>
        /// <param name="logger"> The used logger. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="options" /> or <paramref name="logger" /> is null. </exception>
        protected DbCreatorBase(IDbManagerOptions options, ILogger logger)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            this.Options = options;
            this.Logger = logger;
        }

        #endregion




        #region Instance Properties/Indexer

        /// <summary>
        ///     Gets the used database manager options.
        /// </summary>
        /// <value>
        ///     The used database manager options.
        /// </value>
        protected IDbManagerOptions Options { get; }

        /// <summary>
        ///     Gets the used logger.
        /// </summary>
        /// <value>
        ///     The used logger.
        /// </value>
        protected ILogger Logger { get; }

        /// <summary>
        ///     Writes a log message.
        /// </summary>
        /// <param name="level"> The log level of the log message. </param>
        /// <param name="format"> Log message (with optional string expansion arguments such as <c> {0} </c>, <c> {1} </c>, etc. which are expanded by <paramref name="args" />). </param>
        /// <param name="args"> Optional message arguments expanded into <paramref name="format" />. </param>
        protected void Log(LogLevel level, string format, params object[] args)
        {
            this.Logger.Log(level, this.GetType()
                                       .Name, null, format, args);
        }

        /// <summary>
        ///     Writes a log message.
        /// </summary>
        /// <param name="level"> The log level of the log message. </param>
        /// <param name="exception"> Exception associated with the log message. </param>
        /// <param name="format"> Optional log message (with optional string expansion arguments such as <c> {0} </c>, <c> {1} </c>, etc. which are expanded by <paramref name="args" />). </param>
        /// <param name="args"> Optional message arguments expanded into <paramref name="format" />. </param>
        protected void Log(LogLevel level, Exception exception, string format, params object[] args)
        {
            this.Logger.Log(level, this.GetType()
                                       .Name, exception, format, args);
        }

        #endregion

        /// <summary>
        /// Gets the creation step as batch.
        /// </summary>
        /// <param name="manager"> The used database manager. </param>
        /// <param name="steps">The available step (batch). </param>
        /// <returns>
        /// true if the creation step could be retrieved, false otherwise.
        /// </returns>
        /// <remarks>
        /// <note type="implement">
        /// The default implementation uses <see cref="ISupportDefaultDatabaseCreation.GetDefaultCreationScript"/> to retrieve all creation commands which are converted to batches (one batch per command).
        /// </note>
        /// </remarks>
        protected virtual bool GetCreationSteps(IDbManager<TConnection, TTransaction, TParameterTypes> manager, out IDbBatch<TConnection, TTransaction, TParameterTypes> steps)
        {
            steps = null;
            DbBatchTransactionRequirement transactionRequirement = DbBatchTransactionRequirement.DontCare;
            IsolationLevel? isolationLevel = null;

            string[] commands = (this.Options as ISupportDefaultDatabaseCreation)?.GetDefaultCreationScript(out transactionRequirement, out isolationLevel);

            if (commands == null)
            {
                return false;
            }

            if (commands.Length == 0)
            {
                return false;
            }

            IDbBatch<TConnection, TTransaction, TParameterTypes> batch = manager.CreateBatch();

            foreach (string command in commands)
            {
                batch.AddScript(command, transactionRequirement, isolationLevel);
            }

            steps = batch;

            return true;
        }




        #region Interface: IDbCleanupProcessor<TConnection,TTransaction>

        /// <inheritdoc />
        public virtual bool Create (IDbManager<TConnection, TTransaction, TParameterTypes> manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            IDbBatch<TConnection, TTransaction, TParameterTypes> steps = null;
            bool result = this.GetCreationSteps(manager, out steps);

            if ((!result) || (steps == null))
            {
                this.Log(LogLevel.Error, "No create steps available to create database.");
                return false;
            }

            try
            {
                this.Log(LogLevel.Information, "Beginning create database.");

                result = manager.ExecuteBatch(steps, false, true);

                if (result)
                {
                    this.Log(LogLevel.Information, "Finished create database.");
                }
                else
                {
                    this.Log(LogLevel.Error, "Failed create database.");
                }

                return result;
            }
            catch (Exception exception)
            {
                this.Log(LogLevel.Error, "Failed create database:{0}{1}", Environment.NewLine, exception.ToString());
                return false;
            }
        }

        /// <inheritdoc />
        bool IDbCreator.Create (IDbManager manager) => this.Create((IDbManager<TConnection, TTransaction, TParameterTypes>)manager);

        #endregion
    }
}