# Xperience Community: Usage Applications

## Description

Adds two read-only applications to the Xperience by Kentico administration, grouped under a **Usage reports** category:

- **Content Type Usage** lists every website page and reusable content type with the number of content items that use it. Selecting a content type lists its content items, and each item can be opened in its editor (Content hub for reusable content, the Content tab for pages).
- **Widget Usage** lists every registered Page Builder widget with the number of times it is placed in the latest version of the site content. Widgets that are still present in content but no longer registered in code are flagged as legacy. Selecting a widget lists the pages (per language) that use it, and each page can be opened in the Page Builder.

Widget placement counts are cached in memory for 5 minutes.
<br><br><br>
<img width="1484" height="931" alt="screenshot-1791998646179" src="https://github.com/user-attachments/assets/c70a3656-95ae-4a46-afc2-662a647f636d" />
<br><br><br>
<img width="1766" height="848" alt="screenshot-1791998796208" src="https://github.com/user-attachments/assets/e6c42935-68bf-49d5-8a46-86a15cc47dd3" />
<br><br><br>
<img width="1719" height="674" alt="screenshot-1791998684825" src="https://github.com/user-attachments/assets/5d552744-d05d-4bd2-8c29-ae7e67de849c" />
<br><br><br>
## Requirements

- [ASP.NET Core 10.0](https://dotnet.microsoft.com/en-us/download) or later
- [Xperience by Kentico](https://docs.kentico.com) 31.9.1 or later

## Package Installation

Add the package to your application using the .NET CLI

```
dotnet add package XperienceCommunity.UsageApplications
```

## Quick Start

1. Install the NuGet package.

1. Register the services in `Program.cs`.

    ```csharp
    using XperienceCommunity.UsageApplications;

    ...

    builder.Services.AddUsageApplications();
    ```

1. Start the website, sign in to the administration, and open **Content Type Usage** or **Widget Usage** from the **Usage reports** category.

Both applications require the **View** permission. Grant it to roles in **Role management** as needed.

## Building and testing the package locally

```
dotnet pack src/XperienceCommunity.UsageApplications/XperienceCommunity.UsageApplications.csproj -c Release -o artifacts -p:Version=0.1.0-local
```

Then, in the website that will consume the package, add the `artifacts` folder as a package source and reference the local version:

```
dotnet nuget add source C:\path\to\xperience-community-usage-applications\artifacts --name usage-applications-local
dotnet add package XperienceCommunity.UsageApplications --version 0.1.0-local
```

NuGet caches packages by version, so either bump the version for each new local build or clear the cached copy with `dotnet nuget locals global-packages --clear`.
