# Elevador entre andares

Scripts em `Assets/Assets/Estrutura/Scripts/`:

- **`ElevatorTransition`** — persistente (`DontDestroyOnLoad`), autorado só na `SCN_FirstFloor`.
  Escurece a tela, toca o som, carrega a cena com `LoadSceneAsync`, teleporta o jogador para
  perto do objeto cujo nome contém "elevador"/"elevator" e clareia. Gera o piso sólido da cabine
  (`ElevatorFloor_Generated`) e tampa as emendas das paredes. Por isso **nenhum outro objeto da cena
  pode ter "elevador"/"elevator" no nome**: o teleporte acharia ele primeiro.
- **`ElevatorButton`** — gatilho no painel da cabine. Campo `destinos` (rótulo, cena, tecla):
  - **1 destino**: `[E]` leva até ele ("Aperte [E] para ir ao 2º andar");
  - **2 ou mais**: a tecla numérica de cada um ("Elevador: [1] 1º andar [3] 3º andar");
  - **vazio**: usa o campo legado `targetScene` com `[E]` (é o caso do 1º andar, que também usa o
    texto legado `promptUI`). Sem `promptUI`, o prompt sai pelo `InteractionPrompt` da HUD.
  - Não reage enquanto a `PlayerActionLock` estiver ativa.

| Cena | Destinos |
| :--- | :------- |
| `SCN_FirstFloor` | legado: `SCN_SecondFloor` |
| `SCN_SecondFloor` | `[1]` 1º andar, `[3]` 3º andar |
| `SCN_ThirdFloor` | `[E]` 2º andar (`Trigger_Botao_Volta_Gerado`, mesmo offset do gatilho do 2º andar em relação ao `Elevador`) |

Toda cena de destino precisa estar em **File > Build Settings**. A `SCN_ThirdFloor` é incluída
pelo setup `Tools > Hora-Extra > Configurar 3º andar (Financeiro)`.

Ainda fora do escopo (PDF, págs. 15–17): mensagem de "tarefas do andar concluídas", bloquear o
elevador até concluir o andar e os dois sons novos (`SFX_Elevador`, `SFX_Elevador2`) que a Alice
vai subir.
