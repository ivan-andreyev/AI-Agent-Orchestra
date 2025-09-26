using MediatR;

namespace Orchestra.Core.Queries;

/// <summary>
/// Базовый интерфейс для запросов (операций чтения данных)
/// </summary>
/// <typeparam name="TResult">Тип возвращаемого результата</typeparam>
public interface IQuery<TResult> : IRequest<TResult>
{
}