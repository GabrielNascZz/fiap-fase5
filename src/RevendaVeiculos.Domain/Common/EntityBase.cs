namespace RevendaVeiculos.Domain.Common;

public abstract class EntityBase
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CriadoEm { get; protected set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; protected set; }

    protected void MarcarAtualizacao()
    {
        AtualizadoEm = DateTime.UtcNow;
    }
}
