using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;

namespace LON.Domain.Entities.Customs;

/// <summary>
/// LON Одобрение - Одобрение за увоз за облагородување
/// Извор: УСЦЗ Прилог 27, Упатство увоз за облагородување
/// </summary>
public class LONAuthorization : BaseEntity
{
    /// <summary>
    /// Број на одобрение (издаден од Царинска управа)
    /// </summary>
    public string AuthorizationNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Партнер (имател на одобрение)
    /// </summary>
    public Guid PartnerId { get; set; }
    public virtual Partner Partner { get; set; } = null!;
    
    /// <summary>
    /// Датум на издавање
    /// </summary>
    public DateTime IssueDate { get; set; }
    
    /// <summary>
    /// Рок на важност (ако е ограничено)
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
    
    /// <summary>
    /// Тип: Повеќекратно или Еднократно
    /// </summary>
    public string AuthorizationType { get; set; } = "Повеќекратно";
    
    /// <summary>
    /// Систем: "ОдложеноПлаќање" или "ВраќањеДавачки"
    /// </summary>
    public string SystemType { get; set; } = "ОдложеноПлаќање";
    
    /// <summary>
    /// Операција: Обработка, Преработка, Поправка
    /// </summary>
    public string OperationType { get; set; } = string.Empty;
    
    /// <summary>
    /// Шифра на економски услов (10, 11, 12 - член 349 УСЦЗ)
    /// </summary>
    public string? EconomicConditionCode { get; set; }
    
    /// <summary>
    /// Износ на гаранција
    /// </summary>
    public decimal GuaranteeAmount { get; set; }
    
    /// <summary>
    /// Референца на инструмент за обезбедување
    /// </summary>
    public string? GuaranteeReference { get; set; }
    
    /// <summary>
    /// Надлежна царинарница
    /// </summary>
    public string CompetentCustomsOffice { get; set; } = string.Empty;
    
    /// <summary>
    /// Надзорен царински орган (СНИО)
    /// </summary>
    public string? SupervisoryOffice { get; set; }
    
    /// <summary>
    /// Рок за завршување (во денови)
    /// </summary>
    public int CompletionPeriodDays { get; set; }
    
    /// <summary>
    /// Статус: Active, Suspended, Revoked, Expired
    /// </summary>
    public string Status { get; set; } = "Active";
    
    /// <summary>
    /// Белешки
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Декларации под ова одобрение
    /// </summary>
    public virtual ICollection<CustomsDeclaration> Declarations { get; set; } = new List<CustomsDeclaration>();
    
    /// <summary>
    /// Одобрени стоки (тарифни ознаки)
    /// </summary>
    public virtual ICollection<LONAuthorizationItem> ApprovedItems { get; set; } = new List<LONAuthorizationItem>();
}

/// <summary>
/// Стока одобрена за LON процедура
/// </summary>
public class LONAuthorizationItem : BaseEntity
{
    public Guid LONAuthorizationId { get; set; }
    public virtual LONAuthorization LONAuthorization { get; set; } = null!;
    
    /// <summary>
    /// Увозна стока (репроматеријал)
    /// </summary>
    public Guid ImportItemId { get; set; }
    public virtual Item ImportItem { get; set; } = null!;
    
    /// <summary>
    /// Тарифна ознака на увозна стока
    /// </summary>
    public string ImportTariffCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Компензациски производ (добиен производ)
    /// </summary>
    public Guid? CompensatingProductId { get; set; }
    public virtual Item? CompensatingProduct { get; set; }
    
    /// <summary>
    /// Тарифна ознака на компензациски производ
    /// </summary>
    public string? CompensatingTariffCode { get; set; }
    
    /// <summary>
    /// Коефициент на принос (yield rate)
    /// Пример: 1 kg репроматеријал → 0.85 kg готов производ
    /// </summary>
    public decimal YieldRate { get; set; } = 1.0m;
    
    /// <summary>
    /// Дозволен отпад (%)
    /// </summary>
    public decimal AllowedWastePercentage { get; set; }
}
