using RevendaVeiculos.Domain.Entities;

namespace RevendaVeiculos.Domain.Interfaces;

public interface IPedidoCompraRepository
{
    Task<PedidoCompra?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PedidoCompra?> ObterPorCodigoPagamentoAsync(string codigoPagamento, CancellationToken cancellationToken = default);
    Task<PedidoCompra?> ObterPedidoAtivoPorVeiculoIdAsync(Guid veiculoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PedidoCompra>> ListarTodosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PedidoCompra>> ListarReservasExpiradasAsync(DateTime agoraUtc, CancellationToken cancellationToken = default);
    Task AdicionarAsync(PedidoCompra pedido, CancellationToken cancellationToken = default);
    Task AtualizarAsync(PedidoCompra pedido, CancellationToken cancellationToken = default);
}
