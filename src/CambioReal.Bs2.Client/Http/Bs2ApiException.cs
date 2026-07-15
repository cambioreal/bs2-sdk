using System.Net;

namespace CambioReal.Bs2.Http;

/// <summary>Erro devolvido pela API BS2.</summary>
public class Bs2ApiException : Exception
{
    /// <summary>Cria uma exceção sem contexto de resposta.</summary>
    public Bs2ApiException()
    {
    }

    /// <summary>Cria uma exceção com mensagem.</summary>
    public Bs2ApiException(string message)
        : base(message)
    {
    }

    /// <summary>Cria uma exceção com mensagem e causa.</summary>
    public Bs2ApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Cria uma exceção a partir de uma resposta da API.</summary>
    public Bs2ApiException(HttpStatusCode statusCode, string? errorCode, string message, string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ResponseBody = responseBody;
    }

    /// <summary>Status HTTP da resposta.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Descrição de erro extraída do corpo, quando presente. A BS2 não tem um catálogo de código
    /// máquina-legível confirmado no legado (o único código conhecido, <c>CT02098</c>, foi
    /// observado só num gate de sandbox, nunca modelado em código) — este campo carrega a melhor
    /// descrição textual disponível, não um código estruturado.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>Corpo bruto da resposta, para diagnóstico.</summary>
    public string? ResponseBody { get; }
}

/// <summary>A autenticação falhou mesmo após uma renovação de token.</summary>
public sealed class Bs2AuthenticationException : Bs2ApiException
{
    /// <inheritdoc/>
    public Bs2AuthenticationException()
    {
    }

    /// <inheritdoc/>
    public Bs2AuthenticationException(string message)
        : base(message)
    {
    }

    /// <inheritdoc/>
    public Bs2AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public Bs2AuthenticationException(HttpStatusCode statusCode, string? errorCode, string message, string? responseBody)
        : base(statusCode, errorCode, message, responseBody)
    {
    }
}
