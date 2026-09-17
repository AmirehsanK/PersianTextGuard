// Uses only normalization: the bundled word lists must not be pulled in.
import { normalize } from 'persian-text-guard';

globalThis.result = normalize('كتاب‌هاي  ۱۲ ABC');
