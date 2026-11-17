namespace RevendaVeiculos.Domain.Exceptions;

public class RecursoNaoEncontradoException : DomainException
{
    public RecursoNaoEncontradoException(string message) : base(message)
    {
    }
}
