using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RevendaVeiculos.Api.Middlewares;
using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Infrastructure;
using RevendaVeiculos.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 200;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 20;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FIAP Pós-Tech SOAT - Fase 5 | Plataforma de Revenda de Veículos API",
        Version = "v1.0.0",
        Description = """
            API RESTful para plataforma de revenda de veículos automotores desenvolvida com **Clean Architecture**, **DDD**, **SOLID**, **SAGA Orquestrada** e **Segurança LGPD (AES-256-GCM + HMAC-SHA256 + JWT RBAC)**.

            ### Funcionalidades Principais:
            - **Estoque de Veículos**: Cadastro, edição, listagem de veículos à venda (ordenados do mais barato para o mais caro) e listagem de veículos vendidos (ordenados do mais barato para o mais caro).
            - **Serviço de Compradores & Segurança LGPD**: Cadastro de clientes com dados básicos, dados de faturamento (para código de pagamento) e documentação de retirada (RG e CNH), criptografados em repouso via **AES-256-GCM**, busca indexada por hash **HMAC-SHA256**, mascaramento por padrão (**Privacy by Default**), RBAC JWT e anonimização LGPD.
            - **Orquestrador SAGA de Compra**: Fluxo completo (Seleção -> Reserva com controle de concorrência -> Código de Pagamento -> Confirmação/Recusa de Pagamento -> Documentação e Baixa no Estoque) e transações compensatórias automáticas (Concorrência, Pagamento não efetuado/recusado, Expiração de reserva e Desistência do cliente em qualquer etapa).
            """
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Autenticação JWT Bearer (gere um token em POST /api/v1/seguranca/token). Insira apenas o token abaixo.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? "FIAP-SOAT-FASE5-SUPER-SECRET-JWT-SIGNING-KEY-2026";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RevendaVeiculos.AuthServer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RevendaVeiculos.Api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructureAndApplication(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RevendaDbContext>();
    var cryptoService = scope.ServiceProvider.GetRequiredService<ICryptoService>();
    await DbInitializer.SeedAsync(dbContext, cryptoService);
}

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Revenda de Veículos API v1 (SAGA & LGPD)");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("fixed");

app.Run();

public partial class Program { }
