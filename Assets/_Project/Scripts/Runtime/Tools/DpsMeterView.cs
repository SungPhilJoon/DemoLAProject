using System.Linq;
using System.Text;
using DemoLA.Player;
using DemoLA.UI;
using Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DemoLA.Tools
{
    /// <summary>F8 딜 미터 창 (파티원별 · 스킬별). 0.5초마다 갱신. 초기화 버튼으로 구간을 다시 잰다.</summary>
    public class DpsMeterView : BaseBehaviour
    {
        private Canvas canvas;
        private Text body;
        private float refreshTimer;
        private readonly StringBuilder builder = new StringBuilder();

        public void Build()
        {
            if (canvas != null)
            {
                return;
            }

            DpsMeter.Enable();
            canvas = UIKit.CreateCanvas("DpsMeter", 18);
            canvas.transform.SetParent(transform, false);
            var panel = UIKit.Anchored(canvas.transform, "Panel", new Vector2(1.0f, 1.0f), new Vector2(-20.0f, -180.0f), new Vector2(620.0f, 520.0f));
            panel.pivot = new Vector2(1.0f, 1.0f);
            UIKit.Image(panel, UIKit.PanelColor, true);
            body = UIKit.Label(UIKit.Stretch(panel, "Body", 12.0f), string.Empty, 15, TextAnchor.UpperLeft, Color.white);
            var reset = UIKit.Button(panel, "초기화", 16, DpsMeter.Reset);
            var rect = (RectTransform)reset.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1.0f, 1.0f);
            rect.pivot = new Vector2(1.0f, 1.0f);
            rect.anchoredPosition = new Vector2(-10.0f, -10.0f);
            rect.sizeDelta = new Vector2(90.0f, 30.0f);
            canvas.gameObject.SetActive(false);
            PlayerController.OnDpsMeterToggle += Toggle;
        }

        protected override void OnUnityDestroy()
        {
            PlayerController.OnDpsMeterToggle -= Toggle;
        }

        public void Toggle()
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(canvas.gameObject.activeSelf == false);
            }
        }

        private void Update()
        {
            if (canvas == null || canvas.gameObject.activeSelf == false)
            {
                return;
            }

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0.0f)
            {
                return;
            }

            refreshTimer = 0.5f;
            builder.Length = 0;
            builder.AppendLine("딜 미터 (F8)");
            foreach (var actor in DpsMeter.All.OrderByDescending(a => a.Total))
            {
                builder.AppendLine($"<b>{actor.Name}</b>  {actor.Total:N0}  ·  초당 {actor.Dps:N0}");
                foreach (var skill in actor.Skills.Values.OrderByDescending(s => s.Total).Take(6))
                {
                    float share = actor.Total > 0.0f ? skill.Total / actor.Total * 100.0f : 0.0f;
                    float crit = skill.Hits > 0 ? skill.Crits * 100.0f / skill.Hits : 0.0f;
                    builder.AppendLine($"   {skill.Name}  {share:0.#}%  치명 {crit:0}%  최대 {skill.MaxHit:N0}");
                }
            }

            body.supportRichText = true;
            body.text = builder.ToString();
        }
    }
}
