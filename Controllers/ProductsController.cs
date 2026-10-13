using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

public class CreateProductRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Range(0.01, 100000)]
    public decimal Price { get; set; }
    [Range(1, 100000)]
    public int Stock { get; set; }
}

public class UpdateProductRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Range(0.01, 100000)]
    public decimal Price { get; set; }
    [Range(1, 100000)]
    public int Stock { get; set; }
}

public record ProductResponse(int Id, string Name, decimal Price, int Stock);

public class ProductQueryResponse
{
    public List<ProductResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ProductQueryResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] decimal? minPrice = null)
    {
        // if (Page <= 0 || Limit <= 0 || Limit >= 200)
        //     return BadRequest("Invalid pagination");

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 10;
        if (pageSize >100) pageSize = 100;

        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price >= minPrice.Value);
        }

        var offset = (page - 1) * pageSize;

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Id)
            .Skip(offset)
            .Take(pageSize)
            .Select(p => new ProductResponse(
                p.Id,
                p.Name,
                p.Price,
                p.Stock
            ))
            .ToListAsync();

        return new ProductQueryResponse { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount};
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductResponse>> GetOne(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();
        return new ProductResponse(product.Id, product.Name, product.Price, product.Stock);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request)
    {
        var product = new Product { Name = request.Name, Price = request.Price, Stock = request.Stock };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        ProductResponse productResponse = new ProductResponse(product.Id, product.Name, product.Price, product.Stock);
        return CreatedAtAction(nameof(GetOne), new { id = product.Id }, productResponse);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        product.Name = request.Name;
        product.Price = request.Price;
        product.Stock = request.Stock;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        var isOrdered = await _db.OrderItems.AnyAsync(i => i.ProductId == id);

        if (isOrdered)
            return Conflict("This product appears in existing orders and cannot be deleted.");

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}