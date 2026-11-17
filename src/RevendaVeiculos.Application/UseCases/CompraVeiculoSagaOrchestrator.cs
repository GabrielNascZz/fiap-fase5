using System.Collections.Concurrent;
using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.Interfaces;

namespace RevendaVeiculos.Application.UseCases;

public class CompraVeiculoSagaOrchestrator : ICompraVeiculoSagaOrchestrator
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> LocksPorVeiculo = new();

    private readonly IVeiculoRepository _veiculoRepository;
    private readonly ICompradorRepository _compradorRepository;
    private readonly IPedidoCompraRepository _pedidoRepository;
    private readonly IPagamentoGatewayService _pagamentoGateway;
    private readonly IDocumentacaoVeicularService _documentacaoService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompraVeiculoSagaOrchestrator(
        IVeiculoRepository veiculoRepository,
        ICompradorRepository compradorRepository,
        IPedidoCompraRepository pedidoRepository,
        IPagamentoGatewayService pagamentoGateway,
        IDocumentacaoVeicularService documentacaoService,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork)
    {
        _veiculoRepository = veiculoRepository;
        _compradorRepository = compradorRepository;
        _pedidoRepository = pedidoRepository;
        _pagamentoGateway = pagamentoGateway;
        _documentacaoService = documentacaoService;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PedidoCompraSagaResponse> IniciarSelecaoEReservaAsync(
        IniciarCompraReservaRequest request,
        string usuarioExecutor = "Cliente",
        CancellationToken cancellationToken = default)
    {
        var semaforoVeiculo = LocksPorVeiculo.GetOrAdd(request.VeiculoId, _ => new SemaphoreSlim(1, 1));
        await semaforoVeiculo.WaitAsync(cancellationToken);

        try
        {
            // Passo 0: Validar que o comprador está cadastrado e possui dados válidos para faturamento
            var comprador = await _compradorRepository.ObterPorIdAsync(request.CompradorId, cancellationToken)
                ?? throw new RecursoNaoEncontradoException($"Comprador com ID {request.CompradorId} não encontrado. A venda só é permitida para compradores cadastrados.");

            comprador.ValidarAptidaoParaCompraEPagamento();

            // Passo 1 da SAGA: Selecionar e reservar o veículo no estoque com controle de concorrência
            var veiculo = await _veiculoRepository.ObterPorIdAsync(request.VeiculoId, cancellationToken)
                ?? throw new RecursoNaoEncontradoException($"Veículo com ID {request.VeiculoId} não encontrado.");

            try
            {
                veiculo.Reservar();
            }
            catch (ConflitoConcorrenciaException ex)
            {
                await _auditLogRepository.RegistrarAsync(
                    new AuditLog(
                        usuarioExecutor,
                        "SAGA_CONFLITO_CONCORRENCIA_RESERVA",
                        nameof(Veiculo),
                        veiculo.Id.ToString(),
                        $"Tentativa concorrente de reserva bloqueada para o comprador {comprador.Cpf.Mascarado()}: {ex.Message}"),
                    cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                throw;
            }

            var pedido = new PedidoCompra(veiculo.Id, comprador.Id, veiculo.Preco);

            // Passo 2 da SAGA: Emitir o código de pagamento no serviço financeiro
            try
            {
                var codigoPagamento = await _pagamentoGateway.EmitirCodigoPagamentoAsync(
                    pedido.Id,
                    comprador.Cpf.Numero,
                    veiculo.Preco,
                    cancellationToken);

                pedido.RegistrarCodigoPagamento(codigoPagamento);
            }
            catch (Exception ex)
            {
                // Compensação imediata da SAGA caso a emissão do código de pagamento falhe
                veiculo.LiberarReserva();
                pedido.CompensarPorFalhaPagamento($"Falha ao emitir código de pagamento: {ex.Message}");

                await _veiculoRepository.AtualizarAsync(veiculo, cancellationToken);
                await _pedidoRepository.AdicionarAsync(pedido, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                throw new DomainException($"Erro ao gerar código de pagamento. Reserva desfeita via compensação SAGA: {ex.Message}", ex);
            }

            await _veiculoRepository.AtualizarAsync(veiculo, cancellationToken);
            await _pedidoRepository.AdicionarAsync(pedido, cancellationToken);
            await _auditLogRepository.RegistrarAsync(
                new AuditLog(
                    usuarioExecutor,
                    "SAGA_PASSO1_E_2_RESERVA_E_CODIGO_PAGAMENTO",
                    nameof(PedidoCompra),
                    pedido.Id.ToString(),
                    $"Veículo {veiculo.Placa} reservado ({pedido.CodigoReserva}) e código de pagamento {pedido.CodigoPagamento} emitido para comprador {comprador.Cpf.Mascarado()}."),
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return MapToResponse(pedido, veiculo, comprador);
        }
        finally
        {
            semaforoVeiculo.Release();
        }
    }

    public async Task<PedidoCompraSagaResponse> ProcessarPagamentoAsync(
        Guid pedidoId,
        ProcessarPagamentoRequest request,
        string usuarioExecutor = "GatewayPagamento",
        CancellationToken cancellationToken = default)
    {
        var pedido = await _pedidoRepository.ObterPorIdAsync(pedidoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Pedido de compra SAGA com ID {pedidoId} não encontrado.");

        var veiculo = await _veiculoRepository.ObterPorIdAsync(pedido.VeiculoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Veículo {pedido.VeiculoId} não encontrado.");

        var comprador = await _compradorRepository.ObterPorIdAsync(pedido.CompradorId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador {pedido.CompradorId} não encontrado.");

        if (pedido.ReservaEstaExpirada(DateTime.UtcNow))
        {
            await ExecutarCompensacaoExpiracaoAsync(pedido, veiculo, cancellationToken);
            throw new DomainException("A reserva deste veículo expirou antes da confirmação do pagamento. O veículo foi liberado novamente para o estoque.");
        }

        if (request.PagamentoEfetuado)
        {
            // Passo 3 Positivo da SAGA: Confirmar o pagamento
            pedido.ConfirmarPagamento(request.CodigoPagamento);

            await _pedidoRepository.AtualizarAsync(pedido, cancellationToken);
            await _auditLogRepository.RegistrarAsync(
                new AuditLog(
                    usuarioExecutor,
                    "SAGA_PASSO3_PAGAMENTO_CONFIRMADO",
                    nameof(PedidoCompra),
                    pedido.Id.ToString(),
                    $"Pagamento {pedido.CodigoPagamento} confirmado no valor de R$ {pedido.ValorVenda:N2}."),
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        else
        {
            // Fluxo de Exceção / Compensação SAGA: Pagamento não efetuado ou recusado
            if (!string.IsNullOrWhiteSpace(pedido.CodigoPagamento))
            {
                await _pagamentoGateway.CancelarCodigoPagamentoAsync(pedido.CodigoPagamento, cancellationToken);
            }

            var motivo = string.IsNullOrWhiteSpace(request.MotivoRecusa)
                ? "Pagamento não efetuado ou recusado pela instituição financeira."
                : request.MotivoRecusa;

            pedido.CompensarPorFalhaPagamento(motivo);
            veiculo.LiberarReserva();

            await _veiculoRepository.AtualizarAsync(veiculo, cancellationToken);
            await _pedidoRepository.AtualizarAsync(pedido, cancellationToken);
            await _auditLogRepository.RegistrarAsync(
                new AuditLog(
                    usuarioExecutor,
                    "SAGA_COMPENSACAO_FALHA_PAGAMENTO",
                    nameof(PedidoCompra),
                    pedido.Id.ToString(),
                    $"Compensação SAGA executada: pagamento não efetuado ({motivo}). Reserva {pedido.CodigoReserva} cancelada e veículo {veiculo.Placa} devolvido ao estoque como Disponível."),
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }

        return MapToResponse(pedido, veiculo, comprador);
    }

    public async Task<PedidoCompraSagaResponse> RealizarRetiradaEBaixaEstoqueAsync(
        Guid pedidoId,
        RealizarRetiradaRequest request,
        string usuarioExecutor = "Cliente",
        CancellationToken cancellationToken = default)
    {
        if (!request.ConfirmarAssinaturaTermoEntrega)
            throw new DomainException("A confirmação de assinatura do termo de entrega e documentação é obrigatória para a retirada do veículo.");

        var pedido = await _pedidoRepository.ObterPorIdAsync(pedidoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Pedido de compra SAGA com ID {pedidoId} não encontrado.");

        var veiculo = await _veiculoRepository.ObterPorIdAsync(pedido.VeiculoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Veículo {pedido.VeiculoId} não encontrado.");

        var comprador = await _compradorRepository.ObterPorIdAsync(pedido.CompradorId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador {pedido.CompradorId} não encontrado.");

        // Validar dados sensíveis necessários para emissão da documentação do veículo na retirada
        comprador.ValidarAptidaoParaRetiradaEDocumentacao(DateTime.UtcNow);

        // Passo 4 da SAGA: Emitir documentação oficial (ATPV-e / Nota Fiscal) e realizar baixa definitiva no estoque
        var (protocoloAtpve, numeroNotaFiscal) = await _documentacaoService.EmitirDocumentacaoRetiradaAsync(
            veiculo,
            comprador,
            pedido.ValorVenda,
            cancellationToken);

        veiculo.ConfirmarBaixaEstoque();
        pedido.ConcluirRetiradaEBaixaEstoque(protocoloAtpve, numeroNotaFiscal);

        await _veiculoRepository.AtualizarAsync(veiculo, cancellationToken);
        await _pedidoRepository.AtualizarAsync(pedido, cancellationToken);
        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                usuarioExecutor,
                "SAGA_PASSO4_RETIRADA_E_BAIXA_ESTOQUE",
                nameof(PedidoCompra),
                pedido.Id.ToString(),
                $"Retirada concluída e baixa no estoque efetuada para veículo {veiculo.Placa}. Protocolo ATPV-e: {protocoloAtpve}, NF: {numeroNotaFiscal}. Comprador: {comprador.Cpf.Mascarado()}."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MapToResponse(pedido, veiculo, comprador);
    }

    public async Task<PedidoCompraSagaResponse> DesistirDaCompraAsync(
        Guid pedidoId,
        DesistirCompraRequest request,
        string usuarioExecutor = "Cliente",
        CancellationToken cancellationToken = default)
    {
        var pedido = await _pedidoRepository.ObterPorIdAsync(pedidoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Pedido de compra SAGA com ID {pedidoId} não encontrado.");

        var veiculo = await _veiculoRepository.ObterPorIdAsync(pedido.VeiculoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Veículo {pedido.VeiculoId} não encontrado.");

        var comprador = await _compradorRepository.ObterPorIdAsync(pedido.CompradorId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador {pedido.CompradorId} não encontrado.");

        // Se o pagamento já havia sido aprovado antes da desistência, aciona estorno financeiro via gateway
        if (pedido.StatusPagamento == Domain.Enums.StatusPagamento.Aprovado &&
            !string.IsNullOrWhiteSpace(pedido.CodigoPagamento))
        {
            await _pagamentoGateway.EstornarPagamentoAsync(
                pedido.CodigoPagamento,
                pedido.ValorVenda,
                cancellationToken);
        }
        else if (pedido.StatusPagamento == Domain.Enums.StatusPagamento.Pendente &&
                 !string.IsNullOrWhiteSpace(pedido.CodigoPagamento))
        {
            await _pagamentoGateway.CancelarCodigoPagamentoAsync(
                pedido.CodigoPagamento,
                cancellationToken);
        }

        // Executa compensação na SAGA e libera o veículo de volta para Disponível no estoque
        pedido.CompensarPorDesistenciaCliente(request.MotivoDesistencia);
        veiculo.LiberarReserva();

        await _veiculoRepository.AtualizarAsync(veiculo, cancellationToken);
        await _pedidoRepository.AtualizarAsync(pedido, cancellationToken);
        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                usuarioExecutor,
                "SAGA_COMPENSACAO_DESISTENCIA_CLIENTE",
                nameof(PedidoCompra),
                pedido.Id.ToString(),
                $"Desistência processada via compensação SAGA ({request.MotivoDesistencia}). Veículo {veiculo.Placa} retornado ao estoque como Disponível."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MapToResponse(pedido, veiculo, comprador);
    }

    public async Task<PedidoCompraSagaResponse> EfetuarVendaCompletaOrquestradaAsync(
        EfetuarVendaCompletaRequest request,
        string usuarioExecutor = "Vendedor",
        CancellationToken cancellationToken = default)
    {
        // Executa todos os passos da SAGA Orquestrada de ponta a ponta (Seleção -> Reserva -> Código Pagamento -> Pagamento -> Documentação e Baixa)
        var reservaResponse = await IniciarSelecaoEReservaAsync(
            new IniciarCompraReservaRequest(request.VeiculoId, request.CompradorId),
            usuarioExecutor,
            cancellationToken);

        await ProcessarPagamentoAsync(
            reservaResponse.PedidoId,
            new ProcessarPagamentoRequest(reservaResponse.CodigoPagamento!, true),
            usuarioExecutor,
            cancellationToken);

        return await RealizarRetiradaEBaixaEstoqueAsync(
            reservaResponse.PedidoId,
            new RealizarRetiradaRequest(true),
            usuarioExecutor,
            cancellationToken);
    }

    public async Task<PedidoCompraSagaResponse> ObterStatusSagaPorIdAsync(
        Guid pedidoId,
        CancellationToken cancellationToken = default)
    {
        var pedido = await _pedidoRepository.ObterPorIdAsync(pedidoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Pedido de compra SAGA com ID {pedidoId} não encontrado.");

        var veiculo = await _veiculoRepository.ObterPorIdAsync(pedido.VeiculoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Veículo {pedido.VeiculoId} não encontrado.");

        var comprador = await _compradorRepository.ObterPorIdAsync(pedido.CompradorId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador {pedido.CompradorId} não encontrado.");

        return MapToResponse(pedido, veiculo, comprador);
    }

    public async Task<IReadOnlyList<PedidoCompraSagaResponse>> ListarTodasTransacoesSagaAsync(
        CancellationToken cancellationToken = default)
    {
        var pedidos = await _pedidoRepository.ListarTodosAsync(cancellationToken);
        var resultado = new List<PedidoCompraSagaResponse>(pedidos.Count);

        foreach (var pedido in pedidos)
        {
            var veiculo = await _veiculoRepository.ObterPorIdAsync(pedido.VeiculoId, cancellationToken);
            var comprador = await _compradorRepository.ObterPorIdAsync(pedido.CompradorId, cancellationToken);
            if (veiculo is not null && comprador is not null)
            {
                resultado.Add(MapToResponse(pedido, veiculo, comprador));
            }
        }

        return resultado;
    }

    public async Task<int> ProcessarExpiracaoReservasAsync(CancellationToken cancellationToken = default)
    {
        var expiradas = await _pedidoRepository.ListarReservasExpiradasAsync(DateTime.UtcNow, cancellationToken);
        var contador = 0;

        foreach (var pedido in expiradas)
        {
            var veiculo = await _veiculoRepository.ObterPorIdAsync(pedido.VeiculoId, cancellationToken);
            if (veiculo is not null)
            {
                await ExecutarCompensacaoExpiracaoAsync(pedido, veiculo, cancellationToken);
                contador++;
            }
        }

        return contador;
    }

    private async Task ExecutarCompensacaoExpiracaoAsync(
        PedidoCompra pedido,
        Veiculo veiculo,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(pedido.CodigoPagamento))
        {
            await _pagamentoGateway.CancelarCodigoPagamentoAsync(pedido.CodigoPagamento, cancellationToken);
        }

        pedido.CompensarPorExpiracaoReserva();
        veiculo.LiberarReserva();

        await _veiculoRepository.AtualizarAsync(veiculo, cancellationToken);
        await _pedidoRepository.AtualizarAsync(pedido, cancellationToken);
        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                "SagaTimeoutWorker",
                "SAGA_COMPENSACAO_EXPIRACAO_RESERVA",
                nameof(PedidoCompra),
                pedido.Id.ToString(),
                $"Reserva {pedido.CodigoReserva} expirou por timeout. Veículo {veiculo.Placa} liberado de volta ao estoque."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);
    }

    private static PedidoCompraSagaResponse MapToResponse(
        PedidoCompra pedido,
        Veiculo veiculo,
        Comprador comprador)
    {
        var passos = pedido.PassosSaga
            .OrderBy(p => p.CriadoEm)
            .Select(p => new SagaStepLogDto(
                p.Id,
                p.NomePasso,
                p.TipoPasso.ToString(),
                p.Sucesso,
                p.Detalhes,
                p.CriadoEm))
            .ToList();

        return new PedidoCompraSagaResponse(
            pedido.Id,
            veiculo.Id,
            $"{veiculo.Marca} {veiculo.Modelo} {veiculo.AnoFabricacao}/{veiculo.AnoModelo} ({veiculo.Cor}) - Placa {veiculo.Placa}",
            veiculo.Status.ToString(),
            comprador.Id,
            comprador.NomeCompleto,
            comprador.Anonimizado ? "***.***.***-**" : comprador.Cpf.Mascarado(),
            pedido.ValorVenda,
            pedido.StatusPedido.ToString(),
            pedido.StatusPagamento.ToString(),
            pedido.CodigoReserva,
            pedido.DataReserva,
            pedido.DataExpiracaoReserva,
            pedido.CodigoPagamento,
            pedido.DataConfirmacaoPagamento,
            pedido.ProtocoloDocumentacaoRetirada,
            pedido.NotaFiscalNumero,
            pedido.DataRetiradaBaixaEstoque,
            pedido.MotivoCancelamento,
            pedido.DataCancelamento,
            passos);
    }
}
