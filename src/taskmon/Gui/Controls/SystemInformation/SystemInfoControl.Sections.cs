using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
#if __APPLE__
using Task.Monitor.System.Services.Npu;
#endif

namespace Task.Monitor.Gui.Controls.SystemInformation;

public sealed partial class SystemInfoControl
{
    private void AddCpuSection(ListView target, SystemSnapshot s)
    {
        if (s.Cpu is not { } cpu) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        CpuSpecs specs = cpu.Specs;

        AddRow(target, Strings.SystemInfoProcessor,          OrNotAvailable(specs.CpuName));
        AddRow(target, Strings.SystemInfoLogicalProcessors, specs.CpuCores.ToString());
        AddRow(target, Strings.SystemInfoBaseFrequency,     specs.ToCpuFrequencyGhz());
#if __APPLE__
        AddRow(target, Strings.SystemInfoPerformanceCores, string.Format(CultureInfo.CurrentCulture, Strings.SystemInfoCoresAtFrequency, specs.CpuPerformanceCores, specs.CpuPerformanceFrequency / 1000.0));
        AddRow(target, Strings.SystemInfoEfficiencyCores, string.Format(CultureInfo.CurrentCulture, Strings.SystemInfoCoresAtFrequency, specs.CpuEfficiencyCores, specs.CpuEfficiencyFrequency / 1000.0));
#endif
        AddRow(target, Strings.SystemInfoL1Cache,           specs.ToCpuL1Cache());
        AddRow(target, Strings.SystemInfoL2Cache,           specs.ToCpuL2Cache());
        AddRow(target, Strings.SystemInfoL3Cache,           specs.ToCpuL3Cache());
        AddRow(target, Strings.LabelVirtualisation,     specs.ToCpuVirtualization());
    }

    private void AddDiskSection(ListView target, SystemSnapshot s)
    {
        if (s.Disk is not { } disk) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        DiskSpecs specs = disk.Specs;
        AddRow(target, Strings.SystemInfoInstalledDrives, specs.Devices.Count.ToString());
        AddRow(target, Strings.LabelTotalCapacity,   specs.ToDiskTotalCapacity().ToFormattedByteSize());

        foreach (DiskDevice device in specs.Devices) {
            string model = OrNotAvailable(device.Model);
            AddHeaderRow(target, string.Format(CultureInfo.CurrentCulture, Strings.SystemInfoDiskHeader, device.Index, model), indent: 2);

            AddRow(target, Strings.LabelModel,             model,                                   indent: 4);
            AddRow(target, Strings.LabelManufacturer,      OrNotAvailable(device.Manufacturer),     indent: 4);
            AddRow(target, Strings.LabelFirmwareRevision, OrNotAvailable(device.FirmwareRevision), indent: 4);
            AddRow(target, Strings.LabelSerialNumber,     OrNotAvailable(device.SerialNumber),     indent: 4);
            AddRow(target, Strings.LabelBusType,          OrNotAvailable(device.BusType),          indent: 4);
            AddRow(target, Strings.LabelMediaType,        OrNotAvailable(device.MediaType),        indent: 4);
            AddRow(target, Strings.LabelRemovable,         device.IsRemovable ? Strings.CommonYes : Strings.CommonNo,       indent: 4);
            AddRow(target, Strings.LabelCapacity,          device.Capacity.ToFormattedByteSize(),   indent: 4);

            foreach (DiskVolume volume in device.Volumes) {
                AddVolumeRow(target, volume, indent: 6);
            }
        }

        if (specs.UnattachedVolumes.Count > 0) {
            AddHeaderRow(target, Strings.SystemInfoUnattachedVolumes, indent: 2);

            foreach (DiskVolume volume in specs.UnattachedVolumes) {
                AddVolumeRow(target, volume, indent: 4);
            }
        }
    }

    private void AddGpuSection(ListView target, SystemSnapshot s)
    {
        if (s.Gpu is not { } gpu) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        List<GpuDevice> devices = gpu.Specs.Devices;
        AddRow(target, Strings.SystemInfoInstalledAdapters,     devices.Count.ToString());
        AddRow(target, Strings.SystemInfoTotalDedicatedMemory, gpu.Specs.TotalGpuMemory.ToFormattedByteSize());

#if __APPLE__
        string cores = gpu.Specs.GpuCores > 0 ? gpu.Specs.GpuCores.ToString() : NotAvailable;
#endif

        foreach (GpuDevice device in devices) {
            AddHeaderRow(target, string.Format(CultureInfo.CurrentCulture, Strings.SystemInfoGpuHeader, device.Index, OrNotAvailable(device.Description)), indent: 2);

            AddRow(target, Strings.LabelVendor,                  OrNotAvailable(device.Vendor),                       indent: 4);
            AddRow(target, Strings.LabelDescription,             OrNotAvailable(device.Description),                  indent: 4);
            AddRow(target, Strings.LabelAdapterType,            OrNotAvailable(device.AdapterType),                  indent: 4);
#if __APPLE__
            AddRow(target, Strings.LabelCores,                   cores,                                               indent: 4);
#endif
            AddRow(target, Strings.SystemInfoDedicatedVideoMemory,  device.DedicatedVideoMemory.ToFormattedByteSize(),   indent: 4);
            AddRow(target, Strings.SystemInfoDedicatedSystemMemory, device.DedicatedSystemMemory.ToFormattedByteSize(),  indent: 4);
            AddRow(target, Strings.SystemInfoSharedSystemMemory,    device.SharedSystemMemory.ToFormattedByteSize(),     indent: 4);
            AddRow(target, Strings.SystemInfoVendorDeviceId,      $"0x{device.VendorId:X4} / 0x{device.DeviceId:X4}",  indent: 4);
            AddRow(target, Strings.LabelDriverVersion,          OrNotAvailable(device.DriverVersion),                indent: 4);
            AddRow(target, Strings.LabelDriverDate,             OrNotAvailable(device.DriverDate),                   indent: 4);
        }
    }

    private void AddHeaderRow(ListView target, string title, int indent = 0)
    {
        ListViewItem row = new(new[] {
            new ListViewSubItem(null!, $"{new string(' ', indent)}{title}"),
            new ListViewSubItem(null!, string.Empty)
        });

        target.Items.Add(row);
    }

    private void AddMemorySection(ListView target, SystemSnapshot s)
    {
        if (s.Memory is not { } memory) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        List<MemoryDevice> devices = memory.Specs.Devices;
        AddRow(target, Strings.SystemInfoInstalledModules, devices.Count(device => device.SizeInMegabytes > 0).ToString());

        foreach (MemoryDevice device in devices) {
            AddHeaderRow(target, string.Format(CultureInfo.CurrentCulture, Strings.SystemInfoSlotHeader, device.Slot, OrNotAvailable(device.DeviceLocator)), indent: 2);

            AddRow(target, Strings.LabelCapacity,         device.ToMemoryCapacity(),             indent: 4);
            AddRow(target, Strings.SystemInfoType,             OrNotAvailable(device.MemoryType),     indent: 4);
            AddRow(target, Strings.SystemInfoFormFactor,      OrNotAvailable(device.FormFactor),     indent: 4);
            AddRow(target, Strings.LabelSpeed,            string.Format(CultureInfo.CurrentCulture, Strings.SystemInfoTransferRate, device.Speed),                indent: 4);
            AddRow(target, Strings.SystemInfoConfiguredSpeed, string.Format(CultureInfo.CurrentCulture, Strings.SystemInfoTransferRate, device.ConfiguredClockSpeed), indent: 4);
            AddRow(target, Strings.LabelManufacturer,     OrNotAvailable(device.Manufacturer),   indent: 4);
            AddRow(target, Strings.SystemInfoPartNumber,      OrNotAvailable(device.PartNumber),     indent: 4);
            AddRow(target, Strings.LabelSerialNumber,    OrNotAvailable(device.SerialNumber),   indent: 4);
            AddRow(target, Strings.SystemInfoBankLocator,     OrNotAvailable(device.BankLocator),    indent: 4);
        }
    }

    private void AddNetworkSection(ListView target, SystemSnapshot s)
    {
        if (s.Network is not { } network) {
            AddRow(target, string.Empty, GatheringText);
            return;
        }

        List<NetworkDevice> devices = network.Specs.Devices;
        AddRow(target, Strings.SystemInfoAdapters, devices.Count.ToString());

        foreach (NetworkDevice device in devices) {
            AddHeaderRow(target, device.ToDisplayName().ToUpper(), indent: 2);

            AddRow(target, Strings.LabelName,                OrNotAvailable(device.Name), indent: 4);
            AddRow(target, Strings.LabelDescription,         OrNotAvailable(device.Description), indent: 4);
            AddRow(target, Strings.LabelConnectionType,     OrNotAvailable(device.ConnectionType), indent: 4);
            AddRow(target, Strings.LabelPhysicalMedium,     OrNotAvailable(device.PhysicalMedium), indent: 4);
            AddRow(target, Strings.LabelMacAddress,         OrNotAvailable(device.MacAddress), indent: 4);
            AddRow(target, Strings.LabelIpv4Addresses,      device.IPv4Addresses.ToAddressList(), indent: 4);
            AddRow(target, Strings.LabelIpv6Addresses,      device.IPv6Addresses.ToAddressList(), indent: 4);
            AddRow(target, Strings.LabelTransmitLinkSpeed, device.TransmitLinkSpeed.ToLinkSpeed(), indent: 4);
            AddRow(target, Strings.LabelReceiveLinkSpeed,  device.ReceiveLinkSpeed.ToLinkSpeed(), indent: 4);
            AddRow(target, Strings.LabelOperationalStatus,  OrNotAvailable(device.OperationalStatus), indent: 4);
        }
    }

#if __APPLE__
    private void AddNpuSection(ListView target, SystemSnapshot s)
    {
        if (s.Npu is not { } npu) {
            AddRow(target, string.Empty, s.Soc is null ? GatheringText : NoNeuralEngineText);
            return;
        }

        List<NpuDevice> devices = npu.Specs.Devices;
        AddRow(target, Strings.SystemInfoNeuralEngines, devices.Count.ToString());

        foreach (NpuDevice device in devices) {
            AddHeaderRow(target, device.ToDisplayName(), indent: 2);

            AddRow(target, Strings.LabelDescription,  OrNotAvailable(device.Description),   indent: 4);
            AddRow(target, Strings.LabelVendor,       OrNotAvailable(device.Vendor),        indent: 4);
            AddRow(target, Strings.LabelChip,         OrNotAvailable(s.Cpu?.Specs.CpuName), indent: 4);
            AddRow(target, Strings.LabelCores,        device.Cores.ToString(),              indent: 4);
            AddRow(target, Strings.LabelArchitecture, OrNotAvailable(device.Architecture),  indent: 4);
            AddRow(target, Strings.LabelVersion,      OrNotAvailable(device.Version),       indent: 4);
        }
    }
#endif

    private void AddRow(
        ListView target,
        string   label,
        string   value,
        int      indent = 0) =>
        target.Items.Add(CommonControl.PropertyRow($"{new string(' ', indent)}{label}", value, appConfig.Theme));

    private void AddRow(ListView target) =>
        target.Items.Add(new ListViewItem(new[] { string.Empty, string.Empty }));

    private void AddSystemSection(ListView target, SystemSnapshot s)
    {
        AddRow(target, Strings.SystemInfoMachineName,      Environment.MachineName.ToUpper());
        AddRow(target, Strings.SystemInfoOperatingSystem,  SystemInfo.GetOsVersion());
        AddRow(target, Strings.LabelVersion,           Environment.OSVersion.Version.ToString());
        AddRow(target);
        AddRow(target, Strings.SystemInfoCpu,               s.Cpu?.Specs.CpuName ?? string.Empty);
        AddRow(target, Strings.SystemInfoSummaryLogicalProcessors, s.Cpu?.Specs.CpuCores.ToString() ?? string.Empty);
        AddRow(target, Strings.SystemInfoBaseSpeed,        s.Cpu?.Specs.ToCpuFrequencyGhz() ?? string.Empty);
        AddRow(target);
        AddRow(target, Strings.LabelMemory,            s.Memory?.Metrics.TotalPhysical.ToFormattedByteSize() ?? string.Empty);
    }

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

#if __APPLE__
    private static string NoNeuralEngineText => Strings.SystemInfoNoNeuralEngine;
#endif

    private static string NotAvailable => Strings.CommonNotAvailable;

    private static string OrNotAvailable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private void RebuildRows(SystemSnapshot s)
    {
        systemInfoView.Items.Clear();

        switch (selectedSection) {
            case Section.Cpu:     AddCpuSection(systemInfoView, s);     break;
            case Section.Memory:  AddMemorySection(systemInfoView, s);  break;
            case Section.Gpu:     AddGpuSection(systemInfoView, s);     break;
#if __APPLE__
            case Section.Npu:     AddNpuSection(systemInfoView, s);     break;
#endif
            case Section.Disk:    AddDiskSection(systemInfoView, s);    break;
            case Section.Network: AddNetworkSection(systemInfoView, s); break;
        }
    }

    private void RebuildSummaryRows(SystemSnapshot s)
    {
        systemSummaryView.Items.Clear();
        AddSystemSection(systemSummaryView, s);
    }
}
