# Portal web do Ecosystem (GitHub Pages)

> **Autoridade:** documento de arquitetura, subordinado ao `MANIFEST.md`, ao adendo do proprietário [ADD-0001](../governance/addenda/ADD-0001-portal-github-pages.md) e ao [ADR-0005](../adr/0005-portal-web-github-pages.md).
> Componente `portal` em [`ecosystem.json`](../../ecosystem.json). URL pública: https://abnercruz.github.io/Ecosystem/

## 1. Três superfícies, três papéis

| Superfície | Papel | Pergunta que responde |
|------------|-------|-----------------------|
| **GitHub** | fonte técnica: código, histórico, CI, releases, ADRs | "Onde está a verdade?" |
| **Portal (GitHub Pages)** | portal humano de desenvolvimento, distribuição, documentação, testes e **recuperação** | "Estou com o celular e quero saber o estado do Ecosystem, baixar/testar um app ou recuperar o próprio Hub. Para onde vou?" |
| **Ecosystem Hub** (C#, Fase 3+) | control plane completo | "Quero controlar profundamente o ecossistema, workspaces, capabilities, agentes, instalações, atualizações e integrações." |

O portal **não** é o Hub, não o substitui, não implementa o Hub em HTML e não duplica funções que pertencem ao Hub (Control Plane, Past/Now/Next, workspaces, capabilities). **Exceção decidida pelo proprietário (DEC-0007):** o portal apresenta, como projeção somente leitura, as decisões pendentes e as validações humanas pendentes, sempre com o objeto a decidir/validar (§3.1); o Hub mantém a superfície completa de decisões (MANIFEST §35). Ele continua existindo depois que o Hub existir, como mecanismo de recuperação independente: com o Hub ausente ou quebrado, deve continuar sendo possível acessar a documentação, baixar Hub e Lunet2D, acessar o Urbe Web, localizar releases e localizar instruções de recuperação e teste.

## 2. Nunca fonte de verdade, nunca dependência

```text
MANIFEST / ecosystem.json / ROADMAP / releases / CI / registros de validação
                              ↓
          site/generator/GenerateStatus.cs  (no CI)
                              ↓
          site/data/ecosystem-status.json   (projeção, authority: false)
                              ↓
          site/index.html + app.js          (apenas renderiza)
                              ↓
                        GitHub Pages
```

- Nenhum dado canônico é escrito à mão em `site/` (ex.: proibido `const lunetVersion = "0.4.2"`). `CHK-PORTAL` recusa versões, IDs de decisão, handoff, invariante e fase escritos literalmente nos arquivos publicados.
- A projeção não é versionada (`.gitignore`): é gerada a cada publicação. Se existir localmente, `CHK-PORTAL` a valida contra o schema e contra as fontes canônicas (componentes, fase, repositório, validações pendentes, documentos).
- Todo dado projetado declara proveniência: `derived` (com a fonte citada) ou `not-available` (com o motivo). O portal mostra "não disponível" em vez de inventar.
- Nenhum componente pode declarar dependência do portal (`CHK-BOUNDARIES`). Urbe, Lunet2D e Hub nunca o consultam em runtime.

## 3. Contrato da projeção

Autoridade do formato: [`docs/contracts/schemas/ecosystem-status.schema.json`](../contracts/schemas/ecosystem-status.schema.json) (`ecosystem/contracts/ecosystem-status/1`). Resumo:

| Campo | Conteúdo |
|-------|----------|
| `kind`, `authority` | sempre `projection` / `false` |
| `source` | repositório, ref, commit e lista de arquivos canônicos lidos |
| `ecosystem` | nome, fase e resultado dos checks de consistência do commit projetado |
| `components[]` | ID, nome, tipo, status, descrição, links (repositório, releases, web) e os dados `version`, `release`, `ci` com proveniência; `validation.state` |
| `pendingValidations[]` | validações humanas pendentes derivadas dos registros canônicos (hoje, handoffs) |
| `docs[]` | documentação canônica com link |

O formato de exemplo do adendo (`products.lunet2d.version` etc.) foi analisado e substituído por este contrato porque (a) cada dado precisa carregar proveniência para que "não disponível" nunca vire um valor inventado; (b) todos os componentes do `ecosystem.json` são projetados, não só produtos, para que a verificação de divergência seja total; (c) validação humana tem estados próprios, separados de CI.

### 3.1 Decisões e validações pendentes, com objeto (DEC-0007)

- **Decisão a tomar:** `decisions.json` com `status: pending` e `related` apontando para o(s) documento(s)/artefato(s) a revisar. O portal mostra título, pergunta, alternativas com consequências, recomendação do agente e o objeto como botões.
- **Validação humana pendente:** verificação `kind: human`, `result: pending` em um handoff, com `object` (caminho do repositório ou URL).
- Sem objeto, a pendência não é registrável: `CHK-DECISIONS` e `CHK-HANDOFFS` falham, o gerador aborta e `CHK-PORTAL` recusa uma projeção que omita qualquer pendência.
- O portal **não** recebe respostas: o proprietário responde ao agente, que registra a decisão e a fonte persistida.

Evolução incompatível incrementa `schemaVersion` e exige ADR.

## 4. Estados de validação

O portal mostra CI e validação humana em linhas **separadas**. Estados de validação de build: `UNKNOWN` (sem registro), `IMPLEMENTED`, `AUTOMATED_VERIFIED`, `HUMAN_VALIDATION_PENDING`, `VALIDATED` — definidos em [`definition-of-done.md` §4](../governance/definition-of-done.md). CI verde leva no máximo a `AUTOMATED_VERIFIED`; `VALIDATED` exige evidência de validação humana (`CHK-PORTAL`, NN-017).

## 5. Publicação

Workflow próprio [`.github/workflows/pages.yml`](../../.github/workflows/pages.yml), separado do workflow de consistência:

```text
push na branch padrão (ou execução manual)
↓ checks de consistência        (falha → não publica; o site anterior continua no ar)
↓ gera a projeção
↓ valida a projeção (CHK-PORTAL)
↓ monta o artefato: site/ sem generator/
↓ publica no GitHub Pages
```

Uma falha de publicação não bloqueia desenvolvimento: o workflow não é pré-requisito de nenhum outro, e os produtos nunca dependem do portal.

**Pré-requisito do proprietário:** em *Settings → Pages*, definir *Source* = **GitHub Actions**.

O workflow valida também o estado efetivo do Pages via API antes de publicar: `build_type` precisa ser `workflow`. Isso evita um falso positivo em que o workflow customizado termina verde, mas a publicação legada da branch continua servindo `README.md`. Antes do upload, o artefato é recusado se não contiver `index.html`, `app.js`, `style.css` e a projeção gerada na raiz esperada, ou se contiver `README.md`. Depois do deploy, um smoke test acessa a URL pública com cache-buster do commit e só considera a publicação válida se o HTML servido contiver o entrypoint do portal.

## 6. Estrutura

```text
site/
├── index.html           estrutura estática, mobile-first
├── app.js               busca data/ecosystem-status.json e renderiza (sem dependências)
├── style.css            tema claro/escuro
├── generator/           gerador C# da projeção (não publicado)
└── data/                gerado no CI (não versionado)
```

`assets/` será criado quando houver o primeiro asset (ADR-0004: sem diretórios vazios).

Pré-visualização local:

```bash
dotnet run site/generator/GenerateStatus.cs
python3 -m http.server -d site 8000   # qualquer servidor estático
```

## 7. Evolução planejada (ver ROADMAP)

- **Releases e artefatos (P1-9):** versão, última release, APK, checksum e release notes de cada produto, derivados das releases do GitHub — nunca digitados.
- **Urbe Web (P1-10):** rota previsível para abrir o Urbe Web, decidida durante a auditoria/migração do Urbe sem quebrar sua publicação atual. Não presumida agora.
- **Lunet2D:** o GitHub Pages não executa o Lunet2D; o portal oferece estado, release, APK, checksum, release notes, roteiro de teste, documentação e validações pendentes. Nenhuma versão web falsa.
- **Páginas de validação (P1-11):** `/testing/<componente>/<build>/`, geradas de registros canônicos de validação, com versão/build, objetivo, pré-condições, passos numerados, resultado esperado, problemas conhecidos, link para o artefato e tarefa/roadmap/commit/PR. Persistência de PASS/FAIL não é objetivo inicial.
- **Hub (P3-x):** o portal passa a listar as releases do Hub para instalação e recuperação.
