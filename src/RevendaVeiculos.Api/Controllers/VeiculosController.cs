using Microsoft.AspNetCore.Mvc;
using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Application.Interfaces;

namespace RevendaVeiculos.Api.Controllers;

/// <summary>
/// Gerenciamento do estoque de veículos automotores da revenda.
/// </summary>
[ApiController]
[Route("api/v1/veiculos")]
[Produces("application/json")]
public class VeiculosController : ControllerBase
{
    private readonly IVeiculoUseCase _veiculoUseCase;

    public VeiculosController(IVeiculoUseCase veiculoUseCase)
    {
        _veiculoUseCase = veiculoUseCase;
    }

    /// <summary>
    /// Cadastra um novo veículo para venda no estoque da revenda.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(VeiculoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarVeiculoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _veiculoUseCase.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = result.Id }, result);
    }

    /// <summary>
    /// Edita os dados de um veículo cadastrado (permitido apenas para veículos disponíveis).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(VeiculoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarVeiculoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _veiculoUseCase.AtualizarAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lista os veículos à venda (Disponíveis), ordenados por preço do mais barato para o mais caro.
    /// </summary>
    [HttpGet("a-venda")]
    [ProducesResponseType(typeof(IReadOnlyList<VeiculoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarVeiculosAVenda(CancellationToken cancellationToken)
    {
        var result = await _veiculoUseCase.ListarVeiculosAVendaAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lista os veículos vendidos, ordenados por preço do mais barato para o mais caro.
    /// </summary>
    [HttpGet("vendidos")]
    [ProducesResponseType(typeof(IReadOnlyList<VeiculoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarVeiculosVendidos(CancellationToken cancellationToken)
    {
        var result = await _veiculoUseCase.ListarVeiculosVendidosAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lista todos os veículos do estoque (disponíveis, reservados e vendidos), ordenados por preço do mais barato para o mais caro.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VeiculoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarTodos(CancellationToken cancellationToken)
    {
        var result = await _veiculoUseCase.ListarTodosAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Consulta os detalhes de um veículo específico pelo seu ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VeiculoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var result = await _veiculoUseCase.ObterPorIdAsync(id, cancellationToken);
        return Ok(result);
    }
}
