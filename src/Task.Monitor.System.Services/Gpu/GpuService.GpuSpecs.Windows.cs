#if __WIN32__
using Microsoft.Win32;
#endif
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Gpu;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class GpuService
{
#if __WIN32__
    // The display adapter class key. Used only to enrich the DXGI adapter list with the driver
    // version and date, matched by PCI id rather than by subkey position.
    private const string DisplayClassRegPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private void OnStartGpuSpecs(GpuSpecs specs)
    {
        EnumerateAdapters(specs);
        EnrichFromRegistry(specs.Devices);

        // The aggregate the charts and summary panel read. Now the sum of the per adapter DXGI
        // figures rather than a sum of Registry qwMemorySize values that were absent for iGPUs and
        // left stale for removed cards.
        specs.TotalGpuMemory = specs.Devices.Sum(device => device.DedicatedVideoMemory);
    }

    // DXGI is the source of truth for the device list: EnumAdapters1 already returns one entry per
    // installed adapter with a stable LUID, whereas the class key's 0000/0001 subkeys have no
    // ordering contract and keep phantom entries for removed devices.
    private static void EnumerateAdapters(GpuSpecs specs)
    {
        nint factoryPtr = nint.Zero;

        try {
            Guid factoryIid = Dxgi.IID_IDXGIFactory1;
            int hr = Dxgi.CreateDXGIFactory1(ref factoryIid, out factoryPtr);

            if (hr < 0) {
                TraceEx.WriteLineOnce(
                    nameof(Dxgi.CreateDXGIFactory1),
                    $"Failed {nameof(EnumerateAdapters)}: HRESULT 0x{hr:X8}");

                return;
            }

            uint adapterIndex = 0;

            while (Dxgi.EnumAdapters1(factoryPtr, adapterIndex, out nint adapterPtr) == 0) // S_OK
            {
                try {
                    int descHr = Dxgi.GetDesc1(adapterPtr, out Dxgi.DXGI_ADAPTER_DESC1 desc);

                    if (descHr < 0) {
                        TraceEx.WriteLineOnce(
                            nameof(Dxgi.GetDesc1),
                            $"Failed {nameof(Dxgi.GetDesc1)}: HRESULT 0x{descHr:X8}");

                        continue;
                    }

                    // A pure software adapter (WARP, the Basic Render Driver) is not an installed
                    // GPU. Matches the filter OnDoWorkGpuMemoryMetrics applies, so the two
                    // enumerations stay index aligned.
                    if ((desc.Flags & Dxgi.DXGI_ADAPTER_FLAG_SOFTWARE) != 0) {
                        continue;
                    }

                    specs.Devices.Add(BuildDevice((int)adapterIndex, desc));
                }
                finally {
                    Marshal.Release(adapterPtr);
                    adapterIndex++;
                }
            }
        }
        catch (Exception ex) {
            TraceEx.WriteLineOnce(
                nameof(EnumerateAdapters),
                $"Failed {nameof(EnumerateAdapters)}: {ex.Message}");
        }
        finally {
            if (factoryPtr != nint.Zero) {
                Marshal.Release(factoryPtr);
            }
        }
    }

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

    private static string NormaliseDescription(string description)
    {
        string trimmed = description.Trim();
        return trimmed.Length > 0 ? trimmed : GpuDeviceParser.NotAvailable;
    }

    private static void EnrichFromRegistry(List<GpuDevice> devices)
    {
        if (devices.Count == 0) {
            return;
        }

        using RegistryKey? classKey = Registry.LocalMachine.OpenSubKey(DisplayClassRegPath);

        if (classKey == null) {
            TraceEx.WriteLineOnce(DisplayClassRegPath, $"Failed {nameof(EnrichFromRegistry)} OpenSubKey");
            return;
        }

        // A subkey per adapter (0000, 0001, ...), including stale entries for removed or disabled
        // devices. Matched to a live adapter by PCI id, so a phantom subkey simply finds nothing.
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
