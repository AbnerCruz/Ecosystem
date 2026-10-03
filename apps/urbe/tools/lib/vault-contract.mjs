// UC-3: projeção verificável das autoridades de dados existentes, sem novo formato.
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { ROOT } from './v2-docs.mjs';

const read = p => readFileSync(join(ROOT, p), 'utf8');
const sha = b => createHash('sha256').update(b).digest('hex');
const walk = p => readdirSync(join(ROOT,p), {withFileTypes:true}).flatMap(e => e.isDirectory() ? walk(`${p}/${e.name}`) : [`${p}/${e.name}`]).sort();
export const authorities = ['docs/v2/discovery/DATA-CATALOG.md', 'docs/v2/adr/0004-compatibilidade-1x-e-protecao-forward.md'];
export const implementations = ['src/persistence/workspace.js','src/persistence/vault-meta.js','src/persistence/backup.js','src/persistence/identity.js','src/persistence/gc.js','src/persistence/export-manifest.js','src/core/artifacts.js','src/world/stable-ids.js'];
export function manifest() {
  return {schemaVersion:1,product:'urbe',authority:authorities,
    sources:[...authorities,...implementations].map(path => ({path,sha256:sha(readFileSync(join(ROOT,path)))})),
    fixtures:readdirSync(join(ROOT,'tests/fixtures/vaults'),{withFileTypes:true}).filter(e=>e.isDirectory()).sort((a,b)=>a.name.localeCompare(b.name)).map(e=>{
      const dir=`tests/fixtures/vaults/${e.name}`;
      return {id:e.name,acceptanceCase:`vault-${e.name}`,expectation:`${dir}/expect.json`,files:walk(dir).map(path=>{const b=readFileSync(join(ROOT,path));return {path:path.slice(dir.length+1),bytes:b.length,sha256:sha(b)}})};
    })};
}
const ref = (p,needle) => `${p}:${read(p).split('\n').findIndex(l=>l.includes(needle))+1}`;
export function renderContract(m) {
  const catalog=read(authorities[0]);
  const sections=catalog.split(/(?=^## )/m).filter(s=>/^## (?:4\.|7\.|8\.|9\.)/.test(s));
  return `# Contrato do vault para os clientes Urbe — UC-3

> Projeção gerada por \`node tools/vault-contract.mjs render\`. Autoridade única: DATA-CATALOG e ADR-0004, não este documento. Não cria formato nem altera decisão consolidada. Manifesto verificável: \`acceptance/vault-manifest.json\`.

## Regras de compatibilidade

| Área | Obrigação existente | Evidência da implementação atual |
|---|---|---|
| Vault | Sem vault.json = legado. formatVersion maior que 2 = vault somente leitura. Leitor não sobrescreve formato futuro. | ${ref('src/persistence/vault-meta.js','var FORMAT_VERSION')}; ${ref('src/persistence/workspace.js',"state==='future'")} |
| Mapa | Ausente v = legado; inteiros 1..4 aceitos; futuro/desconhecido preservado. Mapa v4 continua legível pela 1.x. | ${ref('src/persistence/workspace.js','mapa.v é lido')} |
| Sidecars | v2 tem precedência; nomes v1 preservados durante beta. v2 ilegível/estranho desliga escrita daquele artefato; não apaga. | ${ref('src/persistence/workspace.js','async readSide')} |
| Journal | Recuperação v2 antes de v1; somente versão suportada e documents array. Futuro preservado. Não confundir operação multi-arquivo com transação do SO. | ${ref('src/persistence/workspace.js','journal de uma operação')} |
| Migração | Captura originais antes de escrita; backup restaurável antes da migração; reabrir não duplica migração nem backup. | ${ref('src/persistence/workspace.js','this.originals=new Map')}; tests/vault-format.mjs |
| Backup | manifest.version=1; paths .urbe/ viram urbe/ na cópia; absent registra inexistentes. SHA-256 de texto UTF-8. size é quantidade de unidades UTF-16, NÃO bytes. | ${ref('src/persistence/backup.js','files.push')}; ${ref('src/persistence/backup.js','function mapPath')} |
| Restauração | Cópia ausente ou hash divergente rejeita; absent removidos. Implementação restaura sequencialmente: rejeição pode ocorrer após arquivos anteriores serem gravados. Não alegar atomicidade total. | ${ref('src/persistence/backup.js','async function restore')} |
| Identidade | Nenhum ID injetado nas notas; mapa e sidecar guardam IDs. Rename externo herda somente fingerprint único nos dois lados; vazio/ambíguo não herda. | ${ref('src/persistence/identity.js','function reconcile')}; ${ref('src/persistence/identity.js','function pair')} |
| Fingerprint | CRLF/CR → LF; tira espaços/tabs finais e LF finais. Dois FNV de 32 bits sobre unidades UTF-16, wraparound; tamanho UTF-16 em base36. Não usar SHA nem code points em substituição. | ${ref('src/persistence/identity.js','function normalize')}; ${ref('src/persistence/identity.js','function fingerprint')} |
| GC | Simulação padrão; ativo/lixeira nunca órfão; histórico órfão após 30 dias; lixeira nunca expira sem trashDays; composição fica, só referências removidas. Aplicação recusada em vault readonly. | ${ref('src/persistence/gc.js','function plan')}; tests/gc.mjs |
| Exportação | urbe-export.json formatVersion=1, SHA-256 de bytes e size em bytes; journals excluídos; só preferências permitidas, nunca credenciais IA. Import futuro recusado. | ${ref('src/persistence/export-manifest.js','async function build')}; tests/export-manifest.mjs |
| Arquivos | Textos carregados conforme classificação de artefatos; segmentos ocultos não viram notas. Binários preservados como bytes. Caminhos relativos ao vault com /; adapter inclui .urbe. | src/core/artifacts.js; docs/v2/contracts/persistence-adapter.md |

## Precisão e limites

FATO OBSERVADO: vault-meta.parse aceita qualquer número ≤2 como current; o modo readonly global é acionado para future, não para todo corrupt. identity corrupt é derivado e recriado. Estes detalhes não autorizam ampliar leitores nem reduzir a proteção normativa. Qualquer endurecimento que altere comportamento/compatibilidade exige item e revisão próprios.

DECISÃO CONSOLIDADA: ADR-0004 e DATA-CATALOG permanecem acima dos snapshots. Não há schema C# paralelo. O cliente futuro deve executar o mesmo corpus; datas e IDs gerados devem ser testados por invariantes declaradas, nunca mascarados por normalização geral.

FATO OBSERVADO: aprovação de plugins, configurações, cache/handles, conversas e chaves IA pertencem ao aparelho e têm regras distintas de portabilidade. Um vault físico não contém todo o estado IndexedDB. UC-6 deverá tratar migração desses estados, sem exportar segredos.

## Manifesto de fixtures

Cada um dos ${m.fixtures.length} diretórios tem ID estável, expectativa, caso UC-2 e todos os arquivos (inclusive .urbe e binários) com tamanho em bytes e SHA-256. A expectativa é evidência de teste, não schema normativo. O oráculo inicial continua imutável.

| Fixture | Caso | Arquivos |
|---|---|---|
${m.fixtures.map(f=>`| ${f.id} | ${f.acceptanceCase} | ${f.files.length} |`).join('\n')}

## Projeção literal das regras consolidadas do catálogo

As seções abaixo são extraídas da fonte; qualquer mudança na fonte exige regeneração e revisão do diff. O catálogo contém descoberta histórica e atualizações: a política consolidada da seção 9 prevalece sobre propostas antigas da seção 7 e lacunas históricas da seção 8. Não tratar a seção 8 como novo bloqueio nem copiar uma proposta como decisão.

${sections.join('\n')}
`;
}
export function verifyContract(m,doc) {
  if(JSON.stringify(m)!==JSON.stringify(manifest()))throw new Error('manifesto de vault divergente das fontes/fixtures');
  if(doc!==renderContract(m))throw new Error('contrato de vault divergente da projeção');
  for(const f of m.fixtures)if(!f.files.some(p=>p.path==='expect.json'))throw new Error(`fixture sem expectativa: ${f.id}`);
}
