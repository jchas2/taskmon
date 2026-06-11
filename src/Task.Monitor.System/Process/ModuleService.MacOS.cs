using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Process;

public partial class ModuleService : IModuleService
{
#if __APPLE__
    private bool GetModulesInternal(SysDiag::Process process, out List<ModuleInfo> moduleInfos)
    {
        moduleInfos = new List<ModuleInfo>();

        // Note: The below calls to the DyLib functions only work for the current running process.
        // Need an alternative way to get loaded modules for other MacOS processes.
        
        // uint imageCount = DyLib._dyld_image_count();
        //
        // for (uint i = 0; i < imageCount; i++) {
        //     IntPtr imagePathPtr = DyLib._dyld_get_image_name(i);
        //
        //     if (imagePathPtr == IntPtr.Zero) {
        //         continue;
        //     }
        //
        //     string? imagePath = Marshal.PtrToStringAnsi(imagePathPtr);
        //
        //     if (string.IsNullOrWhiteSpace(imagePath)) {
        //         continue;
        //     }
        //
        //     var moduleInfo = new ModuleInfo() {
        //         ModuleName = Path.GetFileName(imagePath),
        //         FileName = imagePath
        //     };
        //     
        //     moduleInfos.Add(moduleInfo);
        // }

        return moduleInfos.Count > 0;
    }
#endif
}