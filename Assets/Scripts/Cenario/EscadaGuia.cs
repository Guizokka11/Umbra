using UnityEngine;

/// <summary>
/// Na escada 3D a escada vai um pouco para o lado conforme sobe. Enquanto a Luma está nesta área,
/// W/S também a levam de lado na mesma medida, para ela seguir os degraus sem precisar apertar A/D.
/// Também marca PlayerState.isOnStairs (animação "Stairs" do PlayerSpriteController).
/// Colocado pelo montador da escada (Umbra > Cenário > Escada 3D do Corredor 1).
/// </summary>
[RequireComponent(typeof(Collider))]
public class EscadaGuia : MonoBehaviour
{
    [Tooltip("Quanto a escada anda em X para cada metro em Z (positivo = vai para a direita subindo).")]
    public float inclinacaoX;

    PlayerMovement mv;
    CharacterController cc;
    PlayerState st;
    int dentro;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        mv = other.GetComponent<PlayerMovement>();
        cc = other.GetComponent<CharacterController>();
        st = other.GetComponent<PlayerState>();
        dentro++;
        if (st != null) st.isOnStairs = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        dentro = Mathf.Max(0, dentro - 1);
        if (dentro == 0 && st != null) st.isOnStairs = false;
    }

    void Update()
    {
        if (dentro == 0 || mv == null || cc == null || !cc.enabled || !mv.canMove || mv.isDepthLocked) return;
        float iz = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(iz) < 0.01f) return;
        float speed = (mv.isRunning ? mv.runSpeed : mv.walkSpeed) * mv.speedMultiplier;
        cc.Move(Vector3.right * (iz * speed * inclinacaoX * Time.deltaTime));
    }
}
