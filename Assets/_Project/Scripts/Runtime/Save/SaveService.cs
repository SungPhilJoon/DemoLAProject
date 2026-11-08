using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using DemoLA.Build;
using DemoLA.Character;
using UnityEngine;

namespace DemoLA.Save
{
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public List<CharacterBuildData> Builds = new List<CharacterBuildData>();
        public ECharacterClass PlayerClass = ECharacterClass.Slayer;
        public string SelectedBoss = "Gate1";
        public bool SpawnPartyAI = true;

        public CharacterBuildData GetBuild(ECharacterClass InClass)
        {
            for (int i = 0; i < Builds.Count; ++i)
            {
                if (Builds[i] != null && Builds[i].Class == InClass)
                {
                    return Builds[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 저장소 추상화. 지금은 로컬 JSON, 이후 PlayFab 구현으로 교체한다 (프로젝트 초기 설정.md).
    /// </summary>
    public interface ISaveService
    {
        UniTask<SaveData> LoadAsync();
        UniTask SaveAsync(SaveData InData);
    }

    public class LocalJsonSaveService : ISaveService
    {
        private readonly string path;

        public LocalJsonSaveService(string InFileName = "demola_save.json")
        {
            path = Path.Combine(Application.persistentDataPath, InFileName);
        }

        public string FilePath
        {
            get { return path; }
        }

        public async UniTask<SaveData> LoadAsync()
        {
            if (File.Exists(path) == false)
            {
                return null;
            }

            try
            {
                string json = await File.ReadAllTextAsync(path);
                // 이후 호출자가 Unity API 를 쓰므로 메인 스레드로 확실히 돌아온다.
                await UniTask.SwitchToMainThread();
                var data = JsonUtility.FromJson<SaveData>(json);
                return Migrate(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 읽기 실패, 새로 만든다: {e.Message}");
                return null;
            }
        }

        public async UniTask SaveAsync(SaveData InData)
        {
            if (InData == null)
            {
                return;
            }

            string json = JsonUtility.ToJson(InData, true);
            string temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, json);
            await UniTask.SwitchToMainThread();
            // 쓰기 도중 종료돼도 원본이 깨지지 않게 임시 파일을 바꿔치기한다.
            if (File.Exists(path) == true)
            {
                File.Delete(path);
            }

            File.Move(temp, path);
        }

        public static SaveData Migrate(SaveData InData)
        {
            if (InData == null)
            {
                return null;
            }

            // 버전이 올라가면 여기서 변환한다.
            InData.Version = SaveData.CurrentVersion;
            return InData;
        }
    }

    /// <summary>테스트/에디터용 메모리 저장소.</summary>
    public class MemorySaveService : ISaveService
    {
        private string json;

        public UniTask<SaveData> LoadAsync()
        {
            return UniTask.FromResult(string.IsNullOrEmpty(json) == true ? null : JsonUtility.FromJson<SaveData>(json));
        }

        public UniTask SaveAsync(SaveData InData)
        {
            json = JsonUtility.ToJson(InData);
            return UniTask.CompletedTask;
        }
    }
}
