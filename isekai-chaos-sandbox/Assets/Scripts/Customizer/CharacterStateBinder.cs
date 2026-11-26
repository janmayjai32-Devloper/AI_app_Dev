using System.Linq;
using IsekaiChaos.Combat;
using IsekaiChaos.SaveSystem;
using UnityEngine;

namespace IsekaiChaos.Customizer
{
    /// <summary>
    /// Bridges a character's appearance, wardrobe and vitals into the save file.
    /// Leave npcId empty for the player; set it for companions (who are customizable too).
    /// </summary>
    public class CharacterStateBinder : MonoBehaviour, ISaveable
    {
        [SerializeField] string npcId;
        [SerializeField] CharacterAppearanceController appearance;
        [SerializeField] LayeredWardrobeController wardrobe;
        [SerializeField] Health health;

        public int RestoreOrder => 15;
        bool IsPlayer => string.IsNullOrEmpty(npcId);

        void OnEnable() => SaveLoadManager.Register(this);
        void OnDisable() => SaveLoadManager.Unregister(this);

        public void CaptureState(SaveData data)
        {
            if (IsPlayer)
            {
                if (appearance) data.player.appearance = appearance.State;
                if (wardrobe) data.player.wardrobe = wardrobe.CaptureState();
                if (health) data.player.hp = health.Hp;
                return;
            }
            var npc = data.npcs.FirstOrDefault(n => n.npcId == npcId);
            if (npc == null) { npc = new NpcSaveState { npcId = npcId }; data.npcs.Add(npc); }
            if (appearance) npc.appearance = appearance.State;
            if (wardrobe) npc.wardrobe = wardrobe.CaptureState();
            var p = transform.position;
            npc.position = new[] { p.x, p.y, p.z };
        }

        public void RestoreState(SaveData data)
        {
            if (IsPlayer)
            {
                if (appearance) appearance.ApplyState(data.player.appearance);
                if (wardrobe) wardrobe.RestoreState(data.player.wardrobe);
                if (health && data.player.hp > 0) health.SetHp(data.player.hp);
                return;
            }
            var npc = data.npcs.FirstOrDefault(n => n.npcId == npcId);
            if (npc == null) return;
            if (appearance) appearance.ApplyState(npc.appearance);
            if (wardrobe) wardrobe.RestoreState(npc.wardrobe);
            if (npc.position is { Length: 3 }) transform.position = new Vector3(npc.position[0], npc.position[1], npc.position[2]);
        }
    }
}
