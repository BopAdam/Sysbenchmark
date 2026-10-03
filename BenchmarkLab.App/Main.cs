using BenchmarkLab.App;

ConsoleView.ShowHeader();

if (args.Contains("--sensors"))
{
    AppStartup.RunSensorDiagnostics();
    return;
}

var monitor = AppStartup.CreateMonitor();
var systemInfo = monitor.GetSystemInfo();

ConsoleView.ShowSystemInfo(systemInfo);

if (args.Contains("--system-info"))
{
    Console.WriteLine("Rendszeradatok ellenőrzése kész.");
    return;
}

var device = new DeviceInfo(
    MachineName: Environment.MachineName,
    OperatingSystem: OperatingSystem.IsWindows() ? "Windows" : "Linux",
    CpuModel: systemInfo.CpuModel,
    TotalMemoryGb: systemInfo.TotalMemoryGb)
{
    DeviceUid = DeviceIdentity.LoadOrCreate()
};

Console.WriteLine($"Eszközazonosító: {device.DeviceUid}");

IBenchmarkStore? store = AppStartup.CreateStore();

Console.WriteLine(
    $"Gép: {device.MachineName} ({device.OperatingSystem})");

var application = new ConsoleApplication(device, store);
application.Run();