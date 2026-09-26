using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinIoCtl
{
    public const uint IOCTL_STORAGE_QUERY_PROPERTY         = 0x002D1400;
    public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX     = 0x000700A0;
    public const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x00560000;

    public const uint StorageDeviceProperty                   = 0;
    public const uint StorageDeviceSeekPenaltyProperty        = 7;
    public const uint StorageDeviceTrimProperty               = 8;
    public const uint StorageAdapterProtocolSpecificProperty  = 49;
    public const uint StorageDeviceProtocolSpecificProperty   = 50;

    public const uint PropertyStandardQuery = 0;

    public const uint ProtocolTypeAta  = 2;
    public const uint ProtocolTypeNvme = 3;

    public const uint NVMeDataTypeIdentify = 1;
    public const uint NVMeDataTypeLogPage  = 2;

    public const uint NVMeLogPageHealthInfo = 0x02;

    // STORAGE_PROTOCOL_SPECIFIC_DATA, 40 bytes:
    //   +0  ProtocolType (STORAGE_PROTOCOL_TYPE)
    //   +4  DataType
    //   +8  ProtocolDataRequestValue
    //   +12 ProtocolDataRequestSubValue
    //   +16 ProtocolDataOffset
    //   +20 ProtocolDataLength
    //   +24 FixedProtocolReturnData
    //   +28 ProtocolDataRequestSubValue2
    //   +32 ProtocolDataRequestSubValue3
    //   +36 ProtocolDataRequestSubValue4
    public const int StoragePropertyQueryHeaderSize   = 8;
    public const int StorageProtocolSpecificDataSize  = 40;
    public const int StorageProtocolDataDescriptorHeaderSize = 8;

    public const int ProtocolSpecificProtocolTypeOffset  = 0;
    public const int ProtocolSpecificDataTypeOffset      = 4;
    public const int ProtocolSpecificRequestValueOffset  = 8;
    public const int ProtocolSpecificDataOffsetOffset    = 16;
    public const int ProtocolSpecificDataLengthOffset    = 20;

    // NVMe SMART / Health Information log page (log page 0x02), 512 bytes.
    public const int NVMeHealthLogSize                    = 512;
    public const int NVMeHealthCompositeTemperatureOffset = 1;
    public const int NVMeHealthTemperatureSensorOffset    = 200;
    public const int NVMeHealthTemperatureSensorCount     = 8;

    // NVMe Identify Controller data (CNS 0x01), 4096 bytes.
    public const int  NVMeIdentifyControllerSize         = 4096;
    public const uint NVMeIdentifyCnsController          = 0x01;
    public const int  NVMeIdentifyPowerStateDescriptorOffset = 2048;

    public const uint BusTypeUnknown           = 0x00;
    public const uint BusTypeScsi              = 0x01;
    public const uint BusTypeAtapi             = 0x02;
    public const uint BusTypeAta               = 0x03;
    public const uint BusType1394              = 0x04;
    public const uint BusTypeSsa               = 0x05;
    public const uint BusTypeFibre             = 0x06;
    public const uint BusTypeUsb               = 0x07;
    public const uint BusTypeRAID              = 0x08;
    public const uint BusTypeiScsi             = 0x09;
    public const uint BusTypeSas               = 0x0A;
    public const uint BusTypeSata              = 0x0B;
    public const uint BusTypeSd                = 0x0C;
    public const uint BusTypeMmc               = 0x0D;
    public const uint BusTypeVirtual           = 0x0E;
    public const uint BusTypeFileBackedVirtual = 0x0F;
    public const uint BusTypeSpaces            = 0x10;
    public const uint BusTypeNvme              = 0x11;
    public const uint BusTypeSCM               = 0x12;
    public const uint BusTypeUfs               = 0x13;

    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_PROPERTY_QUERY
    {
        public uint PropertyId;
        public uint QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_DESCRIPTOR_HEADER
    {
        public uint Version;
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        public byte IncursSeekPenalty;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVICE_TRIM_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        public byte TrimEnabled;
    }

    // STORAGE_DEVICE_DESCRIPTOR is variable length, the layout is expressed as
    // offsets in the same style as the SMBIOS decode in MemoryDeviceParser.
    public const int StorageDeviceDescriptorSizeOffset             = 0x04;
    public const int StorageDeviceDescriptorRemovableMediaOffset   = 0x0A;
    public const int StorageDeviceDescriptorVendorIdOffset         = 0x0C;
    public const int StorageDeviceDescriptorProductIdOffset        = 0x10;
    public const int StorageDeviceDescriptorProductRevisionOffset  = 0x14;
    public const int StorageDeviceDescriptorSerialNumberOffset     = 0x18;
    public const int StorageDeviceDescriptorBusTypeOffset          = 0x1C;
    public const int StorageDeviceDescriptorMinimumLength          = 0x24;

    // DISK_GEOMETRY_EX, 24 bytes.
    public const int DiskGeometryExDiskSizeOffset = 0x18;
    public const int DiskGeometryExBufferSize     = 512;

    // VOLUME_DISK_EXTENTS: DWORD NumberOfDiskExtents then DISK_EXTENT Extents[1]. The extent
    // array is eight byte aligned.
    public const int VolumeDiskExtentsCountOffset      = 0x00;
    public const int VolumeDiskExtentsArrayOffset      = 0x08;
    public const int DiskExtentSize                    = 0x18;
    public const int DiskExtentDiskNumberOffset        = 0x00;
}
