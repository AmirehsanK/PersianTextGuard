import { defineConfig } from 'tsup';

export default defineConfig({
  entry: ['src/index.ts'],
  format: ['esm', 'cjs'],
  // tsup's declaration build sets baseUrl, which TypeScript 6 deprecates; silence only that.
  dts: { compilerOptions: { ignoreDeprecations: '6.0' } },
  target: 'es2020',
  platform: 'neutral',
  sourcemap: false,
  clean: true,
  treeshake: true,
  outExtension: ({ format }) => ({ js: format === 'esm' ? '.mjs' : '.cjs' }),
});
