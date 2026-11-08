namespace DemoLA.Data
{
    /// <summary>정적 데이터 경로 규칙 (CSV 파일 이름 = 테이블 타입 이름).</summary>
    public static class TablePaths
    {
        public const string CsvFolder = "Assets/_Project/Data/CSV";
        public const string TablesFolder = "Assets/_Project/Data/Tables";
        public const string TablesRootAsset = TablesFolder + "/DemoLATables.asset";
        public const string AddressableGroup = "DemoLA Data";

        public static string CsvPath(string InTypeName)
        {
            return $"{CsvFolder}/{InTypeName}.csv";
        }

        /// <summary>프로젝트 루트 기준 CSV 파일을 읽는다 (에디터 · 테스트 전용 — 빌드에는 CSV 가 없다).</summary>
        public static string ReadCsv(string InTypeName)
        {
            string path = CsvPath(InTypeName);
            return System.IO.File.Exists(path) == true ? System.IO.File.ReadAllText(path) : null;
        }
    }
}
