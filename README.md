# LandTitleRegistration — Legacy .NET Framework 4.6.1 Demo Application

A compact ASP.NET / .NET Framework 4.6.1 land title registration system built with
**intentional legacy patterns** across all four COMPASS assessment domains.

**Purpose:** Hands-on Concierto Modernize demo — .NET M-Basic Cloud Readiness transformation.

---

## Tech Stack

| Item | Version |
|---|---|
| Runtime | .NET Framework 4.6.1 (EOL April 2022) |
| Language | C# |
| Web Framework | ASP.NET |
| ORM / Data | ADO.NET / SqlClient |
| Logging | log4net 2.0.8 |
| Serialisation | Newtonsoft.Json 12.0.1 |
| Build | MSBuild / NuGet |

---

## Violation Traceability Matrix

| Rule ID | Domain | Severity | File | Line(s) | Description |
|---|---|---|---|---|---|
| cr-csharp-0065 | Cloud Compatibility | Mandatory | TitleController.cs | 43, 44, 45, 62 | ASP.NET InProc Session — breaks Azure / AWS load balancer routing |
| cr-csharp-0067 | Cloud Compatibility | Potential | TitleController.cs | 48, 73–76 | Static Dictionary cache without TTL — instance-local, grows unbounded |
| cr-csharp-0088 | Cloud Compatibility | Mandatory | TitleController.cs | 67 | Plain HTTP call to internal document service |
| cr-csharp-0088 | Cloud Compatibility | Mandatory | TitleService.cs | 121 | Plain HTTP URL for Government API and report endpoint |
| cr-csharp-0021 | Cloud Compatibility | Mandatory | TitleController.cs | 20, 21, 22 | Hardcoded internal hostnames (docs, notify, search services) |
| cr-csharp-0021 | Cloud Compatibility | Mandatory | TitleService.cs | 13–16 | Hardcoded DB hostname and credentials fields |
| czr-csharp-001 | Software Portability | Mandatory | TitleController.cs | 28, 29, 30 | Hardcoded Windows absolute paths (C:\\ and D:\\) |
| czr-csharp-win32 | Software Portability | Mandatory | TitleController.cs | 78–82 | Windows Registry access via Microsoft.Win32 — breaks Linux containers |
| czr-csharp-port | Software Portability | High | TitleController.cs | 33 | Fixed port 8080 — blocks AKS / ECS dynamic port assignment |
| sql-inject-001 | Security Health | Critical | TitleService.cs | 43–46 | SQL injection via string concat — INSERT statement |
| sql-inject-001 | Security Health | Critical | TitleService.cs | 63 | SQL injection via string concat — SELECT by parcel |
| sql-inject-001 | Security Health | Critical | TitleService.cs | 133 | SQL injection via string concat — LIKE search by owner |
| sec-cred-001 | Security Health | Critical | TitleService.cs | 13, 14, 15, 16 | Hardcoded DB host, username, and password in source |
| sec-cred-001 | Security Health | Critical | TitleService.cs | 19 | Hardcoded Government API key in source code |
| sec-cred-001 | Security Health | High | TitleService.cs | 121 | API key passed as plain-text query string parameter |
| sec-weak-hash | Security Health | High | TitleService.cs | 53, 143–148 | SHA1 used for confirmation code — cryptographically broken since 2017 |
| CVE-2024-21907 | Security Health | Critical | LandTitleRegistration.csproj | 14 | Newtonsoft.Json 12.0.1 — insecure deserialisation (CVSS 9.8) |
| CVE-2018-1285 | Security Health | Critical | LandTitleRegistration.csproj | 18 | log4net 2.0.8 — XXE injection allows arbitrary file read (CVSS 9.8) |
| CVE-2023-29337 | Security Health | High | LandTitleRegistration.csproj | 22 | NuGet.Frameworks 6.0.0 — path traversal vulnerability |
| CVE-2021-26701 | Security Health | High | LandTitleRegistration.csproj | 26 | System.Text.Encodings.Web 5.0.0 — ReDoS vulnerability |
| complexity-001 | Code Sustainability | High | TitleService.cs | 72–94 | Cyclomatic complexity > 10 in CalculateRegistrationFee |
| dup-logic-001 | Code Sustainability | Medium | TitleService.cs | 99–101 | Title type validation duplicated across two methods |
| doc-missing-001 | Code Sustainability | Medium | TitleService.cs | 109 | Missing XML doc comment on public method SearchByOwner |

---

## Expected COMPASS Scores (Pre-Transformation)

| Domain | Expected Score | Primary Driver |
|---|---|---|
| Cloud Compatibility | ~52 / 100 | Session state + hardcoded config + plain HTTP |
| Software Portability | ~65 / 100 | Windows Registry + absolute Win paths + fixed port |
| Code Sustainability | ~63 / 100 | High complexity + duplication + missing docs |
| Security Health | ~38 / 100 | 4 critical CVEs + 3 SQL injection points + hardcoded creds |

---

## How to Build

```bash
nuget restore LandTitleRegistration.csproj
msbuild LandTitleRegistration.csproj /p:Configuration=Debug
```

---

## Line Count Summary

| File | Lines |
|---|---|
| LandTitleRegistration.csproj | 47 |
| Properties/AssemblyInfo.cs | 12 |
| Controllers/TitleController.cs | 100 |
| Services/TitleService.cs | 152 |
| **Total** | **311** |

*C# source lines only: 252*
