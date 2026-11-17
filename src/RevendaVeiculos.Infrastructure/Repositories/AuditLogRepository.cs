using Microsoft.EntityFrameworkCore;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Infrastructure.Persistence;

namespace RevendaVeiculos.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly RevendaDbContext _context;

    public AuditLogRepository(RevendaDbContext context)
    {
        _context = context;
    }

    public async Task RegistrarAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> ListarRecentesAsync(int limite = 100, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CriadoEm)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }
}
