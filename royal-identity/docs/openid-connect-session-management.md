# OpenID Connect Session Management

RoyalIdentity implements the OpenID Provider side of OpenID Connect Session Management 1.0. The feature lets a
Relying Party (RP) detect that the user's OP session changed without exposing the internal session identifier or
user claims to browser JavaScript.

## Endpoint and discovery

Each realm owns its endpoint:

```text
/{realm}/connect/checksession
```

Discovery publishes `check_session_iframe` only when the endpoint is enabled and the effective request scheme is
HTTPS. The endpoint follows the same rule: disabled or non-HTTPS requests receive 404. Behind a reverse proxy,
configure forwarded headers and trusted proxies/networks before the RoyalIdentity protocol pipeline so the
effective scheme cannot be forged by an untrusted peer.

The realm settings are:

- `RealmOptions.Endpoints.EnableCheckSessionEndpoint` — enables the lifecycle, endpoint and discovery entry;
  default `true`.
- `RealmOptions.Authentication.CheckSessionCookieName` — base name for the browser-readable OP state cookie.

The effective cookie name is `{base-name}.{realm-path}` and its path is `/{realm}`. Domain is omitted (host-only)
and the security attributes are fixed: `Secure`, `SameSite=None`, `HttpOnly=false` and `IsEssential=true`. The
cookie value is a random, opaque OP User Agent State. It is distinct from `sid`, `sub`, the security stamp and all
storage handles.

## Authentication responses and iframe protocol

For supported OIDC Authentication Responses with a browser HTTP(S) redirect origin, RoyalIdentity emits an
opaque, origin-bound `session_state`. Native/custom-scheme redirect URIs do not have a browser origin and therefore
do not receive `session_state`.

The RP sends exactly one string to the OP iframe:

```text
{client_id} {session_state}
```

The iframe validates `event.source`, `event.origin`, the envelope and its hash before responding to that exact
origin with `unchanged`, `changed` or `error`. It never uses `postMessage(..., "*")`. An RP must validate the
iframe window and OP origin on the response as well.

`changed` is a signal to attempt an OIDC check such as `prompt=none`; it is not proof that the user logged out.
The same signal occurs after login rotation, account switch, explicit logout, invalidated tickets, missing state
or a browser that withholds third-party cookies. RPs must avoid immediate unbounded retry/polling loops.

## Third-party cookie limitations

`SameSite=None; Secure` permits the cookie in a cross-site iframe when the browser allows third-party state, but
modern privacy policies may still block it. In that case the OP cannot distinguish a blocked cookie from an
absent cookie and correctly returns `changed`. RoyalIdentity does not currently implement Storage Access API or
CHIPS-specific recovery. Applications needing authoritative logout notification should also use front-channel or
back-channel logout.

## Relationship to logout specifications

- **RP-Initiated Logout** starts logout at `/{realm}/connect/endsession` and ends the local OP session.
- **Front-Channel Logout** notifies configured RPs through browser frames. It is useful when browser state is
  available but inherits browser/network limitations.
- **Back-Channel Logout** sends signed logout tokens server-to-server and does not depend on third-party cookies.
- **Session Management / Check Session** only detects changes from an RP iframe. It neither initiates logout nor
  replaces either notification mechanism.

These mechanisms are complementary. Check Session improves browser coordination; Back-Channel Logout is the
stronger choice when delivery must not depend on browser cookie policy.

## Verification

The default solution contains HTTP, realm-lifecycle and architecture tests without requiring a browser. The real
Chromium acceptance remains opt-in:

```powershell
dotnet test RoyalIdentity.sln
./scripts/Test-CheckSessionJavaScript.ps1
./scripts/Test-CheckSessionBrowser.ps1
```

The browser harness uses three locally resolved HTTPS sites, proves cross-site `SameSite=None` delivery, exact
origins, two-realm isolation and bounded behavior when cookie access is unavailable.
