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
            case Section.Cpu:     AddCpuSection(systemInfoView, s); break;
            case Section.Memory:  AddMemorySection(systemInfoView, s); break;
            case Section.Gpu:     AddGpuSection(systemInfoView, s); break;
            case Section.Disk:    AddDiskSection(systemInfoView, s); break;
            case Section.Network: AddNetworkSection(systemInfoView, s); break;
        }
    }

    private void RebuildSummaryRows(SystemSnapshot s)
    {
        systemSummaryView.Items.Clear();
        AddSystemSection(systemSummaryView, s);
    }

    private void AddSystemSection(ListView target, SystemSnapshot s)
    {
        AddRow(target, "Machine Name:", Environment.MachineName.ToUpper());
        AddRow(target, "Operating System:", SystemInfo.GetOsVersion());
        AddRow(target, "Version:", Environment.OSVersion.Version.ToString());
        AddRow(target);
        AddRow(target, "CPU:", s.Cpu?.Specs.CpuName ?? string.Empty);
        AddRow(target, "Logical Processors", s.Cpu?.Specs.CpuCores.ToString() ?? string.Empty);
        AddRow(target, "Base Speed:", s.Cpu?.Specs.ToCpuFrequencyGhz() ?? string.Empty);
        AddRow(target);
        AddRow(target, "Memory:", s.Memory?.Metrics.TotalPhysical.ToFormattedByteSize() ?? string.Empty);
    }

    private void AddCpuSection(ListView target, SystemSnapshot s)
    {
        if (s.Cpu is not { } cpu) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        CpuSpecs specs = cpu.Specs;

        AddRow(target, "Processor:", OrNotAvailable(specs.CpuName));
        AddRow(target, "Logical Processors:", specs.CpuCores.ToString());
        AddRow(target, "Base Frequency:", specs.ToCpuFrequencyGhz());
#if __APPLE__
        AddRow(target, "Performance Cores:", $"{specs.CpuPerformanceCores} @ {specs.CpuPerformanceFrequency / 1000.0:0.00} GHz");
        AddRow(target, "Efficiency Cores:", $"{specs.CpuEfficiencyCores} @ {specs.CpuEfficiencyFrequency / 1000.0:0.00} GHz");
#endif
        AddRow(target, "L1 Cache:", specs.ToCpuL1Cache());
        AddRow(target, "L2 Cache:", specs.ToCpuL2Cache());
        AddRow(target, "L3 Cache:", specs.ToCpuL3Cache());
        AddRow(target, "Virtualization:", specs.ToCpuVirtualization());
    }

    private void AddMemorySection(ListView target, SystemSnapshot s)
    {
        if (s.Memory is not { } memory) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        List<MemoryDevice> devices = memory.Specs.Devices;
        AddRow(target, "Installed Modules:", devices.Count.ToString());

        foreach (MemoryDevice device in devices) {
            AddHeaderRow(target, $"SLOT {device.Slot}  {OrNotAvailable(device.DeviceLocator)}", indent: 2);

            AddRow(target, "Capacity:", device.ToMemoryCapacity(), indent: 4);
            AddRow(target, "Type:", OrNotAvailable(device.MemoryType), indent: 4);
            AddRow(target, "Form Factor:", OrNotAvailable(device.FormFactor), indent: 4);
            AddRow(target, "Speed:", $"{device.Speed} MT/s", indent: 4);
            AddRow(target, "Configured Speed:", $"{device.ConfiguredClockSpeed} MT/s", indent: 4);
            AddRow(target, "Manufacturer:", OrNotAvailable(device.Manufacturer), indent: 4);
            AddRow(target, "Part Number:", OrNotAvailable(device.PartNumber), indent: 4);
            AddRow(target, "Serial Number:", OrNotAvailable(device.SerialNumber), indent: 4);
            AddRow(target, "Bank Locator:", OrNotAvailable(device.BankLocator), indent: 4);
        }
    }

    private void AddGpuSection(ListView target, SystemSnapshot s)
    {
        if (s.Gpu is not { } gpu) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        List<GpuDevice> devices = gpu.Specs.Devices;
        AddRow(target, "Installed Adapters:", devices.Count.ToString());
        AddRow(target, "Total Dedicated Memory:", gpu.Specs.TotalGpuMemory.ToFormattedByteSize());

        foreach (GpuDevice device in devices) {
            AddHeaderRow(target, $"GPU {device.Index}  {OrNotAvailable(device.Description)}", indent: 2);

            AddRow(target, "Vendor:", OrNotAvailable(device.Vendor), indent: 4);
            AddRow(target, "Description:", OrNotAvailable(device.Description), indent: 4);
            AddRow(target, "Adapter Type:", OrNotAvailable(device.AdapterType), indent: 4);
            AddRow(target, "Dedicated Video Memory:", device.DedicatedVideoMemory.ToFormattedByteSize(), indent: 4);
            AddRow(target, "Dedicated System Memory:", device.DedicatedSystemMemory.ToFormattedByteSize(), indent: 4);
            AddRow(target, "Shared System Memory:", device.SharedSystemMemory.ToFormattedByteSize(), indent: 4);
            AddRow(target, "Vendor / Device ID:", $"0x{device.VendorId:X4} / 0x{device.DeviceId:X4}", indent: 4);
            AddRow(target, "Driver Version:", OrNotAvailable(device.DriverVersion), indent: 4);
            AddRow(target, "Driver Date:", OrNotAvailable(device.DriverDate), indent: 4);
        }
    }

    private void AddDiskSection(ListView target, SystemSnapshot s)
    {
        if (s.Disk is not { } disk) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        DiskSpecs specs = disk.Specs;
        AddRow(target, "Installed Drives:", specs.Devices.Count.ToString());
        AddRow(target, "Total Capacity:", specs.ToDiskTotalCapacity().ToFormattedByteSize());

        foreach (DiskDevice device in specs.Devices) {
            string model = OrNotAvailable(device.Model);
            AddHeaderRow(target, $"DISK {device.Index}  {model}", indent: 2);

            AddRow(target, "Model:", model, indent: 4);
            AddRow(target, "Manufacturer:", OrNotAvailable(device.Manufacturer), indent: 4);
            AddRow(target, "Firmware Revision:", OrNotAvailable(device.FirmwareRevision), indent: 4);
            AddRow(target, "Serial Number:", OrNotAvailable(device.SerialNumber), indent: 4);
            AddRow(target, "Bus Type:", OrNotAvailable(device.BusType), indent: 4);
            AddRow(target, "Media Type:", OrNotAvailable(device.MediaType), indent: 4);
            AddRow(target, "Removable:", device.IsRemovable ? "Yes" : "No", indent: 4);
            AddRow(target, "Capacity:", device.Capacity.ToFormattedByteSize(), indent: 4);

            foreach (DiskVolume volume in device.Volumes) {
                AddVolumeRow(target, volume, indent: 6);
            }
        }

        if (specs.UnattachedVolumes.Count > 0) {
            AddHeaderRow(target, "UNATTACHED VOLUMES", indent: 2);

            foreach (DiskVolume volume in specs.UnattachedVolumes) {
                AddVolumeRow(target, volume, indent: 4);
            }
        }
    }

    private void AddNetworkSection(ListView target, SystemSnapshot s)
    {
        if (s.Network is not { } network) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        List<NetworkDevice> devices = network.Specs.Devices;
        AddRow(target, "Adapters:", devices.Count.ToString());

        foreach (NetworkDevice device in devices) {
            AddHeaderRow(target, device.ToDisplayName().ToUpper(), indent: 2);

            AddRow(target, "Name:", OrNotAvailable(device.Name), indent: 4);
            AddRow(target, "Description:", OrNotAvailable(device.Description), indent: 4);
            AddRow(target, "Connection Type:", OrNotAvailable(device.ConnectionType), indent: 4);
            AddRow(target, "Physical Medium:", OrNotAvailable(device.PhysicalMedium), indent: 4);
            AddRow(target, "MAC Address:", OrNotAvailable(device.MacAddress), indent: 4);
            AddRow(target, "IPv4 Addresses:", device.IPv4Addresses.ToAddressList(), indent: 4);
            AddRow(target, "IPv6 Addresses:", device.IPv6Addresses.ToAddressList(), indent: 4);
            AddRow(target, "Transmit Link Speed:", device.TransmitLinkSpeed.ToLinkSpeed(), indent: 4);
            AddRow(target, "Receive Link Speed:", device.ReceiveLinkSpeed.ToLinkSpeed(), indent: 4);
            AddRow(target, "Operational Status:", OrNotAvailable(device.OperationalStatus), indent: 4);
        }
    }

    // ---- Row helpers ----------------------------------------------------------------------------

    private void AddVolumeRow(ListView target, DiskVolume volume, int indent)
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

        AddRow(target, $"{name}:", detail.Length > 0 ? detail : NotAvailable, indent);
    }

    // A row styled in the theme header colours across both cells, so the trailing fill (drawn in
    // the last sub-item's background) carries the bar the full width of the list view.
    private void AddHeaderRow(ListView target, string title, int indent = 0)
    {
        ListViewItem row = new(new[] {
            new ListViewSubItem(null!, $"{new string(' ', indent)}{title}"),
            new ListViewSubItem(null!, string.Empty)
        });

        target.Items.Add(row);
    }

    // The label cell takes the theme's property.key colour and the value cell property.value. Both
    // keep the list view's own background so ListView still treats them as default cells and runs
    // the selection band through them.
    private void AddRow(ListView target, string label, string value, int indent = 0)
    {
        Color background = appConfig.Theme.Background;

        ListViewItem row = new(new[] {
            new ListViewSubItem(null!, $"{new string(' ', indent)}{label}", background, appConfig.Theme.PropertyKey),
            new ListViewSubItem(null!, value, background, appConfig.Theme.PropertyValue)
        });

        target.Items.Add(row);
    }

    private void AddRow(ListView target) =>
        target.Items.Add(new ListViewItem(new[] { string.Empty, string.Empty }));

    private static string OrNotAvailable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;
}
