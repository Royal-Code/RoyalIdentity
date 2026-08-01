# Third-Party Notices

RoyalIdentity is distributed as a combined work under the GNU Affero General Public License v3.0. Portions of
the source retain their original Apache License 2.0 terms and attribution as described below. The AGPL license
for RoyalIdentity does not remove or replace those notices.

## IdentityServer4

- Upstream project: [IdentityServer4](https://github.com/IdentityServer/IdentityServer4)
- Local audit source: `old-is4/src/IdentityServer4/src`
- Copyright: Brock Allen and Dominick Baier. All rights reserved.
- License: Apache License 2.0, reproduced in [`LICENSES/Apache-2.0.txt`](LICENSES/Apache-2.0.txt).
- Use in RoyalIdentity: selected endpoint, validation, option, extension, event, message, response and service
  implementations were rearchitected and modified for the realm-aware RoyalIdentity pipeline.

## IdentityModel

- Upstream project: [Duende IdentityModel](https://github.com/DuendeSoftware/foss/tree/main/identity-model)
- Local audit source: `old-is4/src/IdentityModel`
- Copyright: Duende Software. All rights reserved.
- License: Apache License 2.0, reproduced in [`LICENSES/Apache-2.0.txt`](LICENSES/Apache-2.0.txt).
- Use in RoyalIdentity: selected constants and protocol/claim/certificate helpers were incorporated or modified;
  basename collisions and implementations independently written from public specifications are excluded.

Files classified as derived carry a prominent source header stating that they were modified by RoyalIdentity
contributors. The complete, machine-readable audit — production path, upstream candidate, evidence,
classification and required action — is maintained in
[`royal-identity/.ai/analisys/an-oidc-session-management-provenance.json`](royal-identity/.ai/analisys/an-oidc-session-management-provenance.json).
The 2026-08-01 audit covered all eligible files under both local upstream roots, found no unresolved candidate,
and confirmed that the local upstream distribution contains no root `NOTICE` file to transport under Apache-2.0
section 4(d).

This notice is scoped to the incorporated IS4/IdentityModel source. NuGet dependencies and vendored web assets
retain the licenses and notices shipped with those components, including the license files beside assets under
`wwwroot/lib`.
