using System.Collections;
using IsekaiChaos.Economy;
using UnityEngine;

namespace IsekaiChaos.Combat
{
    /// <summary>
    /// Comedic, non-punishing defeat: fade out, wake up in the Guild Infirmary, lose a slice of
    /// carried gold and receive an itemised "rescue fee" (plus a mocking line from whichever
    /// companion carried you back). Defeat never triggers romance/intimacy content.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDefeatHandler : MonoBehaviour
    {
        [SerializeField] Transform infirmarySpawn;
        [SerializeField, Range(0, 0.5f)] float goldLossFraction = 0.1f;
        [SerializeField] long rescueFee = 50;
        [SerializeField] float fadeSeconds = 1.5f;

        public System.Action<string> ShowToast;          // wired to the HUD

        Health _health;

        void Awake()
        {
            _health = GetComponent<Health>();
            _health.Died += () => StartCoroutine(DefeatRoutine());
        }

        IEnumerator DefeatRoutine()
        {
            yield return new WaitForSeconds(fadeSeconds);
            var eco = EconomyManager.Instance;
            if (eco)
            {
                long loss = (long)(eco.Gold * goldLossFraction);
                if (loss > 0) eco.AddGold(-loss, "Dropped while unconscious");
                eco.IssueFine(rescueFee, "Guild rescue squad (stretcher rental, emotional damages)");
            }

            var cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            if (infirmarySpawn) transform.SetPositionAndRotation(infirmarySpawn.position, infirmarySpawn.rotation);
            if (cc) cc.enabled = true;

            _health.Revive(0.5f);
            ShowToast?.Invoke("You wake up in the Guild Infirmary. The bill is already on your pillow.");
        }
    }
}
