using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Application.Interfaces;

namespace RevendaVeiculos.Api.Controllers;

/// <summary>
/// Serviço de cadastro de clientes (compradores) com criptografia em repouso (AES-256-GCM),
/// mascaramento por padrão (Privacy by Default) e conformidade com a LGPD.
/// </summary>
[ApiController]
[Route("api/v1/compradores")]
[Produces("application/json")]
public class CompradoresController : ControllerBase
{
    private readonly ICompradorUseCase _compradorUseCase;

    public CompradoresController(ICompradorUseCase compradorUseCase)
    {
        _compradorUseCase = compradorUseCase;
    }

    /// <summary>
    /// Cadastra um novo comprador armazenando dados básicos, dados de faturamento (código de pagamento)
    /// e dados para emissão da documentação do veículo na retirada (RG/CNH), criptografados em repouso.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CompradorMascaradoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarCompradorRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name ?? "API-CadastroCliente";
        var result = await _compradorUseCase.CadastrarAsync(request, usuario, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = result.Id }, result);
    }

    /// <summary>
    /// Atualiza os dados cadastrais e de documentação de um comprador existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CompradorMascaradoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarCompradorRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name ?? "API-AtualizacaoCliente";
        var result = await _compradorUseCase.AtualizarAsync(id, request, usuario, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lista todos os compradores cadastrados com dados pessoais sensíveis mascarados (Privacy by Default - LGPD).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CompradorMascaradoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarTodos(CancellationToken cancellationToken)
    {
        var result = await _compradorUseCase.ListarTodosMascaradosAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Consulta um comprador pelo ID com dados sensíveis mascarados por padrão.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompradorMascaradoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var result = await _compradorUseCase.ObterMascaradoPorIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Acesso controlado e auditado (Requer JWT Role: Admin, Vendedor ou DPO) aos dados sensíveis
    /// descriptografados do comprador para emissão de documentação de retirada/faturamento ou portabilidade LGPD.
    /// </summary>
    [HttpGet("{id:guid}/dados-completos")]
    [Authorize(Roles = "Admin,Vendedor,DPO")]
    [ProducesResponseType(typeof(CompradorDadosCompletosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterDadosCompletosAuditado(
        Guid id,
        [FromQuery] string motivoAcesso = "Emissão de documentação veicular / Portabilidade LGPD",
        CancellationToken cancellationToken = default)
    {
        var usuario = User.Identity?.Name ?? "OperadorAutorizado-LGPD";
        var result = await _compradorUseCase.ObterDadosCompletosAuditadoAsync(id, usuario, motivoAcesso, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Inspeção técnica de segurança (Requer JWT Role: Admin ou DPO) demonstrando os payloads cifrados em repouso
    /// (AES-256-GCM: ENC:v1:...) e os Blind Indexes (HMAC-SHA256) armazenados para o comprador.
    /// </summary>
    [HttpGet("{id:guid}/inspecao-criptografia")]
    [Authorize(Roles = "Admin,DPO")]
    [ProducesResponseType(typeof(InspecaoCriptografiaCompradorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InspecionarCriptografiaEmRepouso(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var usuario = User.Identity?.Name ?? "AuditorSeguranca";
        var result = await _compradorUseCase.InspecionarCriptografiaEmRepousoAsync(id, usuario, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Executa a anonimização irreversível dos dados pessoais e sensíveis do titular (Direito ao Esquecimento - Art. 18 da LGPD).
    /// Requer autenticação JWT com papel Admin ou DPO.
    /// </summary>
    [HttpPost("{id:guid}/anonimizar")]
    [Authorize(Roles = "Admin,DPO")]
    [ProducesResponseType(typeof(CompradorMascaradoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnonimizarLgpd(Guid id, CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name ?? "DPO-EncarregadoDados";
        var result = await _compradorUseCase.AnonimizarDadosLgpdAsync(id, usuario, cancellationToken);
        return Ok(result);
    }
}
