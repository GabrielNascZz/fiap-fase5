namespace RevendaVeiculos.Domain.Exceptions;

public class ConflitoConcorrenciaException : DomainException
{
    public ConflitoConcorrenciaException(string message) : base(message)
    {
    }
}
