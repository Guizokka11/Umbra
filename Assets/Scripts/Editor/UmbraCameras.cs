#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Menu "Umbra > Câmera".
///  1. Configurar câmeras da cena: câmera que segue a Luma + diretor + converte CamSwitchers antigos.
///  2. Criar câmera fixa (enquadre na Scene View e clique): câmera parada + zona.
///  3. Criar câmera fixa que acompanha a Luma com o olhar.
///  4. Criar limite da câmera que segue (não mostra fora do cenário).
/// </summary>
public static class UmbraCameras
{
    const string RigName = "Cameras";

    [MenuItem("Umbra/Câmera/1. Configurar câmeras da cena", priority = 80)]
    public static void Setup() => SetupInternal(false);

    /// <summary>Mesma configuração, sem janelas de diálogo (usado pelos montadores de cena).</summary>
    public static CinemachineCamera SetupSilent() => SetupInternal(true);

    static CinemachineCamera SetupInternal(bool silent)
    {
        var main = Camera.main;
        if (main == null)
        {
            if (!silent) EditorUtility.DisplayDialog("Umbra", "Não há câmera com a tag MainCamera na cena.", "OK");
            return null;
        }
        var player = FindPlayer();
        if (player == null)
        {
            if (!silent) EditorUtility.DisplayDialog("Umbra", "Não achei a Luma (tag Player).", "OK");
            return null;
        }

        Undo.SetCurrentGroupName("Umbra: câmeras");
        int group = Undo.GetCurrentGroup();

        // Brain na câmera principal
        var brain = main.GetComponent<CinemachineBrain>();
        if (brain == null) brain = Undo.AddComponent<CinemachineBrain>(main.gameObject);
        Undo.RecordObject(brain, "Brain");
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.2f);

        // Raiz das câmeras
        var rig = GameObject.Find(RigName);
        if (rig == null)
        {
            rig = new GameObject(RigName);
            Undo.RegisterCreatedObjectUndo(rig, "Cameras");
        }

        // Câmera que segue
        var director = Object.FindAnyObjectByType<CameraDirector>(FindObjectsInactive.Include);
        CinemachineCamera follow = director != null ? director.followCamera : null;
        if (follow == null)
        {
            var go = new GameObject("CM_Seguir_Luma");
            Undo.RegisterCreatedObjectUndo(go, "Seguir");
            go.transform.SetParent(rig.transform, false);
            go.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            go.transform.position = player.transform.position + new Vector3(0f, 2.5f, -14f);

            follow = go.AddComponent<CinemachineCamera>();
            var lens = follow.Lens;
            lens.FieldOfView = 30f;
            follow.Lens = lens;
            follow.Follow = player.transform;

            var pc = go.AddComponent<CinemachinePositionComposer>();
            pc.CameraDistance = 14f;
            pc.TargetOffset = new Vector3(0f, 1.2f, 0f);
            pc.Damping = new Vector3(0.6f, 0.8f, 0.6f);
            var comp = pc.Composition;
            comp.ScreenPosition = new Vector2(0f, -0.1f);
            comp.DeadZone.Enabled = true;
            comp.DeadZone.Size = new Vector2(0.12f, 0.25f);
            pc.Composition = comp;
        }
        follow.Priority = 10;

        if (director == null)
        {
            director = Undo.AddComponent<CameraDirector>(rig);
        }
        Undo.RecordObject(director, "Diretor");
        director.followCamera = follow;
        director.player = player.transform;

        // Converte CamSwitcher antigos em CameraZone
        int converted = 0;
        foreach (var sw in Object.FindObjectsByType<CamSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = sw.gameObject;
            var cam = sw.activeCam;
            if (go.GetComponent<BoxCollider>() == null) continue;
            var zone = go.GetComponent<CameraZone>();
            if (zone == null) zone = Undo.AddComponent<CameraZone>(go);
            zone.zoneCamera = cam;
            if (cam != null) { Undo.RecordObject(cam, "Prioridade"); cam.Priority = 0; }
            Undo.DestroyObjectImmediate(sw);
            converted++;
        }

        // Câmeras fixas sem zona ficam com prioridade 0 para não roubar a tela
        foreach (var cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam == follow) continue;
            if (cam.Priority.Value > 0) { Undo.RecordObject(cam, "Prioridade"); cam.Priority = 0; }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
        Selection.activeGameObject = follow.gameObject;
        if (silent) return follow;
        EditorUtility.DisplayDialog("Umbra",
            "Câmeras configuradas.\n\n- CM_Seguir_Luma segue a Luma (prioridade 10).\n" +
            "- " + converted + " zona(s) antiga(s) convertida(s) para CameraZone.\n\n" +
            "Para criar uma câmera fixa: enquadre a vista na Scene View e use\n" +
            "Umbra > Câmera > Criar câmera fixa (enquadrar pela Scene View).", "OK");
        return follow;
    }

    [MenuItem("Umbra/Câmera/Criar câmera fixa (enquadrar pela Scene View)", priority = 81)]
    static void CreateFixed() => CreateFixedCamera(false);

    [MenuItem("Umbra/Câmera/Criar câmera fixa que olha para a Luma", priority = 82)]
    static void CreateFixedLooking() => CreateFixedCamera(true);

    static void CreateFixedCamera(bool lookAtPlayer)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null)
        {
            EditorUtility.DisplayDialog("Umbra", "Abra a Scene View e enquadre a vista desejada.", "OK");
            return;
        }
        var player = FindPlayer();
        var rig = GameObject.Find(RigName);

        // Câmera exatamente onde a Scene View está olhando
        var camGo = new GameObject(lookAtPlayer ? "CM_Fixa_Olhando" : "CM_Fixa");
        Undo.RegisterCreatedObjectUndo(camGo, "Câmera fixa");
        if (rig != null) camGo.transform.SetParent(rig.transform, true);
        camGo.transform.SetPositionAndRotation(sv.camera.transform.position, sv.camera.transform.rotation);
        var cam = camGo.AddComponent<CinemachineCamera>();
        var lens = cam.Lens;
        lens.FieldOfView = 30f;
        cam.Lens = lens;
        cam.Priority = 0;
        if (lookAtPlayer && player != null)
        {
            cam.LookAt = player.transform;
            cam.Follow = player.transform;
            var rc = camGo.AddComponent<CinemachineRotationComposer>();
            rc.TargetOffset = new Vector3(0f, 1f, 0f);
            rc.Damping = new Vector2(0.6f, 0.6f);
        }

        // Zona: centrada no ponto que a Scene View está olhando, na altura da Luma
        var zoneGo = new GameObject("Zona_" + camGo.name);
        Undo.RegisterCreatedObjectUndo(zoneGo, "Zona");
        if (rig != null) zoneGo.transform.SetParent(rig.transform, true);
        Vector3 c = sv.pivot;
        if (player != null) c.y = player.transform.position.y;
        zoneGo.transform.position = c;
        var box = zoneGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(8f, 4f, 6f);
        box.center = new Vector3(0f, 2f, 0f);
        var zone = zoneGo.AddComponent<CameraZone>();
        zone.zoneCamera = cam;

        Selection.activeGameObject = zoneGo;
        EditorSceneManager.MarkSceneDirty(zoneGo.scene);
        Debug.Log("[Umbra] Câmera fixa criada. Ajuste o tamanho da zona (BoxCollider) para cobrir onde ela deve valer.");
    }

    [MenuItem("Umbra/Câmera/Criar limite da câmera que segue", priority = 83)]
    static void CreateConfiner()
    {
        var director = Object.FindAnyObjectByType<CameraDirector>();
        if (director == null || director.followCamera == null)
        {
            EditorUtility.DisplayDialog("Umbra", "Rode antes Umbra > Câmera > 1. Configurar câmeras da cena.", "OK");
            return;
        }
        var go = new GameObject("Limite_Camera");
        Undo.RegisterCreatedObjectUndo(go, "Limite");
        var rig = GameObject.Find(RigName);
        if (rig != null) go.transform.SetParent(rig.transform, false);
        Vector3 p = director.followCamera.transform.position;
        go.transform.position = p;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(40f, 8f, 4f);
        go.layer = 2; // Ignore Raycast

        var follow = director.followCamera.gameObject;
        var conf = follow.GetComponent<CinemachineConfiner3D>();
        if (conf == null) conf = Undo.AddComponent<CinemachineConfiner3D>(follow);
        conf.BoundingVolume = box;

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Umbra] Limite criado em volta da posição da câmera. Estique o BoxCollider no X para cobrir o andar inteiro: " +
                  "a câmera que segue não passa desse volume.");
    }

    static GameObject FindPlayer()
    {
        GameObject p = null;
        try { p = GameObject.FindWithTag("Player"); } catch { }
        if (p != null) return p;
        var mv = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        return mv != null ? mv.gameObject : null;
    }
}
#endif
