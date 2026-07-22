using System;

namespace DCAnt2.Core;

public static class IdGenerator
{
    public static string GenerateWithPrefix(string prefix)
    {
        var datePart = DateTime.UtcNow.ToString("ddMMyy");
        var hexPart = Guid.NewGuid().ToString("N").Substring(0, 12);
        return $"{prefix}_{datePart}_{hexPart}";
    }
}
