﻿using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Commands;
using Convey.CQRS.Events;
using Inflow.Services.Payments.Core.Deposits.Domain.Entities;
using Inflow.Services.Payments.Core.Deposits.Domain.Repositories;
using Inflow.Services.Payments.Core.Deposits.Events;
using Inflow.Services.Payments.Core.Deposits.Exceptions;
using Inflow.Services.Payments.Core.Services;
using Microsoft.Extensions.Logging;

namespace Inflow.Services.Payments.Core.Deposits.Commands.Handlers;

internal sealed class CompleteDepositHandler : ICommandHandler<CompleteDeposit>
{
    private readonly IDepositRepository _depositRepository;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;
    private readonly ILogger<CompleteDepositHandler> _logger;

    public CompleteDepositHandler(IDepositRepository depositRepository, IClock clock, IMessageBroker messageBroker,
        ILogger<CompleteDepositHandler> logger)
    {
        _depositRepository = depositRepository;
        _clock = clock;
        _messageBroker = messageBroker;
        _logger = logger;
    }

    public async Task HandleAsync(CompleteDeposit command, CancellationToken cancellationToken = default)
    {
        var deposit = await _depositRepository.GetAsync(command.DepositId);
        if (deposit is null)
        {
            throw new DepositNotFoundException(command.DepositId);
        }
            
        _logger.LogInformation($"Started processing a deposit with ID: '{command.DepositId}'...");
        var (isCompleted, @event) = TryComplete(deposit, command.Secret);
        var now = _clock.CurrentDate();
        if (isCompleted)
        {
            deposit.Complete(now);
        }
        else
        {
            deposit.Reject(now);
        }
            
        await _depositRepository.UpdateAsync(deposit);
        await _messageBroker.PublishAsync(@event);
        _logger.LogInformation($"{(isCompleted ? "Completed" : "Rejected")} " +
                               $"a deposit with ID: '{command.DepositId}'.");
    }
        
    private static (bool isCompleted, IEvent @event) TryComplete(Deposit deposit, string secret)
    {
        // This could be refactored to an application service with checksum validation etc.
        return secret == "secret"
            ? (true, new DepositCompleted(deposit.Id, deposit.Account.CustomerId,
                deposit.Account.Currency, deposit.Amount))
            : (false, new DepositRejected(deposit.Id, deposit.Account.CustomerId,
                deposit.Account.Currency, deposit.Amount));
    }
}