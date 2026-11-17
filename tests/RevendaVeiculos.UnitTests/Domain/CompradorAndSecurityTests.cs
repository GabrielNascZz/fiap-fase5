using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.ValueObjects;
using RevendaVeiculos.Infrastructure.Security;

namespace RevendaVeiculos.UnitTests.Domain;

public class CompradorAndSecurityTests
{
    [Fact]
    public void AesGcmCryptoService_DeveCriptografarComNonceAleatorioEDescriptografarComIntegridade()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Security:MasterEncryptionKey"]).Returns("TEST-KEY-AES-256-GCM-SOAT-FASE5");
        configMock.Setup(c => c["Security:HmacDeterministicKey"]).Returns("TEST-KEY-HMAC-SHA256-SOAT-FASE5");

        var crypto = new AesGcmCryptoService(configMock.Object);
        const string cpfOriginal = "52998224725";

        var cifrado1 = crypto.Criptografar(cpfOriginal);
        var cifrado2 = crypto.Criptografar(cpfOriginal);

        // Nonce aleatório garante ciphertexts diferentes para o mesmo texto claro (semântica IND-CPA)
        cifrado1.Should().StartWith("ENC:v1:");
        cifrado1.Should().NotBe(cifrado2);
        cifrado1.Should().NotContain(cpfOriginal);

        crypto.Descriptografar(cifrado1).Should().Be(cpfOriginal);
        crypto.Descriptografar(cifrado2).Should().Be(cpfOriginal);

        // Hash determinístico HMAC-SHA256 deve ser idêntico para permitir busca indexada cega
        var hash1 = crypto.GerarHashDeterministico(cpfOriginal);
        var hash2 = crypto.GerarHashDeterministico(cpfOriginal);
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
    }

    [Fact]
    public void Comprador_SemConsentimentoLgpd_OuMenorDeIdade_DeveSerRejeitado()
    {
        var cpf = new Cpf("52998224725");
        var email = new Email("teste@cliente.com");
        var endereco = new Endereco("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP");
        var rg = new DocumentoIdentidade("123456789", "SSP", "SP");
        var cnh = new CarteiraHabilitacao("12345678901", "B", DateTime.UtcNow.AddYears(2));

        var semConsentimento = () => new Comprador(
            "Cliente Sem Consentimento",
            email,
            "hash",
            "11999999999",
            new DateTime(1995, 1, 1),
            cpf,
            "hash",
            endereco,
            rg,
            cnh,
            consentimentoLgpd: false);

        semConsentimento.Should().Throw<DomainException>().WithMessage("*consentimento explícito*LGPD*");

        var menorIdade = () => new Comprador(
            "Menor de Idade",
            email,
            "hash",
            "11999999999",
            DateTime.UtcNow.AddYears(-16),
            cpf,
            "hash",
            endereco,
            rg,
            cnh,
            consentimentoLgpd: true);

        menorIdade.Should().Throw<DomainException>().WithMessage("*maior de 18 anos*");
    }

    [Fact]
    public void Comprador_ComCnhVencida_NaoDevePoderEmitirDocumentacaoDeRetirada()
    {
        var comprador = new Comprador(
            "Carlos Eduardo",
            new Email("carlos@cliente.com"),
            "hash-email",
            "11988887777",
            new DateTime(1990, 5, 10),
            new Cpf("52998224725"),
            "hash-cpf",
            new Endereco("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            new DocumentoIdentidade("123456789", "SSP", "SP"),
            new CarteiraHabilitacao("12345678901", "B", DateTime.UtcNow.AddDays(-1)),
            consentimentoLgpd: true);

        var act = () => comprador.ValidarAptidaoParaRetiradaEDocumentacao(DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("*CNH do comprador está vencida*");
    }

    [Fact]
    public void Comprador_AnonimizarDadosSensiveis_DeveDesativarCadastroEImpedirNovasCompras()
    {
        var comprador = new Comprador(
            "Ana Paula",
            new Email("ana@cliente.com"),
            "hash-email",
            "11988887777",
            new DateTime(1992, 3, 15),
            new Cpf("11144477735"),
            "hash-cpf",
            new Endereco("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            new DocumentoIdentidade("123456789", "SSP", "SP"),
            new CarteiraHabilitacao("12345678901", "B", DateTime.UtcNow.AddYears(2)),
            consentimentoLgpd: true);

        comprador.AnonimizarDadosSensiveis("HASH-ANONIMO-LGPD");

        comprador.Anonimizado.Should().BeTrue();
        comprador.Ativo.Should().BeFalse();
        comprador.ConsentimentoLgpd.Should().BeFalse();
        comprador.NomeCompleto.Should().StartWith("TITULAR ANONIMIZADO LGPD");

        var tentarComprar = () => comprador.ValidarAptidaoParaCompraEPagamento();
        tentarComprar.Should().Throw<DomainException>().WithMessage("*inativo ou anonimizado*");
    }
}
