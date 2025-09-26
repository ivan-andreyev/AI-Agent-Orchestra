using Orchestra.Core.Models.Workflow;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Интерфейс для парсинга markdown workflow документов
    /// </summary>
    public interface IMarkdownWorkflowParser
    {
        /// <summary>
        /// Парсинг markdown содержимого в MarkdownWorkflow объект
        /// </summary>
        /// <param name="markdownContent">Содержимое markdown файла</param>
        /// <param name="filePath">Путь к файлу (опционально)</param>
        /// <returns>Результат парсинга</returns>
        Task<MarkdownWorkflowParseResult> ParseAsync(string markdownContent, string? filePath = null);

        /// <summary>
        /// Проверка валидности markdown workflow документа
        /// </summary>
        /// <param name="markdownContent">Содержимое markdown файла</param>
        /// <returns>True если документ является валидным workflow</returns>
        Task<bool> IsValidWorkflowAsync(string markdownContent);
    }
}