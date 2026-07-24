using System;
using System.Reflection;

class Program {
    static void Main() {
        try {
            var asm = Assembly.LoadFrom(@"C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\bin\Release\net8.0-windows\TradingPlatform.BusinessLayer.dll");
            var coreType = asm.GetType("TradingPlatform.BusinessLayer.Core");
            if (coreType != null) {
                Console.WriteLine("Events of Core:");
                foreach (var ev in coreType.GetEvents()) {
                    Console.WriteLine("- " + ev.Name);
                }
                Console.WriteLine("\nProperties of Core:");
                foreach (var prop in coreType.GetProperties()) {
                    Console.WriteLine("- " + prop.Name);
                }
            }
        } catch (Exception ex) { Console.WriteLine(ex.Message); }
    }
}
