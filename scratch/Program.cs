using System;
using System.Reflection;
using System.Linq;
using TradingPlatform.BusinessLayer;

class Program
{
    static void Main()
    {
        var type = typeof(Order);
        foreach (var prop in type.GetProperties().Where(p => p.Name.Contains("Quant") || p.Name.Contains("Vol") || p.Name.Contains("Total")))
        {
            Console.WriteLine(prop.Name + " - " + prop.PropertyType.Name);
        }
    }
}
