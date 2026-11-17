using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.Application.UseCases;

public class VeiculoUseCase : IVeiculoUseCase
{
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VeiculoUseCase(
        IVeiculoRepository veiculoRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork)
    {
        _veiculoRepository = veiculoRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VeiculoResponse> CadastrarAsync(
        CadastrarVeiculoRequest request,
        CancellationToken cancellationToken = default)
    {
        var placa = new Placa(request.Placa);
        var chassi = new Chassi(request.Chassi);
        var renavam = new Renavam(request.Renavam);

        var existentePlaca = await _veiculoRepository.ObterPorPlacaAsync(placa.Valor, cancellationToken);
        if (existentePlaca is not null)
            throw new DomainException($"Já existe um veículo cadastrado com a placa {placa.Valor}.");

        var existenteChassi = await _veiculoRepository.ObterPorChassiAsync(chassi.Codigo, cancellationToken);
        if (existenteChassi is not null)
            throw new DomainException($"Já existe um veículo cadastrado com o chassi {chassi.Codigo}.");

        var veiculo = new Veiculo(
            request.Marca,
            request.Modelo,
            request.AnoFabricacao,
            request.AnoModelo,
            request.Cor,
            request.Preco,
            placa,
            chassi,
            renavam,
            request.Quilometragem,
            request.Combustivel,
            request.Cambio,
            request.Descricao);

        await _veiculoRepository.AdicionarAsync(veiculo, cancellationToken);
        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                "Vendedor",
                "CADASTRO_VEICULO",
                nameof(Veiculo),
                veiculo.Id.ToString(),
                $"Veículo {veiculo.Marca} {veiculo.Modelo} ({veiculo.Placa}) cadastrado por R$ {veiculo.Preco:N2}."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MapToResponse(veiculo);
    }

    public async Task<VeiculoResponse> AtualizarAsync(
        Guid id,
        AtualizarVeiculoRequest request,
        CancellationToken cancellationToken = default)
    {
        var veiculo = await _veiculoRepository.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Veículo com ID {id} não encontrado.");

        veiculo.AtualizarDados(
            request.Marca,
            request.Modelo,
            request.AnoFabricacao,
            request.AnoModelo,
            request.Cor,
            request.Preco,
            request.Quilometragem,
            request.Combustivel,
            request.Cambio,
            request.Descricao);

        await _veiculoRepository.AtualizarAsync(veiculo, cancellationToken);
        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                "Vendedor",
                "ATUALIZACAO_VEICULO",
                nameof(Veiculo),
                veiculo.Id.ToString(),
                $"Dados do veículo {veiculo.Marca} {veiculo.Modelo} ({veiculo.Placa}) atualizados. Novo preço: R$ {veiculo.Preco:N2}."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MapToResponse(veiculo);
    }

    public async Task<VeiculoResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var veiculo = await _veiculoRepository.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Veículo com ID {id} não encontrado.");

        return MapToResponse(veiculo);
    }

    public async Task<IReadOnlyList<VeiculoResponse>> ListarVeiculosAVendaAsync(CancellationToken cancellationToken = default)
    {
        var veiculos = await _veiculoRepository.ListarVeiculosAVendaOrdenadosPorPrecoAsync(cancellationToken);
        return veiculos.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<VeiculoResponse>> ListarVeiculosVendidosAsync(CancellationToken cancellationToken = default)
    {
        var veiculos = await _veiculoRepository.ListarVeiculosVendidosOrdenadosPorPrecoAsync(cancellationToken);
        return veiculos.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<VeiculoResponse>> ListarTodosAsync(CancellationToken cancellationToken = default)
    {
        var veiculos = await _veiculoRepository.ListarTodosOrdenadosPorPrecoAsync(cancellationToken);
        return veiculos.Select(MapToResponse).ToList();
    }

    internal static VeiculoResponse MapToResponse(Veiculo v) =>
        new(
            v.Id,
            v.Marca,
            v.Modelo,
            v.AnoFabricacao,
            v.AnoModelo,
            v.Cor,
            v.Preco,
            v.Placa.Valor,
            v.Chassi.Codigo,
            v.Renavam.Codigo,
            v.Quilometragem,
            v.Combustivel.ToString(),
            v.Cambio.ToString(),
            v.Descricao,
            v.Status.ToString(),
            v.RowVersion,
            v.CriadoEm,
            v.AtualizadoEm,
            v.DataVenda);
}
