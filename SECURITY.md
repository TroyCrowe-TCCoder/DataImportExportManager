# Security Policy

## Supported Versions

Only the latest release of **DataImportExportManager** receives security fixes. Older versions are not patched.

| Version | Supported |
|---|---|
| 1.x (latest) | ✅ Yes |
| < 1.0 | ❌ No |

---

## Reporting a Vulnerability

**Do not open a public issue for security vulnerabilities.**

Report security concerns privately via GitHub Security Advisories at:

```
https://github.com/TroyCrowe-TCCoder/DataImportExportManager/security/advisories/new
```

Include as much detail as possible:

- A description of the vulnerability
- Steps to reproduce (proof-of-concept code if applicable)
- The potential impact and affected versions
- Any suggested mitigation

You will receive an acknowledgment within **5 business days** and a resolution timeline within **15 business days** of triage.

---

## Security Design Considerations

DataImportExportManager is a class library that parses and generates tabular data files (CSV, Excel, JSON, NDJSON, XML) from caller-supplied streams. Because it accepts untrusted input by design, the following controls are enforced directly in the library and cannot be bypassed by callers.

### Library-Enforced Controls

| Control | Where enforced | What it prevents |
|---|---|---|
| CSV/Excel formula-injection sanitization | `CsvExporter`, `ExcelExporter` (`SanitizeFormulaCells`) | Values starting with `=`, `+`, `-`, `@` are prefixed to prevent formula execution when the file is opened in spreadsheet software |
| Delimiter validation | `CsvExporter` constructor | `ArgumentException` if the configured delimiter is a double-quote, CR, or LF, which would corrupt RFC 4180 escaping |
| Streamed Excel row limit (1,048,576 rows) | `ExcelExporter` | `InvalidOperationException` before OpenXml attempts to write a workbook that exceeds the Excel worksheet row limit |
| Buffered input size limit | `ExcelImporter.CopyWithSizeLimitAsync` (`MaxBufferSize`) | OOM from oversized or unbounded non-seekable streams being fully buffered into memory before OpenXml can read them |
| XML root/shape validation | `XmlImporter` | `InvalidOperationException` on payloads that do not match the deterministic `<rows><row>...</row></rows>` contract, rejecting unexpected/malicious document shapes early |
| Deterministic XML parsing via `XDocument.LoadAsync` (no custom `XmlResolver`, DTD processing disabled by default) | `XmlImporter` | XXE (XML External Entity) injection and DTD-based denial-of-service (billion laughs) via external entity resolution |
| Null/whitespace guards on all public parameters | Importers, Exporters, `DataFormatRouter` | `ArgumentNullException`/`ArgumentException` instead of downstream `NullReferenceException` or malformed-input parsing failures |
| Tenant/subject-scoped cache keys | `InMemoryImportSchemaSessionCache`, `DistributedImportSchemaSessionCache` | Cross-tenant access to cached import payloads; keys are namespaced by `tenantId` + `subjectId` + a server-generated session ID (never caller-suppliable) |
| One-time consume-and-invalidate session reads | `IImportSchemaSessionCache.ConsumeAsync` | Replay of a previously consumed import session payload |
| Positive-TTL enforcement | `InMemoryImportSchemaSessionCache`, `DistributedImportSchemaSessionCache` | Indefinite retention of cached import payloads from misconfigured or zero/negative TTL values (`ArgumentOutOfRangeException`) |

### Untrusted File Handling

- **Treat all imported files as untrusted input**, regardless of the source (user upload, external API, file share). All importers validate structure before processing, but callers should still enforce their own upload size limits and content-type checks ahead of this library.
- **Formula injection is only mitigated on export.** `SanitizeFormulaCells` protects data this library *writes*. It does not sanitize values found in files this library *imports* — if imported data is later re-exported or displayed in a spreadsheet application, apply the same sanitization to values sourced from untrusted imports.
- **Do not disable `SanitizeFormulaCells`** for CSV/Excel exports that may be opened by end users in spreadsheet software, unless the data is fully trusted and machine-generated.
- **Excel imports are memory-buffered.** `ExcelImporterOptions.MaxBufferSize` bounds the buffered stream size; set it appropriately for your expected file sizes rather than relying on the default.

### Session Cache Handling

- **Never share a session cache instance across tenants without providing accurate `tenantId`/`subjectId` values.** The cache does not perform its own authorization — it relies on the caller supplying correct scope identifiers.
- **Prefer `DistributedImportSchemaSessionCache`** (backed by `IDistributedCache`) over the in-memory implementation in multi-instance deployments to avoid session data being unreachable after a load-balanced request lands on a different instance.
- **Set TTLs as short as practical.** Cached payloads may contain sensitive imported data; the default 20-minute TTL should be reduced for highly sensitive datasets.

### Dependency Supply Chain

- Review `DocumentFormat.OpenXml` and `Microsoft.Extensions.*` package updates for security advisories before upgrading, particularly OpenXml given its role in parsing untrusted `.xlsx` input.
- Run `dotnet restore` / `dotnet list package --vulnerable` regularly to catch newly disclosed advisories in transitive dependencies.

---

## Related Resources

- [OWASP CSV Injection](https://owasp.org/www-community/attacks/CSV_Injection)
- [OWASP XML External Entity (XXE) Prevention](https://cheatsheetseries.owasp.org/cheatsheets/XML_External_Entity_Prevention_Cheat_Sheet.html)
- [Microsoft Security Development Lifecycle](https://www.microsoft.com/en-us/securityengineering/sdl)
- [.NET Security Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/security/security-best-practices)
