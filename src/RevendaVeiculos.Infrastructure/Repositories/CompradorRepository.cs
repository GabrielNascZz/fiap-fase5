using Microsoft.EntityFrameworkCore;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Infrastructure.Persistence;

namespace RevendaVeiculos.Infrastructure.Repositories;

public class CompradorRepository : ICompradorRepository
{
    private readonly RevendaDbContext _context;

    public CompradorRepository(RevendaDbContext context)
    {
        _context = context;
    }

    public async Task<Comprador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Compradores.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Comprador?> ObterPorCpfHashAsync(string cpfHash, CancellationToken cancellationToken = default)
    {
        return await _context.Compradores.FirstOrDefaultAsync(c => c.CpfHash == cpfHash, cancellationToken);
    }

    public async Task<Comprador?> ObterPorEmailHashAsync(string emailHash, CancellationToken cancellationToken = default)
    {
        return await _context.Compradores.FirstOrDefaultAsync(c => c.EmailHash == emailHash, cancellationToken);
    }

    public async Task<IReadOnlyList<Comprador>> ListarTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Compradores
            .AsNoTracking()
            .OrderBy(c => c.NomeCompleto)
            .ToListAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Comprador comprador, CancellationToken cancellationToken = default)
    {
        await _context.Compradores.AddAsync(comprador, cancellationToken);
    }

    public Task AtualizarAsync(Comprador comprador, CancellationToken cancellationToken = default)
    {
        _context.Compradores.Update(comprador);
        return Task.CompletedTask;
    }
}
