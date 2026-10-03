# Integração do código C# — UC-7

> VIGENTE: PR crítico #151 autorizado pelo proprietário e integrado em 584fe73. Autoridade: política do Ecosystem, ADD-0012/ADR-0015, ADR-0016 e ROADMAP UC-7. Não autoriza código C# antes de G-C0.

O código novo ficará sob `apps/urbe/csharp/` (UC-8). Essa raiz é uma convenção para classificação, não escolhe UI, hosts, namespaces ou bibliotecas. Dentro dela, arquivos que implementem áreas sensíveis ficam nas zonas abaixo. Código sensível colocado em outro caminho continua crítico por declaração no handoff; o glob não substitui revisão semântica. A estrutura concreta precisa comprovar enquadramento no primeiro PR de código.

| Classe | Diretórios/arquivos protegidos | Motivo |
|---|---|---|
| user-data | Persistence, Storage, Migration, Backup, Identity; nomes *Vault*.cs e *Store*.cs em qualquer profundidade | Leitura/escrita, formatos, backup, recuperação, identidade e estado do usuário |
| security | Security, Credentials, Plugins, Interop, Native | Segredos, execução de extensões e fronteiras do host |
| distribution | csproj, props, targets; Packaging, Platforms, Updates, Distribution | Dependências/build, assinatura, identidade, hosts e atualização |

Uma mudança pode pertencer a várias classes. As zonas JS atuais permanecem intactas; a rotina de autorização não muda. Documentação, testes e UI/domínio sem consequência crítica continuam rotina automática depois de CI verde. Não criar diretórios vazios nem código de demonstração para ativar a política.

REQ-007/035/038 continuam obrigatórios. O contrato do vault permanece no catálogo/ADR-0004; isto não migra dados. Publicação e corte continuam UC-26/31, com assinatura/identidade e validação humana próprias.

## Verificação

`URBE_DOTNET=/caminho/dotnet node tests/consistency/UrbeCsharpPolicy.mjs` executa o classificador C# real com entradas de persistência/segurança/distribuição, profundidades diferentes, casos JS existentes e caso rotineiro de UI. Também prova que a política não remove nenhuma proteção nem muda os autorizadores.

O teste usa a política vigente, com uma lista de arquivos sintéticos em diretório temporário; não escreve código nem move refs. Na integração deste PR o integrador usa a política da main atual, que já classifica o próprio arquivo de política como crítico. Só o proprietário autoriza esse PR; agente não adiciona `integrar`.
