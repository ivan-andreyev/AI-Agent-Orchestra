using System;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services.Metrics;

/// <summary>
/// Базовый класс для сбора метрик с обработкой ошибок.
/// Обеспечивает безопасную регистрацию инструментов метрик и их использование.
/// </summary>
/// <remarks>
/// Наследники этого класса должны инициализировать конкретные метрики в конструкторе
/// и предоставлять методы для записи значений метрик.
/// Все операции потокобезопасны и не генерируют исключения при ошибках регистрации.
/// </remarks>
public abstract class MetricsProvider
{
    /// <summary>
    /// Фабрика для создания измерителей метрик.
    /// </summary>
    protected readonly IMeterFactory MeterFactory;

    /// <summary>
    /// Измеритель метрик для данного сервиса.
    /// </summary>
    protected readonly Meter Meter;

    /// <summary>
    /// Логгер для записи ошибок и предупреждений.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Версия инструментации метрик.
    /// </summary>
    protected string InstrumentationVersion { get; }

    /// <summary>
    /// Инициализирует новый экземпляр провайдера метрик.
    /// </summary>
    /// <param name="logger">Логгер для записи диагностической информации.</param>
    /// <param name="meterFactory">Фабрика для создания измерителей метрик.</param>
    /// <param name="serviceName">Имя сервиса для идентификации источника метрик.</param>
    /// <param name="version">Версия инструментации (по умолчанию "1.0.0").</param>
    /// <exception cref="ArgumentNullException">Если logger или meterFactory равны null.</exception>
    /// <exception cref="ArgumentException">Если serviceName пустой или null.</exception>
    protected MetricsProvider(
        ILogger logger,
        IMeterFactory meterFactory,
        string serviceName,
        string version = "1.0.0")
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        MeterFactory = meterFactory ?? throw new ArgumentNullException(nameof(meterFactory));

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Имя сервиса не может быть пустым", nameof(serviceName));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Версия не может быть пустой", nameof(version));
        }

        InstrumentationVersion = version;
        Meter = MeterFactory.Create(serviceName, version);

        Logger.LogInformation(
            "Инициализирован провайдер метрик для сервиса {ServiceName} версии {Version}",
            serviceName,
            version);
    }

    /// <summary>
    /// Создает счетчик для подсчета событий.
    /// </summary>
    /// <param name="name">Имя метрики (должно быть уникальным в рамках измерителя).</param>
    /// <param name="description">Описание метрики для документации.</param>
    /// <param name="unit">Единица измерения (по умолчанию "1" - безразмерная величина).</param>
    /// <returns>Счетчик или null в случае ошибки создания.</returns>
    /// <remarks>
    /// Метод безопасен и не генерирует исключения. В случае ошибки возвращает null
    /// и записывает предупреждение в лог. Вызывающий код должен проверять результат на null.
    /// </remarks>
    protected Counter<long>? CreateCounter(string name, string description, string unit = "1")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Logger.LogWarning("Попытка создать счетчик с пустым именем");
            return null;
        }

        try
        {
            var counter = Meter.CreateCounter<long>(name, unit, description);
            Logger.LogDebug(
                "Создан счетчик {CounterName} с единицей измерения {Unit}",
                name,
                unit);
            return counter;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Ошибка при создании счетчика {CounterName}: {ErrorMessage}",
                name,
                ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Создает наблюдаемый gauge для текущих значений.
    /// </summary>
    /// <param name="name">Имя метрики (должно быть уникальным в рамках измерителя).</param>
    /// <param name="description">Описание метрики для документации.</param>
    /// <param name="observeValue">Функция, возвращающая текущее значение метрики.</param>
    /// <param name="unit">Единица измерения (по умолчанию "1" - безразмерная величина).</param>
    /// <returns>Наблюдаемый gauge или null в случае ошибки создания.</returns>
    /// <remarks>
    /// Метод безопасен и не генерирует исключения. В случае ошибки возвращает null
    /// и записывает предупреждение в лог. Функция observeValue будет вызываться
    /// периодически для получения текущего значения метрики.
    /// </remarks>
    protected ObservableGauge<long>? CreateObservableGauge(
        string name,
        string description,
        Func<long> observeValue,
        string unit = "1")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Logger.LogWarning("Попытка создать gauge с пустым именем");
            return null;
        }

        if (observeValue == null)
        {
            Logger.LogWarning("Попытка создать gauge {GaugeName} с null функцией наблюдения", name);
            return null;
        }

        try
        {
            var gauge = Meter.CreateObservableGauge(name, observeValue, unit, description);
            Logger.LogDebug(
                "Создан observable gauge {GaugeName} с единицей измерения {Unit}",
                name,
                unit);
            return gauge;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Ошибка при создании gauge {GaugeName}: {ErrorMessage}",
                name,
                ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Создает гистограмму для распределения значений.
    /// </summary>
    /// <param name="name">Имя метрики (должно быть уникальным в рамках измерителя).</param>
    /// <param name="description">Описание метрики для документации.</param>
    /// <param name="unit">Единица измерения (по умолчанию "ms" - миллисекунды).</param>
    /// <returns>Гистограмма или null в случае ошибки создания.</returns>
    /// <remarks>
    /// Метод безопасен и не генерирует исключения. В случае ошибки возвращает null
    /// и записывает предупреждение в лог. Гистограммы обычно используются для
    /// измерения длительности операций и размеров данных.
    /// </remarks>
    protected Histogram<double>? CreateHistogram(string name, string description, string unit = "ms")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Logger.LogWarning("Попытка создать гистограмму с пустым именем");
            return null;
        }

        try
        {
            var histogram = Meter.CreateHistogram<double>(name, unit, description);
            Logger.LogDebug(
                "Создана гистограмма {HistogramName} с единицей измерения {Unit}",
                name,
                unit);
            return histogram;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Ошибка при создании гистограммы {HistogramName}: {ErrorMessage}",
                name,
                ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Безопасно записывает значение в счетчик с проверкой на null.
    /// </summary>
    /// <param name="counter">Счетчик для записи значения.</param>
    /// <param name="value">Значение для добавления к счетчику.</param>
    /// <param name="tags">Опциональные теги для классификации метрики.</param>
    /// <remarks>
    /// Если counter равен null, метод ничего не делает и не генерирует исключения.
    /// Это позволяет безопасно использовать метод даже если создание счетчика не удалось.
    /// </remarks>
    protected void SafeAdd(Counter<long>? counter, long value, params KeyValuePair<string, object?>[] tags)
    {
        if (counter == null)
        {
            return;
        }

        try
        {
            counter.Add(value, tags);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Ошибка при записи значения в счетчик: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Безопасно записывает значение в гистограмму с проверкой на null.
    /// </summary>
    /// <param name="histogram">Гистограмма для записи значения.</param>
    /// <param name="value">Значение для записи в гистограмму.</param>
    /// <param name="tags">Опциональные теги для классификации метрики.</param>
    /// <remarks>
    /// Если histogram равен null, метод ничего не делает и не генерирует исключения.
    /// Это позволяет безопасно использовать метод даже если создание гистограммы не удалось.
    /// </remarks>
    protected void SafeRecord(Histogram<double>? histogram, double value, params KeyValuePair<string, object?>[] tags)
    {
        if (histogram == null)
        {
            return;
        }

        try
        {
            histogram.Record(value, tags);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Ошибка при записи значения в гистограмму: {ErrorMessage}", ex.Message);
        }
    }
}
