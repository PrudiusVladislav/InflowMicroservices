﻿using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Events;
using Inflow.Services.Payments.Core.Services;
using Inflow.Services.Payments.Core.Withdrawals.Domain.Repositories;
using Inflow.Services.Payments.Core.Withdrawals.Exceptions;
using Inflow.Services.Payments.Core.Withdrawals.Services;
using Microsoft.Extensions.Logging;

namespace Inflow.Services.Payments.Core.Withdrawals.Events.External.Handlers;

internal sealed class DeductFundsRejectedHandler : IEventHandler<DeductFundsRejected>
{
    private const string TransferName = "withdrawal";
    private readonly IWithdrawalRepository _withdrawalRepository;
    private readonly IMessageBroker _messageBroker;
    private readonly IWithdrawalMetadataResolver _metadataResolver;
    private readonly IClock _clock;
    private readonly ILogger<FundsDeductedHandler> _logger;

    public DeductFundsRejectedHandler(IWithdrawalRepository withdrawalRepository, IMessageBroker messageBroker,
        IWithdrawalMetadataResolver metadataResolver, IClock clock, ILogger<FundsDeductedHandler> logger)
    {
        _withdrawalRepository = withdrawalRepository;
        _messageBroker = messageBroker;
        _metadataResolver = metadataResolver;
        _clock = clock;
        _logger = logger;
    }
        
    public async Task HandleAsync(DeductFundsRejected @event, CancellationToken cancellationToken = default)
    {
        if (@event.TransferName != TransferName)
        {
            return;
        }

        var withdrawalId = _metadataResolver.TryResolveWithdrawalId(@event.TransferMetadata);
        if (!withdrawalId.HasValue)
        {
            return;
        }

        var withdrawal = await _withdrawalRepository.GetAsync(withdrawalId.Value);
        if (withdrawal is null)
        {
            throw new WithdrawalNotFoundException(withdrawalId.Value);
        }
            
        withdrawal.Reject(_clock.CurrentDate());
        await _withdrawalRepository.UpdateAsync(withdrawal);
        await _messageBroker.PublishAsync(new WithdrawalRejected(withdrawal.Id, withdrawal.Account.CustomerId,
            withdrawal.Currency, withdrawal.Amount));
        _logger.LogInformation($"Rejected withdrawal with ID: '{withdrawal.Id}'.");
    }
}