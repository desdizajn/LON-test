using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// Регулатива за специфично распоредување на стоки
/// Извор: Spisok na Regulativi KN 15.xlsx (1,809 записи)
/// </summary>
public class CustomsRegulation : BaseEntity
{
    /// <summary>
    /// CELEX број - европска референца (нпр. "CELEX бр 32013R0729")
    /// </summary>
    public string CelexNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Службен весник на РМ/РСМ број и датум
    /// </summary>
    public string? OfficialGazetteRef { get; set; }
    
    /// <summary>
    /// Тарифна ознака на која се однесува (нпр. "0307 99 80")
    /// </summary>
    public string TariffNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Опис на стоката (англиски)
    /// </summary>
    public string? DescriptionEN { get; set; }
    
    /// <summary>
    /// Опис на стоката (македонски)
    /// </summary>
    public string? DescriptionMK { get; set; }
    
    /// <summary>
    /// Правен основ на распоредувањето
    /// </summary>
    public string? LegalBasis { get; set; }
    
    /// <summary>
    /// Датум на влегување во сила
    /// </summary>
    public DateTime? EffectiveDate { get; set; }
    
    /// <summary>
    /// Датум на истекување (nullable)
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
    
    /// <summary>
    /// Дали е активна
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Врска кон TariffCode (опционална)
    /// </summary>
    public Guid? TariffCodeId { get; set; }
    public virtual TariffCode? TariffCode { get; set; }
}
