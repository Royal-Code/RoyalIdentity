using System.Reflection;
using System.Text.Json;
using RoyalIdentity.Responses.HttpResults;

const string clientId = "cliente-ç";
const string origin = "https://xn--exmple-xta.test:8443";
const string userAgentState = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
const string cookieName = ".probe.check-session";

var assembly = typeof(CheckSessionResult).Assembly;
var formatType = assembly.GetType("RoyalIdentity.Authentication.SessionStateFormat", throwOnError: true)!;
var createState = formatType.GetMethod(
    "Create",
    BindingFlags.Static | BindingFlags.NonPublic,
    binder: null,
    types: [typeof(string), typeof(string), typeof(string)],
    modifiers: null)
    ?? throw new InvalidOperationException("SessionStateFormat.Create(string, string, string) was not found.");
var createHtml = typeof(CheckSessionResult).GetMethod(
    "CreateHtml",
    BindingFlags.Static | BindingFlags.NonPublic,
    binder: null,
    types: [typeof(string), typeof(string)],
    modifiers: null)
    ?? throw new InvalidOperationException("CheckSessionResult.CreateHtml(string, string) was not found.");

var sessionState = (string?)createState.Invoke(null, [clientId, origin, userAgentState])
    ?? throw new InvalidOperationException("The C# session-state generator returned no value.");
var html = (string?)createHtml.Invoke(null, [cookieName, "probe-nonce"])
    ?? throw new InvalidOperationException("The C# iframe renderer returned no HTML.");

Console.Write(JsonSerializer.Serialize(new ProbePayload(
    ClientId: clientId,
    Origin: origin,
    UserAgentState: userAgentState,
    CookieName: cookieName,
    SessionState: sessionState,
    Html: html)));

internal sealed record ProbePayload(
    string ClientId,
    string Origin,
    string UserAgentState,
    string CookieName,
    string SessionState,
    string Html);
