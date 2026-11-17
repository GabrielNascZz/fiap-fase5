using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.Infrastructure.Persistence;

public class RevendaDbContext : DbContext
{
    private readonly ICryptoService _cryptoService;

    public RevendaDbContext(DbContextOptions<RevendaDbContext> options, ICryptoService cryptoService)
        : base(options)
    {
        _cryptoService = cryptoService;
    }

    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Comprador> Compradores => Set<Comprador>();
    public DbSet<PedidoCompra> PedidosCompra => Set<PedidoCompra>();
    public DbSet<SagaStepLog> SagaSteps => Set<SagaStepLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var encryptedStringConverter = new ValueConverter<string, string>(
            v => _cryptoService.Criptografar(v),
            v => _cryptoService.Descriptografar(v));

        var placaConverter = new ValueConverter<Placa, string>(
            v => v.Valor,
            v => new Placa(v));

        var chassiConverter = new ValueConverter<Chassi, string>(
            v => v.Codigo,
            v => new Chassi(v));

        var renavamConverter = new ValueConverter<Renavam, string>(
            v => v.Codigo,
            v => new Renavam(v));

        var cpfEncryptedConverter = new ValueConverter<Cpf, string>(
            v => _cryptoService.Criptografar(v.Numero),
            v => new Cpf(_cryptoService.Descriptografar(v)));

        var emailEncryptedConverter = new ValueConverter<Email, string>(
            v => _cryptoService.Criptografar(v.Endereco),
            v => new Email(_cryptoService.Descriptografar(v)));

        var enderecoEncryptedConverter = new ValueConverter<Endereco, string>(
            v => _cryptoService.Criptografar(SerializeEndereco(v)),
            v => DeserializeEndereco(_cryptoService.Descriptografar(v)));

        var rgEncryptedConverter = new ValueConverter<DocumentoIdentidade, string>(
            v => _cryptoService.Criptografar(SerializeRg(v)),
            v => DeserializeRg(_cryptoService.Descriptografar(v)));

        var cnhEncryptedConverter = new ValueConverter<CarteiraHabilitacao, string>(
            v => _cryptoService.Criptografar(SerializeCnh(v)),
            v => DeserializeCnh(_cryptoService.Descriptografar(v)));

        modelBuilder.Entity<Veiculo>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Marca).IsRequired().HasMaxLength(80);
            b.Property(x => x.Modelo).IsRequired().HasMaxLength(120);
            b.Property(x => x.Cor).IsRequired().HasMaxLength(50);
            b.Property(x => x.Preco).HasPrecision(18, 2);
            b.Property(x => x.Placa).HasConversion(placaConverter).IsRequired().HasMaxLength(10);
            b.Property(x => x.Chassi).HasConversion(chassiConverter).IsRequired().HasMaxLength(17);
            b.Property(x => x.Renavam).HasConversion(renavamConverter).IsRequired().HasMaxLength(11);
            b.Property(x => x.RowVersion).IsConcurrencyToken();
            b.HasIndex(x => x.Placa).IsUnique();
            b.HasIndex(x => x.Chassi).IsUnique();
            b.HasIndex(x => new { x.Status, x.Preco });
        });

        modelBuilder.Entity<Comprador>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.NomeCompleto).IsRequired().HasMaxLength(160);
            b.Property(x => x.Telefone).HasConversion(encryptedStringConverter).IsRequired();
            b.Property(x => x.Cpf).HasConversion(cpfEncryptedConverter).IsRequired();
            b.Property(x => x.Email).HasConversion(emailEncryptedConverter).IsRequired();
            b.Property(x => x.Endereco).HasConversion(enderecoEncryptedConverter).IsRequired();
            b.Property(x => x.Rg).HasConversion(rgEncryptedConverter).IsRequired();
            b.Property(x => x.Cnh).HasConversion(cnhEncryptedConverter).IsRequired();
            b.Property(x => x.CpfHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.EmailHash).IsRequired().HasMaxLength(64);
            b.HasIndex(x => x.CpfHash).IsUnique();
            b.HasIndex(x => x.EmailHash).IsUnique();
        });

        modelBuilder.Entity<PedidoCompra>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.ValorVenda).HasPrecision(18, 2);
            b.Property(x => x.CodigoReserva).IsRequired().HasMaxLength(40);
            b.Property(x => x.CodigoPagamento).HasMaxLength(60);
            b.HasMany(x => x.PassosSaga)
                .WithOne()
                .HasForeignKey(s => s.PedidoCompraId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.PassosSaga).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<SagaStepLog>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.NomePasso).IsRequired().HasMaxLength(150);
            b.Property(x => x.Detalhes).IsRequired().HasMaxLength(1000);
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.UsuarioResponsavel).IsRequired().HasMaxLength(100);
            b.Property(x => x.Acao).IsRequired().HasMaxLength(100);
            b.Property(x => x.Entidade).IsRequired().HasMaxLength(80);
            b.Property(x => x.EntidadeId).IsRequired().HasMaxLength(80);
            b.Property(x => x.DetalhesSeguranca).IsRequired().HasMaxLength(1500);
        });
    }

    private static string SerializeEndereco(Endereco v) =>
        JsonSerializer.Serialize(new EnderecoPersistenceModel(
            v.Cep, v.Logradouro, v.Numero, v.Complemento, v.Bairro, v.Cidade, v.Uf));

    private static Endereco DeserializeEndereco(string json)
    {
        var model = JsonSerializer.Deserialize<EnderecoPersistenceModel>(json)!;
        return new Endereco(model.Cep, model.Logradouro, model.Numero, model.Complemento, model.Bairro, model.Cidade, model.Uf);
    }

    private static string SerializeRg(DocumentoIdentidade v) =>
        JsonSerializer.Serialize(new RgPersistenceModel(v.NumeroRg, v.OrgaoEmissor, v.UfEmissor));

    private static DocumentoIdentidade DeserializeRg(string json)
    {
        var model = JsonSerializer.Deserialize<RgPersistenceModel>(json)!;
        return new DocumentoIdentidade(model.NumeroRg, model.OrgaoEmissor, model.UfEmissor);
    }

    private static string SerializeCnh(CarteiraHabilitacao v) =>
        JsonSerializer.Serialize(new CnhPersistenceModel(v.NumeroRegistro, v.Categoria, v.DataValidade));

    private static CarteiraHabilitacao DeserializeCnh(string json)
    {
        var model = JsonSerializer.Deserialize<CnhPersistenceModel>(json)!;
        return new CarteiraHabilitacao(model.NumeroRegistro, model.Categoria, model.DataValidade);
    }

    private sealed record EnderecoPersistenceModel(
        string Cep,
        string Logradouro,
        string Numero,
        string? Complemento,
        string Bairro,
        string Cidade,
        string Uf);

    private sealed record RgPersistenceModel(
        string NumeroRg,
        string OrgaoEmissor,
        string UfEmissor);

    private sealed record CnhPersistenceModel(
        string NumeroRegistro,
        string Categoria,
        DateTime DataValidade);
}
