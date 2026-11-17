using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Domain.Interfaces;

namespace RevendaVeiculos.Application.UseCases;

public class AuditoriaUseCase : IAuditoriaUseCase
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditoriaUseCase(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<IReadOnlyList<AuditLogResponse>> ListarLogsRecentesAsync(
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogRepository.ListarRecentesAsync(Math.Clamp(limite, 1, 500), cancellationToken);
        return logs
            .Select(l => new AuditLogResponse(
                l.Id,
                l.UsuarioResponsavel,
                l.Acao,
                l.Entidade,
                l.EntidadeId,
                l.DetalhesSeguranca,
                l.OrigemIp,
                l.CriadoEm))
            .ToList();
    }
}
