# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.59
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração.
**Atualização:** instala por cima da beta anterior (app.urbe.csharp, mesmo certificado).

## Cálculo

- **Integrais certas.** A integral definida dava valor errado em algumas funções periódicas: ∫₀²π (1+cos x)² dx saía 8π/3 em vez de 3π. Corrigido; uma integral que não converge (como 1/x passando por zero) é recusada com explicação.
- **Integral dentro de uma conta.** `0,5*integral de … de 0 até 2*pi` e `pi*integral de x^4 de 0 até 2` agora funcionam.

## Assistente

- **Sem limite de passos.** Tarefas delegadas a outro agente não param mais em 30 (ou 24) etapas.
- **Resposta cortada continua sozinha.** Quando o modelo atinge o limite de texto de uma resposta, o Assistente continua de onde parou, sem você precisar escrever "continue".
- **Chamada grande cortada não falha às cegas.** Se o modelo tenta mandar algo grande demais de uma vez (como um vídeo inteiro), o Urbe avisa que a chamada foi cortada e orienta a fazer por partes, em vez de responder "falta spec".
- **Vídeos montados por partes.** O Assistente cria a publicação com as fontes, recebe os ids das cenas e preenche uma cena por vez.
- **Pergunta o que você quer antes de produzir.** Num pedido como "faz um simulado", o Assistente pergunta uma vez o formato (quantas questões, gabarito recolhível, formulário que corrige sozinho, PDF), com a recomendação primeiro, e depois executa.
- **Contas conferidas em lote.** O Assistente confere todas as contas numa chamada só, em vez de uma por vez.
- **Erro do plano explicado.** O plano diz exatamente quais campos faltam em vez de uma mensagem que o modelo repetia.

## Não testado no aparelho

- As mudanças foram testadas com testes automáticos e simulações do modelo; não rodei de novo as conversas reais com os modelos do proprietário.
