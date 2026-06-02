# Azure VM Manager

Azure VM Manager is a Windows desktop app for managing Azure virtual machines across subscriptions without opening the Azure portal.

## Features

- Interactive Microsoft Entra sign-in.
- Lists accessible Azure subscriptions.
- Lists virtual machines and power states.
- Start VMs quickly.
- Stop and deallocate VMs to reduce compute costs.
- Local schedules to start or stop/deallocate VMs on selected days and times.
- VM details panel with subscription, resource group, region, size, OS, disks, zones, tags, and status.
- Modern WPF UI.
- System tray icon with quick actions.
- MSIX packaging workflow for GitHub Releases.

## Requirements

- Windows 10 or later.
- .NET 10 SDK for development.
- Azure permissions to read and operate virtual machines in the target subscriptions.

## Run locally

```powershell
dotnet run --project .\VMAzureApp\VMAzureApp.csproj
```

## Build

```powershell
dotnet build .\VMAzureApp\VMAzureApp.csproj
```

## Scheduling notes

Schedules are stored locally under the user's application data folder and run in local machine time. The app must be open or running in the system tray for scheduled VM actions to execute.

## MSIX releases

This repository includes a Windows Application Packaging Project and a GitHub Actions workflow that builds an MSIX package when a version tag is pushed:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

See [PACKAGING.md](PACKAGING.md) for signing certificate and GitHub Secrets setup.

## Built with GitHub Copilot

This project was built with [GitHub Copilot](https://github.com/features/copilot), a developer tool for AI-assisted coding and vibe coding workflows.

## License

This project is licensed under the [MIT License](LICENSE). You can use, modify, and improve it freely. The software is provided as-is, without warranty or liability for misuse.
