using UnityEngine;

// Optional for 5B.1. In 5B.2 add Animator triggers with the exact names used here.
[DisallowMultipleComponent]
public class GraveWardenAnimatorBridge5B1 : MonoBehaviour
{
    public Animator animator;
    public bool logMissingTriggers = false;
    private int walkingHash = Animator.StringToHash("IsMoving");
    private int phaseTwoHash = Animator.StringToHash("IsPhaseTwo");

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public void Trigger(string triggerName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        int hash = Animator.StringToHash(triggerName);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(hash);
                return;
            }
        }
        if (logMissingTriggers) Debug.Log("[GraveWarden Animator] Add Trigger: " + triggerName, this);
    }

    public void SetWalking(bool walking, bool phase2)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == walkingHash && parameter.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(walkingHash, walking);
            else if (parameter.nameHash == phaseTwoHash && parameter.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(phaseTwoHash, phase2);
        }
    }
}
