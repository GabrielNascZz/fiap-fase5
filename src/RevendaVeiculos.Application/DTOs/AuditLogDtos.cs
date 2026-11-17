namespace RevendaVeiculos.Application.DTOs;

public record AuditLogResponse(
    Guid Id,
    string UsuarioResponsavel,
    string Acao,
    string Entidade,
    string EntidadeId,
    string DetalhesSeguranca,
    string OrigemIp,
    DateTime CriadoEm);
