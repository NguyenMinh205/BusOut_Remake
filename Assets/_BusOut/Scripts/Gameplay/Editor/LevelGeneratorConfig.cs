using UnityEngine;

namespace BusOut.Gameplay.Editor
{
    /// Số của bộ sinh level. Level Builder tự tạo asset khi chưa có; chỉnh trong Inspector.
    public class LevelGeneratorConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/_BusOut/Data/LevelGeneratorConfig.asset";

        [Tooltip("Mỗi phần tử là một mức khó, thứ tự hiện trong danh sách của Level Builder.")]
        public LevelGeneratorProfile[] profiles = LevelGeneratorProfile.CreateDefaults();
    }
}
