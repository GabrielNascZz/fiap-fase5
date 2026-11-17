using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Application.UseCases;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Infrastructure.BackgroundServices;
using RevendaVeiculos.Infrastructure.Persistence;
using RevendaVeiculos.Infrastructure.Repositories;
using RevendaVeiculos.Infrastructure.Security;
using RevendaVeiculos.Infrastructure.Services;

namespace RevendaVeiculos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureAndApplication(
        this IServiceCollection services,
        IConfiguration _)
    {
        services.AddSingleton<ICryptoService, AesGcmCryptoService>();

        services.AddDbContext<RevendaDbContext>((sp, options) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var usePostgres = !string.IsNullOrWhiteSpace(connectionString) &&
                              bool.TryParse(configuration["UsePostgres"], out var pgEnabled) &&
                              pgEnabled;

            if (usePostgres)
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                var dbName = configuration["InMemoryDatabaseName"] ?? "RevendaVeiculosFase5Db";
                options.UseInMemoryDatabase(dbName);
            }
        });

        services.AddScoped<IVeiculoRepository, VeiculoRepository>();
        services.AddScoped<ICompradorRepository, CompradorRepository>();
        services.AddScoped<IPedidoCompraRepository, PedidoCompraRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPagamentoGatewayService, PagamentoGatewayService>();
        services.AddScoped<IDocumentacaoVeicularService, DocumentacaoVeicularService>();

        services.AddScoped<IVeiculoUseCase, VeiculoUseCase>();
        services.AddScoped<ICompradorUseCase, CompradorUseCase>();
        services.AddScoped<ICompraVeiculoSagaOrchestrator, CompraVeiculoSagaOrchestrator>();

        services.AddHostedService<ReservaExpiracaoBackgroundService>();

        return services;
    }
}
