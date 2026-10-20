using LibreHardwareMonitor.Hardware;

namespace BenchmarkLab.Hardware;

internal sealed class WindowsCpuTemperatureBackend
    : ICpuTemperatureBackend
{
    private readonly Computer _computer;
    private bool _disposed;

    public WindowsCpuTemperatureBackend()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true
        };

        try
        {
            _computer.Open();
        }
        catch
        {
            try
            {
                _computer.Close();
            }
            catch
            {
                // Az eredeti inicializálási hibát őrizzük meg.
            }

            throw;
        }
    }

    public double? ReadCelsius()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double? highestTemperature = null;

        foreach (IHardware hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu)
                continue;

            hardware.Update();

            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType != SensorType.Temperature
                    || !sensor.Value.HasValue
                    || !CpuTemperatureLabels.IsCpuTemperature(sensor.Name))
                {
                    continue;
                }

                double value = sensor.Value.Value;

                if (!double.IsFinite(value))
                    continue;

                highestTemperature = highestTemperature.HasValue
                    ? Math.Max(highestTemperature.Value, value)
                    : value;
            }
        }

        return highestTemperature;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _computer.Close();
    }
}
