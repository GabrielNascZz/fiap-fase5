using RevendaVeiculos.Domain.Common;
using RevendaVeiculos.Domain.Enums;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.Domain.Entities;

public class Veiculo : EntityBase
{
    public string Marca { get; private set; } = string.Empty;
    public string Modelo { get; private set; } = string.Empty;
    public int AnoFabricacao { get; private set; }
    public int AnoModelo { get; private set; }
    public string Cor { get; private set; } = string.Empty;
    public decimal Preco { get; private set; }
    public Placa Placa { get; private set; } = null!;
    public Chassi Chassi { get; private set; } = null!;
    public Renavam Renavam { get; private set; } = null!;
    public int Quilometragem { get; private set; }
    public TipoCombustivel Combustivel { get; private set; }
    public TipoCambio Cambio { get; private set; }
    public string? Descricao { get; private set; }
    public StatusVeiculo Status { get; private set; }
    public Guid RowVersion { get; private set; } = Guid.NewGuid();
    public DateTime? DataVenda { get; private set; }

    protected Veiculo() { }

    public Veiculo(
        string marca,
        string modelo,
        int anoFabricacao,
        int anoModelo,
        string cor,
        decimal preco,
        Placa placa,
        Chassi chassi,
        Renavam renavam,
        int quilometragem,
        TipoCombustivel combustivel,
        TipoCambio cambio,
        string? descricao = null)
    {
        ValidarDadosBasicos(marca, modelo, anoFabricacao, anoModelo, cor, preco, quilometragem);

        Marca = marca.Trim();
        Modelo = modelo.Trim();
        AnoFabricacao = anoFabricacao;
        AnoModelo = anoModelo;
        Cor = cor.Trim();
        Preco = preco;
        Placa = placa ?? throw new DomainException("Placa é obrigatória.");
        Chassi = chassi ?? throw new DomainException("Chassi é obrigatório.");
        Renavam = renavam ?? throw new DomainException("RENAVAM é obrigatório.");
        Quilometragem = quilometragem;
        Combustivel = combustivel;
        Cambio = cambio;
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        Status = StatusVeiculo.Disponivel;
        RowVersion = Guid.NewGuid();
    }

    public void AtualizarDados(
        string marca,
        string modelo,
        int anoFabricacao,
        int anoModelo,
        string cor,
        decimal preco,
        int quilometragem,
        TipoCombustivel combustivel,
        TipoCambio cambio,
        string? descricao = null)
    {
        if (Status == StatusVeiculo.Vendido)
            throw new DomainException("Não é permitido editar os dados de um veículo que já foi vendido.");

        if (Status == StatusVeiculo.Reservado)
            throw new DomainException("Não é permitido alterar dados ou preço de um veículo enquanto estiver reservado em processo de compra.");

        ValidarDadosBasicos(marca, modelo, anoFabricacao, anoModelo, cor, preco, quilometragem);

        Marca = marca.Trim();
        Modelo = modelo.Trim();
        AnoFabricacao = anoFabricacao;
        AnoModelo = anoModelo;
        Cor = cor.Trim();
        Preco = preco;
        Quilometragem = quilometragem;
        Combustivel = combustivel;
        Cambio = cambio;
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();

        AtualizarVersaoConcorrencia();
    }

    public void Reservar()
    {
        if (Status == StatusVeiculo.Reservado)
            throw new ConflitoConcorrenciaException("Este veículo acabou de ser reservado por outro(a) cliente e não está disponível no momento.");

        if (Status == StatusVeiculo.Vendido)
            throw new ConflitoConcorrenciaException("Este veículo já foi vendido e não pode ser reservado.");

        Status = StatusVeiculo.Reservado;
        AtualizarVersaoConcorrencia();
    }

    public void ConfirmarBaixaEstoque()
    {
        if (Status != StatusVeiculo.Reservado)
            throw new DomainException($"Para realizar a baixa no estoque (retirada), o veículo deve estar Reservado. Status atual: {Status}.");

        Status = StatusVeiculo.Vendido;
        DataVenda = DateTime.UtcNow;
        AtualizarVersaoConcorrencia();
    }

    public void LiberarReserva()
    {
        if (Status == StatusVeiculo.Disponivel)
            return;

        Status = StatusVeiculo.Disponivel;
        DataVenda = null;
        AtualizarVersaoConcorrencia();
    }

    private void AtualizarVersaoConcorrencia()
    {
        RowVersion = Guid.NewGuid();
        MarcarAtualizacao();
    }

    private static void ValidarDadosBasicos(
        string marca,
        string modelo,
        int anoFabricacao,
        int anoModelo,
        string cor,
        decimal preco,
        int quilometragem)
    {
        if (string.IsNullOrWhiteSpace(marca))
            throw new DomainException("Marca do veículo é obrigatória.");

        if (string.IsNullOrWhiteSpace(modelo))
            throw new DomainException("Modelo do veículo é obrigatório.");

        var anoMaximo = DateTime.UtcNow.Year + 1;
        if (anoFabricacao < 1900 || anoFabricacao > anoMaximo)
            throw new DomainException($"Ano de fabricação inválido ({anoFabricacao}).");

        if (anoModelo < anoFabricacao || anoModelo > anoFabricacao + 1)
            throw new DomainException("Ano do modelo deve ser igual ou no máximo 1 ano superior ao ano de fabricação.");

        if (string.IsNullOrWhiteSpace(cor))
            throw new DomainException("Cor do veículo é obrigatória.");

        if (preco <= 0)
            throw new DomainException("O preço do veículo deve ser maior que zero.");

        if (quilometragem < 0)
            throw new DomainException("A quilometragem não pode ser negativa.");
    }
}
