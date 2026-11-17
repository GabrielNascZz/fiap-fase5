using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RevendaVeiculos.Application.Interfaces;

namespace RevendaVeiculos.Infrastructure.BackgroundServices;

public class ReservaExpiracaoBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservaExpiracaoBackgroundService> _logger;

    public ReservaExpiracaoBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ReservaExpiracaoBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var sagaOrchestrator = scope.ServiceProvider.GetRequiredService<ICompraVeiculoSagaOrchestrator>();
                var expiradas = await sagaOrchestrator.ProcessarExpiracaoReservasAsync(stoppingToken);
                if (expiradas > 0)
                {
                    _logger.LogInformation("Worker SAGA liberou {Quantidade} reserva(s) expirada(s) de volta ao estoque.", expiradas);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erro ao processar expiração automática de reservas na SAGA.");
            }
        }
    }
}
