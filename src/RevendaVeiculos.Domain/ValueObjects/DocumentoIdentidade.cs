using RevendaVeiculos.Domain.Exceptions;

namespace RevendaVeiculos.Domain.ValueObjects;

public sealed record DocumentoIdentidade
{
    public string NumeroRg { get; }
    public string OrgaoEmissor { get; }
    public string UfEmissor { get; }

    private DocumentoIdentidade()
    {
        NumeroRg = string.Empty;
        OrgaoEmissor = string.Empty;
        UfEmissor = string.Empty;
    }

    public DocumentoIdentidade(string numeroRg, string orgaoEmissor, string ufEmissor)
    {
        if (string.IsNullOrWhiteSpace(numeroRg) || numeroRg.Trim().Length < 5)
            throw new DomainException("Número de RG inválido. Necessário para emissão da documentação de retirada do veículo.");

        if (string.IsNullOrWhiteSpace(orgaoEmissor))
            throw new DomainException("Órgão emissor do RG é obrigatório.");

        var uf = (ufEmissor ?? string.Empty).Trim().ToUpperInvariant();
        if (uf.Length != 2)
            throw new DomainException("UF do órgão emissor do RG deve conter 2 letras.");

        NumeroRg = numeroRg.Trim().ToUpperInvariant();
        OrgaoEmissor = orgaoEmissor.Trim().ToUpperInvariant();
        UfEmissor = uf;
    }

    public string Mascarado()
    {
        if (NumeroRg.Length <= 4)
            return $"*** ({OrgaoEmissor}/{UfEmissor})";

        return $"***{NumeroRg[^3..]} ({OrgaoEmissor}/{UfEmissor})";
    }
}
