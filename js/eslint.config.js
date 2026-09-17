import js from '@eslint/js';
import jsdoc from 'eslint-plugin-jsdoc';
import tseslint from 'typescript-eslint';

const unicodeMessage = 'Use the .NET-equivalent helpers in src/unicode.ts (research R1).';

export default tseslint.config(
  {
    ignores: ['dist/', 'temp/', 'src/generated/', 'consumers/', '.pack/', 'artifacts/', 'node_modules/'],
  },
  js.configs.recommended,
  {
    files: ['src/**/*.ts'],
    extends: [...tseslint.configs.strictTypeChecked],
    languageOptions: {
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
    },
    plugins: { jsdoc },
    rules: {
      // FR-023, SC-006: every export is documented.
      'jsdoc/require-jsdoc': [
        'error',
        {
          publicOnly: true,
          require: { ClassDeclaration: true, FunctionDeclaration: true, MethodDefinition: true },
          contexts: [
            'TSInterfaceDeclaration',
            'TSTypeAliasDeclaration',
            'ExportNamedDeclaration > VariableDeclaration',
            'TSPropertySignature',
          ],
        },
      ],
      'jsdoc/check-tag-names': ['error', { definedTags: ['remarks', 'example', 'defaultValue', 'packageDocumentation', 'typeParam'] }],
      // FR-003: no Node.js globals in the library.
      'no-restricted-globals': ['error', 'process', 'Buffer', 'require', '__dirname', 'global'],
      'no-restricted-syntax': [
        'error',
        {
          selector:
            "CallExpression[callee.property.name=/^(toLowerCase|toUpperCase|toLocaleLowerCase|toLocaleUpperCase|normalize|trim|trimStart|trimEnd)$/]",
          message: unicodeMessage,
        },
        { selector: 'Literal[regex.pattern=/\\\\s|\\\\S/]', message: unicodeMessage },
        { selector: "NewExpression[callee.name='RegExp'] > Literal[value=/\\\\s|\\\\S/]", message: unicodeMessage },
      ],
      // Ported code indexes typed arrays and strings by number, which is checked by hand.
      '@typescript-eslint/no-non-null-assertion': 'off',
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
    },
  },
  {
    files: ['src/unicode.ts'],
    rules: { 'no-restricted-syntax': 'off' },
  },
);
