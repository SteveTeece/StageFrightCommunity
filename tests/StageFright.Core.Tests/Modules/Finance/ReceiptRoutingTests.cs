using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>Unit tests for ReceiptRouting: destination account and received-item creation per payment method.</summary>
public class ReceiptRoutingTests
{
    private static readonly Guid BankId = Guid.NewGuid();

    [Fact]
    public void Should_RouteToPettyCash_When_MethodIsCash()
    {
        Assert.Equal(SystemAccounts.CashId, ReceiptRouting.Destination(PaymentMethod.Cash, null));
    }

    [Fact]
    public void Should_RouteToPettyCash_When_MethodIsCashEvenWithBankAccount()
    {
        Assert.Equal(SystemAccounts.CashId, ReceiptRouting.Destination(PaymentMethod.Cash, BankId));
    }

    [Fact]
    public void Should_RouteToUndepositedFunds_When_MethodIsCheck()
    {
        Assert.Equal(SystemAccounts.UndepositedFundsId, ReceiptRouting.Destination(PaymentMethod.Check, null));
    }

    [Fact]
    public void Should_RouteToUndepositedFunds_When_MethodIsOther()
    {
        Assert.Equal(SystemAccounts.UndepositedFundsId, ReceiptRouting.Destination(PaymentMethod.Other, BankId));
    }

    [Fact]
    public void Should_RouteToChosenBank_When_MethodIsCard()
    {
        Assert.Equal(BankId, ReceiptRouting.Destination(PaymentMethod.Card, BankId));
    }

    [Fact]
    public void Should_RouteToChosenBank_When_MethodIsElectronicTransfer()
    {
        Assert.Equal(BankId, ReceiptRouting.Destination(PaymentMethod.ElectronicTransfer, BankId));
    }

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.ElectronicTransfer)]
    public void Should_Throw_When_BankMethodHasNoBankAccount(PaymentMethod method)
    {
        Assert.Throws<ArgumentException>(() => ReceiptRouting.Destination(method, null));
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, false)]
    [InlineData(PaymentMethod.Check, true)]
    [InlineData(PaymentMethod.Card, false)]
    [InlineData(PaymentMethod.ElectronicTransfer, false)]
    [InlineData(PaymentMethod.Other, true)]
    public void Should_CreateReceivedItem_Only_When_MethodIsCheckOrOther(PaymentMethod method, bool expected)
    {
        Assert.Equal(expected, ReceiptRouting.CreatesReceivedItem(method));
    }
}
