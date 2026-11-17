using System.Text.RegularExpressions;
using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.ValueObjects;

public sealed record Cpf
{
    public string Numero { get; }

    private Cpf() => Numero = string.Empty;

    public Cpf(string numero)
    {
        var limpo = Limpar(numero);
        if (!Validar(limpo))
            throw new DomainException("CPF inválido.");

        Numero = limpo;
    }

    public static string Limpar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        return Regex.Replace(valor, @"[^\d]", "");
    }

    public static bool Validar(string? cpf)
    {
        var limpo = Limpar(cpf);
        if (limpo.Length != 11)
            return false;

        if (limpo.Distinct().Count() == 1)
            return false;

        int[] multiplicador1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] multiplicador2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

        var tempCpf = limpo[..9];
        var soma = 0;

        for (var i = 0; i < 9; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

        var resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;

        var digito = resto.ToString();
        tempCpf += digito;
        soma = 0;

        for (var i = 0; i < 10; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

        resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;

        digito += resto.ToString();
        return limpo.EndsWith(digito, StringComparison.Ordinal);
    }

    public string Formatado() =>
        Numero.Length == 11
            ? $"{Numero[..3]}.{Numero.Substring(3, 3)}.{Numero.Substring(6, 3)}-{Numero.Substring(9, 2)}"
            : Numero;

    public string Mascarado() =>
        Numero.Length == 11
            ? $"***.{Numero.Substring(3, 3)}.{Numero.Substring(6, 3)}-**"
            : "***.***.***-**";

    public override string ToString() => Numero;
}
