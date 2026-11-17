using RevendaVeiculos.Domain.Common;
using RevendaVeiculos.Domain.Enums;

namespace RevendaVeiculos.Domain.Entities;

public class SagaStepLog : EntityBase
{
    public Guid PedidoCompraId { get; private set; }
    public string NomePasso { get; private set; } = string.Empty;
    public TipoPassoSaga TipoPasso { get; private set; }
    public bool Sucesso { get; private set; }
    public string Detalhes { get; private set; } = string.Empty;

    protected SagaStepLog() { }

    public SagaStepLog(
        Guid pedidoCompraId,
        string nomePasso,
        TipoPassoSaga tipoPasso,
        bool sucesso,
        string detalhes)
    {
        PedidoCompraId = pedidoCompraId;
        NomePasso = nomePasso;
        TipoPasso = tipoPasso;
        Sucesso = sucesso;
        Detalhes = detalhes;
    }
}
