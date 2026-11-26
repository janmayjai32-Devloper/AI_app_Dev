using IsekaiChaos.Combat;
using UnityEngine;

namespace IsekaiChaos.UI
{
    /// <summary>Ground decals for ability aiming: range ring, AoE circle, skillshot/dash line.</summary>
    public class AimIndicator : MonoBehaviour
    {
        [SerializeField] Transform rangeRing;
        [SerializeField] Transform aoeCircle;
        [SerializeField] Transform lineQuad;    // unit-length quad pivoted at one end

        public void Show(AbilityDefinition a, Vector3 origin, Vector3 point)
        {
            gameObject.SetActive(true);
            Vector3 lift = Vector3.up * 0.05f;
            rangeRing.position = origin + lift;
            rangeRing.localScale = Vector3.one * a.range * 2f;

            bool aoe = a.targeting == TargetingMode.GroundAoE;
            bool line = a.targeting == TargetingMode.Skillshot || a.targeting == TargetingMode.Dash;
            aoeCircle.gameObject.SetActive(aoe);
            lineQuad.gameObject.SetActive(line);

            if (aoe)
            {
                aoeCircle.position = point + lift;
                aoeCircle.localScale = Vector3.one * Mathf.Max(0.5f, a.aoeRadius) * 2f;
            }
            if (line)
            {
                Vector3 d = point - origin; d.y = 0;
                float len = a.targeting == TargetingMode.Dash ? a.shapeSize : a.range;
                lineQuad.position = origin + lift;
                lineQuad.rotation = Quaternion.LookRotation(d.sqrMagnitude > 0.01f ? d : Vector3.forward);
                lineQuad.localScale = new Vector3(a.shapeSize, 1, len);
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
