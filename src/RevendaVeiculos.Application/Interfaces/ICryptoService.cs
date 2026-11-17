namespace RevendaVeiculos.Application.Interfaces;

public interface ICryptoService
{
    string Criptografar(string textoClaro);
    string Descriptografar(string textoCifrado);
    string GerarHashDeterministico(string valorNormalizado);
}
