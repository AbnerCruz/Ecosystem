# ADD-0019 — Ordem do proprietário: Urbe C# utilizável sem migração histórica como bloqueador

**Data:** 2026-10-09. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0042. **Escopo:** novo cliente Urbe C# no Ecosystem, primeiro beta opt-in de uso próprio e direção para o produto final. **Prioridade imediata:** Issue #386 (vault físico/Android), UC-18 (Editor/Explorer), UC-19 (Cidade), UC-25 (Android), UC-31 (corte posterior).

## Ordem expressa do proprietário

O proprietário esclareceu que **quer migração de funcionalidades** ("tinha no app anterior, então tem que ter no app novo"), porém **não quer migração obrigatória de estado/dados de instalações antigas** ("o usuário fazia isso e a atualização tem que ser compatível com a versão atual"). Declarou ser o único usuário da beta, possuir poucos arquivos `.md` importantes e não desejar que compatibilidade histórica atrase a utilização. Autorizou explicitamente registrar e comunicar essa ordem aos agentes.

**Decisão consolidada:** transportar o **produto e suas funcionalidades** de JavaScript para C#, sem condicionar o beta nem o corte futuro a reconstruir configurações, banco de dados, história, lixeira, posições antigas das casas, identidades legadas, formatos de sidecars ou mecanismos de atualização de instalações anteriores. O usuário pode **fazer backup manual e copiar os `.md` para um vault novo**. A compatibilidade entre versões/instalações antigas não é requisito de aceite do novo cliente.

### Obrigatório — não é negociável

1. **Paridade funcional** do Urbe anterior no produto C# final: Editor Markdown Fonte/Visual, Explorer, cidade/habitações/bairros/estradas, links, páginas, matemática, personalização, IA, ferramentas e demais funcionalidades do escopo; corrigir bugs, não copiá-los. Um **beta utilizável** com escopo essencial pode ser distribuído opt-in antes da paridade total, identificado claramente como beta, sem substituir silenciosamente o produto publicado.
2. **Pasta física do usuário é fonte da verdade dos conteúdos.** `.md` comuns devem abrir e editar sem converter para formato proprietário, offline. Priorizar agora fluxo de ponta a ponta **selecionar/criar pasta → abrir nota → editar → salvar no arquivo real → fechar/reabrir aplicativo → confirmar conteúdo no disco**. Inclui criar, mover, renomear e excluir de modo seguro.
3. Estado **novo** produzido no C# precisa continuar funcionando: metadados do mundo atual (.urbe), opções e dados necessários à operação presente, escrita segura, não sobrescrever arquivos desconhecidos, tratamento de erros e integridade básica. **Não confundir persistência operacional do novo app com migração histórica**; a primeira continua obrigatória.
4. **Não apagar o trabalho existente**, nem modificar/destruir automaticamente vaults antigos. Não sobrescrever dados legados apenas porque estão presentes. Em caso de vault legado/incompatível, oferecer **seleção de outro diretório ou cópia manual dos .md para vault limpo**; deixar o original intacto. Import/export e retrocompatibilidade já implementados podem permanecer sem bloquear a entrega; não criar novas rodadas extensas de migração histórica.
5. Preservar o cliente 1.8.4-beta distribuído como referência/recuperação enquanto C# é testado; APK C# experimental separado/opt-in com link direto. G-C3/G-C4 e validações reais continuam honestos (não alegar testes físicos não realizados), assim como CI, segurança, permissão de pasta e processo de PR crítico.

### Deixam de bloquear

- Compatibilidade automática entre vaults e formatos 1.x/2.x legados e o novo C#.
- Migração de `IndexedDB` antigo, `localStorage`, configurações, layouts, posições, lixeira, histórico, páginas/temas obsoletos, IDs e sidecars históricos.
- Export → reinstalar → importar → validar **todos os dados antigos** como requisito universal do corte C#; bastam cópia manual dos `.md` e uso de vault limpo para o proprietário.
- Compatibilidade de atualização in-place ou rollback automático JS→C#; já não exigida. Preservar instalação antiga e backup manual é suficiente para esta beta.

### Prioridade operacional imposta a todos os agentes do Urbe

1. **Concluir e testar o acesso físico ao vault do C#**, principalmente Android (Issue #386) e fluxo do Explorer/Editor (UC-18), sem segundo armazenamento canônico.
2. **Concluir Cidade C# utilizável** (UC-19) com casas/bairros derivados dos arquivos e posições novas gravadas sem perda; UI mobile-first.
3. Empacotar **beta opt-in instalável e funcional** para o proprietário testar em aparelho, mantendo distribuição existente intacta.
4. Prosseguir com **paridade funcional restante** até produto completo. Não reivindicar beta concluído ou corte final sem evidências de uso real e critérios aplicáveis.
5. Ao ler ADR-0004, ADR-0010, ADR-0026, UC-3/6/9/10/26/28/31 e testes/fixtures antigos, **distinguir a história das implementações da obrigação atual do C#**. O código de compatibilidade já criado pode ficar; **não é requisito bloquear a entrega para ampliá-lo**. Preservar requisitos do cliente JS legado sem retroativamente alterar o seu comportamento.

### Regra de precedência

Esta ordem **altera somente o requisito de retrocompatibilidade/migração de estado legado para o novo cliente C#**, anteriormente exigido por DEC-0036-C/ADR-0026 e pela aplicação do ADR-0004 ao corte C#. **Não revoga** a migração de funcionalidades, proteção dos arquivos reais, requisitos de segurança, independência do Hub, histórico Git, gates de qualidade, política de autorização de PR crítico nem validação humana necessária para declarar conclusão.

Esta é uma **mudança de direção aprovada pelo proprietário**, não proposta de agente. **A aprovação do rumo não substitui a label `integrar` exigida para um PR crítico**: após CI verde, a autorização de integração é um evento separado do próprio proprietário, conforme ADD-0012/ADR-0015.

Referências: Issue #386; Issue #281; PR #385; `apps/urbe/docs/csharp/ROADMAP.md`; Ecosystem ADR-0016/0026; Urbe ADR-0004/0010; MANIFEST NN-001, 005, 008, 009, 010, 011, 013, 017, 018, 023.
