﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Commands;
using Inflow.Services.Wallets.Application.Services;
using Inflow.Services.Wallets.Application.Wallets.Commands;
using Inflow.Services.Wallets.Application.Wallets.Commands.Handlers;
using Inflow.Services.Wallets.Application.Wallets.Events;
using Inflow.Services.Wallets.Core.Owners.Entities;
using Inflow.Services.Wallets.Core.Owners.Repositories;
using Inflow.Services.Wallets.Core.Wallets.Entities;
using Inflow.Services.Wallets.Core.Wallets.Repositories;
using Inflow.Services.Wallets.Core.Wallets.ValueObjects;
using Inflow.Services.Wallets.Infrastructure.EF.Repositories;
using Inflow.Services.Wallets.Infrastructure.Time;
using Inflow.Services.Wallets.Tests.Integration.Common;
using Inflow.Services.Wallets.Tests.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Inflow.Services.Wallets.Tests.Integration.Commands;

public class TransferFundsHandlerTests : IDisposable
{
    private Task Act(TransferFunds command, CancellationToken cancellationToken = default) => _handler.HandleAsync(command);
        
    [Fact]
    public async Task given_valid_command_transfer_funds_should_succeed()
    {
        await _dbContext.Context.Database.EnsureCreatedAsync();
            
        const decimal initialFunds = 1000;
        const decimal sentFunds = 100;
        const string currency = "EUR";
        var now = _clock.CurrentDate();
            
        var owner = new IndividualOwner(Guid.NewGuid(), "Owner 1", "John Doe 1", now);
        var receiver = new IndividualOwner(Guid.NewGuid(), "Owner 2", "John Doe 2", now);
        await _ownerRepository.AddAsync(owner);
        await _ownerRepository.AddAsync(receiver);
            
        var ownerWallet = new Wallet(Guid.NewGuid(), owner.Id, currency, now);
        var receiverWallet = new Wallet(Guid.NewGuid(), receiver.Id, currency, now);
        await _walletRepository.AddAsync(ownerWallet);
        await _walletRepository.AddAsync(receiverWallet);
            
        ownerWallet.AddFunds(Guid.NewGuid(), initialFunds, now, "test_add");
        await _walletRepository.UpdateAsync(ownerWallet);

        var command = new TransferFunds(owner.Id, ownerWallet.Id, receiverWallet.Id, currency, sentFunds);
        await Act(command);

        var updatedOwnerWallet = await _walletRepository.GetAsync(ownerWallet.Id);
        updatedOwnerWallet.Transfers.OfType<IncomingTransfer>().ShouldHaveSingleItem();
        updatedOwnerWallet.Transfers.OfType<OutgoingTransfer>().ShouldHaveSingleItem();
        updatedOwnerWallet.CurrentAmount().ShouldBe(new Amount(initialFunds - sentFunds));
            
        var updatedReceiverWallet = await _walletRepository.GetAsync(receiverWallet.Id);
        updatedReceiverWallet.Transfers.ShouldHaveSingleItem();
        updatedReceiverWallet.CurrentAmount().ShouldBe(new Amount(sentFunds));
            
        _messageBroker.Events.ShouldNotBeEmpty();
        _messageBroker.Events.Count.ShouldBe(2);
        var firstEvent = _messageBroker.Events[0];
        var secondEvent = _messageBroker.Events[1];
        firstEvent.ShouldBeOfType<FundsDeducted>();
        secondEvent.ShouldBeOfType<FundsAdded>();
    }

    private readonly TestWalletsDbContext _dbContext;
    private readonly IIndividualOwnerRepository _ownerRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IClock _clock;
    private readonly ILogger<TransferFundsHandler> _logger;
    private readonly TestMessageBroker _messageBroker;
    private readonly ICommandHandler<TransferFunds> _handler;

    public TransferFundsHandlerTests()
    {
        _dbContext = new TestWalletsDbContext();
        _ownerRepository = new IndividualOwnerRepository(_dbContext.Context);
        _walletRepository = new WalletRepository(_dbContext.Context);
        _clock = new UtcClock();
        _logger = new NullLogger<TransferFundsHandler>();
        _messageBroker = new TestMessageBroker();
        _handler = new TransferFundsHandler(_walletRepository, _clock, _messageBroker, _logger);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}