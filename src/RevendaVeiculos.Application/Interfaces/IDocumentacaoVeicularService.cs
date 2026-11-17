using RevendaVeiculos.Domain.Entities;

namespace RevendaVeiculos.Application.Interfaces;

public interface IDocumentacaoVeicularService
{
    Task<(string ProtocoloAtpve, string NumeroNotaFiscal)> EmitirDocumentacaoRetiradaAsync(
        Veiculo veiculo,
        Comprador comprador,
        decimal valorVenda,
        CancellationToken cancellationToken = default);
}
