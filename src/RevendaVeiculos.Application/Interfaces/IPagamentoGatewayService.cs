namespace RevendaVeiculos.Application.Interfaces;

public interface IPagamentoGatewayService
{
    Task<string> EmitirCodigoPagamentoAsync(
        Guid pedidoId,
        string cpfComprador,
        decimal valor,
        CancellationToken cancellationToken = default);

    Task CancelarCodigoPagamentoAsync(
        string codigoPagamento,
        CancellationToken cancellationToken = default);

    Task EstornarPagamentoAsync(
        string codigoPagamento,
        decimal valor,
        CancellationToken cancellationToken = default);
}
