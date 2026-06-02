# MSIX packaging

This repo includes an MSIX packaging project and a GitHub Actions workflow that builds a signed package for Windows.

## Release flow

Create and push a version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The `Build MSIX` workflow builds the package and uploads it to the GitHub Release.

## Signing

MSIX packages must be signed. The workflow supports two modes:

1. If no secrets are configured, it creates a temporary self-signed certificate in CI. This is useful for testing, but users must trust the certificate before installing the package.
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
