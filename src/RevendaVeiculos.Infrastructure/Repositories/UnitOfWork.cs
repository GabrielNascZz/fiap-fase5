using Microsoft.EntityFrameworkCore;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Infrastructure.Persistence;

namespace RevendaVeiculos.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly RevendaDbContext _context;

    public UnitOfWork(RevendaDbContext context)
    {
        _context = context;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoConcorrenciaException(
                $"Conflito de concorrência detectado: o veículo foi atualizado ou reservado simultaneamente por outro(a) comprador(a). ({ex.Message})");
        }
    }
}
