using LibreHardwareMonitor.Hardware;

namespace BenchmarkLab.Hardware;

public static class WindowsSensorDiagnostics
{
    public static void Print()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine(
                "Ez a szenzordiagnosztika jelenleg Windowson használható."
            );
            return;
        }

        Console.WriteLine("Windowsos CPU-szenzorok ellenőrzése");
        Console.WriteLine(
            $"Könyvtárverzió: {typeof(Computer).Assembly.GetName().Version}"
        );
        Console.WriteLine(
    $"PawnIO telepítve: {LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled}"
);

Console.WriteLine(
    $"PawnIO verzió: {LibreHardwareMonitor.PawnIo.PawnIo.Version?.ToString() ?? "nincs"}"
);

using var identity =
    System.Security.Principal.WindowsIdentity.GetCurrent();

var principal =
    new System.Security.Principal.WindowsPrincipal(identity);

Console.WriteLine(
    $"Rendszergazdai futtatás: " +
    principal.IsInRole(
        System.Security.Principal.WindowsBuiltInRole.Administrator
    )
);

        var computer = new Computer
        {
            IsCpuEnabled = true
        };

        try
        {
            computer.Open();

            var processors = computer.Hardware
                .Where(hardware => hardware.HardwareType == HardwareType.Cpu)
                .ToArray();

            if (processors.Length == 0)
            {
                Console.WriteLine(
                    "A könyvtár nem talált CPU-eszközt."
                );
                return;
            }

            // Egy nyitott szenzorolvasóval több mintát veszünk.
            for (int sample = 1; sample <= 3; sample++)
            {
                if (sample > 1)
                    Thread.Sleep(1000);

                Console.WriteLine(
                    $"\n--- {sample}. kiolvasás | {DateTime.Now:HH:mm:ss} ---"
                );

                foreach (IHardware processor in processors)
                {
                    PrintHardware(processor);
                }
            }
        }
        finally
        {
            computer.Close();
        }
    }

    private static void PrintHardware(IHardware hardware)
    {
        hardware.Update();

        Console.WriteLine($"\nEszköz: {hardware.Name}");

        if (hardware.Sensors.Length == 0)
        {
            Console.WriteLine("  Nincs elérhető szenzor.");
        }

        foreach (ISensor sensor in hardware.Sensors)
        {
            string value = sensor.Value.HasValue
                ? sensor.Value.Value.ToString("F2")
                : "nincs adat";

            string unit = sensor.SensorType == SensorType.Temperature
                ? " °C"
                : "";

            Console.WriteLine(
                $"  [{sensor.SensorType}] {sensor.Name}: {value}{unit}"
            );

            Console.WriteLine(
                $"    Azonosító: {sensor.Identifier}"
            );
        }

        foreach (IHardware child in hardware.SubHardware)
        {
            PrintHardware(child);
        }
    }
}