using RevendaVeiculos.Application.DTOs;

namespace RevendaVeiculos.Application.Interfaces;

public interface ICompradorUseCase
{
    Task<CompradorMascaradoResponse> CadastrarAsync(
        CadastrarCompradorRequest request,
        string usuarioExecutor = "Sistema",
        CancellationToken cancellationToken = default);

    Task<CompradorMascaradoResponse> AtualizarAsync(
        Guid id,
        AtualizarCompradorRequest request,
        string usuarioExecutor = "Sistema",
        CancellationToken cancellationToken = default);

    Task<CompradorMascaradoResponse> ObterMascaradoPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CompradorDadosCompletosResponse> ObterDadosCompletosAuditadoAsync(
        Guid id,
        string usuarioExecutor,
        string motivoAcesso,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompradorMascaradoResponse>> ListarTodosMascaradosAsync(
        CancellationToken cancellationToken = default);

    Task<CompradorMascaradoResponse> AnonimizarDadosLgpdAsync(
        Guid id,
        string usuarioExecutor,
        CancellationToken cancellationToken = default);
}
