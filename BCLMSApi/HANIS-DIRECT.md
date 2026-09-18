# Direct HANIS V5 integration

The API calls SOAP 1.1 GetData directly using the supplied November 2025 specification, including its WSDL field names (IDN, TransactionDate, SiteID, WrkStnID; response IDNBlocked and Photo). It does not call the old REST gateway or send its API key. The old settings-page gateway fields no longer configure this integration.

Before deployment, set these server configuration values using the IDs supplied by DHA:

```
Hanis__Direct__Endpoint=https://hanisrvs2.hanis.gov.za/PersonData_4/HANISNPRRequest.asmx
Hanis__Direct__SiteId=<DHA-issued uppercase Site ID>
Hanis__Direct__WorkstationId=<DHA-issued uppercase Workstation ID>
```

Both IDs are required and limited to 20 characters. Do not invent IDs. DHA/SITA must allow the API server's outbound IP. HTTPS certificate validation remains enabled. Transaction dates use South African time (UTC+02:00); keep the server clock synchronized. There is no automatic secondary-server failover. Change the endpoint to the documented secondary only after DHA authorizes it.

The supplied contract has no token-issuing operation: upstream access uses institution registration and IP allowlisting. BCLMS protects its endpoints using existing signed, expiring login tokens:

```
Authorization: Bearer <token returned by BCLMS login>
GET  /api/HomeAffairs/applications/{applicationId}/lookup
POST /api/HomeAffairs/applications/{applicationId}/lookup?refresh=true
```

Missing, malformed, tampered or expired tokens receive 401 and a Bearer challenge. Customer tokens and officials without the required workflow assignment remain forbidden. Existing application/region visibility checks apply. The browser already sends this header. Production requires HTTPS and the existing PasswordSecurity:Pepper signing secret; never put it in browser code.

All nonzero HANIS error codes, including photo-only error 10088, prevent approval. Missing death, blocked or NPR flags also prevent approval. Full name and ID must match, and a valid transaction reference is required. Workflow approval continues to perform a fresh lookup. Cache keys now include the direct provider version, endpoint and institution IDs, so previous gateway results are not reused. No database migration is needed; the existing HomeAffairsIdentityLookups table is still required.

JPEG2000 photos are converted to JPEG with Magick.NET-Q8-AnyCPU 14.17.1, preserving the existing browser and database contract. Encoded size and decoded dimensions are bounded; metadata is stripped. Publish the package's native runtime dependencies with the API. See https://www.nuget.org/packages/Magick.NET-Q8-AnyCPU/14.17.1.

Synthetic checks in the Angular workspace `.hanis-direct/checks` cover SOAP requests/responses, identity rejection, all documented error codes, photo conversion and token validation. These tests do not contact DHA or write to the database. Live acceptance testing must be performed from the authorized server with DHA-provided test identities after institution IDs and network access are configured.
