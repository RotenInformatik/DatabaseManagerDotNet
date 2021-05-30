﻿using System;
using System.Data;




namespace RI.DatabaseManager.Batches.Locators
{
    /// <summary>
    ///     Optional attribute to override batch name and/or transaction requirement of
    ///     <see cref="ICallbackBatch{TConnection,TTransaction,TParameterTypes}" /> implementations.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         See <see cref="AssemblyCallbackBatchLocator{TConnection,TTransaction,TParameterTypes}" /> for more details.
    ///     </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class),]
    public sealed class CallbackBatchAttribute : Attribute
    {
        #region Instance Properties/Indexer

        /// <summary>
        ///     Gets or sets the isolation level requirement.
        /// </summary>
        /// <value>
        ///     The isolation level requirement.
        /// </value>
        public IsolationLevel IsolationLevel
        {
            get => this.IsolationLevelInternal.GetValueOrDefault(IsolationLevel.Unspecified);
            set => this.IsolationLevelInternal = value == IsolationLevel.Unspecified ? null : value;
        }

        /// <summary>
        ///     Gets or sets the name of the batch.
        /// </summary>
        /// <value>
        ///     The name of the batch or null if the batch name is not specified or overriden respectively.
        /// </value>
        public string Name { get; set; } = null;

        /// <summary>
        ///     Gets or sets the transaction requirement.
        /// </summary>
        /// <value>
        ///     The transaction requirement.
        /// </value>
        public DbBatchTransactionRequirement TransactionRequirement { get; set; } =
            DbBatchTransactionRequirement.DontCare;

        internal IsolationLevel? IsolationLevelInternal { get; set; } = null;

        #endregion
    }
}
