// Requires UniVRM (VRM 1.0 package, com.vrmc.vrm). After importing it, add ICS_UNIVRM to
// Project Settings ▸ Player ▸ Scripting Define Symbols. Without the define this file compiles
// to nothing, so the project builds before UniVRM is installed.
#if ICS_UNIVRM
using System.Collections;
using IsekaiChaos.Combat;
using UniVRM10;
using UnityEngine;

namespace IsekaiChaos.Customizer
{
    public enum Emotion { Neutral, Happy, Angry, Sad, Surprised, Relaxed, Pain }

    /// <summary>
    /// Drives VRM facial expressions from gameplay: hurt face on hits, happy on combo finishers,
    /// surprised on dodges, blinking, plus timed emotes for dialogue and comedy beats.
    /// Blends smoothly; one emotion at a time + independent blink.
    /// </summary>
    [RequireComponent(typeof(Vrm10Instance))]
    public class VrmExpressionController : MonoBehaviour
    {
        [SerializeField] float blendSpeed = 8f;
        [SerializeField] Vector2 blinkInterval = new Vector2(2.5f, 5.5f);
        [SerializeField] Health health;
        [SerializeField] CombatController combat;

        Vrm10Instance _vrm;
        Emotion _current = Emotion.Neutral, _target = Emotion.Neutral;
        float _weight, _revertAt = -1f;
        bool _blinkSuppressed;

        void Awake()
        {
            _vrm = GetComponent<Vrm10Instance>();
            if (!health) health = GetComponentInParent<Health>();
            if (!combat) combat = GetComponentInParent<CombatController>();
            if (health) { health.Damaged += _ => Show(Emotion.Pain, 0.6f); health.Died += () => Show(Emotion.Sad, 999f); }
            if (combat) combat.HitLanded += (_, atk, __) => { if (atk.reaction >= HitReaction.Knockdown) Show(Emotion.Happy, 1f); };
            StartCoroutine(BlinkLoop());
        }

        /// <summary>Show an emotion for a duration (seconds), then return to neutral.</summary>
        public void Show(Emotion e, float duration = 2f)
        {
            _target = e;
            _revertAt = Time.time + duration;
        }

        void LateUpdate()
        {
            if (_revertAt > 0 && Time.time > _revertAt) { _target = Emotion.Neutral; _revertAt = -1f; }

            // Fade out the old emotion before fading in the new one.
            if (_current != _target)
            {
                _weight = Mathf.MoveTowards(_weight, 0f, blendSpeed * Time.deltaTime);
                if (_weight <= 0f) { SetWeight(_current, 0f); _current = _target; }
            }
            else _weight = Mathf.MoveTowards(_weight, _current == Emotion.Neutral ? 0f : 1f, blendSpeed * Time.deltaTime);

            SetWeight(_current, _weight);
            _blinkSuppressed = _current == Emotion.Happy || _current == Emotion.Pain;  // those presets close the eyes
        }

        void SetWeight(Emotion e, float w)
        {
            var key = KeyFor(e);
            if (key.HasValue) _vrm.Runtime.Expression.SetWeight(key.Value, w);
        }

        static ExpressionKey? KeyFor(Emotion e) => e switch
        {
            Emotion.Happy => ExpressionKey.Happy,
            Emotion.Angry => ExpressionKey.Angry,
            Emotion.Sad => ExpressionKey.Sad,
            Emotion.Surprised => ExpressionKey.Surprised,
            Emotion.Relaxed => ExpressionKey.Relaxed,
            Emotion.Pain => ExpressionKey.Angry,           // VRoid has no "pain"; angry + squint reads well
            _ => null,
        };

        IEnumerator BlinkLoop()
        {
            var blink = ExpressionKey.Blink;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(blinkInterval.x, blinkInterval.y));
                if (_blinkSuppressed) continue;
                for (float t = 0; t < 1; t += Time.deltaTime / 0.06f) { _vrm.Runtime.Expression.SetWeight(blink, t); yield return null; }
                for (float t = 1; t > 0; t -= Time.deltaTime / 0.1f) { _vrm.Runtime.Expression.SetWeight(blink, t); yield return null; }
                _vrm.Runtime.Expression.SetWeight(blink, 0);
            }
        }
    }
}
#endif
