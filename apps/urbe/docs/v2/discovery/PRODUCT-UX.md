# Produto e UX — Urbe 1.8.2-beta (base da 2.0)

> Descoberta da issue #33 (REQ-011, 083–087). **[F]** fato (README/CHANGELOG/tutorial/código), **[I]** inferência, **[P]** proposta, **[C]** consolidada.

## 1. Proposta de valor [F→I]
"Suas notas como uma cidade": Markdown/PKM local-first onde nota = casa, pasta = bairro, `[[link]]` = rua, sobre terreno procedural vivo (clima, luz, moradores, fauna, eventos), com editor, páginas/livros, assistente de IA e personalização/plugins. Distribuído como Web/PWA, Windows (Electron) e Android (Capacitor).

## 2. Capacidades atuais (a preservar) [F]
- **Cidade/aquário:** terreno por placas tectônicas, rios, biomas, mapa, dia/noite, moradores, fauna, clima, eventos (`world/life.js`), modo Ciclo de 24 min; comandos `city.fit`, `city.reorganize`, `world.event.*`.
- **Editor:** modos Visual e Fonte, tabelas, callouts, tarefas, listas aninhadas, tags e propriedades, abas, painel dividido, localizar/substituir, versões anteriores, lixeira, menu `/`, bolha de formatação, undo/redo, outline; **Matemática** LaTeX (KaTeX) com editor visual.
- **Páginas/livros:** estúdio com 34 blocos, layout livre (19 peças, 24 composições), 13 modelos, 10 temas, exportação HTML de arquivo único ou PDF; **Composições** juntam várias notas.
- **Assistente de IA:** agentes (geral, pesquisador, escritor, organizador…), aprovação com diff, plano, subagente, memória, desfazer.
- **Personalização:** `tema.json`, temas salvos, estilos CSS, texturas, plugins, modo seguro.
- **Tutorial:** 46 notas na primeira abertura, com restauração.
- **Atalhos:** Ctrl+K paleta, Ctrl+P quick-open, Ctrl+F, Ctrl+N, Ctrl+S, Ctrl+W, Ctrl+\\, Ctrl+Shift+M/F (`core/keymap.js:28-38`).
- **Configurações:** Personalização, Tutorial, Organizar bairros, Exportar tudo (.zip), Importar arquivos, Pasta no dispositivo, Assistente, Páginas, Diagnóstico de toques, Procurar atualização (`ui/settings.js`).

## 3. Usuários e cenários [I → P]
| Persona | Cenário essencial | Plataforma |
|---|---|---|
| Autor/PKM individual | escrever e ligar notas, ver o mapa, nunca perder dados | Windows/Web |
| Estudante/pesquisador | notas com LaTeX, páginas/livros, exportar PDF | Windows/Android |
| Usuário móvel | capturar notas rápidas, sincronizar via pasta externa | Android |
| Personalizador/desenvolvedor | tema, CSS, plugin, IA como assistente | Windows/Web |
| Proprietário/mantenedor | evoluir com segurança, migrar 1.x→2.x | todas |

## 4. Fluxos essenciais observados [F]
1. **Primeira abertura:** cria `Tutorial/` e abre "Comece aqui" se a cidade estiver vazia; Android mostra diálogo de acesso a todos os arquivos (`app.js:5584-5596`); desktop abre `Documentos\Urbe`.
2. **Erro de boot:** diálogo "Não consegui abrir o Urbe" com tentar de novo, outra pasta ou armazenamento interno (`app.js:5610+`).
3. **Escrita:** nota → casa; edição → debounce 900 ms → disco; indicador de salvamento (ponto vermelho em erro).
4. **Recuperação:** journal, lixeira, versões anteriores, Exportar tudo (.zip).
5. **Edição externa:** perceptível na hora no Windows (chokidar), ao voltar ao app no Android; edição local não salva vence.
6. **IA:** conectar modelo (chave) → agente → ferramentas com prévia/diff → aprovar, recusar ou "aprovar tudo" → desfazer.
7. **Plugin:** criar por modelo (Básico, Diário, Mapa, Assistente) → "Ligar plugin?" → hash; mudou → "Revisei, ligar".
8. **Páginas:** estúdio → blocos ou layout livre → prévia responsiva → exportar HTML/PDF/impressão.
9. **Atualização:** Windows "Reiniciar e atualizar"; Android "Baixar e instalar" (manual); Web banner de recarga.
10. **Ajuda:** `Solução de problemas`, `?seguro=1`, Diagnóstico de toques.

## 5. Achados de produto/UX
- [F] `tests/tutorial.mjs` exige que o Tutorial trate "Pede aprovação", "Modo seguro" e "Instalar"; tutorial avisa sobre SmartScreen e APK de fonte desconhecida (`Instalar como aplicativo.md`).
- [F] Android: pasta fixa, sem seletor (`app.js:3280`); app instalado tem **um vault** (`bridge.js:117-123`); navegador suporta várias cidades, migradas para `Cidades/<nome>`.
- [F] Sem E2E, fluxos Android (permissão, voltar, atualização) só têm mocks.
- [I] O diálogo de aprovação de plugin subestima o alcance (THREAT-MODEL §5).
- [I] Migração 1.x→2.x precisa de comunicação clara no app (REQ-082, REQ-085).
- [I] Acessibilidade nunca foi levantada (REQ-086); i18n não é demanda (REQ-087).

## 6. Critérios de sucesso da 2.0 [P → gate G6, REQ-083]
| # | Critério | Verificação |
|---|---|---|
| S1 | Nenhum dado do usuário perdido ao abrir vault 1.x na 2.x (fixtures históricos) | testes de migração (G1) |
| S2 | Versão desconhecida nunca sobrescreve/descarta arquivos (proteção forward) | testes forward (G1) |
| S3 | Um vault de 500 notas abre e digita sem travar em D2 dentro dos budgets do baseline | harness (G5) |
| S4 | Caminho crítico (abrir → criar nota → salvar → recarregar → ver na cidade) passa por E2E em web e Electron | E2E (G4) |
| S5 | Plugin/IA/HTML têm trust boundaries documentados e testados | threat model + testes (G3) |
| S6 | Release só sai por tag com testes verdes; versão única | CI (G4) |
| S7 | `app.js` sem regra de domínio nem camadas de versão; adapters legacy removidos ou justificados | métrica estrutural + gates (G2) |
| S8 | Recuperação (journal, lixeira, versões, backup de migração) acessível e documentada | manual + teste (G6) |
| S9 | Tutorial/documentação coerentes com o produto | `build-tutorial --check` ampliado (G6) |
| S10 | Requisitos rastreáveis REQ→SPEC→ROADMAP→teste | verificador (G0) |

## 7. Não objetivos da 2.0 [C]
Reescrita geral; framework/bundler (ADR-0001); launcher multi-app (REQ-033); isolamento de plugins (REQ-052); i18n (REQ-087); iOS/macOS/Linux (REQ-078); nuvem/sync próprio; telemetria; novas features de cidade/IA/páginas fora do ledger (REQ-011/012).

## 8. Regra de evolução de produto [C]
Cidade, editor/PKM, IA, páginas, plugins e UX continuam no escopo (REQ-011), mas **cada nova capacidade nasce como REQ** e não pode ampliar dívida (REQ-012).
