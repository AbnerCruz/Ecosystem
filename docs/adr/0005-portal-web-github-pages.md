# ADR-0005 — Portal web via GitHub Pages como projeção

## Status

Proposto — a existência, o papel e as restrições do portal foram decididos pelo proprietário (DEC-0005, [ADD-0001](../governance/addenda/ADD-0001-portal-github-pages.md)); as escolhas técnicas abaixo aguardam ratificação em DEC-0001.

## Contexto

O proprietário decidiu (ADD-0001) que o Ecosystem terá desde o início um portal web no GitHub Pages: superfície humana de desenvolvimento, distribuição, documentação, testes e recuperação; projeção do estado canônico, nunca fonte de verdade; nunca dependência de runtime de nenhum componente; distinto do Hub. Este ADR registra as decisões técnicas que o adendo deixou em aberto e que tocam itens de MANIFEST §29 (formato de manifest, linguagem excepcional, packaging).

## Problema

1. Como registrar o portal em `ecosystem.json` sem distorcer os tipos de MANIFEST §6.
2. Que linguagem usar, dado que C# é o padrão para novos aplicativos (MANIFEST §4.1).
3. Como garantir que o portal nunca vire segunda autoridade (NN-001).
4. Qual contrato de projeção adotar.
5. Como publicar sem acoplar a publicação ao desenvolvimento.

## Opções

- Registro: (a) novo tipo `portal`; (b) tipo `product`; (c) não registrar.
- Linguagem da UI: (a) HTML/CSS/JavaScript estáticos sem build; (b) C# via Blazor WebAssembly.
- Dados: (a) projeção gerada no CI e não versionada, validada contra as fontes; (b) projeção versionada no repositório; (c) dados escritos no HTML.
- Gerador da projeção: (a) C# *file-based app*; (b) JavaScript/Node; (c) shell + jq.

## Decisão

- **Registro:** componente `portal`, de tipo novo `portal` (adição compatível ao enum de `ecosystem.schema.json`, sem mudar `schemaVersion`). `ecosystem.json` ganha `ecosystem.repository` (base única dos links derivados) e o campo opcional `publicUrl`.
- **UI:** HTML/CSS/JavaScript estáticos, sem build e sem dependências, em `site/` — estrutura preferida pelo proprietário. É uma exceção à linguagem padrão, justificada porque o proprietário especificou essa estrutura, o portal é uma página estática sem lógica de domínio e Blazor WASM traria download de runtime de vários MB, carregamento lento no celular e build obrigatório (MANIFEST §49).
- **Gerador:** C# *file-based app* em `site/generator/GenerateStatus.cs`, seguindo a linguagem padrão e o mesmo runtime dos checks (ADR-0003). Não é publicado.
- **Dados:** a projeção `site/data/ecosystem-status.json` é gerada no CI e **não versionada** (`.gitignore`), para que não exista uma cópia commitada capaz de divergir. Contrato: `docs/contracts/schemas/ecosystem-status.schema.json`, com `authority: false` e proveniência (`derived` | `not-available`) em cada dado.
- **Fiscalização:** `CHK-PORTAL` (literais canônicos nos arquivos publicados; projeção contra schema e fontes; `VALIDATED` exige evidência) e `CHK-BOUNDARIES` (nenhum componente depende do portal).
- **Publicação:** workflow próprio `pages.yml`, disparado só na branch padrão: checks → gera → valida → publica. Falha nele não bloqueia outros workflows nem os produtos.

## Consequências

- O portal só mostra o que as fontes canônicas contêm; na Fase 0, versão, release e CI dos produtos aparecem como "não disponível", com o motivo, até existirem fontes automáticas (P1-9, P1-11).
- A projeção é um segundo consumidor dos registros de governança, o que exercita os contratos da fundação antes do Hub.
- `CHK-PORTAL` duplica, de forma mínima, a regra "último handoff por tarefa" do gerador para detectar divergência; se essa regra crescer, deve ir para um contrato compartilhado entre os dois.
- O GitHub Pages precisa ser habilitado nas configurações do repositório com fonte "GitHub Actions" (ação do proprietário).

## Alternativas rejeitadas

- **Tipo `product`:** o portal não tem domínio próprio nem lifecycle de release; apresentá-lo como produto confundiria o mapa (MANIFEST §6.1).
- **Não registrar:** deixaria uma superfície pública implícita (NN-021).
- **Blazor WebAssembly:** custo desproporcional para uma página estática; contraria a estrutura pedida pelo proprietário.
- **Projeção versionada:** criaria uma cópia commitada que envelhece e pode ser lida como autoridade (NN-001).
- **Dados no HTML:** proibido pelo ADD-0001.
- **Gerador em Node ou shell:** exceção desnecessária à linguagem padrão.

## Referências

ADD-0001; DEC-0005; MANIFEST §4.1, §6, §10, §29, §49; NN-001, NN-003, NN-017, NN-021; ADR-0002, ADR-0003, ADR-0004; `docs/architecture/portal.md`.
