using RevendaVeiculos.Domain.Entities;

namespace RevendaVeiculos.Domain.Interfaces;

public interface ICompradorRepository
{
    Task<Comprador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Comprador?> ObterPorCpfHashAsync(string cpfHash, CancellationToken cancellationToken = default);
    Task<Comprador?> ObterPorEmailHashAsync(string emailHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Comprador>> ListarTodosAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(Comprador comprador, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Comprador comprador, CancellationToken cancellationToken = default);
}
