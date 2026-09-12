using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.Gui.Controls.SystemInformation;

public sealed partial class SystemInfoControl
{
    private const string NotAvailable = "N/A";

    private void RebuildRows(SystemSnapshot s)
    {
        systemInfoView.Items.Clear();

        switch (selectedSection) {
            case Section.System:  AddSystemSection(s); break;
            case Section.Cpu:     AddCpuSection(s); break;
            case Section.Memory:  AddMemorySection(s); break;
            case Section.Gpu:     AddGpuSection(s); break;
            case Section.Disk:    AddDiskSection(s); break;
            case Section.Network: AddNetworkSection(s); break;
        }
    }

    private void AddSystemSection(SystemSnapshot s)
    {
        AddRow("Machine Name:", Environment.MachineName.ToUpper());
        AddRow("Operating System:", SystemInfo.GetOsVersion());
        AddRow("Version:", Environment.OSVersion.Version.ToString());
        AddRow();      
        AddRow("CPU:", s.Cpu?.Specs.CpuName ?? string.Empty);
        AddRow("Logical Processors", s.Cpu?.Specs.CpuCores.ToString() ?? string.Empty);
        AddRow("Base Speed:", s.Cpu?.Specs.ToCpuFrequencyGhz() ?? string.Empty);
        AddRow();
        AddRow("Memory:", s.Memory?.Metrics.TotalPhysical.ToFormattedByteSize() ?? string.Empty);
        
        foreach (GpuDevice gpuDevice in s.Gpu?.Specs.Devices ?? []) {
            AddRow();
            AddRow($"GPU {gpuDevice.Index}:", gpuDevice.Description);
            AddRow("Dedicated Memory:", gpuDevice.DedicatedVideoMemory.ToFormattedByteSize());
        }

        foreach (DiskDevice diskDevice in s.Disk?.Specs.Devices ?? []) {
            AddRow();
            AddRow($"Disk: {diskDevice.Index}:", diskDevice.ToDisplayName());
            AddRow("Capacity:", diskDevice.Capacity.ToFormattedByteSize());
        }
    }

    private void AddCpuSection(SystemSnapshot s)
    {
        if (s.Cpu is not { } cpu) {
            AddRow(string.Empty, GatheringText);
            return;
        }

        CpuSpecs specs = cpu.Specs;

        AddRow("Processor:", OrNotAvailable(specs.CpuName));
        AddRow("Logical Processors:", specs.CpuCores.ToString());
        AddRow("Base Frequency:", specs.ToCpuFrequencyGhz());
#if __APPLE__
        AddRow("Performance Cores:", $"{specs.CpuPerformanceCores} @ {specs.CpuPerformanceFrequency / 1000.0:0.00} GHz");
        AddRow("Efficiency Cores:", $"{specs.CpuEfficiencyCores} @ {specs.CpuEfficiencyFrequency / 1000.0:0.00} GHz");
#endif
        AddRow("L1 Cache:", specs.ToCpuL1Cache());
        AddRow("L2 Cache:", specs.ToCpuL2Cache());
        AddRow("L3 Cache:", specs.ToCpuL3Cache());
        AddRow("Virtualization:", specs.ToCpuVirtualization());
    }

    private void AddMemorySection(SystemSnapshot s)
    {
        if (s.Memory is not { } memory) {
            AddRow(string.Empty, GatheringText);
            return;
        }

        List<MemoryDevice> devices = memory.Specs.Devices;
        AddRow("Installed Modules:", devices.Count.ToString());

        foreach (MemoryDevice device in devices) {
            AddHeaderRow($"SLOT {device.Slot}  {OrNotAvailable(device.DeviceLocator)}", indent: 2);

            AddRow("Capacity:", device.ToMemoryCapacity(), indent: 4);
            AddRow("Type:", OrNotAvailable(device.MemoryType), indent: 4);
            AddRow("Form Factor:", OrNotAvailable(device.FormFactor), indent: 4);
            AddRow("Speed:", $"{device.Speed} MT/s", indent: 4);
            AddRow("Configured Speed:", $"{device.ConfiguredClockSpeed} MT/s", indent: 4);
            AddRow("Manufacturer:", OrNotAvailable(device.Manufacturer), indent: 4);
            AddRow("Part Number:", OrNotAvailable(device.PartNumber), indent: 4);
            AddRow("Serial Number:", OrNotAvailable(device.SerialNumber), indent: 4);
            AddRow("Bank Locator:", OrNotAvailable(device.BankLocator), indent: 4);
        }
    }

    private void AddGpuSection(SystemSnapshot s)
    {
        if (s.Gpu is not { } gpu) {
            AddRow(string.Empty, GatheringText);
            return;
        }

        List<GpuDevice> devices = gpu.Specs.Devices;
        AddRow("Installed Adapters:", devices.Count.ToString());
        AddRow("Total Dedicated Memory:", gpu.Specs.TotalGpuMemory.ToFormattedByteSize());

        foreach (GpuDevice device in devices) {
            AddHeaderRow($"GPU {device.Index}  {OrNotAvailable(device.Description)}", indent: 2);

            AddRow("Vendor:", OrNotAvailable(device.Vendor), indent: 4);
            AddRow("Description:", OrNotAvailable(device.Description), indent: 4);
            AddRow("Adapter Type:", OrNotAvailable(device.AdapterType), indent: 4);
            AddRow("Dedicated Video Memory:", device.DedicatedVideoMemory.ToFormattedByteSize(), indent: 4);
            AddRow("Dedicated System Memory:", device.DedicatedSystemMemory.ToFormattedByteSize(), indent: 4);
            AddRow("Shared System Memory:", device.SharedSystemMemory.ToFormattedByteSize(), indent: 4);
            AddRow("Vendor / Device ID:", $"0x{device.VendorId:X4} / 0x{device.DeviceId:X4}", indent: 4);
            AddRow("Driver Version:", OrNotAvailable(device.DriverVersion), indent: 4);
            AddRow("Driver Date:", OrNotAvailable(device.DriverDate), indent: 4);
        }
    }

    private void AddDiskSection(SystemSnapshot s)
    {
        if (s.Disk is not { } disk) {
            AddRow(string.Empty, GatheringText);
            return;
        }

        DiskSpecs specs = disk.Specs;
        AddRow("Installed Drives:", specs.Devices.Count.ToString());
        AddRow("Total Capacity:", specs.ToDiskTotalCapacity().ToFormattedByteSize());

        foreach (DiskDevice device in specs.Devices) {
            string model = OrNotAvailable(device.Model);
            AddHeaderRow($"DISK {device.Index}  {model}", indent: 2);

            AddRow("Model:", model, indent: 4);
            AddRow("Manufacturer:", OrNotAvailable(device.Manufacturer), indent: 4);
            AddRow("Firmware Revision:", OrNotAvailable(device.FirmwareRevision), indent: 4);
            AddRow("Serial Number:", OrNotAvailable(device.SerialNumber), indent: 4);
            AddRow("Bus Type:", OrNotAvailable(device.BusType), indent: 4);
            AddRow("Media Type:", OrNotAvailable(device.MediaType), indent: 4);
            AddRow("Removable:", device.IsRemovable ? "Yes" : "No", indent: 4);
            AddRow("Capacity:", device.Capacity.ToFormattedByteSize(), indent: 4);

            foreach (DiskVolume volume in device.Volumes) {
                AddVolumeRow(volume, indent: 6);
            }
        }

        if (specs.UnattachedVolumes.Count > 0) {
            AddHeaderRow("UNATTACHED VOLUMES", indent: 2);

            foreach (DiskVolume volume in specs.UnattachedVolumes) {
                AddVolumeRow(volume, indent: 4);
            }
        }
    }

    private void AddNetworkSection(SystemSnapshot s)
    {
        if (s.Network is not { } network) {
            AddRow(string.Empty, GatheringText);
            return;
        }

        List<NetworkDevice> devices = network.Specs.Devices;
        AddRow("Adapters:", devices.Count.ToString());

        foreach (NetworkDevice device in devices) {
            AddHeaderRow(device.ToDisplayName().ToUpper(), indent: 2);

            AddRow("Name:", OrNotAvailable(device.Name), indent: 4);
            AddRow("Description:", OrNotAvailable(device.Description), indent: 4);
            AddRow("Connection Type:", OrNotAvailable(device.ConnectionType), indent: 4);
            AddRow("Physical Medium:", OrNotAvailable(device.PhysicalMedium), indent: 4);
            AddRow("MAC Address:", OrNotAvailable(device.MacAddress), indent: 4);
            AddRow("IPv4 Addresses:", device.IPv4Addresses.ToAddressList(), indent: 4);
            AddRow("IPv6 Addresses:", device.IPv6Addresses.ToAddressList(), indent: 4);
            AddRow("Transmit Link Speed:", device.TransmitLinkSpeed.ToLinkSpeed(), indent: 4);
            AddRow("Receive Link Speed:", device.ReceiveLinkSpeed.ToLinkSpeed(), indent: 4);
            AddRow("Operational Status:", OrNotAvailable(device.OperationalStatus), indent: 4);
        }
    }

    // ---- Row helpers ----------------------------------------------------------------------------

    private void AddVolumeRow(DiskVolume volume, int indent)
    {
        string name = volume.MountPoints.Length > 0
            ? string.Join(" ", volume.MountPoints.Select(mountPoint => mountPoint.TrimEnd('\\')))
            : OrNotAvailable(volume.Label.Length > 0 ? volume.Label : volume.VolumeName);

        string? used = volume.IsReady
            ? $"{(volume.FormattedCapacity - volume.AvailableFreeSpace).ToFormattedByteSize()} / {volume.FormattedCapacity.ToFormattedByteSize()}"
            : null;

        string detail = string.Join("  ", new[] {
                volume.Label.Length > 0 ? volume.Label : null,
                volume.FileSystem.Length > 0 ? volume.FileSystem : null,
                used
            }
            .Where(part => !string.IsNullOrEmpty(part)));

        AddRow($"{name}:", detail.Length > 0 ? detail : NotAvailable, indent);
    }

    // A row styled in the theme header colours across both cells, so the trailing fill (drawn in
    // the last sub-item's background) carries the bar the full width of the list view.
    private void AddHeaderRow(string title, int indent = 0)
    {
        ListViewItem row = new(new[] {
            new ListViewSubItem(null!, $"{new string(' ', indent)}{title}"), 
            new ListViewSubItem(null!, string.Empty)
        });

        systemInfoView.Items.Add(row);
    }

    private void AddRow(string label, string value, int indent = 0) =>
        systemInfoView.Items.Add(new ListViewItem(new[] { $"{new string(' ', indent)}{label}", value }));

    private void AddRow() =>
        systemInfoView.Items.Add(new ListViewItem(new[] { string.Empty, string.Empty }));

    private static string OrNotAvailable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;
}
