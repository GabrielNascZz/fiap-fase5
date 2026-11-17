namespace RevendaVeiculos.Domain.Enums;

public enum StatusPedidoCompra
{
    Iniciado = 1,
    VeiculoReservado = 2,
    AguardandoPagamento = 3,
    PagamentoConfirmado = 4,
    ConcluidoBaixaEstoque = 5,
    CanceladoPorDesistencia = 6,
    CanceladoPorFalhaPagamento = 7,
    CanceladoPorExpiracaoReserva = 8,
    FalhaConcorrencia = 9
}
