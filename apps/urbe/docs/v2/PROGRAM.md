# Programa Urbe 2.0

> Estado atual: **DESCOBERTA CONCLUÍDA; SPEC/ROADMAP/TRACEABILITY redigidos, aguardando aprovação do proprietário**. Execução da 2.0 não iniciada.
>
> Este documento define o programa de trabalho. Ele ainda não é o ROADMAP normativo final.

## 1. Tese da 2.0

Urbe 2.0 não é uma coleção de funcionalidades novas. É a consolidação do Urbe como produto e base de software:

- arquitetura coerente;
- autoridades explícitas;
- dados compatíveis e migráveis;
- processo de desenvolvimento rastreável;
- documentação normativa;
- qualidade verificável;
- release controlado;
- espaço seguro para evolução de cidade, editor, IA, páginas e plugins.

A 1.x demonstrou capacidade de entrega rápida. A 2.0 deve preservar essa velocidade sem depender de contexto implícito ou de camadas históricas acumuladas.

## 2. Não objetivos desta etapa

Durante a fase de descoberta:

- não reescrever o app;
- não adotar framework/bundler por preferência;
- não reorganizar pastas sem mapa de dependências;
- não remover legacy sem prova de substituição;
- não alterar formatos persistidos;
- não transformar ideias futuras em features antes de virarem requisitos.

Bugs críticos da 1.x continuam podendo ser corrigidos.

## 3. Fontes de verdade

A estrutura documental planejada é:

1. `docs/v2/REQUIREMENTS.md` — Requirement Ledger.
2. `docs/v2/SPEC.md` — futura especificação canônica, criada somente quando a descoberta estiver madura.
3. `docs/v2/ROADMAP.md` — futuro roadmap executável, derivado integralmente da SPEC.
4. `docs/v2/TRACEABILITY.md` — futura matriz REQ → SPEC → roadmap → teste/gate.
5. `docs/v2/adr/` — decisões arquiteturais significativas.
6. `docs/v2/AUDIT-1X.md` — evidência da linha de base 1.x.

PR, commit e CHANGELOG não substituem requisitos ou decisões normativas.

## 4. Processo obrigatório de concepção

A 2.0 seguirá:

```
IDEIA
  ↓
DESCOBERTA
  ↓
DEFINIÇÃO
  ↓
DECISÕES
  ↓
REQUISITOS
  ↓
ARQUITETURA
  ↓
RISCOS
  ↓
ESTRATÉGIA
  ↓
SPEC CANÔNICA
  ↓
ROADMAP INTEGRAL
  ↓
AUDITORIA DE COBERTURA
  ↓
EXECUÇÃO
```

Nenhuma etapa deve ser pulada apenas para começar a codificar.

## 5. Trilhas de descoberta

As trilhas abaixo são inventário de trabalho de descoberta, não itens comprimidos de roadmap.

### T1 — Produto e UX

Levantar:

- usuários e cenários primários;
- fluxos essenciais;
- papel da cidade no trabalho real;
- editor/PKM;
- páginas/livros;
- IA;
- plugins;
- onboarding;
- mobile e desktop;
- critérios de sucesso da 2.0.

### T2 — Arquitetura e dependências

Mapear:

- responsabilidades reais por arquivo/serviço;
- serviços, comandos e eventos;
- globals e ordem de inicialização;
- consumidores de legacy;
- dependências circulares/implícitas;
- hotspots por acoplamento;
- boundaries alvo.

### T3 — Dados e compatibilidade

Catalogar:

- Markdown/notas;
- `.urbe/mapa.json`;
- journal;
- histórico;
- lixeira;
- composições;
- páginas;
- personalização;
- plugins;
- assets/binários;
- dados locais do aparelho;
- configurações/credenciais de IA.

Para cada formato: autoridade, schema atual, versão, migração, backup, recuperação e compatibilidade.

### T4 — Qualidade e testes

Construir matriz:

- capacidade → teste existente;
- regressão crítica → fixture;
- integração → runner reproduzível;
- E2E → plataforma;
- teste manual → instrução;
- performance → cenário/métrica/budget;
- segurança → propriedade verificável.

### T5 — Segurança

Produzir threat model para:

- vault/filesystem;
- Electron IPC;
- Android bridge;
- plugins;
- IA e API keys;
- HTML incorporado/exportado;
- links externos;
- sincronizadores externos;
- importação/exportação.

### T6 — Performance

Definir baselines para:

- tempo de abertura;
- vault pequeno/médio/grande;
- edição/salvamento;
- busca;
- render da cidade;
- memória;
- Android;
- desktop;
- operação offline.

### T7 — Distribuição e governança

Decidir:

- versionamento;
- política de release;
- channels alpha/beta/stable, se aplicável;
- licença;
- suporte/migração 1.x;
- branch policy;
- issue/PR policy;
- ADRs;
- release notes;
- rollback.

## 6. Esqueleto de fases futuras

Este mapa serve apenas para organizar a descoberta. Não substitui o roadmap detalhado.

- **Fase A — Constituição do projeto:** visão, escopo, requisitos, glossário e regras.
- **Fase B — Governança do repositório:** issues, branches, PRs, ADRs, releases e Definition of Done.
- **Fase C — Baseline 1.x:** auditorias técnicas, dados, UX, performance e segurança.
- **Fase D — Arquitetura alvo 2.0:** boundaries, contratos, modelo de dados e decisões.
- **Fase E — Plano de migração:** ordem de substituição de autoridades, compatibilidade e gates.
- **Fase F — Migração de runtime:** decomposição incremental de legacy e hotspots.
- **Fase G — Evolução do produto:** cidade, editor, IA, páginas, plugins e UX sob a nova estrutura.
- **Fase H — Hardening e release 2.0:** segurança, performance, compatibilidade, documentação e distribuição.

O ROADMAP final deverá decompor cada requisito em itens individualmente verificáveis. Estes títulos não contam como cobertura.

## 7. Gates antes de escrever a SPEC

A descoberta só está madura quando existirem, no mínimo:

- inventário de autoridades e legacy;
- catálogo de dados persistidos;
- mapa de capacidades e testes;
- decisões abertas explicitamente listadas;
- visão e não objetivos aprovados;
- critérios de sucesso;
- riscos principais;
- decisões de plataforma relevantes ou adiamentos explícitos.

## 8. Gate da SPEC

A SPEC só pode ser considerada canônica quando:

- todo requisito IMPLEMENTAR aparece nela;
- decisões abertas restantes estiverem marcadas como adiadas;
- requisitos conflitantes estiverem resolvidos;
- arquitetura e dados tiverem contratos suficientes para orientar execução;
- critérios de aceite forem observáveis.

## 9. Gate do ROADMAP

O ROADMAP só pode ser aprovado após matriz:

```
REQ → seção da SPEC → fase → item → teste/gate
```

Cada REQ IMPLEMENTAR precisa chegar a pelo menos um item concreto.

## 10. Execução futura

Quando o roadmap for aprovado, cada fase seguirá:

```
IMPLEMENTAR
→ TESTAR
→ VERIFICAR
→ CORRIGIR
→ DOCUMENTAR
→ AUDITAR
→ VALIDAR GATE
→ AVANÇAR
```

Nenhum item será concluído apenas porque “há código relacionado”.

## 11. Regra para agentes

Agentes podem implementar, investigar e testar. Eles não podem:

- apagar requisitos para simplificar;
- resumir o roadmap normativo;
- criar nova arquitetura implícita;
- transformar item real em TODO e marcá-lo concluído;
- alterar formato de dados sem decisão/migração;
- publicar versão por efeito colateral não aprovado.

Descobertas novas aumentam o ledger/roadmap; não o encolhem.
