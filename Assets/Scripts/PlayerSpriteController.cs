using UnityEngine;

public class PlayerSpriteController : MonoBehaviour
{
    private PlayerMovement movement;
    private Animator       animator;
    private SpriteRenderer sr;

    private int  lastDirIndex = 0;
    private bool lastFlipX    = false;

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

        if (movement.isMoving)
            UpdateDirection(movement.moveDirection);

        string prefix = "Idle";
        if (movement.isJumping)      prefix = "Jump";
        else if (movement.isRunning) prefix = "Run";
        else if (movement.isMoving)  prefix = "Walk";

        string stateName = prefix + "_" + GetDirName(lastDirIndex);
        animator.Play(stateName);
        sr.flipX = lastFlipX;
    }

    string GetDirName(int index)
    {
        switch (index)
        {
            case 0:  return "Front";
            case 1:  return "DiagFront";
            case 2:  return "Side";
            case 3:  return "DiagBack";
            case 4:  return "Back";
            default: return "Front";
        }
    }

    void UpdateDirection(Vector3 dir)
    {
        float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        int sector = Mathf.RoundToInt(angle / 45f) % 8;

        switch (sector)
        {
            case 6: lastDirIndex = 0; lastFlipX = false; break;
            case 7: lastDirIndex = 1; lastFlipX = false; break;
            case 0: lastDirIndex = 2; lastFlipX = false; break;
            case 1: lastDirIndex = 3; lastFlipX = false; break;
            case 2: lastDirIndex = 4; lastFlipX = false; break;
            case 3: lastDirIndex = 3; lastFlipX = true;  break;
            case 4: lastDirIndex = 2; lastFlipX = true;  break;
            case 5: lastDirIndex = 1; lastFlipX = true;  break;
        }
    }
}