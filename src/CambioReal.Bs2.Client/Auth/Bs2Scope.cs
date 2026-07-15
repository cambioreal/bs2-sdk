namespace CambioReal.Bs2.Auth;

/// <summary>
/// Escopo OAuth2 da BS2. Ao contrário da Ripple (um token cobre toda a API), a BS2 exige **um
/// token por escopo** — confirmado no legado (<c>PixService</c> usa <c>pix.cambio.collection.order</c>,
/// <c>PayoutService</c> usa <c>pix.cambio.payment.order</c>) e ao vivo (2026-07-15: dois tokens
/// distintos emitidos com sucesso, um por escopo).
/// </summary>
public enum Bs2Scope
{
    /// <summary>Payin — ordens de cobrança (<c>collection-orders</c>).</summary>
    CollectionOrder = 0,

    /// <summary>Payout — ordens de pagamento (<c>payment-orders</c>).</summary>
    PaymentOrder = 1,
}

internal static class Bs2ScopeExtensions
{
    public static string ToScopeString(this Bs2Scope scope) => scope switch
    {
        Bs2Scope.CollectionOrder => "pix.cambio.collection.order",
        Bs2Scope.PaymentOrder => "pix.cambio.payment.order",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Escopo BS2 desconhecido."),
    };
}
