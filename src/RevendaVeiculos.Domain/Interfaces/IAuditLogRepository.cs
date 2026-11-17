using RevendaVeiculos.Domain.Entities;

namespace RevendaVeiculos.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task RegistrarAsync(AuditLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> ListarRecentesAsync(int limite = 100, CancellationToken cancellationToken = default);
}
