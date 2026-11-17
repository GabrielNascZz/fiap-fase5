namespace RevendaVeiculos.Application.DTOs;

public record IniciarCompraReservaRequest(
    Guid VeiculoId,
    Guid CompradorId);

public record ProcessarPagamentoRequest(
    string CodigoPagamento,
    bool PagamentoEfetuado,
    string? MotivoRecusa = null);

public record RealizarRetiradaRequest(
    bool ConfirmarAssinaturaTermoEntrega = true);

public record DesistirCompraRequest(
    string MotivoDesistencia);

public record EfetuarVendaCompletaRequest(
    Guid VeiculoId,
    Guid CompradorId);

public record SagaStepLogDto(
    Guid Id,
    string NomePasso,
    string TipoPasso,
    bool Sucesso,
    string Detalhes,
    DateTime ExecutadoEm);

public record PedidoCompraSagaResponse(
    Guid PedidoId,
    Guid VeiculoId,
    string VeiculoDescricao,
    string StatusAtualVeiculoNoEstoque,
    Guid CompradorId,
    string CompradorNome,
    string CompradorCpfMascarado,
    decimal ValorVenda,
    string StatusPedidoSaga,
    string StatusPagamento,
    string CodigoReserva,
    DateTime DataReserva,
    DateTime DataExpiracaoReserva,
    string? CodigoPagamento,
    DateTime? DataConfirmacaoPagamento,
    string? ProtocoloDocumentacaoRetirada,
    string? NotaFiscalNumero,
    DateTime? DataRetiradaBaixaEstoque,
    string? MotivoCancelamento,
    DateTime? DataCancelamento,
    IReadOnlyList<SagaStepLogDto> HistoricoPassosSaga);
