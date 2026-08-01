# Real-Time Currency Conversion & Audit Trail (Implementation Plan)

This repo implements the business intent from `intent/input-spec-agent.md` under the existing stack and infrastructure constraints.

## What’s implemented

### Frontend (React + Vite)

- Single-page UI to convert `amount` between `fromCurrency` and `toCurrency`.
- Displays the latest audit trail entries.
- Runtime API base URL support: the built `index.html` contains `__VITE_API_URL__`, which is replaced at container startup by `src/frontend/entrypoint.sh`.

### Backend (.NET 10 / minimal API)

- `POST /api/conversions`: fetches an external FX rate (schema-flexible parsing), converts the amount, and persists an audit record with an exact backend execution timestamp and provider date marker.
- `GET /api/conversions?limit=N`: lists the latest audit trail items.
- Cosmos DB provisioning:
  - ARM (control plane) provisioning is best-effort during startup.
  - Data-plane `CreateDatabaseIfNotExistsAsync` and `CreateContainerIfNotExistsAsync` are executed with token-based Managed Identity credentials, and startup fails if these operations fail.

## Key compliance constraints

- Backend reads Cosmos configuration from environment variables using the exact keys in `docs/CONTAINER_ENVIRONMENT_VARIABLES.md`.
- Backend authenticates to Cosmos DB using `DefaultAzureCredential` with `ManagedIdentityClientId`.
- External provider schema is handled via flexible JSON property mapping (supports multiple possible field names like `rates` vs `conversion_rates`).
- External HTTP failures throw domain exceptions (no raw network/serialization errors are bubbled to the client).
