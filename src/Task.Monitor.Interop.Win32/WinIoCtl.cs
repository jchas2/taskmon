using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinIoCtl
{
    // CTL_CODE(DeviceType, Function, Method, Access) packs to
    // (DeviceType << 16) | (Access << 14) | (Function << 2) | Method.
    // All three below are METHOD_BUFFERED with FILE_ANY_ACCESS, which is what lets them run
    // against a handle opened with no access rights.
    public const uint IOCTL_STORAGE_QUERY_PROPERTY        = 0x002D1400;
    public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX    = 0x000700A0;
    public const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x00560000;

    public const uint StorageDeviceProperty                   = 0;
    public const uint StorageDeviceSeekPenaltyProperty        = 7;
    public const uint StorageDeviceTrimProperty               = 8;
    public const uint StorageAdapterProtocolSpecificProperty  = 49;
    public const uint StorageDeviceProtocolSpecificProperty   = 50;

    public const uint PropertyStandardQuery = 0;

    // STORAGE_PROTOCOL_TYPE
    public const uint ProtocolTypeAta  = 2;
    public const uint ProtocolTypeNvme = 3;

    // STORAGE_PROTOCOL_NVME_DATA_TYPE
    public const uint NVMeDataTypeIdentify = 1;
    public const uint NVMeDataTypeLogPage  = 2;

    // NVMe log page identifiers
    public const uint NVMeLogPageHealthInfo = 0x02;

    // STORAGE_PROTOCOL_SPECIFIC_DATA sits in the AdditionalParameters area of STORAGE_PROPERTY_QUERY
    // (offset 8) on the way in, and inside STORAGE_PROTOCOL_DATA_DESCRIPTOR (also offset 8) on the
    // way out. It is 40 bytes: 10 DWORDs.
    //   +0  ProtocolType          (STORAGE_PROTOCOL_TYPE)
    //   +4  DataType              (protocol-specific: NVMeDataTypeLogPage etc.)
    //   +8  ProtocolDataRequestValue      (log page id / CNS)
    //   +12 ProtocolDataRequestSubValue
    //   +16 ProtocolDataOffset    (bytes from the start of this struct to the returned data)
    //   +20 ProtocolDataLength
    //   +24 FixedProtocolReturnData
    //   +28 ProtocolDataRequestSubValue2
    //   +32 ProtocolDataRequestSubValue3
    //   +36 ProtocolDataRequestSubValue4
    public const int StoragePropertyQueryHeaderSize   = 8;   // PropertyId + QueryType
    public const int StorageProtocolSpecificDataSize  = 40;
    public const int StorageProtocolDataDescriptorHeaderSize = 8; // Version + Size

    public const int ProtocolSpecificProtocolTypeOffset  = 0;
    public const int ProtocolSpecificDataTypeOffset      = 4;
    public const int ProtocolSpecificRequestValueOffset  = 8;
    public const int ProtocolSpecificDataOffsetOffset    = 16;
    public const int ProtocolSpecificDataLengthOffset    = 20;

    // NVMe SMART / Health Information log page (log page 0x02), 512 bytes.
    //   +1..2   Composite Temperature      (uint16 LE, Kelvin)
    //   +200..  Temperature Sensor 1..8    (uint16 LE, Kelvin; 0 = not implemented)
    public const int NVMeHealthLogSize                   = 512;
    public const int NVMeHealthCompositeTemperatureOffset = 1;
    public const int NVMeHealthTemperatureSensorOffset    = 200;
    public const int NVMeHealthTemperatureSensorCount     = 8;

    // NVMe Identify Controller data (CNS 0x01), 4096 bytes.
    //   Power State Descriptors start at +2048, 32 bytes each (PSD0 is the peak-performance state).
    //   PSD:  +0..1 MP (Maximum Power)    +3 bit0 MXPS (0 => MP in 0.01 W, 1 => MP in 0.0001 W)
    public const int NVMeIdentifyControllerSize          = 4096;
    public const uint NVMeIdentifyCnsController          = 0x01;
    public const int NVMeIdentifyPowerStateDescriptorOffset = 2048;

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

    // STORAGE_DEVICE_DESCRIPTOR is variable length: a fixed header followed by a raw properties
    // area that VendorIdOffset and friends index into. It is read as a byte span rather than
    // marshalled, so the layout is expressed as offsets in the same style as the SMBIOS decode
    // in MemoryDeviceParser.
    public const int StorageDeviceDescriptorSizeOffset             = 0x04;
    public const int StorageDeviceDescriptorRemovableMediaOffset   = 0x0A;
    public const int StorageDeviceDescriptorVendorIdOffset         = 0x0C;
    public const int StorageDeviceDescriptorProductIdOffset        = 0x10;
    public const int StorageDeviceDescriptorProductRevisionOffset  = 0x14;
    public const int StorageDeviceDescriptorSerialNumberOffset     = 0x18;
    public const int StorageDeviceDescriptorBusTypeOffset          = 0x1C;
    public const int StorageDeviceDescriptorMinimumLength          = 0x24;

    // DISK_GEOMETRY_EX: DISK_GEOMETRY (24 bytes) then LARGE_INTEGER DiskSize, then a trailing
    // partition information blob whose size varies by partition style.
    public const int DiskGeometryExDiskSizeOffset = 0x18;
    public const int DiskGeometryExBufferSize     = 512;

    // VOLUME_DISK_EXTENTS: DWORD NumberOfDiskExtents then DISK_EXTENT Extents[1]. The extent
    // array is eight byte aligned because DISK_EXTENT leads with a DWORD followed by two
    // LARGE_INTEGERs.
    public const int VolumeDiskExtentsCountOffset      = 0x00;
    public const int VolumeDiskExtentsArrayOffset      = 0x08;
    public const int DiskExtentSize                    = 0x18;
    public const int DiskExtentDiskNumberOffset        = 0x00;
}
