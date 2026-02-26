# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Chromedroid is a .NET class library for orchestrating Chromium-based browsers on connected Android devices. It communicates with Android devices via ADB and controls Chrome/Chromium instances through the Chrome DevTools Protocol (CDP).

## Build Commands

```bash
# Build the solution
dotnet build Chromedroid.slnx

# Build just the library project
dotnet build Chromedroid/Chromedroid.csproj

# Run tests (when test project exists)
dotnet test Chromedroid.slnx
```

## Tech Stack

- **Framework**: .NET 10.0 (preview)
- **Language**: C# with nullable reference types and implicit usings enabled
- **Solution format**: .slnx (XML-based slim solution)
- **IDE**: JetBrains Rider

## Architecture

This is currently a greenfield library project with a single class library (`Chromedroid/`). The intended architecture involves:

- **ADB communication** — discovering and managing connected Android devices
- **Chrome DevTools Protocol (CDP)** — controlling Chromium browser instances on those devices
- **Browser orchestration** — creating, managing, and automating browser sessions across multiple devices
