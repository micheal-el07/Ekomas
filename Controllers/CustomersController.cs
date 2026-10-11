using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CreateCustomerRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(100)] public string Email { get; set; } = "";
}

public record CustomerResponse(int id, string Name, string Email);


[ApiController]
[Route("customers")]
public class CustomerController : ControllerBase
{
    private readonly AppDbContext _db;
    public CustomerController(AppDbContext db) => _db = db;

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerResponse>> GetOne(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        return new CustomerResponse(c.Id, c.Name, c.Email);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request)
    {
        var c = new Customer { Name = request.Name, Email = request.Email};
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOne), new { id = c.Id}, new CustomerResponse(c.Id, c.Name, c.Email));
    }
}

