using LON.Application.Finance;
using LON.Application.Finance.CostRates;
using LON.Application.Finance.PayrollPeriods;
using LON.Domain.Entities.Finance;
using LON.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// P12.2 / P12.3 — finance MVP: client contracts, rate cards, invoices.
/// Lives under <c>/api/Finance</c>.
/// </summary>
/// <remarks>
/// All request bodies use init-only properties (never positional records —
/// see `feedback_positional_records_trap` memory).
/// </remarks>
[Route("api/Finance")]
public class FinanceController : BaseController
{
    // ───────────── P12.3 — contracts ─────────────

    public sealed record RateCardEntryBody
    {
        public RateType RateType { get; init; }
        public Guid? ItemId { get; init; }
        public string? OperationCode { get; init; }
        public decimal RatePerUnit { get; init; }
        public string? Currency { get; init; }
        public DateTime ValidFrom { get; init; }
        public DateTime? ValidTo { get; init; }
        public string? Notes { get; init; }
    }

    public sealed record CreateContractBody
    {
        public string Number { get; init; } = string.Empty;
        public Guid PartnerId { get; init; }
        public DateTime ValidFrom { get; init; }
        public DateTime? ValidTo { get; init; }
        public int PaymentTermsDays { get; init; } = 30;
        public string? Currency { get; init; }
        public string? Notes { get; init; }
        public List<RateCardEntryBody>? RateCard { get; init; }
    }

    [HttpPost("contracts")]
    public async Task<IActionResult> CreateContract([FromBody] CreateContractBody body)
    {
        var result = await Mediator.Send(new CreateContractCommand(
            body.Number, body.PartnerId, body.ValidFrom, body.ValidTo,
            body.PaymentTermsDays, body.Currency ?? "EUR", body.Notes,
            body.RateCard?.Select(r => new RateCardEntryInput(
                r.RateType, r.ItemId, r.OperationCode, r.RatePerUnit, r.Currency,
                r.ValidFrom, r.ValidTo, r.Notes)).ToList()));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record UpdateContractBody
    {
        public DateTime? ValidTo { get; init; }
        public int PaymentTermsDays { get; init; }
        public bool IsActive { get; init; }
        public string? Notes { get; init; }
    }

    [HttpPut("contracts/{id}")]
    public async Task<IActionResult> UpdateContract(Guid id, [FromBody] UpdateContractBody body)
    {
        var result = await Mediator.Send(new UpdateContractCommand(
            id, body.ValidTo, body.PaymentTermsDays, body.IsActive, body.Notes));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts([FromQuery] Guid? partnerId, [FromQuery] bool? activeOnly)
    {
        var result = await Mediator.Send(new GetContractsQuery(partnerId, activeOnly));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("contracts/{id}")]
    public async Task<IActionResult> GetContract(Guid id)
    {
        var result = await Mediator.Send(new GetContractByIdQuery(id));
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    public sealed record UpsertRateEntryBody
    {
        public Guid? EntryId { get; init; }
        public RateType RateType { get; init; }
        public Guid? ItemId { get; init; }
        public string? OperationCode { get; init; }
        public decimal RatePerUnit { get; init; }
        public string? Currency { get; init; }
        public DateTime ValidFrom { get; init; }
        public DateTime? ValidTo { get; init; }
        public string? Notes { get; init; }
    }

    [HttpPost("contracts/{contractId}/rates")]
    public async Task<IActionResult> UpsertRate(Guid contractId, [FromBody] UpsertRateEntryBody body)
    {
        var result = await Mediator.Send(new UpsertRateCardEntryCommand(
            contractId, body.EntryId, body.RateType, body.ItemId, body.OperationCode,
            body.RatePerUnit, body.Currency, body.ValidFrom, body.ValidTo, body.Notes));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("contracts/{contractId}/rates/{entryId}")]
    public async Task<IActionResult> DeleteRate(Guid contractId, Guid entryId)
    {
        var result = await Mediator.Send(new DeleteRateCardEntryCommand(contractId, entryId));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ───────────── P12.2 — invoices ─────────────

    public sealed record InvoiceLineBody
    {
        public string Description { get; init; } = string.Empty;
        public Guid? ItemId { get; init; }
        public Guid? RelatedProductionOrderId { get; init; }
        public Guid? RelatedShipmentId { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
    }

    public sealed record CreateInvoiceBody
    {
        public Guid PartnerId { get; init; }
        public Guid? ContractId { get; init; }
        public DateTime? IssueDate { get; init; }
        public DateTime? DueDate { get; init; }
        public string? Currency { get; init; }
        public string? Notes { get; init; }
        public List<InvoiceLineBody>? Lines { get; init; }
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceBody body)
    {
        var result = await Mediator.Send(new CreateInvoiceCommand(
            body.PartnerId, body.ContractId, body.IssueDate, body.DueDate,
            body.Currency, body.Notes,
            body.Lines?.Select(l => new InvoiceLineInput(
                l.Description, l.ItemId, l.RelatedProductionOrderId,
                l.RelatedShipmentId, l.Quantity, l.UnitPrice)).ToList()));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("invoices/{id}/lines")]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] InvoiceLineBody body)
    {
        var result = await Mediator.Send(new AddInvoiceLineCommand(id, new InvoiceLineInput(
            body.Description, body.ItemId, body.RelatedProductionOrderId,
            body.RelatedShipmentId, body.Quantity, body.UnitPrice)));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("invoices/{id}/lines/{lineId}")]
    public async Task<IActionResult> RemoveLine(Guid id, Guid lineId)
    {
        var result = await Mediator.Send(new RemoveInvoiceLineCommand(id, lineId));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record GenerateFromPoBody
    {
        public Guid ProductionOrderId { get; init; }
        public Guid? ContractId { get; init; }
        public decimal? OverrideUnitPrice { get; init; }
        public DateTime? IssueDate { get; init; }
    }

    [HttpPost("invoices/generate-from-po")]
    public async Task<IActionResult> GenerateFromPo([FromBody] GenerateFromPoBody body)
    {
        var result = await Mediator.Send(new GenerateInvoiceFromPOCommand(
            body.ProductionOrderId, body.ContractId, body.OverrideUnitPrice, body.IssueDate));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("invoices/{id}/issue")]
    public async Task<IActionResult> Issue(Guid id)
    {
        var result = await Mediator.Send(new IssueInvoiceCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record MarkPaidBody { public DateTime? PaidAt { get; init; } }

    [HttpPost("invoices/{id}/mark-paid")]
    public async Task<IActionResult> MarkPaid(Guid id, [FromBody] MarkPaidBody body)
    {
        var result = await Mediator.Send(new MarkInvoicePaidCommand(id, body.PaidAt));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record CancelBody { public string? Reason { get; init; } }

    [HttpPost("invoices/{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBody body)
    {
        var result = await Mediator.Send(new CancelInvoiceCommand(id, body.Reason));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] Guid? partnerId,
        [FromQuery] InvoiceStatus? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var result = await Mediator.Send(new GetInvoicesQuery(partnerId, status, from, to));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("invoices/{id}")]
    public async Task<IActionResult> GetInvoice(Guid id)
    {
        var result = await Mediator.Send(new GetInvoiceByIdQuery(id));
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    // ─────────────── P16.C3.a — cost rates ───────────────
    // Backs pages/Finance/CostAccounting.tsx. Tenant isolation via the
    // global EF query filter on the ITenantScoped CostRate entity.

    [HttpGet("cost-rates")]
    public async Task<IActionResult> GetCostRates([FromQuery] CostRateScope? scope)
    {
        var result = await Mediator.Send(new GetCostRatesQuery(scope));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("cost-rates")]
    public async Task<IActionResult> CreateCostRate([FromBody] CreateCostRateCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("cost-rates/{id:guid}")]
    public async Task<IActionResult> UpdateCostRate(Guid id, [FromBody] UpdateCostRateCommand command)
    {
        if (command.Id != id) command = command with { Id = id };
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("cost-rates/{id:guid}")]
    public async Task<IActionResult> DeleteCostRate(Guid id)
    {
        var result = await Mediator.Send(new DeleteCostRateCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ─────────────── P16.C3.b — payroll periods ───────────────

    [HttpGet("payroll-periods")]
    public async Task<IActionResult> GetPayrollPeriods()
    {
        var result = await Mediator.Send(new GetPayrollPeriodsQuery());
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("payroll-periods/{id:guid}")]
    public async Task<IActionResult> GetPayrollPeriod(Guid id)
    {
        var result = await Mediator.Send(new GetPayrollPeriodByIdQuery(id));
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [HttpPost("payroll-periods")]
    public async Task<IActionResult> CreatePayrollPeriod([FromBody] CreatePayrollPeriodCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("payroll-periods/lines/{id:guid}")]
    public async Task<IActionResult> UpdatePayrollLine(Guid id, [FromBody] UpdatePayrollLineCommand command)
    {
        if (command.Id != id) command = command with { Id = id };
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payroll-periods/{id:guid}/finalize")]
    public async Task<IActionResult> FinalizePayrollPeriod(Guid id)
    {
        var result = await Mediator.Send(new FinalizePayrollPeriodCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payroll-periods/{id:guid}/export")]
    public async Task<IActionResult> ExportPayrollPeriod(Guid id)
    {
        var result = await Mediator.Send(new ExportPayrollPeriodCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
