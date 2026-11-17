using System.Text.RegularExpressions;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.ValueObjects;

public sealed record Chassi
{
    private static readonly Regex ChassiRegex = new(
        @"^[A-HJ-NPR-Z0-9]{17}$",
        RegexOptions.Compiled);

    public string Codigo { get; }

    private Chassi() => Codigo = string.Empty;

    public Chassi(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new DomainException("Chassi (VIN) do veículo é obrigatório.");

        var normalizado = codigo.Trim().ToUpperInvariant();
        if (!ChassiRegex.IsMatch(normalizado))
            throw new DomainException("Chassi (VIN) inválido. Deve conter exatamente 17 caracteres alfanuméricos válidos (sem I, O ou Q).");

        Codigo = normalizado;
    }

    public override string ToString() => Codigo;
}
