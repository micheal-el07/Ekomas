using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

public record CustomerResponse(int Id, string Name, string Email);


[ApiController]
[Authorize]
[Route("customers")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;
    public CustomersController(AppDbContext db) => _db = db;

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerResponse>> GetOne(int id)
    {
        var isAdmin = User.IsInRole("Admin");
        int.TryParse(User.FindFirstValue("customerId"), out var customerId);
        var c = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && ( isAdmin || c.Id == customerId));
        if (c is null) return NotFound();
        return new CustomerResponse(c.Id, c.Name, c.Email);
    }
}
