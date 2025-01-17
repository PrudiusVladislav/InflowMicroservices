﻿using System;
using Inflow.Services.Payments.Shared.Exceptions;

namespace Inflow.Services.Payments.Core.Withdrawals.Exceptions;

internal class InvalidWithdrawalAccountCurrencyException : CustomException
{
    public Guid AccountId { get; }
    public string Currency { get; }

    public InvalidWithdrawalAccountCurrencyException(Guid accountId, string currency)
        : base($"Withdrawal account with ID: '{accountId}' has invalid currency: '{currency}'.")
    {
        AccountId = accountId;
        Currency = currency;
    }
}