using RevendaVeiculos.Application.Interfaces;

namespace RevendaVeiculos.Infrastructure.Services;

public class PagamentoGatewayService : IPagamentoGatewayService
{
    public Task<string> EmitirCodigoPagamentoAsync(
        Guid pedidoId,
        string cpfComprador,
        decimal valor,
        CancellationToken cancellationToken = default)
    {
        var sufixoPedido = pedidoId.ToString("N")[..8].ToUpperInvariant();
        var codigoPagamento = $"PAY-PIX-{DateTime.UtcNow:yyyyMMdd}-{sufixoPedido}";
        return Task.FromResult(codigoPagamento);
    }

    public Task CancelarCodigoPagamentoAsync(
        string codigoPagamento,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task EstornarPagamentoAsync(
        string codigoPagamento,
        decimal valor,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
