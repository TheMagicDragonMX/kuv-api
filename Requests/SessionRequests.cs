namespace kuv_api.Requests;

public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);