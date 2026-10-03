using ScreenShot.Helper;
using UnityEditor;
using UnityEngine;

namespace ScreenShot.ResizeTools
{
    public class ResizeTool
    {
        protected GameViewSizeChanger Window;
        public string Label { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        /// <summary>
        /// Create Resize Tool
        /// </summary>
        /// <param name="label"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public ResizeTool( string label, int width, int height)
        {
            Label = label;
            Width = width;
            Height = height;
        }
        /// <summary>
        /// Public Resize function
        /// </summary>
        /// <param name="width">width</param>
        /// <param name="height">height</param>
        public virtual void Resize(int width, int height)
        {
            ResizeImple(width, height);
        }
        /// <summary>
        /// Protected Resize function
        /// </summary>
        /// <param name="width">width</param>
        /// <param name="height">height</param>
        protected void ResizeImple(int width , int height)
        {
            var groupType = ResizeToolsHelper.GetCurrentGameViewSizeGroupType();
            var gameViewSize = new ResizeToolsHelper.GameViewSize();
            gameViewSize.type = ResizeToolsHelper.GameViewSizeType.FixedResolution;
            gameViewSize.width = width;
            gameViewSize.height = height;
            gameViewSize.baseText = Label;

            if (!ResizeToolsHelper.Contains(groupType, gameViewSize))
            {
                ResizeToolsHelper.AddCustomSize(groupType, gameViewSize);
            }

            ResizeToolsHelper.ChangeGameViewSize(groupType, gameViewSize);
            Save.Set<string>(GameViewSizeChanger.Key_LastLabel, Label);

            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    var flag = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    var assembly = typeof(Editor).Assembly;
                    var type = assembly.GetType("UnityEditor.GameView");
                    var gameView = EditorWindow.GetWindow(type, false, "Game", false);
                    var minScaleProperty = type.GetProperty("minScale", flag);
                    float minScale = (float)minScaleProperty.GetValue(gameView, null);
                    type.GetMethod("SnapZoom", flag, null, new System.Type[] { typeof(float) }, null).Invoke(gameView, new object[] { minScale });
                    gameView.Repaint();
                    Window?.Focus();
                };
            };
        }
    }
}
