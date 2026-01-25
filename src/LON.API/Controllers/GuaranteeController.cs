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

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] GuaranteeAccountRequest request)
    {
        var account = new LON.Domain.Entities.Guarantee.GuaranteeAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = request.AccountNumber,
            AccountName = request.AccountName,
            BankPartnerId = request.BankPartnerId,
            Currency = request.Currency,
            TotalLimit = request.TotalLimit,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.GuaranteeAccounts.Add(account);
        await _context.SaveChangesAsync();

        account = await _context.GuaranteeAccounts
            .Include(a => a.BankPartner)
            .FirstAsync(a => a.Id == account.Id);

        return Ok(account);
    }

    [HttpPut("accounts/{id}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] GuaranteeAccountRequest request)
    {
        var account = await _context.GuaranteeAccounts.FirstOrDefaultAsync(a => a.Id == id);
        if (account == null)
            return NotFound();

        account.AccountNumber = request.AccountNumber;
        account.AccountName = request.AccountName;
        account.BankPartnerId = request.BankPartnerId;
        account.Currency = request.Currency;
        account.TotalLimit = request.TotalLimit;
        account.IsActive = request.IsActive;
        account.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        account = await _context.GuaranteeAccounts
            .Include(a => a.BankPartner)
            .FirstAsync(a => a.Id == id);

        return Ok(account);
    }

    [HttpDelete("accounts/{id}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var account = await _context.GuaranteeAccounts.FirstOrDefaultAsync(a => a.Id == id);
        if (account == null)
            return NotFound();

        account.IsActive = false;
        account.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("ledger")]
    public async Task<IActionResult> CreateLedgerEntry([FromBody] GuaranteeLedgerRequest request)
    {
        var account = await _context.GuaranteeAccounts.FirstOrDefaultAsync(a => a.Id == request.GuaranteeAccountId);
        if (account == null)
            return BadRequest(new { message = "Guarantee account not found" });

        var currentBalance = GetAccountBalance(request.GuaranteeAccountId);

        if (request.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit)
        {
            if (currentBalance + request.Amount > account.TotalLimit)
                return BadRequest(new { message = "Insufficient guarantee limit" });
        }

        var entry = new LON.Domain.Entities.Guarantee.GuaranteeLedgerEntry
        {
            Id = Guid.NewGuid(),
            GuaranteeAccountId = request.GuaranteeAccountId,
            EntryDate = DateTime.UtcNow,
            EntryType = request.EntryType,
            Amount = request.Amount,
            Currency = request.Currency,
            MRN = request.MRN,
            CustomsDeclarationId = request.CustomsDeclarationId,
            Description = request.Description ?? string.Empty,
            ExpectedReleaseDate = request.ExpectedReleaseDate,
            IsReleased = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.GuaranteeLedgerEntries.Add(entry);
        await _context.SaveChangesAsync();

        return Ok(entry);
    }

    [HttpPut("ledger/{id}/release")]
    public async Task<IActionResult> ReleaseLedgerEntry(Guid id)
    {
        var entry = await _context.GuaranteeLedgerEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null)
            return NotFound();

        entry.IsReleased = true;
        entry.ActualReleaseDate = DateTime.UtcNow;
        entry.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(entry);
    }

    private decimal GetAccountBalance(Guid accountId)
    {
        return _context.GuaranteeLedgerEntries
            .Where(e => e.GuaranteeAccountId == accountId && !e.IsDeleted && !e.IsReleased)
            .Sum(e => e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
    }
}

// Request DTOs
public class GuaranteeAccountRequest
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid BankPartnerId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal TotalLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public class GuaranteeLedgerRequest
{
    public Guid GuaranteeAccountId { get; set; }
    public LON.Domain.Enums.GuaranteeEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? MRN { get; set; }
    public Guid? CustomsDeclarationId { get; set; }
    public string? Description { get; set; }
    public DateTime? ExpectedReleaseDate { get; set; }
}
