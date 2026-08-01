import { readFileSync } from 'node:fs';

const payload = JSON.parse(readFileSync(0, 'utf8').replace(/^\uFEFF/, ''));
const scriptMatch = payload.Html.match(/<script nonce="[^"]+">([\s\S]*?)<\/script>/);
if (!scriptMatch) {
    throw new Error('The rendered iframe does not contain its nonce-bearing script.');
}

const messages = [];
const parent = {
    postMessage(result, origin) {
        messages.push({ result, origin });
    },
};

globalThis.window = { parent };
window.addEventListener = (_name, listener) => {
    globalThis.checkSessionListener = listener;
};
globalThis.document = { cookie: '' };

(0, eval)(scriptMatch[1]);
if (typeof globalThis.checkSessionListener !== 'function') {
    throw new Error('The rendered script did not register the message listener.');
}

async function evaluate({ data, origin = payload.Origin, cookie = payload.UserAgentState, source = parent }) {
    messages.length = 0;
    document.cookie = cookie === null ? '' : `${payload.CookieName}=${cookie}`;
    await globalThis.checkSessionListener({ data, origin, source });
    if (messages.length > 1) {
        throw new Error(`The iframe answered ${messages.length} times for one message.`);
    }

    return messages.length === 0 ? null : messages[0];
}

const validMessage = `${payload.ClientId} ${payload.SessionState}`;
const scenarios = [
    ['matching cookie', { data: validMessage }, 'unchanged'],
    ['different cookie', { data: validMessage, cookie: `B${payload.UserAgentState.slice(1)}` }, 'changed'],
    ['missing cookie', { data: validMessage, cookie: null }, 'changed'],
    ['different event origin', { data: validMessage, origin: 'https://other.example' }, 'error'],
    ['malformed message', { data: 'malformed' }, 'error'],
    ['unsupported version', { data: validMessage.replace(' v1.', ' v2.') }, 'error'],
    ['non-string payload', { data: { clientId: payload.ClientId } }, 'error'],
    ['different client id', { data: `other-client ${payload.SessionState}` }, 'changed'],
    ['source other than parent', { data: validMessage, source: {} }, null],
];

for (const [name, input, expected] of scenarios) {
    const answer = await evaluate(input);
    const actual = answer?.result ?? null;
    if (actual !== expected) {
        throw new Error(`${name}: expected ${expected ?? 'no response'}, received ${actual ?? 'no response'}.`);
    }
    if (answer && answer.origin !== input.origin && answer.origin !== payload.Origin) {
        throw new Error(`${name}: response used an unexpected target origin ${answer.origin}.`);
    }

    process.stdout.write(`${name}: ${actual ?? 'no response'}\n`);
}
