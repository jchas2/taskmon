namespace Task.Monitor.Interop.Win32;

public static class IpTypes
{
    // IP_ADAPTER_ADDRESSES_LH is a linked list node, and its layout is version dependent past the fields below.
    // It is walked as a byte span using offsets as COM Marshalling is not supported in .net native AoT.
    public const int AdapterAddressesIfIndexOffset              = 0x04;
    public const int AdapterAddressesNextOffset                 = 0x08;
    public const int AdapterAddressesAdapterNameOffset          = 0x10;
    public const int AdapterAddressesFirstUnicastAddressOffset  = 0x18;
    public const int AdapterAddressesDescriptionOffset          = 0x40;
    public const int AdapterAddressesFriendlyNameOffset         = 0x48;
    public const int AdapterAddressesPhysicalAddressOffset      = 0x50;
    public const int AdapterAddressesPhysicalAddressLenOffset   = 0x58;
    public const int AdapterAddressesIfTypeOffset               = 0x64;
    public const int AdapterAddressesOperStatusOffset           = 0x68;
    public const int AdapterAddressesTransmitLinkSpeedOffset    = 0xB8;
    public const int AdapterAddressesReceiveLinkSpeedOffset     = 0xC0;
    public const int AdapterAddressesLuidOffset                 = 0xE0;
    public const int AdapterAddressesMinimumLength              = 0xE8;

    public const int MaxAdapterAddressLength = 8;

    // IP_ADAPTER_UNICAST_ADDRESS_LH
    public const int UnicastAddressNextOffset    = 0x08;
    public const int UnicastAddressAddressOffset = 0x10;
    public const int UnicastAddressMinimumLength = 0x20;
}
