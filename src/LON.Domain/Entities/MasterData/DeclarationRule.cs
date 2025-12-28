using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// Правила за валидација на царинска декларација
/// Извор: ПРАВИЛНИК ЗА НАЧИНОТ НА ПОПОЛНУВАЊЕ НА ЦАРИНСКАТА ДЕКЛАРАЦИЈА.pdf
/// </summary>
public class DeclarationRule : BaseEntity
{
    /// <summary>
    /// Уникатен код на правило (нпр. "BOX33_FORMAT", "BOX40_TARIFF_MATCH")
    /// </summary>
    public string RuleCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Име на поле (Box01, Box02, ..., Box54)
    /// </summary>
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип на правило: Required, Format, CrossField, ValueList, Calculation
    /// </summary>
    public string RuleType { get; set; } = string.Empty;
    
    /// <summary>
    /// Логика за валидација (JSON expression, SQL, или regex)
    /// </summary>
    public string ValidationLogic { get; set; } = string.Empty;
    
    /// <summary>
    /// Порака за грешка (македонски)
    /// </summary>
    public string ErrorMessageMK { get; set; } = string.Empty;
    
    /// <summary>
    /// Порака за грешка (англиски)
    /// </summary>
    public string? ErrorMessageEN { get; set; }
    
    /// <summary>
    /// Сериозност: Error, Warning, Info
    /// </summary>
    public string Severity { get; set; } = "Error";
    
    /// <summary>
    /// Референца кон документ (нпр. "Правилник, Член 15")
    /// </summary>
    public string? ReferenceDocument { get; set; }
    
    /// <summary>
    /// Процедурен код на кој се применува (42 00, 51 00, ...) - nullable за општи правила
    /// </summary>
    public string? ProcedureCode { get; set; }
    
    /// <summary>
    /// Приоритет (1-100, помал број = поважно)
    /// </summary>
    public int Priority { get; set; } = 50;
    
    /// <summary>
    /// Дали е активно
    /// </summary>
    public bool IsActive { get; set; } = true;
}
