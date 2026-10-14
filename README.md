# ZombiesMod

Mod de zumbis para GTA V (ScriptHookVDotNet3 + LemonUI), alvo .NET Framework 4.8.

## Estrutura

```
ZombiesMod/                  # inventário, mapa, controlador principal
ZombiesMod.Controllers/      # controladores de mundo e sobreviventes
ZombiesMod.Scripts/          # scripts do SHVDN (spawners, loot, interacção)
ZombiesMod.PlayerManagement/ # inventário, veículos, guardas, mapa do jogador
ZombiesMod.Static/           # config, logger, serializador, JSON, idiomas
ZombiesMod_lang/pt.json      # textos (embutido no DLL e copiado para scripts\ZombiesMod\lang)
ZombiesMod_CustomItems.json  # itens do jogador (modelo de exemplo)
```

## Compilar

1. Coloque `ScriptHookVDotNet3.dll` e `LemonUI.SHVDN3.dll` em `..\dlls\`.
2. `dotnet build -c Release`
3. Copie o conteúdo da saída do build (`ZombiesMod.dll`, `LemonUI.SHVDN3.dll` e a pasta `ZombiesMod\`) para a pasta `scripts` do GTA V.

## Ficheiros em tempo de execução

Em `scripts` ficam só a DLL e o `ZombiesMod.ini`. O resto vive em `scripts\ZombiesMod\`:

- `saves\`: `Inventory.json`, `Map.json`, `Vehicles.json`, `Guards.json` e `World.json` (a semente do mundo; JSON, só dados). Cada gravação deixa o anterior em `*.json.bak`; um save ilegível é movido para `*.json.bad`. O modo desenvolvedor fica no `Inventory.json`.
- `items\`: `CustomItems.json`, o catálogo de itens do jogador (criado com um modelo de exemplo se não existir).
- `lang\`: `pt.json` e outros idiomas.
- `logs\`: `errors.log`.

## Mundo procedural

O mapa é dividido em células de 150 m. Tudo o que cada célula contém é função de (semente, célula): a semente é gerada na primeira vez e fica fixa em `saves\World.json`.

| Conteúdo | Chance por célula | Comportamento |
|---|---|---|
| Densidade de zumbis | todas | população máxima × 0,2 a 1,8 |
| Horda | 4 % | 12 a 30 zumbis; a célula só fica limpa se você matar 80 % |
| Acampamento de sobreviventes | 2,5 % | aliados; resolvido quando todos são recrutados/morrem |
| Hostis (veículo armado) | 2 % | resolvido quando todos morrem |
| Patrulha Merryweather | 1 % | caixa de suprimentos/armas; resolvido ao saquear ou expirar |
| Manada de animais | 6 % | só longe de estradas; volta quando você retorna |

Eventos resolvidos ficam gravados no `World.json` e não voltam. Nascimentos são espalhados em vários ticks para não travar o jogo. Para voltar ao spawn aleatório antigo: `procedural_spawning = false` em `[spawning]` do `ZombiesMod.ini`.