# MaxerZ Desktop — Windows Build Instructions

This guide provides step-by-step instructions for setting up, building, and running **MaxerZ** on a Windows computer.

---

## Step 1: Install Prerequisites (One-time setup)

Make sure your Windows laptop has:

1. **[.NET 10 SDK](https://dotnet.microsoft.com/download)** installed.
2. **[Node.js (LTS)](https://nodejs.org)** installed.
3. **MAUI Workload** installed. Open PowerShell or Command Prompt (as Administrator) and run:

```cmd
dotnet workload install maui
```

---

## Step 2: Build the Web Frontend

The desktop app embeds an Angular UI. You must build the web assets after cloning:

```cmd
cd src\MaxerZ.Web
npm install
npm run build
cd ..\..
```

*(This automatically compiles the Angular app directly into `src/MaxerZ.Api/wwwroot/`).*

---

## Step 3: Build & Run the Windows App

### Option A: Quick Run (Development mode)

```cmd
dotnet run --project src/MaxerZ.Maui/MaxerZ.Maui.csproj -f net10.0-windows10.0.19041.0
```

### Option B: Build Standalone `.exe` Release Folder

```cmd
dotnet publish src/MaxerZ.Maui/MaxerZ.Maui.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true
```

Your standalone Windows application (`MaxerZ.exe` and all required dependency files) will be generated in:

```
src\MaxerZ.Maui\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\
```
