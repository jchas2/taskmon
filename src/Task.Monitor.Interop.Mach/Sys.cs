using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

using size_t = System.IntPtr;

public static unsafe class Sys
{
    public enum Selectors
    {
        CTL_KERN = 0x01,            /* Kernel */
        CTL_VM = 0x02,              /* Virtual Memory */
        CTL_NET = 0x04,             /* Network */
        CTL_HW = 0x06,              /* Hardware */
    }
    
    public enum Hardware
    {
        HW_MODEL = 0x02,            /* Cpu model */
        HW_PAGESIZE = 0x07,         /* Mem Page Size */
        HW_CPU_FREQ = 0x08,         /* Cpu frequency in Hz, does not work on Apple Silicon Mn chips */
        HW_MEMSIZE = 0x18,          /* Memory size in bytes */
    }

    public enum NetRouting
    {
        NET_RT_IFLIST = 0x03,       /* Get interface list */
        NET_RT_IFLIST2 = 0x06,      /* Enhanced interface list (macOS specific) */
    }
    
    public const int VM_SWAPUSAGE = 5;
    public const int PF_ROUTE = 17;     /* Protocol family for routing */
    public const int AF_UNSPEC = 0;     /* Address family unspecified */
    public const int RTM_IFINFO2 = 0x12; /* Interface info message type */
    
    private enum Error
    {
        ENOMEM = 0x10031,           /* Not enough space */
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XswUsage
    {
        public ulong total;
        public ulong avail;
        public ulong used;
        public uint  pagesize;
        public uint  encrypted;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct if_data64
    {
        public byte ifi_type;           /* Ethernet, etc. */
        public byte ifi_typelen;
        public byte ifi_physical;
        public byte ifi_addrlen;
        public byte ifi_hdrlen;
        public byte ifi_recvquota;
        public byte ifi_xmitquota;
        public byte ifi_unused1;
        public uint ifi_mtu;            /* Maximum transmission unit */
        public uint ifi_metric;
        public ulong ifi_baudrate;      /* Link speed */
        public ulong ifi_ipackets;      /* Packets received */
        public ulong ifi_ierrors;       /* Input errors */
        public ulong ifi_opackets;      /* Packets sent */
        public ulong ifi_oerrors;       /* Output errors */
        public ulong ifi_collisions;
        public ulong ifi_ibytes;        /* Bytes received */
        public ulong ifi_obytes;        /* Bytes sent */
        public ulong ifi_imcasts;
        public ulong ifi_omcasts;
        public ulong ifi_iqdrops;
        public ulong ifi_noproto;
        public uint ifi_recvtiming;
        public uint ifi_xmittiming;
        public ulong ifi_lastchange_tv_sec;
        public uint ifi_lastchange_tv_usec;
        public uint ifi_unused2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct if_msghdr2
    {
        public ushort ifm_msglen;       /* Message length */
        public byte ifm_version;
        public byte ifm_type;           /* RTM_IFINFO2 */
        public int ifm_addrs;           /* Address mask */
        public int ifm_flags;           /* Interface flags */
        public ushort ifm_index;        /* Interface index */
        public int ifm_snd_len;
        public int ifm_snd_maxlen;
        public int ifm_snd_drops;
        public int ifm_timer;
        public if_data64 ifm_data;      /* Interface statistics */
    }
    
    [DllImport(Libraries.LibSystemNative, EntryPoint = "SystemNative_Sysctl", SetLastError = true)]
    private static extern unsafe int Sysctl(
        int* name,
        int namelen,
        void* value,
        size_t* len);

    [DllImport(Libraries.LibSystemNative, EntryPoint = "SystemNative_Free")]
    private static extern unsafe void Free(void* ptr);

    [DllImport(Libraries.LibC, EntryPoint = "sysctlbyname", SetLastError = true)]
    private static extern unsafe int SysctlByName(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        void* oldp,
        size_t* oldlenp,
        void* newp,
        size_t newlen);

    // Reads a string-valued sysctl by name (e.g. "machdep.cpu.brand_string").
    public static unsafe string? SysctlByNameString(string name)
    {
        size_t len = IntPtr.Zero;

        if (SysctlByName(name, null, &len, null, IntPtr.Zero) != 0) {
            return null;
        }

        int byteLen = (int)len;

        if (byteLen <= 0) {
            return null;
        }

        byte* buffer = (byte*)Marshal.AllocHGlobal(byteLen);

        try {
            size_t outLen = (IntPtr)byteLen;

            if (SysctlByName(name, buffer, &outLen, null, IntPtr.Zero) != 0) {
                return null;
            }

            int strLen = (int)outLen;

            // sysctl strings are null terminated; drop the terminator.
            if (strLen > 0 && buffer[strLen - 1] == 0) {
                strLen--;
            }

            return System.Text.Encoding.UTF8.GetString(buffer, strLen);
        }
        finally {
            Marshal.FreeHGlobal((IntPtr)buffer);
        }
    }

    // Reads a 32-bit integer-valued sysctl by name (e.g. "hw.nperflevels").
    public static unsafe bool SysctlByNameInt(string name, out int value)
    {
        value = 0;
        int result = 0;
        size_t len = (IntPtr)sizeof(int);

        if (SysctlByName(name, &result, &len, null, IntPtr.Zero) != 0) {
            return false;
        }

        value = result;
        return true;
    }
    
    public static unsafe void FreeMemory(void* ptr)
    {
        if ((IntPtr)ptr == IntPtr.Zero) {
            return;
        }

        Sys.Free(ptr);
    }
    
    public static unsafe bool Sysctl(ReadOnlySpan<int> name, ref byte* value, ref int len)
    {
        fixed (int* ptr = &MemoryMarshal.GetReference(name)) {
            return Sysctl(ptr, name.Length, ref value, ref len);
        }
    }
    
    private static unsafe bool Sysctl(int* name, int name_len, ref byte* value, ref int len)
    {
        nint bytesLength = len;
        int ret = -1;
        bool autoSize = (value == null && len == 0);

        if (autoSize) {
            ret = Sysctl(name, name_len, value, &bytesLength);
            
            if (ret != 0) {
                return false;
            }
            
            value = (byte*)Marshal.AllocHGlobal((int)bytesLength);
        }

        ret = Sysctl(
            name, 
            name_len, 
            value, 
            &bytesLength);
        
        int lastError = Marshal.GetLastPInvokeError();
        
        while (autoSize && ret != 0 && lastError == (int)Error.ENOMEM)
        {
            Marshal.FreeHGlobal((IntPtr)value);
            
            if ((int)bytesLength == int.MaxValue) {
                throw new OutOfMemoryException();
            }
            
            if ((int)bytesLength >= int.MaxValue / 2) {
                bytesLength = int.MaxValue;
            } 
            else {
                bytesLength = (int)bytesLength * 2;
            }
            
            value = (byte*)Marshal.AllocHGlobal(bytesLength);
            ret = Sysctl(name, name_len, value, &bytesLength);
        }
        
        if (ret != 0) {
            if (autoSize) {
                Marshal.FreeHGlobal((IntPtr)value);
            }
            
            return false;
        }

        len = (int)bytesLength;

        return true;
    }
}
