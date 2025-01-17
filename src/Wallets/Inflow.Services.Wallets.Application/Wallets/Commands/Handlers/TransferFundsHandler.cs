﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Commands;
using Inflow.Services.Wallets.Application.Services;
using Inflow.Services.Wallets.Application.Wallets.Events;
using Inflow.Services.Wallets.Application.Wallets.Exceptions;
using Inflow.Services.Wallets.Core.Wallets.Entities;
using Inflow.Services.Wallets.Core.Wallets.Exceptions;
using Inflow.Services.Wallets.Core.Wallets.Repositories;
using Inflow.Services.Wallets.Core.Wallets.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Inflow.Services.Wallets.Application.Wallets.Commands.Handlers;

internal class TransferFundsHandler : ICommandHandler<TransferFunds>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;
    private readonly ILogger<TransferFundsHandler> _logger;

    public TransferFundsHandler(IWalletRepository walletRepository, IClock clock, IMessageBroker messageBroker,
        ILogger<TransferFundsHandler> logger)
    {
        _walletRepository = walletRepository;
        _clock = clock;
        _messageBroker = messageBroker;
        _logger = logger;
    }

    public async Task HandleAsync(TransferFunds command, CancellationToken cancellationToken = default)
    {
        var amount = new Amount(command.Amount);
        var ownerWallet = await _walletRepository.GetAsync(command.OwnerWalletId);
        if (ownerWallet is null || ownerWallet.OwnerId != command.OwnerId)
        {
            throw new WalletNotFoundException(command.OwnerWalletId);
        }

        if (ownerWallet.Currency != command.Currency)
        {
            throw new InvalidTransferCurrencyException(command.Currency);
        }

        var receiverWallet = await _walletRepository.GetAsync(command.ReceiverWalletId);
        if (receiverWallet is null)
        {
            throw new WalletNotFoundException(command.ReceiverWalletId);
        }

        if (receiverWallet.Currency != command.Currency)
        {
            throw new InvalidTransferCurrencyException(command.Currency);
        }

        var now = _clock.CurrentDate();
        var transfers = ownerWallet.TransferFunds(receiverWallet, amount, now);
        var outgoingTransfer = transfers.OfType<OutgoingTransfer>().Single();
        var incomingTransfer = transfers.OfType<IncomingTransfer>().Single();
        await _walletRepository.UpdateAsync(ownerWallet);
        await _walletRepository.UpdateAsync(receiverWallet);
        await _messageBroker.PublishAsync(new FundsDeducted(ownerWallet.Id, ownerWallet.OwnerId, ownerWallet.Currency,
                outgoingTransfer.Amount, outgoingTransfer.Name, outgoingTransfer.Metadata), 
            new FundsAdded(receiverWallet.Id, receiverWallet.OwnerId, receiverWallet.Currency,
                incomingTransfer.Amount, incomingTransfer.Name, incomingTransfer.Metadata));
        _logger.LogInformation($"Transferred {outgoingTransfer.Amount} {outgoingTransfer.Currency}" +
                               $"from wallet with ID: '{ownerWallet.Id}' to wallet with ID: '{receiverWallet.Id}'.");
    }
}