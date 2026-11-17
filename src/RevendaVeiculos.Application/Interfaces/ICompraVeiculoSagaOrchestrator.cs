using RevendaVeiculos.Application.DTOs;

namespace RevendaVeiculos.Application.Interfaces;

public interface ICompraVeiculoSagaOrchestrator
{
    Task<PedidoCompraSagaResponse> IniciarSelecaoEReservaAsync(
        IniciarCompraReservaRequest request,
        string usuarioExecutor = "Cliente",
        CancellationToken cancellationToken = default);

    Task<PedidoCompraSagaResponse> ProcessarPagamentoAsync(
        Guid pedidoId,
        ProcessarPagamentoRequest request,
        string usuarioExecutor = "GatewayPagamento",
        CancellationToken cancellationToken = default);

    Task<PedidoCompraSagaResponse> RealizarRetiradaEBaixaEstoqueAsync(
        Guid pedidoId,
        RealizarRetiradaRequest request,
        string usuarioExecutor = "Cliente",
        CancellationToken cancellationToken = default);

    Task<PedidoCompraSagaResponse> DesistirDaCompraAsync(
        Guid pedidoId,
        DesistirCompraRequest request,
        string usuarioExecutor = "Cliente",
        CancellationToken cancellationToken = default);

    Task<PedidoCompraSagaResponse> EfetuarVendaCompletaOrquestradaAsync(
        EfetuarVendaCompletaRequest request,
        string usuarioExecutor = "Vendedor",
        CancellationToken cancellationToken = default);

    Task<PedidoCompraSagaResponse> ObterStatusSagaPorIdAsync(
        Guid pedidoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PedidoCompraSagaResponse>> ListarTodasTransacoesSagaAsync(
        CancellationToken cancellationToken = default);

    Task<int> ProcessarExpiracaoReservasAsync(
        CancellationToken cancellationToken = default);
}
