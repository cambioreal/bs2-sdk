using CambioReal.Bs2.Models;
using Shouldly;
using Xunit;

namespace CambioReal.Bs2.Tests;

/// <summary>
/// Taxonomia de status. O invariante mais importante nao e "X e terminal" e sim que status
/// DESCONHECIDO nunca seja tratado como terminal — encerrar cedo descarta uma ordem que ainda
/// podia ser paga.
/// </summary>
public sealed class CollectionOrderStatusTests
{
    [Theory]
    [InlineData("Failed")]
    [InlineData("RequestedCancel")]
    [InlineData("Canceled")]
    [InlineData("Cancelled")]
    [InlineData("failed")]          // case-insensitive
    [InlineData("CANCELED")]
    public void TerminalStatusesAreRecognised(string status) =>
        Bs2CollectionOrderStatus.IsTerminal(status).ShouldBeTrue();

    [Theory]
    [InlineData("Issued")]          // observado ao vivo 2026-09-16
    [InlineData("QrCodeGenerated")]
    public void PendingStatusesAreNotTerminal(string status)
    {
        Bs2CollectionOrderStatus.IsTerminal(status).ShouldBeFalse();
        Bs2CollectionOrderStatus.IsPending(status).ShouldBeTrue();
    }

    [Fact]
    public void SucceedIsPaidAndNotTerminal()
    {
        Bs2CollectionOrderStatus.IsPaid("Succeed").ShouldBeTrue();
        Bs2CollectionOrderStatus.IsTerminal("Succeed").ShouldBeFalse();
        Bs2CollectionOrderStatus.IsPending("Succeed").ShouldBeFalse();
    }

    /// <summary>
    /// Regra de ouro: desconhecido = em voo. Um status novo da BS2 nao pode fazer o poll abortar.
    /// </summary>
    [Theory]
    [InlineData("StatusQueAindaNaoExiste")]
    [InlineData("Analyzing")]       // estava na lista do v3, a BS2 nao envia
    [InlineData("AwaitingPayment")] // idem
    [InlineData("")]
    [InlineData(null)]
    public void UnknownStatusIsTreatedAsInFlightNeverTerminal(string? status)
    {
        Bs2CollectionOrderStatus.IsTerminal(status).ShouldBeFalse();
        Bs2CollectionOrderStatus.IsPending(status).ShouldBeTrue();
    }

    /// <summary>
    /// Regressao: `QrCodeGenerated` significa QR apto, NAO pago. Tratar como pago contabilizaria
    /// uma cobranca nao liquidada (gotcha de 2026-06-09 no vault).
    /// </summary>
    [Fact]
    public void QrCodeGeneratedIsNotPaid() =>
        Bs2CollectionOrderStatus.IsPaid("QrCodeGenerated").ShouldBeFalse();

    [Fact]
    public void TerminalAndPendingSetsAreDisjoint() =>
        Bs2CollectionOrderStatus.Terminal
            .Intersect(Bs2CollectionOrderStatus.Pending, System.StringComparer.OrdinalIgnoreCase)
            .ShouldBeEmpty();
}
