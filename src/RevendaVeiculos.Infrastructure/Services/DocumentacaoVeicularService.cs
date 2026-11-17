using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Domain.Entities;

namespace RevendaVeiculos.Infrastructure.Services;

public class DocumentacaoVeicularService : IDocumentacaoVeicularService
{
    public Task<(string ProtocoloAtpve, string NumeroNotaFiscal)> EmitirDocumentacaoRetiradaAsync(
        Veiculo veiculo,
        Comprador comprador,
        decimal valorVenda,
        CancellationToken cancellationToken = default)
    {
        var protocoloAtpve = $"ATPVE-DETRAN-{veiculo.Placa.Valor}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var numeroNotaFiscal = $"NFE-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(100000, 999999)}";
        return Task.FromResult((protocoloAtpve, numeroNotaFiscal));
    }
}
