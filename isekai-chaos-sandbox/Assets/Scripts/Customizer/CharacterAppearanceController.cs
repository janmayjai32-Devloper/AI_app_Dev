using System;
using System.Collections.Generic;
using IsekaiChaos.SaveSystem;
using UnityEngine;

namespace IsekaiChaos.Customizer
{
    /// <summary>
    /// Applies avatar-creator sliders to blendshapes / bone scales, colors to material
    /// properties (via MaterialPropertyBlock — no material instancing, keeps SRP batching),
    /// and toggles race parts (ears, tails, horns, wings).
    /// </summary>
    public class CharacterAppearanceController : MonoBehaviour
    {
        [Serializable]
        public class MorphBinding
        {
            public string sliderId;                     // "height", "build", "muscle", "bust", "jaw", ...
            [Tooltip("Blendshape driven 0..100 by the slider (optional).")]
            public string blendShape;
            [Tooltip("Bone scaled by the slider (optional), e.g. root for height.")]
            public Transform bone;
            public Vector3 boneScaleAtMin = Vector3.one;
            public Vector3 boneScaleAtMax = Vector3.one;
        }

        [Serializable]
        public class ColorBinding
        {
            public string channel;                      // "skin", "hair", "eyes", "fur", "furPattern"
            public string shaderProperty = "_BaseColor";
            public Renderer[] renderers;
        }

        [Serializable]
        public class RacePart
        {
            public string partId;                       // "ears_wolf", "tail_fox", "horns_demon", ...
            public GameObject root;
        }

        [SerializeField] SkinnedMeshRenderer[] morphTargets;
        [SerializeField] List<MorphBinding> morphs = new List<MorphBinding>();
        [SerializeField] List<ColorBinding> colors = new List<ColorBinding>();
        [SerializeField] List<RacePart> raceParts = new List<RacePart>();

        AppearanceState _state = new AppearanceState();
        MaterialPropertyBlock _mpb;

        public AppearanceState State => _state;

        public void SetMorph(string sliderId, float value01)
        {
            value01 = Mathf.Clamp01(value01);
            _state.morphs[sliderId] = value01;
            foreach (var b in morphs)
            {
                if (b.sliderId != sliderId) continue;
                if (!string.IsNullOrEmpty(b.blendShape))
                    foreach (var smr in morphTargets)
                    {
                        int idx = smr.sharedMesh ? smr.sharedMesh.GetBlendShapeIndex(b.blendShape) : -1;
                        if (idx >= 0) smr.SetBlendShapeWeight(idx, value01 * 100f);
                    }
                if (b.bone) b.bone.localScale = Vector3.Lerp(b.boneScaleAtMin, b.boneScaleAtMax, value01);
            }
        }

        public void SetColor(string channel, Color c)
        {
            _state.colors[channel] = "#" + ColorUtility.ToHtmlStringRGBA(c);
            _mpb ??= new MaterialPropertyBlock();
            foreach (var b in colors)
            {
                if (b.channel != channel) continue;
                int id = Shader.PropertyToID(b.shaderProperty);
                foreach (var r in b.renderers)
                {
                    if (!r) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(id, c);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }

        public void SetRacePart(string category, string partId)
        {
            switch (category)
            {
                case "ears": _state.earsId = partId; break;
                case "tail": _state.tailId = partId; break;
                case "horns": _state.hornsId = partId; break;
                case "wings": _state.wingsId = partId; break;
            }
            RefreshRaceParts();
        }

        void RefreshRaceParts()
        {
            foreach (var p in raceParts)
                if (p.root)
                    p.root.SetActive(p.partId == _state.earsId || p.partId == _state.tailId ||
                                     p.partId == _state.hornsId || p.partId == _state.wingsId);
        }

        public void ApplyState(AppearanceState s)
        {
            _state = s ?? new AppearanceState();
            foreach (var kv in new Dictionary<string, float>(_state.morphs)) SetMorph(kv.Key, kv.Value);
            foreach (var kv in new Dictionary<string, string>(_state.colors))
                if (ColorUtility.TryParseHtmlString(kv.Value, out var c)) SetColor(kv.Key, c);
            RefreshRaceParts();
        }
    }
}
