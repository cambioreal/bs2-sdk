using System.Text.Json;
using System.Text.Json.Serialization;

namespace CambioReal.Bs2.Serialization;

/// <summary>Convenções de JSON da API BS2 PIX Câmbio.</summary>
public static class Bs2Json
{
    /// <summary>
    /// Nomes de campo em <c>camelCase</c> (<c>externalId</c>, <c>creditorDebtorType</c>,
    /// <c>financialInstitution</c>, …), confirmado nos payloads reais do legado
    /// (<c>cerebro/app/Libraries/Bs2/{PixService,PayoutService}.php</c> + <c>config/bs2-mock.php</c>).
    /// </summary>
    /// <remarks>
    /// Sem <see cref="JsonStringEnumConverter"/> global: a BS2 mistura casing entre valores de
    /// enum — <c>"Current"</c>/<c>"Savings"</c>/<c>"Succeed"</c>/<c>"Issued"</c> (PascalCase) e
    /// <c>"01"</c>/<c>"05"</c> (string numérica) sem uma convenção uniforme comprovada (ao
    /// contrário da Ripple, que usa <c>UPPER_SNAKE_CASE</c> em toda a API). Por isso todos os
    /// campos de valor fechado são modelados como <see cref="string"/> simples em <c>Models/</c>,
    /// não como enum C#, seguindo a regra do goal-loop: "Não instalar conversor global de enum sem
    /// provar que a convenção de casing do provider é uniforme."
    /// </remarks>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
