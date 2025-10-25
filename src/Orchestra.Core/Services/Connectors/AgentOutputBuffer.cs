using System.Text.RegularExpressions;

namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Потокобезопасная реализация буфера для вывода агента
/// </summary>
/// <remarks>
/// <para>
/// AgentOutputBuffer использует циркулярный буфер для хранения последних N строк вывода.
/// Когда буфер достигает максимального размера, старые строки автоматически удаляются.
/// </para>
/// <para>
/// Все операции потокобезопасны благодаря использованию SemaphoreSlim.
/// </para>
/// </remarks>
public class AgentOutputBuffer : IAgentOutputBuffer, IDisposable
{
    private readonly CircularBuffer<OutputLine> _buffer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly int _maxLines;
    private bool _disposed;

    /// <summary>
    /// Инициализирует новый экземпляр класса AgentOutputBuffer
    /// </summary>
    /// <param name="maxLines">Максимальное количество строк в буфере (по умолчанию 10000)</param>
    /// <exception cref="ArgumentOutOfRangeException">Выбрасывается если maxLines меньше или равно 0</exception>
    public AgentOutputBuffer(int maxLines = 10000)
    {
        if (maxLines <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLines), "maxLines must be greater than 0");
        }

        _maxLines = maxLines;
        _buffer = new CircularBuffer<OutputLine>(maxLines);
    }

    /// <inheritdoc />
    public event EventHandler<OutputLineAddedEventArgs>? LineAdded;

    /// <inheritdoc />
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _lock.Wait();
            try
            {
                return _buffer.Count;
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <inheritdoc />
    public async Task AppendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (line == null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var timestamp = DateTime.UtcNow;
            var outputLine = new OutputLine(line, timestamp);
            _buffer.Add(outputLine);

            // NOTE: Event firing outside lock to prevent potential deadlocks
            OnLineAdded(new OutputLineAddedEventArgs { Line = line, Timestamp = timestamp });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetLastLinesAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than 0");
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var lines = _buffer
                .TakeLast(count)
                .Select(line => line.Content)
                .ToList();

            return lines;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetLinesAsync(
        string? regexFilter = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Regex? regex = null;
        if (!string.IsNullOrEmpty(regexFilter))
        {
            try
            {
                regex = new Regex(regexFilter, RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regex pattern: {regexFilter}", nameof(regexFilter), ex);
            }
        }

        await _lock.WaitAsync(cancellationToken);
        List<string> snapshot;
        try
        {
            snapshot = _buffer
                .Select(line => line.Content)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }

        foreach (var line in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (regex == null || regex.IsMatch(line))
            {
                yield return line;
            }
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _buffer.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Освобождает ресурсы, используемые AgentOutputBuffer
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Освобождает управляемые и неуправляемые ресурсы
    /// </summary>
    /// <param name="disposing">True если вызывается из Dispose(), false если из финализатора</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _lock?.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Вызывает событие LineAdded
    /// </summary>
    /// <param name="e">Аргументы события</param>
    protected virtual void OnLineAdded(OutputLineAddedEventArgs e)
    {
        LineAdded?.Invoke(this, e);
    }
}

/// <summary>
/// Циркулярный буфер с фиксированной емкостью
/// </summary>
/// <typeparam name="T">Тип элементов в буфере</typeparam>
/// <remarks>
/// <para>
/// При достижении максимальной емкости новые элементы заменяют самые старые.
/// </para>
/// <para>
/// NOTE: Этот класс не является потокобезопасным. Потокобезопасность обеспечивается
/// на уровне AgentOutputBuffer через SemaphoreSlim.
/// </para>
/// </remarks>
internal class CircularBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private readonly int _capacity;
    private int _start;
    private int _count;

    /// <summary>
    /// Инициализирует новый экземпляр класса CircularBuffer
    /// </summary>
    /// <param name="capacity">Максимальная емкость буфера</param>
    /// <exception cref="ArgumentOutOfRangeException">Выбрасывается если capacity меньше или равно 0</exception>
    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be greater than 0");
        }

        _capacity = capacity;
        _buffer = new T[capacity];
        _start = 0;
        _count = 0;
    }

    /// <summary>
    /// Получает количество элементов в буфере
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Получает емкость буфера
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Добавляет элемент в буфер
    /// </summary>
    /// <param name="item">Элемент для добавления</param>
    /// <remarks>
    /// Если буфер полон, самый старый элемент будет заменен новым.
    /// </remarks>
    public void Add(T item)
    {
        var index = (_start + _count) % _capacity;
        _buffer[index] = item;

        if (_count < _capacity)
        {
            _count++;
        }
        else
        {
            _start = (_start + 1) % _capacity;
        }
    }

    /// <summary>
    /// Очищает буфер
    /// </summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _capacity);
        _start = 0;
        _count = 0;
    }

    /// <summary>
    /// Получает элемент по индексу
    /// </summary>
    /// <param name="index">Индекс элемента (0 = самый старый элемент)</param>
    /// <returns>Элемент на указанной позиции</returns>
    /// <exception cref="ArgumentOutOfRangeException">Выбрасывается если индекс вне допустимого диапазона</exception>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_count - 1}");
            }

            var actualIndex = (_start + index) % _capacity;
            return _buffer[actualIndex];
        }
    }

    /// <summary>
    /// Возвращает перечислитель для буфера
    /// </summary>
    /// <returns>Перечислитель элементов от самого старого к самому новому</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    /// <summary>
    /// Возвращает нетипизированный перечислитель для буфера
    /// </summary>
    /// <returns>Нетипизированный перечислитель</returns>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
