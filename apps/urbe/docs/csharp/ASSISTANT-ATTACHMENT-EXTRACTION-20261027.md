# UC-63 — Extração real de arquivos do chat (2026-10-27)

**Escopo:** Urbe nativo C# Avalonia, Android/Windows. Issue #826. Sem alterar formato do vault.

## Falha observada
O agente recebe arquivos como blocos no histórico da conversa, mas não dispõe de cópia física no vault. Hoje os arquivos de texto são blocos ocultos de conteúdo decodificado e imagens/PDF são binários em base64.

## Investigação de referências e feedback
- Obsidian copia anexos para arquivos reais dentro do vault, integrando editor e filesystem: https://obsidian.md/help/attachments
- Android Storage Access Framework: URIs e streams escolhidos pelo usuário não são paths confiáveis; reutilizar seletor e writer nativos: https://developer.android.com/training/data-storage/shared/documents-files
- OWASP recomenda validar nomes, tipos, assinaturas, tamanho, autorização e impedir arquivos conflitantes: https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html
- Feedback de Markdown: localização previsível e links relativos de anexos reduzem trabalho manual: https://github.com/marktext/marktext/issues/5030 e https://github.com/marktext/marktext/issues/2169

## Implementação
- Ação Extrair na mensagem: arquivos admissíveis são copiados para Anexos/ e abertos no visualizador/editor já existente. Se o nome existir, recebe sufixo numerado, sem sobrescrita.
- Ferramenta extract_attachment disponível ao agente no escopo da própria conversa, Access=write, respeitando políticas e aprovação já existentes.
- Nenhum byte fornecido pelo modelo vira conteúdo de arquivo. O host resolve somente anexos reais da conversa.
- Writer nativo verificado grava texto em UTF-8 apenas em conversas antigas que não guardaram bytes de origem; PDF e imagens mantêm bytes exatos. Para novos anexos textuais também preserva os bytes originais SAF (UTF-8/UTF-16 com BOM) como metadados opcionais da conversa, invisíveis ao modelo. O extrator canônico os revalida antes da gravação e o vault físico recebe bytes exatos. HTML continua dado inerte, nunca executado na extração.
- Somente tipos já suportados pelo writer do vault são extraíveis com extensão original. Tipos textuais adicionais como TSX e CSPROJ exigem extensão futura da política de armazenamento.

## Testes primeiro — evidência Git
- 6bc07cee: teste HTML de comportamento salvo antes do código.
- 9df470e4: testes de binários, nomes repetidos, corrupção e falhas gravados antes do código.
- be321bcd: implementação inicial do mecanismo, depois dos testes.
- Testes headless de gravação em disco incluídos como etapa de integração, além do contrato inicial.

## Gates
CI: testes Core e Client Avalonia, compatibilidade das ferramentas históricas, build Android/Windows, consistency e integrador oficial.
Validação Android física: HTML/PDF/imagem, duplicados, Explorer, editor e abertura do visualizador, reinício. CI não substitui aparelho (NN-017).
Release: apenas após integração governada na main, pelo workflow urbe-csharp-mobile-release.yml, tag inédita e APK assinado.

## Reconciliação da PR #825, sem segundo extrator (27/10/2026)
- Base: main já com #827; aproveitados somente preservação de bytes, gravação importada pelos writers existentes e atualização do baseline.
- Mantidos os contratos da #827: extract_attachment e IAssistantAttachmentExtractor. Não importar AssistantChatAttachments, AssistantChatFileTools ou AssistantPanel.ChatExtraction da #825.
- Testes antes da implementação: AssistantAttachmentByteFidelityTests (UTF-16 LE/BE, serialização de conversa, corrupção, legado, spoofing) e ChatAttachmentByteStorageTests (bytes exatos, nova nota, não sobrescrever, caminhos internos).
- Metadados source_name e source_bytes são opcionais no bloco oculto da conversa. Histórico antigo segue legível; somente uploads reais do usuário podem ser extraídos.
- Mudanças em storage Android SAF são críticas: aprovação formal do proprietário depois dos gates verdes, nunca merge manual.
- Não há destino arbitrário, nova ferramenta, UI paralela, mudança do formato do vault nem bump de APK nesta PR.
- Limitação: bytes originais de anexos textuais antigos nunca foram salvos; sua reconstrução é UTF-8. .NET SDK indisponível neste executor; comprovação via CI e DEVICE distinto.
