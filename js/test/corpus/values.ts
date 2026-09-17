// Corpus values: building inputs, code-point positions, visible text and exact comparison.
// Port of the matching parts of dotnet/tests/PersianTextGuard.Conformance/Corpus.cs.
import { categoryOfUnit, isHighSurrogate, isLowSurrogate, isNoncharacter, isWhiteSpace } from '../../src/unicode.ts';
import type { Json, JsonObject } from './load.ts';

function isObject(value: Json | undefined): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Builds an Input: a string, `null`, or `{ "build": [ parts… ] }`. */
export function buildInput(input: Json | undefined): string | null {
  if (input === null || input === undefined) {
    return null;
  }

  if (typeof input === 'string') {
    return input;
  }

  if (isObject(input) && Object.keys(input).length === 1 && Array.isArray(input.build)) {
    let text = '';
    for (const part of input.build) {
      if (!isObject(part)) {
        throw new Error(`A build part must be an object: ${display(part)}`);
      }

      const keys = Object.keys(part);
      if (keys.length === 1 && typeof part.text === 'string') {
        text += part.text;
      } else if (keys.length === 2 && typeof part.repeat === 'string' && typeof part.times === 'number' && part.times >= 1) {
        text += part.repeat.repeat(part.times);
      } else if (keys.length === 1 && typeof part.utf16 === 'string' && /^[0-9A-Fa-f]{4}$/.test(part.utf16)) {
        // One UTF-16 unit, which may be a lone surrogate: JavaScript strings can hold every part.
        text += String.fromCharCode(Number.parseInt(part.utf16, 16));
      } else {
        throw new Error(`Not a build part: ${display(part)}`);
      }
    }

    return text;
  }

  throw new Error(`Not an Input: ${display(input)}`);
}

/** Whether a value is text: a string or a build object. */
export function isText(node: Json | undefined): boolean {
  return typeof node === 'string' || (isObject(node) && Object.keys(node).length === 1 && Array.isArray(node.build));
}

function isPairAt(text: string, i: number): boolean {
  return i + 1 < text.length && isHighSurrogate(text.charCodeAt(i)) && isLowSurrogate(text.charCodeAt(i + 1));
}

/** A code-point region as a UTF-16 index and length. A lone surrogate is one code point and one unit. */
export function codePointsToUtf16(text: string, start: number, length: number): [number, number] {
  const advance = (unit: number, codePoints: number): number => {
    for (; codePoints > 0 && unit < text.length; codePoints--) {
      unit += isPairAt(text, unit) ? 2 : 1;
    }

    return unit;
  };

  const index = advance(0, start);
  return [index, advance(index, length) - index];
}

/** A UTF-16 region as a code-point start and length. */
export function utf16ToCodePoints(text: string, index: number, length: number): [number, number] {
  const count = (from: number, to: number): number => {
    let n = 0;
    for (let i = from; i < to; n++) {
      i += isPairAt(text, i) && i + 1 < to ? 2 : 1;
    }

    return n;
  };

  return [count(0, index), count(index, index + length)];
}

function isInvisible(unit: number): boolean {
  const category = categoryOfUnit(unit);
  return (
    category === 'Cf' ||
    category === 'Cc' ||
    category === 'Zl' ||
    category === 'Zp' ||
    (isWhiteSpace(unit) && unit !== 0x20) ||
    isNoncharacter(unit)
  );
}

const hex = (unit: number): string => `\\u${unit.toString(16).toUpperCase().padStart(4, '0')}`;

/**
 * The text with invisible characters shown as `\uXXXX`: Cf, Cc, Zl, Zp, whitespace other than U+0020,
 * lone surrogates and noncharacters.
 */
export function showInvisible(text: string | null): string {
  if (text === null) {
    return 'null';
  }

  let result = '';
  for (let i = 0; i < text.length; i++) {
    const unit = text.charCodeAt(i);
    if (isPairAt(text, i)) {
      const low = text.charCodeAt(i + 1);
      const codePoint = 0x10000 + ((unit - 0xd800) << 10) + (low - 0xdc00);
      result += isNoncharacter(codePoint) ? hex(unit) + hex(low) : text.substring(i, i + 2);
      i++;
    } else if ((unit >= 0xd800 && unit <= 0xdfff) || isInvisible(unit)) {
      result += hex(unit);
    } else {
      result += text[i]!;
    }
  }

  return result;
}

/** Text for messages, shortened when long. */
export function showText(text: string | null): string {
  const limit = 200;
  if (text === null) {
    return 'null';
  }

  return text.length <= limit
    ? `"${showInvisible(text)}"`
    : `"${showInvisible(text.substring(0, limit))}…" (${text.length} UTF-16 units)`;
}

/** A value on one line, for messages. */
export function display(node: Json | undefined): string {
  if (node === undefined) {
    return '(missing)';
  }

  if (isText(node)) {
    return showText(buildInput(node));
  }

  return JSON.stringify(node, (_key, value: unknown) => (typeof value === 'string' ? showInvisible(value) : value));
}

export interface Difference {
  readonly path: string;
  readonly expected: string;
  readonly actual: string;
}

/**
 * Every difference between an expected and an actual result, field by field. Text compares by its built
 * value, so a string and an equivalent build object are equal.
 */
export function compare(expected: Json | undefined, actual: Json | undefined, path = 'expected'): Difference[] {
  const differences: Difference[] = [];
  walk(expected, actual, path, differences);
  return differences;
}

function walk(expected: Json | undefined, actual: Json | undefined, path: string, out: Difference[]): void {
  if (expected === null || actual === null || expected === undefined || actual === undefined) {
    if (expected !== actual) {
      out.push({ path, expected: display(expected), actual: display(actual) });
    }

    return;
  }

  if (isText(expected) && isText(actual)) {
    const e = buildInput(expected);
    const a = buildInput(actual);
    if (e !== a) {
      out.push({ path, expected: showText(e), actual: showText(a) });
    }

    return;
  }

  if (isObject(expected) && isObject(actual)) {
    for (const key of Object.keys(expected)) {
      if (key in actual) {
        walk(expected[key], actual[key], `${path}.${key}`, out);
      } else {
        out.push({ path: `${path}.${key}`, expected: display(expected[key]), actual: '(missing)' });
      }
    }

    for (const key of Object.keys(actual)) {
      if (!(key in expected)) {
        out.push({ path: `${path}.${key}`, expected: '(missing)', actual: display(actual[key]) });
      }
    }

    return;
  }

  if (Array.isArray(expected) && Array.isArray(actual)) {
    for (let i = 0; i < Math.max(expected.length, actual.length); i++) {
      if (i >= expected.length) {
        out.push({ path: `${path}[${i}]`, expected: '(missing)', actual: display(actual[i]) });
      } else if (i >= actual.length) {
        out.push({ path: `${path}[${i}]`, expected: display(expected[i]), actual: '(missing)' });
      } else {
        walk(expected[i], actual[i], `${path}[${i}]`, out);
      }
    }

    return;
  }

  if (typeof expected !== typeof actual || Array.isArray(expected) !== Array.isArray(actual) || expected !== actual) {
    out.push({ path, expected: display(expected), actual: display(actual) });
  }
}
