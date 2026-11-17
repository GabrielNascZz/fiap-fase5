using System.Text.RegularExpressions;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.ValueObjects;

public sealed record Placa
{
    private static readonly Regex PlacaRegex = new(
        @"^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$",
        RegexOptions.Compiled);

    public string Valor { get; }

    private Placa() => Valor = string.Empty;

    public Placa(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException("Placa do veículo é obrigatória.");

        var normalizada = valor.Replace("-", "").Trim().ToUpperInvariant();
        if (!PlacaRegex.IsMatch(normalizada))
            throw new DomainException("Placa inválida. Utilize o padrão Mercosul (ABC1D23) ou antigo (ABC1234).");

        Valor = normalizada;
    }

    public override string ToString() => Valor;
}
