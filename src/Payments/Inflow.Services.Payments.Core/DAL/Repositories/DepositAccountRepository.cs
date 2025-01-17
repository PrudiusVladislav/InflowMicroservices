﻿using System;
using System.Threading.Tasks;
using Inflow.Services.Payments.Core.Deposits.Domain.Entities;
using Inflow.Services.Payments.Core.Deposits.Domain.Repositories;
using Inflow.Services.Payments.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Inflow.Services.Payments.Core.DAL.Repositories;

internal class DepositAccountRepository : IDepositAccountRepository
{
    private readonly PaymentsDbContext _context;
    private readonly DbSet<DepositAccount> _depositAccounts;

    public DepositAccountRepository(PaymentsDbContext context)
    {
        _context = context;
        _depositAccounts = _context.DepositAccounts;
    }

    public  Task<DepositAccount> GetAsync(Guid id)
        => _depositAccounts.SingleOrDefaultAsync(x => x.Id == id);

    public Task<DepositAccount> GetAsync(Guid customerId, Currency currency)
        => _depositAccounts.SingleOrDefaultAsync(x => x.CustomerId == customerId && x.Currency.Equals(currency));

    public async Task AddAsync(DepositAccount depositAccount)
    {
        await _depositAccounts.AddAsync(depositAccount);
        await _context.SaveChangesAsync();
    }
}