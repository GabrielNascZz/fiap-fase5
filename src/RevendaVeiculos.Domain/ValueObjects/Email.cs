using System.Text.RegularExpressions;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.ValueObjects;

public sealed record Email
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Endereco { get; }

    private Email() => Endereco = string.Empty;

    public Email(string endereco)
    {
        if (string.IsNullOrWhiteSpace(endereco))
            throw new DomainException("E-mail é obrigatório.");

        var normalizado = endereco.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(normalizado))
            throw new DomainException("Formato de e-mail inválido.");

        Endereco = normalizado;
    }

    public string Mascarado()
    {
        if (string.IsNullOrWhiteSpace(Endereco) || !Endereco.Contains('@'))
            return "***@***";

        var partes = Endereco.Split('@');
        var usuario = partes[0];
        var dominio = partes[1];

        var prefixo = usuario.Length > 2 ? usuario[..2] : usuario[..1];
        return $"{prefixo}***@{dominio}";
    }

    public override string ToString() => Endereco;
}
