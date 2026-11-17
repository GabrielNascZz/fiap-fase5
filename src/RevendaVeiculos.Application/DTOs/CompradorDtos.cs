namespace RevendaVeiculos.Application.DTOs;

public record EnderecoDto(
    string Cep,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Uf);

public record DocumentoIdentidadeDto(
    string NumeroRg,
    string OrgaoEmissor,
    string UfEmissor);

public record CarteiraHabilitacaoDto(
    string NumeroRegistro,
    string Categoria,
    DateTime DataValidade);

public record CadastrarCompradorRequest(
    string NomeCompleto,
    string Email,
    string Telefone,
    DateTime DataNascimento,
    string Cpf,
    EnderecoDto Endereco,
    DocumentoIdentidadeDto Rg,
    CarteiraHabilitacaoDto Cnh,
    bool ConsentimentoLgpd);

public record AtualizarCompradorRequest(
    string NomeCompleto,
    string Email,
    string Telefone,
    EnderecoDto Endereco,
    DocumentoIdentidadeDto Rg,
    CarteiraHabilitacaoDto Cnh);

public record CompradorMascaradoResponse(
    Guid Id,
    string NomeCompleto,
    string EmailMascarado,
    string TelefoneMascarado,
    string CpfMascarado,
    string RgMascarado,
    string CnhMascarada,
    string EnderecoMascarado,
    bool ConsentimentoLgpd,
    DateTime DataConsentimentoLgpd,
    bool Anonimizado,
    bool Ativo,
    DateTime CriadoEm);

public record CompradorDadosCompletosResponse(
    Guid Id,
    string NomeCompleto,
    string Email,
    string Telefone,
    DateTime DataNascimento,
    string CpfFormatado,
    EnderecoDto Endereco,
    DocumentoIdentidadeDto Rg,
    CarteiraHabilitacaoDto Cnh,
    bool ConsentimentoLgpd,
    DateTime DataConsentimentoLgpd,
    string FinalidadeTratamento,
    bool Anonimizado,
    DateTime CriadoEm);

public record InspecaoCriptografiaCompradorResponse(
    Guid CompradorId,
    string AlgoritmoCriptografia,
    string AlgoritmoBlindIndex,
    string CpfMascarado,
    string CpfCifradoEmRepousoAes256Gcm,
    string CpfBlindIndexHmacSha256,
    string EmailCifradoEmRepousoAes256Gcm,
    string EmailBlindIndexHmacSha256,
    string RgCifradoEmRepousoAes256Gcm,
    string CnhCifradaEmRepousoAes256Gcm,
    string EnderecoCifradoEmRepousoAes256Gcm);
