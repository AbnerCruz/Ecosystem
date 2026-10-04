# Persistência local v1

Autoridade operacional da persistência implementada em RPG-002. Subordinada a `SPEC.md` e às regras do Product.

## Envelope

Cada campanha é codificada como JSON com:

- `format: "tabletop-rpg-campaign"`;
- `schemaVersion: 1`;
- `savedAtUtc`;
- `payloadSha256`;
- `payload`.

`schemaVersion` é a versão do formato de dados, não a versão do aplicativo. Um leitor v1 recusa versão futura que não conhece; migração futura precisa ser explícita.

O checksum detecta corrupção ou alteração acidental do payload. Ele **não é assinatura criptográfica de autenticidade** e não deve ser vendido como proteção contra um atacante que possa reescrever arquivo e checksum.

## Conteúdo persistido

O payload v1 preserva:

- CampaignId, nome e relógio lógico;
- personagens e atributos;
- participantes, tipo de controlador, papel e personagens controlados;
- World Facts;
- Knowledge e Memory por personagem;
- eventos ordenados;
- sessões de campanha e sessão ativa.

IDs e tempos lógicos são restaurados, não recriados. Estado inválido é recusado.

Credenciais, tokens e configuração secreta de provedor de IA não pertencem ao arquivo de campanha.

## Arquivos locais

Para uma campanha `<id>`:

- primário: `<id>.campaign.json`;
- backup: `<id>.campaign.json.bak`;
- temporário transitório: `<destino>.tmp`.

O nome deriva exclusivamente de CampaignId; nome de campanha fornecido pelo usuário não entra em caminho de arquivo.

## Save

1. serializar o estado inteiro;
2. calcular checksum;
3. se o primário atual existir e for válido, copiá-lo para o backup por escrita temporária + rename;
4. escrever o novo documento em arquivo temporário no mesmo diretório;
5. fazer flush do conteúdo;
6. substituir o primário por rename no mesmo diretório;
7. limpar temporário remanescente.

Um primário já reconhecido como corrompido nunca substitui um backup válido.

Instâncias de `FileCampaignStore` no mesmo processo serializam I/O por diretório raiz, evitando que autosaves concorrentes disputem o mesmo arquivo temporário. O formato v1 não pretende ser um banco de dados multi-processo nem mecanismo de sincronização multiplayer.

## Load e recuperação

1. tentar primário;
2. validar JSON, formato, schema, checksum, identidade e invariantes do estado;
3. se o primário falhar, tentar backup;
4. se o backup for válido, restaurá-lo atomicamente como novo primário e retornar a campanha com origem `Backup`;
5. se ambos falharem, lançar `CampaignRecoveryException`.

A recuperação nunca cria uma campanha vazia silenciosamente.

## Export/import

Export e import usam exatamente o mesmo envelope v1. Importar não exige backend nem conta e pode opcionalmente persistir a campanha no store local.

Arquivos binários, mapas e mídia não são incorporados neste formato em RPG-002; quando esses domínios chegarem, precisam de estratégia própria de assets e compatibilidade.
