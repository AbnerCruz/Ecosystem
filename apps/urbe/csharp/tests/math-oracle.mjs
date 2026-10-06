// UC-16 supplemental oracle. Does not rewrite the UC-2 acceptance baseline.
// --write explicitly ports the frozen catalog and captures editing expectations;
// --check detects any drift. No renderer or KaTeX is loaded.
import fs from 'node:fs';
import vm from 'node:vm';
import crypto from 'node:crypto';
const source = fs.readFileSync(new URL('../../src/math/core.js', import.meta.url), 'utf8');
const context = vm.createContext({});
vm.runInContext(source, context);
const m = context.UrbeMath;
const texts = [
  '', '$\u0000x$', '$x$\u0000', 'a $x+1$ b $$y$$', '$x$ \\(y\\) \\[z\\] $$w$$',
  'Seja $x^2$ e \\(y\\) mas R$ 10 e R$ 20, `$a$` e \\$5.\n\n```\n$$nao$$\n```\n\n$$\n\\int_0^1 f\n$$\n\nTexto \\[a=b\\] fim. E $5 e $6.',
  '$$ \nA=B\n\t$$', '  \\[a=b\\] \t\n', 'texto $$x$$ fim', '$$a\n\nb$$',
  '$x\ny$ \\(a\nb\\)', '$ x$ $x $ $x$2 $x$٢',
  '\\$x$ e $x\\$y$', '\\(\\) \\[\\] $$$$',
  '~~~tex\n$x$\n~~~\n$y$', '   ```tex\n$x$\n   ```\n$y$',
  '````\n$x$\n```\n$y$\n````\n$z$', '`$x$` ``$y$`` $z$',
  '😀 $α+β$ e \\[∑\\]', '$\uFEFFx$ $x\uFEFF$', '$\u0085x$ $x\u0085$',
  '$$a\\$$b$$', 'não fecha $x', '$$', '\\', 'CRLF\r\n$$\r\nx\r\n$$',
  '\\[a\n\nb\\]', '$a\\$b$', '\\\\$x$', '``` sem fechar\n$x$',
  '$x$\n\t$$\nx\n$$\n', '``sem fechar $x$', 'um $a$ e $b$'
];
const prefixes = ['', '\\', '\\fr', '\\Del', '\\del', '\\MA', '\\not', '\\sqrt', '\\begin', '\\frac{', 'alpha', '\\α', '\\fr\n'];
const oracle = {
  source: 'src/math/core.js', sha256: crypto.createHash('sha256').update(source).digest('hex'),
  scan: texts.map(markdown => ({ markdown, items: m.scan(markdown),
    cursors: Array.from({length: markdown.length + 3}, (_, i) => {
      const found = m.findAt(markdown, i - 1);
      return found ? m.scan(markdown).findIndex(item => item.start === found.start) : -1;
    }) })),
  commands: m.COMMANDS, symbols: m.SYMBOLS, templates: m.TEMPLATES,
  complete: prefixes.flatMap(prefix => [undefined, 1, 0, 4, 1000, -1, -1000].map(limit =>
    ({prefix, limit: limit ?? 8, items: m.complete(prefix, limit)}))),
  snippets: [...m.COMMANDS.map(c => c.snip), ...m.TEMPLATES.map(c => c.tex),
    null, '', 'abc', '●a●b', '😀●x', 'α●β'].map(template => ({template, expected: m.snippet(template)}))
};
const q = s => JSON.stringify(s);
const command = c => `        new(${q(c.cmd)}, ${q(c.snip)}, ${q(c.desc)}, ${q(c.prev)})`;
const symbol = s => `            new(${q(s.label)}, ${q(s.tex)})`;
const catalog = `// Port of the frozen UrbeMath catalog; verified by tests/math-oracle.mjs --check.
namespace Urbe.Core;

public static partial class MathEditing
{
    public static IReadOnlyList<MathCommand> Commands { get; } = Array.AsReadOnly<MathCommand>([
${m.COMMANDS.map(command).join(',\n')}
    ]);

    public static IReadOnlyList<MathSymbolGroup> Symbols { get; } = Array.AsReadOnly<MathSymbolGroup>([
${m.SYMBOLS.map(g => `        new(${q(g.name)}, Array.AsReadOnly<MathSymbol>([\n${g.items.map(symbol).join(',\n')}\n        ]))`).join(',\n')}
    ]);

    public static IReadOnlyList<MathSymbol> Templates { get; } = Array.AsReadOnly<MathSymbol>([
${m.TEMPLATES.map(symbol).join(',\n')}
    ]);
}
`;
const outputs = [
  [new URL('../src/Urbe.Core/MathCatalog.cs', import.meta.url), catalog],
  [new URL('fixtures/math-editing.json', import.meta.url), JSON.stringify(oracle, null, 2) + '\n']
];
if (!['--write', '--check'].includes(process.argv[2])) throw new Error('Use --write or --check');
for (const [url, contents] of outputs) {
  if (process.argv[2] === '--write') { fs.mkdirSync(new URL('.', url), {recursive: true}); fs.writeFileSync(url, contents); }
  else if (fs.readFileSync(url, 'utf8') !== contents) throw new Error(`Oracle/catalog drift: ${url.pathname}`);
}
console.log(`Math oracle: ${texts.length} scans, ${oracle.complete.length} completion cases, ${oracle.snippets.length} snippets; catalog verified.`);
