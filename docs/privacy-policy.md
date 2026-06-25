---
title: Privacy Policy
permalink: /privacy-policy/
---

# Privacy Policy — Cloud VM Manager for Windows

_Last updated: 25 June 2026_

Cloud VM Manager for Windows ("the app") is a desktop application developed by Pablito Piova that lets you view and operate your Microsoft Azure virtual machines from Windows. This policy explains what the app accesses, what it stores, and what it does not do.

## Summary

- The app does **not** collect, transmit, or sell your personal data to the developer or any third party.
- The app has **no analytics, advertising, or tracking**.
- Authentication is handled by **Microsoft identity services**; the app never sees or stores your password.
- The only data the app stores is kept **locally on your own computer**.

## Authentication and Azure access

To work with your virtual machines, the app signs you in interactively through **Microsoft Entra ID** using Microsoft's official identity libraries (MSAL). Sign-in happens in a Microsoft-hosted window. The app:

- never receives, handles, or stores your account password;
- receives only the access tokens issued by Microsoft, which are managed by the Microsoft identity libraries and the operating system token cache, not by the developer;
- performs every Azure operation using **your own permissions** on your own subscriptions.

## What the app reads

While signed in, the app reads, from the Azure Resource Manager APIs, the information needed to display and manage your VMs, including:

- the subscriptions your account can access (names and IDs);
- virtual machine names, resource groups, regions, sizes, OS type, disks, zones, tags, and power state.

This information is shown to you in the app. It is **not** sent to the developer or any third party.

## What the app does

At your request, the app performs standard VM power operations on your behalf: **start, stop/deallocate, restart, and hibernate** (hibernate only where the VM supports it). These actions run against your Azure resources using your signed-in permissions.

## What the app stores locally

The app stores a small amount of data **only on your computer**, under your user profile (`%AppData%\AzureVMManager`):

- the local schedules you create, including the target VM and its identifying details (name, subscription name, resource group, and the Azure resource ID, which contains your subscription ID), the action, the selected days and time, whether the schedule is enabled, and the time and result of the last run.

These files never leave your machine. You can delete them at any time by removing that folder.

## What the app does not do

- It does not store your Azure or Microsoft account credentials.
- It does not send telemetry, usage analytics, or crash reports anywhere.
- It does not share any data with the developer or third parties.
- It does not run background services outside the app itself; local schedules only run while the app is open or in the system tray.

## Third-party services

The app communicates only with **Microsoft services** (Microsoft Entra ID for sign-in and Azure Resource Manager for VM operations), under your account. Your use of those services is governed by the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).

## Changes to this policy

If this policy changes, the updated version will be published at this page with a new "Last updated" date.

## Contact

For questions about this policy, contact the developer at: **p.piova@gmail.com**.
