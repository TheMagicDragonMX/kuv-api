namespace kuv_api.Exceptions;

public sealed class UserAlreadyExistsException()
    : Exception("El nombre de usuario o correo ya está registrado.");
