using RevendaVeiculos.Domain.Enums;

namespace RevendaVeiculos.Application.DTOs;

public record CadastrarVeiculoRequest(
    string Marca,
    string Modelo,
    int AnoFabricacao,
    int AnoModelo,
    string Cor,
    decimal Preco,
    string Placa,
    string Chassi,
    string Renavam,
    int Quilometragem,
    TipoCombustivel Combustivel,
    TipoCambio Cambio,
    string? Descricao);

public record AtualizarVeiculoRequest(
    string Marca,
    string Modelo,
    int AnoFabricacao,
    int AnoModelo,
    string Cor,
    decimal Preco,
    int Quilometragem,
    TipoCombustivel Combustivel,
    TipoCambio Cambio,
    string? Descricao);

public record VeiculoResponse(
    Guid Id,
    string Marca,
    string Modelo,
    int AnoFabricacao,
    int AnoModelo,
    string Cor,
    decimal Preco,
    string Placa,
    string Chassi,
    string Renavam,
    int Quilometragem,
    string Combustivel,
    string Cambio,
    string? Descricao,
    string Status,
    Guid RowVersion,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    DateTime? DataVenda);
