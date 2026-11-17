using System.Text.RegularExpressions;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.ValueObjects;

public sealed record Endereco
{
    public string Cep { get; }
    public string Logradouro { get; }
    public string Numero { get; }
    public string? Complemento { get; }
    public string Bairro { get; }
    public string Cidade { get; }
    public string Uf { get; }

    private Endereco()
    {
        Cep = string.Empty;
        Logradouro = string.Empty;
        Numero = string.Empty;
        Bairro = string.Empty;
        Cidade = string.Empty;
        Uf = string.Empty;
    }

    public Endereco(
        string cep,
        string logradouro,
        string numero,
        string? complemento,
        string bairro,
        string cidade,
        string uf)
    {
        var cepLimpo = Regex.Replace(cep ?? string.Empty, @"[^\d]", "");
        if (cepLimpo.Length != 8)
            throw new DomainException("CEP inválido. Deve conter 8 dígitos numéricos.");

        if (string.IsNullOrWhiteSpace(logradouro))
            throw new DomainException("Logradouro do endereço é obrigatório.");

        if (string.IsNullOrWhiteSpace(numero))
            throw new DomainException("Número do endereço é obrigatório.");

        if (string.IsNullOrWhiteSpace(bairro))
            throw new DomainException("Bairro do endereço é obrigatório.");

        if (string.IsNullOrWhiteSpace(cidade))
            throw new DomainException("Cidade do endereço é obrigatória.");

        var ufNormalizada = (uf ?? string.Empty).Trim().ToUpperInvariant();
        if (ufNormalizada.Length != 2)
            throw new DomainException("UF inválida. Deve conter 2 letras (ex: SP, RJ).");

        Cep = cepLimpo;
        Logradouro = logradouro.Trim();
        Numero = numero.Trim();
        Complemento = string.IsNullOrWhiteSpace(complemento) ? null : complemento.Trim();
        Bairro = bairro.Trim();
        Cidade = cidade.Trim();
        Uf = ufNormalizada;
    }

    public string FormatadoCompleto() =>
        $"{Logradouro}, {Numero}{(string.IsNullOrWhiteSpace(Complemento) ? "" : $" ({Complemento})")} - {Bairro}, {Cidade}/{Uf} - CEP {Cep[..5]}-{Cep[5..]}";

    public string Mascarado() =>
        $"*** - {Bairro}, {Cidade}/{Uf} - CEP {Cep[..5]}-***";
}
