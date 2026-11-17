using System.Text.RegularExpressions;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.ValueObjects;

public sealed record CarteiraHabilitacao
{
    public string NumeroRegistro { get; }
    public string Categoria { get; }
    public DateTime DataValidade { get; }

    private CarteiraHabilitacao()
    {
        NumeroRegistro = string.Empty;
        Categoria = string.Empty;
    }

    public CarteiraHabilitacao(string numeroRegistro, string categoria, DateTime dataValidade)
    {
        var limpo = Regex.Replace(numeroRegistro ?? string.Empty, @"[^\d]", "");
        if (limpo.Length is < 9 or > 11)
            throw new DomainException("Número de registro da CNH inválido. Deve conter entre 9 e 11 dígitos.");

        var catNormalizada = (categoria ?? string.Empty).Trim().ToUpperInvariant();
        string[] categoriasValidas = ["A", "B", "AB", "C", "D", "E", "AC", "AD", "AE"];
        if (!categoriasValidas.Contains(catNormalizada))
            throw new DomainException("Categoria da CNH inválida.");

        NumeroRegistro = limpo.PadLeft(11, '0');
        Categoria = catNormalizada;
        DataValidade = dataValidade.Date;
    }

    public bool EstaValidaNaData(DateTime dataReferencia) => DataValidade.Date >= dataReferencia.Date;

    public string Mascarado() =>
        NumeroRegistro.Length >= 4
            ? $"*******{NumeroRegistro[^4..]} (Cat. {Categoria})"
            : $"*** (Cat. {Categoria})";
}
