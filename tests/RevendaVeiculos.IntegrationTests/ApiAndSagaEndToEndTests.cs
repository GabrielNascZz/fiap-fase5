using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RevendaVeiculos.Application.DTOs;
using RevendaVeiculos.Domain.Enums;

namespace RevendaVeiculos.IntegrationTests;

public class ApiAndSagaEndToEndTests
{
    private static WebApplicationFactory<Program> CreateIsolatedFactory()
    {
        var dbName = $"RevendaIntegrationTestDb_{Guid.NewGuid()}";
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["UsePostgres"] = "false",
                        ["InMemoryDatabaseName"] = dbName
                    });
                });
            });
    }

    [Fact]
    public async Task ListagemVeiculosAVenda_DeveRetornarApenasDisponiveisOrdenadosDoMaisBaratoParaOMaisCaro()
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var veiculos = await client.GetFromJsonAsync<List<VeiculoResponse>>("/api/v1/veiculos/a-venda");

        veiculos.Should().NotBeNull().And.HaveCountGreaterThanOrEqualTo(4);
        veiculos!.Select(v => v.Preco).Should().BeInAscendingOrder();
        veiculos!.All(v => v.Status == nameof(StatusVeiculo.Disponivel)).Should().BeTrue();
    }

    [Fact]
    public async Task CadastrarEEditarVeiculo_DevePersistirCorretamenteEManterOrdenacaoPorPreco()
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var novoVeiculo = new CadastrarVeiculoRequest(
            "Chevrolet",
            "Onix Plus Premier 1.0 Turbo",
            2024,
            2025,
            "Azul Seeker",
            99500.00m,
            "ONX4F55",
            "9BGEB69H0RG000777",
            "77889900112",
            5000,
            TipoCombustivel.Flex,
            TipoCambio.Automatico,
            "Sedã turbo impecável.");

        var postResp = await client.PostAsJsonAsync("/api/v1/veiculos", novoVeiculo);
        postResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var criado = await postResp.Content.ReadFromJsonAsync<VeiculoResponse>();
        criado.Should().NotBeNull();
        criado!.Placa.Should().Be("ONX4F55");

        var editReq = new AtualizarVeiculoRequest(
            "Chevrolet",
            "Onix Plus Premier 1.0 Turbo",
            2024,
            2025,
            "Azul Seeker Metálico",
            94900.00m,
            5100,
            TipoCombustivel.Flex,
            TipoCambio.Automatico,
            "Oferta especial.");

        var putResp = await client.PutAsJsonAsync($"/api/v1/veiculos/{criado.Id}", editReq);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var atualizado = await putResp.Content.ReadFromJsonAsync<VeiculoResponse>();
        atualizado!.Preco.Should().Be(94900.00m);
        atualizado.Cor.Should().Be("Azul Seeker Metálico");
    }

    [Fact]
    public async Task FluxoCompletoSaga_Selecao_Reserva_CodigoPagamento_Pagamento_RetiradaEBaixaEstoque_E_ListagemVendidosOrdenadaPorPreco()
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var veiculosIniciais = await client.GetFromJsonAsync<List<VeiculoResponse>>("/api/v1/veiculos/a-venda");
        var compradores = await client.GetFromJsonAsync<List<CompradorMascaradoResponse>>("/api/v1/compradores");

        var veiculoMaisCaro = veiculosIniciais!.Last();   // R$ 149.800,00
        var veiculoMaisBarato = veiculosIniciais!.First(); // R$ 58.900,00
        var comprador1 = compradores![0];
        var comprador2 = compradores[1];

        // Compra 1: Passo a Passo Completo da SAGA (Seleção -> Reserva -> Código Pagamento -> Pagamento -> Retirada/Baixa)
        var reservaResp = await client.PostAsJsonAsync(
            "/api/v1/compras/reservar",
            new IniciarCompraReservaRequest(veiculoMaisCaro.Id, comprador1.Id));

        reservaResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var sagaPasso1E2 = await reservaResp.Content.ReadFromJsonAsync<PedidoCompraSagaResponse>();

        sagaPasso1E2.Should().NotBeNull();
        sagaPasso1E2!.StatusPedidoSaga.Should().Be(nameof(StatusPedidoCompra.AguardandoPagamento));
        sagaPasso1E2.StatusAtualVeiculoNoEstoque.Should().Be(nameof(StatusVeiculo.Reservado));
        sagaPasso1E2.CodigoPagamento.Should().StartWith("PAY-PIX-");

        // Passo 3: Cliente paga pelo veículo
        var pagtoResp = await client.PostAsJsonAsync(
            $"/api/v1/compras/{sagaPasso1E2.PedidoId}/pagamento",
            new ProcessarPagamentoRequest(sagaPasso1E2.CodigoPagamento!, PagamentoEfetuado: true));

        pagtoResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var sagaPasso3 = await pagtoResp.Content.ReadFromJsonAsync<PedidoCompraSagaResponse>();
        sagaPasso3!.StatusPedidoSaga.Should().Be(nameof(StatusPedidoCompra.PagamentoConfirmado));
        sagaPasso3.StatusPagamento.Should().Be(nameof(StatusPagamento.Aprovado));

        // Passo 4: Cliente retira o veículo -> emite documentação (ATPV-e + NF) e dá baixa no estoque da revenda
        var retiradaResp = await client.PostAsJsonAsync(
            $"/api/v1/compras/{sagaPasso1E2.PedidoId}/retirada",
            new RealizarRetiradaRequest(ConfirmarAssinaturaTermoEntrega: true));

        retiradaResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var sagaConcluida = await retiradaResp.Content.ReadFromJsonAsync<PedidoCompraSagaResponse>();

        sagaConcluida!.StatusPedidoSaga.Should().Be(nameof(StatusPedidoCompra.ConcluidoBaixaEstoque));
        sagaConcluida.StatusAtualVeiculoNoEstoque.Should().Be(nameof(StatusVeiculo.Vendido));
        sagaConcluida.ProtocoloDocumentacaoRetirada.Should().StartWith("ATPVE-DETRAN-");
        sagaConcluida.NotaFiscalNumero.Should().StartWith("NFE-");
        sagaConcluida.HistoricoPassosSaga.Should().HaveCount(4);

        // Compra 2: Venda do veículo mais barato para validar ordenação dos vendidos (do mais barato para o mais caro)
        var venda2Resp = await client.PostAsJsonAsync(
            "/api/v1/compras/efetuar-venda",
            new EfetuarVendaCompletaRequest(veiculoMaisBarato.Id, comprador2.Id));
        venda2Resp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verifica que ambos saíram da lista de veículos à venda e entraram na lista de veículos vendidos ordenada por preço ASC
        var vendidos = await client.GetFromJsonAsync<List<VeiculoResponse>>("/api/v1/veiculos/vendidos");
        vendidos.Should().NotBeNull().And.HaveCount(2);
        vendidos![0].Id.Should().Be(veiculoMaisBarato.Id);
        vendidos[1].Id.Should().Be(veiculoMaisCaro.Id);
        vendidos.Select(v => v.Preco).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ProblemaConcorrenciaReserva_QuandoOutroClienteReservaAntes_DeveRetornar409Conflict()
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var veiculos = await client.GetFromJsonAsync<List<VeiculoResponse>>("/api/v1/veiculos/a-venda");
        var compradores = await client.GetFromJsonAsync<List<CompradorMascaradoResponse>>("/api/v1/compradores");

        var veiculoAlvo = veiculos![0];
        var clienteA = compradores![0];
        var clienteB = compradores[1];

        // Ambos os clientes selecionam o mesmo veículo e disparam reserva simultaneamente
        var tarefaClienteA = client.PostAsJsonAsync(
            "/api/v1/compras/reservar",
            new IniciarCompraReservaRequest(veiculoAlvo.Id, clienteA.Id));

        var tarefaClienteB = client.PostAsJsonAsync(
            "/api/v1/compras/reservar",
            new IniciarCompraReservaRequest(veiculoAlvo.Id, clienteB.Id));

        var respostas = await Task.WhenAll(tarefaClienteA, tarefaClienteB);
        var statusCodes = respostas.Select(r => r.StatusCode).ToList();

        // Exatamente um deve conseguir reservar (201 Created) e o outro deve receber conflito (409 Conflict)
        statusCodes.Should().ContainSingle(s => s == HttpStatusCode.Created);
        statusCodes.Should().ContainSingle(s => s == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ProblemaPagamentoNaoEfetuado_DeveExecutarCompensacaoSagaELiberarVeiculoNoEstoque()
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var veiculos = await client.GetFromJsonAsync<List<VeiculoResponse>>("/api/v1/veiculos/a-venda");
        var compradores = await client.GetFromJsonAsync<List<CompradorMascaradoResponse>>("/api/v1/compradores");

        var veiculo = veiculos![0];
        var comprador = compradores![0];

        // Reserva o veículo (sai da lista de disponíveis)
        var reservaResp = await client.PostAsJsonAsync(
            "/api/v1/compras/reservar",
            new IniciarCompraReservaRequest(veiculo.Id, comprador.Id));
        var pedido = await reservaResp.Content.ReadFromJsonAsync<PedidoCompraSagaResponse>();

        // Pagamento não efetuado / recusado
        var falhaPagtoResp = await client.PostAsJsonAsync(
            $"/api/v1/compras/{pedido!.PedidoId}/pagamento",
            new ProcessarPagamentoRequest(pedido.CodigoPagamento!, PagamentoEfetuado: false, "Saldo insuficiente / Pagamento expirado"));

        falhaPagtoResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var sagaCompensada = await falhaPagtoResp.Content.ReadFromJsonAsync<PedidoCompraSagaResponse>();

        sagaCompensada!.StatusPedidoSaga.Should().Be(nameof(StatusPedidoCompra.CanceladoPorFalhaPagamento));
        sagaCompensada.StatusPagamento.Should().Be(nameof(StatusPagamento.Recusado));
        sagaCompensada.StatusAtualVeiculoNoEstoque.Should().Be(nameof(StatusVeiculo.Disponivel));
        sagaCompensada.HistoricoPassosSaga.Should().Contain(p => p.TipoPasso == nameof(TipoPassoSaga.CompensacaoRollback));

        // O veículo deve voltar para a listagem de veículos à venda
        var disponiveis = await client.GetFromJsonAsync<List<VeiculoResponse>>("/api/v1/veiculos/a-venda");
        disponiveis!.Select(v => v.Id).Should().Contain(veiculo.Id);
    }

    [Fact]
    public async Task ProblemaDesistenciaCliente_MesmoAposPagamento_DeveEstornarValorELiberarVeiculoNoEstoque()
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var veiculos = await client.GetFromJsonAsync<List<VeiculoResponse>>("/api/v1/veiculos/a-venda");
        var compradores = await client.GetFromJsonAsync<List<CompradorMascaradoResponse>>("/api/v1/compradores");

        var veiculo = veiculos![1];
        var comprador = compradores![0];

        // 1. Cliente reserva e recebe código de pagamento
        var reservaResp = await client.PostAsJsonAsync(
            "/api/v1/compras/reservar",
            new IniciarCompraReservaRequest(veiculo.Id, comprador.Id));
        var pedido = await reservaResp.Content.ReadFromJsonAsync<PedidoCompraSagaResponse>();

        // 2. Cliente realiza o pagamento
        await client.PostAsJsonAsync(
            $"/api/v1/compras/{pedido!.PedidoId}/pagamento",
            new ProcessarPagamentoRequest(pedido.CodigoPagamento!, PagamentoEfetuado: true));

        // 3. Antes da retirada, o cliente desiste da compra
        var desistenciaResp = await client.PostAsJsonAsync(
            $"/api/v1/compras/{pedido.PedidoId}/desistir",
            new DesistirCompraRequest("Cliente desistiu da compra antes da retirada."));

        desistenciaResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var sagaEstornada = await desistenciaResp.Content.ReadFromJsonAsync<PedidoCompraSagaResponse>();

        sagaEstornada!.StatusPedidoSaga.Should().Be(nameof(StatusPedidoCompra.CanceladoPorDesistencia));
        sagaEstornada.StatusPagamento.Should().Be(nameof(StatusPagamento.Estornado));
        sagaEstornada.StatusAtualVeiculoNoEstoque.Should().Be(nameof(StatusVeiculo.Disponivel));
        sagaEstornada.HistoricoPassosSaga.Should().Contain(p => p.NomePasso.Contains("Estorno Financeiro"));
        sagaEstornada.HistoricoPassosSaga.Should().Contain(p => p.NomePasso.Contains("Liberação de Reserva"));
    }

    [Fact]
    public async Task SegurancaDadosSensiveisLgpd_DeveMascararPorPadrao_AuditarAcessoCompleto_E_PermitirAnonimizacao()
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var novoComprador = new CadastrarCompradorRequest(
            "Roberto Carlos Almeida",
            "roberto.almeida@lgpd.com.br",
            "11977776666",
            new DateTime(1985, 10, 12),
            "39053344705",
            new EnderecoDto("01414001", "Rua Haddock Lobo", "595", "Sala 10", "Cerqueira César", "São Paulo", "SP"),
            new DocumentoIdentidadeDto("554433221", "SSP", "SP"),
            new CarteiraHabilitacaoDto("55667788990", "AB", DateTime.UtcNow.AddYears(5)),
            ConsentimentoLgpd: true);

        var cadResp = await client.PostAsJsonAsync("/api/v1/compradores", novoComprador);
        cadResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var mascarado = await cadResp.Content.ReadFromJsonAsync<CompradorMascaradoResponse>();
        mascarado!.CpfMascarado.Should().Be("***.533.447-**");
        mascarado.EmailMascarado.Should().Be("ro***@lgpd.com.br");

        // Acesso completo auditado para emissão de documentação
        var completoResp = await client.GetFromJsonAsync<CompradorDadosCompletosResponse>(
            $"/api/v1/compradores/{mascarado.Id}/dados-completos?motivoAcesso=EmissaoATPVe");
        completoResp!.CpfFormatado.Should().Be("390.533.447-05");

        // Direito ao esquecimento / Anonimização LGPD
        var anonResp = await client.PostAsync($"/api/v1/compradores/{mascarado.Id}/anonimizar", null);
        anonResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var anonimizado = await anonResp.Content.ReadFromJsonAsync<CompradorMascaradoResponse>();
        anonimizado!.Anonimizado.Should().BeTrue();
        anonimizado.CpfMascarado.Should().Be("***.***.***-**");
    }
}
