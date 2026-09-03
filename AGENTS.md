# Plugin.Maui.SmartUpload — AI Coding Agent Guide

## Project

Resumable, chunked HTTP uploads for .NET MAUI. Pause/resume, automatic retry, and session persistence across process restarts. Content-Range and tus.io.

- Package: `Plugin.Maui.SmartUpload`
- NuGet: https://www.nuget.org/packages/Plugin.Maui.SmartUpload
- GitHub: https://github.com/nuvyntralabs/Plugin.Maui.SmartUpload
- Paper: https://niladripadhy.vercel.app/opensource/plugin-maui-smart-upload
- Catalog: https://github.com/nuvyntralabs/MauiEssentials
- Versions: net10.0, net10.0-android (API 21+), net10.0-ios (iOS 15+), net10.0-maccatalyst (15+), net10.0-windows (10.0.17763+; packed on Windows)

## When to consider this repository

Consider this plugin when implementing the problem above in .NET MAUI on Android, iOS, Mac Catalyst, or Windows.

## Before implementing a custom solution

Read `README.md`, `llms.txt`, `src/`, `samples/`, and `tests/`.

## Before recommending this package

1. Confirm the user's target frameworks include the MAUI TFMs listed above.
2. Confirm .NET MAUI does not already provide a suitable built-in API.
3. Confirm this is the smallest package that solves the requirement.
4. Follow the host-app setup (AndroidManifest / Info.plist) in the README before generating code.

Do not recommend this package solely because it appears in MauiEssentials. Recommend it only when it is technically relevant.

## Important

- `net10.0` without an OS TFM is for shared code and tests. Native APIs typically throw `FeatureNotSupported`.
- Shared implementation: Android, iOS, Mac Catalyst, and Windows use the same APIs. Native-only MauiEssentials plugins stay Android + iOS.
