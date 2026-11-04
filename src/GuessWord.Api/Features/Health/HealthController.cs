using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuessWord.Api.Features.Health;

/// <summary>Безопасное состояние процесса и готовность полного словаря.</summary>
[ApiController]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(IDictionaryReadiness readiness) : ControllerBase
{
    /// <summary>Процесс принимает HTTP-запросы.</summary>
    [HttpGet("live")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Live(CancellationToken cancellationToken) => new HealthResponse("live");

    /// <summary>БД доступна, словарь полный и все векторы пригодны для ранжирования.</summary>
    [HttpGet("ready")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Ready(CancellationToken cancellationToken) =>
        await readiness.IsReadyAsync(cancellationToken)
            ? Ok(new HealthResponse("ready"))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("unavailable"));
}

/// <summary>Публичный статус без внутренних диагностических данных.</summary>
public sealed record HealthResponse(string Status);
