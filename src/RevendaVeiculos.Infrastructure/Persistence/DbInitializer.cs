using RevendaVeiculos.Application.Interfaces;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Enums;
using RevendaVeiculos.Domain.ValueObjects;

namespace RevendaVeiculos.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(RevendaDbContext context, ICryptoService cryptoService)
    {
        await context.Database.EnsureCreatedAsync();

        if (!context.Veiculos.Any())
        {
            var v1 = new Veiculo(
                "Volkswagen",
                "Polo Track 1.0",
                2024,
                2025,
                "Branco Cristal",
                84990.00m,
                new Placa("BRA2E19"),
                new Chassi("9BWAG5BZ0RP000101"),
                new Renavam("12345678901"),
                0,
                TipoCombustivel.Flex,
                TipoCambio.Manual,
                "Veículo zero km, completo com direção elétrica e ar-condicionado.");

            var v2 = new Veiculo(
                "Toyota",
                "Corolla XEi 2.0",
                2023,
                2024,
                "Prata Supernova",
                148500.00m,
                new Placa("FAP5A25"),
                new Chassi("9BRBD3HE0P0000202"),
                new Renavam("23456789012"),
                18500,
                TipoCombustivel.Flex,
                TipoCambio.CVT,
                "Único dono, revisões em concessionária, pacote Toyota Safety Sense.");

            var v3 = new Veiculo(
                "Fiat",
                "Mobi Like 1.0",
                2022,
                2023,
                "Preto Vulcano",
                58900.00m,
                new Placa("ABC1D23"),
                new Chassi("9BD195A4ZNP000303"),
                new Renavam("34567890123"),
                32000,
                TipoCombustivel.Flex,
                TipoCambio.Manual,
                "Econômico, laudo cautelar 100% aprovado.");

            var v4 = new Veiculo(
                "BYD",
                "Dolphin GS Elétrico",
                2024,
                2025,
                "Cinza Dolphin",
                149800.00m,
                new Placa("ELC9B88"),
                new Chassi("LGXC16DF0R0000404"),
                new Renavam("45678901234"),
                4200,
                TipoCombustivel.Eletrico,
                TipoCambio.Automatico,
                "100% elétrico, bateria Blade, carregador wallbox incluso.");

            await context.Veiculos.AddRangeAsync(v1, v2, v3, v4);
        }

        if (!context.Compradores.Any())
        {
            var cpf1 = new Cpf("52998224725");
            var email1 = new Email("gabriel.nascimento@cliente.com.br");
            var c1 = new Comprador(
                "Gabriel Nascimento",
                email1,
                cryptoService.GerarHashDeterministico(email1.Endereco),
                "11987654321",
                new DateTime(1998, 5, 15),
                cpf1,
                cryptoService.GerarHashDeterministico(cpf1.Numero),
                new Endereco("01310100", "Avenida Paulista", "1106", "Andar 7", "Bela Vista", "São Paulo", "SP"),
                new DocumentoIdentidade("456789123", "SSP", "SP"),
                new CarteiraHabilitacao("12345678901", "AB", DateTime.UtcNow.AddYears(4)),
                true);

            var cpf2 = new Cpf("11144477735");
            var email2 = new Email("mariana.silva@cliente.com.br");
            var c2 = new Comprador(
                "Mariana Silva Costa",
                email2,
                cryptoService.GerarHashDeterministico(email2.Endereco),
                "11912345678",
                new DateTime(1994, 8, 22),
                cpf2,
                cryptoService.GerarHashDeterministico(cpf2.Numero),
                new Endereco("04538133", "Avenida Brigadeiro Faria Lima", "3477", "Conjunto 42", "Itaim Bibi", "São Paulo", "SP"),
                new DocumentoIdentidade("321654987", "SSP", "SP"),
                new CarteiraHabilitacao("98765432109", "B", DateTime.UtcNow.AddYears(3)),
                true);

            await context.Compradores.AddRangeAsync(c1, c2);
        }

        await context.SaveChangesAsync();
    }
}
