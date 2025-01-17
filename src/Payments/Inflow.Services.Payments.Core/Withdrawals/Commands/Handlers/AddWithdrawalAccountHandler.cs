﻿using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Commands;
using Inflow.Services.Payments.Core.Services;
using Inflow.Services.Payments.Core.Withdrawals.Domain.Entities;
using Inflow.Services.Payments.Core.Withdrawals.Domain.Repositories;
using Inflow.Services.Payments.Core.Withdrawals.Events;
using Inflow.Services.Payments.Core.Withdrawals.Exceptions;
using Inflow.Services.Payments.Shared.Exceptions;
using Inflow.Services.Payments.Shared.Repositories;
using Inflow.Services.Payments.Shared.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Inflow.Services.Payments.Core.Withdrawals.Commands.Handlers;

internal sealed class AddWithdrawalAccountHandler : ICommandHandler<AddWithdrawalAccount>
{
    private readonly IWithdrawalAccountRepository _withdrawalAccountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMessageBroker _messageBroker;
    private readonly IClock _clock;
    private readonly ILogger<AddWithdrawalAccountHandler> _logger;

    public AddWithdrawalAccountHandler(IWithdrawalAccountRepository withdrawalAccountRepository,
        ICustomerRepository customerRepository, IMessageBroker messageBroker, IClock clock,
        ILogger<AddWithdrawalAccountHandler> logger)
    {
        _withdrawalAccountRepository = withdrawalAccountRepository;
        _customerRepository = customerRepository;
        _messageBroker = messageBroker;
        _clock = clock;
        _logger = logger;
    }

    public async Task HandleAsync(AddWithdrawalAccount command, CancellationToken cancellationToken = default)
    {
        _ = new Currency(command.Currency);
        _ = new Iban(command.Iban);
            
        var customer = await _customerRepository.GetAsync(command.CustomerId);
        if (customer is null)
        {
            throw new CustomerNotFoundException(command.CustomerId);
        }

        if (!customer.IsActive || !customer.IsVerified)
        {
            throw new CustomerNotActiveException(command.CustomerId);
        }
            
        if (await _withdrawalAccountRepository.ExistsAsync(command.CustomerId, command.Currency))
        {
            throw new WithdrawalAccountAlreadyExistsException(command.CustomerId, command.Currency);
        }

        var account = new WithdrawalAccount(command.AccountId, command.CustomerId, command.Currency, command.Iban,
            _clock.CurrentDate());
        await _withdrawalAccountRepository.AddAsync(account);
        await _messageBroker.PublishAsync(new WithdrawalAccountAdded(command.AccountId, command.CustomerId,
            command.Currency));
        _logger.LogInformation($"Added a withdrawal account with ID: '{command.AccountId}' " +
                               $"for customer with ID: '{command.CustomerId}'.");
    }
}