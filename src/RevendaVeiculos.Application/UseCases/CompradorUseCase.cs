using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Exceptions;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.Application.UseCases;

public class CompradorUseCase : ICompradorUseCase
{
    private readonly ICompradorRepository _compradorRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICryptoService _cryptoService;
    private readonly IUnitOfWork _unitOfWork;

    public CompradorUseCase(
        ICompradorRepository compradorRepository,
        IAuditLogRepository auditLogRepository,
        ICryptoService cryptoService,
        IUnitOfWork unitOfWork)
    {
        _compradorRepository = compradorRepository;
        _auditLogRepository = auditLogRepository;
        _cryptoService = cryptoService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CompradorMascaradoResponse> CadastrarAsync(
        CadastrarCompradorRequest request,
        string usuarioExecutor = "Sistema",
        CancellationToken cancellationToken = default)
    {
        var cpf = new Cpf(request.Cpf);
        var email = new Email(request.Email);

        var cpfHash = _cryptoService.GerarHashDeterministico(cpf.Numero);
        var emailHash = _cryptoService.GerarHashDeterministico(email.Endereco);

        var existenteCpf = await _compradorRepository.ObterPorCpfHashAsync(cpfHash, cancellationToken);
        if (existenteCpf is not null)
            throw new DomainException("Já existe um comprador cadastrado com este CPF.");

        var existenteEmail = await _compradorRepository.ObterPorEmailHashAsync(emailHash, cancellationToken);
        if (existenteEmail is not null)
            throw new DomainException("Já existe um comprador cadastrado com este e-mail.");

        var endereco = new Endereco(
            request.Endereco.Cep,
            request.Endereco.Logradouro,
            request.Endereco.Numero,
            request.Endereco.Complemento,
            request.Endereco.Bairro,
            request.Endereco.Cidade,
            request.Endereco.Uf);

        var rg = new DocumentoIdentidade(
            request.Rg.NumeroRg,
            request.Rg.OrgaoEmissor,
            request.Rg.UfEmissor);

        var cnh = new CarteiraHabilitacao(
            request.Cnh.NumeroRegistro,
            request.Cnh.Categoria,
            request.Cnh.DataValidade);

        var comprador = new Comprador(
            request.NomeCompleto,
            email,
            emailHash,
            request.Telefone,
            request.DataNascimento,
            cpf,
            cpfHash,
            endereco,
            rg,
            cnh,
            request.ConsentimentoLgpd);

        await _compradorRepository.AdicionarAsync(comprador, cancellationToken);
        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                usuarioExecutor,
                "CADASTRO_COMPRADOR_LGPD",
                nameof(Comprador),
                comprador.Id.ToString(),
                $"Comprador cadastrado com consentimento LGPD ativo. CPF mascarado: {cpf.Mascarado()}, E-mail mascarado: {email.Mascarado()}."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MapToMascarado(comprador);
    }

    public async Task<CompradorMascaradoResponse> AtualizarAsync(
        Guid id,
        AtualizarCompradorRequest request,
        string usuarioExecutor = "Sistema",
        CancellationToken cancellationToken = default)
    {
        var comprador = await _compradorRepository.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador com ID {id} não encontrado.");

        var email = new Email(request.Email);
        var emailHash = _cryptoService.GerarHashDeterministico(email.Endereco);

        var outroComMesmoEmail = await _compradorRepository.ObterPorEmailHashAsync(emailHash, cancellationToken);
        if (outroComMesmoEmail is not null && outroComMesmoEmail.Id != id)
            throw new DomainException("Já existe outro comprador cadastrado com este e-mail.");

        var endereco = new Endereco(
            request.Endereco.Cep,
            request.Endereco.Logradouro,
            request.Endereco.Numero,
            request.Endereco.Complemento,
            request.Endereco.Bairro,
            request.Endereco.Cidade,
            request.Endereco.Uf);

        var rg = new DocumentoIdentidade(
            request.Rg.NumeroRg,
            request.Rg.OrgaoEmissor,
            request.Rg.UfEmissor);

        var cnh = new CarteiraHabilitacao(
            request.Cnh.NumeroRegistro,
            request.Cnh.Categoria,
            request.Cnh.DataValidade);

        comprador.AtualizarDados(
            request.NomeCompleto,
            email,
            emailHash,
            request.Telefone,
            endereco,
            rg,
            cnh);

        await _compradorRepository.AtualizarAsync(comprador, cancellationToken);
        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                usuarioExecutor,
                "ATUALIZACAO_DADOS_SENSIVEIS_COMPRADOR",
                nameof(Comprador),
                comprador.Id.ToString(),
                $"Dados cadastrais/sensíveis do comprador {comprador.Cpf.Mascarado()} atualizados com criptografia em repouso."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MapToMascarado(comprador);
    }

    public async Task<CompradorMascaradoResponse> ObterMascaradoPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var comprador = await _compradorRepository.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador com ID {id} não encontrado.");

        return MapToMascarado(comprador);
    }

    public async Task<CompradorDadosCompletosResponse> ObterDadosCompletosAuditadoAsync(
        Guid id,
        string usuarioExecutor,
        string motivoAcesso,
        CancellationToken cancellationToken = default)
    {
        var comprador = await _compradorRepository.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador com ID {id} não encontrado.");

        if (comprador.Anonimizado)
            throw new DomainException("Os dados deste titular foram anonimizados em conformidade com a LGPD e não estão mais disponíveis.");

        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                usuarioExecutor,
                "ACESSO_DADOS_SENSIVEIS_COMPLETOS",
                nameof(Comprador),
                comprador.Id.ToString(),
                $"Descriptografia e acesso autorizado aos dados completos do titular ({comprador.Cpf.Mascarado()}). Finalidade/Motivo: {motivoAcesso}"),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return new CompradorDadosCompletosResponse(
            comprador.Id,
            comprador.NomeCompleto,
            comprador.Email.Endereco,
            comprador.Telefone,
            comprador.DataNascimento,
            comprador.Cpf.Formatado(),
            new EnderecoDto(
                comprador.Endereco.Cep,
                comprador.Endereco.Logradouro,
                comprador.Endereco.Numero,
                comprador.Endereco.Complemento,
                comprador.Endereco.Bairro,
                comprador.Endereco.Cidade,
                comprador.Endereco.Uf),
            new DocumentoIdentidadeDto(
                comprador.Rg.NumeroRg,
                comprador.Rg.OrgaoEmissor,
                comprador.Rg.UfEmissor),
            new CarteiraHabilitacaoDto(
                comprador.Cnh.NumeroRegistro,
                comprador.Cnh.Categoria,
                comprador.Cnh.DataValidade),
            comprador.ConsentimentoLgpd,
            comprador.DataConsentimentoLgpd,
            comprador.FinalidadeTratamento,
            comprador.Anonimizado,
            comprador.CriadoEm);
    }

    public async Task<IReadOnlyList<CompradorMascaradoResponse>> ListarTodosMascaradosAsync(
        CancellationToken cancellationToken = default)
    {
        var compradores = await _compradorRepository.ListarTodosAsync(cancellationToken);
        return compradores.Select(MapToMascarado).ToList();
    }

    public async Task<CompradorMascaradoResponse> AnonimizarDadosLgpdAsync(
        Guid id,
        string usuarioExecutor,
        CancellationToken cancellationToken = default)
    {
        var comprador = await _compradorRepository.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador com ID {id} não encontrado.");

        var hashAnonimo = _cryptoService.GerarHashDeterministico($"ANON-{comprador.Id}-{Guid.NewGuid()}");
        comprador.AnonimizarDadosSensiveis(hashAnonimo);

        await _compradorRepository.AtualizarAsync(comprador, cancellationToken);
        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                usuarioExecutor,
                "ANONIMIZACAO_DIREITO_ESQUECIMENTO_LGPD",
                nameof(Comprador),
                comprador.Id.ToString(),
                "Dados pessoais e sensíveis anonimizados permanentemente conforme Art. 18 da LGPD."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MapToMascarado(comprador);
    }

    public async Task<InspecaoCriptografiaCompradorResponse> InspecionarCriptografiaEmRepousoAsync(
        Guid id,
        string usuarioExecutor,
        CancellationToken cancellationToken = default)
    {
        var comprador = await _compradorRepository.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Comprador com ID {id} não encontrado.");

        await _auditLogRepository.RegistrarAsync(
            new AuditLog(
                usuarioExecutor,
                "INSPECAO_CRIPTOGRAFIA_LGPD",
                nameof(Comprador),
                comprador.Id.ToString(),
                $"Inspeção técnica de criptografia em repouso (AES-256-GCM + HMAC-SHA256) realizada para o comprador {comprador.Id}."),
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return new InspecaoCriptografiaCompradorResponse(
            comprador.Id,
            "AES-256-GCM (Nonce 96-bit + Tag 128-bit)",
            "HMAC-SHA256 (256-bit Blind Index)",
            comprador.Anonimizado ? "***.***.***-**" : comprador.Cpf.Mascarado(),
            _cryptoService.Criptografar(comprador.Cpf.Numero),
            comprador.CpfHash,
            _cryptoService.Criptografar(comprador.Email.Endereco),
            comprador.EmailHash,
            _cryptoService.Criptografar($"{comprador.Rg.NumeroRg}|{comprador.Rg.OrgaoEmissor}|{comprador.Rg.UfEmissor}"),
            _cryptoService.Criptografar($"{comprador.Cnh.NumeroRegistro}|{comprador.Cnh.Categoria}|{comprador.Cnh.DataValidade:O}"),
            _cryptoService.Criptografar(comprador.Endereco.FormatadoCompleto()));
    }

    internal static CompradorMascaradoResponse MapToMascarado(Comprador c) =>
        new(
            c.Id,
            c.NomeCompleto,
            c.Anonimizado ? "***@anonimizado.lgpd" : c.Email.Mascarado(),
            c.TelefoneMascarado(),
            c.Anonimizado ? "***.***.***-**" : c.Cpf.Mascarado(),
            c.Anonimizado ? "***" : c.Rg.Mascarado(),
            c.Anonimizado ? "***" : c.Cnh.Mascarado(),
            c.Anonimizado ? "*** ANONIMIZADO LGPD ***" : c.Endereco.Mascarado(),
            c.ConsentimentoLgpd,
            c.DataConsentimentoLgpd,
            c.Anonimizado,
            c.Ativo,
            c.CriadoEm);
}
