using Cysharp.Threading.Tasks;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Content;
using DemoLA.Data;
using DemoLA.Save;
using Framework;

namespace DemoLA.Core
{
    /// <summary>
    /// 게임 세션 (Managers 에 등록되는 서비스). 정의 데이터와 저장 데이터를 들고 있다.
    /// </summary>
    public class GameSession : IManager
    {
        public GameDatabase Database { get; private set; }
        public SaveData Save { get; private set; }
        public ISaveService SaveService { get; private set; }

        public GameSession(GameDatabase InDatabase, ISaveService InSaveService)
        {
            Database = InDatabase;
            SaveService = InSaveService;
        }

        void IManager.Initialize()
        {
        }

        void IManager.UnInitialize()
        {
        }

        void IManager.Update(float InDeltaTime)
        {
        }

        public async UniTask LoadAsync()
        {
            Save = SaveService != null ? await SaveService.LoadAsync() : null;
            if (Save == null)
            {
                Save = new SaveData();
            }

            EnsureBuilds();
        }

        public UniTask SaveAsync()
        {
            return SaveService != null ? SaveService.SaveAsync(Save) : UniTask.CompletedTask;
        }

        /// <summary>4개 직업 모두 기본 빌드(최대 강화/각인/카드/보석/스킬)를 가진다.</summary>
        public void EnsureBuilds()
        {
            foreach (ECharacterClass cls in System.Enum.GetValues(typeof(ECharacterClass)))
            {
                if (Save.GetBuild(cls) == null && Database.GetClass(cls) != null)
                {
                    Save.Builds.Add(DefaultContent.CreateDefaultBuild(Database, cls));
                }
            }
        }

        public CharacterBuildData GetBuild(ECharacterClass InClass)
        {
            var build = Save.GetBuild(InClass);
            if (build == null)
            {
                build = DefaultContent.CreateDefaultBuild(Database, InClass);
                Save.Builds.Add(build);
            }

            return build;
        }

        public void ResetBuild(ECharacterClass InClass)
        {
            var existing = Save.GetBuild(InClass);
            if (existing != null)
            {
                Save.Builds.Remove(existing);
            }

            Save.Builds.Add(DefaultContent.CreateDefaultBuild(Database, InClass));
        }
    }
}
