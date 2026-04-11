using System;

namespace zavod.Persistence;

public sealed class ZavodPersistenceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
