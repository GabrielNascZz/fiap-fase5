using FluentAssertions;
using Moq;
using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Application.UseCases;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Enums;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.UnitTests.Application;

public class SagaOrchestratorUnitTests
{
    private readonly Mock<IVeiculoRepository> _veiculoRepo = new();
    private readonly Mock<ICompradorRepository> _compradorRepo = new();
    private readonly Mock<IPedidoCompraRepository> _pedidoRepo = new();
    private readonly Mock<IPagamentoGatewayService> _pagamentoGateway = new();
    private readonly Mock<IDocumentacaoVeicularService> _docService = new();
    private readonly Mock<IAuditLogRepository> _auditRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private CompraVeiculoSagaOrchestrator CriarSut() =>
        new(
            _veiculoRepo.Object,
            _compradorRepo.Object,
            _pedidoRepo.Object,
            _pagamentoGateway.Object,
            _docService.Object,
            _auditRepo.Object,
            _uow.Object);

    private static Veiculo CriarVeiculo() =>
        new(
            "Toyota",
            "Corolla Altis Hybrid",
            2024,
            2025,
            "Branco Pérola",
            189900m,
            new Placa("HYB1A23"),
            new Chassi("9BRBD3HE0P0000999"),
            new Renavam("99887766554"),
            0,
            TipoCombustivel.Hibrido,
            TipoCambio.CVT);

    private static Comprador CriarComprador() =>
        new(
            "João Pedro Cardoso",
            new Email("joao@cliente.com.br"),
            "hash-email",
            "11999998888",
            new DateTime(1993, 4, 20),
            new Cpf("52998224725"),
            "hash-cpf",
            new Endereco("01310100", "Av Paulista", "1500", null, "Bela Vista", "São Paulo", "SP"),
            new DocumentoIdentidade("112233445", "SSP", "SP"),
            new CarteiraHabilitacao("12345678901", "AB", DateTime.UtcNow.AddYears(3)),
            consentimentoLgpd: true);

    [Fact]
    public async Task IniciarSelecaoEReserva_ComCompradorNaoCadastrado_DeveLancarRecursoNaoEncontradoException()
    {
        var sut = CriarSut();
        _compradorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Comprador?)null);

        var act = () => sut.IniciarSelecaoEReservaAsync(
            new IniciarCompraReservaRequest(Guid.NewGuid(), Guid.NewGuid()));

        await act.Should().ThrowAsync<RecursoNaoEncontradoException>()
            .WithMessage("*A venda só é permitida para compradores cadastrados*");
    }

    [Fact]
    public async Task IniciarSelecaoEReserva_SeGatewayDePagamentoFalharAoGerarCodigo_DeveCompensarLiberandoReservaDoVeiculo()
    {
        var sut = CriarSut();
        var veiculo = CriarVeiculo();
        var comprador = CriarComprador();

        _compradorRepo.Setup(r => r.ObterPorIdAsync(comprador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comprador);
        _veiculoRepo.Setup(r => r.ObterPorIdAsync(veiculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(veiculo);
        _pagamentoGateway.Setup(g => g.EmitirCodigoPagamentoAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Serviço PIX indisponível."));

        var act = () => sut.IniciarSelecaoEReservaAsync(
            new IniciarCompraReservaRequest(veiculo.Id, comprador.Id));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Reserva desfeita via compensação SAGA*");

        // Garante que a compensação devolveu o veículo para Disponível
        veiculo.Status.Should().Be(StatusVeiculo.Disponivel);
    }

    [Fact]
    public async Task ProcessarExpiracaoReservas_ComReservaVencida_DeveExecutarCompensacaoEDevolverVeiculoAoEstoque()
    {
        var sut = CriarSut();
        var veiculo = CriarVeiculo();
        veiculo.Reservar();

        var comprador = CriarComprador();
        var pedidoExpirado = new PedidoCompra(veiculo.Id, comprador.Id, veiculo.Preco, minutosExpiracaoReserva: -5);
        pedidoExpirado.RegistrarCodigoPagamento("PAY-PIX-EXPIRADO");

        _pedidoRepo.Setup(r => r.ListarReservasExpiradasAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pedidoExpirado]);
        _veiculoRepo.Setup(r => r.ObterPorIdAsync(veiculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(veiculo);

        var compensadas = await sut.ProcessarExpiracaoReservasAsync();

        compensadas.Should().Be(1);
        veiculo.Status.Should().Be(StatusVeiculo.Disponivel);
        pedidoExpirado.StatusPedido.Should().Be(StatusPedidoCompra.CanceladoPorExpiracaoReserva);
        pedidoExpirado.StatusPagamento.Should().Be(StatusPagamento.Cancelado);
    }
}
