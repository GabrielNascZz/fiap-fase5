using RevendaVeiculos.Application.DTOs;

namespace RevendaVeiculos.Application.Interfaces;

public interface IVeiculoUseCase
{
    Task<VeiculoResponse> CadastrarAsync(CadastrarVeiculoRequest request, CancellationToken cancellationToken = default);
    Task<VeiculoResponse> AtualizarAsync(Guid id, AtualizarVeiculoRequest request, CancellationToken cancellationToken = default);
    Task<VeiculoResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VeiculoResponse>> ListarVeiculosAVendaAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VeiculoResponse>> ListarVeiculosVendidosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VeiculoResponse>> ListarTodosAsync(CancellationToken cancellationToken = default);
}
