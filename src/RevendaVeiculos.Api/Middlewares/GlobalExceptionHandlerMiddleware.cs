using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Api.Middlewares;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            RecursoNaoEncontradoException => (HttpStatusCode.NotFound, "Recurso não encontrado"),
            ConflitoConcorrenciaException => (HttpStatusCode.Conflict, "Conflito de concorrência na reserva/estoque"),
            DomainException => (HttpStatusCode.BadRequest, "Violação de regra de negócio ou validação de domínio"),
            _ => (HttpStatusCode.InternalServerError, "Erro interno no processamento da requisição")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Erro inesperado ao processar requisição {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Exceção de negócio ({StatusCode}): {Message}", (int)statusCode, exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = statusCode == HttpStatusCode.InternalServerError
                ? "Ocorreu um erro interno inesperado. Consulte o identificador de rastreamento."
                : exception.Message,
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }
}
