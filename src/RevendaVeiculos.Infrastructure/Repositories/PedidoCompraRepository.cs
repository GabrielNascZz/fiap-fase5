using Microsoft.EntityFrameworkCore;
using RevendaVeiculos.Domain.Entities;
using RevendaVeiculos.Domain.Enums;
using RevendaVeiculos.Domain.Interfaces;
using RevendaVeiculos.Infrastructure.Persistence;

namespace RevendaVeiculos.Infrastructure.Repositories;

public class PedidoCompraRepository : IPedidoCompraRepository
{
    private readonly RevendaDbContext _context;

    public PedidoCompraRepository(RevendaDbContext context)
    {
        _context = context;
    }

    public async Task<PedidoCompra?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PedidosCompra
            .Include(p => p.PassosSaga)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PedidoCompra?> ObterPorCodigoPagamentoAsync(string codigoPagamento, CancellationToken cancellationToken = default)
    {
        return await _context.PedidosCompra
            .Include(p => p.PassosSaga)
            .FirstOrDefaultAsync(p => p.CodigoPagamento == codigoPagamento, cancellationToken);
    }

    public async Task<PedidoCompra?> ObterPedidoAtivoPorVeiculoIdAsync(Guid veiculoId, CancellationToken cancellationToken = default)
    {
        return await _context.PedidosCompra
            .Include(p => p.PassosSaga)
            .FirstOrDefaultAsync(
                p => p.VeiculoId == veiculoId &&
                     (p.StatusPedido == StatusPedidoCompra.VeiculoReservado ||
                      p.StatusPedido == StatusPedidoCompra.AguardandoPagamento ||
                      p.StatusPedido == StatusPedidoCompra.PagamentoConfirmado),
                cancellationToken);
    }

    public async Task<IReadOnlyList<PedidoCompra>> ListarTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PedidosCompra
            .Include(p => p.PassosSaga)
            .AsNoTracking()
            .OrderByDescending(p => p.CriadoEm)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PedidoCompra>> ListarReservasExpiradasAsync(DateTime agoraUtc, CancellationToken cancellationToken = default)
    {
        return await _context.PedidosCompra
            .Include(p => p.PassosSaga)
            .Where(p =>
                (p.StatusPedido == StatusPedidoCompra.VeiculoReservado ||
                 p.StatusPedido == StatusPedidoCompra.AguardandoPagamento) &&
                p.DataExpiracaoReserva <= agoraUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AdicionarAsync(PedidoCompra pedido, CancellationToken cancellationToken = default)
    {
        await _context.PedidosCompra.AddAsync(pedido, cancellationToken);
    }

    public async Task AtualizarAsync(PedidoCompra pedido, CancellationToken cancellationToken = default)
    {
        var entryPedido = _context.Entry(pedido);
        if (entryPedido.State == EntityState.Detached)
        {
            _context.PedidosCompra.Attach(pedido);
            entryPedido.State = EntityState.Modified;
        }

        foreach (var passo in pedido.PassosSaga)
        {
            var existeNoBanco = await _context.SagaSteps
                .AsNoTracking()
                .AnyAsync(s => s.Id == passo.Id, cancellationToken);

            if (!existeNoBanco)
            {
                _context.Entry(passo).State = EntityState.Added;
            }
        }
    }
}
