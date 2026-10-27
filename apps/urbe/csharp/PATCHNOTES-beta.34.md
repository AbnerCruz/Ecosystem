# Urbe 2.0.0-beta.34 — lançamento nativo Android / Windows

Data: 27 de outubro de 2026  
Tag prevista: `urbe-csharp-v2.0.0-beta.34`  
Canal: beta nativo (`app.urbe.csharp`, mesmo certificado da beta.33)  
Base: `main` após integração de PRs #816 e #819 e validações UC-51/UC-63.

## Atualizações

### Anexos no chat da IA — UC-63 (PR #819)

- Aceita HTML e outros arquivos de texto reais: `.html`, `.htm`, Markdown, JSON, YAML, CSV, XML, SVG, código-fonte e extensões não catalogadas quando os bytes constituem texto.
- Aumenta o limite de texto de 96 KiB para 384 KiB por arquivo; 512 KiB de texto somados por mensagem; até quatro anexos. Evita truncamento silencioso e mostra motivo quando o arquivo é recusado.
- Lê `content://` do seletor nativo Android com streams limitados; reconhece UTF-8 e UTF-16 com BOM e rejeita binários disfarçados.
- Corrige preparação de imagens/PDFs para envio aos provedores multimodais.
- Mantém documentos como dados, sem executar HTML ou scripts, sem importar anexos para o vault.

### Editor e selo de autoria — UC-51 (PR #816)

- Quando uma linha inteira é apagada, remove o selo de IA órfão dessa linha, preservando marcas válidas e as demais linhas.
- Inclui botões Desfazer/Refazer na barra do editor, inclusive quando existe personalização antiga da barra.

## Verificações e distribuição

- PR #819: 902 testes Core e 258 testes Client aprovados, Android/Windows compilados e E2E/consistência aprovados.
- O corte beta.34 exige novo CI na `main` combinada, assinatura APK pela chave beta estável, publicação de checksums e ZIP Windows pelo workflow oficial `urbe-csharp-mobile-release.yml`.
- A publicação não substitui a beta.33; incrementa versionCode para 2000034.
- Teste de instalação e anexo de arquivo HTML real no Android é um gate físico separado dos testes automatizados. Não há alegação de validação em dispositivo antes de execução.

**Arquivos esperados:** `Urbe-CSharp-2.0.0-beta.34-Android.apk`, `Urbe-CSharp-2.0.0-beta.34-Windows-x64.zip`, `SHA256SUMS.txt`, `SHA256SUMS-Windows.txt`.
