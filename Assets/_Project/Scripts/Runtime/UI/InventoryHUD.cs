using System.Text;
using HorrorGame.Items;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>
    /// Bottom-left readout of carried scrap: the item names and the total value.
    /// Hidden while the player carries nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class InventoryHUD : MonoBehaviour
    {
        [SerializeField] int fontSize = 22;
        [SerializeField] Color textColor = new Color(0.92f, 0.92f, 0.88f);
        [SerializeField] Color totalColor = new Color(0.55f, 0.95f, 0.6f);

        static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        PlayerInventory inventory;
        GameObject canvasObject;
        Text label;
        readonly StringBuilder builder = new StringBuilder();

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            canvasObject = HudFactory.CreateCanvas("InventoryHUD", transform, 9);
            label = HudFactory.CreateText("Carried", canvasObject.transform, fontSize, TextAnchor.LowerLeft);
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.color = textColor;
            HudFactory.Place(label.rectTransform, BottomLeft, new Vector2(40f, 40f), new Vector2(600f, 300f));
            Refresh();
        }

        void OnEnable()
        {
            inventory.Changed += Refresh;
            if (canvasObject != null)
                canvasObject.SetActive(true);
        }

        void OnDisable()
        {
            inventory.Changed -= Refresh;
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        void Refresh()
        {
            if (inventory.Items.Count == 0)
            {
                label.enabled = false;
                return;
            }
            builder.Clear();
            foreach (var item in inventory.Items)
                builder.Append(item.DisplayName).Append("  $").Append(item.Value).Append('\n');
            string total = ColorUtility.ToHtmlStringRGB(totalColor);
            builder.Append("<color=#").Append(total).Append(">Total  $").Append(inventory.TotalValue).Append("</color>");
            label.text = builder.ToString();
            label.enabled = true;
        }
    }
}
