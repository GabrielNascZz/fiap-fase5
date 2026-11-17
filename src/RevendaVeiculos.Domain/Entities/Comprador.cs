using RevendaVeiculos.Domain.Common;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.Domain.Entities;

public class Comprador : EntityBase
{
    public string NomeCompleto { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string Telefone { get; private set; } = string.Empty;
    public DateTime DataNascimento { get; private set; }

    // Dados sensíveis para emissão de código de pagamento e faturamento
    public Cpf Cpf { get; private set; } = null!;
    public string CpfHash { get; private set; } = string.Empty;
    public string EmailHash { get; private set; } = string.Empty;
    public Endereco Endereco { get; private set; } = null!;

    // Dados sensíveis para emissão da documentação do veículo na retirada (ATPV-e / CRV / DETRAN)
    public DocumentoIdentidade Rg { get; private set; } = null!;
    public CarteiraHabilitacao Cnh { get; private set; } = null!;

    // Governança e Segurança LGPD (Lei nº 13.709/2018)
    public bool ConsentimentoLgpd { get; private set; }
    public DateTime DataConsentimentoLgpd { get; private set; }
    public string FinalidadeTratamento { get; private set; } = string.Empty;
    public bool Anonimizado { get; private set; }
    public DateTime? DataAnonimizacao { get; private set; }
    public bool Ativo { get; private set; } = true;

    protected Comprador() { }

    public Comprador(
        string nomeCompleto,
        Email email,
        string emailHash,
        string telefone,
        DateTime dataNascimento,
        Cpf cpf,
        string cpfHash,
        Endereco endereco,
        DocumentoIdentidade rg,
        CarteiraHabilitacao cnh,
        bool consentimentoLgpd)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto) || nomeCompleto.Trim().Length < 3)
            throw new DomainException("Nome completo do comprador é obrigatório e deve ter ao menos 3 caracteres.");

        if (string.IsNullOrWhiteSpace(telefone) || telefone.Trim().Length < 8)
            throw new DomainException("Telefone de contato do comprador é obrigatório.");

        var idade = CalcularIdade(dataNascimento);
        if (idade < 18)
            throw new DomainException("O comprador deve ser maior de 18 anos para adquirir e transferir veículos automotores.");

        if (!consentimentoLgpd)
            throw new DomainException("O consentimento explícito para tratamento de dados pessoais e sensíveis (LGPD) é obrigatório.");

        NomeCompleto = nomeCompleto.Trim();
        Email = email ?? throw new DomainException("E-mail é obrigatório.");
        EmailHash = emailHash ?? throw new DomainException("Hash do e-mail é obrigatório.");
        Telefone = telefone.Trim();
        DataNascimento = dataNascimento.Date;
        Cpf = cpf ?? throw new DomainException("CPF é obrigatório.");
        CpfHash = cpfHash ?? throw new DomainException("Hash do CPF é obrigatório.");
        Endereco = endereco ?? throw new DomainException("Endereço completo é obrigatório para emissão do código de pagamento e nota fiscal.");
        Rg = rg ?? throw new DomainException("Documento de identidade (RG) é obrigatório para emissão da documentação de retirada.");
        Cnh = cnh ?? throw new DomainException("Carteira Nacional de Habilitação (CNH) é obrigatória para emissão da documentação de retirada.");

        ConsentimentoLgpd = true;
        DataConsentimentoLgpd = DateTime.UtcNow;
        FinalidadeTratamento = "Execução de contrato de compra e venda de veículo automotor, faturamento financeiro e emissão de documentação de transferência veicular (Art. 7º, I e V da LGPD).";
        Anonimizado = false;
        Ativo = true;
    }

    public void AtualizarDados(
        string nomeCompleto,
        Email email,
        string emailHash,
        string telefone,
        Endereco endereco,
        DocumentoIdentidade rg,
        CarteiraHabilitacao cnh)
    {
        if (Anonimizado)
            throw new DomainException("Não é possível atualizar um cadastro que já foi anonimizado via solicitação LGPD.");

        if (string.IsNullOrWhiteSpace(nomeCompleto) || nomeCompleto.Trim().Length < 3)
            throw new DomainException("Nome completo do comprador é obrigatório.");

        if (string.IsNullOrWhiteSpace(telefone) || telefone.Trim().Length < 8)
            throw new DomainException("Telefone de contato do comprador é obrigatório.");

        NomeCompleto = nomeCompleto.Trim();
        Email = email ?? throw new DomainException("E-mail é obrigatório.");
        EmailHash = emailHash ?? throw new DomainException("Hash do e-mail é obrigatório.");
        Telefone = telefone.Trim();
        Endereco = endereco ?? throw new DomainException("Endereço é obrigatório.");
        Rg = rg ?? throw new DomainException("RG é obrigatório.");
        Cnh = cnh ?? throw new DomainException("CNH é obrigatória.");

        MarcarAtualizacao();
    }

    public void ValidarAptidaoParaCompraEPagamento()
    {
        if (!Ativo || Anonimizado)
            throw new DomainException("O cadastro do comprador está inativo ou anonimizado e não pode realizar compras.");

        if (!ConsentimentoLgpd)
            throw new DomainException("Comprador sem termo de consentimento LGPD ativo.");

        if (Cpf is null || Endereco is null)
            throw new DomainException("Dados de faturamento (CPF e Endereço) incompletos para emissão do código de pagamento.");
    }

    public void ValidarAptidaoParaRetiradaEDocumentacao(DateTime dataReferencia)
    {
        ValidarAptidaoParaCompraEPagamento();

        if (Rg is null || Cnh is null)
            throw new DomainException("Dados de documentação (RG e CNH) incompletos para emissão do documento de retirada do veículo.");

        if (!Cnh.EstaValidaNaData(dataReferencia))
            throw new DomainException("A CNH do comprador está vencida. É necessária CNH válida para emissão da documentação de retirada do veículo.");
    }

    public void AnonimizarDadosSensiveis(string hashAnonimizado)
    {
        if (Anonimizado)
            throw new DomainException("Os dados deste comprador já foram anonimizados.");

        NomeCompleto = $"TITULAR ANONIMIZADO LGPD #{Id.ToString()[..8].ToUpperInvariant()}";
        Telefone = "00000000000";
        CpfHash = hashAnonimizado;
        EmailHash = hashAnonimizado;
        ConsentimentoLgpd = false;
        Anonimizado = true;
        Ativo = false;
        DataAnonimizacao = DateTime.UtcNow;

        MarcarAtualizacao();
    }

    public string TelefoneMascarado()
    {
        if (Anonimizado || string.IsNullOrWhiteSpace(Telefone) || Telefone.Length < 4)
            return "(**) *****-****";

        return $"(**) *****-{Telefone[^4..]}";
    }

    private static int CalcularIdade(DateTime dataNascimento)
    {
        var hoje = DateTime.UtcNow.Date;
        var idade = hoje.Year - dataNascimento.Year;
        if (dataNascimento.Date > hoje.AddYears(-idade))
            idade--;

        return idade;
    }
}
