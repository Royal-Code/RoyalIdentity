/*
 * The result shape originated in IdentityServer4's Apache-2.0 CheckSessionResult.
 * RoyalIdentity substantially rewrote the HTML and JavaScript in 2026: the bundled third-party SHA-256,
 * global HTML cache and legacy two-segment protocol implementation were removed.
 */
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Authentication;
using RoyalIdentity.Extensions;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Security.Encoding;

namespace RoyalIdentity.Responses.HttpResults;

public class CheckSessionResult : IResult, IStatusCodeHttpResult
{
    private const int NonceSize = 32;

    public int? StatusCode => StatusCodes.Status200OK;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var realm = httpContext.GetCurrentRealm();
        var response = httpContext.Response;
        var nonce = Base64Url.Encode(CryptoRandom.CreateRandomKey(NonceSize));
        var csp = $"default-src 'none'; script-src 'nonce-{nonce}'";

        response.SetNoCache();
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Content-Security-Policy"] = csp;
        if (realm.Options.Csp.AddDeprecatedHeader)
            response.Headers["X-Content-Security-Policy"] = csp;
        else
            response.Headers.Remove("X-Content-Security-Policy");

        var cookieName = CheckSessionStateManager.GetCookieName(realm.Options.Authentication, realm);
        await response.WriteHtmlAsync(CreateHtml(cookieName, nonce));
    }

    internal static string CreateHtml(string cookieName, string nonce)
    {
        ArgumentException.ThrowIfNullOrEmpty(cookieName);
        ArgumentException.ThrowIfNullOrEmpty(nonce);

        var configurationJson = JsonSerializer.Serialize(new
        {
            cookieName,
            version = SessionStateFormat.Version,
            hashSize = SessionStateFormat.HashSize,
            saltSize = SessionStateFormat.SaltSize,
        });
        var encodedNonce = HtmlEncoder.Default.Encode(nonce);

        return $$"""
            <!DOCTYPE html>
            <!-- RoyalIdentity original reimplementation of OpenID Connect Session Management 1.0. -->
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>Check Session IFrame</title>
            </head>
            <body>
                <script nonce="{{encodedNonce}}">
            (() => {
                'use strict';

                const config = {{configurationJson}};
                const encoder = new TextEncoder();
                const decoder = new TextDecoder('utf-8', { fatal: true });
                const base64UrlPattern = /^[A-Za-z0-9_-]+$/;

                function readCookie(name) {
                    for (const entry of document.cookie.split(';')) {
                        const separator = entry.indexOf('=');
                        if (separator < 0) {
                            continue;
                        }

                        if (entry.slice(0, separator).trim() === name) {
                            return entry.slice(separator + 1).trim();
                        }
                    }

                    return '';
                }

                function encodeBase64Url(bytes) {
                    let binary = '';
                    for (const value of bytes) {
                        binary += String.fromCharCode(value);
                    }

                    return btoa(binary)
                        .replace(/=/g, '')
                        .replace(/\+/g, '-')
                        .replace(/\//g, '_');
                }

                function decodeBase64Url(value) {
                    if (typeof value !== 'string' || value.length === 0 || !base64UrlPattern.test(value)) {
                        return null;
                    }

                    let encoded = value.replace(/-/g, '+').replace(/_/g, '/');
                    const remainder = encoded.length % 4;
                    if (remainder === 1) {
                        return null;
                    }

                    encoded += '='.repeat((4 - remainder) % 4);

                    let binary;
                    try {
                        binary = atob(encoded);
                    }
                    catch {
                        return null;
                    }

                    const bytes = new Uint8Array(binary.length);
                    for (let index = 0; index < binary.length; index++) {
                        bytes[index] = binary.charCodeAt(index);
                    }

                    return encodeBase64Url(bytes) === value ? bytes : null;
                }

                function parseSessionState(value) {
                    if (typeof value !== 'string' || value.length === 0 || /\s/u.test(value)) {
                        return null;
                    }

                    const segments = value.split('.');
                    if (segments.length !== 4 || segments[0] !== config.version) {
                        return null;
                    }

                    const originBytes = decodeBase64Url(segments[1]);
                    const hash = decodeBase64Url(segments[2]);
                    const salt = decodeBase64Url(segments[3]);
                    if (!originBytes || !hash || !salt
                        || hash.length !== config.hashSize
                        || salt.length !== config.saltSize) {
                        return null;
                    }

                    let origin;
                    try {
                        origin = decoder.decode(originBytes);
                        const parsed = new URL(origin);
                        if ((parsed.protocol !== 'https:' && parsed.protocol !== 'http:')
                            || parsed.username !== ''
                            || parsed.password !== ''
                            || parsed.origin !== origin) {
                            return null;
                        }
                    }
                    catch {
                        return null;
                    }

                    return { origin, hash, salt };
                }

                function appendField(parts, value) {
                    const length = new Uint8Array(4);
                    new DataView(length.buffer).setUint32(0, value.length, false);
                    parts.push(length, value);
                }

                function createCanonicalBytes(clientId, origin, userAgentState, salt) {
                    const parts = [];
                    appendField(parts, encoder.encode(config.version));
                    appendField(parts, encoder.encode(clientId));
                    appendField(parts, encoder.encode(origin));
                    appendField(parts, encoder.encode(userAgentState));
                    appendField(parts, salt);

                    const size = parts.reduce((total, part) => total + part.length, 0);
                    const canonical = new Uint8Array(size);
                    let offset = 0;
                    for (const part of parts) {
                        canonical.set(part, offset);
                        offset += part.length;
                    }

                    return canonical;
                }

                function equalBytes(left, right) {
                    if (left.length !== right.length) {
                        return false;
                    }

                    let difference = 0;
                    for (let index = 0; index < left.length; index++) {
                        difference |= left[index] ^ right[index];
                    }

                    return difference === 0;
                }

                async function calculateResult(eventOrigin, message) {
                    if (typeof message !== 'string' || message.length === 0) {
                        return 'error';
                    }

                    const separator = message.lastIndexOf(' ');
                    if (separator <= 0 || separator === message.length - 1) {
                        return 'error';
                    }

                    const clientId = message.slice(0, separator);
                    const state = parseSessionState(message.slice(separator + 1));
                    if (!state || state.origin !== eventOrigin) {
                        return 'error';
                    }

                    const canonical = createCanonicalBytes(
                        clientId,
                        state.origin,
                        readCookie(config.cookieName),
                        state.salt);
                    const expected = new Uint8Array(await crypto.subtle.digest('SHA-256', canonical));
                    return equalBytes(expected, state.hash) ? 'unchanged' : 'changed';
                }

                if (window.parent === window) {
                    return;
                }

                window.addEventListener('message', async event => {
                    if (event.source !== window.parent) {
                        return;
                    }

                    let result = 'error';
                    try {
                        result = await calculateResult(event.origin, event.data);
                    }
                    catch {
                        result = 'error';
                    }

                    event.source.postMessage(result, event.origin);
                }, false);
            })();
                </script>
            </body>
            </html>
            """;
    }
}
