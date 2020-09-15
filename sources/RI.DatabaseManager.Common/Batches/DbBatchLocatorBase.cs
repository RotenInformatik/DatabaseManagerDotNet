﻿using System;
using System.Collections.Generic;
using System.Linq;

using RI.Abstractions.Logging;




namespace RI.DatabaseManager.Batches
{
    /// <summary>
    ///     Boilerplate implementation of <see cref="IDbBatchLocator" />.
    /// </summary>
    /// <remarks>
    ///     <note type="implement">
    ///         It is recommended that database batch locator implementations use this base class as it already implements already some boilerplate code.
    ///     </note>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public abstract class DbBatchLocatorBase : IDbBatchLocator
    {
        /// <summary>
        /// Creates a new instance of <see cref="DbBatchLocatorBase"/>.
        /// </summary>
        /// <param name="logger"> The used logger. </param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
        protected DbBatchLocatorBase (ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            this.Logger = logger;
        }

        /// <summary>
        /// Gets the used logger.
        /// </summary>
        /// <value>
        /// The used logger.
        /// </value>
        protected ILogger Logger { get; }

        /// <inheritdoc />
        IDbBatch IDbBatchLocator.GetBatch(string name, string commandSeparator)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Argument is an empty string.", nameof(name));
            }

            if (commandSeparator != null)
            {
                if (string.IsNullOrWhiteSpace(commandSeparator))
                {
                    throw new ArgumentException("Argument is an empty string.", nameof(commandSeparator));
                }
            }

            commandSeparator ??= this.DefaultCommandSeparator;
            commandSeparator = string.IsNullOrWhiteSpace(commandSeparator) ? null : commandSeparator;

            DbBatch batch = new DbBatch();

            if (!this.FillBatch(batch, name, commandSeparator))
            {
                return null;
            }

            return batch;
        }

        /// <inheritdoc />
        ISet<string> IDbBatchLocator.GetNames()
        {
            return new HashSet<string>(this.GetNames()?.Where(x => x != null) ?? new string[0], this.DefaultNameComparer);
        }

        /// <summary>
        /// Gets the names of all available batches this batch locator can retrieve.
        /// </summary>
        /// <returns>
        /// The sequence with the names of all available batches.
        ///     Details about failures should be written to logs.
        /// </returns>
        protected abstract IEnumerable<string> GetNames ();

        /// <summary>
        /// Retrieves the batch with a specified name.
        /// </summary>
        /// <param name="batch">The prepared batch instance which is to be filled with code or scripts associated with the specified name.</param>
        /// <param name="name"> The name of the batch. </param>
        /// <param name="commandSeparator"> The string which is used as the separator to separate commands within the batch or null if neither a specific command separator nor a value through <see cref="DefaultCommandSeparator"/> is provided. </param>
        /// <returns>
        /// true if the batch could be successfully retrieved, false otherwise.
        ///     Details about failures should be written to logs.
        /// </returns>
        protected abstract bool FillBatch (IDbBatch batch, string name, string commandSeparator);

        /// <summary>
        /// Gets the default command separator string used by this batch locator.
        /// </summary>
        /// <value>
        /// The default command separator string used by this batch locator or null if this batch locator does not support command separators.
        /// </value>
        /// <remarks>
        ///<note type="implement">
        /// The default implementation returns <c>GO</c>.
        /// </note>
        /// </remarks>
        protected virtual string DefaultCommandSeparator => "GO";

        /// <summary>
        /// Gets the string comparer used for comparing batch names.
        /// </summary>
        /// <value>
        /// The string comparer used for comparing batch names.
        /// </value>
        /// <remarks>
        ///<note type="implement">
        /// The default implementation returns <see cref="StringComparer.InvariantCultureIgnoreCase"/>.
        /// </note>
        ///<note type="implement">
        ///         <see cref="DefaultNameComparer" /> should never return null.
        /// </note>
        /// </remarks>
        protected virtual StringComparer DefaultNameComparer => StringComparer.InvariantCultureIgnoreCase;

        protected virtual List<string> SeparateScriptCommands (string script, string commandSeparator)
        {
            //TODO: separation
            throw new NotImplementedException();
        }
    }
}
