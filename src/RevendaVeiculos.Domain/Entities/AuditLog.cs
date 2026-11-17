using RevendaVeiculos.Domain.Common;

namespace RevendaVeiculos.Domain.Entities;

public class AuditLog : EntityBase
{
    public string UsuarioResponsavel { get; private set; } = string.Empty;
    public string Acao { get; private set; } = string.Empty;
    public string Entidade { get; private set; } = string.Empty;
    public string EntidadeId { get; private set; } = string.Empty;
    public string DetalhesSeguranca { get; private set; } = string.Empty;
    public string OrigemIp { get; private set; } = string.Empty;

    protected AuditLog() { }

    public AuditLog(
        string usuarioResponsavel,
        string acao,
        string entidade,
        string entidadeId,
        string detalhesSeguranca,
        string origemIp = "127.0.0.1")
    {
        UsuarioResponsavel = string.IsNullOrWhiteSpace(usuarioResponsavel) ? "Sistema" : usuarioResponsavel;
        Acao = acao;
        Entidade = entidade;
        EntidadeId = entidadeId;
        DetalhesSeguranca = detalhesSeguranca;
        OrigemIp = string.IsNullOrWhiteSpace(origemIp) ? "127.0.0.1" : origemIp;
    }
}
