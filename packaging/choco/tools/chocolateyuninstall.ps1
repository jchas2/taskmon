$ErrorActionPreference = 'Stop'

# taskmon.exe is extracted into the package's tools directory and shimmed by Chocolatey.
# Chocolatey removes the shim and the package files automatically on uninstall, so there
# is nothing extra to clean up here. This file exists to make the uninstall intent explicit.
