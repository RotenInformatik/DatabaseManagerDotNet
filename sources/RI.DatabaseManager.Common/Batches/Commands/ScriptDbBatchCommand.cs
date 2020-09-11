﻿using System;
using System.Data.Common;




namespace RI.DatabaseManager.Batches.Commands
{
    /// <summary>
    ///     A batch command which provides a script to be executed.
    /// </summary>
    /// <threadsafety static="false" instance="false" />
    public sealed class ScriptDbBatchCommand : IDbBatchCommand
    {
        #region Instance Constructor/Destructor

        /// <summary>
        ///     Creates a new instance of <see cref="CallbackDbBatchCommand{TConnection,TTransaction}" />.
        /// </summary>
        /// <param name="script"> The database script. </param>
        /// <param name="transactionRequirement"> The optional transaction requirement specification. Default values is <see cref="DbBatchTransactionRequirement.DontCare" />. </param>
        public ScriptDbBatchCommand (string script, DbBatchTransactionRequirement transactionRequirement = DbBatchTransactionRequirement.DontCare)
        {
            this.Script = string.IsNullOrWhiteSpace(script) ? null : script;
            this.TransactionRequirement = transactionRequirement;
        }

        #endregion




        #region Interface: IDbBatchCommand

        /// <inheritdoc />
        Func<DbConnection, DbTransaction, object> IDbBatchCommand.Code => null;

        /// <inheritdoc />
        public object Result { get; set; }

        /// <inheritdoc />
        public string Script { get; }

        /// <inheritdoc />
        public DbBatchTransactionRequirement TransactionRequirement { get; }

        /// <inheritdoc />
        public bool WasExecuted { get; set; }

        #endregion
    }
}
