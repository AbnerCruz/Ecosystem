# Transcrição dos E2E — UC-2

Inventário de trabalho sobre os 14 cenários congelados em `oracle.json`. Esta tabela registra transcrição, não paridade C# nem conclusão de UC-2. `cases.json` contém os resultados exatos; a suíte JS original permanece como evidência complementar. Um teste original verde não substitui um caso portável ausente.

| Fonte em `tests/e2e/` | Corpus portável relacionado | Critérios ainda a transcrever |
|---|---|---|
| `app-runtime-visual.e2e.mjs` | `visual-001` a `visual-110`, `ui-visual-save-reload` | Nenhum dos critérios atuais de serialização/edição; execução C# futura permanece aberta |
| `app-runtime.e2e.mjs` | `ui-first-boot`, `ui-create-save-reload`; Markdown/Visual | Boot da fixture e vault de escala S; preview integrado coberto por `ui-integrated-preview` |
| `smoke.e2e.mjs` | `ui-first-boot` | Boot do vault de escala S e contagem documental |
| `routing.e2e.mjs` | `ui-artifact-routing` | Nenhum dos critérios atuais de abertura e resolução; execução C# futura permanece aberta |
| `lifecycle.e2e.mjs` | `ui-document-lifecycle` | Execução com vault de escala S; os critérios documentais atuais têm dados portáveis |
| `gc.e2e.mjs` | `ui-gc-cancel`, `ui-gc-apply`; `gc.plan` | Nenhum dos critérios atuais de confirmação/cancelamento e registro; execução C# futura permanece aberta |
| `fixtures.e2e.mjs` | `vault.scenario`, 11 `browser.vault`, `idb.legacy-city` | Nenhum dos critérios atuais: oito vaults históricos, três futuros e `browser.idb-legacy` transcrevem abertura/salvamento, backup, sidecars, readonly e boot legado; execução C# futura permanece aberta |
| `identity.e2e.mjs` | `identity.text/parse/pair`; fixtures de vault | Rename/move externos no IDB, reabertura/sync, uma casa por documento, geometria de região/asset e vínculo preservados |
| `stable-ids.e2e.mjs` | Fixtures de vault | IDs region/asset, campos de compatibilidade e vínculo mantidos ao recarregar/renomear pasta pelo app |
| `adapters.e2e.mjs` | `storage.scenario` | IndexedDB real: persistência, isolamento de vaults e operações observadas pelo navegador |
| `layout.e2e.mjs` | Fixtures de vault | Reorganização, diálogo manter/desfazer, backup byte a byte, registro e reabertura sem nova reorganização |
| `multi-city.e2e.mjs` | Fixture mesclada e `idb.legacy-city` cobrem partes do domínio | Stores Norte/Sul, três boots idempotentes, IDs/geometria de origem, arquivar sem apagar stores |
| `map-writer.e2e.mjs` | `ui-create-save-reload` cobre persistência explícita | Persistência automática de documento/mundo. O critério de escritor único exige instrumentação própria da pilha C#; não portar regex de stack JS como regra de produto |
| `zip.e2e.mjs` | Testes JS congelados; ainda sem operação portável ZIP | Export/import, manifesto/hashes, binários, preferências sem chaves, adulteração cancelada e versão futura recusada |

Próxima fatia: fechar os critérios restantes dos cenários já transcritos antes de acrescentar famílias maiores. Depois, fixtures/identidade/layout, adapters/multi-city e ZIP. Cenários físicos e permissões do SO são aceite de UC-23/24/25/29; seus protocolos devem ser definidos sem fabricar execução humana.

Os caminhos e IDs dos testes permanecem no inventário congelado. O cliente C# poderá usar seus próprios comandos e seletores para produzir os mesmos resultados; equivalência de resultado não exige copiar Electron, Capacitor, IndexedDB ou nomes de módulos JS.
