using LON.API.MasterData;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

[Route("api/MasterData/partners")]
public class PartnersController : BaseController
{
    private readonly ApplicationDbContext _context;

    public PartnersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetPartners([FromQuery] LON.Domain.Enums.PartnerType? type = null)
    {
        var query = _context.Partners.Where(p => p.IsActive).AsQueryable();
        if (type.HasValue) query = query.Where(p => p.Type == type.Value);

        var partners = await query.ToListAsync();
        return Ok(partners.Select(MasterDataMappings.MapPartner).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPartner(Guid id)
    {
        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner == null) return NotFound();
        return Ok(MasterDataMappings.MapPartner(partner));
    }

    [HttpPost]
    public async Task<IActionResult> CreatePartner([FromBody] PartnerRequest request)
    {
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Type = request.PartnerType,
            TaxNumber = request.TaxNumber,
            Address = request.Address,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            Phone = request.Phone,
            Country = request.Country,
            IsActive = request.IsActive
        };

        _context.Partners.Add(partner);
        await _context.SaveChangesAsync();
        return Ok(MasterDataMappings.MapPartner(partner));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePartner(Guid id, [FromBody] PartnerRequest request)
    {
        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner == null) return NotFound();

        partner.Code = request.Code;
        partner.Name = request.Name;
        partner.Type = request.PartnerType;
        partner.TaxNumber = request.TaxNumber;
        partner.Address = request.Address;
        partner.ContactPerson = request.ContactPerson;
        partner.Email = request.Email;
        partner.Phone = request.Phone;
        partner.Country = request.Country;
        partner.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return Ok(MasterDataMappings.MapPartner(partner));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePartner(Guid id)
    {
        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner == null) return NotFound();

        partner.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
