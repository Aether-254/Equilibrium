# Equilibrium Market API

This directory is the design-stage API contract and Bruno collection for the
Equilibrium Market Server.

## Files

- `API-DESIGN.md`: endpoint catalog, invariants, transaction flows, and policy.
- `openapi.yaml`: normative HTTP(S) contract, OpenAPI 3.1.
- `asyncapi.yaml`: normative WSS event envelope and event types, AsyncAPI 3.0.
- `opencollection.yml`: Bruno/OpenCollection collection configuration.
- numbered folders: representative executable requests.

## Bruno setup

1. Open this directory as an OpenCollection collection in Bruno.
2. Set `baseUrl` to the selected Market Server origin.
3. Complete the server-advertised OAuth/OIDC login flow.
4. Set `accessToken` as a local or secret variable. Do not commit it.
5. Set `realmId` after running compatibility evaluation.

The checked-in base URL uses the reserved `.invalid` domain and cannot contact a
real server accidentally.

## Status

This is an API design, not a server implementation. Endpoint paths, schemas, and
state transitions are intended to be reviewed before implementation. No request
in this collection is proof that a corresponding server route exists.
