# ADD-0001 — Portal Web do Ecosystem via GitHub Pages

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-09-30, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Registro de decisão:** DEC-0005 em [`../decisions.json`](../decisions.json). **Implementação:** [ADR-0005](../../adr/0005-portal-web-github-pages.md), [`docs/architecture/portal.md`](../../architecture/portal.md).
> **Relação com o MANIFEST:** adendo posterior. O `MANIFEST.md` continua sendo a fonte normativa principal e **não foi alterado**; este adendo o complementa sem contradizê-lo, resumi-lo ou substituí-lo.
>
> O texto abaixo é a transcrição integral da instrução do proprietário; a única alteração foi envolver os exemplos em blocos de código Markdown. Não editar; mudanças exigem novo adendo ou decisão registrada.

---

O "MANIFEST.md" já enviado continua sendo a fonte normativa principal e deve ser seguido integralmente. A instrução abaixo é um adendo posterior e deve ser incorporada ao planejamento, arquitetura e roadmap sem contradizer, resumir ou substituir o manifesto.

Adendo — Portal Web do Ecosystem via GitHub Pages

Quero que o Ecosystem possua desde o início uma superfície web simples publicada no GitHub Pages.

Essa superfície NÃO é o Ecosystem Hub, NÃO substitui o futuro aplicativo Hub em C# e NÃO deve se tornar dependência de runtime do Urbe, Lunet2D ou de qualquer outro componente.

Ela é um portal humano de desenvolvimento, distribuição, documentação, testes e recuperação.

Objetivo

O proprietário deve poder abrir no celular algo equivalente a:

"https://abnercruz.github.io/Ecosystem/"

e encontrar, de forma clara, o estado dos principais produtos e atalhos para as operações mais comuns.

Exemplo conceitual:

```text
ECOSYSTEM

Apps

Lunet2D
Versão instalada/publicada: ...
Última release: ...
CI: ...
Validação humana: ...
[Baixar APK]
[Ver release]
[Ver roteiro de teste]

Urbe
Versão: ...
CI: ...
[Executar versão Web]
[Baixar APK]
[Ver release]

Ecosystem Hub
Versão: ...
[Baixar]
[Ver release]

Validações pendentes
- ...

Documentação
- MANIFEST
- Architecture
- Roadmap
- ADRs
```

A UI definitiva não precisa seguir exatamente esse desenho. O requisito é a função.

---

Princípio arquitetural

O portal deve ser uma projeção do estado canônico do Ecosystem, nunca uma nova fonte de verdade.

Portanto:

```text
MANIFEST / ecosystem.json / ROADMAP / releases / CI / validation records
                              ↓
                      dados derivados
                              ↓
                        GitHub Pages
```

É proibido criar no site uma segunda versão manual de informações que já possuam autoridade em outro lugar.

Exemplo proibido:

```js
const lunetVersion = "0.4.2";
```

se a versão canônica já existir em manifest ou arquivo de versão.

O site deve progressivamente consumir dados gerados das fontes oficiais.

---

Estrutura

Preferência inicial:

```text
site/
├── index.html
├── app.js
├── style.css
├── assets/
└── data/
    └── ecosystem-status.json
```

Essa estrutura pode ser refinada se houver motivo técnico documentado.

Não colocar um "index.html" solto na raiz de forma que gere ambiguidade entre o portal e o produto Ecosystem.

"site/" representa a superfície web pública.

---

ecosystem-status.json

Planejar uma projeção legível por máquina com dados derivados do estado real.

Exemplo conceitual:

```json
{
  "generatedAt": "...",
  "products": {
    "lunet2d": {
      "version": "...",
      "ci": "...",
      "release": "...",
      "validation": "pending"
    },
    "urbe": {
      "version": "...",
      "ci": "...",
      "release": "...",
      "web": "..."
    }
  }
}
```

Não trate esse formato de exemplo como contrato definitivo sem análise.

Se um contrato formal for necessário, documente-o adequadamente.

O arquivo gerado é cache/projeção.

Não é autoridade.

---

GitHub Actions / Pages

Adicionar ou planejar workflow próprio para publicar "site/" no GitHub Pages.

O processo deve poder evoluir para:

```text
fontes canônicas
↓
CI
↓
gera status
↓
valida consistência
↓
publica GitHub Pages
```

Uma falha ao publicar o portal não pode impedir desnecessariamente o desenvolvimento ou funcionamento dos produtos.

---

Uso para testes

O portal deverá ser a forma mais simples de o proprietário encontrar builds que precisam de validação.

Exemplo:

```text
Validações pendentes

Lunet2D
0.4.8-dev.12
Fase 3 / Editor
[Baixar APK]
[Ver roteiro]
```

Cada build candidata poderá futuramente possuir uma página de validação:

```text
/testing/lunet2d/<build>/
```

contendo:

- versão/build;
- objetivo da mudança;
- pré-condições;
- passos numerados;
- resultado esperado;
- problemas conhecidos;
- link para artefato;
- identificação da tarefa/roadmap/commit/PR relacionados.

Não é necessário implementar imediatamente persistência de botões PASS/FAIL.

O primeiro objetivo é centralizar acesso ao build e às instruções.

---

Regra de validação humana

O portal deve refletir claramente a diferença entre:

```text
IMPLEMENTED
AUTOMATED VERIFIED
HUMAN VALIDATION PENDING
VALIDATED
```

ou estados semanticamente equivalentes que forem formalizados pelo Ecosystem.

CI verde nunca deve ser apresentado como se significasse automaticamente "validado no aparelho".

Isso deve respeitar os "NON-NEGOTIABLES" correspondentes do "MANIFEST.md".

---

Urbe

Como o Urbe possui superfície web, o portal poderá futuramente oferecer:

```text
[Abrir Urbe Web]
```

preferencialmente em uma rota previsível dentro da publicação apropriada.

A migração para o monorepo não deve quebrar a capacidade atual do Urbe de rodar/publicar sua versão web.

A estratégia definitiva de publicação deve ser decidida durante a auditoria/migração, não presumida agora.

---

Lunet2D

Lunet2D é primariamente aplicativo Android/C#.

GitHub Pages não executará o produto Lunet.

O portal servirá para:

- mostrar estado;
- disponibilizar release;
- disponibilizar APK;
- checksum;
- release notes;
- roteiro de teste;
- documentação;
- validações pendentes.

Não criar uma versão web falsa do Lunet apenas para preencher o portal.

---

Hub

Quando o Ecosystem Hub existir, ele assumirá uma experiência mais poderosa:

```text
detectar versão
→ baixar
→ verificar
→ conduzir instalação
→ apresentar testes
→ registrar validação
```

Mesmo assim, o GitHub Pages deve continuar existindo.

Motivo: ele funciona como mecanismo de recuperação independente.

Se o Hub estiver quebrado ou não estiver instalado, o usuário ainda deve conseguir:

- acessar documentação;
- baixar Hub;
- baixar Lunet;
- acessar Urbe Web;
- localizar releases;
- localizar instruções de recuperação/teste.

Portanto:

```text
GitHub
    ↓
fonte técnica

GitHub Pages
    ↓
portal humano / recuperação

Ecosystem Hub
    ↓
control plane completo
```

Os três possuem papéis distintos.

---

Relação com a Fase 0

Não desvie a fundação para construir um portal sofisticado.

Na Fase 0, é suficiente:

1. criar ou reservar "site/";
2. criar um "index.html" simples, funcional e mobile-first;
3. mostrar Ecosystem, Lunet2D e Urbe;
4. disponibilizar links para documentação canônica;
5. documentar claramente que os dados ainda não automatizados são provisórios;
6. configurar ou planejar corretamente GitHub Pages;
7. documentar o modelo futuro de "ecosystem-status.json";
8. adicionar os itens necessários ao ROADMAP;
9. garantir que o portal nunca vire fonte canônica;
10. adicionar checks simples contra divergência quando já houver fonte automática disponível.

Não criar dashboard complexo agora.

Não implementar o futuro Hub em HTML.

Não duplicar funcionalidade que pertence ao Hub.

---

Requisitos adicionais para o primeiro commit

Se o primeiro commit do Ecosystem ainda não tiver ocorrido, incorpore esta decisão nele.

Se ele já tiver ocorrido, faça um commit posterior específico para este adendo.

Em ambos os casos:

- atualize "ARCHITECTURE.md" se necessário;
- atualize "ROADMAP.md";
- atualize "ecosystem.json" caso o portal deva ser registrado como superfície/componente;
- crie ADR apenas se houver uma decisão estrutural que realmente exija ADR;
- não altere o "MANIFEST.md" silenciosamente;
- caso este adendo deva ser incorporado ao "MANIFEST.md", preserve integralmente seu significado e registre a mudança explicitamente.

A mensagem de commit deve deixar claro que se trata da introdução/formalização do portal GitHub Pages, não de uma mudança de arquitetura central.

---

Resultado esperado

Depois dessa alteração, deve existir uma resposta clara para:

«"Estou com meu celular e quero saber o estado do Ecosystem, baixar/testar um aplicativo ou recuperar o próprio Hub. Para onde vou?"»

Resposta:

«GitHub Pages do Ecosystem.»

E deve existir uma resposta diferente para:

«"Quero controlar profundamente o ecossistema, workspaces, capabilities, agentes, instalações, atualizações e integrações."»

Resposta:

«Ecosystem Hub.»

Mantenha essa distinção arquitetural desde o início.
