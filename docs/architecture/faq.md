# Perguntas-chave do Ecosystem

> **Autoridade:** navegação. Este arquivo **resume**; nunca substitui as fontes normativas (NN-010): `MANIFEST.md`, [`product-model.md`](product-model.md), [`distribution.md`](distribution.md), ADRs e contracts. Em caso de divergência, a fonte vence e este arquivo deve ser corrigido.
> Responde às perguntas que um agente novo deve conseguir responder sem depender de conversa anterior (ADD-0002 §22).

**1. O que é o Ecosystem Hub?** Control Plane, Host geral, Registry e Launcher do ecossistema: observação, administração e ponto de entrada para produtos, Tools e Workspaces. Nunca dependência essencial dos produtos (NN-003, NN-023). Não existe ainda (MANIFEST §5.3, Fase 3+).

**2. O que é um Product?** Aplicativo ou sistema com domínio próprio e valor independente: Lunet2D, Urbe, Hub (MANIFEST §6.1).

**3. O que é um Product Shell?** A superfície especializada e Host de um Product: representa seu domínio, navegação, biblioteca, projetos, integrações e capabilities, com experiência própria, sem possuir as Tools reutilizáveis que hospeda. Ver [`product-model.md`](product-model.md) §2.

**4. O que é um Host?** Ambiente capaz de hospedar uma interface ou capability; oferece contexto e serviços sem exigir que a ferramenta conheça sua implementação (MANIFEST §6.2). O Hub e o Product Shell de cada produto são Hosts.

**5. O que é Context?** O escopo atual em que uma operação, Tool, Workspace ou Agent trabalha (Ecosystem → Produto → Projeto/Vault → Ferramenta/documento). Só o conceito existe; o contrato é da Fase 2/5. Ver [`product-model.md`](product-model.md) §4.

**6. O que é uma Capability?** Contrato versionado que representa uma capacidade fornecida por algum componente (MANIFEST §6.3, NN-006).

**7. O que é uma Tool?** Capability com operação de usuário independente e interface própria; abrível standalone quando o domínio permite (MANIFEST §6.4).

**8. O que é um Workspace?** Superfície de trabalho que organiza contexto, ferramentas e estado para uma atividade (MANIFEST §6.7).

**9. O que é um Service?** Capability primariamente operacional, sem interface própria (MANIFEST §6.5). Nenhum Service compartilhado deve ser criado antes de haver consumidores reais e redução de complexidade (NN-020, NN-022).

**10. Qual a diferença entre arquitetura e distribuição?** Existir no monorepo ou no Registry não significa estar incluído, instalado, visível, público, gratuito, adquirível ou utilizável em determinado Host. Quem decide isso é a distribuição (Distribution Profile, conceito futuro). Ver [`distribution.md`](distribution.md) §1–§2.

**11. Um Product pode ser publicado sem o Hub?** **Sim, e deve poder.** NN-023.

**12. O Hub pode permanecer privado?** **Sim.** `Ecosystem Hub: private/internal` com `Lunet2D` e `Urbe` públicos é cenário suportado; o proprietário pode nunca distribuir o Hub (NN-023).

**13. O Lunet2D pode ser distribuído como aplicativo/plataforma completa?** Sim: é a visão do proprietário — plataforma de criação, desenvolvimento, execução, extensão, distribuição e comunidade para jogos 2D em C#, integrada ao Ecosystem mas distribuível e utilizável de forma independente. É visão, não estado atual. Ver [`product-vision.md`](product-vision.md).

**14. O que é Lunet Store?** O catálogo/marketplace do Lunet: conteúdo disponível para adquirir/instalar (assets, plugins, templates, packages, Tools). Visão futura, nada implementado. Ver [`distribution.md`](distribution.md) §6.

**15. Qual a diferença entre Store e Library?** Store = conteúdo disponível para adquirir/instalar. Library = conteúdo que o usuário possui, instalou ou tem direito de usar ([`distribution.md`](distribution.md) §5).

**16. Assets, Plugins, Templates, Packages e Tools podem fazer parte do catálogo?** Sim. "Asset Store" é uma área de UX; tecnicamente assets são uma categoria do catálogo geral, sem uma segunda infraestrutura.

**17. Como capabilities externas aparecem dentro de um Product Shell?** Por descoberta no Capability Registry, sujeitas a compatibilidade, permissões e estado de instalação/distribuição, apresentadas pela superfície de UX chamada Connections. A Tool não conhece o Host (NN-007).

**18. O que significa Connections?** Experiência de usuário no Product Shell para ver e ligar capacidades externas (Editor, Sprite Studio, Agent Workspace, GitHub, Audio Studio…). Connections = UX; Capabilities = mecanismo arquitetural; não é um sistema paralelo ([`product-model.md`](product-model.md) §5).

**19. Qual é a estratégia first-party?** *First-party by default; external distribution by choice*: o proprietário pode ter sua própria plataforma de distribuição, downloads, atualizações, catálogo, identidade, biblioteca, licenças, commerce, marketplace e comunidade; plataformas externas não são a autoridade arquitetural e podem receber Starter, divulgação, edição limitada ou canal de aquisição ([`distribution.md`](distribution.md) §7). Nada disso está implementado.

**20. Por que a migração não deve executar a arquitetura futura imediatamente?** Porque migrar preserva comportamento e histórico, e refatorar/extrair/redesenhar são tarefas separadas (NN-012, NN-013). A sequência é: inventariar → importar → preservar histórico → restaurar build, testes e releases → validar → provar ausência de regressão → classificar candidatos → extrair/modernizar gradualmente. Só se conhece o código real depois dos inventários; extrair antes disso cria abstrações sem consumidor (NN-020, NN-022). Ver [`docs/migration/README.md`](../migration/README.md) §7.
