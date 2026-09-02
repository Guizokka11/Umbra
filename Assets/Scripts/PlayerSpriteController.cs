using UnityEngine;

public class PlayerSpriteController : MonoBehaviour
{
    private PlayerMovement movement;
    private Animator       animator;
    private SpriteRenderer sr;

    void Start()
    {
        // PlayerMovement está no mesmo objeto (Player)
        movement = GetComponent<PlayerMovement>();

        // Animator e SpriteRenderer estão no filho (SpriteObject)
        animator = GetComponentInChildren<Animator>();
        sr       = GetComponentInChildren<SpriteRenderer>();

        if (movement == null) Debug.LogError("PlayerMovement não achado no Player!");
        if (animator == null) Debug.LogError("Animator não achado no SpriteObject!");
        if (sr == null)       Debug.LogError("SpriteRenderer não achado no SpriteObject!");
    }

    void Update()
    {
        if (movement == null || animator == null || sr == null) return;

        // Em 2.5D lateral não existe mais direção por ângulo — só espelha o sprite.
        sr.flipX = !movement.facingRight;

        string stateName;
        if (movement.isJumping)      stateName = "Jump";
        else if (movement.isRunning && movement.isMoving) stateName = "Run";
        else if (movement.isMoving)  stateName = "Walk";
        else                         stateName = "Idle";

        animator.Play(stateName);
    }
}
