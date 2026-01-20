namespace backend.Application.Common.Interfaces;

/// <summary>
/// Marker interface for commands (write operations).
/// </summary>
public interface ICommand { }

/// <summary>
/// Command that returns a result.
/// </summary>
/// <typeparam name="TResult">Result type</typeparam>
public interface ICommand<TResult> : ICommand { }

/// <summary>
/// Handler for commands without return value.
/// </summary>
/// <typeparam name="TCommand">Command type</typeparam>
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for commands with return value.
/// </summary>
/// <typeparam name="TCommand">Command type</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for queries (read operations).
/// </summary>
/// <typeparam name="TResult">Query result type</typeparam>
public interface IQuery<TResult> { }

/// <summary>
/// Handler for queries.
/// </summary>
/// <typeparam name="TQuery">Query type</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
