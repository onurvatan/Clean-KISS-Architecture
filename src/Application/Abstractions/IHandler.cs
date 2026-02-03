namespace Application.Abstractions;

public interface IHandler<TRequest, TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken = default);
}

public interface IHandler<TRequest>
{
    Task<Result> Handle(TRequest request, CancellationToken cancellationToken = default);
}
