import { readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');

const space = '[\\t\\v\\f\\r\\x85\\x20\\xa0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000]';
const follows = new RegExp(
    `(?<![^\\n])${space}*(?:/// </summary>|/// <summary>[^\\n]*</summary>)${space}*`
        + `(?=\\n${space}*/// <summary>)`,
    'gi');

function sourcesUnder(place) {
    const found = [];
    for (const entry of readdirSync(place, { withFileTypes: true })) {
        const path = join(place, entry.name);
        const name = entry.name.toLowerCase();
        if (entry.isDirectory()) {
            if (name !== 'bin' && name !== 'obj') found.push(...sourcesUnder(path));
        } else if (entry.isFile() && name.endsWith('.cs')) {
            found.push(path);
        }
    }

    return found;
}

function decodeUtf32(bytes, littleEndian) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const chars = [];
    let at = 4;
    for (; at + 4 <= bytes.length; at += 4) {
        const point = view.getUint32(at, littleEndian);
        chars.push(String.fromCodePoint(
            point <= 0x10ffff && (point < 0xd800 || point > 0xdfff) ? point : 0xfffd));
    }

    if (at < bytes.length) chars.push('�');

    return chars.join('');
}

function decode(encoding, bytes) {
    return new TextDecoder(encoding, { ignoreBOM: true }).decode(bytes);
}

function textOf(file) {
    const bytes = readFileSync(file);
    const starts = (...marks) => marks.every((mark, at) => bytes[at] === mark);
    if (starts(0xff, 0xfe, 0x00, 0x00)) return decodeUtf32(bytes, true);
    if (starts(0x00, 0x00, 0xfe, 0xff)) return decodeUtf32(bytes, false);
    if (starts(0xef, 0xbb, 0xbf)) return decode('utf-8', bytes.subarray(3));
    if (starts(0xff, 0xfe)) return decode('utf-16le', bytes.subarray(2));
    if (starts(0xfe, 0xff)) return decode('utf-16be', bytes.subarray(2));

    return decode('utf-8', bytes);
}

const orphans = [];
for (const file of ['src', 'tests'].flatMap((top) => sourcesUnder(join(root, top)))) {
    const text = textOf(file).replace(/\r\n?/g, '\n');
    for (const match of text.matchAll(follows)) {
        const line = text.slice(0, match.index).split('\n').length + 1;
        orphans.push(`${file}:${line}`);
    }
}

if (orphans.length !== 0) {
    console.error('summaryコメントが、説明する対象のすぐ上に無い: ' + orphans.join('・'));
    process.exitCode = 1;
}
