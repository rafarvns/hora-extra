using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HoraExtra.Interactions;

/// <summary>
/// Configura o 3º andar (Financeiro) para ser jogável — Tarefas 6 (cofres) e 7 (e-mail).
///
/// Idempotente: pode rodar de novo, ele recria só o que gerou (objetos com sufixo
/// "_Gerado") e reaplica os campos. Faz:
///   - SCN_SecondFloor: elevador com destinos [1] 1º andar e [3] 3º andar;
///   - SCN_ThirdFloor: gatilho do elevador (volta ao 2º), zonas de interação dos dois
///     cofres (SafeInteraction + SafeDoor, dinheiro ligado por proximidade) e do PC do
///     e-mail (EmailComputer);
///   - Build Settings: inclui SCN_ThirdFloor.
///
/// Menu: Tools/Hora-Extra/Configurar 3º andar (Financeiro).
/// Batch: Unity -batchmode -quit -projectPath . -executeMethod HoraExtraFinanceiroSetup.Executar
/// </summary>
public static class HoraExtraFinanceiroSetup
{
    private const string CENA_2 = "Assets/Scenes/SCN_SecondFloor.unity";
    private const string CENA_3 = "Assets/Scenes/SCN_ThirdFloor.unity";
    private const string SUFIXO = "_Gerado";

    private const string COFRE_DIREITO_CODIGO = "1512";
    private const string COFRE_ESQUERDO_CODIGO = "2803";

    private const string MENU_TESTE = "Tools/Hora-Extra/Teste rápido: Financeiro";

    /// <summary>Liga/desliga o modo de teste rápido (ver HoraExtra.Dev.DevQuickTest).</summary>
    [MenuItem(MENU_TESTE)]
    public static void AlternarTesteRapido()
    {
        bool novo = !EditorPrefs.GetBool(HoraExtra.Dev.DevQuickTest.PREF_KEY, false);
        EditorPrefs.SetBool(HoraExtra.Dev.DevQuickTest.PREF_KEY, novo);
        Debug.Log(novo
            ? "[SETUP] Teste rápido do Financeiro LIGADO: dê Play na SCN_FirstFloor."
            : "[SETUP] Teste rápido do Financeiro desligado.");
    }

    [MenuItem(MENU_TESTE, true)]
    private static bool ValidarTesteRapido()
    {
        Menu.SetChecked(MENU_TESTE, EditorPrefs.GetBool(HoraExtra.Dev.DevQuickTest.PREF_KEY, false));
        return true;
    }

    [MenuItem("Tools/Hora-Extra/Configurar 3º andar (Financeiro)")]
    public static void Executar()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 1) 2º andar: destinos do elevador + offset do gatilho em relação ao elevador.
        var cena2 = EditorSceneManager.OpenScene(CENA_2, OpenSceneMode.Single);
        var gatilho2 = ConfigurarSegundoAndar(out Vector3 offsetLocal, out Quaternion rotRelativa, out Vector3 tamanho, out Vector3 centro);
        EditorSceneManager.MarkSceneDirty(cena2);
        EditorSceneManager.SaveScene(cena2);

        // 2) 3º andar.
        var cena3 = EditorSceneManager.OpenScene(CENA_3, OpenSceneMode.Single);
        if (gatilho2) ConfigurarElevadorTerceiro(offsetLocal, rotRelativa, tamanho, centro);
        ConfigurarCofres();
        ConfigurarComputador();
        EditorSceneManager.MarkSceneDirty(cena3);
        EditorSceneManager.SaveScene(cena3);

        // 3) Build Settings.
        IncluirNoBuild(CENA_3);

        AssetDatabase.SaveAssets();
        Debug.Log("[SETUP] 3º andar (Financeiro) configurado.");
    }

    // =========================================================================
    // 2º andar
    // =========================================================================

    private static bool ConfigurarSegundoAndar(out Vector3 offsetLocal, out Quaternion rotRelativa, out Vector3 tamanho, out Vector3 centro)
    {
        offsetLocal = Vector3.zero; rotRelativa = Quaternion.identity; tamanho = Vector3.one; centro = Vector3.zero;

        var botao = Object.FindFirstObjectByType<ElevatorButton>();
        var elevador = AcharElevador();
        if (botao == null || elevador == null)
        {
            Debug.LogError("[SETUP] SCN_SecondFloor sem ElevatorButton ou sem 'Elevador'.");
            return false;
        }

        var so = new SerializedObject(botao);
        var destinos = so.FindProperty("destinos");
        destinos.arraySize = 2;
        Destino(destinos.GetArrayElementAtIndex(0), "1º andar", "SCN_FirstFloor", 1);
        Destino(destinos.GetArrayElementAtIndex(1), "3º andar", "SCN_ThirdFloor", 3);
        so.ApplyModifiedPropertiesWithoutUndo();

        var col = botao.GetComponent<BoxCollider>();
        var t = botao.transform;
        offsetLocal = Quaternion.Inverse(elevador.rotation) * (t.position - elevador.position);
        rotRelativa = Quaternion.Inverse(elevador.rotation) * t.rotation;
        tamanho = col != null ? Vector3.Scale(col.size, t.lossyScale) : new Vector3(0.6f, 1.2f, 0.5f);
        centro = col != null ? Vector3.Scale(col.center, t.lossyScale) : Vector3.zero;

        Debug.Log($"[SETUP] 2º andar: elevador com destinos 1º/3º; offset do gatilho {offsetLocal}.");
        return true;
    }

    private static void Destino(SerializedProperty p, string rotulo, string cena, int tecla)
    {
        p.FindPropertyRelative("rotulo").stringValue = rotulo;
        p.FindPropertyRelative("cena").stringValue = cena;
        p.FindPropertyRelative("tecla").intValue = tecla;
    }

    // =========================================================================
    // 3º andar — elevador
    // =========================================================================

    private static void ConfigurarElevadorTerceiro(Vector3 offsetLocal, Quaternion rotRelativa, Vector3 tamanho, Vector3 centro)
    {
        var elevador = AcharElevador();
        if (elevador == null)
        {
            Debug.LogError("[SETUP] SCN_ThirdFloor sem 'Elevador'.");
            return;
        }

        // Sem "elevador"/"elevator" no nome: o ElevatorTransition procura o modelo por nome.
        var go = Recriar("Trigger_Botao_Volta" + SUFIXO, null);
        go.transform.SetPositionAndRotation(elevador.position + elevador.rotation * offsetLocal, elevador.rotation * rotRelativa);

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = tamanho;
        col.center = centro;

        var botao = go.AddComponent<ElevatorButton>();
        var so = new SerializedObject(botao);
        var destinos = so.FindProperty("destinos");
        destinos.arraySize = 1;
        Destino(destinos.GetArrayElementAtIndex(0), "2º andar", "SCN_SecondFloor", 2);
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[SETUP] 3º andar: painel do elevador em {go.transform.position} (volta ao 2º).");
    }

    // =========================================================================
    // 3º andar — cofres
    // =========================================================================

    private static void ConfigurarCofres()
    {
        var c1 = AcharPorNome("Cofre_Senha1");
        var c2 = AcharPorNome("Cofre_Senha2");
        if (c1 == null || c2 == null)
        {
            Debug.LogError("[SETUP] Cofre_Senha1/Cofre_Senha2 não encontrados em SCN_ThirdFloor.");
            return;
        }

        Vector3 frente = Frente(c1);
        // Quem olha para o cofre olha na direção -frente; a direita dessa pessoa:
        Vector3 direitaDoJogador = Vector3.Cross(Vector3.up, -frente).normalized;

        bool c1EhDireito = Vector3.Dot(Limites(c1).center, direitaDoJogador) > Vector3.Dot(Limites(c2).center, direitaDoJogador);
        var direito = c1EhDireito ? c1 : c2;
        var esquerdo = c1EhDireito ? c2 : c1;

        var todosCofres = direito.transform.parent != null
            ? direito.transform.parent.Cast<Transform>()
                .Where(t => t.name.StartsWith("Cofre_Senha") || t.name.StartsWith("SM_Env_Safe"))
                .Select(t => t.gameObject).ToList()
            : new List<GameObject> { direito, esquerdo };

        ConfigurarCofre(direito, "cofre-direito", COFRE_DIREITO_CODIGO, frente, direitaDoJogador, todosCofres);
        ConfigurarCofre(esquerdo, "cofre-esquerdo", COFRE_ESQUERDO_CODIGO, Frente(esquerdo), direitaDoJogador, todosCofres);
    }

    private static void ConfigurarCofre(GameObject cofre, string itemId, string codigo, Vector3 frente, Vector3 direitaDoJogador, List<GameObject> todosCofres)
    {
        Bounds b = Limites(cofre);
        Transform pai = cofre.transform.parent;

        var zona = Recriar($"Interacao_{cofre.name}{SUFIXO}", pai);
        Vector3 centroZona = b.center + frente * 0.7f;
        centroZona.y = b.center.y - 0.4f;
        zona.transform.SetPositionAndRotation(centroZona, Quaternion.LookRotation(frente, Vector3.up));
        CompensarEscala(zona.transform);

        var col = zona.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.0f, 2.2f, 1.4f);

        // Porta + dobradiça (lado esquerdo de quem olha, abrindo para fora).
        var porta = cofre.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t != cofre.transform && t.name.ToLowerInvariant().Contains("door"));

        SafeDoor safeDoor = null;
        if (porta != null)
        {
            Bounds bp = Limites(porta.gameObject);
            float extDireita = Mathf.Abs(direitaDoJogador.x) * bp.extents.x + Mathf.Abs(direitaDoJogador.z) * bp.extents.z;
            float extFrente = Mathf.Abs(frente.x) * bp.extents.x + Mathf.Abs(frente.z) * bp.extents.z;

            var dobradica = new GameObject("Dobradica");
            dobradica.transform.SetParent(zona.transform, false);
            dobradica.transform.position = bp.center - direitaDoJogador * extDireita + frente * extFrente;

            Vector3 bordaLivre = bp.center + direitaDoJogador * extDireita;
            Vector3 girada = dobradica.transform.position +
                             Quaternion.AngleAxis(100f, Vector3.up) * (bordaLivre - dobradica.transform.position);
            float angulo = Vector3.Dot(girada - dobradica.transform.position, frente) > 0f ? 100f : -100f;

            safeDoor = zona.AddComponent<SafeDoor>();
            var sod = new SerializedObject(safeDoor);
            sod.FindProperty("_porta").objectReferenceValue = porta;
            sod.FindProperty("_dobradica").objectReferenceValue = dobradica.transform;
            sod.FindProperty("_eixoLocal").vector3Value = porta.InverseTransformDirection(Vector3.up);
            sod.FindProperty("_angulo").floatValue = angulo;

            // Objetos colados na frente da porta (o teclado é uma calculadora posta ali na cena).
            var colados = ObjetosColadosNaPorta(cofre, bp, frente, extFrente);
            var acomp = sod.FindProperty("_acompanham");
            acomp.arraySize = colados.Count;
            for (int i = 0; i < colados.Count; i++) acomp.GetArrayElementAtIndex(i).objectReferenceValue = colados[i];
            sod.ApplyModifiedPropertiesWithoutUndo();

            if (colados.Count > 0)
                Debug.Log($"[SETUP] {cofre.name}: giram junto com a porta → {string.Join(", ", colados.Select(c => c.name))}.");
        }
        else
        {
            Debug.LogWarning($"[SETUP] '{cofre.name}' sem filho 'Door' — a porta não vai girar.");
        }

        // Dinheiro: notas/moedas mais próximas deste cofre do que de qualquer outro.
        var dinheiro = new List<GameObject>();
        if (pai != null)
        {
            foreach (Transform t in pai)
            {
                if (!t.name.StartsWith("Cash") && !t.name.StartsWith("coins_pile")) continue;
                Vector3 p = Limites(t.gameObject).center;
                var maisPerto = todosCofres.OrderBy(c => (Limites(c).center - p).sqrMagnitude).FirstOrDefault();
                if (maisPerto == cofre) dinheiro.Add(t.gameObject);
            }
        }

        var safe = zona.AddComponent<SafeInteraction>();
        var so = new SerializedObject(safe);
        so.FindProperty("_itemId").stringValue = itemId;
        so.FindProperty("_codigo").stringValue = codigo;
        so.FindProperty("_porta").objectReferenceValue = safeDoor;
        var arr = so.FindProperty("_dinheiroVisual");
        arr.arraySize = dinheiro.Count;
        for (int i = 0; i < dinheiro.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = dinheiro[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[SETUP] {cofre.name} → {itemId} (senha {codigo}), porta={(porta != null ? porta.name : "-")}, " +
                  $"dinheiro={dinheiro.Count} objeto(s), frente={frente}.");
    }

    // =========================================================================
    // 3º andar — computador do e-mail
    // =========================================================================

    private static void ConfigurarComputador()
    {
        var pc = AcharPorNome("PC (9)");
        if (pc == null)
        {
            Debug.LogError("[SETUP] 'PC (9)' (tela de e-mail) não encontrado em SCN_ThirdFloor.");
            return;
        }

        Bounds b = Limites(pc);
        var zona = Recriar("Interacao_Email_PC9" + SUFIXO, pc.transform.parent);
        zona.transform.SetPositionAndRotation(new Vector3(b.center.x, b.min.y, b.center.z), Quaternion.identity);
        CompensarEscala(zona.transform);

        var col = zona.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(2.0f, 2.4f, 2.0f);
        col.center = new Vector3(0f, 0f, 0f);

        zona.AddComponent<EmailComputer>();
        Debug.Log($"[SETUP] PC (9) → EmailComputer em {zona.transform.position}.");
    }

    // =========================================================================
    // Auxiliares
    // =========================================================================

    /// <summary>
    /// Objetos da cena (fora do cofre) cujo centro fica na frente da porta, dentro do
    /// contorno dela, e que são pequenos — o teclado, o botão etc. Só os de topo: se um
    /// pai e um filho casam, entra só o pai.
    /// </summary>
    private static List<Transform> ObjetosColadosNaPorta(GameObject cofre, Bounds porta, Vector3 frente, float extFrente)
    {
        var zona = porta;
        zona.Expand(0.04f);
        zona.Encapsulate(porta.center + frente * (extFrente + 0.12f));

        float maxTamanho = porta.size.magnitude * 1.2f;
        var candidatos = new List<Transform>();

        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.IsChildOf(cofre.transform) || cofre.transform.IsChildOf(t)) continue;
                if (t.name.EndsWith(SUFIXO) || t.name == "Dobradica") continue;
                if (t.name.StartsWith("Cash") || t.name.StartsWith("coins_pile")) continue;      // dinheiro fica
                if (t.name.StartsWith("Cofre_Senha") || t.name.StartsWith("SM_Env_Safe")) continue; // outros cofres
                if (t.GetComponentInParent<SafeInteraction>(true) != null) continue;
                if (t.GetComponentsInChildren<Renderer>(true).Length == 0) continue;

                Bounds b = Limites(t.gameObject);
                if (b.size.magnitude > maxTamanho) continue;
                if (!zona.Contains(b.center)) continue;

                candidatos.Add(t);
            }
        }

        return candidatos.Where(c => !candidatos.Any(o => o != c && c.IsChildOf(o))).ToList();
    }

    private static Transform AcharElevador()
    {
        var go = AcharPorNome("Elevador");
        return go != null ? go.transform : null;
    }

    private static GameObject AcharPorNome(string nome)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
        return null;
    }

    /// <summary>Apaga o objeto gerado numa execução anterior e cria um novo vazio.</summary>
    private static GameObject Recriar(string nome, Transform pai)
    {
        var antigo = AcharPorNome(nome);
        if (antigo != null) Object.DestroyImmediate(antigo);

        var go = new GameObject(nome);
        if (pai != null) go.transform.SetParent(pai, true);
        return go;
    }

    /// <summary>Deixa a escala de mundo em ~1 para os tamanhos do collider valerem em metros.</summary>
    private static void CompensarEscala(Transform t)
    {
        Vector3 s = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(1f / NaoZero(s.x), 1f / NaoZero(s.y), 1f / NaoZero(s.z));
    }

    private static float NaoZero(float v) => Mathf.Abs(v) < 1e-5f ? 1f : v;

    private static Bounds Limites(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    /// <summary>
    /// Para onde o cofre "olha" (lado da porta), no plano horizontal: do centro do corpo
    /// para o centro da porta.
    /// </summary>
    private static Vector3 Frente(GameObject cofre)
    {
        var porta = cofre.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t != cofre.transform && t.name.ToLowerInvariant().Contains("door"));
        if (porta != null)
        {
            Vector3 d = Limites(porta.gameObject).center - Limites(cofre).center;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-6f) return d.normalized;
        }
        Debug.LogWarning($"[SETUP] Não deu para deduzir a frente de '{cofre.name}'; usando -Z.");
        return Vector3.back;
    }

    private static void IncluirNoBuild(string caminho)
    {
        var cenas = EditorBuildSettings.scenes.ToList();
        if (cenas.Any(s => s.path == caminho)) return;
        cenas.Add(new EditorBuildSettingsScene(caminho, true));
        EditorBuildSettings.scenes = cenas.ToArray();
        Debug.Log($"[SETUP] {caminho} incluída no Build Settings.");
    }
}
