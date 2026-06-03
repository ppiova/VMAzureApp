# MSIX packaging

This repo includes an MSIX packaging project and a GitHub Actions workflow that builds a signed package for Windows.

## Release flow

Create and push a version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The `Build MSIX` workflow builds the package and uploads it to the GitHub Release.

## Installing a GitHub Release

Download `AzureVMManager-MSIX-<version>.zip` from the release, extract it, then run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Install-AzureVMManager.ps1
```

The script imports the included `.cer` certificate into `Cert:\CurrentUser\TrustedPeople` and then installs the `.msix` package.

If you install the `.msix` directly and see `0x800B010A`, Windows does not trust the signing certificate yet. Import the `.cer` first:

```powershell
Import-Certificate -FilePath .\VMAzureApp.Package_1.0.4.0_x64.cer -CertStoreLocation Cert:\CurrentUser\TrustedPeople
Add-AppxPackage .\VMAzureApp.Package_1.0.4.0_x64.msix
```

## Signing

MSIX packages must be signed. The workflow supports two modes:

1. If no secrets are configured, it creates a temporary self-signed certificate in CI. This is useful for testing, but users must trust the included `.cer` before installing the package.
2. For real distribution, configure these GitHub repository secrets:

| Secret | Description |
| --- | --- |
| `MSIX_PFX_BASE64` | Base64-encoded `.pfx` signing certificate. |
| `MSIX_PFX_PASSWORD` | Password for the `.pfx` certificate. |
| `MSIX_PUBLISHER` | Certificate subject, for example `CN=Your Company`. Must match the PFX subject. |

To convert a PFX to base64:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("path\to\certificate.pfx")) | Set-Clipboard
```

Never commit `.pfx` files or certificate passwords to the public repository.
