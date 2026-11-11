#if __WIN32__
using System.Diagnostics;
using Microsoft.Win32;
#endif
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Gpu;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class GpuService
{
#if __WIN32__
    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    private const string DisplayClassRegPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private static GpuDevice BuildDevice(int index, Dxgi.DXGI_ADAPTER_DESC1 desc)
    {
        long dedicatedVideo = (long)(ulong)desc.DedicatedVideoMemory;

        GpuDevice device = new();
        // Not inline declared to assist with debugging.
        device.Index                 = index;
        device.AdapterLuid           = desc.AdapterLuid;
        device.Description           = NormaliseDescription(desc.GetDescription());
        device.VendorId              = desc.VendorId;
        device.DeviceId              = desc.DeviceId;
        device.SubSysId              = desc.SubSysId;
        device.Revision              = desc.Revision;
        device.Vendor                = GpuDeviceParser.DecodeVendor(desc.VendorId);
        device.AdapterType           = GpuDeviceParser.DecodeAdapterType(desc.Flags, desc.VendorId, dedicatedVideo);
        device.DedicatedVideoMemory  = dedicatedVideo;
        device.DedicatedSystemMemory = (long)(ulong)desc.DedicatedSystemMemory;
        device.SharedSystemMemory    = (long)(ulong)desc.SharedSystemMemory;

        return device;
    }

    private static void EnumerateAdapters(GpuSpecs specs)
    {
        try {
            Guid factoryIid = Dxgi.IID_IDXGIFactory1;
            int  hr         = Dxgi.CreateDXGIFactory1(ref factoryIid, out nint factoryRef);

            using ComScope factory = new(factoryRef);

            if (hr < 0) {
                Trace.WriteLine($"{nameof(GpuService)}: DXGI {nameof(Dxgi.CreateDXGIFactory1)} FAILED {ErrorCodes.Describe(hr)}");
                return;
            }

            Trace.WriteLine($"{nameof(GpuService)}: DXGI {nameof(Dxgi.CreateDXGIFactory1)} OK");

            for (uint adapterIndex = 0;
                 Dxgi.EnumAdapters1(factory, adapterIndex, out nint adapterRef) == Dxgi.S_OK;
                 adapterIndex++) {

                using ComScope adapter = new(adapterRef);

                int descHr = Dxgi.GetDesc1(adapter, out Dxgi.DXGI_ADAPTER_DESC1 desc);

                if (descHr < 0) {
                    Trace.WriteLine(
                        $"{nameof(GpuService)}: adapter {adapterIndex} {nameof(Dxgi.GetDesc1)} FAILED {ErrorCodes.Describe(descHr)}");

                    continue;
                }

                if ((desc.Flags & Dxgi.DXGI_ADAPTER_FLAG_SOFTWARE) != 0) {
                    Trace.WriteLine($"{nameof(GpuService)}: adapter {adapterIndex} {desc.GetDescription().Trim()} SKIPPED, software");
                    continue;
                }

                specs.Devices.Add(BuildDevice((int)adapterIndex, desc));
            }
        }
        catch (Exception ex) {
            Trace.WriteLine($"{nameof(GpuService)}: DXGI adapters FAILED with {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string NormaliseDescription(string description)
    {
        string trimmed = description.Trim();
        
        return trimmed.Length > 0 
            ? trimmed 
            : GpuDeviceParser.NotAvailable;
    }

    private void OnStartGpuSpecs(GpuSpecs specs)
    {
        EnumerateAdapters(specs);
        UpdateFromRegistry(specs.Devices);

        foreach (GpuDevice device in specs.Devices) {
            Trace.WriteLine(
                $"{ServiceName}: adapter {device.Index} {device.Description}, {device.VendorId:X4}:{device.DeviceId:X4}, " +
                $"{device.AdapterType}, {device.DedicatedVideoMemory / BytesPerMegabyte:0} MB, " +
                $"driver {device.DriverVersion} ({device.DriverDate})");
        }

        specs.TotalGpuMemory = specs.Devices.Sum(device => device.DedicatedVideoMemory);
    }

    private static void UpdateFromRegistry(List<GpuDevice> devices)
    {
        if (devices.Count == 0) {
            return;
        }

        using RegistryKey? classKey = Registry.LocalMachine.OpenSubKey(DisplayClassRegPath);

        if (classKey == null) {
            Trace.WriteLine($"{nameof(GpuService)}: registry {DisplayClassRegPath} FAILED, the key could not be opened");
            return;
        }

        // A subkey per adapter (0000, 0001, ...).
        foreach (string subKeyName in classKey.GetSubKeyNames()) {
            if (!subKeyName.StartsWith('0')) {
                continue;
            }

            using RegistryKey? subKey = classKey.OpenSubKey(subKeyName);

            if (subKey == null) {
                continue;
            }

            if (!GpuDeviceParser.TryParsePciIds(
                subKey.GetValue("MatchingDeviceId") as string,
                out uint vendorId,
                out uint deviceId)) {

                continue;
            }

            GpuDevice? device = devices.FirstOrDefault(
                candidate => candidate.VendorId == vendorId && candidate.DeviceId == deviceId);

            if (device == null) {
                continue;
            }

            if (subKey.GetValue("DriverVersion") is string driverVersion && driverVersion.Length > 0) {
                device.DriverVersion = driverVersion;
            }

            if (subKey.GetValue("DriverDate") is string driverDate && driverDate.Length > 0) {
                device.DriverDate = driverDate;
            }
        }
    }
#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
