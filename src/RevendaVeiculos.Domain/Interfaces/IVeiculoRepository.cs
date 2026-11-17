using RevendaVeiculos.Domain.Entities;

namespace RevendaVeiculos.Domain.Interfaces;

public interface IVeiculoRepository
{
    Task<Veiculo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Veiculo?> ObterPorPlacaAsync(string placa, CancellationToken cancellationToken = default);
    Task<Veiculo?> ObterPorChassiAsync(string chassi, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Veiculo>> ListarVeiculosAVendaOrdenadosPorPrecoAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Veiculo>> ListarVeiculosVendidosOrdenadosPorPrecoAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Veiculo>> ListarTodosOrdenadosPorPrecoAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(Veiculo veiculo, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Veiculo veiculo, CancellationToken cancellationToken = default);
}
