using FluentAssertions;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Enums;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.UnitTests.Domain;

public class VeiculoDomainTests
{
    private static Veiculo CriarVeiculoPadrao() =>
        new(
            "Honda",
            "Civic Touring 1.5 Turbo",
            2023,
            2024,
            "Branco Estelar",
            159900m,
            new Placa("CIV9A88"),
            new Chassi("93HFC1690PZ000111"),
            new Renavam("11223344556"),
            12000,
            TipoCombustivel.Gasolina,
            TipoCambio.CVT,
            "Teto solar, impecável.");

    [Fact]
    public void Veiculo_RecemCriado_DeveEstarDisponivelEPermitirEdicao()
    {
        var veiculo = CriarVeiculoPadrao();
        var rowVersionInicial = veiculo.RowVersion;

        veiculo.Status.Should().Be(StatusVeiculo.Disponivel);

        veiculo.AtualizarDados(
            "Honda",
            "Civic Touring 1.5 Turbo",
            2023,
            2024,
            "Preto Cristal",
            154900m,
            12500,
            TipoCombustivel.Gasolina,
            TipoCambio.CVT,
            "Preço reduzido.");

        veiculo.Cor.Should().Be("Preto Cristal");
        veiculo.Preco.Should().Be(154900m);
        veiculo.RowVersion.Should().NotBe(rowVersionInicial);
    }

    [Fact]
    public void Veiculo_JaReservado_DeveLancarConflitoConcorrenciaSeOutroClienteTentarReservar()
    {
        var veiculo = CriarVeiculoPadrao();
        veiculo.Reservar();

        veiculo.Status.Should().Be(StatusVeiculo.Reservado);

        var segundaReserva = () => veiculo.Reservar();
        segundaReserva.Should().Throw<ConflitoConcorrenciaException>()
            .WithMessage("*reservado por outro(a) cliente*");
    }

    [Fact]
    public void Veiculo_Vendido_NaoDevePermitirEdicaoNemNovaReserva()
    {
        var veiculo = CriarVeiculoPadrao();
        veiculo.Reservar();
        veiculo.ConfirmarBaixaEstoque();

        veiculo.Status.Should().Be(StatusVeiculo.Vendido);
        veiculo.DataVenda.Should().NotBeNull();

        var editarAcao = () => veiculo.AtualizarDados(
            "Honda", "Civic", 2023, 2024, "Azul", 100000m, 12000, TipoCombustivel.Gasolina, TipoCambio.CVT);

        editarAcao.Should().Throw<DomainException>()
            .WithMessage("*Não é permitido editar os dados de um veículo que já foi vendido*");
    }

    [Fact]
    public void Veiculo_LiberarReserva_DeveRetornarStatusParaDisponivelNoEstoque()
    {
        var veiculo = CriarVeiculoPadrao();
        veiculo.Reservar();
        veiculo.Status.Should().Be(StatusVeiculo.Reservado);

        veiculo.LiberarReserva();

        veiculo.Status.Should().Be(StatusVeiculo.Disponivel);
        veiculo.DataVenda.Should().BeNull();
    }
}
