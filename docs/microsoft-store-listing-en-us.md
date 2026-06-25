# Cloud VM Manager for Windows | Store listing - English (United States)

## Product name

Cloud VM Manager for Windows

## Description

Cloud VM Manager for Windows is a desktop application that helps cloud administrators, cloud engineers, developers, and IT professionals manage virtual machines directly from Windows.

The app provides a simple Windows desktop experience for viewing and operating virtual machines across cloud subscriptions without opening a web portal.

Features include:

- Sign in with your cloud account
- View accessible subscriptions and virtual machines, with the list reloading automatically when you switch subscription
- Search and filter VMs by name, resource group, region, size, OS, or status
- See VM power state and important VM details
- Start, stop/deallocate, restart, and hibernate virtual machines (hibernate where supported)
- Select multiple VMs and start or stop them in bulk
- Live status refresh after each action, plus an optional automatic refresh
- Create local schedules to start or stop/deallocate VMs on selected days and times
- Use the system tray for quick access and VM actions
- View VM details such as resource group, region, size, OS, disks, zones, tags, and status
- Use a modern Windows desktop interface
- Authenticate securely through Microsoft identity services

Cloud VM Manager for Windows does not store cloud credentials. Authentication is handled through Microsoft identity services, and all resource operations depend on the permissions granted to the signed-in account.

Local schedules run only while the app is open or running in the system tray.

## What's new in this version

Initial Microsoft Store submission.

This version includes sign-in, subscription and VM listing with automatic reload on subscription change, search and filtering, VM start, stop/deallocate, restart, and hibernate actions, multi-select bulk start/stop, live and automatic status refresh, local VM schedules, system tray integration, VM details, and MSIX packaging.

## Category

- Primary: Productivity
- Secondary: Utilities & tools

## Privacy policy URL

https://ppiova.github.io/VMAzureApp/privacy-policy/

(Enable GitHub Pages for this repository: Settings > Pages > Source: "Deploy from a branch" > Branch: `main`, folder `/docs`. The policy is published from `docs/privacy-policy.md`.)

## Product features

- Sign in with your cloud account
- View subscriptions and virtual machines
- Start virtual machines from Windows
- Stop and deallocate virtual machines
- Schedule VM start and stop/deallocate actions
- Access quick actions from the system tray
- View VM details, status, size, OS, disks, tags, and region
- Modern Windows desktop experience
- Secure authentication through Microsoft identity services

## Screenshots

Upload at least:

- `docs/store-assets/screenshot-1400x900.png`

Recommended additional screenshot ideas:

- Main VM list
- VM details panel
- Schedule panel
- System tray menu

## Store logos

Generated assets:

- 9:16 Poster art: `docs/store-assets/poster-art-720x1080.png`
- 1:1 Box art: `docs/store-assets/box-art-1080x1080.png`
- 1:1 App tile icon: `docs/store-assets/app-tile-300x300.png`

## Short title

Cloud VM Manager

## Voice title

Cloud VM Manager

## Short description

Manage cloud virtual machines from Windows. Sign in, view VMs across subscriptions, start or stop/deallocate machines, create local schedules, and access quick actions from the system tray.

## Keywords

- virtual machines
- cloud management
- VM scheduler
- system tray
- cloud admin
- desktop tools
- compute manager

## Copyright and trademark info

Copyright (c) 2026 Pablito Piova. Microsoft and related service names are trademarks of Microsoft Corporation.

## Additional license terms

This application is licensed under the MIT License and is provided as-is, without warranty of any kind. Users are responsible for ensuring they have appropriate Azure permissions and for any actions performed on their Azure resources.

## Developed by

Pablito Piova

## Notes for certification

This app uses the `runFullTrust` capability because it is a packaged desktop application built with WPF. It uses full trust to run as a standard desktop application, provide a system tray icon, and interact with the local user session. Cloud resource access is performed through Microsoft identity services and SDK APIs using the signed-in user's permissions.

### How to test (for the certification reviewer)

The core features require signing in to a Microsoft Entra (Azure) account that has at least one virtual machine.

1. Launch the app and click **Sign in / Reload**. Complete the Microsoft sign-in in the window that appears.
2. After sign-in, accessible subscriptions load and the virtual machines for the first subscription are listed. Switching the subscription in the dropdown reloads its VMs automatically.
3. Use the **Search** box to filter the list.
4. For a listed VM, use **Start**, **Stop**, **Restart**, or **Hibernate** (Hibernate appears only for VMs that have hibernation enabled). Select multiple VMs and use **Start selected** / **Stop selected** for bulk actions.
5. **Refresh status** re-queries power states; the **Auto** checkbox enables periodic refresh.

Reviewer access: please use a Microsoft account with an active Azure subscription containing at least one VM. If a demo account is required, the developer can provide test credentials on request through Partner Center.

Notes:
- Hibernate is offered only for VM sizes/configurations that support it; it is hidden otherwise.
- Without network access or a successful sign-in, the app shows an error message and remains usable (it does not crash); VM actions simply cannot be performed until sign-in succeeds.
- The app stores only local schedule data under `%AppData%\AzureVMManager`; it does not store credentials. See the privacy policy for details.
