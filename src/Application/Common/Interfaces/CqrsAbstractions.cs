namespace Application.Common.Interfaces;

public interface ICommandHandler<TRequest>
{
    Task Handle(TRequest request, CancellationToken cancellationToken);
}

public interface ICommandHandler<TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IQueryHandler<TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
