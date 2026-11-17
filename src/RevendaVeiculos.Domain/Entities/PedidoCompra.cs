using RevendaVeiculos.Domain.Common;
using RevendaVeiculos.Domain.Enums;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.Entities;

public class PedidoCompra : EntityBase
{
    private readonly List<SagaStepLog> _passosSaga = [];

    public Guid VeiculoId { get; private set; }
    public Guid CompradorId { get; private set; }
    public decimal ValorVenda { get; private set; }
    public StatusPedidoCompra StatusPedido { get; private set; }
    public StatusPagamento StatusPagamento { get; private set; }

    // Dados da Reserva
    public string CodigoReserva { get; private set; } = string.Empty;
    public DateTime DataReserva { get; private set; }
    public DateTime DataExpiracaoReserva { get; private set; }

    // Dados do Pagamento
    public string? CodigoPagamento { get; private set; }
    public DateTime? DataConfirmacaoPagamento { get; private set; }

    // Dados da Retirada e Baixa no Estoque
    public string? ProtocoloDocumentacaoRetirada { get; private set; }
    public string? NotaFiscalNumero { get; private set; }
    public DateTime? DataRetiradaBaixaEstoque { get; private set; }

    // Dados de Cancelamento / Compensação SAGA
    public string? MotivoCancelamento { get; private set; }
    public DateTime? DataCancelamento { get; private set; }

    public IReadOnlyCollection<SagaStepLog> PassosSaga => _passosSaga.AsReadOnly();

    protected PedidoCompra() { }

    public PedidoCompra(Guid veiculoId, Guid compradorId, decimal valorVenda, int minutosExpiracaoReserva = 30)
    {
        if (veiculoId == Guid.Empty)
            throw new DomainException("Veículo inválido para abertura do pedido de compra.");

        if (compradorId == Guid.Empty)
            throw new DomainException("Comprador inválido para abertura do pedido de compra.");

        if (valorVenda <= 0)
            throw new DomainException("Valor da venda deve ser maior que zero.");

        VeiculoId = veiculoId;
        CompradorId = compradorId;
        ValorVenda = valorVenda;
        StatusPedido = StatusPedidoCompra.VeiculoReservado;
        StatusPagamento = StatusPagamento.NaoIniciado;
        CodigoReserva = $"RES-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        DataReserva = DateTime.UtcNow;
        DataExpiracaoReserva = DataReserva.AddMinutes(minutosExpiracaoReserva);

        RegistrarPassoAvanco(
            "1. Seleção e Reserva do Veículo",
            $"Veículo {veiculoId} reservado com código {CodigoReserva} até {DataExpiracaoReserva:O}.");
    }

    public void RegistrarCodigoPagamento(string codigoPagamento)
    {
        if (StatusPedido != StatusPedidoCompra.VeiculoReservado)
            throw new DomainException($"Não é possível gerar código de pagamento para pedido no status {StatusPedido}.");

        if (string.IsNullOrWhiteSpace(codigoPagamento))
            throw new DomainException("Código de pagamento inválido.");

        CodigoPagamento = codigoPagamento;
        StatusPagamento = StatusPagamento.Pendente;
        StatusPedido = StatusPedidoCompra.AguardandoPagamento;
        MarcarAtualizacao();

        RegistrarPassoAvanco(
            "2. Emissão de Código de Pagamento",
            $"Código de pagamento {CodigoPagamento} emitido para o comprador {CompradorId}.");
    }

    public void ConfirmarPagamento(string? codigoPagamentoInformado = null)
    {
        if (StatusPedido != StatusPedidoCompra.AguardandoPagamento)
            throw new DomainException($"O pedido não está aguardando pagamento. Status atual: {StatusPedido}.");

        if (!string.IsNullOrWhiteSpace(codigoPagamentoInformado) &&
            !string.Equals(CodigoPagamento, codigoPagamentoInformado, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("O código de pagamento informado não corresponde ao código emitido para esta reserva.");
        }

        StatusPagamento = StatusPagamento.Aprovado;
        StatusPedido = StatusPedidoCompra.PagamentoConfirmado;
        DataConfirmacaoPagamento = DateTime.UtcNow;
        MarcarAtualizacao();

        RegistrarPassoAvanco(
            "3. Confirmação de Pagamento",
            $"Pagamento do código {CodigoPagamento} aprovado no valor de R$ {ValorVenda:N2}.");
    }

    public void ConcluirRetiradaEBaixaEstoque(string protocoloDocumentacao, string numeroNotaFiscal)
    {
        if (StatusPedido != StatusPedidoCompra.PagamentoConfirmado || StatusPagamento != StatusPagamento.Aprovado)
            throw new DomainException("A retirada do veículo e baixa no estoque só podem ser realizadas após a confirmação do pagamento.");

        if (string.IsNullOrWhiteSpace(protocoloDocumentacao))
            throw new DomainException("Protocolo da documentação de transferência/retirada é obrigatório.");

        ProtocoloDocumentacaoRetirada = protocoloDocumentacao;
        NotaFiscalNumero = numeroNotaFiscal;
        StatusPedido = StatusPedidoCompra.ConcluidoBaixaEstoque;
        DataRetiradaBaixaEstoque = DateTime.UtcNow;
        MarcarAtualizacao();

        RegistrarPassoAvanco(
            "4. Emissão de Documentação, Retirada e Baixa no Estoque",
            $"Documentação ATPV-e/CRV protocolo {protocoloDocumentacao} e NF {numeroNotaFiscal} emitidas. Veículo retirado pelo comprador e baixado no estoque como Vendido.");
    }

    public void CompensarPorFalhaPagamento(string motivoFalha)
    {
        if (StatusPedido == StatusPedidoCompra.ConcluidoBaixaEstoque)
            throw new DomainException("Não é possível cancelar por falha de pagamento um pedido cuja retirada e baixa já foram concluídas.");

        StatusPagamento = StatusPagamento.Recusado;
        StatusPedido = StatusPedidoCompra.CanceladoPorFalhaPagamento;
        MotivoCancelamento = string.IsNullOrWhiteSpace(motivoFalha) ? "Pagamento não efetuado ou recusado." : motivoFalha;
        DataCancelamento = DateTime.UtcNow;
        MarcarAtualizacao();

        RegistrarPassoCompensacao(
            "Compensação SAGA: Cancelamento de Código de Pagamento",
            $"Código de pagamento {CodigoPagamento ?? "N/A"} invalidado devido a: {MotivoCancelamento}");

        RegistrarPassoCompensacao(
            "Compensação SAGA: Liberação de Reserva no Estoque",
            $"Reserva {CodigoReserva} desfeita e veículo {VeiculoId} retornado para status Disponível no estoque.");
    }

    public void CompensarPorDesistenciaCliente(string motivoDesistencia)
    {
        if (StatusPedido == StatusPedidoCompra.ConcluidoBaixaEstoque)
            throw new DomainException("O veículo já foi retirado e baixado do estoque. Desistência não aplicável ao fluxo de reserva/compra em andamento.");

        if (StatusPedido is StatusPedidoCompra.CanceladoPorDesistencia
            or StatusPedidoCompra.CanceladoPorFalhaPagamento
            or StatusPedidoCompra.CanceladoPorExpiracaoReserva)
        {
            throw new DomainException($"O pedido já se encontra cancelado ({StatusPedido}).");
        }

        var haviaPago = StatusPagamento == StatusPagamento.Aprovado;

        if (haviaPago)
        {
            StatusPagamento = StatusPagamento.Estornado;
            RegistrarPassoCompensacao(
                "Compensação SAGA: Estorno Financeiro ao Comprador",
                $"Pagamento {CodigoPagamento} no valor de R$ {ValorVenda:N2} estornado integralmente ao comprador por desistência antes da retirada.");
        }
        else if (StatusPagamento == StatusPagamento.Pendente)
        {
            StatusPagamento = StatusPagamento.Cancelado;
            RegistrarPassoCompensacao(
                "Compensação SAGA: Cancelamento de Código de Pagamento",
                $"Código de pagamento {CodigoPagamento} cancelado por desistência do comprador.");
        }

        StatusPedido = StatusPedidoCompra.CanceladoPorDesistencia;
        MotivoCancelamento = string.IsNullOrWhiteSpace(motivoDesistencia) ? "Desistência solicitada pelo(a) comprador(a)." : motivoDesistencia;
        DataCancelamento = DateTime.UtcNow;
        MarcarAtualizacao();

        RegistrarPassoCompensacao(
            "Compensação SAGA: Liberação de Reserva no Estoque",
            $"Reserva {CodigoReserva} cancelada por desistência ({MotivoCancelamento}). Veículo {VeiculoId} liberado para Disponível no estoque.");
    }

    public void CompensarPorExpiracaoReserva()
    {
        if (StatusPedido is not (StatusPedidoCompra.VeiculoReservado or StatusPedidoCompra.AguardandoPagamento))
            return;

        StatusPagamento = StatusPagamento.Cancelado;
        StatusPedido = StatusPedidoCompra.CanceladoPorExpiracaoReserva;
        MotivoCancelamento = $"Reserva {CodigoReserva} expirou em {DataExpiracaoReserva:O} sem confirmação de pagamento.";
        DataCancelamento = DateTime.UtcNow;
        MarcarAtualizacao();

        RegistrarPassoCompensacao(
            "Compensação SAGA: Expiração de Timeout da Reserva",
            $"Código de pagamento {CodigoPagamento ?? "N/A"} cancelado e veículo {VeiculoId} retornado para Disponível no estoque por timeout.");
    }

    public bool ReservaEstaExpirada(DateTime agoraUtc) =>
        StatusPedido is StatusPedidoCompra.VeiculoReservado or StatusPedidoCompra.AguardandoPagamento &&
        agoraUtc >= DataExpiracaoReserva;

    private void RegistrarPassoAvanco(string nomePasso, string detalhes) =>
        _passosSaga.Add(new SagaStepLog(Id, nomePasso, TipoPassoSaga.ExecucaoAvanco, true, detalhes));

    private void RegistrarPassoCompensacao(string nomePasso, string detalhes) =>
        _passosSaga.Add(new SagaStepLog(Id, nomePasso, TipoPassoSaga.CompensacaoRollback, true, detalhes));
}
