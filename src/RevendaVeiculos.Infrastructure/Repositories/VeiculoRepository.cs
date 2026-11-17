using Microsoft.EntityFrameworkCore;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Enums;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Domain.ValueObjects;
using RevendaVeiculos.Infrastructure.Persistence;

namespace RevendaVeiculos.Infrastructure.Repositories;

public class VeiculoRepository : IVeiculoRepository
{
    private readonly RevendaDbContext _context;

    public VeiculoRepository(RevendaDbContext context)
    {
        _context = context;
    }

    public async Task<Veiculo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Veiculos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Veiculo?> ObterPorPlacaAsync(string placa, CancellationToken cancellationToken = default)
    {
        var vo = new Placa(placa);
        return await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa == vo, cancellationToken);
    }

    public async Task<Veiculo?> ObterPorChassiAsync(string chassi, CancellationToken cancellationToken = default)
    {
        var vo = new Chassi(chassi);
        return await _context.Veiculos.FirstOrDefaultAsync(v => v.Chassi == vo, cancellationToken);
    }

    public async Task<IReadOnlyList<Veiculo>> ListarVeiculosAVendaOrdenadosPorPrecoAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Veiculos
            .AsNoTracking()
            .Where(v => v.Status == StatusVeiculo.Disponivel)
            .OrderBy(v => v.Preco)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Veiculo>> ListarVeiculosVendidosOrdenadosPorPrecoAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Veiculos
            .AsNoTracking()
            .Where(v => v.Status == StatusVeiculo.Vendido)
            .OrderBy(v => v.Preco)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Veiculo>> ListarTodosOrdenadosPorPrecoAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Veiculos
            .AsNoTracking()
            .OrderBy(v => v.Preco)
            .ToListAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Veiculo veiculo, CancellationToken cancellationToken = default)
    {
        await _context.Veiculos.AddAsync(veiculo, cancellationToken);
    }

    public Task AtualizarAsync(Veiculo veiculo, CancellationToken cancellationToken = default)
    {
        _context.Veiculos.Update(veiculo);
        return Task.CompletedTask;
    }
}
