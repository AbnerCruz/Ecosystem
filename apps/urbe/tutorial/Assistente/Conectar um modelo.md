# Conectar um modelo

O Urbe não vem com uma IA embutida: você escolhe qual usar. Assim você controla o custo, a privacidade e a qualidade.

## Provedores disponíveis

| Provedor | Para quem |
|---|---|
| **OpenRouter** (recomendado) | Uma chave só para usar Claude, GPT, Gemini, Llama e muitos outros |
| **Anthropic** | Claude, direto da Anthropic |
| **OpenAI** | GPT, direto da OpenAI |
| **Google Gemini** | Gemini, do Google |
| **Ollama (local)** | Modelos rodando no seu computador, sem internet e sem custo |
| **Compatível com OpenAI** | LM Studio, vLLM, Groq e outros serviços com API parecida |

## Passo a passo

1. Abra o **Assistente** e toque no provedor que quer usar (ou em ⚙️ → **Provedores de modelo**).
2. Crie uma **chave da API** no site do provedor. A tela mostra o link.
3. Cole a chave e toque em **Salvar e escolher modelo**.
4. Escolha o modelo na lista. O botão **Testar** confere se está tudo certo.

> [!info] Sua chave fica só no seu aparelho
> A chave é guardada apenas neste aparelho e enviada somente ao provedor que você escolheu. O uso é cobrado por ele, direto na sua conta.

## Ollama (sem internet)

1. Instale o Ollama no computador e baixe um modelo.
2. Abra o Ollama permitindo o acesso do navegador: `OLLAMA_ORIGINS=*` (a tela do Urbe mostra o comando).
3. No Urbe, escolha **Ollama (local)**. O endereço padrão já vem preenchido.

## Trocar de modelo

Toque no nome do modelo, nas configurações ou no topo da conversa, e escolha outro. Dá para ter vários provedores salvos e alternar entre eles.

Próximo: [[Tutorial/Assistente/Agentes e modos|Agentes e modos]]
