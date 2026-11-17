using System.Text.RegularExpressions;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.ValueObjects;

public sealed record Renavam
{
    public string Codigo { get; }

    private Renavam() => Codigo = string.Empty;

    public Renavam(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new DomainException("RENAVAM do veículo é obrigatório.");

        var limpo = Regex.Replace(codigo, @"[^\d]", "");
        if (limpo.Length is < 9 or > 11)
            throw new DomainException("RENAVAM inválido. Deve conter entre 9 e 11 dígitos numéricos.");

        Codigo = limpo.PadLeft(11, '0');
    }

    public override string ToString() => Codigo;
}
