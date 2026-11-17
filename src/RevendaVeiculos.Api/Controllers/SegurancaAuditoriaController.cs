using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RevendaVeiculos.Domain.Interfaces;

namespace RevendaVeiculos.Api.Controllers;

/// <summary>
/// Endpoints de governança de segurança da informação, autenticação JWT, políticas LGPD e trilha de auditoria.
/// </summary>
[ApiController]
[Route("api/v1/seguranca")]
[Produces("application/json")]
public class SegurancaAuditoriaController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IConfiguration _configuration;

    public SegurancaAuditoriaController(
        IAuditLogRepository auditLogRepository,
        IConfiguration configuration)
    {
        _auditLogRepository = auditLogRepository;
        _configuration = configuration;
    }

    public record GerarTokenRequest(string Usuario = "gabriel.admin", string Role = "Admin");

    /// <summary>
    /// Emite um token JWT Bearer assinado (HMAC-SHA256) para autenticação e controle de acesso baseado em papéis (RBAC).
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GerarToken([FromBody] GerarTokenRequest request)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "FIAP-SOAT-FASE5-SUPER-SECRET-JWT-SIGNING-KEY-2026";
        var issuer = _configuration["Jwt:Issuer"] ?? "RevendaVeiculos.AuthServer";
        var audience = _configuration["Jwt:Audience"] ?? "RevendaVeiculos.Api";

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, request.Usuario),
            new Claim(ClaimTypes.Role, request.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiraEm = DateTime.UtcNow.AddHours(4);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiraEm,
            signingCredentials: creds);

        return Ok(new
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType = "Bearer",
            ExpiresAtUtc = expiraEm,
            Usuario = request.Usuario,
            Role = request.Role
        });
    }

    /// <summary>
    /// Consulta a trilha de auditoria de segurança (AuditLog) contendo registros imutáveis de operações
    /// de negócio, compensações SAGA, bloqueios de concorrência e acessos a dados sensíveis LGPD.
    /// </summary>
    [HttpGet("auditoria")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarLogsAuditoria(
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogRepository.ListarRecentesAsync(Math.Clamp(limite, 1, 500), cancellationToken);
        return Ok(logs);
    }

    /// <summary>
    /// Retorna o manifesto técnico das regras de segurança de dados e proteção LGPD implementadas na plataforma.
    /// </summary>
    [HttpGet("politicas-lgpd")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ObterPoliticasSegurancaLgpd()
    {
        return Ok(new
        {
            LegislacaoReferencia = "Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018)",
            CriptografiaEmRepouso = "AES-256-GCM (Authenticated Encryption with Associated Data) com Nonce aleatório de 96 bits e Tag de autenticação de 128 bits por registro.",
            IndexacaoCegaSegura = "HMAC-SHA256 determinístico (CpfHash e EmailHash) para validação de unicidade e busca indexada sem exposição de texto claro no banco de dados.",
            MascaramentoPorPadrao = "Privacy by Default: listagens e consultas padrão retornam CPF (***.456.789-**), E-mail, Telefone, RG, CNH e Endereço mascarados.",
            ControleDeAcessoEAuditoria = "Todo acesso administrativo aos dados completos descriptografados para emissão de ATPV-e/NF ou portabilidade exige justificativa e gera registro imutável em AuditLog.",
            DireitosDoTitular = "Suporte nativo à Anonimização / Direito ao Esquecimento (POST /api/v1/compradores/{id}/anonimizar) e Portabilidade/Acesso Auditado.",
            PadraoTransacionalDistribuido = "SAGA Orquestrada com controle de concorrência otimista (RowVersion) + semáforo transacional por veículo e transações compensatórias automáticas."
        });
    }
}
