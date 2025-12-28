using LON.Application.Guarantee.Commands.DebitGuarantee;
using LON.Application.Guarantee.Commands.CreditGuarantee;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class GuaranteeController : BaseController
{
    private readonly ApplicationDbContext _context;

    public GuaranteeController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var accounts = await _context.GuaranteeAccounts
            .Include(a => a.BankPartner)
            .Where(a => a.IsActive)
            .ToListAsync();

        var result = accounts.Select(a => new
        {
            a.Id,
            a.AccountNumber,
            a.AccountName,
            a.Currency,
            a.TotalLimit,
            CurrentBalance = GetAccountBalance(a.Id),
            AvailableLimit = a.TotalLimit - GetAccountBalance(a.Id),
            BankName = a.BankPartner?.Name
        });

        return Ok(result);
    }

    [HttpGet("accounts/{id}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        var account = await _context.GuaranteeAccounts
            .Include(a => a.BankPartner)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null)
            return NotFound();

        var ledgerEntries = await _context.GuaranteeLedgerEntries
            .Where(l => l.GuaranteeAccountId == id && !l.IsDeleted)
            .OrderByDescending(l => l.EntryDate)
            .Take(100)
            .ToListAsync();

        var result = new
        {
            account.Id,
            account.AccountNumber,
            account.AccountName,
            account.Currency,
            account.TotalLimit,
            CurrentBalance = GetAccountBalance(id),
            AvailableLimit = account.TotalLimit - GetAccountBalance(id),
            BankName = account.BankPartner?.Name,
            RecentEntries = ledgerEntries
        };

        return Ok(result);
    }

    [HttpPost("debit")]
    public async Task<IActionResult> DebitGuarantee([FromBody] DebitGuaranteeCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    [HttpPost("credit")]
    public async Task<IActionResult> CreditGuarantee([FromBody] CreditGuaranteeCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedger([FromQuery] Guid? accountId = null, [FromQuery] bool? isReleased = null)
    {
        var query = _context.GuaranteeLedgerEntries
            .AsQueryable();

        if (accountId.HasValue)
            query = query.Where(l => l.GuaranteeAccountId == accountId.Value);

        if (isReleased.HasValue)
            query = query.Where(l => l.IsReleased == isReleased.Value);

        var entries = await query
            .OrderByDescending(l => l.EntryDate)
            .Take(200)
            .ToListAsync();

        return Ok(entries);
    }

    [HttpGet("active-guarantees")]
    public async Task<IActionResult> GetActiveGuarantees()
    {
        var activeDebits = await _context.GuaranteeLedgerEntries
            .Where(l => l.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit && !l.IsReleased)
            .Include(l => l.GuaranteeAccount)
            .OrderBy(l => l.ExpectedReleaseDate)
            .ToListAsync();

        return Ok(activeDebits);
    }

    private decimal GetAccountBalance(Guid accountId)
    {
        return _context.GuaranteeLedgerEntries
            .Where(e => e.GuaranteeAccountId == accountId && !e.IsDeleted)
            .Sum(e => e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
    }
}
