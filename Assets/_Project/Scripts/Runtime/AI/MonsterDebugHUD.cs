using HorrorGame.Player;
using UnityEngine;

namespace HorrorGame.AI
{
    /// <summary>
    /// Press F3 in Play mode to show what every monster is thinking: state, awareness, whether it can
    /// see you, distance, and the last noise it heard. For testing and tuning; not part of the game UI.
    /// </summary>
    public class MonsterDebugHUD : MonoBehaviour
    {
        [SerializeField] bool visible;

        PlayerInputReader input;
        DetectableTarget self;
        GUIStyle style;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            self = GetComponent<DetectableTarget>();
        }

        void Update()
        {
            if (input != null && input.DebugTogglePressed)
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };

            var text = new System.Text.StringBuilder();
            text.AppendLine("<b>Monster debug (F3)</b>");
            if (self != null)
                text.AppendLine($"You: {(self.IsLit ? "<color=yellow>LIT</color>" : "dark")}" +
                                (self.Health != null ? $"   health {self.Health.Current:0}" : ""));
            foreach (var brain in MonsterBrain.All)
            {
                string distance = self != null ? $"{Vector3.Distance(brain.transform.position, self.transform.position):0.0} m" : "?";
                string sees = self != null && brain.CanSee(self, out _) ? "<color=red>SEES YOU</color>" : "can't see you";
                text.AppendLine($"{brain.DisplayName}: <b>{brain.State}</b> ({brain.TimeInState:0.0}s)   " +
                                $"awareness {brain.Awareness:0.00}   {sees}   {distance}");
                if (brain.LastHeard.HasValue)
                    text.AppendLine($"   last heard: {brain.LastHeard.Value.Radius:0} m noise at {brain.LastHeard.Value.Position:F0}");
            }
            GUI.Label(new Rect(12f, 12f, 900f, 400f), text.ToString(), style);
        }
    }
}
