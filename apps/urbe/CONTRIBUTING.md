# Contribuindo com o Urbe

Este documento define o fluxo de desenvolvimento do repositório. Durante a preparação da 2.0, o objetivo é preservar velocidade sem depender de contexto implícito.

## 1. Antes do código

Mudanças não triviais devem possuir uma origem rastreável:

- bug: issue com reprodução e resultado esperado;
- feature: requisito/issue com problema, objetivo e aceite;
- arquitetura: requisito/issue + ADR quando a decisão for significativa;
- migração 2.0: REQ e item do futuro ROADMAP quando ele existir.

Correções pequenas e óbvias podem usar apenas issue/PR, mas não devem introduzir decisão arquitetural silenciosa.

## 2. Requirement Ledger

Requisitos da 2.0 vivem em `docs/v2/REQUIREMENTS.md`.

Um REQ não é apagado porque deixou de ser conveniente. Estados válidos:

- IMPLEMENTAR;
- ADIADO;
- REJEITADO;
- SUBSTITUÍDO;
- FORA DE ESCOPO.

Estados diferentes de IMPLEMENTAR precisam de justificativa.

## 3. Branches

Branches são temporárias.

Formato recomendado:

```
feat/REQ-xxx-descricao
fix/ISSUE-xxx-descricao
refactor/REQ-xxx-descricao
docs/REQ-xxx-descricao
chore/descricao
```

Uma branch antiga só deve ser apagada após confirmar que o trabalho foi incorporado ou conscientemente descartado.

O histórico permanente pertence a commits, PRs, issues, requisitos e ADRs.

## 4. Pull requests

Todo PR relevante deve informar:

- problema/necessidade;
- REQ/issue relacionada;
- o que muda e o que não muda;
- impacto em dados;
- impacto em plataformas;
- impacto em performance e segurança;
- testes executados;
- documentação alterada;
- dívida/legacy removida ou criada;
- instruções de validação manual quando necessárias.

Evite PRs que misturem feature, refatoração ampla e mudança de formato de dados sem necessidade técnica.

## 5. Regra para legacy

Até a arquitetura 2.0 estar definida:

- não adicionar novas “camadas de versão” ao fim de `src/app.js`;
- novas regras de domínio devem preferir serviços canônicos;
- código em `app.js`/`legacy` só deve crescer quando for bugfix inevitável, adapter temporário ou parte explicitamente justificada da migração;
- remover legacy exige identificar consumidores, substituto canônico e teste de regressão.

Refatorar significa trocar autoridade, não apenas mover funções para outro arquivo.

## 6. Dados e compatibilidade

Não altere formatos persistidos sem registrar:

- formato afetado;
- versões existentes;
- leitura de dados antigos;
- escrita nova;
- estratégia de migração;
- possibilidade de rollback;
- fixtures/testes.

Local-first e recuperação de dados têm precedência sobre limpeza estética.

## 7. Testes

Antes de abrir PR:

```bash
npm test
```

Quando aplicável, inclua:

- teste unitário;
- teste de contrato;
- integração;
- E2E;
- performance;
- validação manual.

Um teste citado somente no texto do PR, mas impossível de reproduzir a partir do repositório, não deve ser o único gate de uma capacidade crítica.

## 8. Arquitetura e ADR

Crie ADR quando uma mudança:

- altera boundary entre subsistemas;
- escolhe tecnologia estrutural;
- muda modelo de dados;
- muda política de segurança;
- muda estratégia de release;
- cria ou remove autoridade;
- torna uma decisão futura significativamente mais cara.

ADRs ficam em `docs/v2/adr/`.

## 9. Versionamento e releases

Até a política 2.0 substituir o workflow atual:

- não alterar versão como efeito colateral de um PR comum;
- version bump deve ser intencional e explicitado;
- PR deve dizer se pretende release;
- documentação de release deve corresponder ao código realmente entregue.

A separação definitiva entre integração e publicação é requisito da 2.0.

## 10. Definition of Done

Uma mudança só está concluída quando todos os critérios aplicáveis forem verdadeiros:

- implementação real;
- integração no fluxo de produção;
- compilação/build aplicável;
- testes relevantes;
- comportamento esperado;
- documentação atualizada;
- migração/compatibilidade tratada;
- ausência de placeholder relevante;
- ausência de regressão conhecida;
- critérios de aceite satisfeitos;
- validação humana realizada quando exigida.

“Existe código” não significa “concluído”.

## 11. Agentes de IA

Agentes podem investigar, implementar e testar, mas devem trabalhar sob os mesmos contratos.

Eles não podem:

- resumir requisitos para reduzir escopo;
- omitir item porque parece difícil;
- criar nova arquitetura sem registrá-la;
- marcar scaffold/TODO como concluído;
- alterar persistência silenciosamente;
- usar o PR como única fonte de decisão.

Novas necessidades descobertas devem aumentar o ledger/roadmap, nunca encolhê-los silenciosamente.
