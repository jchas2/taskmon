using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

using size_t = nint;

public static unsafe class Sys
{
    public enum Selectors
    {
        CTL_KERN = 0x01,            /* Kernel */
        CTL_VM   = 0x02,            /* Virtual Memory */
        CTL_NET  = 0x04,            /* Network */
        CTL_HW   = 0x06,            /* Hardware */
    }

    public enum Hardware
    {
        HW_MODEL    = 0x02,         /* Cpu model */
        HW_PAGESIZE = 0x07,         /* Mem Page Size */
        HW_CPU_FREQ = 0x08,         /* Cpu frequency in Hz, does not work on Apple Silicon Mn chips */
        HW_MEMSIZE  = 0x18,         /* Memory size in bytes */
    }

    public enum NetRouting
    {
        NET_RT_IFLIST  = 0x03,      /* Get interface list */
        NET_RT_IFLIST2 = 0x06,      /* Enhanced interface list (macOS specific) */
    }

    public const int VM_SWAPUSAGE = 5;
    public const int PF_ROUTE     = 17;     /* Protocol family for routing */
    public const int AF_UNSPEC    = 0;      /* Address family unspecified */
    public const int RTM_IFINFO2  = 0x12;   /* Interface info message type */

    public const int KERN_ARGMAX    = 8;    /* Max bytes of exec arguments */
    public const int KERN_PROCARGS2 = 49;   /* A process's argc, exec path and arguments */

    public const int ENOMEM = 12;           /* Not enough space */

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
        public byte  ifi_type;          /* Ethernet, etc. */
        public byte  ifi_typelen;
        public byte  ifi_physical;
        public byte  ifi_addrlen;
        public byte  ifi_hdrlen;
        public byte  ifi_recvquota;
        public byte  ifi_xmitquota;
        public byte  ifi_unused1;
        public uint  ifi_mtu;           /* Maximum transmission unit */
        public uint  ifi_metric;
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
        public uint  ifi_recvtiming;
        public uint  ifi_xmittiming;
        public ulong ifi_lastchange_tv_sec;
        public uint  ifi_lastchange_tv_usec;
        public uint  ifi_unused2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct if_msghdr2
    {
        public ushort    ifm_msglen;       /* Message length */
        public byte      ifm_version;
        public byte      ifm_type;         /* RTM_IFINFO2 */
        public int       ifm_addrs;        /* Address mask */
        public int       ifm_flags;        /* Interface flags */
        public ushort    ifm_index;        /* Interface index */
        public int       ifm_snd_len;
        public int       ifm_snd_maxlen;
        public int       ifm_snd_drops;
        public int       ifm_timer;
        public if_data64 ifm_data;         /* Interface statistics */
    }

    [DllImport(Libraries.LibC)]
    public static extern int getloadavg([Out] double[] loadavg, int nelem);

    [DllImport(Libraries.LibSystemNative, EntryPoint = "SystemNative_Sysctl", SetLastError = true)]
    public static extern int Sysctl(
        int*    name,
        int     namelen,
        void*   value,
        size_t* len);

    [DllImport(Libraries.LibC, EntryPoint = "sysctlbyname", SetLastError = true)]
    public static extern int SysctlByName(
        [MarshalAs(UnmanagedType.LPStr)]
        string  name,
        void*   oldp,
        size_t* oldlenp,
        void*   newp,
        size_t  newlen);
}
