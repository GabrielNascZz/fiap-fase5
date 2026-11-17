using RevendaVeiculos.Application.DTOs;

namespace RevendaVeiculos.Application.Interfaces;

public interface IAuditoriaUseCase
{
    Task<IReadOnlyList<AuditLogResponse>> ListarLogsRecentesAsync(
        int limite = 50,
        CancellationToken cancellationToken = default);
}
