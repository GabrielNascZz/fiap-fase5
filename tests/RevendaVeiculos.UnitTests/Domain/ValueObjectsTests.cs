using FluentAssertions;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.UnitTests.Domain;

public class ValueObjectsTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    [InlineData("11144477735")]
    public void Cpf_ComNumeroValido_DeveCriarComSucessoEMascararCorretamente(string input)
    {
        var cpf = new Cpf(input);

        cpf.Numero.Should().HaveLength(11);
        cpf.Mascarado().Should().StartWith("***.").And.EndWith("-**");
        cpf.Formatado().Should().Contain(".").And.Contain("-");
    }

    [Theory]
    [InlineData("")]
    [InlineData("111.111.111-11")]
    [InlineData("12345678900")]
    [InlineData("52998224799")]
    public void Cpf_ComNumeroInvalido_DeveLancarDomainException(string input)
    {
        var act = () => new Cpf(input);
        act.Should().Throw<DomainException>().WithMessage("*CPF inválido*");
    }

    [Theory]
    [InlineData("BRA2E19", "BRA2E19")]
    [InlineData("abc-1234", "ABC1234")]
    [InlineData("fap5a25", "FAP5A25")]
    public void Placa_ComPadraoMercosulOuAntigo_DeveNormalizarComSucesso(string input, string esperado)
    {
        var placa = new Placa(input);
        placa.Valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("ABCD123")]
    public void Placa_ComFormatoInvalido_DeveLancarDomainException(string input)
    {
        var act = () => new Placa(input);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Chassi_Com17CaracteresValidos_DeveCriarComSucesso()
    {
        var chassi = new Chassi("9BWAG5BZ0RP000101");
        chassi.Codigo.Should().Be("9BWAG5BZ0RP000101");
    }

    [Theory]
    [InlineData("CURTO123")]
    [InlineData("9BWAG5BZ0RP00010I")] // letra I proibida no padrão VIN
    [InlineData("9BWAG5BZ0RP00010O")] // letra O proibida no padrão VIN
    public void Chassi_Invalido_DeveLancarDomainException(string input)
    {
        var act = () => new Chassi(input);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Email_E_Endereco_DevemMascararDadosSensiveisParaPrivacidadeLgpd()
    {
        var email = new Email("gabriel.cliente@revenda.com.br");
        var endereco = new Endereco("01310-100", "Avenida Paulista", "1106", "Conj 70", "Bela Vista", "São Paulo", "SP");

        email.Mascarado().Should().Be("ga***@revenda.com.br");
        endereco.Mascarado().Should().Contain("***").And.Contain("Bela Vista");
    }
}
