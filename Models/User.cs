public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Customer";
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

}