using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// Кодни листи - шифри што се употребуваат при пополнување на декларација
/// Извор: ПРАВИЛНИК - Кодекс на шифри
/// </summary>
public class CodeListItem : BaseEntity
{
    /// <summary>
    /// Тип на листа: ProcedureCode, DocumentType, CountryCode, TransportMode, PackageType, ...
    /// </summary>
    public string ListType { get; set; } = string.Empty;
    
    /// <summary>
    /// Код (нпр. "42 00", "N380", "MK", "10")
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Опис на македонски
    /// </summary>
    public string DescriptionMK { get; set; } = string.Empty;
    
    /// <summary>
    /// Опис на англиски
    /// </summary>
    public string? DescriptionEN { get; set; }
    
    /// <summary>
    /// Број на рубрика (Box број) - за контекстуално мапирање
    /// Пример: "1", "15а", "37", "44"
    /// </summary>
    public string? BoxNumber { get; set; }
    
    /// <summary>
    /// Tooltip текст (контекстуален опис за UI)
    /// </summary>
    public string? Tooltip { get; set; }
    
    /// <summary>
    /// Родителски код (за хиерархиски листи)
    /// </summary>
    public string? ParentCode { get; set; }
    
    /// <summary>
    /// Дополнителни податоци (JSON)
    /// Пример: { "requiresAuthorization": true, "validForLON": true }
    /// </summary>
    public string? AdditionalData { get; set; }
    
    /// <summary>
    /// Сортирање
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// Дали е активен
    /// </summary>
    public bool IsActive { get; set; } = true;
}
