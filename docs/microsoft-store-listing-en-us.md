# Cloud VM Manager for Windows | Store listing - English (United States)

## Product name

Cloud VM Manager for Windows

## Description

Cloud VM Manager for Windows is a desktop application that helps cloud administrators, cloud engineers, developers, and IT professionals manage virtual machines directly from Windows.

The app provides a simple Windows desktop experience for viewing and operating virtual machines across cloud subscriptions without opening a web portal.

Features include:

- Sign in with your cloud account
- View accessible subscriptions and virtual machines
- See VM power state and important VM details
- Start virtual machines
- Stop and deallocate virtual machines to help reduce compute costs
- Create local schedules to start or stop/deallocate VMs on selected days and times
- Use the system tray for quick access and VM actions
- View VM details such as resource group, region, size, OS, disks, zones, tags, and status
- Use a modern Windows desktop interface
- Authenticate securely through Microsoft identity services

Cloud VM Manager for Windows does not store cloud credentials. Authentication is handled through Microsoft identity services, and all resource operations depend on the permissions granted to the signed-in account.

Local schedules run only while the app is open or running in the system tray.

## What's new in this version

Initial Microsoft Store submission.

This version includes sign-in, subscription and VM listing, VM start and stop/deallocate actions, local VM schedules, system tray integration, VM details, and MSIX packaging.

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
