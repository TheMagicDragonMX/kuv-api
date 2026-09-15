namespace kuv_api.Models;

public class User : BaseEntity
{
	public string Username { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public int Age { get; set; }
	public string Hashword { get; set; } = string.Empty;
	public string? RefreshToken { get; set; }
}