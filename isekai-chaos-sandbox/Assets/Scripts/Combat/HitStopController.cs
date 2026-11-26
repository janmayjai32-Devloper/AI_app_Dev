using System.Collections;
using UnityEngine;

namespace IsekaiChaos.Combat
{
    /// <summary>
    /// Local-only hit-stop: briefly freezes the attacker's and victim's animators (not global
    /// timeScale, which would also freeze UI tweens, physics-driven hair/cloth and other fights).
    /// Attach to any character with a CombatController.
    /// </summary>
    [RequireComponent(typeof(CombatController))]
    public class HitStopController : MonoBehaviour
    {
        [SerializeField] Animator animator;
        CombatController _combat;
        Coroutine _routine;

        void Awake()
        {
            _combat = GetComponent<CombatController>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            _combat.HitLanded += OnHit;
        }

        void OnHit(Health victim, AttackDefinition atk, Vector3 point)
        {
            if (atk.hitStop <= 0) return;
            Freeze(animator, atk.hitStop);
            var va = victim.GetComponentInChildren<Animator>();
            if (va) victim.StartCoroutine(FreezeRoutine(va, atk.hitStop));
        }

        void Freeze(Animator a, float seconds)
        {
            if (_routine != null) { StopCoroutine(_routine); a.speed = 1f; }
            _routine = StartCoroutine(FreezeRoutine(a, seconds));
        }

        static IEnumerator FreezeRoutine(Animator a, float seconds)
        {
            a.speed = 0.02f;
            yield return new WaitForSecondsRealtime(seconds);
            if (a) a.speed = 1f;
        }
    }
}
