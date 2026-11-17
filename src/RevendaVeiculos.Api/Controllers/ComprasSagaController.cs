using Microsoft.AspNetCore.Mvc;
using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Application.Interfaces;

namespace RevendaVeiculos.Api.Controllers;

/// <summary>
/// Orquestrador SAGA do processo de compra e venda de veículos do começo ao fim:
/// 1. Seleção e Reserva do veículo (com proteção contra concorrência);
/// 2. Emissão do código de pagamento;
/// 3. Confirmação ou falha do pagamento (com compensação automática SAGA);
/// 4. Emissão de documentação, retirada pelo cliente e baixa definitiva no estoque;
/// 5. Tratamento de desistência do cliente em qualquer um dos passos (com estorno e devolução ao estoque).
/// </summary>
[ApiController]
[Route("api/v1/compras")]
[Produces("application/json")]
public class ComprasSagaController : ControllerBase
{
    private readonly ICompraVeiculoSagaOrchestrator _sagaOrchestrator;

    public ComprasSagaController(ICompraVeiculoSagaOrchestrator sagaOrchestrator)
    {
        _sagaOrchestrator = sagaOrchestrator;
    }

    /// <summary>
    /// Passo 1 e 2 da SAGA: O(a) cliente seleciona o veículo, realiza a reserva no estoque
    /// (com controle de concorrência contra reservas simultâneas) e recebe o código de pagamento.
    /// Somente compradores cadastrados podem iniciar a compra.
    /// </summary>
    [HttpPost("reservar")]
    [ProducesResponseType(typeof(PedidoCompraSagaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IniciarSelecaoEReserva(
        [FromBody] IniciarCompraReservaRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name ?? "ClienteComprador";
        var result = await _sagaOrchestrator.IniciarSelecaoEReservaAsync(request, usuario, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { pedidoId = result.PedidoId }, result);
    }

    /// <summary>
    /// Passo 3 da SAGA: Processa o pagamento do veículo utilizando o código de pagamento emitido.
    /// Caso PagamentoEfetuado = false (pagamento não efetuado ou recusado), a SAGA executa
    /// automaticamente as transações de compensação, cancelando o código e liberando o veículo no estoque.
    /// </summary>
    [HttpPost("{pedidoId:guid}/pagamento")]
    [ProducesResponseType(typeof(PedidoCompraSagaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessarPagamento(
        Guid pedidoId,
        [FromBody] ProcessarPagamentoRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name ?? "GatewayPagamento";
        var result = await _sagaOrchestrator.ProcessarPagamentoAsync(pedidoId, request, usuario, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Passo 4 da SAGA: Realiza a retirada do veículo pelo(a) cliente, emitindo a documentação
    /// de transferência (ATPV-e / Nota Fiscal) e efetuando a baixa definitiva do veículo no estoque (Vendido).
    /// </summary>
    [HttpPost("{pedidoId:guid}/retirada")]
    [ProducesResponseType(typeof(PedidoCompraSagaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RealizarRetiradaEBaixaEstoque(
        Guid pedidoId,
        [FromBody] RealizarRetiradaRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name ?? "ClienteComprador";
        var result = await _sagaOrchestrator.RealizarRetiradaEBaixaEstoqueAsync(pedidoId, request, usuario, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Fluxo de Exceção / Compensação SAGA: O(a) cliente desiste da compra em qualquer um dos passos
    /// antes da retirada. A SAGA estorna o pagamento (se já realizado), invalida o código de pagamento,
    /// cancela a reserva e devolve o veículo para Disponível no estoque.
    /// </summary>
    [HttpPost("{pedidoId:guid}/desistir")]
    [ProducesResponseType(typeof(PedidoCompraSagaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesistirDaCompra(
        Guid pedidoId,
        [FromBody] DesistirCompraRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name ?? "ClienteComprador";
        var result = await _sagaOrchestrator.DesistirDaCompraAsync(pedidoId, request, usuario, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Efetua a venda completa de um veículo orquestrando todos os passos da SAGA
    /// (Reserva -> Código de Pagamento -> Confirmação do Pagamento -> Documentação e Baixa no Estoque).
    /// Somente permitido para compradores cadastrados.
    /// </summary>
    [HttpPost("efetuar-venda")]
    [ProducesResponseType(typeof(PedidoCompraSagaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EfetuarVendaCompleta(
        [FromBody] EfetuarVendaCompletaRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name ?? "Vendedor";
        var result = await _sagaOrchestrator.EfetuarVendaCompletaOrquestradaAsync(request, usuario, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { pedidoId = result.PedidoId }, result);
    }

    /// <summary>
    /// Consulta o estado atual de um pedido de compra e o histórico completo de passos da SAGA (avanço e compensação).
    /// </summary>
    [HttpGet("{pedidoId:guid}")]
    [ProducesResponseType(typeof(PedidoCompraSagaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid pedidoId, CancellationToken cancellationToken)
    {
        var result = await _sagaOrchestrator.ObterStatusSagaPorIdAsync(pedidoId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lista todas as transações de compra e seus respectivos estados na máquina de estados da SAGA.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PedidoCompraSagaResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarTodas(CancellationToken cancellationToken)
    {
        var result = await _sagaOrchestrator.ListarTodasTransacoesSagaAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Aciona manualmente a varredura de expiração de reservas vencidas (executada automaticamente pelo Background Worker).
    /// </summary>
    [HttpPost("expirar-reservas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExpirarReservasVencidas(CancellationToken cancellationToken)
    {
        var quantidade = await _sagaOrchestrator.ProcessarExpiracaoReservasAsync(cancellationToken);
        return Ok(new
        {
            ReservasExpiradasCompensadas = quantidade,
            ProcessadoEm = DateTime.UtcNow
        });
    }
}
