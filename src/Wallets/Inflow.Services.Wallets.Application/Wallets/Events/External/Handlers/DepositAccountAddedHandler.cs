﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Events;
using Inflow.Services.Wallets.Application.Services;
using Inflow.Services.Wallets.Core.Wallets.Entities;
using Inflow.Services.Wallets.Core.Wallets.Repositories;
using Microsoft.Extensions.Logging;

namespace Inflow.Services.Wallets.Application.Wallets.Events.External.Handlers;

internal sealed class DepositAccountAddedHandler : IEventHandler<DepositAccountAdded>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;
    private readonly ILogger<DepositAccountAddedHandler> _logger;

    public DepositAccountAddedHandler(IWalletRepository  walletRepository, IClock clock,
        IMessageBroker messageBroker, ILogger<DepositAccountAddedHandler> logger)
    {
        _walletRepository = walletRepository;
        _clock = clock;
        _messageBroker = messageBroker;
        _logger = logger;
    }

    public async Task HandleAsync(DepositAccountAdded @event, CancellationToken cancellationToken = default)
    {
        var wallet = new Wallet(Guid.NewGuid(), @event.CustomerId, @event.Currency, _clock.CurrentDate());
        await _walletRepository.AddAsync(wallet);
        await _messageBroker.PublishAsync(new WalletAdded(wallet.Id, wallet.OwnerId, wallet.Currency));
        _logger.LogInformation($"Created a new wallet with ID: '{wallet.Id}', owner ID: '{wallet.OwnerId}', " +
                               $"currency: '{@event.Currency}' for deposit account with ID: '{@event.AccountId}'.");
    }
}