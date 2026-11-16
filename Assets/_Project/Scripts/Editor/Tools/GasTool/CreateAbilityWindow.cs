using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DemoLA.Boss;
using DemoLA.Core;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// "새 Ability" 창: 보스 · 템플릿 · 패턴 태그 · 이름 · 페이즈를 고르면 패턴 에셋을 만든다 (원본 CreateAbilityWindow 의 DemoLA 판).
    /// 패턴 ID 는 GameplayTag(ETag) 다:
    ///  - 이미 정의돼 있고 아직 에셋이 없는 Pattern 태그를 고르거나,
    ///  - "새 태그" 에 이름을 적으면 GameTags.cs 의 그 관문 블록 끝에 ETag 항목을 추가하고(코드 생성), 재컴파일 뒤 이 창의 내용으로 패턴을 이어서 만든다.
    /// 에셋은 Data/Authored/Boss/Patterns 에 만들어 전체 셋업이 지우지 않고, 셋업 때 보스 · 페이즈 목록에 다시 들어간다.
    /// </summary>
    public class CreateAbilityWindow : EditorWindow
    {
        public const string PatternFolder = GasToolWindow.AuthoredRoot + "/Boss/Patterns";
        public const string GameTagsPath = "Assets/_Project/Scripts/Runtime/Core/GameTags.cs";
        private const string PendingKey = "DemoLA.CreateAbility.Pending";

        private GasToolWindow Owner;
        private int BossIndex;
        private AbilityTemplates.EKind Kind = AbilityTemplates.EKind.FrontBackCounterSlam;
        private bool NewTag = true;
        private ETag ExistingTag = ETag.None;
        private string NewTagName = "NewPattern";
        private string DisplayName = "새 패턴";
        private int PhaseIndex;

        /// <summary>재컴파일 뒤 이어서 만들 패턴 (SessionState 에 JSON 으로 보관).</summary>
        [Serializable]
        private class Pending
        {
            public string BossId;
            public string Tag;
            public int Kind;
            public string Name;
            public string PhaseId;
        }

        public static void Open(GasToolWindow InOwner)
        {
            var window = GetWindow<CreateAbilityWindow>(true, "새 Ability", true);
            window.Owner = InOwner;
            window.minSize = new Vector2(460.0f, 260.0f);
        }

        [InitializeOnLoadMethod]
        private static void resumePending()
        {
            string json = SessionState.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json) == true)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                SessionState.EraseString(PendingKey);
                var pending = JsonUtility.FromJson<Pending>(json);
                ETag tag;
                var database = AssetDatabase.LoadAssetAtPath<DemoLA.Data.GameDatabase>(DemoLAProjectSetup.DatabasePath);
                var boss = database != null ? database.GetBoss(pending.BossId) : null;
                if (boss == null || GameTags.TryParse(pending.Tag, out tag) == false)
                {
                    Debug.LogError($"[GAS Tool] 새 태그 {pending.Tag} 로 패턴을 만들지 못했습니다 (컴파일 오류가 있으면 고친 뒤 다시 시도하세요)");
                    return;
                }

                var pattern = Create(boss, (AbilityTemplates.EKind)pending.Kind, tag, pending.Name, pending.PhaseId);
                GasToolWindow.OpenPattern(pattern.PatternTag);
            };
        }

        private void OnGUI()
        {
            var database = AssetDatabase.LoadAssetAtPath<DemoLA.Data.GameDatabase>(DemoLAProjectSetup.DatabasePath);
            if (database == null)
            {
                EditorGUILayout.HelpBox("GameDatabase 없음", MessageType.Error);
                return;
            }

            if (EditorApplication.isCompiling == true)
            {
                EditorGUILayout.HelpBox("컴파일 중… 새 태그가 등록되면 패턴을 이어서 만듭니다.", MessageType.Info);
                return;
            }

            var bosses = database.Bosses.Where(b => b != null).ToList();
            BossIndex = EditorGUILayout.Popup("보스", Mathf.Clamp(BossIndex, 0, bosses.Count - 1), bosses.Select(b => b.DisplayName).ToArray());
            var boss = bosses[BossIndex];
            var gate = BossGate(boss);
            Kind = (AbilityTemplates.EKind)EditorGUILayout.Popup("템플릿", (int)Kind, AbilityTemplates.Labels);

            EditorGUILayout.Space(4.0f);
            NewTag = GUILayout.Toolbar(NewTag ? 0 : 1, new[] { "새 태그 추가", "정의된 태그 사용" }) == 0;
            string tagText;
            string problem = null;
            if (NewTag == true)
            {
                NewTagName = EditorGUILayout.TextField(new GUIContent("태그 이름", "영문 PascalCase (예: CrossSlam). GameTags.cs 에 ETag 항목으로 추가된다"), NewTagName);
                tagText = gate != ETag.None ? $"{GameTags.ToTagString(gate)}.{NewTagName}" : "(보스 관문 태그를 모름)";
                EditorGUILayout.LabelField("GameplayTag", tagText, EditorStyles.boldLabel);
                ETag existing;
                if (gate == ETag.None)
                {
                    problem = "이 보스의 패턴 관문 태그(Pattern.G1 / Pattern.G2)를 알 수 없습니다. 기존 패턴이 하나 이상 있어야 합니다.";
                }
                else if (Regex.IsMatch(NewTagName ?? string.Empty, "^[A-Z][A-Za-z0-9]*$") == false)
                {
                    problem = "태그 이름은 영문 대문자로 시작하는 PascalCase (밑줄 · 점 없이) 여야 합니다.";
                }
                else if (GameTags.TryParse(tagText, out existing) == true)
                {
                    problem = $"{tagText} 는 이미 있는 태그입니다 — '정의된 태그 사용' 에서 고르세요.";
                }
            }
            else
            {
                ExistingTag = (ETag)PatternTagDrawer.Popup(EditorGUILayout.GetControlRect(), new GUIContent("패턴 태그"), ExistingTag);
                tagText = GameTags.ToTagString(ExistingTag);
                if (GameTags.IsPattern(ExistingTag) == false)
                {
                    problem = "패턴 태그를 고르세요.";
                }
                else if (GameTags.PatternGate(ExistingTag) != gate)
                {
                    problem = $"{tagText} 는 이 보스의 관문({GameTags.ToTagString(gate)}) 태그가 아닙니다.";
                }
                else if (boss.GetPattern(ExistingTag) != null)
                {
                    problem = $"{tagText} 패턴은 이미 있습니다.";
                }
            }

            DisplayName = EditorGUILayout.TextField("이름", DisplayName);
            var phases = boss.Phases.Select(p => $"{p.PhaseId} {p.DisplayName}").Prepend("(페이즈 목록에 넣지 않음 — StateTree 로만 사용)").ToArray();
            PhaseIndex = EditorGUILayout.Popup(new GUIContent("랜덤 패턴 후보 페이즈"), Mathf.Clamp(PhaseIndex, 0, phases.Length - 1), phases);

            if (problem != null)
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(problem != null))
            {
                if (GUILayout.Button(NewTag ? "태그 추가 후 만들기 (재컴파일)" : "만들기", GUILayout.Height(28.0f)) == true)
                {
                    string phaseId = PhaseIndex > 0 ? boss.Phases[PhaseIndex - 1].PhaseId : string.Empty;
                    if (NewTag == true)
                    {
                        addTagAndResume(boss, gate, NewTagName, phaseId);
                    }
                    else
                    {
                        var pattern = Create(boss, Kind, ExistingTag, DisplayName, phaseId);
                        GasToolWindow.OpenPattern(pattern.PatternTag);
                    }

                    Close();
                }
            }
        }

        /// <summary>보스 패턴들의 관문 태그 (Pattern_G1 / Pattern_G2).</summary>
        public static ETag BossGate(BossDefinition InBoss)
        {
            foreach (var pattern in InBoss.Patterns)
            {
                var gate = pattern != null ? GameTags.PatternGate(pattern.PatternTag) : ETag.None;
                if (gate != ETag.None)
                {
                    return gate;
                }
            }

            return ETag.None;
        }

        private void addTagAndResume(BossDefinition InBoss, ETag InGate, string InName, string InPhaseId)
        {
            string enumName = $"{InGate}_{InName}";
            if (AddPatternTag(InGate, enumName, DisplayName) == false)
            {
                return;
            }

            var pending = new Pending { BossId = InBoss.BossId, Tag = enumName, Kind = (int)Kind, Name = DisplayName, PhaseId = InPhaseId };
            SessionState.SetString(PendingKey, JsonUtility.ToJson(pending));
            AssetDatabase.ImportAsset(GameTagsPath);
        }

        /// <summary>
        /// GameTags.cs 의 ETag 에서 InGate 블록(Pattern_G1_*) 마지막 항목 뒤에 새 항목을 넣는다. 값 = 블록 최댓값 + 1.
        /// 블록 범위(G1 = 211~249 / G2 = 251~299)를 넘으면 실패.
        /// </summary>
        public static bool AddPatternTag(ETag InGate, string InEnumName, string InComment)
        {
            string text = File.ReadAllText(GameTagsPath);
            var entries = Regex.Matches(text, $@"^(?<indent>[ \t]*){InGate}_\w+ = (?<value>\d+),[^\n]*\n", RegexOptions.Multiline);
            if (entries.Count == 0)
            {
                Debug.LogError($"[GAS Tool] {GameTagsPath} 에서 {InGate} 블록을 찾지 못했습니다");
                return false;
            }

            var last = entries.Cast<Match>().OrderBy(m => int.Parse(m.Groups["value"].Value)).Last();
            int value = int.Parse(last.Groups["value"].Value) + 1;
            int blockEnd = (int)InGate == (int)ETag.Pattern_G1 ? 249 : 299;
            if (value > blockEnd || Enum.IsDefined(typeof(ETag), value) == true)
            {
                Debug.LogError($"[GAS Tool] {InGate} 블록에 남은 값이 없습니다 ({value}). GameTags.cs 에서 직접 범위를 정하세요");
                return false;
            }

            string indent = last.Groups["indent"].Value;
            string newline = text.Contains("\r\n") == true ? "\r\n" : "\n";
            string comment = string.IsNullOrWhiteSpace(InComment) == true ? string.Empty : $"{indent}/// <summary>{InComment.Replace("<", "(").Replace(">", ")")} (GAS Tool 추가).</summary>{newline}";
            int insertAt = last.Index + last.Length;
            text = text.Insert(insertAt, $"{comment}{indent}{InEnumName} = {value},{newline}");
            File.WriteAllText(GameTagsPath, text);
            Debug.Log($"[GAS Tool] 새 태그 {InEnumName} = {value} → {GameTagsPath}");
            return true;
        }

        public static BossPatternDefinition Create(BossDefinition InBoss, AbilityTemplates.EKind InKind, ETag InPatternTag, string InName, string InPhaseId)
        {
            DemoLAProjectSetup.ensureFolder(PatternFolder);
            var pattern = CreateInstance<BossPatternDefinition>();
            pattern.PatternTag = InPatternTag;
            pattern.DisplayName = InName;
            pattern.PhaseId = InPhaseId;
            pattern.Notes = "GAS Tool 로 만든 패턴";
            pattern.ToolEdited = true;
            AbilityTemplates.Fill(pattern, InKind);
            AssetDatabase.CreateAsset(pattern, AssetDatabase.GenerateUniqueAssetPath($"{PatternFolder}/PT_{GameTags.PatternAssetName(InPatternTag)}.asset"));

            Undo.RecordObject(InBoss, "새 패턴");
            InBoss.Patterns.Add(pattern);
            var phase = InBoss.GetPhase(InPhaseId);
            if (phase != null && phase.PatternTags.Contains(InPatternTag) == false)
            {
                phase.PatternTags.Add(InPatternTag);
            }

            EditorUtility.SetDirty(InBoss);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GAS Tool] 새 패턴 {pattern.TagName} '{InName}' ({AbilityTemplates.Labels[(int)InKind]}) → {AssetDatabase.GetAssetPath(pattern)}");
            return pattern;
        }
    }
}
