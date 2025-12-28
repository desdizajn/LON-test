using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// TARIC тарифна ознака - основна класификација на стоки
/// Извор: TARIC.xlsx (10,307 записи)
/// </summary>
public class TariffCode : BaseEntity
{
    /// <summary>
    /// 10-цифрена тарифна ознака (нпр. 0101210000)
    /// </summary>
    public string TariffNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Прва група (4 цифри) - TARBR
    /// </summary>
    public string TARBR { get; set; } = string.Empty;
    
    /// <summary>
    /// Втора група - TAROZ1
    /// </summary>
    public string TAROZ1 { get; set; } = string.Empty;
    
    /// <summary>
    /// Трета група - TAROZ2
    /// </summary>
    public string TAROZ2 { get; set; } = string.Empty;
    
    /// <summary>
    /// Четврта група - TAROZ3
    /// </summary>
    public string TAROZ3 { get; set; } = string.Empty;
    
    /// <summary>
    /// Опис на стоката (EN)
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Царинска стапка (%)
    /// </summary>
    public decimal? CustomsRate { get; set; }
    
    /// <summary>
    /// Мерна единица (kg, l, m3, ...)
    /// </summary>
    public string? UnitMeasure { get; set; }
    
    /// <summary>
    /// ДДВ стапка (%)
    /// </summary>
    public decimal? VATRate { get; set; }
    
    /// <summary>
    /// FI - формулар/индикатор
    /// </summary>
    public string? FI { get; set; }
    
    /// <summary>
    /// FU - формулар/услов
    /// </summary>
    public string? FU { get; set; }
    
    /// <summary>
    /// PV - посебни услови
    /// </summary>
    public string? PV { get; set; }
    
    /// <summary>
    /// Ex - исклучок
    /// </summary>
    public string? Ex { get; set; }
    
    /// <summary>
    /// Дали е активен
    /// </summary>
    public bool IsActive { get; set; } = true;
}
