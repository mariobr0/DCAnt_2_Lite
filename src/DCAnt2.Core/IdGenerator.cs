using System;

namespace DCAnt2.Core;

public static class IdGenerator
{
    public static string GenerateWithPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix must be provided.", nameof(prefix));
        }

        var datePart = DateTime.UtcNow.ToString("ddMMyy");
        var hexPart = Guid.NewGuid().ToString("N")[..12];
        return $"{prefix}_{datePart}_{hexPart}";
    }
}
