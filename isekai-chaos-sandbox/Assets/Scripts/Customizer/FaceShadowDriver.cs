using UnityEngine;

namespace IsekaiChaos.Customizer
{
    /// <summary>
    /// Feeds the head bone's forward/right vectors to the AnimeToon face material every frame
    /// so the SDF face shadow rotates with the head (smooth anime nose/cheek shadows).
    /// Uses a MaterialPropertyBlock (no material instance). This takes the face renderer out of
    /// the SRP Batcher, which is negligible: one small draw per visible character.
    /// </summary>
    [ExecuteAlways]
    public class FaceShadowDriver : MonoBehaviour
    {
        static readonly int FaceForwardId = Shader.PropertyToID("_FaceForward");
        static readonly int FaceRightId = Shader.PropertyToID("_FaceRight");

        [SerializeField] Transform headBone;
        [SerializeField] Renderer faceRenderer;
        [Tooltip("Head bone axes vary by rig; fix them here.")]
        [SerializeField] Vector3 localForward = Vector3.forward;
        [SerializeField] Vector3 localRight = Vector3.right;

        MaterialPropertyBlock _mpb;

        void LateUpdate()
        {
            if (!headBone || !faceRenderer) return;
            _mpb ??= new MaterialPropertyBlock();
            faceRenderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(FaceForwardId, headBone.TransformDirection(localForward).normalized);
            _mpb.SetVector(FaceRightId, headBone.TransformDirection(localRight).normalized);
            faceRenderer.SetPropertyBlock(_mpb);
        }
    }
}
