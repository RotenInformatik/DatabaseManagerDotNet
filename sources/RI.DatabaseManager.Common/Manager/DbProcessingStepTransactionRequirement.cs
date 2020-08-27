﻿using System;




namespace RI.DatabaseManager.Manager
{
	/// <summary>
	///     Describes the transaction requirements of a processing sub-step.
	/// </summary>
	[Serializable]
	public enum DbProcessingStepTransactionRequirement
	{
		/// <summary>
		///     The sub-step has no transaction requirement and can be used with or without a transaction.
		/// </summary>
		DontCare = 0,

		/// <summary>
		///     The sub-step requires a transaction.
		/// </summary>
		Required = 1,

		/// <summary>
		///     The sub-step cannot be used with a transaction.
		/// </summary>
		Disallowed = 2,
	}
}
