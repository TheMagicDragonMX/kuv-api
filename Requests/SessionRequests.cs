using System.ComponentModel.DataAnnotations;

namespace kuv_api.Requests;

public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record RegisterRequest(
	string Username,	
	string Email,	
	int Age,
	string Password
);