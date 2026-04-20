using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

/// <summary>
/// Tenant (Uvoznik) CRUD. Admin-only for now; future SuperAdmin role will
/// inherit. Tenants themselves are not tenant-scoped (they ARE the scope).
/// </summary>
[Authorize(Roles = "Administrator")]
public class TenantsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public TenantsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Tenant>>> GetAll()
    {
        var tenants = await _context.Tenants.OrderBy(t => t.Code).ToListAsync();
        return Ok(tenants);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tenant>> GetById(Guid id)
    {
        var t = await _context.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        return Ok(t);
    }

    [HttpPost]
    public async Task<ActionResult<Tenant>> Create([FromBody] TenantRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { errorMessage = "Code is required." });
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { errorMessage = "Name is required." });

        var normalizedCode = req.Code.Trim().ToUpperInvariant();

        if (await _context.Tenants.AnyAsync(t => t.Code == normalizedCode))
            return Conflict(new { errorMessage = $"Tenant with code '{normalizedCode}' already exists." });

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = req.Name.Trim(),
            LegacyUvoznik = string.IsNullOrWhiteSpace(req.LegacyUvoznik) ? null : req.LegacyUvoznik.Trim().ToUpperInvariant(),
            TaxNumber = Nullable(req.TaxNumber),
            Address = Nullable(req.Address),
            Country = Nullable(req.Country),
            ContactName = Nullable(req.ContactName),
            Email = Nullable(req.Email),
            Phone = Nullable(req.Phone),
            CustomsAuthorizationNumber = Nullable(req.CustomsAuthorizationNumber),
            DefaultLanguage = string.IsNullOrWhiteSpace(req.DefaultLanguage) ? "mk" : req.DefaultLanguage,
            IsActive = req.IsActive ?? true
        };

        await _context.Tenants.AddAsync(tenant);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Tenant>> Update(Guid id, [FromBody] TenantRequest req)
    {
        var t = await _context.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.Name)) t.Name = req.Name.Trim();
        if (req.LegacyUvoznik is not null) t.LegacyUvoznik = string.IsNullOrWhiteSpace(req.LegacyUvoznik) ? null : req.LegacyUvoznik.Trim().ToUpperInvariant();
        if (req.TaxNumber is not null) t.TaxNumber = Nullable(req.TaxNumber);
        if (req.Address is not null) t.Address = Nullable(req.Address);
        if (req.Country is not null) t.Country = Nullable(req.Country);
        if (req.ContactName is not null) t.ContactName = Nullable(req.ContactName);
        if (req.Email is not null) t.Email = Nullable(req.Email);
        if (req.Phone is not null) t.Phone = Nullable(req.Phone);
        if (req.CustomsAuthorizationNumber is not null) t.CustomsAuthorizationNumber = Nullable(req.CustomsAuthorizationNumber);
        if (!string.IsNullOrWhiteSpace(req.DefaultLanguage)) t.DefaultLanguage = req.DefaultLanguage;
        if (req.IsActive.HasValue) t.IsActive = req.IsActive.Value;
        if (req.InflateImportForWaste.HasValue) t.InflateImportForWaste = req.InflateImportForWaste.Value;
        if (req.AllowFefoAutoPick.HasValue) t.AllowFefoAutoPick = req.AllowFefoAutoPick.Value;

        await _context.SaveChangesAsync();
        return Ok(t);
    }

    /// <summary>
    /// P5.2.5 — quick toggle for the per-tenant FEFO auto-pick policy.
    /// PATCH-ish dedicated endpoint so admin UI can flip the flag without
    /// round-tripping the entire Tenant entity.
    /// </summary>
    [HttpPut("{id}/settings/fefo")]
    public async Task<ActionResult<Tenant>> SetFefoAutoPick(Guid id, [FromBody] TenantFefoFlagRequest req)
    {
        var t = await _context.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.AllowFefoAutoPick = req.AllowFefoAutoPick;
        await _context.SaveChangesAsync();
        return Ok(t);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> SoftDelete(Guid id)
    {
        var t = await _context.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.IsDeleted = true;
        t.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string? Nullable(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public record TenantRequest
    {
        public string? Code { get; init; }
        public string? Name { get; init; }
        public string? LegacyUvoznik { get; init; }
        public string? TaxNumber { get; init; }
        public string? Address { get; init; }
        public string? Country { get; init; }
        public string? ContactName { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public string? CustomsAuthorizationNumber { get; init; }
        public string? DefaultLanguage { get; init; }
        public bool? IsActive { get; init; }
        public bool? InflateImportForWaste { get; init; }
        public bool? AllowFefoAutoPick { get; init; }
    }

    public record TenantFefoFlagRequest
    {
        public bool AllowFefoAutoPick { get; init; }
    }
}
