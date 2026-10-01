# Quem pode ver o portal e quem pode decidir

> **Autoridade:** documento de arquitetura, subordinado ao `MANIFEST.md`. Descreve o estado verificado em 2026-10-01 e as opções; a escolha é do proprietário (DEC-0012).
> **Decidido em DEC-0012 (2026-10-01, pelo portal): alternativa A, manter público.** Nada a implementar; decidir continua restrito ao dono do repositório. [Registro](../governance/responses/DEC-0012.md).
> Legenda: **FATO** verificado nesta data · **NÃO VERIFICADO** não foi possível conferir agora.

## 1. Quem pode **ver** (hoje: qualquer pessoa com o link)

| Item | Situação |
|------|----------|
| Repositório `AbnerCruz/Ecosystem` | **público** (FATO: API do GitHub, `visibility: public`). Código, documentos, decisões, handoffs e inventários já são legíveis por qualquer pessoa, no GitHub, **independentemente do portal**. |
| Portal (GitHub Pages) | **público** (FATO: publicado a partir do repositório público). |
| Conteúdo sensível | Nenhum encontrado: sem segredos nem e-mail pessoal no repositório (FATO: `git grep`; `CHK-SECRETS` passa). Os inventários citam apenas **nomes** de secrets, nunca valores (MANIFEST §30.2). Lunet2D e Urbe também são repositórios públicos. |

## 2. Quem pode **decidir** (somente o proprietário)

- Qualquer pessoa pode abrir o portal e tocar em "Escolher X", mas isso só abre uma Issue pré-preenchida no GitHub, e **criar Issue exige login no GitHub**.
- Uma conta qualquer pode, em tese, enviar a Issue (o repositório é público), mas o workflow `decision.yml` **só roda se o autor é o dono do repositório** e se `author_association` é `OWNER`; qualquer outra Issue é ignorada. Nenhuma decisão é registrada (FATO: guarda no workflow, fiscalizada por `CHK-DECISION-FLOW`; recusa de autor errado coberta pelo self-test do aplicador).
- O que um estranho consegue: ver as decisões pendentes e deixar Issues "de ruído", que o proprietário pode fechar (ou limitar com *Interaction limits* nas configurações do repositório).
- O que um estranho **não** consegue: registrar, alterar ou apagar uma decisão.

## 3. Por que uma senha no portal **não** protege (GitHub Pages é estático)

- Um site estático não tem servidor para conferir a senha. Qualquer "senha" seria JavaScript entregue a todos: o código e os dados estão no navegador de quem abre a página.
- O repositório é público: mesmo com a página trancada, os mesmos dados ficam em `docs/governance/decisions.json`, nos handoffs e nos documentos.
- Se a senha fosse guardada como hash no código, uma senha curta (palavra + números) seria quebrada em segundos, offline. Por isso **nenhuma senha foi gravada no repositório** (MANIFEST §30.2). A senha enviada no chat deve ser considerada **exposta**: não a reutilize em outro lugar.

## 4. Opções (decisão DEC-0012)

| Opção | O que protege | O que não protege / custo |
|-------|---------------|---------------------------|
| **A. Manter público** | Decisões já restritas ao dono. | Qualquer pessoa vê estado, decisões e documentos. |
| **B. Barreira de senha no navegador** | Esconde a interface e os dados da projeção de quem não tem a senha (dados cifrados no build com uma senha guardada como *secret* do repositório, não no código). | **Não** esconde o repositório; cifra quebrável offline se a senha for fraca; exige criar o secret (ação sua). Dá sensação de segurança maior que a real. |
| **C. Repositório privado** | Esconde código, documentos, decisões e handoffs. | Segundo a documentação do GitHub (**NÃO VERIFICADO agora**: `docs.github.com` está bloqueado neste ambiente), o Pages de repositório privado depende do plano e, mesmo quando publica, o **site** continua público a menos que a conta seja GitHub Enterprise Cloud. Os links de "objeto" do portal passariam a exigir login. Um portal realmente privado exigiria hospedagem com login fora do GitHub Pages (fora do escopo atual). |

Só a opção C restringe o **conteúdo**; B e A não.
