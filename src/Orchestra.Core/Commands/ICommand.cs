using MediatR;

namespace Orchestra.Core.Commands;

/// <summary>
/// Базовый интерфейс для команд (операций изменения состояния)
/// </summary>
public interface ICommand : IRequest
{
}

/// <summary>
/// Базовый интерфейс для команд с возвращаемым результатом
/// </summary>
/// <typeparam name="TResult">Тип возвращаемого результата</typeparam>
public interface ICommand<TResult> : IRequest<TResult>
{
}